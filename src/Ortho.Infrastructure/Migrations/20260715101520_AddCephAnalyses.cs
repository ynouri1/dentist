using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ortho.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCephAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CephAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MedicalImageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TemplateCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TemplateVersion = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CephAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CephAnalyses_Images_MedicalImageId",
                        column: x => x.MedicalImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CephLandmarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    X = table.Column<double>(type: "REAL", nullable: false),
                    Y = table.Column<double>(type: "REAL", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: true),
                    PlacedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CephLandmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CephLandmarks_CephAnalyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "CephAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CephAnalyses_MedicalImageId_TemplateCode",
                table: "CephAnalyses",
                columns: new[] { "MedicalImageId", "TemplateCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CephLandmarks_AnalysisId_Code",
                table: "CephLandmarks",
                columns: new[] { "AnalysisId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CephLandmarks");

            migrationBuilder.DropTable(
                name: "CephAnalyses");
        }
    }
}
