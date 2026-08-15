using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DarsJadvali.Infrastructure.Migrations
{
    /// <summary>
    /// <c>V2_06</c> — eski 2 holatli <c>TeacherAvailability</c> dan 3 holatli
    /// <c>TimeOff</c> ga o'tish uchun SXEMA tayyorgarligi.
    /// </summary>
    /// <remarks>
    /// <b>Ma'lumot ko'chirish bu yerda EMAS</b> — u <c>LegacyToV2Backfill</c> ichida
    /// (00 §10.6): vaqt oralig'ini (kun, soat) katakchalariga yoyish qoidasi
    /// (<c>LessonAvailabilityRules</c>: qora ro'yxat ustun, oq ro'yxat faqat mavjud
    /// bo'lsa qo'llanadi) SQL'da takrorlanmasligi va testlanadigan bo'lishi kerak.
    /// <para>
    /// <b>Eski jadval o'chirilmaydi:</b> <c>TeacherAvailabilities</c> joyida qoladi —
    /// Desktop/Web hali unga tayanadi (<c>V2_05_DropLegacyEntry</c> alohida bosqich).
    /// </para>
    /// <para>
    /// <b>Indeks ataylab unikal EMAS:</b> bitta eski vaqt oralig'i bir nechta
    /// katakchaga yoyiladi. Ko'chirishning idempotentligini <c>UX_TimeOffs_Owner_Slot</c>
    /// (o'quv yili + ega + kun + soat + haftalar) kafolatlaydi.
    /// </para>
    /// </remarks>
    public partial class V2_06_TimeOffFromAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LegacyTeacherAvailabilityId",
                table: "TimeOffs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffs_LegacyTeacherAvailabilityId",
                table: "TimeOffs",
                column: "LegacyTeacherAvailabilityId",
                filter: "\"LegacyTeacherAvailabilityId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TimeOffs_LegacyTeacherAvailabilityId",
                table: "TimeOffs");

            migrationBuilder.DropColumn(
                name: "LegacyTeacherAvailabilityId",
                table: "TimeOffs");
        }
    }
}
