using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DarsJadvali.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class V2_03_ClassStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_Subjects_Code",
                table: "Subjects",
                newName: "UX_Subjects_Code");

            migrationBuilder.AddColumn<int>(
                name: "AcademicYearId",
                table: "Teachers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContractPeriodsPerWeek",
                table: "Teachers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ContractRate",
                table: "Teachers",
                type: "decimal(4,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Teachers",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Teachers",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Teachers",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "Teachers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Teachers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVacancy",
                table: "Teachers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Teachers",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxGapsPerDay",
                table: "Teachers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxLessonsPerDay",
                table: "Teachers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "Teachers",
                type: "TEXT",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AcademicYearId",
                table: "Subjects",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Distribution",
                table: "Subjects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Subjects",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Subjects",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxStudents",
                table: "Subjects",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsHomework",
                table: "Subjects",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresSpecialClassroom",
                table: "Subjects",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "Subjects",
                type: "TEXT",
                maxLength: 24,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Classrooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AcademicYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Capacity = table.Column<int>(type: "INTEGER", nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    IsShared = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classrooms", x => x.Id);
                    table.CheckConstraint("CK_Classrooms_Capacity", "\"Capacity\" IS NULL OR \"Capacity\" > 0");
                    table.ForeignKey(
                        name: "FK_Classrooms_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Grades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AcademicYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    GradeNo = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grades", x => x.Id);
                    table.CheckConstraint("CK_Grades_GradeNo", "\"GradeNo\" >= 0 AND \"GradeNo\" <= 20");
                    table.ForeignKey(
                        name: "FK_Grades_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SchoolClasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AcademicYearId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    GradeId = table.Column<int>(type: "INTEGER", nullable: true),
                    ShiftId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClassTeacherId = table.Column<int>(type: "INTEGER", nullable: true),
                    HomeClassroomId = table.Column<int>(type: "INTEGER", nullable: true),
                    Language = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    StudentCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LegacyClassGroupId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolClasses", x => x.Id);
                    table.CheckConstraint("CK_SchoolClasses_StudentCount", "\"StudentCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_SchoolClasses_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SchoolClasses_Classrooms_HomeClassroomId",
                        column: x => x.HomeClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolClasses_Grades_GradeId",
                        column: x => x.GradeId,
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolClasses_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolClasses_Teachers_ClassTeacherId",
                        column: x => x.ClassTeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassDivisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SchoolClassId = table.Column<int>(type: "INTEGER", nullable: false),
                    DivisionTag = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassDivisions", x => x.Id);
                    table.CheckConstraint("CK_ClassDivisions_DivisionTag", "\"DivisionTag\" >= 0");
                    table.ForeignKey(
                        name: "FK_ClassDivisions_SchoolClasses_SchoolClassId",
                        column: x => x.SchoolClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SchoolClassId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClassDivisionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsEntireClass = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    StudentCount = table.Column<int>(type: "INTEGER", nullable: true),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentGroups", x => x.Id);
                    table.CheckConstraint("CK_StudentGroups_StudentCount", "\"StudentCount\" IS NULL OR \"StudentCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_StudentGroups_ClassDivisions_ClassDivisionId",
                        column: x => x.ClassDivisionId,
                        principalTable: "ClassDivisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentGroups_SchoolClasses_SchoolClassId",
                        column: x => x.SchoolClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Teachers_AcademicYearId_ShortName",
                table: "Teachers",
                columns: new[] { "AcademicYearId", "ShortName" },
                unique: true,
                filter: "\"AcademicYearId\" IS NOT NULL AND \"ShortName\" IS NOT NULL AND \"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "UX_Subjects_AcademicYearId_ShortName",
                table: "Subjects",
                columns: new[] { "AcademicYearId", "ShortName" },
                unique: true,
                filter: "\"AcademicYearId\" IS NOT NULL AND \"ShortName\" IS NOT NULL AND \"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "UX_ClassDivisions_SchoolClassId_DivisionTag",
                table: "ClassDivisions",
                columns: new[] { "SchoolClassId", "DivisionTag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ClassDivisions_Uid",
                table: "ClassDivisions",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Classrooms_AcademicYearId_ShortName",
                table: "Classrooms",
                columns: new[] { "AcademicYearId", "ShortName" },
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "UX_Classrooms_Uid",
                table: "Classrooms",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Grades_AcademicYearId_GradeNo",
                table: "Grades",
                columns: new[] { "AcademicYearId", "GradeNo" },
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "UX_Grades_Uid",
                table: "Grades",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolClasses_ClassTeacherId",
                table: "SchoolClasses",
                column: "ClassTeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolClasses_GradeId",
                table: "SchoolClasses",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolClasses_HomeClassroomId",
                table: "SchoolClasses",
                column: "HomeClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolClasses_ShiftId",
                table: "SchoolClasses",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "UX_SchoolClasses_AcademicYearId_Name",
                table: "SchoolClasses",
                columns: new[] { "AcademicYearId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "UX_SchoolClasses_AcademicYearId_ShortName",
                table: "SchoolClasses",
                columns: new[] { "AcademicYearId", "ShortName" },
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "UX_SchoolClasses_LegacyClassGroupId",
                table: "SchoolClasses",
                column: "LegacyClassGroupId",
                unique: true,
                filter: "\"LegacyClassGroupId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_SchoolClasses_Uid",
                table: "SchoolClasses",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentGroups_SchoolClassId",
                table: "StudentGroups",
                column: "SchoolClassId");

            migrationBuilder.CreateIndex(
                name: "UX_StudentGroups_ClassDivisionId_Name",
                table: "StudentGroups",
                columns: new[] { "ClassDivisionId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "UX_StudentGroups_SchoolClassId_EntireClass",
                table: "StudentGroups",
                column: "SchoolClassId",
                unique: true,
                filter: "\"IsEntireClass\" = 1 AND \"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "UX_StudentGroups_Uid",
                table: "StudentGroups",
                column: "Uid",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Subjects_AcademicYears_AcademicYearId",
                table: "Subjects",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Teachers_AcademicYears_AcademicYearId",
                table: "Teachers",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subjects_AcademicYears_AcademicYearId",
                table: "Subjects");

            migrationBuilder.DropForeignKey(
                name: "FK_Teachers_AcademicYears_AcademicYearId",
                table: "Teachers");

            migrationBuilder.DropTable(
                name: "StudentGroups");

            migrationBuilder.DropTable(
                name: "ClassDivisions");

            migrationBuilder.DropTable(
                name: "SchoolClasses");

            migrationBuilder.DropTable(
                name: "Classrooms");

            migrationBuilder.DropTable(
                name: "Grades");

            migrationBuilder.DropIndex(
                name: "UX_Teachers_AcademicYearId_ShortName",
                table: "Teachers");

            migrationBuilder.DropIndex(
                name: "UX_Subjects_AcademicYearId_ShortName",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "ContractPeriodsPerWeek",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "ContractRate",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "IsVacancy",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "MaxGapsPerDay",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "MaxLessonsPerDay",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "Distribution",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "MaxStudents",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "NeedsHomework",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "RequiresSpecialClassroom",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "Subjects");

            migrationBuilder.RenameIndex(
                name: "UX_Subjects_Code",
                table: "Subjects",
                newName: "IX_Subjects_Code");
        }
    }
}
