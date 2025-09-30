# Time Evidence Tracker

Production-ready Blazor Server app (ASP.NET Core 6 + EF Core + SQLite) for collecting time tracking events (badge scans), validating access against employee schedules, and visualizing activity in real time.

## Highlights

- Real-time dashboard: new events appear instantly with a subtle indicator
- Solid auth: cookie-based UI login, API key for all `/api/*` routes
- Clean domain model: normalized employees, card assignments, and work schedules
- Automated notifications: late arrivals and early logouts (pluggable SMS via SMSAPI)
- Persistent storage: EF Core + SQLite (`timeevidence.db`), last 50 records shown for perf

## Tech stack

- Blazor Server (.NET 6)
- ASP.NET Core Authentication (Cookies + custom API Key)
- Entity Framework Core + SQLite
- Minimal, DI-first services (`TimeTrackerDataService`, `EmployeeService`, `NotificationService`)

## Quick start

Prerequisites:
- .NET 6 SDK

Run locally:

```bash
# optional: set UI credentials (fallback to appsettings Auth:Username/Password)
export AUTH__USERNAME=admin
export AUTH__PASSWORD=admin

# for local dev, API auth is disabled by default via launchSettings; to require:
export Api__RequireAuth=true
export Api__Key="<your-strong-api-key>"

dotnet run
```

Open:
- UI: https://localhost:7157 or http://localhost:5103
- Login at /login (cookie auth). After login, you’ll see the live dashboard.

Tip (VS Code): there’s a task named "run" you can start from the Run Task menu.

## Authentication

- UI (Blazor pages): protected by cookie login
  - Login path: `/login`
  - Credentials via env or appsettings: `AUTH__USERNAME`, `AUTH__PASSWORD` or `Auth:Username`, `Auth:Password`
- API: protected by API key (enabled when `Api__RequireAuth=true`)
  - Send `X-Api-Key: <key>` or `Authorization: ApiKey <key>`
  - Configure via `Api__Key` (preferred), `API_KEY`, or `Api:Key` in appsettings

Example (bash):

```bash
export Api__RequireAuth=true
export Api__Key="<your-strong-api-key>"
dotnet run
```

## API reference

Base URL: `/api/timetracker`

- POST `/data` – ingest a time-tracker event
- GET `/data` – latest 50 records (descending)
- GET `/data/latest` – most recent record
- GET `/data/action/{action}` – filter by action (e.g., LOGIN, LOGOUT)
- GET `/data/system/{systemId}` – filter by device/system
- GET `/stats` – counters + last seen metadata
- DELETE `/data` – clear all data

### Example: send an event (curl)

```bash
curl -X POST "http://localhost:5103/api/timetracker/data" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: <your-strong-api-key>" \
  -d '{
    "system_id": "TIME_TRACKER_01",
    "action": "LOGIN",
    "card_id": "F3222711",
    "status": "SUCCESS",
    "timestamp_iso": "2025-10-14T22:05:12+02:00",
    "timestamp_local": "22:05:12",
    "active_sessions": 1,
    "system_uptime": 35372,
    "wifi_connected": true
  }'
```

Response (Arduino-friendly):

```json
{
  "message": "Data received successfully",
  "access_granted": true,
  "employee_name": "Jane",
  "employee_surname": "Doe",
  "position": "Operator",
  "access_level": "Authorized",
  "timestamp": "2025-10-14T20:05:13.123Z",
  "system_message": "Have a nice day!"
}
```

### Example: C# client

```csharp
using System.Net.Http;
using System.Net.Http.Json;

var http = new HttpClient { BaseAddress = new Uri("http://localhost:5103") };
http.DefaultRequestHeaders.Add("X-Api-Key", "<your-strong-api-key>");

var payload = new {
    system_id = "TIME_TRACKER_01",
    action = "LOGIN",
    card_id = "F3222711",
    timestamp_iso = DateTimeOffset.Now.ToString("O")
};

var res = await http.PostAsJsonAsync("/api/timetracker/data", payload);
res.EnsureSuccessStatusCode();
```

### Arduino/ESP example

```cpp
#include <WiFi.h>
#include <HTTPClient.h>
#include <ArduinoJson.h>

const char* ssid = "your-wifi-ssid";
const char* password = "your-wifi-password";
const char* serverURL = "http://localhost:5103/api/timetracker/data";

void setup() {
  Serial.begin(115200);
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED) { delay(500); }
}

void loop() {
  if (WiFi.status() == WL_CONNECTED) {
    HTTPClient http;
    http.begin(serverURL);
    http.addHeader("Content-Type", "application/json");
    http.addHeader("X-Api-Key", "<your-strong-api-key>");

    DynamicJsonDocument doc(512);
    doc["system_id"] = "ESP32_GATE";
    doc["action"] = "LOGIN";
    doc["card_id"] = "F3222711";
    doc["timestamp_iso"] = "2025-10-14T22:05:12+02:00";

    String body;
    serializeJson(doc, body);
    int code = http.POST(body);
    Serial.printf("HTTP %d\n", code);
    http.end();
  }
  delay(5000);
}
```

## Data model (core entity)

`TimeEvidence.Models.TimeTrackerData` maps cleanly to incoming JSON via `JsonPropertyName` and enriches it with computed properties and access resolution.

```csharp
public class TimeTrackerData
{
    public int Id { get; set; }
    public string? SystemId { get; set; }            // system_id
    public string? Action { get; set; }              // action (LOGIN/LOGOUT)
    public string? CardId { get; set; }              // card_id
    public string? Status { get; set; }              // server-side access/schedule result
    public string? TimestampIso { get; set; }        // timestamp_iso
    public string? TimestampLocal { get; set; }      // timestamp_local
    public int? ActiveSessions { get; set; }         // active_sessions
    public long? SystemUptime { get; set; }          // system_uptime (sec)
    public bool? WifiConnected { get; set; }         // wifi_connected
    public DateTime ReceivedTimestamp { get; set; }  // set on ingest

    // employee resolution
    public Guid? AssignedEmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? AccessLevel { get; set; }         // Authorized/Unauthorized/Unknown

    // convenience
    public DateTime? ParsedTimestamp => DateTime.TryParse(TimestampIso, out var dt) ? dt : null;
    public string UptimeFormatted => SystemUptime.HasValue ? TimeSpan.FromSeconds(SystemUptime.Value).ToString(@"dd\.hh\:mm\:ss") : "N/A";
    public bool IsAuthorized => AccessLevel == "Authorized";
}
```

How it works:
1) Controller stores the event, enriches it (employee lookup, access level, schedule validation)
2) Blazor components subscribe to service events to refresh the table instantly
3) Optional SMS notifications fire for late arrivals / early logouts

## UI features

- Dashboard cards with latest status and quick actions (refresh, clear, assign card, manage employees)
- Time-tracker table with card assignment shortcut and access indicators
- Auto-refresh backup every 10 seconds in addition to real-time events

## Configuration

The app reads configuration from `appsettings.json`, `appsettings.{Environment}.json`, and environment variables. Prefer env vars for secrets.

- UI auth: `AUTH__USERNAME`, `AUTH__PASSWORD` (or `Auth:Username`, `Auth:Password`)
- API key: `Api__RequireAuth` (true/false), `Api__Key` (or `API_KEY`)
- Database: `ConnectionStrings:DefaultConnection` (SQLite by default)
- SMS (SMSAPI):
  - `Sms__SMSAPI__AccessToken` or `SMSAPI_ACCESS_TOKEN`
  - `Sms__SMSAPI__From` or `SMSAPI_FROM`

Windows PowerShell examples:

```powershell
$env:AUTH__USERNAME = "admin"
$env:AUTH__PASSWORD = "admin"
$env:Api__RequireAuth = "true"
$env:Api__Key = "<your-strong-api-key>"
dotnet run
```

Windows bash (Git Bash) examples:

```bash
export AUTH__USERNAME=admin
export AUTH__PASSWORD=admin
export Api__RequireAuth=true
export Api__Key="<your-strong-api-key>"
dotnet run
```

## Project structure (key files)

```
Controllers/
  ArduinoController.cs        # TimeTrackerController: /api/timetracker endpoints
Models/
  ArduinoData.cs             # TimeTrackerData entity + JSON mapping
  DTOs/ArduinoResponseDto.cs # API response for devices
Services/
  TimeTrackerDataService.cs  # EF Core access, events, schedule validation, notifications
  EmployeeService.cs         # Employees and card assignments
  NotificationService.cs     # SMS notifications (SMSAPI)
Pages/
  Index.razor                # Real-time dashboard (authorized)
Program.cs                   # DI, auth (Cookies + ApiKey), EF Core (SQLite)
```

## Recruiter notes

- Custom multi-auth setup: PolicyScheme chooses API Key for API calls, Cookies for UI
- Event-driven UI updates without SignalR boilerplate (service events consumed by Blazor)
- Secure key handling with constant-time comparison, flexible headers (`X-Api-Key` or `Authorization`)
- Pragmatic persistence: SQLite for local dev; easily swappable via connection string

## Maintenance

- Database is created automatically at startup; to reset, stop the app and delete `timeevidence.db`
- Only the latest 50 records are displayed for performance; adjust in `TimeTrackerDataService.GetAllData()`

---

If you’d like a quick walkthrough during an interview, start from `Program.cs` (auth + DI), then `TimeTrackerController`, and finally the dashboard in `Pages/Index.razor`.
