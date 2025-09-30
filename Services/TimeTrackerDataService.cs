using TimeEvidence.Models;
using TimeEvidence.Data;
using Microsoft.EntityFrameworkCore;

namespace TimeEvidence.Services
{
    public class TimeTrackerDataService
    {
        private readonly TimeEvidenceDbContext _context;
        private readonly EmployeeService _employeeService;
        private readonly NotificationService _notificationService;

        public event EventHandler<TimeTrackerData>? DataAdded;
        public event EventHandler? DataCleared;

        public TimeTrackerDataService(TimeEvidenceDbContext context, EmployeeService employeeService, NotificationService notificationService)
        {
            _context = context;
            _employeeService = employeeService;
            _notificationService = notificationService;
        }

        public IEnumerable<TimeTrackerData> GetAllData()
        {
            return _context.TimeTrackerData
                .OrderByDescending(d => d.ReceivedTimestamp)
                .Take(50)
                .ToList();
        }

        public TimeTrackerData? GetLatestData()
        {
            return _context.TimeTrackerData
                .OrderByDescending(d => d.ReceivedTimestamp)
                .FirstOrDefault();
        }

        public int GetDataCount()
        {
            return _context.TimeTrackerData.Count();
        }

        public IEnumerable<TimeTrackerData> GetDataByAction(string action)
        {
            if (string.IsNullOrWhiteSpace(action)) return Enumerable.Empty<TimeTrackerData>();
            return _context.TimeTrackerData
                .Where(d => d.Action != null && d.Action.ToLower() == action.ToLower())
                .OrderByDescending(d => d.ReceivedTimestamp)
                .Take(100)
                .ToList();
        }

        public IEnumerable<TimeTrackerData> GetDataBySystemId(string systemId)
        {
            if (string.IsNullOrWhiteSpace(systemId)) return Enumerable.Empty<TimeTrackerData>();
            return _context.TimeTrackerData
                .Where(d => d.SystemId == systemId)
                .OrderByDescending(d => d.ReceivedTimestamp)
                .Take(200)
                .ToList();
        }

        public int GetActiveSessionsCount()
        {
            return _context.TimeTrackerData
                .OrderByDescending(d => d.ReceivedTimestamp)
                .Take(500)
                .Where(d => d.ActiveSessions.HasValue)
                .Select(d => d.ActiveSessions!.Value)
                .FirstOrDefault();
        }

        public void AddData(TimeTrackerData data)
        {
            data.Id = GetNextId();
            data.ReceivedTimestamp = DateTime.Now;

            ProcessEmployeeAccess(data);

            _context.TimeTrackerData.Add(data);
            _context.SaveChanges();

            // After saving, trigger late login notifications if applicable
            TryNotifyLateLoginAsync(data).GetAwaiter().GetResult();
            // After saving, trigger early logout notifications if applicable
            TryNotifyEarlyLogoutAsync(data).GetAwaiter().GetResult();

            DataAdded?.Invoke(this, data);
        }

        private async Task TryNotifyLateLoginAsync(TimeTrackerData data)
        {
            if (!string.Equals(data.Action, "LOGIN", StringComparison.OrdinalIgnoreCase))
                return;

            if (string.IsNullOrWhiteSpace(data.CardId)) return;
            var employee = _employeeService.GetEmployeeByCardId(data.CardId);
            if (employee == null || employee.Supervisor == null) return;
            if (employee.WorkSchedule == null) return; // no schedule, no late notion

            // Use provided timestamp if available; otherwise ReceivedTimestamp
            var loginDateTime = data.ParsedTimestamp ?? data.ReceivedTimestamp;

            // Only consider if today is a scheduled working day
            var days = employee.WorkSchedule.GetSelectedDays();
            if (days.Count > 0 && !days.Contains(loginDateTime.DayOfWeek))
                return;

            var ranges = employee.WorkSchedule.GetTimeRanges();
            if (ranges == null || ranges.Count == 0) return;

            var today = loginDateTime;
            var startTimes = ranges.Select(r => r.StartTime).OrderBy(t => t).ToList();
            var firstStart = startTimes.FirstOrDefault();

            // If employee logged in after the earliest start time, they are late
            if (firstStart == default) return;

            var loginTime = today.TimeOfDay;
            if (loginTime > firstStart)
            {
                await _notificationService.SendLateArrivalNotificationAsync(employee, today);
            }
        }

        private int GetNextId()
        {
            var lastId = _context.TimeTrackerData
                .OrderByDescending(d => d.Id)
                .Select(d => d.Id)
                .FirstOrDefault();

            return lastId + 1;
        }

        private async Task TryNotifyEarlyLogoutAsync(TimeTrackerData data)
        {
            if (!string.Equals(data.Action, "LOGOUT", StringComparison.OrdinalIgnoreCase))
                return;

            if (string.IsNullOrWhiteSpace(data.CardId)) return;
            var employee = _employeeService.GetEmployeeByCardId(data.CardId);
            if (employee == null || employee.Supervisor == null) return;
            if (employee.WorkSchedule == null) return; // no schedule, no early logout notion

            var logoutDateTime = data.ParsedTimestamp ?? data.ReceivedTimestamp;

            // Only consider if today is a scheduled working day
            var workingDays = employee.WorkSchedule.GetSelectedDays();
            if (workingDays.Count > 0 && !workingDays.Contains(logoutDateTime.DayOfWeek))
                return;

            var ranges = employee.WorkSchedule.GetTimeRanges();
            if (ranges == null || ranges.Count == 0) return;

            // Determine the scheduled end of the workday (latest end among ranges)
            var latestEnd = ranges.Max(r => r.EndTime);
            var logoutTime = logoutDateTime.TimeOfDay;

            if (logoutTime < latestEnd)
            {
                await _notificationService.SendEarlyLogoutNotificationAsync(employee, logoutDateTime, latestEnd);
            }
        }

        private void ProcessEmployeeAccess(TimeTrackerData data)
        {
            if (string.IsNullOrEmpty(data.CardId))
            {
                data.AccessLevel = "Unknown";
                return;
            }

            var employee = _employeeService.GetEmployeeByCardId(data.CardId);

            if (employee != null)
            {
                data.EmployeeName = employee.FullName;

                if (employee.Access)
                {
                    data.AccessLevel = "Authorized";
                    data.Status = ValidateWorkSchedule(employee, data) ? "SUCCESS" : "SCHEDULE_VIOLATION";
                }
                else
                {
                    data.AccessLevel = "Unauthorized";
                    data.Status = "ACCESS_DENIED";
                }
            }
            else
            {
                data.AccessLevel = "Unknown";
                data.EmployeeName = null;
                data.Status = "CARD_NOT_ASSIGNED";
            }
        }

        private bool ValidateWorkSchedule(Employee employee, TimeTrackerData data)
        {
            if (employee.WorkSchedule == null)
                return true;

            var currentTime = data.ReceivedTimestamp;
            var dayOfWeek = (int)currentTime.DayOfWeek;

            var workingDays = employee.WorkSchedule.GetSelectedDays();
            if (!workingDays.Contains((DayOfWeek)dayOfWeek))
                return false;

            var timeRanges = employee.WorkSchedule.GetTimeRanges();
            var currentTimeSpan = currentTime.TimeOfDay;

            return timeRanges.Any(range =>
                currentTimeSpan >= range.StartTime &&
                currentTimeSpan <= range.EndTime);
        }

        public void ClearData()
        {
            var allData = _context.TimeTrackerData.ToList();
            _context.TimeTrackerData.RemoveRange(allData);
            _context.SaveChanges();

            DataCleared?.Invoke(this, EventArgs.Empty);
        }
    }
}