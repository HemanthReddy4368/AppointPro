using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AppointPro.Models
{
    public class Doctor
    {
        [Key]
        public int DoctorId { get; set; }

        [Required]
        public int HospitalId { get; set; }

        [ForeignKey("HospitalId")]
        public Hospital Hospital { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; }

        [Phone]
        [StringLength(20)]
        public string PhoneNumber { get; set; }

        [StringLength(100)]
        public string Specialization { get; set; }

        [StringLength(255)]
        public string Qualifications { get; set; }

        [StringLength(255)]
        public string LanguagesSpoken { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ConsultationFee { get; set; }

        [Range(0, 5)]
        public double? Rating { get; set; }

        [StringLength(255)]
        public string ProfilePicture { get; set; }

        public string Bio { get; set; }

        [Required]
        [Display(Name = "Start Time")]
        [DataType(DataType.Time)]
        public TimeSpan StartTime { get; set; } = new TimeSpan(8, 30, 0); // 8:30 AM default

        [Required]
        [Display(Name = "End Time")]
        [DataType(DataType.Time)]
        public TimeSpan EndTime { get; set; } = new TimeSpan(15, 30, 0); // 3:30 PM default

        [Required]
        [Display(Name = "Slots Per Day")]
        [Range(1, 20)]
        public int SlotsPerDay { get; set; } = 7; // Default: 7 slots (1 hour each)

        [Required]
        [Display(Name = "Slot Duration (minutes)")]
        [Range(15, 120)]
        public int SlotDurationMinutes { get; set; } = 60; // Default: 60 minutes
    }
}