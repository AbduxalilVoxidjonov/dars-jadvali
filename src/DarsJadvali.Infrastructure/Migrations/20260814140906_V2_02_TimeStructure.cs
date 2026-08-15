using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DarsJadvali.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class V2_02_TimeStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_WorkDays_DayOfWeek",
                table: "WorkDays",
                newName: "UX_WorkDays_DayOfWeek");

            migrationBuilder.RenameIndex(
                name: "IX_Schedules_AcademicYearId_Name",
                table: "Schedules",
                newName: "UX_Schedules_AcademicYearId_Name");

            migrationBuilder.RenameIndex(
                name: "IX_AcademicYears_Name",
                table: "AcademicYears",
                newName: "UX_AcademicYears_Name");

            migrationBuilder.AddColumn<int>(
                name: "AcademicYearId",
                table: "WorkDays",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DayNo",
                table: "WorkDays",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinLessonsPerDay",
                table: "WorkDays",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "WorkDays",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "WorkDays",
                type: "TEXT",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CopiedFromScheduleId",
                table: "Schedules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Schedules",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TermId",
                table: "Schedules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeeksInCycle",
                table: "Schedules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "DaysPerWeek",
                table: "AcademicYears",
                type: "INTEGER",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndsOn",
                table: "AcademicYears",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartsOn",
                table: "AcademicYears",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TermsCount",
                table: "AcademicYears",
                type: "INTEGER",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<int>(
                name: "WeeksInCycle",
                table: "AcademicYears",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AcademicYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    ShiftNo = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                    table.CheckConstraint("CK_Shifts_ShiftNo", "\"ShiftNo\" >= 1 AND \"ShiftNo\" <= 4");
                    table.ForeignKey(
                        name: "FK_Shifts_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Terms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AcademicYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EndsOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terms", x => x.Id);
                    table.CheckConstraint("CK_Terms_Ordinal", "\"Ordinal\" >= 1 AND \"Ordinal\" <= 12");
                    table.ForeignKey(
                        name: "FK_Terms_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Periods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AcademicYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    ShiftId = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodNo = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<int>(type: "INTEGER", nullable: false),
                    EndTime = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    IsBreak = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Periods", x => x.Id);
                    table.CheckConstraint("CK_Periods_PeriodNo", "\"PeriodNo\" >= 0");
                    table.CheckConstraint("CK_Periods_TimeOrder", "\"EndTime\" > \"StartTime\"");
                    table.ForeignKey(
                        name: "FK_Periods_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Periods_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Mavjud ish kunlarida DayNo hosila ustun: dushanba(1) -> 0, ... yakshanba(7) -> 6.
            // Nomlar ham to'ldiriladi (UI hozircha WeekDay enum'idan oladi, lekin v2 kodi
            // DayNo/Name ustunlariga tayanadi).
            migrationBuilder.Sql("""
                UPDATE "WorkDays"
                SET "DayNo"     = "DayOfWeek" - 1,
                    "Name"      = CASE "DayOfWeek"
                                      WHEN 1 THEN 'Dushanba'  WHEN 2 THEN 'Seshanba'
                                      WHEN 3 THEN 'Chorshanba' WHEN 4 THEN 'Payshanba'
                                      WHEN 5 THEN 'Juma'      WHEN 6 THEN 'Shanba'
                                      WHEN 7 THEN 'Yakshanba' END,
                    "ShortName" = CASE "DayOfWeek"
                                      WHEN 1 THEN 'Du' WHEN 2 THEN 'Se' WHEN 3 THEN 'Cho'
                                      WHEN 4 THEN 'Pa' WHEN 5 THEN 'Ju' WHEN 6 THEN 'Sha'
                                      WHEN 7 THEN 'Yak' END
                WHERE "DayOfWeek" BETWEEN 1 AND 7;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_WorkDays_AcademicYearId_DayNo",
                table: "WorkDays",
                columns: new[] { "AcademicYearId", "DayNo" },
                unique: true,
                filter: "\"AcademicYearId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_CopiedFromScheduleId",
                table: "Schedules",
                column: "CopiedFromScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_TermId",
                table: "Schedules",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_Periods_ShiftId",
                table: "Periods",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "UX_Periods_AcademicYearId_PeriodNo",
                table: "Periods",
                columns: new[] { "AcademicYearId", "PeriodNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Periods_Uid",
                table: "Periods",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Shifts_AcademicYearId_ShiftNo",
                table: "Shifts",
                columns: new[] { "AcademicYearId", "ShiftNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Shifts_Uid",
                table: "Shifts",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Terms_AcademicYearId_Ordinal",
                table: "Terms",
                columns: new[] { "AcademicYearId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Terms_Uid",
                table: "Terms",
                column: "Uid",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedules_Schedules_CopiedFromScheduleId",
                table: "Schedules",
                column: "CopiedFromScheduleId",
                principalTable: "Schedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedules_Terms_TermId",
                table: "Schedules",
                column: "TermId",
                principalTable: "Terms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkDays_AcademicYears_AcademicYearId",
                table: "WorkDays",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Schedules_Schedules_CopiedFromScheduleId",
                table: "Schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_Schedules_Terms_TermId",
                table: "Schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkDays_AcademicYears_AcademicYearId",
                table: "WorkDays");

            migrationBuilder.DropTable(
                name: "Periods");

            migrationBuilder.DropTable(
                name: "Terms");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropIndex(
                name: "UX_WorkDays_AcademicYearId_DayNo",
                table: "WorkDays");

            migrationBuilder.DropIndex(
                name: "IX_Schedules_CopiedFromScheduleId",
                table: "Schedules");

            migrationBuilder.DropIndex(
                name: "IX_Schedules_TermId",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "WorkDays");

            migrationBuilder.DropColumn(
                name: "DayNo",
                table: "WorkDays");

            migrationBuilder.DropColumn(
                name: "MinLessonsPerDay",
                table: "WorkDays");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "WorkDays");

            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "WorkDays");

            migrationBuilder.DropColumn(
                name: "CopiedFromScheduleId",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "TermId",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "WeeksInCycle",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "DaysPerWeek",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "EndsOn",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "StartsOn",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "TermsCount",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "WeeksInCycle",
                table: "AcademicYears");

            migrationBuilder.RenameIndex(
                name: "UX_WorkDays_DayOfWeek",
                table: "WorkDays",
                newName: "IX_WorkDays_DayOfWeek");

            migrationBuilder.RenameIndex(
                name: "UX_Schedules_AcademicYearId_Name",
                table: "Schedules",
                newName: "IX_Schedules_AcademicYearId_Name");

            migrationBuilder.RenameIndex(
                name: "UX_AcademicYears_Name",
                table: "AcademicYears",
                newName: "IX_AcademicYears_Name");
        }
    }
}
