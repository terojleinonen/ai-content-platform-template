using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiContentPlatform.Api.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddBrandVoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrandVoice",
                table: "Projects",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrandVoice",
                table: "Projects");
        }
    }
}
