using System.ComponentModel.DataAnnotations;

namespace TimeEvidence.Models
{
    public class CardAssignment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        [StringLength(20)]
        public string CardId { get; set; } = string.Empty;
        
        [Required]
        public Guid EmployeeId { get; set; }
        
        public DateTime AssignedAt { get; set; } = DateTime.Now;
        
        [StringLength(50)]
        public string? AssignedBy { get; set; }
        
        public DateTime? UnassignedAt { get; set; }
        
        [StringLength(50)]
        public string? UnassignedBy { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        public Employee? Employee { get; set; }
    }
}