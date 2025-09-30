using System.Text.Json.Serialization;

namespace TimeEvidence.Models.DTOs
{
    public class ArduinoResponseDto
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
        
        [JsonPropertyName("access_granted")]
        public bool AccessGranted { get; set; }
        
        [JsonPropertyName("employee_name")]
        public string? EmployeeName { get; set; }
        
        [JsonPropertyName("employee_surname")]
        public string? EmployeeSurname { get; set; }
        
        [JsonPropertyName("position")]
        public string? Position { get; set; }
        
        [JsonPropertyName("access_level")]
        public string AccessLevel { get; set; } = "Unauthorized";
        
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        [JsonPropertyName("system_message")]
        public string? SystemMessage { get; set; }
    }
    
    public class CardAssignmentDto
    {
        public string CardId { get; set; } = string.Empty;
        public Guid EmployeeId { get; set; }
        public bool GrantAccess { get; set; } = true;
    }
}