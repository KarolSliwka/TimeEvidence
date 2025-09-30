using Microsoft.EntityFrameworkCore;
using TimeEvidence.Data;
using TimeEvidence.Models;

namespace TimeEvidence.Services
{
    public class EmployeeService
    {
        private readonly TimeEvidenceDbContext _context;

        public EmployeeService(TimeEvidenceDbContext context)
        {
            _context = context;
        }

        // Employees
        public IEnumerable<Employee> GetAllEmployees()
        {
            return _context.Employees
                .Include(e => e.Supervisor)
                .Include(e => e.WorkSchedule)
                .OrderBy(e => e.Surname)
                .ThenBy(e => e.Name)
                .AsNoTracking()
                .ToList();
        }

        public Employee? GetEmployeeById(Guid id)
        {
            return _context.Employees
                .Include(e => e.Supervisor)
                .Include(e => e.WorkSchedule)
                .FirstOrDefault(e => e.Id == id);
        }

        public Employee? GetEmployeeByCardId(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId)) return null;
            var normalized = cardId.Trim();

            return _context.Employees
                .Include(e => e.Supervisor)
                .Include(e => e.WorkSchedule)
                .FirstOrDefault(e => e.CardId == normalized);
        }

        public Employee CreateEmployee(Employee employee)
        {
            employee.Id = employee.Id == Guid.Empty ? Guid.NewGuid() : employee.Id;
            employee.CreatedAt = DateTime.Now;
            employee.UpdatedAt = DateTime.Now;
            _context.Employees.Add(employee);
            _context.SaveChanges();
            return employee;
        }

        public Employee? UpdateEmployee(Employee updated)
        {
            var existing = _context.Employees.FirstOrDefault(e => e.Id == updated.Id);
            if (existing == null) return null;

            existing.Name = updated.Name;
            existing.Surname = updated.Surname;
            existing.Position = updated.Position;
            existing.SupervisorId = updated.SupervisorId;
            existing.WorkScheduleId = updated.WorkScheduleId;
            existing.Access = updated.Access;
            // Intentionally not updating CardId here; use Assign/Unassign methods to avoid conflicts
            existing.UpdatedAt = DateTime.Now;

            _context.SaveChanges();
            return existing;
        }

        public bool DeleteEmployee(Guid id)
        {
            var employee = _context.Employees.FirstOrDefault(e => e.Id == id);
            if (employee == null) return false;

            // If employee has a card assigned, mark assignment history as closed
            if (!string.IsNullOrEmpty(employee.CardId))
            {
                var activeAssignments = _context.CardAssignments
                    .Where(ca => ca.CardId == employee.CardId && ca.IsActive && ca.EmployeeId == employee.Id)
                    .ToList();
                foreach (var a in activeAssignments)
                {
                    a.IsActive = false;
                    a.UnassignedAt = DateTime.Now;
                    a.UnassignedBy = "system";
                }
            }

            _context.Employees.Remove(employee);
            _context.SaveChanges();
            return true;
        }

        public IEnumerable<Employee> GetUnassignedEmployees()
        {
            return _context.Employees
                .Where(e => string.IsNullOrEmpty(e.CardId))
                .OrderBy(e => e.Surname).ThenBy(e => e.Name)
                .AsNoTracking()
                .ToList();
        }

        // Card management
        public Employee? AssignCardToEmployee(Guid employeeId, string cardId, bool grantAccess)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId cannot be empty", nameof(cardId));

            var normalized = cardId.Trim();
            var employee = _context.Employees.FirstOrDefault(e => e.Id == employeeId);
            if (employee == null) return null;

            // Ensure no other employee has this card
            var ownerOfCard = _context.Employees.FirstOrDefault(e => e.CardId == normalized && e.Id != employeeId);
            if (ownerOfCard != null)
            {
                throw new InvalidOperationException($"Card {normalized} is already assigned to {ownerOfCard.FullName}");
            }

            // If the employee had a different card, close that assignment
            if (!string.IsNullOrEmpty(employee.CardId) && employee.CardId != normalized)
            {
                var prevAssignments = _context.CardAssignments
                    .Where(ca => ca.CardId == employee.CardId && ca.IsActive && ca.EmployeeId == employee.Id)
                    .ToList();
                foreach (var a in prevAssignments)
                {
                    a.IsActive = false;
                    a.UnassignedAt = DateTime.Now;
                    a.UnassignedBy = "system";
                }
            }

            employee.CardId = normalized;
            employee.Access = grantAccess;
            employee.UpdatedAt = DateTime.Now;

            // Create/Update assignment history for this card
            var currentAssignment = _context.CardAssignments
                .FirstOrDefault(ca => ca.CardId == normalized && ca.EmployeeId == employee.Id && ca.IsActive);
            if (currentAssignment == null)
            {
                _context.CardAssignments.Add(new CardAssignment
                {
                    CardId = normalized,
                    EmployeeId = employee.Id,
                    AssignedAt = DateTime.Now,
                    AssignedBy = "system",
                    IsActive = true
                });
            }

            _context.SaveChanges();
            return employee;
        }

        public bool UnassignCard(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId)) return false;
            var normalized = cardId.Trim();

            var employee = _context.Employees.FirstOrDefault(e => e.CardId == normalized);
            if (employee == null) return false;

            employee.CardId = null;
            employee.UpdatedAt = DateTime.Now;

            var activeAssignments = _context.CardAssignments
                .Where(ca => ca.CardId == normalized && ca.IsActive)
                .ToList();
            foreach (var a in activeAssignments)
            {
                a.IsActive = false;
                a.UnassignedAt = DateTime.Now;
                a.UnassignedBy = "system";
            }

            _context.SaveChanges();
            return true;
        }

        public (bool isValid, Employee? employee, string message) ValidateCardAccess(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                return (false, null, "No card ID provided");

            var employee = GetEmployeeByCardId(cardId);
            if (employee == null)
                return (false, null, $"Card {cardId} not assigned to any employee");

            if (!employee.Access)
                return (false, employee, "Access denied for this employee");

            return (true, employee, "Access granted");
        }

        public bool IsCardAssigned(string? cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId)) return false;
            var normalized = cardId.Trim();
            return _context.Employees.Any(e => e.CardId == normalized);
        }

        // Supervisors
        public IEnumerable<Supervisor> GetAllSupervisors()
        {
            return _context.Supervisors.AsNoTracking().OrderBy(s => s.Surname).ThenBy(s => s.Name).ToList();
        }

        public Supervisor CreateSupervisor(Supervisor supervisor)
        {
            supervisor.Id = supervisor.Id == Guid.Empty ? Guid.NewGuid() : supervisor.Id;
            supervisor.CreatedAt = DateTime.Now;
            supervisor.UpdatedAt = DateTime.Now;
            _context.Supervisors.Add(supervisor);
            _context.SaveChanges();
            return supervisor;
        }

        public Supervisor? UpdateSupervisor(Supervisor updated)
        {
            var existing = _context.Supervisors.FirstOrDefault(s => s.Id == updated.Id);
            if (existing == null) return null;

            existing.Name = updated.Name;
            existing.Surname = updated.Surname;
            existing.Position = updated.Position;
            existing.Email = updated.Email;
            existing.PhoneNumber = updated.PhoneNumber;
            existing.NotificationPreference = updated.NotificationPreference;
            existing.UpdatedAt = DateTime.Now;
            _context.SaveChanges();
            return existing;
        }

        public (bool success, string message) DeleteSupervisor(Guid id)
        {
            var supervisor = _context.Supervisors.FirstOrDefault(s => s.Id == id);
            if (supervisor == null) return (false, "Supervisor not found");

            // Optionally unassign employees; due to FK OnDelete(SetNull) this is handled automatically
            var affected = _context.Employees.Count(e => e.SupervisorId == id);

            _context.Supervisors.Remove(supervisor);
            _context.SaveChanges();
            var msg = affected > 0
                ? $"Supervisor deleted. {affected} employee(s) unassigned from this supervisor."
                : "Supervisor deleted.";
            return (true, msg);
        }

        // WorkSchedules
        public IEnumerable<WorkSchedule> GetAllWorkSchedules()
        {
            return _context.WorkSchedules.AsNoTracking().OrderBy(ws => ws.ScheduleName).ToList();
        }

        public WorkSchedule CreateWorkSchedule(WorkSchedule schedule)
        {
            schedule.Id = schedule.Id == Guid.Empty ? Guid.NewGuid() : schedule.Id;
            schedule.CreatedAt = DateTime.Now;
            schedule.UpdatedAt = DateTime.Now;
            _context.WorkSchedules.Add(schedule);
            _context.SaveChanges();
            return schedule;
        }

        public WorkSchedule? UpdateWorkSchedule(WorkSchedule updated)
        {
            var existing = _context.WorkSchedules.FirstOrDefault(ws => ws.Id == updated.Id);
            if (existing == null) return null;

            existing.ScheduleName = updated.ScheduleName;
            existing.SelectedDays = updated.SelectedDays;
            existing.TimeRanges = updated.TimeRanges;
            existing.UpdatedAt = DateTime.Now;
            _context.SaveChanges();
            return existing;
        }

        public (bool success, string message) DeleteWorkSchedule(Guid id)
        {
            var schedule = _context.WorkSchedules.FirstOrDefault(ws => ws.Id == id);
            if (schedule == null) return (false, "Work schedule not found");

            // Unassign employees that reference this schedule
            var affectedEmployees = _context.Employees.Where(e => e.WorkScheduleId == id).ToList();
            foreach (var e in affectedEmployees)
            {
                e.WorkScheduleId = null;
                e.UpdatedAt = DateTime.Now;
            }

            _context.WorkSchedules.Remove(schedule);
            _context.SaveChanges();
            var msg = affectedEmployees.Count > 0
                ? $"Work schedule deleted. {affectedEmployees.Count} employee(s) unassigned from this schedule."
                : "Work schedule deleted.";
            return (true, msg);
        }
    }
}
