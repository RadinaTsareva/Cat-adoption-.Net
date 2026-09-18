using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatAdoption.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddImageUrlToCat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Cats",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Cats");
        }
    }
}
