using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraCinema.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSeatAndOrderSeatStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Seats");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "OrderSeats",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "OrderSeats");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Seats",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }
    }
}
