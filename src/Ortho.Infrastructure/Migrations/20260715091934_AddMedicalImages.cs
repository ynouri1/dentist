using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ortho.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicalImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PatientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    Modality = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    SourceDescription = table.Column<string>(type: "TEXT", nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    PixelSpacingXMm = table.Column<double>(type: "REAL", nullable: true),
                    PixelSpacingYMm = table.Column<double>(type: "REAL", nullable: true),
                    CalibrationSource = table.Column<int>(type: "INTEGER", nullable: false),
                    StorageKeyOriginal = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    StorageKeyDisplay = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    AcquiredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Images_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Images_PatientId",
                table: "Images",
                column: "PatientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Images");
        }
    }
}
