using AppointPro.Models;
using Microsoft.EntityFrameworkCore;

namespace AppointPro.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<ApplicationUser> Users { get; set; }
        public DbSet<Hospital> Hospitals { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Payment> Payments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Add unique constraint for Email
            modelBuilder.Entity<ApplicationUser>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Add unique constraint for Doctor Email
            modelBuilder.Entity<Doctor>()
                .HasIndex(d => d.Email)
                .IsUnique();

            // Configure the relationship between Doctor and Hospital
            modelBuilder.Entity<Doctor>()
                .HasOne(d => d.Hospital)
                .WithMany()
                .HasForeignKey(d => d.HospitalId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete

            // Configure the relationship between Payment and Appointment
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Appointment)
                .WithMany()
                .HasForeignKey(p => p.AppointmentId)
                .OnDelete(DeleteBehavior.SetNull); // This allows the appointment to be deleted
        }
    }
}