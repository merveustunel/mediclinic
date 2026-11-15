using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediClinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BilingualMedicalNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "MedicalServices",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameTr",
                table: "MedicalServices",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Departments",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameTr",
                table: "Departments",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "MedicalServices");

            migrationBuilder.DropColumn(
                name: "NameTr",
                table: "MedicalServices");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "NameTr",
                table: "Departments");
        }
    }
}
