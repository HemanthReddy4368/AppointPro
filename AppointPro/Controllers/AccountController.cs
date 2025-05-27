using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppointPro.Data;
using AppointPro.Models;
using System.Security.Claims;
using BCrypt.Net;

namespace AppointPro.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == model.Email && u.Role == model.Role);

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid login attempt.");
                return View(model);
            }

            // Check if user has a password (might be a Google user)
            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                ModelState.AddModelError("", "This account doesn't have a password. Please use Google login.");
                return View(model);
            }

            // Verify the password
            if (!BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Invalid login attempt.");
                return View(model);
            }

            // Create claims
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Name, user.Name),
        new Claim(ClaimTypes.Role, user.Role.ToString())
    };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // Redirect based on role
            if (user.Role == UserRole.Doctor)
            {
                return RedirectToAction("Index", "DoctorDashboard");
            }
            else if (user.Role == UserRole.SystemAdmin)
            {
                return RedirectToAction("Index", "Admin");
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if email already exists
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "Email already in use");
                    return View(model);
                }

                // Hash the password
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password);

                var user = new ApplicationUser
                {
                    Email = model.Email,
                    Name = model.Name,
                    PasswordHash = hashedPassword,
                    Role = model.Role,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // If the user is a doctor, create a doctor record
                if (model.Role == UserRole.Doctor)
                {
                    // You might want to redirect to a form to collect more doctor information
                    return RedirectToAction("CreateDoctorProfile", new { userId = user.UserId });
                }

                return RedirectToAction("Login");
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties { RedirectUri = Url.Action("GoogleResponse") };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet]
        public async Task<IActionResult> GoogleResponse()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!result.Succeeded)
                return RedirectToAction("Login");

            var googleUser = result.Principal;

            var email = googleUser.FindFirst(ClaimTypes.Email)?.Value;
            var name = googleUser.FindFirst(ClaimTypes.Name)?.Value;
            var googleId = googleUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Check if user exists
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                // Create new user
                user = new ApplicationUser
                {
                    Email = email,
                    Name = name,
                    GoogleId = googleId,
                    CreatedAt = DateTime.UtcNow,
                    // Role is automatically set to UserRole.Patient due to the default value in the model
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }

            // Update claims if needed
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, user.Role.ToString()), // Add the role claim
                new Claim("GoogleId", user.GoogleId ?? "")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectToAction("Index", "Home");
        }

        // Optional: Method to create doctor profile after registration
        [HttpGet]
        public IActionResult CreateDoctorProfile(int userId)
        {
            ViewBag.UserId = userId;
            ViewBag.Hospitals = _context.Hospitals.ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDoctorProfile(Doctor doctor, int userId)
        {
            if (ModelState.IsValid)
            {
                // Get the user
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound();
                }

                // Set the doctor's email to match the user's email
                doctor.Email = user.Email;

                _context.Doctors.Add(doctor);
                await _context.SaveChangesAsync();

                return RedirectToAction("Login");
            }

            ViewBag.UserId = userId;
            ViewBag.Hospitals = _context.Hospitals.ToList();
            return View(doctor);
        }

        public async Task<IActionResult> Profile()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
                return NotFound();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ApplicationUser model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null)
                return NotFound();

            user.Name = model.Name;
            user.PhoneNumber = model.PhoneNumber;
            user.Address = model.Address;
            user.DateOfBirth = model.DateOfBirth;
            user.Gender = model.Gender;
            // Add more fields as needed

            await _context.SaveChangesAsync();
            ViewBag.Success = true;
            return View(user);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}