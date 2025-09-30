# Time Evidence Tracker

A .NET Blazor Server application that receives JSON data from time tracking systems and displays employee login/logout activities in a real-time table format.

## Features

- **Real-time Data Display**: Automatically refreshes every 5 seconds to show the latest time tracking activities
- **REST API Endpoints**: Receive JSON data from time tracking systems via HTTP POST requests
- **Responsive Table View**: Display employee login/logout data in a clean, organized table format
- **Data Management**: View, refresh, and clear collected data
- **Session Tracking**: Monitor active sessions and system status
- **Card ID Support**: Track employee activities by card/badge ID

## Getting Started

### Prerequisites

## Authentication

This app is secured by login for all UI pages. APIs remain protected by API Key.

- UI login path: `/login`
- Default credentials (override via environment variables `AUTH__USERNAME` and `AUTH__PASSWORD` or appsettings `Auth:Username`, `Auth:Password`):
  - Username: `admin`
  - Password: `admin123`

API endpoints require an API key using the `X-Api-Key` header or `Authorization: ApiKey <key>`.
Configure via environment variables `Api__Key` or `API_KEY`, or `appsettings.json` under `Api:Key`.


- .NET 6.0 or later
- Visual Studio Code or Visual Studio

### Running the Application

1. **Build the project:**
   ```bash
   dotnet build
   ```

2. **Run the application:**
   ```bash
   dotnet run
   ```

3. **Access the application:**
   - Open your browser and navigate to `https://localhost:7157` or `http://localhost:5103`
   - The main page will show the Arduino data monitor

## API Endpoints

### Send Time Tracking Data

**POST** `/api/timetracker/data`

Send JSON data from your time tracking system to this endpoint.

**Example JSON structure:**
```json
{
  "system_id": "TIME_TRACKER_01",
  "action": "LOGIN",
  "card_id": "F3222711", 
  "status": "SUCCESS",
  "timestamp_iso": "2025-10-14T22:05:12+02:00",
  "timestamp_local": "22:05:12",
  "active_sessions": 1,
  "system_uptime": 35372,
  "wifi_connected": true
}
```

### Get All Data

**GET** `/api/timetracker/data`

Retrieve all stored time tracking data.

### Get Latest Data

**GET** `/api/timetracker/data/latest`

Get the most recent data entry.

### Get Data by Action

**GET** `/api/timetracker/data/action/{action}`

Get data filtered by action (LOGIN, LOGOUT, etc.).

### Get Data by System

**GET** `/api/timetracker/data/system/{systemId}`

Get data filtered by system ID.

### Get Statistics

**GET** `/api/timetracker/stats`

Get system statistics including active sessions and totals.

### Clear All Data

**DELETE** `/api/timetracker/data`

Remove all stored data.

## API Authentication

All API endpoints under `/api/*` are protected with an API key when running in production. Blazor UI remains publicly accessible unless you add additional auth.

Configure the API key via environment variables (preferred):

- `Api__Key` (recommended; hierarchical)
- `API_KEY` (flat alias)

You can also set `Api:RequireAuth` to false to disable protection (not recommended except for local dev):

- `Api__RequireAuth=false`

Example (bash):

```bash
export Api__Key="<your-strong-key>"
export Api__RequireAuth=true
dotnet run
```

Example request:

```
POST /api/timetracker/data HTTP/1.1
Host: yourserver
Content-Type: application/json
X-Api-Key: <your-strong-key>

{ ...json body... }
```

Alternatively, use the Authorization header:

```
Authorization: ApiKey <your-strong-key>
```

## Arduino Example Code

Here's a simple example of how to send data from an Arduino with WiFi capability:

```cpp
#include <WiFi.h>
#include <HTTPClient.h>
#include <ArduinoJson.h>

const char* ssid = "your-wifi-ssid";
const char* password = "your-wifi-password";
const char* serverURL = "http://localhost:5103/api/arduino/data";

void setup() {
  Serial.begin(115200);
  WiFi.begin(ssid, password);
  
  while (WiFi.status() != WL_CONNECTED) {
    delay(1000);
    Serial.println("Connecting to WiFi...");
  }
  
  Serial.println("Connected to WiFi");
}

void loop() {
  if (WiFi.status() == WL_CONNECTED) {
    HTTPClient http;
    http.begin(serverURL);
    http.addHeader("Content-Type", "application/json");
    
    // Create JSON data
    DynamicJsonDocument doc(1024);
    doc["deviceId"] = "Arduino_001";
    doc["temperature"] = 23.5;
    doc["humidity"] = 65.2;
    doc["lightLevel"] = 450;
    doc["motionDetected"] = false;
    doc["status"] = "active";
    
    String jsonString;
    serializeJson(doc, jsonString);
    
    int httpResponseCode = http.POST(jsonString);
    
    if (httpResponseCode > 0) {
      Serial.print("HTTP Response: ");
      Serial.println(httpResponseCode);
    }
    
    http.end();
  }
  
  delay(5000); // Send data every 5 seconds
}
```

## Project Structure

```
TimeEvidence/
├── Controllers/
│   └── ArduinoController.cs    # API endpoints for receiving data
├── Models/
│   └── ArduinoData.cs          # Data model for Arduino sensor data
├── Services/
│   └── ArduinoDataService.cs   # Service for managing data storage
├── Pages/
│   └── Index.razor             # Main page with data table
├── Shared/
│   ├── MainLayout.razor        # Layout template
│   └── NavMenu.razor           # Navigation menu
└── Program.cs                  # Application configuration
```

## Data Fields

The application supports the following data fields from Arduino devices:

- **DeviceId**: Unique identifier for the Arduino device
- **Temperature**: Temperature sensor reading (°C)
- **Humidity**: Humidity sensor reading (%)
- **LightLevel**: Light sensor reading
- **MotionDetected**: Boolean indicating motion detection
- **Status**: Custom status string
- **Timestamp**: Automatically added when data is received
- **AdditionalData**: Dictionary for custom sensor data

## Development

To modify or extend the application:

1. **Add new sensor types**: Update the `ArduinoData` model in `Models/ArduinoData.cs`
2. **Customize the display**: Modify the table in `Pages/Index.razor`
3. **Add new API endpoints**: Extend the `ArduinoController`
4. **Change data storage**: Modify or replace the `ArduinoDataService`

## Notes

- Data is stored in memory and will be lost when the application restarts
- For production use, consider implementing persistent storage (database)
- The application auto-refreshes the display every 5 seconds
- Only the latest 50 records are shown in the table for performance

## Configuration: SMTP and SMS

The app reads configuration from `appsettings.json`, `appsettings.{Environment}.json`, and environment variables. For sensitive values like SMS API tokens, prefer environment variables.

### SMS (SMSAPI)

Set the SMSAPI access token via one of the following environment variables (first non-empty wins):

- `Sms__SMSAPI__AccessToken` (recommended; hierarchical format)
- `SMSAPI_ACCESS_TOKEN` (flat alias)

Optional sender name/number can be set via:

- `Sms__SMSAPI__From`
- `SMSAPI_FROM`

Example (PowerShell):

```powershell
$env:Sms__SMSAPI__AccessToken = "<your-token>"
$env:Sms__SMSAPI__From = "MyCompany"
dotnet run
```

Example (bash):

```bash
export Sms__SMSAPI__AccessToken="<your-token>"
export Sms__SMSAPI__From="MyCompany"
dotnet run
```

In `appsettings.json`, leave `Sms:SMSAPI:AccessToken` empty to avoid committing secrets.# TimeEvidence
