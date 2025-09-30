using System.Text.Json.Serialization;

namespace TimeEvidence.Models
{
    public class TimeTrackerData
    {
        public int Id { get; set; }
        
        [JsonPropertyName("system_id")]
        public string? SystemId { get; set; }
        
        [JsonPropertyName("action")]
        public string? Action { get; set; }
        
        [JsonPropertyName("card_id")]
        public string? CardId { get; set; }
        
        [JsonPropertyName("status")]
        public string? Status { get; set; }
        
        [JsonPropertyName("timestamp_iso")]
        public string? TimestampIso { get; set; }
        
        [JsonPropertyName("timestamp_local")]
        public string? TimestampLocal { get; set; }
        
        [JsonPropertyName("active_sessions")]
        public int? ActiveSessions { get; set; }
        
        [JsonPropertyName("system_uptime")]
        public long? SystemUptime { get; set; }
        
        [JsonPropertyName("wifi_connected")]
        public bool? WifiConnected { get; set; }
        
        // Auto-generated fields
        public DateTime ReceivedTimestamp { get; set; } = DateTime.Now;
        
        // Employee assignment fields
        public Guid? AssignedEmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? AccessLevel { get; set; } // "Authorized", "Unauthorized", "Unknown"
        
        // Computed properties
        public DateTime? ParsedTimestamp => 
            DateTime.TryParse(TimestampIso, out var dt) ? dt : null;
            
        public string UptimeFormatted => 
            SystemUptime.HasValue ? TimeSpan.FromSeconds(SystemUptime.Value).ToString(@"dd\.hh\:mm\:ss") : "N/A";
            
        public bool IsAuthorized => AccessLevel == "Authorized";
    }
}