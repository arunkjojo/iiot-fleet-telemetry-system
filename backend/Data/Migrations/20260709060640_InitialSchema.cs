using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FleetTelemetry.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    driver_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "telemetry_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vehicle_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    fuel_percent = table.Column<double>(type: "double precision", nullable: true),
                    speed_kph = table.Column<double>(type: "double precision", nullable: true),
                    engine_health = table.Column<int>(type: "integer", nullable: true),
                    temp_celsius = table.Column<int>(type: "integer", nullable: true),
                    cargo_load = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_telemetry_snapshots_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vehicle_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    logged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_vehicle_logs_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_telemetry_vehicle_time",
                table: "telemetry_snapshots",
                columns: new[] { "vehicle_id", "recorded_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_logs_vehicle_time",
                table: "vehicle_logs",
                columns: new[] { "vehicle_id", "logged_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telemetry_snapshots");

            migrationBuilder.DropTable(
                name: "vehicle_logs");

            migrationBuilder.DropTable(
                name: "vehicles");
        }
    }
}
