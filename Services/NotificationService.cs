using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TimeEvidence.Models;

namespace TimeEvidence.Services
{
    public class NotificationService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<NotificationService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public NotificationService(IConfiguration config, ILogger<NotificationService> logger, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task SendLateArrivalNotificationAsync(Employee employee, DateTime loginTime)
        {
            var supervisor = employee.Supervisor;
            if (supervisor == null) return;

            var pref = supervisor.NotificationPreference;
            if (pref == NotificationPreference.None) return;

            var msg = $"Employee {employee.FullName} logged in at {loginTime:HH:mm} - status: late to work";
            var subject = $"Late arrival: {employee.FullName} at {loginTime:HH:mm}";

            try
            {
                switch (pref)
                {
                    case NotificationPreference.Email:
                        if (!string.IsNullOrWhiteSpace(supervisor.Email))
                        {
                            await SendEmailAsync(supervisor.Email, subject, msg);
                        }
                        else
                        {
                            _logger.LogWarning("Supervisor {Supervisor} has Email preference but no email set.", supervisor.FullName);
                        }
                        break;
                    case NotificationPreference.Sms:
                        if (!string.IsNullOrWhiteSpace(supervisor.PhoneNumber))
                        {
                            await SendSmsAsync(supervisor.PhoneNumber, msg);
                        }
                        else
                        {
                            _logger.LogWarning("Supervisor {Supervisor} has SMS preference but no phone set.", supervisor.FullName);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification for late arrival of {Employee}", employee.FullName);
            }
        }

        public async Task SendEarlyLogoutNotificationAsync(Employee employee, DateTime logoutTime, TimeSpan scheduledEnd)
        {
            var supervisor = employee.Supervisor;
            if (supervisor == null) return;

            var pref = supervisor.NotificationPreference;
            if (pref == NotificationPreference.None) return;

            var scheduledEndText = new DateTime(logoutTime.Date.Ticks).Add(scheduledEnd).ToString("HH:mm");
            var msg = $"Employee {employee.FullName} logged out at {logoutTime:HH:mm} before scheduled end ({scheduledEndText})";
            var subject = $"Early logout: {employee.FullName} at {logoutTime:HH:mm} (scheduled end {scheduledEndText})";

            try
            {
                switch (pref)
                {
                    case NotificationPreference.Email:
                        if (!string.IsNullOrWhiteSpace(supervisor.Email))
                        {
                            await SendEmailAsync(supervisor.Email, subject, msg);
                        }
                        else
                        {
                            _logger.LogWarning("Supervisor {Supervisor} has Email preference but no email set.", supervisor.FullName);
                        }
                        break;
                    case NotificationPreference.Sms:
                        if (!string.IsNullOrWhiteSpace(supervisor.PhoneNumber))
                        {
                            await SendSmsAsync(supervisor.PhoneNumber, msg);
                        }
                        else
                        {
                            _logger.LogWarning("Supervisor {Supervisor} has SMS preference but no phone set.", supervisor.FullName);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification for early logout of {Employee}", employee.FullName);
            }
        }

        private async Task SendEmailAsync(string to, string subject, string body)
        {
            var host = _config["Smtp:Host"];
            var port = _config.GetValue<int?>("Smtp:Port") ?? 587;
            var from = _config["Smtp:From"];
            var user = _config["Smtp:User"];
            var pass = _config["Smtp:Pass"];
            var useSsl = _config.GetValue<bool?>("Smtp:UseSsl") ?? true;

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            {
                _logger.LogInformation("SMTP not configured. Would send email to {To}: {Subject} - {Body}", to, subject, body);
                await Task.CompletedTask;
                return;
            }

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = useSsl
            };

            if (!string.IsNullOrWhiteSpace(user))
            {
                client.Credentials = new NetworkCredential(user, pass);
            }

            using var message = new MailMessage(from, to, subject, body);
            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent to {To} with subject '{Subject}'", to, subject);
        }

        private async Task SendSmsAsync(string toPhone, string message)
        {
            var provider = _config["Sms:Provider"]; // e.g., "SMSAPI" | "Twilio"
            if (string.Equals(provider, "SMSAPI", StringComparison.OrdinalIgnoreCase))
            {
                // Prefer environment variables, fallback to configuration
                // Supported names:
                //   Sms__SMSAPI__AccessToken (ASP.NET Core hierarchical env var)
                //   SMSAPI_ACCESS_TOKEN (flat name)
                var token =
                    Environment.GetEnvironmentVariable("Sms__SMSAPI__AccessToken") ??
                    Environment.GetEnvironmentVariable("SMSAPI_ACCESS_TOKEN") ??
                    _config["Sms:SMSAPI:AccessToken"]; // Bearer token

                var from =
                    Environment.GetEnvironmentVariable("Sms__SMSAPI__From") ??
                    Environment.GetEnvironmentVariable("SMSAPI_FROM") ??
                    _config["Sms:SMSAPI:From"]; // Optional sender name/number
                if (string.IsNullOrWhiteSpace(token))
                {
                    _logger.LogWarning("SMSAPI provider selected but no access token configured (Sms:SMSAPI:AccessToken).");
                }
                else
                {
                    try
                    {
                        var client = _httpClientFactory.CreateClient("smsapi");
                        client.BaseAddress = new Uri("https://api.smsapi.pl/");
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                        var content = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("to", toPhone),
                            new KeyValuePair<string, string>("message", message),
                        }.Concat(string.IsNullOrWhiteSpace(from) ? Array.Empty<KeyValuePair<string, string>>() : new[] { new KeyValuePair<string, string>("from", from!) }));

                        var response = await client.PostAsync("sms.do", content);
                        var responseText = await response.Content.ReadAsStringAsync();
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogError("SMSAPI error {Status}: {Response}", (int)response.StatusCode, responseText);
                        }
                        else
                        {
                            _logger.LogInformation("SMS sent via SMSAPI to {To}: {Preview}", toPhone, Truncate(message, 60));
                        }
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send SMS via SMSAPI");
                    }
                }
            }

            // Optional Twilio logging block for future use
            if (string.Equals(provider, "Twilio", StringComparison.OrdinalIgnoreCase))
            {
                var from = _config["Sms:Twilio:From"];
                _logger.LogInformation("[Twilio] Would send SMS from {From} to {To}: {Message}", from, toPhone, message);
                return;
            }

            // Fallback: log-only
            _logger.LogInformation("[SMS] Would send to {To}: {Message}", toPhone, message);
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= max ? value : value.Substring(0, max) + "…";
        }
    }
}
