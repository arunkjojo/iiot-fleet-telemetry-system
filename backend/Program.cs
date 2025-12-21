using FleetTelemetry.Hubs;
using FleetTelemetry.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    // allow frontend origin from environment (FRONTEND_ORIGIN) or fallback to localhost
    options.AddPolicy("frontend", policy =>
    {
        var origin = Environment.GetEnvironmentVariable("FRONTEND_ORIGIN") ?? "http://localhost:3000";
        var extras = (Environment.GetEnvironmentVariable("ADDITIONAL_FRONTEND_ORIGINS") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries);
        var origins = new List<string> { origin };
        origins.AddRange(extras.Where(x => !string.IsNullOrWhiteSpace(x)));
        policy.WithOrigins(origins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddSignalR().AddMessagePackProtocol();
builder.Services.AddHostedService<TelemetrySimulationService>();

var app = builder.Build();

app.UseCors("frontend");
app.MapControllers();
app.MapHub<FleetHub>("/fleethub");

app.Run();
