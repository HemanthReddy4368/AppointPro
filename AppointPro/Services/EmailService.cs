using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace AppointPro.Services
{
    public interface IEmailService
    {
        Task SendAppointmentConfirmationAsync(string recipientEmail, string patientName,
            string doctorName, DateTime appointmentDate, string hospitalName);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendAppointmentConfirmationAsync(string recipientEmail, string patientName,
            string doctorName, DateTime appointmentDate, string hospitalName)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            var host = smtpSettings["Host"];
            var port = int.Parse(smtpSettings["Port"]);
            var username = smtpSettings["Username"];
            var password = smtpSettings["Password"];
            var senderEmail = smtpSettings["SenderEmail"];
            var senderName = smtpSettings["SenderName"];

            var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };

            var message = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = "Appointment Confirmation - AppointPro",
                Body = GetAppointmentConfirmationTemplate(patientName, doctorName, appointmentDate, hospitalName),
                IsBodyHtml = true
            };
            message.To.Add(recipientEmail);

            await client.SendMailAsync(message);
        }

        private string GetAppointmentConfirmationTemplate(string patientName, string doctorName,
            DateTime appointmentDate, string hospitalName)
        {
            return $@"
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
                        <h2>Appointment Confirmation</h2>
                    </div>
                    <div class='content'>
                        <p>Dear {patientName},</p>
                        <p>Your appointment has been successfully booked with the following details:</p>
                        <ul>
                            <li><strong>Doctor:</strong> {doctorName}</li>
                            <li><strong>Hospital:</strong> {hospitalName}</li>
                            <li><strong>Date & Time:</strong> {appointmentDate.ToString("dddd, MMMM d, yyyy")} at {appointmentDate.ToString("h:mm tt")}</li>
                        </ul>
                        <p>Please arrive 15 minutes before your scheduled appointment time.</p>
                        <p>If you need to reschedule or cancel your appointment, please log in to your account or contact us at least 24 hours in advance.</p>
                        <p>Thank you for choosing our services!</p>
                        <p>Best regards,<br>AppointPro Team</p>
                    </div>
                    <div class='footer'>
                        <p>This is an automated message. Please do not reply to this email.</p>
                    </div>
                </div>
            </body>
            </html>";
        }
    }
}