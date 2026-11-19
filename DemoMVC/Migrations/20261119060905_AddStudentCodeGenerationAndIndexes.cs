using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DemoMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentCodeGenerationAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdmissionYear",
                table: "Students",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EducationSystemCode",
                table: "Students",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MajorCode",
                table: "Students",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE Students
                SET AdmissionYear = CASE
                        WHEN length(StudentCode) = 10
                            AND substr(StudentCode, 1, 2) GLOB '[0-9][0-9]'
                        THEN 2000 + CAST(substr(StudentCode, 1, 2) AS INTEGER)
                        ELSE strftime('%Y', 'now')
                    END,
                    EducationSystemCode = CASE
                        WHEN length(StudentCode) = 10
                            AND substr(StudentCode, 3, 2) GLOB '[0-9][0-9]'
                        THEN substr(StudentCode, 3, 2)
                        ELSE '21'
                    END,
                    MajorCode = CASE
                        WHEN length(StudentCode) = 10
                            AND substr(StudentCode, 5, 2) GLOB '[0-9][0-9]'
                        THEN substr(StudentCode, 5, 2)
                        ELSE '05'
                    END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "AdmissionYear",
                table: "Students",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EducationSystemCode",
                table: "Students",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MajorCode",
                table: "Students",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_StudentCode",
                table: "Students",
                column: "StudentCode");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_DepartmentCode",
                table: "Departments",
                column: "DepartmentCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_StudentCode",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Departments_DepartmentCode",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "AdmissionYear",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "EducationSystemCode",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MajorCode",
                table: "Students");
        }
    }
}
