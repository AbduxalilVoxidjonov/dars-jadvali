# Migratsiya — eski bazadan sxema v2 ga

Bu hujjat maktabda **allaqachon ishlab turgan** bazani yangi sxemaga (v2) o'tkazish
jarayonini tushuntiradi: qaysi migratsiyalar bor, `LegacyToV2Backfill` nima qiladi,
zaxira qayerga tushadi va nima noto'g'ri ketsa nima qilish kerak.

> **Eng muhim xabar:** o'tish **additiv**. Eski jadvallar (`ScheduleEntries`,
> `TeacherAssignments`, `ClassGroups`, `LessonSlots`) **o'chirilmaydi** — ular joyida
> qoladi va eski ekranlar ishlashda davom etadi. Yangi model yonma-yon quriladi.

---

## 1. Qisqacha — foydalanuvchi nima ko'radi

Dasturning yangi versiyasini birinchi marta ochganingizda, **hech qanday tugma
bosmasdan**, quyidagilar avtomatik bajariladi:

```
1. Zaxira nusxa olinadi        →  backups/darsjadvali-YYYYMMDD-HHMMSS.db
2. Migratsiyalar qo'llanadi    →  yangi jadvallar quriladi
3. Boshlang'ich ma'lumot       →  ish kunlari, dars soatlari, faol jadval
4. Eski ma'lumot ko'chiriladi  →  ScheduleEntry → Card → CardOccurrence
```

Jadvalingiz joyida qoladi. Eski ekranlar ham, yangi jadval taxtasi ham **bir xil
darslarni** ko'rsatadi.

Manba: `src/DarsJadvali.Infrastructure/Persistence/DatabaseInitializer.cs`
(`InitializeAsync`, 54–70-qatorlar).

---

## 2. Migratsiyalar ro'yxati

Barchasi haqiqiy EF Core migratsiyalari —
`src/DarsJadvali.Infrastructure/Migrations/`.

| # | Migratsiya | Nima qiladi |
|---|---|---|
| 1 | `InitialCreate` | v1 sxemasi: `Teachers`, `Subjects`, `ClassGroups`, `TeacherAssignments`, `WorkDays`, `TeacherAvailabilities`, `LessonSlots`, `ScheduleEntries` |
| 2 | `AddAcademicYearAndSchedule` | `AcademicYear` + `Schedule`; mavjud `ScheduleEntry` larni standart jadvalga bog'lovchi SQL |
| 3 | **`V2_01_AuditAndSafety`** | 10 ta eski jadvalga `Uid` / `CreatedAtUtc` / `UpdatedAtUtc` / `RowVersion`; har jadvalda `UX_<Jadval>_Uid`; mavjud qatorlarga `randomblob` dan UUID v4 backfill |
| 4 | **`V2_02_TimeStructure`** | `Terms`, `Shifts`, `Periods`; `AcademicYears` va `WorkDays` kengaytmalari; `WorkDay.DayNo`/`Name`/`ShortName` backfill'i |
| 5 | **`V2_03_ClassStructure`** | `Grades`, `SchoolClasses`, `ClassDivisions`, `StudentGroups`, `Classrooms`; `Subjects`/`Teachers` kengaytmalari |
| 6 | **`V2_04_LessonAndCard`** | `Lessons`, `LessonTeachers`, `LessonClasses`, `LessonGroups`, `LessonClassrooms`, `Cards`, `CardClassrooms`, `CardOccurrences`, `TimeOffs` |
| 7 | **`V2_05_CardLengthAndConstraints`** | `Card.Length`; FK'larni qattiqlashtirish (`Cascade` → `Restrict`) + `CHECK` cheklovlari |
| 8 | **`V2_06_TimeOffFromAvailability`** | `TimeOffs.LegacyTeacherAvailabilityId` ustuni + indeks. **Faqat sxema** |
| 9 | **`V2_07_ClassroomsFromLegacyRoom`** | `Classrooms.LegacySourceName` + filtrlangan unikal indeks. **Faqat sxema** |

> **`V2_06` va `V2_07` ma'lumotni ko'chirmaydi** — ular faqat ustun va indeks qo'shadi.
> Haqiqiy ko'chirish `LegacyToV2Backfill` da (§4).

### Nom haqida ogohlantirish

Dastlabki rejada `V2_05` **`DropLegacyEntry`** deb atalgan edi (eski jadvalni tashlash).
Amalda u yozilmadi va **`V2_05` raqami boshqa migratsiyaga berildi**
(`V2_05_CardLengthAndConstraints`). Eski modelni olib tashlaydigan migratsiya
**hali mavjud emas** va yozilganda **boshqa nom** oladi.

---

## 3. Avtomatik zaxira

Manba: `src/DarsJadvali.Infrastructure/Persistence/DatabaseBackupService.cs`.

| Savol | Javob |
|---|---|
| **Qachon** | Har ishga tushishda, **migratsiyadan va ko'chirishdan OLDIN** (`DatabaseInitializer.TryBackupAsync`) |
| **Qanday** | SQLite'ning **`VACUUM INTO`** buyrug'i — oddiy fayl nusxasi emas |
| **Qayerga** | `<baza papkasi>/backups/darsjadvali-YYYYMMDD-HHMMSS.db` |
| **Nechta saqlanadi** | Oxirgi **10 tasi** (`KeepCount = 10`); eskilari avtomatik o'chadi |

To'liq yo'llar:

| Tizim | Zaxira papkasi |
|---|---|
| Windows | `%LOCALAPPDATA%\DarsJadvali\backups\` |
| macOS | `~/Library/Application Support/DarsJadvali/backups/` |
| Linux | `~/.local/share/DarsJadvali/backups/` |

### Nega `VACUUM INTO`, oddiy nusxa emas

Baza **WAL rejimida** ishlaydi (`SqlitePragmaInterceptor`: `journal_mode=WAL`).
Bu degani, oxirgi o'zgarishlar `darsjadvali.db` **faylida emas**, yonidagi
`darsjadvali.db-wal` faylida turishi mumkin. Faqat `.db` faylini ko'chirsangiz —
nusxa **to'liq bo'lmaydi**. `VACUUM INTO` esa izchil, bitta faylli nusxa yaratadi.

Shu sababli **qo'lda** zaxira olayotganda ham **dasturni yoping**.

### Zaxira olinmasa nima bo'ladi

Hech narsa to'xtamaydi. `IOException`, `UnauthorizedAccessException` va
`SqliteException` ushlanadi, jurnalga ogohlantirish yoziladi va **migratsiya davom
etadi** (`DatabaseInitializer.cs:87-93`). Sabab: zaxira olinmagani uchun dasturni
umuman ochmaslik foydalanuvchiga zarar keltirardi.

### Qachon zaxira olinmaydi

- Xotiradagi baza (`:memory:`) — testlarda.
- Migratsiya kutilmayotgan **va** eski ma'lumot allaqachon ko'chirilgan bo'lsa —
  ya'ni o'zgartiriladigan narsa yo'q (`onlyIfMigrationsPending`).

---

## 4. `LegacyToV2Backfill` — ma'lumot ko'chirish

Manba: `src/DarsJadvali.Infrastructure/Persistence/Backfill/LegacyToV2Backfill.cs`
(+ `ClassStructureFactory.cs`, `LegacyBackfillResult.cs`).

**Nega migratsiya ichida emas?** Yetim yozuvlar, qisqartma dublikatlari va guruhlarga
yoyish mantiqi SQL'da ifodalab bo'lmaydigan darajada murakkab — va u **testlanadigan**
bo'lishi kerak.

### 4.1 Nima nimaga aylanadi

```
ClassGroup          →  SchoolClass  (+ 3 ta ClassDivision, + 5 ta StudentGroup)
LessonSlot          →  Period
TeacherAssignment   →  Lesson  (+ LessonTeacher, LessonClass, LessonGroup)
ScheduleEntry       →  Card    →  CardOccurrence
TeacherAvailability →  TimeOff                        (V2_06)
Card.LegacyRoomNumber (matn)  →  Classroom + CardClassroom   (V2_07)
```

### 4.2 Bajarilish tartibi

Har bir o'quv yili uchun, **aynan shu tartibda** (`BackfillYearAsync`):

1. `EnsureTermsAsync` — choraklar
2. `EnsureShiftsAsync` — **doim 2 ta smena** yaratiladi
3. `EnsurePeriodsAsync` — `LessonSlot` va `ScheduleEntry` da **haqiqatan ishlatilgan**
   dars raqamlaridan; vaqt yo'q bo'lsa 45 daqiqa dars + 10 daqiqa tanaffus
4. `EnsureReferenceYearLinksAsync` — ma'lumotnomalarni o'quv yiliga bog'lash
5. `EnsureSchoolClassesAsync` — sinflar + standart bo'linish/guruh tuzilmasi
6. `EnsureLessonsAsync` — biriktirmalardan dars ta'riflari
7. `EnsureCardsAsync` — jadval yozuvlaridan kartochkalar
8. `EnsureTimeOffsAsync` — o'qituvchi bandligi
9. `EnsureClassroomsAsync` — matn xona nomlaridan xona yozuvlari
10. `ICardOccurrenceProjector.RebuildForScheduleAsync` — bandlik qatorlarini qayta qurish

### 4.3 Guruh tuzilmasi — har sinfga aniq 5 guruh

`ClassStructureFactory` har bir sinf uchun quyidagini yaratadi:

| `DivisionTag` | Bo'linish | Guruhlar |
|---|---|---|
| `0` | Butun sinf | 1 ta (`IsEntireClass = true`) |
| `1` | 1/2 guruh | 2 ta |
| `2` | O'g'il / qiz | 2 ta |

Eski jadvaldagi barcha darslar **"Butun sinf"** guruhiga tushadi — chunki v1 da guruh
tushunchasi umuman yo'q edi.

### 4.4 Idempotentlik

**Takror ishga tushirilsa 0 ta yangi yozuv qo'shadi.** Buni kod emas, **bazaning
o'zi** kafolatlaydi — quyidagi ustunlardagi **filtrlangan unikal indekslar**:

| Ustun | Nimani to'sadi |
|---|---|
| `SchoolClass.LegacyClassGroupId` | Bitta `ClassGroup` dan ikkinchi `SchoolClass` |
| `Lesson.LegacyTeacherAssignmentId` | Bitta biriktirmadan ikkinchi `Lesson` |
| `Card.LegacyScheduleEntryId` | Bitta `ScheduleEntry` dan ikkinchi `Card` |
| `TimeOff` → `UX_TimeOffs_Owner_Slot` | Bitta katakka ikkinchi cheklov |
| `Classroom` → `UX_Classrooms_AcademicYearId_LegacySourceName` | Bitta xona nomidan ikkinchi yozuv |

Shu sababli ko'chirish **har ishga tushishda** chaqirilsa ham xavfsiz.

### 4.5 Nima ko'chirilmaydi — halol ro'yxat

| Holat | Nima bo'ladi |
|---|---|
| **Eski jadvallar** | **O'chirilmaydi.** `ScheduleEntry`, `TeacherAssignment`, `ClassGroup`, `LessonSlot` joyida qoladi |
| **Xona to'qnashuvlari** | v1 da xona bandligi umuman tekshirilmagan, shuning uchun haqiqiy bazada bir xonada ikki dars uchrashi mumkin. Bunday holda **birinchi** kartochka xonani oladi, qolganlari **xonasiz** qoladi (matn `LegacyRoomNumber` saqlanadi) va `LegacyBackfillResult.RoomConflicts` da sanaladi. **Hech bir dars yo'qolmaydi** |
| **`TeacherAvailability`** | O'chirilmaydi va o'zgartirilmaydi — faqat oldinga `TimeOff` **`Forbidden`** qatorlariga proyeksiya qilinadi. Eski ma'lumotdan **`NotRecommended`** darajasi hech qachon hosil bo'lmaydi |
| **Qo'lda tahrirlangan `TimeOff`** | Bir marta yaratilgan katak **qayta yozilmaydi** — foydalanuvchi uni qo'lda o'zgartirgan bo'lishi mumkin |
| **Smenaga taqsimlash** | 2 ta smena yaratiladi, lekin **barcha dars soatlari 1-smenaga** tushadi. Ularni bo'lish uchun UI hali yo'q |
| **Yetim yozuvlar** | Biriktirmasi yo'q `ScheduleEntry` uchun **avtomatik `Lesson` yaratiladi** va `LegacyBackfillResult.OrphanLessons` da sanaladi — yozuv tashlab yuborilmaydi |

### 4.6 Xato bo'lsa nima bo'ladi

Ko'chirish xatosi **dasturni ishga tushirmay qo'ymaydi**
(`RunLegacyBackfillAsync`, `DatabaseInitializer.cs:150`):

- Istisno ushlanadi va jurnalga yoziladi;
- eski jadvallar **buzilmagan holda** joyida qoladi;
- foydalanuvchi hech narsa yo'qotmaydi — u eski ekranlarda ishlashda davom etadi;
- keyingi ishga tushirishda ko'chirish **qaytadan urinib ko'radi** (idempotent).

`ICardOccurrenceProjector` DI da ro'yxatdan o'tmagan bo'lsa, ko'chirish umuman
**o'tkazib yuboriladi**.

### 4.7 Natija hisoboti

`LegacyBackfillResult` (`DatabaseInitializer.LastBackfill` orqali) quyidagilarni
qaytaradi: `Terms`, `Shifts`, `Periods`, `SchoolClasses`, `ClassDivisions`,
`StudentGroups`, `Lessons`, **`OrphanLessons`**, `Cards`, `CardOccurrences`,
`TimeOffs`, `Classrooms`, `CardClassrooms`, **`RoomConflicts`** va o'zbekcha
`Messages` ro'yxati.

---

## 5. Haqiqiy bazada o'lchangan natija

Haqiqiy foydalanuvchi bazasining nusxasida (asl fayl o'zgartirilmagan):

```
Kirish : 65 ScheduleEntry · 9 TeacherAssignment · 4 ClassGroup · 9 Teacher · 7 Subject
         2 AcademicYear · 1 Schedule · 7 LessonSlot

Chiqish: 4 Term · 2 Shift · 7 Period · 4 SchoolClass · 12 ClassDivision · 20 StudentGroup
         9 Lesson (yetim: 0) · 65 Card · 390 CardOccurrence
         (390 = 65 karta × [1 o'qituvchi + 5 guruh])

Eski model buzilmagan: 65 / 9 / 4 · pragma foreign_key_check = bo'sh
Har sinfda "Butun sinf" guruhi soni: [1, 1, 1, 1]
```

---

## 6. Orqaga qaytarish

### 6.1 Eng ishonchli yo'l — zaxiradan tiklash

**Tavsiya etiladigan usul shu.**

1. Dasturni **yoping**.
2. Baza papkasini oching (§3 dagi jadval).
3. Joriy `darsjadvali.db`, `darsjadvali.db-wal`, `darsjadvali.db-shm`
   fayllarini boshqa joyga olib qo'ying (yoki nomini o'zgartiring).
4. `backups/` dan kerakli `darsjadvali-YYYYMMDD-HHMMSS.db` faylni oling,
   nomini **`darsjadvali.db`** ga o'zgartirib, baza papkasiga qo'ying.
5. Dasturning **eski versiyasini** oching.

> `-wal` va `-shm` fayllarini ham olib qo'yishni unutmang — aks holda eski WAL
> yangi fayl bilan mos kelmasligi mumkin.

### 6.2 Migratsiyani qaytarish (`Down`)

`V2_01`…`V2_04` migratsiyalarining `Down()` metodlari yozilgan va **to'liq
oldinga/orqaga aylanish** haqiqiy foydalanuvchi bazasining nusxasida sinalgan
(65 ta `ScheduleEntry` yo'qolmadi, `pragma foreign_key_check` bo'sh).

```bash
dotnet ef database update AddAcademicYearAndSchedule \
  -p src/DarsJadvali.Infrastructure -s src/DarsJadvali.Infrastructure
```

> **Diqqat:** `dotnet-ef` ning **8.x** versiyasi kerak (loyiha `net8.0`).
>
> **Cheklov — halol ogohlantirish:** `V2_05`, `V2_06`, `V2_07` uchun `Down()`
> alohida to'liq aylanish sinovidan **o'tkazilmagan**. Ular asosan ustun va indeks
> qo'shadi, lekin `V2_05` FK'larni ham qayta quradi. Shu sababli **birinchi tanlov
> doim zaxiradan tiklash** (§6.1) bo'lsin.
>
> `Down()` ma'lumotni qaytarmaydi: ko'chirilgan `Card`/`CardOccurrence` qatorlari
> jadval bilan birga o'chadi. Eski `ScheduleEntry` yozuvlari esa hech qachon
> o'chirilmagani uchun joyida qoladi.

### 6.3 Butunlay noldan boshlash

Ma'lumot kerak bo'lmasa: baza faylini o'chiring — dastur keyingi ochilishida yangi,
bo'sh baza yaratadi.

```bash
# macOS
rm -rf ~/Library/Application\ Support/DarsJadvali
```
```bash
# Linux
rm -rf ~/.local/share/DarsJadvali
```
```powershell
# Windows
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\DarsJadvali"
```

---

## 7. Yangi migratsiya qo'shish (dasturchi uchun)

```bash
dotnet ef migrations add <Nom> \
  -p src/DarsJadvali.Infrastructure -s src/DarsJadvali.Infrastructure
```

`dotnet ef` o'rnatilmagan bo'lsa:

```bash
dotnet tool install --global dotnet-ef --version 8.*
```

Tizimda 9.x/10.x global o'rnatilgan bo'lsa, loyihaga lokal tool sifatida qo'shing:

```bash
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 8.*
dotnet dotnet-ef migrations add <Nom> -p src/DarsJadvali.Infrastructure -s src/DarsJadvali.Infrastructure
```

### SQLite'ga xos ehtiyot choralari

- SQLite **ustunni o'chira olmaydi va turini o'zgartira olmaydi** — EF Core buni
  "jadvalni qayta qurish" bilan hal qiladi. FK'lar va indekslar shu paytda
  yo'qolmasligini tekshiring.
- `HasDefaultValue` ni ehtiyot bo'lib ishlating: EF Core CLR standart qiymatini
  (`0`, `false`) "sentinel" deb hisoblab ustunni `INSERT` dan **tushirib qoldiradi**.
  Aynan shu sababli `Card.WeeksMask` va `Lesson.PeriodsPerCard` da `HasDefaultValue`
  **olib tashlangan** — aks holda `WeeksMask = 0` jimgina `1` ga aylanib, `CHECK`
  cheklovi hech qachon ishlamas edi.
- Indekslarni **nomlab** qo'ying (`.HasDatabaseName("UX_...")`) — nomsiz indeks
  keyingi migratsiyada boshqa nom olib, UI so'rovlarini sindirishi mumkin.

---

## 8. Ma'lum cheklovlar

| Cheklov | Tafsilot |
|---|---|
| **Eski model olib tashlanmagan** | `ScheduleEntry` hamon `DbSet` va jadval sifatida turibdi; uni tashlaydigan migratsiya **yozilmagan** |
| **`V2_05` nomi band** | Rejadagi `V2_05_DropLegacyEntry` amalga oshmadi; raqam `V2_05_CardLengthAndConstraints` ga berildi |
| **`V2_05`–`V2_07` `Down()`** | To'liq aylanish sinovidan o'tkazilmagan — §6.2 |
| **Smena taqsimoti** | Barcha dars soatlari 1-smenaga tushadi; taqsimlash UI'si yo'q |
| **Xona to'qnashuvlari** | Avtomatik hal qilinmaydi — ortiqcha kartochkalar xonasiz qoladi va sanaladi |
| **`NotRecommended` darajasi** | Eski `TeacherAvailability` dan hosil bo'lmaydi — faqat `Forbidden` |
| **Ikki jarayon** | Desktop va Web bir vaqtda bitta bazani ochishi mumkin (WAL yoqilgan), lekin migratsiya paytida **faqat bittasi** ishlab turgani ma'qul |

---

Tegishli hujjatlar:
[`CONTRACT.md`](CONTRACT.md) · [`ARXITEKTURA.md`](ARXITEKTURA.md) ·
[`ALGORITM.md`](ALGORITM.md) ·
[`research/00-MAQSAD-ARXITEKTURA.md`](research/00-MAQSAD-ARXITEKTURA.md) §10
