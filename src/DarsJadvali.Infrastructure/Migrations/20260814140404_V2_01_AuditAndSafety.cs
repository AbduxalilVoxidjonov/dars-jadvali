using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DarsJadvali.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class V2_01_AuditAndSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "WorkDays",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "RowVersion",
                table: "WorkDays",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "WorkDays",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "WorkDays",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "Teachers",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "RowVersion",
                table: "Teachers",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "Teachers",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "Teachers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "TeacherAvailabilities",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "RowVersion",
                table: "TeacherAvailabilities",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "TeacherAvailabilities",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "TeacherAvailabilities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "TeacherAssignments",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "RowVersion",
                table: "TeacherAssignments",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "TeacherAssignments",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "TeacherAssignments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "Subjects",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "RowVersion",
                table: "Subjects",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "Subjects",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "Subjects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "Schedules",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "RowVersion",
                table: "Schedules",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "Schedules",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "Schedules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "ScheduleEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "RowVersion",
                table: "ScheduleEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "ScheduleEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "ScheduleEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "LessonSlots",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "RowVersion",
                table: "LessonSlots",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "LessonSlots",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "LessonSlots",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "ClassGroups",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "RowVersion",
                table: "ClassGroups",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "ClassGroups",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "ClassGroups",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "AcademicYears",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "RowVersion",
                table: "AcademicYears",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "AcademicYears",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "AcademicYears",
                type: "TEXT",
                nullable: true);

            // Mavjud qatorlarga Uid/audit qiymatlari beriladi. Unikal Uid indeksi
            // shundan KEYIN quriladi — aks holda barcha eski qatorlar bir xil
            // (bo'sh) Uid bilan qolib, indeks yaratilishi yiqilardi.
            BackfillAuditColumns(migrationBuilder);

            migrationBuilder.CreateIndex(
                name: "UX_WorkDays_Uid",
                table: "WorkDays",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Teachers_Uid",
                table: "Teachers",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_TeacherAvailabilities_Uid",
                table: "TeacherAvailabilities",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_TeacherAssignments_Uid",
                table: "TeacherAssignments",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Subjects_Uid",
                table: "Subjects",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Schedules_Uid",
                table: "Schedules",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ScheduleEntries_Uid",
                table: "ScheduleEntries",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_LessonSlots_Uid",
                table: "LessonSlots",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ClassGroups_Uid",
                table: "ClassGroups",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AcademicYears_Uid",
                table: "AcademicYears",
                column: "Uid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_WorkDays_Uid",
                table: "WorkDays");

            migrationBuilder.DropIndex(
                name: "UX_Teachers_Uid",
                table: "Teachers");

            migrationBuilder.DropIndex(
                name: "UX_TeacherAvailabilities_Uid",
                table: "TeacherAvailabilities");

            migrationBuilder.DropIndex(
                name: "UX_TeacherAssignments_Uid",
                table: "TeacherAssignments");

            migrationBuilder.DropIndex(
                name: "UX_Subjects_Uid",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "UX_Schedules_Uid",
                table: "Schedules");

            migrationBuilder.DropIndex(
                name: "UX_ScheduleEntries_Uid",
                table: "ScheduleEntries");

            migrationBuilder.DropIndex(
                name: "UX_LessonSlots_Uid",
                table: "LessonSlots");

            migrationBuilder.DropIndex(
                name: "UX_ClassGroups_Uid",
                table: "ClassGroups");

            migrationBuilder.DropIndex(
                name: "UX_AcademicYears_Uid",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "WorkDays");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "WorkDays");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "WorkDays");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "WorkDays");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "TeacherAvailabilities");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TeacherAvailabilities");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "TeacherAvailabilities");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "TeacherAvailabilities");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "TeacherAssignments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TeacherAssignments");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "TeacherAssignments");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "TeacherAssignments");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "ScheduleEntries");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ScheduleEntries");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "ScheduleEntries");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "ScheduleEntries");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "LessonSlots");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "LessonSlots");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "LessonSlots");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "LessonSlots");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "ClassGroups");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ClassGroups");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "ClassGroups");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "ClassGroups");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "AcademicYears");
        }

        /// <summary>
        /// Sxema v1 jadvallaridagi mavjud qatorlarga <c>Uid</c>, <c>RowVersion</c> va
        /// <c>CreatedAtUtc</c> qiymatlarini beradi.
        /// </summary>
        /// <remarks>
        /// SQLite'da <c>randomblob()</c> dan RFC 4122 v4 UUID yig'iladi. C# tomonidan
        /// <c>DateTime.Now</c> interpolatsiya QILINMAYDI — migratsiya deterministik
        /// bo'lishi uchun <c>strftime('now')</c> ishlatiladi (00 §4.2.5).
        /// EF Core SQLite'da <c>Guid</c> ni kichik harfli TEXT, <c>DateTimeOffset</c> ni
        /// <c>yyyy-MM-dd HH:mm:ss.fffffff+00:00</c> TEXT sifatida saqlaydi.
        /// </remarks>
        private static void BackfillAuditColumns(MigrationBuilder migrationBuilder)
        {
            const string newUuid = """
                lower(
                    hex(randomblob(4)) || '-' ||
                    hex(randomblob(2)) || '-4' ||
                    substr(hex(randomblob(2)), 2) || '-' ||
                    substr('89ab', abs(random()) % 4 + 1, 1) ||
                    substr(hex(randomblob(2)), 2) || '-' ||
                    hex(randomblob(6))
                )
                """;

            const string nowUtc = "strftime('%Y-%m-%d %H:%M:%S', 'now') || '.0000000+00:00'";

            const string emptyUuid = "'00000000-0000-0000-0000-000000000000'";

            foreach (var table in AuditedV1Tables)
            {
                migrationBuilder.Sql($"""
                    UPDATE "{table}"
                    SET "Uid"        = {newUuid},
                        "RowVersion" = {newUuid},
                        "CreatedAtUtc" = {nowUtc}
                    WHERE "Uid" = {emptyUuid};
                    """);
            }
        }

        /// <summary>V2_01 paytida mavjud bo'lgan sxema v1 jadvallari.</summary>
        private static readonly string[] AuditedV1Tables =
        {
            "AcademicYears",
            "Schedules",
            "ScheduleEntries",
            "Teachers",
            "Subjects",
            "ClassGroups",
            "TeacherAssignments",
            "TeacherAvailabilities",
            "WorkDays",
            "LessonSlots"
        };
    }
}
