using System.ComponentModel.DataAnnotations;

namespace TimeEvidence.Models
{
    public class Employee
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string Surname { get; set; } = string.Empty;
        
        [StringLength(50)]
        public string Position { get; set; } = "Tester";
        
        public Guid? SupervisorId { get; set; }
        public Supervisor? Supervisor { get; set; }
        
        public Guid? WorkScheduleId { get; set; }
        public WorkSchedule? WorkSchedule { get; set; }
        
        [StringLength(20)]
        public string? CardId { get; set; }
        
        public bool Access { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        
        // Computed properties
        public string FullName => $"{Name} {Surname}";
        public string AccessStatus => Access ? "Authorized" : "Unauthorized";
        public bool IsCardAssigned => !string.IsNullOrEmpty(CardId);
    }
}