using AppointPro.Data;
using AppointPro.Models;
using AppointPro.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AppointPro.Controllers
{
    [Authorize]
    public class AppointmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public AppointmentController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // GET: Appointment
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var appointments = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Hospital) // Include the Hospital navigation property
                .Include(a => a.Patient)
                .Where(a => a.PatientId == int.Parse(userId))
                .ToListAsync();
            return View(appointments);
        }

        // GET: Appointment/Create
        public IActionResult Create()
        {
            // Fetch hospitals and doctors for the view
            ViewBag.Hospitals = _context.Hospitals.ToList();
            ViewBag.Doctors = _context.Doctors
                .Include(d => d.Hospital) // Make sure Hospital is included
                .ToList();
            return View();
        }

        // POST: Appointment/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int hospitalId, int doctorId, string timeSlot, string notes)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (ModelState.IsValid)
            {
                // Parse the selected time slot
                DateTime slotDateTime;
                if (!DateTime.TryParse(timeSlot, out slotDateTime))
                {
                    ModelState.AddModelError("", "Invalid time slot selected");
                    ViewBag.Hospitals = _context.Hospitals.ToList();
                    ViewBag.Doctors = _context.Doctors.Include(d => d.Hospital).ToList();
                    return View();
                }

                // Instead of creating the appointment directly, redirect to payment
                return RedirectToAction("Checkout", "Payment", new
                {
                    doctorId = doctorId,
                    appointmentDateTime = timeSlot,
                    notes = notes
                });
            }

            // If model state is not valid, repopulate hospitals and doctors
            ViewBag.Hospitals = _context.Hospitals.ToList();
            ViewBag.Doctors = _context.Doctors
                .Include(d => d.Hospital)
                .ToList();
            return View();
        }

        // GET: Appointment/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Hospital)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appointment == null)
            {
                return NotFound();
            }

            ViewBag.Hospitals = _context.Hospitals.ToList();
            ViewBag.Doctors = _context.Doctors
                .Include(d => d.Hospital)
                .ToList();
            return View(appointment);
        }

        // POST: Appointment/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("AppointmentId,PatientId,DoctorId,AppointmentDate,Notes,Status")] Appointment appointment)
        {
            if (id != appointment.AppointmentId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Preserve the original CreatedAt date
                    var originalAppointment = await _context.Appointments.AsNoTracking()
                        .FirstOrDefaultAsync(a => a.AppointmentId == id);
                    appointment.CreatedAt = originalAppointment.CreatedAt;

                    _context.Update(appointment);
                    await _context.SaveChangesAsync();

                    // Send update notification if status changed
                    if (originalAppointment.Status != appointment.Status)
                    {
                        await SendAppointmentUpdateEmail(appointment.AppointmentId, appointment.Status);
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AppointmentExists(appointment.AppointmentId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Hospitals = _context.Hospitals.ToList();
            ViewBag.Doctors = _context.Doctors
                .Include(d => d.Hospital)
                .ToList();
            return View(appointment);
        }

        // GET: Appointment/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Hospital)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(m => m.AppointmentId == id);

            if (appointment == null)
            {
                return NotFound();
            }

            return View(appointment);
        }

        // POST: Appointment/Delete/5
        // POST: Appointment/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var appointment = await _context.Appointments
                        .Include(a => a.Doctor)
                            .ThenInclude(d => d.Hospital)
                        .Include(a => a.Patient)
                        .FirstOrDefaultAsync(a => a.AppointmentId == id);

                    if (appointment == null)
                    {
                        return NotFound();
                    }

                    // Find and update related payments
                    var relatedPayments = await _context.Payments
                        .Where(p => p.AppointmentId == id)
                        .ToListAsync();

                    foreach (var payment in relatedPayments)
                    {
                        payment.AppointmentId = null;
                        _context.Update(payment);
                    }

                    await _context.SaveChangesAsync();

                    // Mark as cancelled instead of deleting
                    appointment.Status = AppointmentStatus.Cancelled;
                    _context.Update(appointment);
                    await _context.SaveChangesAsync();

                    // Send cancellation email
                    await SendAppointmentCancellationEmail(appointment);

                    await transaction.CommitAsync();
                    TempData["SuccessMessage"] = "Appointment cancelled successfully.";

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = $"Error cancelling appointment: {ex.Message}";
                    return RedirectToAction(nameof(Index));
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableSlots(int doctorId, DateTime date)
        {
            // Validate input
            if (doctorId <= 0 || date.Date < DateTime.Today)
            {
                return BadRequest("Invalid doctor ID or date");
            }

            // Get doctor with their working hours
            var doctor = await _context.Doctors.FindAsync(doctorId);
            if (doctor == null)
            {
                return NotFound("Doctor not found");
            }

            // Calculate all possible slots for this doctor on this date
            var startTime = doctor.StartTime;
            var endTime = doctor.EndTime;
            var slotDuration = TimeSpan.FromMinutes(doctor.SlotDurationMinutes);

            var slots = new List<DateTime>();
            var currentSlot = date.Date.Add(startTime);
            var dayEnd = date.Date.Add(endTime);

            // Generate all possible slots for this day
            while (currentSlot.Add(slotDuration) <= dayEnd)
            {
                slots.Add(currentSlot);
                currentSlot = currentSlot.Add(slotDuration);
            }

            // Get already booked slots for this doctor on this date
            var bookedSlots = await _context.Appointments
                .Where(a => a.DoctorId == doctorId &&
                           a.AppointmentDate.Date == date.Date &&
                           a.Status != AppointmentStatus.Cancelled)
                .Select(a => a.AppointmentDate)
                .ToListAsync();

            // Remove booked slots from available slots
            var availableSlots = slots
                .Where(s => !bookedSlots.Any(b =>
                    b >= s && b < s.Add(slotDuration)))
                .ToList();

            // Limit to doctor's max slots per day
            int bookedCount = bookedSlots.Count;
            int remainingSlots = doctor.SlotsPerDay - bookedCount;
            if (remainingSlots < availableSlots.Count)
            {
                availableSlots = availableSlots.Take(remainingSlots).ToList();
            }

            // Format slots for display
            var formattedSlots = availableSlots.Select(s => new
            {
                dateTime = s.ToString("yyyy-MM-ddTHH:mm:ss"),
                displayTime = $"{s.ToString("h:mm tt")} - {s.Add(slotDuration).ToString("h:mm tt")}"
            });

            return Json(formattedSlots);
        }

        // Helper method to send appointment update email
        private async Task SendAppointmentUpdateEmail(int appointmentId, AppointmentStatus newStatus)
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
                    return;
                }

                string subject = $"Appointment Status Updated - {newStatus}";
                string body = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #4CAF50; color: white; padding: 10px; text-align: center; }}
                        .content {{ padding: 20px; border: 1px solid #ddd; }}
                        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #777; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>Appointment Update</h2>
                        </div>
                        <div class='content'>
                            <p>Dear {appointment.Patient.Name},</p>
                            <p>Your appointment status has been updated to: <strong>{newStatus}</strong></p>
                            <p>Appointment details:</p>
                            <ul>
                                <li><strong>Doctor:</strong> {appointment.Doctor.Name}</li>
                                <li><strong>Hospital:</strong> {appointment.Doctor.Hospital.Name}</li>
                                <li><strong>Date & Time:</strong> {appointment.AppointmentDate.ToString("dddd, MMMM d, yyyy")} at {appointment.AppointmentDate.ToString("h:mm tt")}</li>
                            </ul>
                            <p>If you have any questions, please contact our support team.</p>
                            <p>Thank you for choosing our services!</p>
                            <p>Best regards,<br>AppointPro Team</p>
                        </div>
                        <div class='footer'>
                            <p>This is an automated message. Please do not reply to this email.</p>
                        </div>
                    </div>
                </body>
                </html>";

                // Send email using the email service
                // This is a simplified example - you might want to create a more specific method in your email service
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
                Console.WriteLine($"Error sending update email: {ex.Message}");
            }
        }

        // Helper method to send appointment cancellation email
        private async Task SendAppointmentCancellationEmail(Appointment appointment)
        {
            try
            {
                string subject = "Appointment Cancelled";
                string body = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #f44336; color: white; padding: 10px; text-align: center; }}
                        .content {{ padding: 20px; border: 1px solid #ddd; }}
                        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #777; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>Appointment Cancelled</h2>
                        </div>
                        <div class='content'>
                            <p>Dear {appointment.Patient.Name},</p>
                            <p>Your appointment has been cancelled:</p>
                            <ul>
                                <li><strong>Doctor:</strong> {appointment.Doctor.Name}</li>
                                <li><strong>Hospital:</strong> {appointment.Doctor.Hospital.Name}</li>
                                <li><strong>Date & Time:</strong> {appointment.AppointmentDate.ToString("dddd, MMMM d, yyyy")} at {appointment.AppointmentDate.ToString("h:mm tt")}</li>
                            </ul>
                            <p>If you would like to reschedule, please log in to your account and book a new appointment.</p>
                            <p>Thank you for choosing our services!</p>
                            <p>Best regards,<br>AppointPro Team</p>
                        </div>
                        <div class='footer'>
                            <p>This is an automated message. Please do not reply to this email.</p>
                        </div>
                    </div>
                </body>
                </html>";

                // Send email using the email service
                // This is a simplified example - you might want to create a more specific method in your email service
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
                Console.WriteLine($"Error sending cancellation email: {ex.Message}");
            }
        }

        private bool AppointmentExists(int id)
        {
            return _context.Appointments.Any(e => e.AppointmentId == id);
        }
    }
}