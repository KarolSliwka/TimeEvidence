using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TimeEvidence.Models;
using TimeEvidence.Models.DTOs;
using TimeEvidence.Services;

namespace TimeEvidence.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = TimeEvidence.Security.ApiKeyAuthenticationDefaults.AuthenticationScheme)]
    public class EmployeeController : ControllerBase
    {
        private readonly EmployeeService _employeeService;

        public EmployeeController(EmployeeService employeeService)
        {
            _employeeService = employeeService;
        }

        // Employee CRUD operations
        [HttpGet]
        public IActionResult GetAllEmployees()
        {
            var employees = _employeeService.GetAllEmployees();
            return Ok(employees);
        }

        [HttpGet("{id}")]
        public IActionResult GetEmployee(Guid id)
        {
            var employee = _employeeService.GetEmployeeById(id);
            if (employee == null)
                return NotFound($"Employee with ID {id} not found");

            return Ok(employee);
        }

        [HttpPost]
        public IActionResult CreateEmployee([FromBody] Employee employee)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var createdEmployee = _employeeService.CreateEmployee(employee);
            return CreatedAtAction(nameof(GetEmployee), new { id = createdEmployee.Id }, createdEmployee);
        }

        [HttpPut("{id}")]
        public IActionResult UpdateEmployee(Guid id, [FromBody] Employee employee)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            employee.Id = id;
            var updatedEmployee = _employeeService.UpdateEmployee(employee);
            if (updatedEmployee == null)
                return NotFound($"Employee with ID {id} not found");

            return Ok(updatedEmployee);
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteEmployee(Guid id)
        {
            var success = _employeeService.DeleteEmployee(id);
            if (!success)
                return NotFound($"Employee with ID {id} not found");

            return NoContent();
        }

        // Card assignment operations
        [HttpPost("assign-card")]
        public IActionResult AssignCard([FromBody] CardAssignmentDto assignment)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var employee = _employeeService.AssignCardToEmployee(
                    assignment.EmployeeId,
                    assignment.CardId,
                    assignment.GrantAccess);

                if (employee == null)
                    return NotFound($"Employee with ID {assignment.EmployeeId} not found");

                return Ok(new
                {
                    message = $"Card {assignment.CardId} successfully assigned to {employee.FullName}",
                    employee = employee
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("unassign-card/{cardId}")]
        public IActionResult UnassignCard(string cardId)
        {
            var success = _employeeService.UnassignCard(cardId);
            if (!success)
                return NotFound($"Card {cardId} is not assigned to any employee");

            return Ok(new { message = $"Card {cardId} has been unassigned successfully" });
        }

        [HttpGet("unassigned")]
        public IActionResult GetUnassignedEmployees()
        {
            var employees = _employeeService.GetUnassignedEmployees();
            return Ok(employees);
        }

        [HttpGet("card/{cardId}")]
        public IActionResult GetEmployeeByCard(string cardId)
        {
            var employee = _employeeService.GetEmployeeByCardId(cardId);
            if (employee == null)
                return NotFound($"No employee assigned to card {cardId}");

            return Ok(employee);
        }

        [HttpGet("card-status/{cardId}")]
        public IActionResult GetCardStatus(string cardId)
        {
            var (isValid, employee, message) = _employeeService.ValidateCardAccess(cardId);

            return Ok(new
            {
                card_id = cardId,
                is_assigned = employee != null,
                access_granted = isValid,
                message = message,
                employee = employee != null ? new
                {
                    id = employee.Id,
                    full_name = employee.FullName,
                    position = employee.Position,
                    access_status = employee.AccessStatus
                } : null
            });
        }

        // Supervisor operations
        [HttpGet("supervisors")]
        public IActionResult GetAllSupervisors()
        {
            var supervisors = _employeeService.GetAllSupervisors();
            return Ok(supervisors);
        }

        [HttpPost("supervisors")]
        public IActionResult CreateSupervisor([FromBody] Supervisor supervisor)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var createdSupervisor = _employeeService.CreateSupervisor(supervisor);
            return Ok(createdSupervisor);
        }

        // Work schedule operations
        [HttpGet("schedules")]
        public IActionResult GetAllWorkSchedules()
        {
            var schedules = _employeeService.GetAllWorkSchedules();
            return Ok(schedules);
        }

        [HttpPost("schedules")]
        public IActionResult CreateWorkSchedule([FromBody] WorkSchedule schedule)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var createdSchedule = _employeeService.CreateWorkSchedule(schedule);
            return Ok(createdSchedule);
        }
    }
}