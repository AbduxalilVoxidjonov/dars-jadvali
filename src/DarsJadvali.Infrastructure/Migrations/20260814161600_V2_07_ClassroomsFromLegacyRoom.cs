using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DarsJadvali.Infrastructure.Migrations
{
    /// <summary>
    /// <c>V2_07</c> — <c>Card.LegacyRoomNumber</c> (erkin matn) dan
    /// <c>Classroom</c> + <c>CardClassroom</c> ga o'tish uchun SXEMA tayyorgarligi.
    /// </summary>
    /// <remarks>
    /// <b>Nima uchun alohida ustun.</b> Matn xona nomidan yaratilgan yozuvni keyin
    /// TOPISH kerak, aks holda takror ishga tushirishda dublikat xona paydo bo'lardi.
    /// <c>ShortName</c> bo'yicha solishtirish yaramaydi: u 24 belgigacha kesiladi va
    /// to'qnashuvda raqam qo'shiladi. Filtrlangan unikal indeks
    /// (<c>UX_Classrooms_AcademicYearId_LegacySourceName</c>) dublikatni BAZA
    /// darajasida to'sadi — idempotentlik shu bilan kafolatlanadi.
    /// <para>
    /// <b>Natijaviy ta'sir:</b> xona endi <c>CardOccurrence</c> ga
    /// <c>ResourceKind.Classroom</c> qatori sifatida tushadi, ya'ni
    /// <c>UX_CardOccurrences_Schedule_Resource_Slot</c> "bitta xonada ikki dars"
    /// holatini rad etadi (ilgari <c>LegacyRoomNumber</c> proyeksiyaga umuman tushmasdi).
    /// </para>
    /// <para>
    /// <b>Eski ustun o'chirilmaydi:</b> <c>Card.LegacyRoomNumber</c> joyida qoladi —
    /// Desktop/Web hali unga tayanadi.
    /// </para>
    /// </remarks>
    public partial class V2_07_ClassroomsFromLegacyRoom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegacySourceName",
                table: "Classrooms",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Classrooms_AcademicYearId_LegacySourceName",
                table: "Classrooms",
                columns: new[] { "AcademicYearId", "LegacySourceName" },
                unique: true,
                filter: "\"LegacySourceName\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Classrooms_AcademicYearId_LegacySourceName",
                table: "Classrooms");

            migrationBuilder.DropColumn(
                name: "LegacySourceName",
                table: "Classrooms");
        }
    }
}
