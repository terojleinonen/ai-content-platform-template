using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiContentPlatform.Api.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddAiUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiUsage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Operation = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    InputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    OutputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    CacheReadTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    CacheWriteTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    Estimated = table.Column<bool>(type: "INTEGER", nullable: false),
                    CostUsd = table.Column<double>(type: "REAL", nullable: true),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsage", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsage_CreatedAt",
                table: "AiUsage",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsage_ProjectId",
                table: "AiUsage",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsage");
        }
    }
}
