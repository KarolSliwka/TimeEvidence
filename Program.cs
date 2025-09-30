using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using TimeEvidence.Services;
using TimeEvidence.Data;
using TimeEvidence.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Authentication/Authorization
// Use a policy scheme that selects API Key when an API key header is present, otherwise use Cookies for normal site auth
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = "MultiAuth";
        options.DefaultAuthenticateScheme = "MultiAuth";
        options.DefaultChallengeScheme = "MultiAuth";
    })
    .AddPolicyScheme("MultiAuth", "Multi-Auth (Cookies or ApiKey)", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            // If API key header or ApiKey auth header is present, use ApiKey scheme; otherwise, use Cookies
            if (context.Request.Headers.ContainsKey(ApiKeyAuthenticationDefaults.HeaderName) ||
                (context.Request.Headers.TryGetValue("Authorization", out var auth) && auth.ToString().StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase)))
            {
                return ApiKeyAuthenticationDefaults.AuthenticationScheme;
            }
            return CookieAuthenticationDefaults.AuthenticationScheme;
        };
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.SlidingExpiration = true;
    })
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationDefaults.AuthenticationScheme, options => { });

builder.Services.AddAuthorization();

// Add Entity Framework
builder.Services.AddDbContext<TimeEvidenceDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ??
                     "Data Source=timeevidence.db"));

// Register application services
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TimeTrackerDataService>();
builder.Services.AddScoped<DatabaseInitializer>();

var app = builder.Build();

// Initialize the database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TimeEvidenceDbContext>();
    context.Database.EnsureCreated();
    // Apply lightweight schema updates for SQLite when migrations aren't used
    var dbInit = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await dbInit.EnsureSchemaUpdatedAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
