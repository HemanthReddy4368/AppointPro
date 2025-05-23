using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AppointPro.Models;

namespace AppointPro.Models
{
    public class Doctor
    {
        [Key]
        public int DoctorId { get; set; }

        [Required]
        public int UserId { get; set; } // Link to ApplicationUser

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }

        [Required]
        public int HospitalId { get; set; }

        [ForeignKey("HospitalId")]
        public Hospital Hospital { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

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
    }
}