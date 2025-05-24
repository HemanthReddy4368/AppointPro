using AppointPro.Data;
using AppointPro.Models;
using AppointPro.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AppointPro.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly StripeSettings _stripeSettings;
        private readonly IEmailService _emailService;

        public PaymentController(
            ApplicationDbContext context,
            IOptions<StripeSettings> stripeSettings,
            IEmailService emailService)
        {
            _context = context;
            _stripeSettings = stripeSettings.Value;
            _emailService = emailService;
            // Ensure Stripe API key is set
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;
        }

        // GET: Payment/Checkout
        public async Task<IActionResult> Checkout(int doctorId, string appointmentDateTime, string notes)
        {
            try
            {
                // Validate inputs
                if (doctorId <= 0 || string.IsNullOrEmpty(appointmentDateTime))
                {
                    return BadRequest("Invalid appointment details");
                }

                // Parse appointment date time
                DateTime slotDateTime;
                if (!DateTime.TryParse(appointmentDateTime, out slotDateTime))
                {
                    return BadRequest("Invalid appointment time");
                }

                // Get doctor details including consultation fee
                var doctor = await _context.Doctors
                    .Include(d => d.Hospital)
                    .FirstOrDefaultAsync(d => d.DoctorId == doctorId);

                if (doctor == null)
                {
                    return NotFound("Doctor not found");
                }

                // Check if the slot is still available
                bool isSlotAvailable = await IsSlotAvailable(doctorId, slotDateTime);
                if (!isSlotAvailable)
                {
                    TempData["ErrorMessage"] = "This slot is no longer available. Please select another time.";
                    return RedirectToAction("Create", "Appointment");
                }

                // Create view model for checkout
                var model = new PaymentViewModel
                {
                    DoctorId = doctorId,
                    DoctorName = doctor.Name,
                    HospitalName = doctor.Hospital?.Name,
                    AppointmentDateTime = slotDateTime,
                    Amount = doctor.ConsultationFee ?? 0,
                    Notes = notes
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error preparing checkout: {ex.Message}";
                return RedirectToAction("Create", "Appointment");
            }
        }

        // POST: Payment/ProcessPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int doctorId, string appointmentDateTime, decimal amount, string notes)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                DateTime slotDateTime = DateTime.Parse(appointmentDateTime);

                // Get doctor details
                var doctor = await _context.Doctors
                    .Include(d => d.Hospital)
                    .FirstOrDefaultAsync(d => d.DoctorId == doctorId);

                if (doctor == null)
                {
                    return NotFound("Doctor not found");
                }

                // Create a temporary payment record
                var payment = new Payment
                {
                    PatientId = userId,
                    DoctorId = doctorId,
                    Amount = amount,
                    Status = PaymentStatus.Pending,
                    AppointmentDateTime = slotDateTime,
                    Notes = notes,
                    PaymentDate = DateTime.UtcNow,
                    TransactionId = "pending_" + Guid.NewGuid().ToString("N").Substring(0, 10)
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                // Create Stripe checkout session
                var options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card" },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                UnitAmount = (long)(amount * 100), // Stripe uses cents
                                Currency = "usd",
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = "Appointment with Dr. " + doctor.Name,
                                    Description = $"Appointment on {slotDateTime.ToString("MMM dd, yyyy h:mm tt")}"
                                }
                            },
                            Quantity = 1
                        }
                    },
                    Mode = "payment",
                    SuccessUrl = Url.Action("PaymentSuccess", "Payment", new { paymentId = payment.PaymentId }, Request.Scheme),
                    CancelUrl = Url.Action("PaymentCancel", "Payment", new { paymentId = payment.PaymentId }, Request.Scheme),
                    ClientReferenceId = payment.PaymentId.ToString()
                };

                var service = new SessionService();
                var session = await service.CreateAsync(options);

                // Redirect directly to Stripe's hosted checkout page
                return Redirect(session.Url);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Payment error: {ex.Message}";
                return RedirectToAction("Create", "Appointment");
            }
        }

        // GET: Payment/PaymentSuccess
        public async Task<IActionResult> PaymentSuccess(int paymentId)
        {
            try
            {
                var payment = await _context.Payments.FindAsync(paymentId);
                if (payment == null)
                {
                    return NotFound("Payment record not found");
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Update payment status
                payment.Status = PaymentStatus.Completed;
                payment.TransactionId = "tx_" + Guid.NewGuid().ToString("N").Substring(0, 16);

                // Create the appointment
                var appointment = new Appointment
                {
                    PatientId = int.Parse(userId),
                    DoctorId = payment.DoctorId,
                    AppointmentDate = payment.AppointmentDateTime,
                    Notes = payment.Notes,
                    CreatedAt = DateTime.UtcNow,
                    Status = AppointmentStatus.Scheduled
                };

                // Save both records in a transaction
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        _context.Appointments.Add(appointment);
                        await _context.SaveChangesAsync();

                        payment.AppointmentId = appointment.AppointmentId;
                        _context.Update(payment);
                        await _context.SaveChangesAsync();

                        await transaction.CommitAsync();

                        // Send confirmation email
                        await SendAppointmentConfirmationEmail(appointment.AppointmentId);

                        return RedirectToAction("Confirmation", new { id = payment.PaymentId });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        TempData["ErrorMessage"] = $"Error creating appointment: {ex.Message}";
                        return RedirectToAction("Index", "Home");
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error processing payment success: {ex.Message}";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: Payment/PaymentCancel
        public async Task<IActionResult> PaymentCancel(int paymentId)
        {
            try
            {
                var payment = await _context.Payments.FindAsync(paymentId);
                if (payment != null)
                {
                    payment.Status = PaymentStatus.Failed;
                    await _context.SaveChangesAsync();
                }

                TempData["ErrorMessage"] = "Payment was cancelled. Your appointment has not been booked.";
                return RedirectToAction("Create", "Appointment");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error processing payment cancellation: {ex.Message}";
                return RedirectToAction("Create", "Appointment");
            }
        }

        // GET: Payment/Confirmation/5
        public async Task<IActionResult> Confirmation(int id)
        {
            try
            {
                var payment = await _context.Payments
                    .Include(p => p.Appointment)
                    .ThenInclude(a => a.Doctor)
                    .ThenInclude(d => d.Hospital)
                    .FirstOrDefaultAsync(p => p.PaymentId == id);

                if (payment == null)
                {
                    return NotFound("Payment record not found");
                }

                return View(payment);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading confirmation: {ex.Message}";
                return RedirectToAction("Index", "Home");
            }
        }

        // Helper method to send appointment confirmation email
        private async Task SendAppointmentConfirmationEmail(int appointmentId)
        {
            try
            {
                var appointment = await _context.Appointments
                    .Include(a => a.Doctor)
                        .ThenInclude(d => d.Hospital)
                    .Include(a => a.Patient)
                    .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

                if (appointment == null)
                {
                    // Log error
                    return;
                }

                await _emailService.SendAppointmentConfirmationAsync(
                    appointment.Patient.Email,
                    appointment.Patient.Name,
                    appointment.Doctor.Name,
                    appointment.AppointmentDate,
                    appointment.Doctor.Hospital.Name);
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the transaction
                // Consider implementing a retry mechanism or queue
                Console.WriteLine($"Error sending confirmation email: {ex.Message}");
            }
        }

        // Helper method to check if a slot is available
        private async Task<bool> IsSlotAvailable(int doctorId, DateTime slotDateTime)
        {
            var doctor = await _context.Doctors.FindAsync(doctorId);
            if (doctor == null)
            {
                return false;
            }

            // Check if the slot is within doctor's working hours
            TimeSpan timeOfDay = slotDateTime.TimeOfDay;
            if (timeOfDay < doctor.StartTime || timeOfDay >= doctor.EndTime)
            {
                return false;
            }

            // Check if the slot is already booked
            var slotEndTime = slotDateTime.AddMinutes(doctor.SlotDurationMinutes);
            bool isBooked = await _context.Appointments
                .Where(a => a.DoctorId == doctorId &&
                           a.Status != AppointmentStatus.Cancelled &&
                           a.AppointmentDate < slotEndTime &&
                           a.AppointmentDate.AddMinutes(doctor.SlotDurationMinutes) > slotDateTime)
                .AnyAsync();

            if (isBooked)
            {
                return false;
            }

            // Check if doctor has reached max slots for the day
            int bookedSlotsCount = await _context.Appointments
                .Where(a => a.DoctorId == doctorId &&
                           a.AppointmentDate.Date == slotDateTime.Date &&
                           a.Status != AppointmentStatus.Cancelled)
                .CountAsync();

            return bookedSlotsCount < doctor.SlotsPerDay;
        }
    }

    // View model for the checkout page
    public class PaymentViewModel
    {
        public int DoctorId { get; set; }
        public string DoctorName { get; set; }
        public string HospitalName { get; set; }
        public DateTime AppointmentDateTime { get; set; }
        public decimal Amount { get; set; }
        public string Notes { get; set; }
    }
}