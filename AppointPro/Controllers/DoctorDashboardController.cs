using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppointPro.Data;
using AppointPro.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AppointPro.Controllers
{
    [Authorize(Roles = "Doctor")]
    public class DoctorDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DoctorDashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);

            // Find the doctor record associated with the logged-in user's email
            var doctor = await _context.Doctors
                .Include(d => d.Hospital)
                .FirstOrDefaultAsync(d => d.Email == userEmail);

            if (doctor == null)
            {
                // If no doctor record exists yet, redirect to create one
                return RedirectToAction("CreateProfile");
            }

            // Get upcoming appointments for this doctor
            var appointments = await _context.Appointments
                .Include(a => a.Patient)
                .Where(a => a.DoctorId == doctor.DoctorId && a.Status == AppointmentStatus.Scheduled)
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync();

            ViewBag.Doctor = doctor;
            return View(appointments);
        }

        [HttpGet]
        public IActionResult CreateProfile()
        {
            ViewBag.Hospitals = new SelectList(_context.Hospitals.ToList(), "HospitalId", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProfile(Doctor doctor, IFormFile ProfileImage, string[] WorkingDays)
        {
            // Add debugging information
            if (!ModelState.IsValid)
            {
                // Log validation errors
                foreach (var state in ModelState)
                {
                    foreach (var error in state.Value.Errors)
                    {
                        Console.WriteLine($"Error in {state.Key}: {error.ErrorMessage}");
                    }
                }
            }

            try
            {
                // Get the current user's email
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                doctor.Email = userEmail;

                // Handle profile image upload
                if (ProfileImage != null && ProfileImage.Length > 0)
                {
                    // Create a unique filename
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(ProfileImage.FileName);
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/doctors", fileName);

                    // Ensure directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                    // Save the file
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await ProfileImage.CopyToAsync(stream);
                    }

                    // Save the file path to the doctor record
                    doctor.ProfilePicture = "/images/doctors/" + fileName;
                }

                // Handle working days
                if (WorkingDays != null && WorkingDays.Length > 0)
                {
                    // Store working days as a JSON string or in a related table
                    // For now, we'll just log them
                    Console.WriteLine($"Working days: {string.Join(", ", WorkingDays)}");
                }

                // Add the doctor to the database
                _context.Doctors.Add(doctor);
                await _context.SaveChangesAsync();

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error creating doctor profile: {ex.Message}");
                ModelState.AddModelError("", "An error occurred while creating your profile. Please try again.");
            }

            // If we get here, something went wrong
            ViewBag.Hospitals = new SelectList(_context.Hospitals.ToList(), "HospitalId", "Name");
            return View(doctor);
        }

        public async Task<IActionResult> Appointments()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);

            // Find the doctor record associated with the logged-in user's email
            var doctor = await _context.Doctors
                .Include(d => d.Hospital)
                .FirstOrDefaultAsync(d => d.Email == userEmail);

            if (doctor == null)
            {
                // If no doctor record exists yet, redirect to create one
                return RedirectToAction("CreateProfile");
            }

            // Get all appointments for this doctor
            var appointments = await _context.Appointments
                .Include(a => a.Patient)
                .Where(a => a.DoctorId == doctor.DoctorId)
                .OrderByDescending(a => a.AppointmentDate)
                .ToListAsync();

            ViewBag.Doctor = doctor;
            return View(appointments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAppointmentNotes(int id, string notes)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appointment == null)
            {
                return NotFound();
            }

            // Verify this appointment belongs to the logged-in doctor
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (appointment.Doctor.Email != userEmail)
            {
                return Forbid();
            }

            appointment.Notes = notes;
            await _context.SaveChangesAsync();

            return RedirectToAction("Appointments");
        }

        public async Task<IActionResult> AppointmentDetails(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .ThenInclude(d => d.Hospital)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appointment == null)
            {
                return NotFound();
            }

            // Verify this appointment belongs to the logged-in doctor
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (appointment.Doctor.Email != userEmail)
            {
                return Forbid();
            }

            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAppointmentStatus(int id, AppointmentStatus status)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appointment == null)
            {
                return NotFound();
            }

            // Verify this appointment belongs to the logged-in doctor
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (appointment.Doctor.Email != userEmail)
            {
                return Forbid();
            }

            appointment.Status = status;
            await _context.SaveChangesAsync();

            return RedirectToAction("AppointmentDetails", new { id = id });
        }
    }
}