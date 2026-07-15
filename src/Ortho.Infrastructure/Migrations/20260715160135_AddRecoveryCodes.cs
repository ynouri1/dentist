using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ortho.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoveryCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RecoveryCodeHash",
                table: "Users",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RecoveryCodeSalt",
                table: "Users",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecoveryCodeHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RecoveryCodeSalt",
                table: "Users");
        }
    }
}
