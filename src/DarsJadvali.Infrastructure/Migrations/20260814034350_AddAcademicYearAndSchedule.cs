using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DarsJadvali.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicYearAndSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduleEntries_ClassGroupId_DayOfWeek_LessonNumber",
                table: "ScheduleEntries");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleEntries_TeacherId_DayOfWeek_LessonNumber",
                table: "ScheduleEntries");

            migrationBuilder.AddColumn<int>(
                name: "ScheduleId",
                table: "ScheduleEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AcademicYears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartYear = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicYears", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Schedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AcademicYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Schedules_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // -----------------------------------------------------------------
            // MA'LUMOT KO'CHIRISH — mavjud dars yozuvlari YO'QOLMAYDI.
            // Bo'sh baza uchun ham, to'ldirilgan eski baza uchun ham ishlaydi:
            //   1) o'quv yili bo'lmasa — joriy sanadan hisoblab yaratiladi;
            //   2) jadval bo'lmasa — "Asosiy jadval" yaratilib faol qilinadi;
            //   3) barcha eski ScheduleEntries yozuvlari o'sha jadvalga biriktiriladi.
            // Barcha INSERT'lar shartli — migratsiya takror ishlasa ham dublikat bo'lmaydi.
            // -----------------------------------------------------------------
            var now = DateTime.Now;
            var startYear = now.Month >= 9 ? now.Year : now.Year - 1;
            var yearName = $"{startYear}–{startYear + 1}";
            var createdAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffffff",
                System.Globalization.CultureInfo.InvariantCulture);

            migrationBuilder.Sql($"""
                INSERT INTO "AcademicYears" ("Name", "StartYear", "Note")
                SELECT '{yearName}', {startYear}, NULL
                WHERE NOT EXISTS (SELECT 1 FROM "AcademicYears");
                """);

            migrationBuilder.Sql($"""
                INSERT INTO "Schedules" ("AcademicYearId", "Name", "IsActive", "CreatedAt")
                SELECT (SELECT MIN("Id") FROM "AcademicYears"), 'Asosiy jadval', 1, '{createdAt}'
                WHERE NOT EXISTS (SELECT 1 FROM "Schedules");
                """);

            migrationBuilder.Sql("""
                UPDATE "ScheduleEntries"
                SET "ScheduleId" = COALESCE(
                    (SELECT "Id" FROM "Schedules" WHERE "IsActive" = 1 ORDER BY "Id" LIMIT 1),
                    (SELECT MIN("Id") FROM "Schedules"))
                WHERE "ScheduleId" NOT IN (SELECT "Id" FROM "Schedules");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_ClassGroupId",
                table: "ScheduleEntries",
                column: "ClassGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_ScheduleId_ClassGroupId_DayOfWeek_LessonNumber",
                table: "ScheduleEntries",
                columns: new[] { "ScheduleId", "ClassGroupId", "DayOfWeek", "LessonNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_ScheduleId_TeacherId_DayOfWeek_LessonNumber",
                table: "ScheduleEntries",
                columns: new[] { "ScheduleId", "TeacherId", "DayOfWeek", "LessonNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_TeacherId",
                table: "ScheduleEntries",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_Name",
                table: "AcademicYears",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_AcademicYearId_Name",
                table: "Schedules",
                columns: new[] { "AcademicYearId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_IsActive",
                table: "Schedules",
                column: "IsActive");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEntries_Schedules_ScheduleId",
                table: "ScheduleEntries",
                column: "ScheduleId",
                principalTable: "Schedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEntries_Schedules_ScheduleId",
                table: "ScheduleEntries");

            migrationBuilder.DropTable(
                name: "Schedules");

            migrationBuilder.DropTable(
                name: "AcademicYears");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleEntries_ClassGroupId",
                table: "ScheduleEntries");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleEntries_ScheduleId_ClassGroupId_DayOfWeek_LessonNumber",
                table: "ScheduleEntries");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleEntries_ScheduleId_TeacherId_DayOfWeek_LessonNumber",
                table: "ScheduleEntries");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleEntries_TeacherId",
                table: "ScheduleEntries");

            migrationBuilder.DropColumn(
                name: "ScheduleId",
                table: "ScheduleEntries");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_ClassGroupId_DayOfWeek_LessonNumber",
                table: "ScheduleEntries",
                columns: new[] { "ClassGroupId", "DayOfWeek", "LessonNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_TeacherId_DayOfWeek_LessonNumber",
                table: "ScheduleEntries",
                columns: new[] { "TeacherId", "DayOfWeek", "LessonNumber" },
                unique: true);
        }
    }
}
