using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetTelemetry.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleDisplayNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "display_number",
                table: "vehicles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "display_number",
                table: "vehicles");
        }
    }
}
