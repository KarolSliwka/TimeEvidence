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
    public class TimeTrackerController : ControllerBase
    {
        private readonly TimeTrackerDataService _timeTrackerService;
        private readonly EmployeeService _employeeService;

        public TimeTrackerController(TimeTrackerDataService timeTrackerService, EmployeeService employeeService)
        {
            _timeTrackerService = timeTrackerService;
            _employeeService = employeeService;
        }

        [HttpPost("data")]
        public IActionResult ReceiveData([FromBody] TimeTrackerData data)
        {
            if (data == null)
            {
                return BadRequest("Invalid data received");
            }

            _timeTrackerService.AddData(data);

            // Create Arduino response with employee information
            var response = CreateArduinoResponse(data);
            return Ok(response);
        }

        private ArduinoResponseDto CreateArduinoResponse(TimeTrackerData data)
        {
            var response = new ArduinoResponseDto
            {
                Message = "Data received successfully",
                Timestamp = DateTime.Now,
                AccessLevel = data.AccessLevel ?? "Unknown"
            };

            if (!string.IsNullOrEmpty(data.CardId))
            {
                var (isValid, employee, message) = _employeeService.ValidateCardAccess(data.CardId);

                response.AccessGranted = isValid;
                response.SystemMessage = message;

                if (employee != null)
                {
                    response.EmployeeName = employee.Name;
                    response.EmployeeSurname = employee.Surname;
                    response.Position = employee.Position;
                    response.AccessLevel = employee.AccessStatus;
                }
                else
                {
                    response.SystemMessage = "Card not assigned to any employee";
                }
            }

            return response;
        }

        [HttpGet("data")]
        public IActionResult GetAllData()
        {
            var data = _timeTrackerService.GetAllData();
            return Ok(data);
        }

        [HttpGet("data/latest")]
        public IActionResult GetLatestData()
        {
            var data = _timeTrackerService.GetLatestData();
            if (data == null)
            {
                return NotFound("No data available");
            }
            return Ok(data);
        }

        [HttpGet("data/action/{action}")]
        public IActionResult GetDataByAction(string action)
        {
            var data = _timeTrackerService.GetDataByAction(action);
            return Ok(data);
        }

        [HttpGet("data/system/{systemId}")]
        public IActionResult GetDataBySystemId(string systemId)
        {
            var data = _timeTrackerService.GetDataBySystemId(systemId);
            return Ok(data);
        }

        [HttpGet("stats")]
        public IActionResult GetStats()
        {
            var latestData = _timeTrackerService.GetLatestData();
            return Ok(new
            {
                total_records = _timeTrackerService.GetDataCount(),
                active_sessions = _timeTrackerService.GetActiveSessionsCount(),
                last_system_id = latestData?.SystemId,
                last_action = latestData?.Action,
                last_received = latestData?.ReceivedTimestamp
            });
        }

        [HttpDelete("data")]
        public IActionResult ClearData()
        {
            _timeTrackerService.ClearData();
            return Ok(new { message = "All time tracker data cleared successfully" });
        }
    }
}