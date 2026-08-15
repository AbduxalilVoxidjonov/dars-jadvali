using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DarsJadvali.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class V2_05_CardLengthAndConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEntries_ClassGroups_ClassGroupId",
                table: "ScheduleEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEntries_Subjects_SubjectId",
                table: "ScheduleEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEntries_Teachers_TeacherId",
                table: "ScheduleEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_TeacherAssignments_ClassGroups_ClassGroupId",
                table: "TeacherAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_TeacherAssignments_Subjects_SubjectId",
                table: "TeacherAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_TeacherAssignments_Teachers_TeacherId",
                table: "TeacherAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Schedules_IsActive",
                table: "Schedules");

            // Kartochka uzunligi. Mavjud qatorlar eski modeldan kelgan — hammasi bir soatlik.
            // DEFAULT 1 faqat SHU migratsiya davomida kerak: CK_Cards_Length qo'shilganda
            // SQLite jadvalni qayta quradi va yangi ta'rif MODELDAN olinadi, modelda esa
            // HasDefaultValue ATAYLAB yo'q (0 "sentinel" tuzog'i — CardConfiguration izohi).
            migrationBuilder.AddColumn<int>(
                name: "Length",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            // Himoya: qandaydir sabab bilan 0 qolgan qator CHECK'ni yiqitmasin.
            migrationBuilder.Sql(
                "UPDATE \"Cards\" SET \"Length\" = 1 WHERE \"Length\" IS NULL OR \"Length\" < 1;");

            // Filtrlangan UNIQUE indeksdan OLDIN: bir nechta faol jadval qolgan bo'lsa
            // eng eskisidan boshqasi o'chiriladi, aks holda indeks yaratilmasdi.
            migrationBuilder.Sql(
                "UPDATE \"Schedules\" SET \"IsActive\" = 0 " +
                "WHERE \"IsActive\" = 1 AND \"Id\" <> " +
                "(SELECT MIN(\"Id\") FROM \"Schedules\" WHERE \"IsActive\" = 1);");

            migrationBuilder.CreateIndex(
                name: "UX_Schedules_IsActive",
                table: "Schedules",
                column: "IsActive",
                unique: true,
                filter: "\"IsActive\" = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Cards_Length",
                table: "Cards",
                sql: "\"Length\" >= 1 AND \"Length\" <= 8");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEntries_ClassGroups_ClassGroupId",
                table: "ScheduleEntries",
                column: "ClassGroupId",
                principalTable: "ClassGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEntries_Subjects_SubjectId",
                table: "ScheduleEntries",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEntries_Teachers_TeacherId",
                table: "ScheduleEntries",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeacherAssignments_ClassGroups_ClassGroupId",
                table: "TeacherAssignments",
                column: "ClassGroupId",
                principalTable: "ClassGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeacherAssignments_Subjects_SubjectId",
                table: "TeacherAssignments",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeacherAssignments_Teachers_TeacherId",
                table: "TeacherAssignments",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEntries_ClassGroups_ClassGroupId",
                table: "ScheduleEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEntries_Subjects_SubjectId",
                table: "ScheduleEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleEntries_Teachers_TeacherId",
                table: "ScheduleEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_TeacherAssignments_ClassGroups_ClassGroupId",
                table: "TeacherAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_TeacherAssignments_Subjects_SubjectId",
                table: "TeacherAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_TeacherAssignments_Teachers_TeacherId",
                table: "TeacherAssignments");

            migrationBuilder.DropIndex(
                name: "UX_Schedules_IsActive",
                table: "Schedules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Cards_Length",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Length",
                table: "Cards");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_IsActive",
                table: "Schedules",
                column: "IsActive");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEntries_ClassGroups_ClassGroupId",
                table: "ScheduleEntries",
                column: "ClassGroupId",
                principalTable: "ClassGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEntries_Subjects_SubjectId",
                table: "ScheduleEntries",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleEntries_Teachers_TeacherId",
                table: "ScheduleEntries",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeacherAssignments_ClassGroups_ClassGroupId",
                table: "TeacherAssignments",
                column: "ClassGroupId",
                principalTable: "ClassGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeacherAssignments_Subjects_SubjectId",
                table: "TeacherAssignments",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeacherAssignments_Teachers_TeacherId",
                table: "TeacherAssignments",
                column: "TeacherId",
                principalTable: "Teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
