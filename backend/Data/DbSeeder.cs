using FleetTelemetry.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetTelemetry.Data;

public static class DbSeeder
{
    private static readonly string[] Drivers =
        ["Joy", "Rinto", "Aisha", "Maya", "Sam", "Liam", "Noah", "Eva", "Zara", "Omar", "Isha", "Kaden"];

    private static readonly string[] Models = ["NV Cargo", "Apex Hauler"];

    public static async Task SeedVehiclesAsync(IServiceScope scope, ILogger logger)
    {
        var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        if (await db.Vehicles.AnyAsync())
        {
            logger.LogInformation("Vehicles already seeded — skipping.");
            return;
        }

        logger.LogInformation("Seeding 10,000 vehicles into PostgreSQL...");

        const int total = 10_000;
        const int batchSize = 500;
        var batch = new List<VehicleEntity>(batchSize);

        for (int i = 0; i < total; i++)
        {
            batch.Add(new VehicleEntity
            {
                Id         = $"VEH-{i:D5}",
                DriverName = Drivers[i % Drivers.Length],
                Model      = Models[i % Models.Length],
            });

            if (batch.Count == batchSize)
            {
                await db.Vehicles.AddRangeAsync(batch);
                await db.SaveChangesAsync();
                batch.Clear();
                logger.LogInformation("Seeded {Count}/{Total} vehicles.", i + 1, total);
            }
        }

        if (batch.Count > 0)
        {
            await db.Vehicles.AddRangeAsync(batch);
            await db.SaveChangesAsync();
        }

        logger.LogInformation("Vehicle seeding complete. {Total} rows inserted.", total);
    }
}
