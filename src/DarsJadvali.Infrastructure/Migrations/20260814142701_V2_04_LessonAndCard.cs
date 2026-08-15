using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DarsJadvali.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class V2_04_LessonAndCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Lessons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AcademicYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodsPerWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodsPerCard = table.Column<int>(type: "INTEGER", nullable: false),
                    AllowedDaysMask = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    AllowedWeeksMask = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    RequiredClassroomCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LegacyTeacherAssignmentId = table.Column<int>(type: "INTEGER", nullable: true),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lessons", x => x.Id);
                    table.CheckConstraint("CK_Lessons_PeriodsConsistency", "\"PeriodsPerWeek\" >= \"PeriodsPerCard\"");
                    table.CheckConstraint("CK_Lessons_PeriodsPerCard", "\"PeriodsPerCard\" >= 1 AND \"PeriodsPerCard\" <= 8");
                    table.CheckConstraint("CK_Lessons_PeriodsPerWeek", "\"PeriodsPerWeek\" > 0");
                    table.CheckConstraint("CK_Lessons_RequiredClassroomCount", "\"RequiredClassroomCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_Lessons_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Lessons_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimeOffs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AcademicYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerKind = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    DayNo = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodNo = table.Column<int>(type: "INTEGER", nullable: false),
                    WeeksMask = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Availability = table.Column<int>(type: "INTEGER", nullable: false),
                    Penalty = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeOffs", x => x.Id);
                    table.CheckConstraint("CK_TimeOffs_DayNo", "\"DayNo\" >= 0 AND \"DayNo\" <= 13");
                    table.CheckConstraint("CK_TimeOffs_Penalty", "\"Penalty\" >= 0 AND \"Penalty\" <= 1000");
                    table.CheckConstraint("CK_TimeOffs_PeriodNo", "\"PeriodNo\" >= 0");
                    table.ForeignKey(
                        name: "FK_TimeOffs_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScheduleId = table.Column<int>(type: "INTEGER", nullable: false),
                    LessonId = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodId = table.Column<int>(type: "INTEGER", nullable: false),
                    DayNo = table.Column<int>(type: "INTEGER", nullable: false),
                    WeeksMask = table.Column<int>(type: "INTEGER", nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    LegacyRoomNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    LegacyScheduleEntryId = table.Column<int>(type: "INTEGER", nullable: true),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cards", x => x.Id);
                    table.CheckConstraint("CK_Cards_DayNo", "\"DayNo\" >= 0 AND \"DayNo\" <= 13");
                    table.CheckConstraint("CK_Cards_WeeksMask", "\"WeeksMask\" > 0");
                    table.ForeignKey(
                        name: "FK_Cards_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Cards_Periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "Periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cards_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LessonClasses",
                columns: table => new
                {
                    LessonId = table.Column<int>(type: "INTEGER", nullable: false),
                    SchoolClassId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonClasses", x => new { x.LessonId, x.SchoolClassId });
                    table.ForeignKey(
                        name: "FK_LessonClasses_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LessonClasses_SchoolClasses_SchoolClassId",
                        column: x => x.SchoolClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonClassrooms",
                columns: table => new
                {
                    LessonId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClassroomId = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonClassrooms", x => new { x.LessonId, x.ClassroomId });
                    table.ForeignKey(
                        name: "FK_LessonClassrooms_Classrooms_ClassroomId",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonClassrooms_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LessonGroups",
                columns: table => new
                {
                    LessonId = table.Column<int>(type: "INTEGER", nullable: false),
                    StudentGroupId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonGroups", x => new { x.LessonId, x.StudentGroupId });
                    table.ForeignKey(
                        name: "FK_LessonGroups_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LessonGroups_StudentGroups_StudentGroupId",
                        column: x => x.StudentGroupId,
                        principalTable: "StudentGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonTeachers",
                columns: table => new
                {
                    LessonId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeacherId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonTeachers", x => new { x.LessonId, x.TeacherId });
                    table.ForeignKey(
                        name: "FK_LessonTeachers_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LessonTeachers_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CardClassrooms",
                columns: table => new
                {
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClassroomId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardClassrooms", x => new { x.CardId, x.ClassroomId });
                    table.ForeignKey(
                        name: "FK_CardClassrooms_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CardClassrooms_Classrooms_ClassroomId",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CardOccurrences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScheduleId = table.Column<int>(type: "INTEGER", nullable: false),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    DayNo = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodNo = table.Column<int>(type: "INTEGER", nullable: false),
                    WeekNo = table.Column<int>(type: "INTEGER", nullable: false),
                    ResourceKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ResourceId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardOccurrences", x => x.Id);
                    table.CheckConstraint("CK_CardOccurrences_DayNo", "\"DayNo\" >= 0 AND \"DayNo\" <= 13");
                    table.CheckConstraint("CK_CardOccurrences_PeriodNo", "\"PeriodNo\" >= 0");
                    table.CheckConstraint("CK_CardOccurrences_WeekNo", "\"WeekNo\" >= 0");
                    table.ForeignKey(
                        name: "FK_CardOccurrences_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CardOccurrences_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardClassrooms_ClassroomId",
                table: "CardClassrooms",
                column: "ClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_CardOccurrences_CardId",
                table: "CardOccurrences",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_CardOccurrences_Schedule_Day_Period",
                table: "CardOccurrences",
                columns: new[] { "ScheduleId", "DayNo", "PeriodNo" });

            migrationBuilder.CreateIndex(
                name: "UX_CardOccurrences_Schedule_Resource_Slot",
                table: "CardOccurrences",
                columns: new[] { "ScheduleId", "ResourceKind", "ResourceId", "DayNo", "PeriodNo", "WeekNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_LessonId",
                table: "Cards",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_PeriodId",
                table: "Cards",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_ScheduleId_DayNo",
                table: "Cards",
                columns: new[] { "ScheduleId", "DayNo" });

            migrationBuilder.CreateIndex(
                name: "UX_Cards_LegacyScheduleEntryId",
                table: "Cards",
                column: "LegacyScheduleEntryId",
                unique: true,
                filter: "\"LegacyScheduleEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Cards_Schedule_Lesson_Day_Period_Weeks",
                table: "Cards",
                columns: new[] { "ScheduleId", "LessonId", "DayNo", "PeriodId", "WeeksMask" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Cards_Uid",
                table: "Cards",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonClasses_SchoolClassId",
                table: "LessonClasses",
                column: "SchoolClassId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonClassrooms_ClassroomId",
                table: "LessonClassrooms",
                column: "ClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonGroups_StudentGroupId",
                table: "LessonGroups",
                column: "StudentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_AcademicYearId_SubjectId",
                table: "Lessons",
                columns: new[] { "AcademicYearId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_SubjectId",
                table: "Lessons",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "UX_Lessons_LegacyTeacherAssignmentId",
                table: "Lessons",
                column: "LegacyTeacherAssignmentId",
                unique: true,
                filter: "\"LegacyTeacherAssignmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Lessons_Uid",
                table: "Lessons",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonTeachers_TeacherId",
                table: "LessonTeachers",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "UX_TimeOffs_Owner_Slot",
                table: "TimeOffs",
                columns: new[] { "AcademicYearId", "OwnerKind", "OwnerId", "DayNo", "PeriodNo", "WeeksMask" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_TimeOffs_Uid",
                table: "TimeOffs",
                column: "Uid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardClassrooms");

            migrationBuilder.DropTable(
                name: "CardOccurrences");

            migrationBuilder.DropTable(
                name: "LessonClasses");

            migrationBuilder.DropTable(
                name: "LessonClassrooms");

            migrationBuilder.DropTable(
                name: "LessonGroups");

            migrationBuilder.DropTable(
                name: "LessonTeachers");

            migrationBuilder.DropTable(
                name: "TimeOffs");

            migrationBuilder.DropTable(
                name: "Cards");

            migrationBuilder.DropTable(
                name: "Lessons");
        }
    }
}
