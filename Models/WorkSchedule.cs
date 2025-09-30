using System.ComponentModel.DataAnnotations;

namespace TimeEvidence.Models
{
    public class WorkSchedule
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(100)]
        public string ScheduleName { get; set; } = string.Empty;

        // Days of week (stored as comma-separated values)
        public string SelectedDays { get; set; } = string.Empty;

        // Time ranges (stored as JSON string)
        public string TimeRanges { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();

        // Computed properties
        public List<DayOfWeek> GetSelectedDays()
        {
            if (string.IsNullOrWhiteSpace(SelectedDays))
                return new List<DayOfWeek>();

            var result = new List<DayOfWeek>();
            foreach (var token in SelectedDays.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var part = token.Trim();
                if (Enum.TryParse<DayOfWeek>(part, ignoreCase: true, out var byName))
                {
                    result.Add(byName);
                    continue;
                }
                if (int.TryParse(part, out var num) && Enum.IsDefined(typeof(DayOfWeek), num))
                {
                    result.Add((DayOfWeek)num);
                }
            }
            return result.Distinct().ToList();
        }

        public void SetSelectedDays(List<DayOfWeek> days)
        {
            // Store as names for readability and compatibility
            SelectedDays = string.Join(',', days.Select(d => d.ToString()));
        }

        public List<TimeRange> GetTimeRanges()
        {
            var ranges = new List<TimeRange>();
            if (string.IsNullOrWhiteSpace(TimeRanges))
                return ranges;

            // Try JSON first
            try
            {
                var deserialized = System.Text.Json.JsonSerializer.Deserialize<List<TimeRange>>(TimeRanges);
                if (deserialized != null && deserialized.Count > 0)
                    return deserialized;
            }
            catch
            {
                // ignore and fall back to delimited parsing
            }

            // Fallback: support formats like "09:00:00-17:00:00" or multiple ranges separated by ';' or ','
            var parts = TimeRanges.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                // also try comma-separated ranges
                parts = TimeRanges.Split(new[] { ';', '|', ',' }, StringSplitOptions.RemoveEmptyEntries);
            }

            foreach (var raw in parts)
            {
                var s = raw.Trim();
                var split = s.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (split.Length == 2 &&
                    TimeSpan.TryParse(split[0], out var start) &&
                    TimeSpan.TryParse(split[1], out var end))
                {
                    ranges.Add(new TimeRange { StartTime = start, EndTime = end });
                }
            }
            return ranges;
        }

        public void SetTimeRanges(List<TimeRange> ranges)
        {
            TimeRanges = System.Text.Json.JsonSerializer.Serialize(ranges);
        }
    }

    public class TimeRange
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public string DisplayText => $"{StartTime:hh\\:mm} - {EndTime:hh\\:mm}";
    }
}