using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraCinema.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMinAmountToPromotion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinAmount",
                table: "Promotions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinAmount",
                table: "Promotions");
        }
    }
}
