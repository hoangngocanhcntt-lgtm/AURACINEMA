using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraCinema.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewSurchargeAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NewSurchargeAmount",
                table: "PriceConfigs",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewSurchargeAmount",
                table: "PriceConfigs");
        }
    }
}
