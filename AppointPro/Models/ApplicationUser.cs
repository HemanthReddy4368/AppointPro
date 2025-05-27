using System.ComponentModel.DataAnnotations;

namespace AppointPro.Models
{
    public enum UserRole
    {
        Patient,
        Doctor,
        SystemAdmin
    }

    public class ApplicationUser
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        public string Name { get; set; }

        public string? ProfilePicture { get; set; }

        public string? GoogleId { get; set; }

        // Add PasswordHash property
        public string? PasswordHash { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public UserRole Role { get; set; } = UserRole.Patient; // Default role is Patient

        // Additional fields you might want
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
    }
}