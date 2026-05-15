using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraCinema.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMovieCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MovieCode",
                table: "Movies",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MovieCode",
                table: "Movies");
        }
    }
}
