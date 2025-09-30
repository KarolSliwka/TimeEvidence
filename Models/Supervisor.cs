using System.ComponentModel.DataAnnotations;

namespace TimeEvidence.Models
{
    public enum NotificationPreference
    {
        None = 0,
        Email = 1,
        Sms = 2
    }

    public class Supervisor
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Surname { get; set; } = string.Empty;

        [StringLength(50)]
        public string Position { get; set; } = "Manager";

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Phone]
        [StringLength(30)]
        public string? PhoneNumber { get; set; }

        [Required]
        public NotificationPreference NotificationPreference { get; set; } = NotificationPreference.None;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();

        // Computed properties
        public string FullName => $"{Name} {Surname}";
    }
}