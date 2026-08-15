# aSc TimeTables — To'liq ma'lumotlar modeli (reverse engineering hisoboti)

> **Tadqiqot manbai:** `/Users/me/Projects/TimeTables/TimeTables` (aSc TimeTables 2016, build `roz.exe` 2016-07-07)
> **Sana:** 2026-08-14
> **Maqsad:** `darsjadvali` loyihasi uchun PostgreSQL/EF Core sxemasini loyihalashda foydalaniladigan kanonik entity/field ro'yxati.

## Manbalar va ularning ishonchlilik darajasi

| Manba | Nima beradi | Ishonchlilik |
|---|---|---|
| `template/xmlexport/asctt2012.xml` | aSc'ning **kanonik** entity + column ro'yxati (eng yangi sxema) | ★★★★★ |
| `template/xmlexport/asctt2008.xml` | eski sxema — evolyutsiyani ko'rsatadi | ★★★★★ |
| `template/xmlexport/{agenda_sk,dinaplo_hu,gescola_es,sakhr_om,sample}.xml` | vendor integratsiyalari — qo'shimcha entity'lar (`groupsubjects`, `classtimetables`) | ★★★★☆ |
| `template/Import Samples/XML/*.xml` | import namunalari — `classsubjects` (lesson grid) entity'si va real XML shakli | ★★★★☆ |
| `lang.asc` (4.7 MB, 2812 ta EN string) | **barcha** UI maydon nomlari, cheklov nomlari, dialog nomlari | ★★★★☆ (UI label, DB column emas) |
| `*.roz` binar fayllar | haqiqiy saqlash formati, record tuzilishi, real maktab hajmi | ★★★☆☆ (qisman dekodlangan) |
| `designs/*/def.xml`, `template/Web`, `template/excelexport` | chop etish/eksport uchun kerak bo'ladigan denormalizatsiya | ★★★★☆ |
| `roz.exe` | **foydasiz** — ASPack bilan packed (`.aspack`/`.adata` sectionlari), stringlar siqilgan | ★☆☆☆☆ |

---

## 1. Kanonik entity ro'yxati

XML eksport sxemasi qoidasi: konteyner tegi — ko'plik (`<teachers>`), bola element — birlik (`<teacher>`), `columns="..."` atributi ustunlar ro'yxatini beradi, har bir ustun XML atribut sifatida yoziladi.

```xml
<teachers options="canadd,export:silent" columns="id,name,short">
   <teacher id="1" name="Bacova" short="Bc"/>
</teachers>
```

Quyidagi jadvallarda **Manba** ustuni: `2012` = asctt2012.xml, `2008` = asctt2008.xml, `vendor` = boshqa xmlexport fayllari, `lang` = lang.asc'dan topilgan (XML'da yo'q), `roz` = binar fayldan aniqlangan.

### 1.1 `periods` — dars soatlari (kun ichidagi pozitsiyalar)

| Maydon | Tip | Majburiy | Izoh | Manba |
|---|---|---|---|---|
| `period` | int | ✔ | Pozitsiya raqami; **PK**. `0` — "nolinchi soat" (aSc'da alohida qo'llab-quvvatlanadi, `#2656 Work with zero periods`) | 2012, 2008 |
| `name` | string | | To'liq nomi ("1-soat") | 2012 |
| `short` | string | | Qisqartma ("1") | 2012 |
| `starttime` | time `HH:MM` | | Boshlanish vaqti | 2012, 2008 |
| `endtime` | time `HH:MM` | | Tugash vaqti | 2012, 2008 |
| `printinsummary` | bool | | `#3749 Print this period in summary timetables` | lang |
| `printinteacher` / `printinclass` / `printinclassroom` | bool | | `#3751/#3752/#3872` | lang |
| `isbreak` | bool | | `#3438 Break`, `#3430 Add break that will be printed between lessons`, `#3506 Name of the break:` | lang |
| `bellsindex` | int | | `#3753 Print in bells:`, `#3764 Show Bells 1 in column headers` — bir nechta qo'ng'iroq jadvali (bells) mavjud | lang |

> `dayperiods` (faqat 2008): `day,period,starttime,endtime` — har kun uchun alohida qo'ng'iroq vaqtlari. 2012'da olib tashlangan (bells mexanizmi bilan almashtirilgan).

### 1.2 `days` / `daysdefs` — kunlar

**2008 (`days`):**

| Maydon | Tip | Izoh |
|---|---|---|
| `day` | int | Kun raqami (0 yoki 1'dan — `daynumbering1` opsiyasiga bog'liq) |
| `name` | string | "Dushanba" |
| `short` | string | "Du" |

**2012 (`daysdefs`)** — kun **to'plamlari** (bitmask):

| Maydon | Tip | Majburiy | Izoh | Manba |
|---|---|---|---|---|
| `id` | string | ✔ | PK | 2012 |
| `days` | bitstring | ✔ | `'1'`/`'0'` belgilaridan iborat satr, uzunligi = haftadagi kunlar soni. `"10000"` = faqat dushanba; `"00000"` = "har qanday kun" (`#3850 Lesson can be on any day`); `"11111"` = har kuni (`#3549 Every day`) | 2012 |
| `name` | string | | "Dushanba" yoki "Har qanday kun" | 2012 |
| `short` | string | | | 2012 |

Kunlar soni 7 dan katta bo'lishi mumkin: `#2654 Timetable for more than 7 days`, `#2655 Day %1`.

### 1.3 `weeksdefs` — haftalar (multi-week cycle)

| Maydon | Tip | Majburiy | Izoh | Manba |
|---|---|---|---|---|
| `id` | string | ✔ | PK | 2012 |
| `weeks` | bitstring | ✔ | Sikl ichidagi haftalar bitmask'i. `"10"` = juft/toq siklda 1-hafta; `"11"` = ikkala hafta | 2012 |
| `name` | string | | `#1376..#1381 First..Sixth week`, `#3408 Here you can rename individual weeks` | 2012 |
| `short` | string | | | 2012 |

2008'da bu `lessons.weeks` ustuni edi (to'g'ridan-to'g'ri bitmask, alohida entity emas).

### 1.4 `termsdefs` — choraklar/semestrlar

| Maydon | Tip | Majburiy | Izoh | Manba |
|---|---|---|---|---|
| `id` | string | ✔ | PK | 2012 |
| `terms` | bitstring | ✔ | Choraklar bitmask'i. `"100"` = 1-chorak; `"111"` = butun yil (`#3415 Whole year`) | 2012 |
| `name` | string | | `#3395 Term %d`, `#3427 Define terms` | 2012 |
| `short` | string | | `#3397 T%d` | 2012 |

`roz` faylida ko'rindi: Demo1.roz ichida `Tretina 1.`, `Tretina 2.`, `Tretina 3.` (slovakcha "chorak") — termsdefs yozuvlari nom+qisqartma juftligi bilan saqlanadi.

### 1.5 `subjects` — fanlar

| Maydon | Tip | Majburiy | Izoh | Manba |
|---|---|---|---|---|
| `id` | string | ✔ | PK | 2012, 2008 |
| `name` | string | ✔ | `#1013 Subject title :` | 2012, 2008 |
| `short` | string | ✔ | `#1014 Short :`; UI'da `#3734 Prefill short names with names` | 2012, 2008 |
| `partner_id` | string | | Tashqi tizim (SIS) identifikatori — sinxronizatsiya uchun | 2012 |
| `color` | rgb | | `roz`'da RGB sifatida saqlanadi | roz, lang `#1045` |
| `needshomework` | bool | | `#1018 Homework preparation required`, `#2334 which subjects require home preparation` | lang |
| `canbejoined` | bool | | `#2107 Can be joined` (o'rinbosarlikda darslarni birlashtirish) | lang |
| `maxstudents` | int | | `#3511 Max students on lesson with this subject:` | lang |
| `distribution` | enum | | `#3720 Distribution`: `#3722 No dist.` / `#3723 Low` / `#3724 Medium` / `#3725 Ideal` / `#3726 Ideal/no cons.` — haftaga tarqatish rejimi. **`roz` faylida `#3375 Distribution` custom field sifatida har bir fanga yozilgan** | roz, lang |
| `picture` | blob/path | | `#3155 Print subject pictures` | lang |
| `istemporary` | bool | | `#4025 Temporary subject` | lang |
| `minutesperweek` | int | | `#3574 Allow inputing subjects in minutes per week` | lang |

### 1.6 `teachers` — o'qituvchilar

| Maydon | Tip | Majburiy | Izoh | Manba |
|---|---|---|---|---|
| `id` | string | ✔ | PK | 2012, 2008 |
| `name` | string | ✔ | To'liq ism (`#1061 Name of teacher :`) | 2012, 2008 |
| `short` | string | ✔ | Qisqartma | 2012, 2008 |
| `firstname` | string | | `#3657 First name` | 2012 |
| `lastname` | string | | `#3658 Last name` | 2012 |
| `gender` | enum `M`/`F` | | `#3516 Gender`, `#3517 Female` | 2012, 2008 |
| `color` | rgb | | Kartochka rangi | 2012, 2008 |
| `email` | string | | `#3768 E-mail` | 2012 |
| `mobile` | string | | `#3769 Phone` | 2012 |
| `partner_id` | string | | Tashqi ID | 2012 |
| `contract` / `lessonsincontract` | int | | `#1008 Contracts`, `#1057 Contract`, `#3313 Teacher's contract`, `#3512 Length for teacher's contract:`, `#4033 Contract overview` | lang |
| `totallessons` | int (derived) | | `#3314 Total teacher's lessons` | lang |
| `overtime` | int (derived) | | `#3315 Overtime lessons` | lang |
| `cansubstitute` | bool | | `#1472 Cannot substitute` | lang |
| `substitutionminimum` | int | | `#2948 Substituting minimum:` | lang |
| `points` | int | | `#2693 Points`, `#2703 Points (this year)`, `#2706 Offset points:` — o'rinbosarlik adolatliligi uchun | lang |
| `islocked` | bool | | `#2709 Lock teacher` | lang |
| `isresource` | bool | | `#2700 Resource teacher` | lang |
| `nameformat` | enum | | `#3656 Name format` (Ism Familiya / Familiya Ism) | lang |
| `designid` | fk | | `#3124 Design` — shaxsiy chop etish dizayni | roz, lang |

> **`roz` binar tasdiqlash:** o'qituvchi yozuvi ketma-ketligi `[int maydonlar][8-baytli hash/GUID][len+name][len+short][~1400 bayt time-off/constraint bloki][len+firstname][len+lastname]` — ya'ni **time-off matritsasi o'qituvchi yozuvining ichida** saqlanadi.

### 1.7 `classrooms` — xonalar

| Maydon | Tip | Majburiy | Izoh | Manba |
|---|---|---|---|---|
| `id` | string | ✔ | PK | 2012, 2008 |
| `name` | string | ✔ | `#1095 Classroom name :` | 2012, 2008 |
| `short` | string | ✔ | | 2012, 2008 |
| `capacity` | int | | `#3491 Classroom capacity`, `#3492 Capacity`, `#3265 Classroom capacity exceeded` | 2012, vendor |
| `partner_id` | string | | | 2012 |
| `buildingid` | fk | | `#3090 Class has to be in one building during the whole day`, `#4133 Currently: %d different buildings` | lang |
| `needssupervision` | bool | | `#3169 This room requires supervision`, `#3121 Room supervision` | lang |
| `nearbyclassrooms` | fk[] | | `#3171 Nearby classrooms`, `#3213 In nearby classroom before OR after` | lang |
| `isshared` | bool | | `#1068 Shared room`, `#1410 Shared classrooms` | lang |

### 1.8 `grades` — parallellar (sinf darajasi)

| Maydon | Tip | Majburiy | Izoh | Manba |
|---|---|---|---|---|
| `grade` | int | ✔ | **2012'da PK** (surrogat `id` yo'q) | 2012 |
| `id` | string | | 2008'da PK bo'lgan, 2012'da olib tashlangan | 2008 |
| `name` | string | ✔ | "Параллель 10" / "Grade 10" | 2012, 2008 |
| `short` | string | | "G 10" | 2012, 2008 |
| `noofperiodsinweek` | int | | Faqat Oman integratsiyasida | vendor |

Real fayl tasdig'i: `Зиё интелект 2024-2025.roz` ichida 20 ta grade (`Параллель 1` … `Параллель 20`), har biri `name` + `short` juftligi bilan.

### 1.9 `classes` — sinflar

| Maydon | Tip | Majburiy | Izoh | Manba |
|---|---|---|---|---|
| `id` | string | ✔ | PK | 2012, 2008 |
| `name` | string | ✔ | `#1036 Class name :` ("5 А Узб") | 2012, 2008 |
| `short` | string | ✔ | ("5 А") | 2012, 2008 |
| `grade` | int | | FK → `grades.grade`; `#3232 Class level` | 2012, 2008 |
| `teacherid` | fk | | Sinf rahbari (`#1052 Class teacher for the class`, `#1532 Class teacher`) | 2012, 2008 |
| `classroomids` | fk[] | | Vergul bilan ajratilgan **ro'yxat** — `#1067 Home classroom` | 2012, 2008 |
| `partner_id` | string | | | 2012 |
| `language` | string | | `roz`'da `#3956 Language` custom field sifatida saqlangan (real maktabda har bir sinfda mavjud) | roz |
| `printsubjectprefix` | string | | `#3487 Printout prefix` | lang |
| `designid` | fk | | Chop etish dizayni | roz |
| `studentcount` | int (derived) | | `#3926 Student count in class` | lang |

### 1.10 `groups` — sinf ichidagi guruhlar (divisions)

| Maydon | Tip | Majburiy | Izoh | Manba |
|---|---|---|---|---|
| `id` | string | ✔ | PK | 2012, 2008 |
| `classid` | fk | ✔ | FK → `classes.id` | 2012, 2008 |
| `name` | string | ✔ | "Весь класс" / "Entire class", "1 группа", "Мальчики" | 2012, 2008 |
| `entireclass` | bool (`0`/`1`) | ✔ | `1` — bu guruh butun sinfni ifodalaydi | 2012, 2008 |
| `divisiontag` | int | ✔ | **Bo'linish (division) raqami.** Bir xil `divisiontag`ga ega guruhlar bitta bo'linishga tegishli va **bir vaqtning o'zida** dars o'tishi mumkin | 2012, 2008 |
| `studentcount` | int | | Guruhdagi o'quvchilar soni | 2012, 2008 |
| `studentids` | fk[] | | 2012'da qo'shilgan — guruh a'zolari | 2012 |

`roz` tasdig'i: har bir sinfda **standart 5 ta guruh** avtomatik yaratiladi:
`Весь класс` (entireclass=1, divisiontag=0), `1 группа` + `2 группа` (divisiontag=1), `Мальчики` + `Девочки` (divisiontag=2).
Foydalanuvchi qo'shimcha bo'linish qo'sha oladi (`#1065 Add division`, `#1192 Define divisions`, `#1894 add a new division with groups 'Beginners' and 'Advanced'`).

### 1.11 `students` — o'quvchilar

| Maydon | Tip | Majburiy | Izoh | Manba |
|---|---|---|---|---|
| `id` | string | ✔ | PK | 2012, 2008 |
| `classid` | fk | ✔ | FK → `classes.id` | 2012, 2008 |
| `name` | string | ✔ | `#3308 Student's name` | 2012, 2008 |
| `firstname` | string | | | 2012 |
| `lastname` | string | | | 2012 |
| `number` | string | | `#3678 Number(code)` — jurnal raqami | 2012 |
| `email` | string | | | 2012 |
| `mobile` | string | | | 2012 |
| `gender` | enum | | vendor (agenda_sk) | vendor |
| `partner_id` | string | | | 2012 |

### 1.12 `studentsubjects` — o'quvchi fan tanlovi (seminar/elective)

**2012'da yangi.** O'quvchi–fan bog'lanishi (many-to-many + qo'shimcha atributlar).

| Maydon | Tip | Majburiy | Izoh | Manba |
|---|---|---|---|---|
| `studentid` | fk | ✔ | FK → `students.id` | 2012, vendor |
| `subjectid` | fk | ✔ | FK → `subjects.id` | 2012, vendor |
| `seminargroup` | int/string | | Seminar seksiya raqami — `#3646 Seminar section`, `#3529 Section number` | 2012, vendor |
| `importance` | enum | | `#3493 Student must have subject` / `#3494 Student should not have subject` / `#3495 Student should have subject or alternative`; `#3309 Optional/Optimize` | 2012 |
| `alternatefor` | fk | | `#3310 Alternative to` — muqobil fan (`#3935 Using alternative instead of preferred subject`) | 2012 |

### 1.13 `lessons` — darslar (specification)

**Eng markaziy entity.** Bir `lesson` = "shu fan, shu sinf(lar)/guruh(lar), shu o'qituvchi(lar)ga haftada N soat" degan **talab**.

| Maydon | Tip | Majburiy | Izoh | Manba |
|---|---|---|---|---|
| `id` | string | ✔ | PK | 2012, 2008 |
| `subjectid` | fk | ✔ | FK → `subjects.id` | 2012, 2008 |
| `classids` | fk[] | ✔ | **Ro'yxat** — bir nechta sinf birlashtirilishi mumkin (`#3845 Joined classes`, `#3111 Join classes`) | 2012, 2008 |
| `groupids` | fk[] | | **Ro'yxat** — qaysi guruhlar uchun | 2012, 2008 |
| `teacherids` | fk[] | | **Ro'yxat** — bir darsda bir nechta o'qituvchi (team teaching); bo'sh bo'lsa `#3118 Without teacher`, `#3570 Teacher to be decided later` | 2012, 2008 |
| `classroomids` | fk[] | | Ruxsat etilgan xonalar to'plami | 2012, 2008 |
| `studentids` | fk[] | | **Faqat 2008** — 2012'da `groups.studentids` + `studentsubjects` ga ko'chirilgan | 2008, vendor |
| `periodspercard` | int | ✔ | Bitta kartochka necha soatdan iborat: `1` = single, `2` = double (`#1645 Double`), `3` = triple (`#1646 Triple`) | 2012, 2008 |
| `periodsperweek` | decimal | ✔ | Haftada jami necha soat (`#1075 Lessons/week`). Kasr bo'lishi mumkin — shuning uchun `decimalseparatordot` opsiyasi mavjud | 2012, 2008 |
| `daysdefid` | fk | | FK → `daysdefs.id` — dars qaysi kunlarda bo'lishi mumkin | 2012 |
| `weeksdefid` | fk | | FK → `weeksdefs.id` | 2012 |
| `termsdefid` | fk | | FK → `termsdefs.id` | 2012 |
| `weeks` | bitstring | | **Faqat 2008** — `weeksdefid` bilan almashtirilgan | 2008 |
| `seminargroup` | int | | Seminar seksiyasi | 2012, vendor |
| `capacity` | int | | `#3496 Lesson capacity`, `#3616 Set max students on lesson:` | 2012, vendor |
| `partner_id` | string | | | 2012 |
| `classroomcount` | int | | `#2554 This lesson requires, this number of classroom(s):` | lang |
| `durationinminutes` | int | | `#3162 Minutes`, `#3629/#3630 Min/Max total minutes per week` | lang |

### 1.14 `cards` — joylashtirilgan kartochkalar (schedule)

Bir `lesson` → bir nechta `card`. Kartochka = darsning **jadvaldagi aniq o'rni**.

**2012:**

| Maydon | Tip | Majburiy | Izoh | Manba |
|---|---|---|---|---|
| `lessonid` | fk | ✔ | FK → `lessons.id` | 2012, 2008 |
| `period` | int | ✔ | Boshlanish pozitsiyasi | 2012, 2008 |
| `days` | bitstring | ✔ | Qaysi kun(lar)da — odatda aynan bitta `1` (`"00100"` = chorshanba) | 2012 |
| `weeks` | bitstring | ✔ | Qaysi hafta(lar)da | 2012 |
| `terms` | bitstring | ✔ | Qaysi chorak(lar)da | 2012 |
| `classroomids` | fk[] | | **Yakuniy tayinlangan xona(lar)** (lesson'dagi `classroomids` — ruxsat etilganlar to'plami, card'dagi — tanlangani) | 2012, 2008 |
| `day` | int | | **Faqat 2008** — `days` bitmask bilan almashtirilgan | 2008 |
| `locked` | bool | | `#1618 Lock` / `#1707 Lock`; qulflangan kartochka generatsiyada ko'chirilmaydi | lang |

`sakhr_om.xml` denormalizatsiyalangan card variantini ko'rsatadi: `day,period,subjectid,teacherid,classroomid,classids,studentids,lessonid` — ya'ni eksportda `lessons` orqali JOIN qilinadigan maydonlar to'g'ridan-to'g'ri kartochkaga yoziladi.

### 1.15 `classsubjects` / `groupsubjects` — "lesson grid" (dars to'ri)

`lessons`ning soddalashtirilgan ko'rinishi; import uchun ishlatiladi.

| Entity | Ustunlar | Manba |
|---|---|---|
| `classsubjects` | `classid,subjectid,periodsperweek,teacherid` | `Import Samples/XML/import_basicdata+lessongrid.xml` |
| `groupsubjects` | `id,classids,subjectid,teacherids[,studentids][,periodsperweek][,groupids]` | `agenda_sk.xml`, `dinaplo_hu.xml`, `export_basicdata+lessons+timetable.xml` |

lang: `#3107 Lesson grid`, `#3958 Lessons per week`, `#3960 Lessons for class`, `#3961 Lessons for subject`.

### 1.16 `classtimetables` — denormalizatsiyalangan jadval eksporti

Faqat `gescola_es.xml`: `classid,day,period,teacherids,subjectids,classroomids`.
Tashqi tizimlarga "tayyor jadval" berish uchun — bizning API'da ham shunga o'xshash **read model** kerak bo'ladi.

### 1.17 XML sxema opsiyalari lug'ati

`<timetable>` darajasida:

| Opsiya | Ma'nosi |
|---|---|
| `importtype="database"` | Import rejimi |
| `defaultexport="1"` | Standart eksport formati |
| `displayname`, `displaycountries`, `displayinmenu` | UI'da ko'rsatish |
| `groupstype1` | Guruhlarni 1-tipda ifodalash |
| `decimalseparatordot` | `periodsperweek` uchun `.` ajratkich |
| `daynumbering1` | Kunlar 1'dan raqamlanadi (0'dan emas) |
| `idprefix:X` | Barcha ID'larga prefiks |
| `export:idprefix:%CHRID` / `import:idprefix:%TEMPID` | 2012'dagi ID mapping mexanizmi |
| `lessonsincludeclasseswithoutstudents` | O'quvchisiz sinflarni ham darsga qo'shish |
| `handlestudentsafterlessons` | O'quvchilarni darslardan **keyin** qayta ishlash (import tartibi) |

Har bir entity darajasida: `canadd`, `canremove`, `canupdate`, `import:disable`, `export:disable`, `primarytt`, `silent`, `export:silent`.

### 1.18 asctt2008 → asctt2012 farqlari (xulosa)

| O'zgarish | Tafsilot |
|---|---|
| `days` + `dayperiods` → `daysdefs` | Kunlar ro'yxati o'rniga kun **to'plamlari** (bitmask) |
| yangi `weeksdefs` | Ko'p haftalik sikl birinchi darajali entity bo'ldi |
| yangi `termsdefs` | Chorak/semestr qo'llab-quvvatlash |
| yangi `studentsubjects` | O'quvchi fan tanlovi (seminar/elective) — `lessons.studentids` o'rniga |
| `grades`: `id,name,short,grade` → `grade,name,short` | `grade` (int) PK bo'ldi |
| `lessons`: `weeks` → `daysdefid,weeksdefid,termsdefid` | Vaqt cheklovlari normalizatsiya qilindi |
| `lessons`: `+seminargroup,+capacity,+partner_id`, `−studentids` | Seminar va sig'im qo'shildi |
| `cards`: `day` → `days,weeks,terms` | Kartochka pozitsiyasi 3 o'lchovli bo'ldi |
| `teachers`: `+email,+mobile,+firstname,+lastname,+partner_id` | |
| `classrooms`: `+capacity,+partner_id` | |
| `students`: `+number,+email,+mobile,+firstname,+lastname,+partner_id` | |
| `groups`: `+studentids` | |
| `periods`: `+name,+short` | |
| `partner_id` hamma joyda | Tashqi SIS bilan sinxronizatsiya (`#3174 Database`, `#3175 Synchronization with database`) |

### 1.18-bis Barcha entity va ustun nomlarining birlashmasi (quick reference)

**Barcha jadval nomlari** (7 ta xmlexport + 6 ta Import Samples faylidan):
```
days, periods, dayperiods, daysdefs, weeksdefs, termsdefs,
teachers, subjects, classrooms, grades, classes, groups, students,
studentsubjects, classsubjects, groupsubjects,
lessons, cards, classtimetables
```

**Barcha ustun nomlari:**
```
id, name, short, firstname, lastname, partner_id
gender, color, email, mobile, number
day, days, period, starttime, endtime, weeks, terms
grade, gradeid, noofperiodsinweek
capacity, studentcount, entireclass, divisiontag
classid, classids, subjectid, subjectids, teacherid, teacherids,
classroomid, classroomids, groupids, studentids, studentid, lessonid,
daysdefid, weeksdefid, termsdefid
periodspercard, periodsperweek, durationperiods
seminargroup, importance, alternatefor
```

> To'liq rasmiy ustunlar katalogi `sample.xml` da havola qilingan (`help.asctimetables.com`), lekin offline mavjud emas. Yuqoridagi ro'yxat — o'rnatilgan fayllardagi **hammasi**.

### 1.19 Chop etish va eksport uchun kerak bo'ladigan maydonlar (denormalizatsiya talablari)

Bu bo'lim `designs/*/def.xml`, `template/Web/*.htm`, `template/excelexport/*.xml` va `template/Import Samples/*` tahlilidan olingan — ya'ni **read model** loyihalashda qaysi maydonlar oldindan JOIN qilingan holda kerakligini ko'rsatadi.

#### a) Print design token'lari (`designs/*/def.xml`)

Fayl ildizi nomsiz `<>` … `</>` elementi (haqiqiy XML emas — aSc'ning o'z parseri). Bolalari: `<head version name rtl/>`, `<PrintObject m_nTyp="0|1">`, `<PrintObjectLegend m_nTyp="3">`, `<TimeTableSettings>`.

Ikki xil token sintaksisi:
- `{!#NNNN}` — `lang.asc` dan tarjima qilingan **yorliq** (label).
- `{#ENTITY:#FIELD}` — **ma'lumot maydoni**. `{#FIELD}` yolg'iz = joriy kontekst obyektining maydoni.

| Token | Entity ID | Field ID | Ma'nosi |
|---|---|---|---|
| `{#1635}` | joriy | 1635 = Name | Joriy obyekt nomi |
| `{#1035:#1635}` | 1035 = Class | Name | Sinf nomi |
| `{#1048:#1635}` | 1048 = Teacher | Name | O'qituvchi nomi |
| `{#1035:#1067}` | Class | 1067 = Home classroom | **Sinfning uy xonasi** (denormalizatsiya!) |
| `{#1035:#1532}` | Class | 1532 = Class teacher | **Sinf rahbari nomi** (denormalizatsiya!) |
| `{#3148:#1166}` | 3148 = School | 1166 = School name | Maktab nomi |
| `{#3148:#4055}` | School | 4055 = School logo | Maktab logotipi (rasm) |

Ya'ni **entity ID va field ID ham `lang.asc` string ID'lari** — bir xil mexanizm custom field'larda ham ishlatiladi (4.7-bo'lim).

Legend (afsona) turlari — `m_LegendaType`:

| Qiymat | Ro'yxat |
|---|---|
| `0` | Subjects |
| `2` | Classrooms |
| `3` | Teachers |
| `8` | Lessons table (guruhlangan/yig'indili hisobot) |

Legend ustunlari `<Reportdata><columns><column nID nWidth strUserHeaderName strUserFooterName/>`:

| `nID` | Ma'nosi |
|---|---|
| `1` | To'liq nom (Name) |
| `2` | Qisqartma (Short) |
| `3` | Tor ustun — rang/raqam |
| `6` | O'qituvchiga xos qo'shimcha |
| `28000` | Subject (header `#1021`, footer `#1075 Lessons/week`) |
| `28001` | Teacher |
| `28004` | Soatlar soni (header `#1266 Count`, footer `{sum}`) |
| `28006` | Classroom(s) |
| `28008` | Class/Group |

Footer agregat tokeni: `{sum}`.

Uslub (style) atributlari `.roz` faylida ham saqlanadi: `font/fontheader/fontfooter` (`ratio,width,weight,escapement,italic,underline,strikeout,pitchAndFamily,faceName`), `m_bgColor,m_fontColor,m_lineColor,m_lineTop/Bottom/Left/Right/Middle,m_bAutoFont,m_bOnTop`; `TimeTableSettings` → `m_ColorRectRowHeader/ColumnHeader/Card/Break/Vnutro` (har biri `padding*`, `rgbBackColor1/2`, `rgbColorFont`, `strPicture`).

> **Muhim:** kartochka (card) ichidagi matn tartibi (qaysi qatorda fan, o'qituvchi, xona) `def.xml` da **yo'q** — u `.roz` hujjatining o'zida saqlanadi.

#### b) Web (HTML) eksport token'lari — `template/Web/*.htm`

Bu buildda **JS ma'lumot massivlari yo'q** — faqat o'rinbosarlik hisobotlari (`{TOKEN}` shablonlari) va Flash/Flex viewer (`fl/swfcombiner.swf`).

```
{CHARSET} {INSERTTABLE} {CREATEDBY} {DateTime} {SchoolName}
{dayname} {date} {note} {MissingTeachers}
{STUDENT} {substStudentsHTML} {substTeachersHTML}
```
Yorliqlar: `{!#1474!}` Substitution, `{!#1503!}` Day, `{!#2087!}` Information for students, `{!#1038!}` Teachers, `{!#2956!}` Student. (Diqqat: bu yerda token oxirida `!` bor, design'larda yo'q.)

#### c) Excel eksport token'lari — `template/excelexport/*.xml` (SpreadsheetML 2003)

Tuzilma token'lari: `{*header} {*separator} {*firstrow} {*repeat} {*lastrow}`.

**Ma'lumot token'larining to'liq ro'yxati** — bu bizning `timetable_slot` / hisobot read-model'imiz uchun to'g'ridan-to'g'ri talablar:

```
{SCHOOL NAME} {SCHOOL YEAR}
{nr} {class} {class_teacher} {home_classroom}
{teacher} {teachers} {subject} {group}
{count} {length} {total} {totallessons}
{classrooms} {classes} {weeks} {terms}
{date} {month} {period} {reason} {type} {points} {totalpointsyear}
{day_name} {room_name} {seminar_group} {positions}
{student_count} {student_name} {student_class} {student_group} {student_number}
{cf:<key>}                                  -- custom field (masalan {cf:tc}, {cf:sk})
{hours_day0..4} {egze_day0..4} {seflik_day0..4} {Koor_day0..4}   -- kun bo'yicha pivot
{spvision_/0} {spvision_0} {spvision_0/1} ... {spvision_12}      -- nazorat slotlari
{$/0} {$0} {$0/1} ... {$12}                                      -- slot sarlavhalari
```

Nazorat slot nomlash konvensiyasi: `N` = N-soat, `N/M` = N va M soat **orasidagi tanaffus**, `/0` = 0-soatdan oldin.

Hisobot ustunlari tartibi (kontrakt hisoboti — `export_contracts_template.xml`):
- sinf bo'yicha: `nr, teacher, subject, group, count, length, total, classrooms`; ajratkich qator: `{class} {class_teacher} {home_classroom}` + `{totallessons}`
- o'qituvchi bo'yicha: `nr, class, subject, group, count, length, total, classrooms` + `{totallessons}`
- hafta/chorak bilan: yuqoridagilarga `weeks, terms` qo'shiladi

O'rinbosarlik hisoboti (`export_supl_template.xml`): `nr, date, period, class, subject, reason, type, points`; ajratkich `{month}`.
Xona nazorati (`room_supervisions_template.xml`): `day_name, room_name` + 25 ta nazorat sloti.
O'quvchi hisobotlari: `student_count, student_name, student_class[, student_group]`.

**Xulosa — bizning API/read-model'da oldindan JOIN qilingan holda kerak:**
`class_teacher` (nom), `home_classroom` (nom), `teachers` (vergul bilan birlashtirilgan nomlar), `classes`, `classrooms`, `group`, `positions`, `seminar_group`, hamda agregatlar: `count` (kartochkalar soni), `length` (`periodspercard`), `total` (soatlar), `totallessons` (o'qituvchi/sinf bo'yicha yig'indi), `totalpointsyear`.

#### d) Clipboard/Excel import ustunlari — `template/Import Samples/Clipboard_Excel/sample_clip.xls`

Bu **eng sodda import formati** — bizning UI'da ham shunga o'xshash "Excel'dan qo'yish" imkoniyati kerak.

| Sheet | Ustunlar |
|---|---|
| `Lessons - Simple` | `Teachers, Class, Subject, Length, Total periods per week` |
| `Classes` | `Name, Short, Divisions` |
| `Lesson with Groups` | `Teachers, Class, Subject, Length, Lessons per week, Total periods per week, Groups` |
| `Lessons with Classrooms` | `Teachers, Class, Subject, Length, Total periods per week, Classrooms` |

Sintaksis qoidalari:
- Ko'p qiymatli katak — **vergul** bilan (`1.A,1.B`).
- `Divisions` ustuni: bir bo'linish ichidagi guruhlar **`/`** bilan, bo'linishlar **`,`** bilan: `Boys/Girls,Group1/Group2` — bu 3.2-bo'limdagi `divisiontag` mexanizmining matnli ko'rinishi.
- `Classrooms` katagida maxsus qiymatlar: `Home classroom`, `Shared classroom`, `Subject classroom`, `Teacher classroom` yoki haqiqiy xona nomlari.

#### e) O'quvchi tanlovlari importi — `Students_picks/SeminarImportTemplate.xls`

Matritsa (nomli ustunlar emas): `col0 = o'quvchi ismi`, `col1 = sinf`, `col2 = bo'sh`, `col3.. = har bir seminar uchun bitta ustun`. Katak qiymati: `X` = tanlangan, `1`/`2` = ustuvorlik / muqobil darajasi. Bu `student_subject.importance` va `alternate_for_subject_id` ga to'g'ridan-to'g'ri mos keladi.

---

## 2. Munosabatlar (ER diagramma)

```mermaid
erDiagram
    SCHOOL ||--o{ ACADEMIC_YEAR : has
    ACADEMIC_YEAR ||--o{ PERIOD : defines
    ACADEMIC_YEAR ||--o{ DAYSDEF : defines
    ACADEMIC_YEAR ||--o{ WEEKSDEF : defines
    ACADEMIC_YEAR ||--o{ TERMSDEF : defines
    ACADEMIC_YEAR ||--o{ SUBJECT : has
    ACADEMIC_YEAR ||--o{ TEACHER : has
    ACADEMIC_YEAR ||--o{ CLASSROOM : has
    ACADEMIC_YEAR ||--o{ GRADE : has
    ACADEMIC_YEAR ||--o{ CLASS : has

    GRADE   ||--o{ CLASS : groups
    TEACHER |o--o{ CLASS : "class teacher"
    CLASS   ||--o{ CLASSROOM_OF_CLASS : "home rooms"
    CLASSROOM ||--o{ CLASSROOM_OF_CLASS : ""
    BUILDING ||--o{ CLASSROOM : contains

    CLASS   ||--o{ GROUP : "divided into"
    GROUP   }o--|| DIVISION : "belongs to (divisiontag)"
    CLASS   ||--o{ DIVISION : has
    CLASS   ||--o{ STUDENT : enrolls
    GROUP   ||--o{ STUDENT_GROUP : ""
    STUDENT ||--o{ STUDENT_GROUP : ""

    STUDENT ||--o{ STUDENT_SUBJECT : picks
    SUBJECT ||--o{ STUDENT_SUBJECT : ""

    LESSON  }o--|| SUBJECT : teaches
    LESSON  ||--o{ LESSON_CLASS : ""
    CLASS   ||--o{ LESSON_CLASS : ""
    LESSON  ||--o{ LESSON_GROUP : ""
    GROUP   ||--o{ LESSON_GROUP : ""
    LESSON  ||--o{ LESSON_TEACHER : ""
    TEACHER ||--o{ LESSON_TEACHER : ""
    LESSON  ||--o{ LESSON_CLASSROOM : "allowed rooms"
    CLASSROOM ||--o{ LESSON_CLASSROOM : ""
    LESSON  }o--o| DAYSDEF : "daysdefid"
    LESSON  }o--o| WEEKSDEF : "weeksdefid"
    LESSON  }o--o| TERMSDEF : "termsdefid"

    LESSON  ||--o{ CARD : "scheduled as"
    CARD    }o--|| PERIOD : "starts at"
    CARD    ||--o{ CARD_CLASSROOM : "assigned room"
    CLASSROOM ||--o{ CARD_CLASSROOM : ""

    TEACHER ||--o{ TIME_OFF : "unavailable"
    CLASS   ||--o{ TIME_OFF : ""
    CLASSROOM ||--o{ TIME_OFF : ""
    SUBJECT ||--o{ TIME_OFF : ""

    CONSTRAINT }o--o| TEACHER : "applies to"
    CONSTRAINT }o--o| CLASS : ""
    CONSTRAINT }o--o| SUBJECT : ""
    CONSTRAINT }o--o| CLASSROOM : ""
    CARD_RELATION }o--o{ LESSON : "relates"

    TEACHER ||--o{ ABSENCE : has
    ABSENCE ||--o{ SUBSTITUTION : causes
    CARD    ||--o{ SUBSTITUTION : "for card"
    TEACHER ||--o{ SUBSTITUTION : "substitutes"
    ABSENCE_REASON ||--o{ ABSENCE : ""
    SUBSTITUTION_TYPE ||--o{ SUBSTITUTION : ""
```

---

## 3. Kalit tushunchalar

### 3.1 `lesson` vs `card`

| | `lesson` | `card` |
|---|---|---|
| Ma'nosi | **Talab** (specification): "5-A sinfga matematika haftada 5 soat, Ivanov o'qituvchi" | **Natija** (schedule): "bu dars chorshanba 3-soatda, 204-xonada" |
| Yaratilishi | Foydalanuvchi qo'lda kiritadi / import qiladi | Generator (yoki qo'lda sudrab) joylashtiradi |
| Soni | 1 ta | `periodsperweek / periodspercard` ta |
| O'chirilishi | Ma'lumot yo'qoladi | `#1620 Remove timetable` — jadval tozalanadi, darslar qoladi |

**Muhim:** `lessons.classroomids` = **ruxsat etilgan** xonalar to'plami (constraint), `cards.classroomids` = **tayinlangan** xona (natija). Bu ikki xil semantika, bitta nom. Bizning sxemada ularni `lesson_allowed_classroom` va `card_classroom` deb ajratish kerak.

Kartochka joylashmagan bo'lishi mumkin: `#1372 Unassigned cards`, `#2770 Cards left:`, `#3498 Pending card`, `#3499 Card does not have a classroom assigned`.

### 3.2 `group` / `division` mexanizmi

Bu aSc'ning eng nozik joyi.

- Sinf bir nechta **bo'linishga** (division) ega bo'lishi mumkin: `#1893 Each class can contain several divisions of students`.
- Har bir bo'linish sinfni **guruhlarga** ajratadi (masalan: `Boys`/`Girls`, yoki `Beginners`/`Advanced`, yoki `Group 1`/`Group 2`).
- XML'da bo'linish alohida entity emas — `groups.divisiontag` (int) orqali ifodalanadi. Bir xil `divisiontag`ga ega guruhlar = bitta bo'linish.
- **Asosiy qoida:** *"Only groups from the same division can have lessons at the same time"* (`#1895`). Ya'ni `Boys` va `Advanced` bir vaqtda dars o'ta olmaydi, chunki ular turli bo'linishlarga tegishli — o'quvchilar to'plami kesishadi.
- `entireclass=1` bo'lgan guruh butun sinfni ifodalaydi (`divisiontag=0`) va **hech qanday** boshqa guruh bilan parallel bo'la olmaydi.
- `#3191 Combine divisions` — bo'linishlarni birlashtirish; `#3620 Show divisions together`.

`roz` faylidan tasdiq (real maktab): har bir sinfda avtomatik 5 guruh — `Весь класс` (dt=0), `1 группа`/`2 группа` (dt=1), `Мальчики`/`Девочки` (dt=2).

**Modellashda:** `division` ni alohida jadval qilish tavsiya etiladi (`divisiontag` ni surrogat kalitga aylantirish), chunki bo'linish darajasida nom va cheklov saqlash kerak bo'ladi.

### 3.3 `seminargroup` / seminar (elective) mexanizmi

Yuqori sinflarda o'quvchi fanni **tanlaydi**, sinf bo'yicha emas:

1. `studentsubjects` — o'quvchi qaysi fanlarni tanlagani (`importance`, `alternatefor` bilan).
2. Dastur o'quvchilarni **seksiyalarga** (`seminargroup` / `#3646 Seminar section`) taqsimlaydi: `#3667 Assign students to seminars`, `#3040 Rearrange students in seminar sections`, `#3622 Seminar section count`.
3. Har bir seksiya uchun `lesson` yaratiladi (`lessons.seminargroup`).
4. `lessons.capacity` — seksiyaga sig'adigan maksimal o'quvchilar soni.
5. Cheklovlar: `#3864 Student must have seminar A in a term before seminar B`, `#3866/#3867 same/not same term`, `#3767 Student of these seminars must have the same teacher`, `#4026 Allow student to be in different sections in different terms`.

`#3842 TimeTable based on student's choices (Master)` vs `#3843 (Classes/Grades)` — ikki xil rejim.

### 3.4 `daysdef` / `weeksdef` / `termsdef` bit-mask semantikasi

Bitta uzunlikdagi `'0'`/`'1'` satr; **chapdan o'ngga**, indeks 0 = birinchi kun/hafta/chorak.

| Qiymat | Ma'nosi (5 kunlik hafta) |
|---|---|
| `"10000"` | Faqat dushanba |
| `"00100"` | Faqat chorshanba |
| `"11111"` | Har kuni (`#3549 Every day`) |
| `"00000"` | Cheklov yo'q — istalgan kun (`#3850 Lesson can be on any day`, `#3550 Any day`) |
| `"11000"` | Dushanba yoki seshanba (`#3641 Lesson will be placed in one of the selected days`) |

Xuddi shu mantiq `weeks` (`#3639 ... one of the selected weeks`, `#2646 Any week` / `#2647 Specific week`) va `terms` (`#3638 ... one of the selected terms`, `#3441 Any term`) uchun.

- **`lessons.daysdefid`** = dars **qaysi kunlarda bo'lishi mumkin** (cheklov).
- **`cards.days`** = kartochka **aynan qaysi kunda turibdi** (odatda bitta `1`).

`weeks` uzunligi = sikldagi haftalar soni (`#4124 Currently: %d week(s)`), `terms` uzunligi = choraklar soni (`#4126 Currently: %d terms(s)`). Kunlar soni 7 dan katta bo'lishi mumkin (`#2654 Timetable for more than 7 days`).

### 3.5 `periodspercard` vs `periodsperweek`

- `periodsperweek` — dars haftada jami necha soat (`#1075 Lessons/week`). **Decimal** bo'lishi mumkin (multi-week siklda "2 hafta ichida 3 soat" = 1.5).
- `periodspercard` — bitta uzluksiz blok necha soatdan iborat: 1 = single, 2 = double (`#1645`), 3 = triple (`#1646`).
- **Kartochkalar soni = `periodsperweek / periodspercard`**.
- Aralash holat: aSc'da bitta fan uchun bir nechta `lesson` yaratiladi (masalan 1×double + 1×single = haftada 3 soat).
- Bog'liq cheklovlar: `#2705 Double lessons cannot span this break`, `#2744 Doublelessons can span over 'long breaks'`, `#4044 Can split one double lesson`, `#2658 Class %1 cannot be completed because of double-lessons`, `#3655 Print consecutive single lessons as one merged lesson`.

### 3.6 `grade` vs `class`

`grade` = parallel/daraja (10-sinflar), `class` = aniq sinf (10-A). `classes.grade` → `grades.grade`. Cheklovlarni butun parallelga qo'llash mumkin: `#3682 Apply to grades of selected classes`, `#3892 Apply to selected teachers' grades`.

---

## 4. Qo'shimcha ma'lumotlar — aSc'da bor, XML eksportda YO'Q

XML eksport sxemasi faqat **asosiy ma'lumotlarni** (basic data + lessons + cards) qamrab oladi. `lang.asc` va `.roz` tahlili quyidagi katta bloklar eksport qilinmasligini ko'rsatdi.

### 4.1 Time-off (vaqt bo'yicha bandlik) matritsalari

Har bir `teacher`, `class`, `classroom`, `subject`, `group` uchun **kun × soat × hafta × chorak** o'lchamli matritsa saqlanadi. Har bir katak qiymati — ruxsat darajasi:

- `#1033 Time off`, `#1778 Time off:`, `#1501 Duties and time-off`
- `#3468 Forbidden position in Time-off`
- `#3500 Question marked positions` — "?" belgisi = "iloji boricha qo'ymang" (yumshoq taqiq)
- `#3469/#3470 Max question marked periods per week/day`
- `#1270 Lessons placed in a position not recommended` vs `#1271 ... not permitted` — **kamida 3 daraja**: ruxsat / tavsiya etilmaydi / taqiqlangan

**`.roz` tasdig'i:** o'qituvchi yozuvi ichida ~1400 baytli, har bir katak uchun 4 baytli massiv (qiymatlar `0x04`, `0x06` va h.k.) mavjud — bu aynan time-off matritsasi.

### 4.2 Cheklovlar (constraints) — ular qanday SAQLANADI

aSc cheklovlarni **alohida "constraint" yozuvlari** sifatida saqlaydi, har biri:
`{tip, qo'llanish doirasi (scope), parametrlar, muhimlik (importance), yoqilgan/o'chirilgan}`.

Dalillar: `#3071 Constraints`, `#3326 Grouped constraints`, `#3719 List of inputted constraints`, `#3067 Importance` (`#3064 Normal` / `#3065 Low` / `#3066 High`), `#3311 Disabled`, `#3943 Disable` / `#3944 Enable`, `#3072 Allow relaxation` / `#3073 Strict`, `#2986 Disabled constraint`, `#3226 Relaxed constraints`, `#3945 Help on this constraint`.

**Scope** (kimga qo'llaniladi): `#3028 Global for all:` / `#3029 Only for:` / `#3053 Apply to selected teachers` / `#3054 ... classes` / `#3893 ... subjects` / `#3115 ... classrooms` / `#3880 Apply to groups in selected classes` / `#3681 Apply to students in selected classes` / `#3682 Apply to grades of selected classes` / `#3891 Apply to selected teachers' classes`.

**Cheklov tiplari** (lang.asc'dan to'liq ro'yxat — generatsiya agentiga topshiriladi, bu yerda faqat SAQLASH strukturasi uchun):

| Guruh | Misollar (lang ID) |
|---|---|
| Soatlar soni | `#3453/#3454 Min/Max periods per day`, `#3455/#3456 Max/Min periods per week`, `#3733 Max periods per all weeks/terms`, `#3629/#3630 Min/Max total minutes per week` |
| Kunlar | `#3458/#3459 Max/Min days per week`, `#3471 Max consecutive days`, `#3910/#3911 Max/Min days per all weeks`, `#3268 Maximum free days between education during week` |
| Oynalar (gaps) | `#3460 Max gaps per day`, `#3461 Max gaps per week`, `#3878 Max gaps per all weeks`, `#3479/#3480 Min/Max gap length`, `#3462 Max free days between cards per week`, `#3058/#3059 ... on selected positions` |
| Ketma-ketlik | `#3472 Max consecutive periods`, `#3060 Max consecutive periods of education on selected positions`, `#1217 Max. number of consecutive periods:`, `#2742 Max. number of consequentive free lessons:` |
| Pozitsiya | `#3736/#3737 Card can not start/end on selected positions`, `#2768 Class must start with this hour:`, `#2715 Class must finish before or on lesson:`, `#2714 Class must have lessons in this interval:`, `#3757 Subject must be first or last` |
| Kartochkalar munosabati | `#1400 Card relationships`, `#3473 Cards must follow` (`#3474 in specified order`), `#3475 Cards can not follow`, `#3483 Must be on the same days`, `#3484 Must be on the same positions`, `#3485 Can not be on the same period`, `#3486 Can not be on the same day`, `#3877 Can not be in the same term`, `#3874 Lessons "A" must be before lessons "B" in a week`, `#3916 Gaps in A must be filled with B`, `#3626 "A" lessons can be only on positions of "B" lessons`, `#3759 "A" must be before or after "B" in a day`, `#3477 B must be last in a day` |
| Xonalar | `#3290/#3291 Max different classrooms per day/week`, `#3868 Max classrooms on one period`, `#3917/#3918 Max/Min periods per week in selected classrooms` |
| Binolar | `#3090 Class has to be in one building during the whole day`, `#3091 Number of periods needed to transfer between buildings`, `#3481 Maximum number of transits between the buildings per week`, `#3482 Time for transfer between buildings` |
| Tushlik | `#2641 Lunch`, `#2642 Lunch break must be in following interval:`, `#2712 Groups must have lunch at the same time`, `#3545 Can be over lunch`, `#2643 Forbid placing other lessons after lunch...` |
| Tarqatish | `#3720 Distribution` (No/Low/Medium/Ideal), `#3731 Check distribution within week`, `#2776 Distribute cards into this number of days:`, `#3478 Max days with lesson on the same period`, `#3070 Max number of lessons on the same period per week` |
| Boshqa | `#3971 Max different subjects per day`, `#3869 Max teachers on one period`, `#3098 Max cards on one period`, `#3189 Min cards on single position`, `#3451 Min cards on one period`, `#3240 Max different period numbers per week`, `#3466/#3467 Min/Max students assigned`, `#3837 Max over room capacity:` |

### 4.3 O'rinbosarlik (substitution) moduli — to'liq alohida sxema

Bu **butunlay alohida** ma'lumotlar to'plami (`.ziptt` yonida `.zipsubst` fayli — `backup/autosave_subst_2025_2_5__14_7_STAT0_0S.zipsubst`).

| Entity | Maydonlar (lang.asc'dan) |
|---|---|
| `substitution_timetable` | `#2103 Name of the timetable`, `#1498 Timetable for substitution from :`, `#3743 Valid to`, `#3747 Valid for`, `#1504 Cycle :`, `#3738 File Name`, `#3739 Last Saved`, `#3742 Status`, `#3744 Draft` |
| `absence` | `#1449 Who absent :` (teacher/class/room — `#3779 Absent classes`, `#3780 Absent rooms`), `#1464 Reason of the absence`, `#1451 When does he/she absent:`, `#1453 Longtime absence`, `#1454 Entire day` / `#1455 Part of the day :` + `#1457 From:` / `#1458 Till:`, `#2694 Approved absence` / `#2695 Not approved absence` |
| `absence_reason` | `#3803 Reasons of absence` — foydalanuvchi ta'riflaydigan lug'at |
| `substitution` | `#1477 Absenting :`, `#1481 Substituting :`, `#1483 Type of substitution :`, `#1476 Cancelled`, `#2898 Substituted`, `#3793 Remove substitution`, `#3794 Change other lesson`, `#2645 Joint:` |
| `substitution_type` | `#3802 Types of substitution` — masalan "paid / unpaid / joined" (`#2125`) |
| `substitution_criteria` | `#1530 Criterion` bilan vaznlar: `#1531 Teaches the class`, `#1532 Class teacher`, `#1533 Approbation`, `#1534 Exhaustion`, `#1535 Joining`, `#3488 Position`, `#1538 Equable substitution` |
| `duty` (navbatchilik) | `#1471 Duty`, `#3206 Print duties in summary timetables`, `#3450 Print duties in individual timetables` |
| `daily_remark` | `#3795 Daily remark`, `#2214 Note for the day`, `#3796 Day in timetable` |
| `holidays` | `#3882 Holidays`, `#3936 Holidays` |
| teacher points | `#2693 Points`, `#2703 Points (this year)`, `#2706 Offset points:`, `#4036 Teacher has to do this number of substitutions of this type`, `#4037 per school year`, `#2946 Under substituting minimum` |

### 4.4 Xona nazorati (supervision)

`#3294 Supervision`, `#3295 Supervisions`, `#3121 Room supervision`, `#3122 Add room supervision`, `#3169 This room requires supervision`, `#3653 Print room supervisions in color`, `#3207 More/less supervisions count than defined in teacher's constraints`, `#3211..#3220` — nazorat vaqti qoidalari (`Teacher teaches before OR after`, `On a day without lessons`, `Before first teacher's lesson`, `All on one day`, `Continuously`).
Eksport shabloni mavjud: `template/excelexport/room_supervisions_template.xml`.

### 4.5 O'qituvchi shartnomasi va yuklamasi

`#1008 Contracts`, `#1057 Contract`, `#3313 Teacher's contract`, `#3512 Length for teacher's contract:`, `#3314 Total teacher's lessons`, `#3315 Overtime lessons`, `#4033 Contract overview`, `#4038 Show teachers' contracts when choosing teacher for a lesson`, `#2895 Meant to teach (number of lessons)`, `#2896 Realized lessons`, `#2897 Total realized lessons`.
Eksport shabloni: `template/excelexport/export_contracts_template.xml`.

### 4.6 Binolar (buildings)

XML'da umuman yo'q, lekin cheklovlarda bor: `#3090`, `#3091`, `#3481`, `#3482`, `#4133 Currently: %d different buildings`. Xonalar binolarga bo'linadi, binolar orasida ko'chish vaqti hisobga olinadi. **"Floor/qavat" atamasi topilmadi** — faqat "building".

### 4.7 Custom fields (foydalanuvchi maydonlari) — muhim mexanizm

`#3119 Custom fields`, `#3547 Custom field`, `#3364 Select a custom field:`, `#3125 Add field "%s" to the printout design`.

**`.roz` faylida bevosita kuzatildi:** har bir obyektga `#<langID>` kalitli qo'shimcha maydonlar biriktiriladi. Real maktab faylida:

| Obyekt | Kalit | lang.asc dagi ma'nosi | Qiymat |
|---|---|---|---|
| school | `#1166` | Name of the school | `зиё Интелект` |
| school | `#1167` | Academic year | `2024/2025` |
| school | `#3957` | School code | |
| school | `#3149` / `#3150` | Address: street / city | |
| school | `#3974` | District | |
| school | `#3975` | Headmaster | |
| school | `#3976` / `#3977` | Valid from / Valid to | |
| school | `#3205` | Registration name | |
| school | `#4055` | School logo | |
| **subject** | `#3375` | **Distribution** | har bir fanda (43 marta) |
| **class** | `#3956` | **Language** | har bir sinfda (30 marta) |

Ya'ni aSc'da **EAV (entity-attribute-value)** kengaytma mexanizmi bor. Bizning sxemada ham `custom_field` + `custom_field_value` jadvallari kerak bo'ladi (yoki `jsonb` ustuni).

### 4.8 Chop etish dizaynlari (print designs)

`.roz` fayli ichida dizayn ma'lumotlari **saqlanadi** (shrift nomlari `Arial`, `Times New Roman`, o'lchamlar, ranglar, `report_header`, `class_row1..3`, `teacher_row1..2`, `teacher2_row1..2`, `0|internal_table;`, `0|internal_table_teacher;`).
`#3124 Design`, `#3131 Edit design`, `#3156 Apply this design to:`, `#4057 Standard design`, `#3192 Remove design from all classes/teachers`.
Ya'ni **har bir sinf/o'qituvchi o'z chop etish dizayniga ega bo'lishi mumkin**.

### 4.9 Boshqa saqlanadigan narsalar

| Narsa | Dalil |
|---|---|
| Views (ko'rinishlar) | `#1682 View 1`, `#3303 Modify current view`, `#3532 Save modifications of standard views` |
| Generatsiya parametrlari | `#1335 Parameters`, `#1336 Complexity of generation` (Small/Normal/Large/Huge), `#2732 Allow automatic relaxation`, `#3164 Enable multiprocessor generator`, `#3077 Allow network generators` |
| Fayl paroli | `#3762 Save with password`, `#3760 Incorrect password` |
| Backup | `#3861 Backup type`, `#3862 Automatic backup` / `#3863 Manual backup` — `backup/*.ziptt` |
| Online sinxronizatsiya | `#3683 TimeTables Online`, `#3679/#3680 Save to / Open from TimeTables Online`, `#3186 Synchronize with server`, EduPage integratsiyasi (`#3103`) |
| Til/mamlakat sozlamalari | `#3526 Country specific options`, `#3525 High school terminology`, `#3527 Time format`, `#3347 Right to Left` |

### 4.10 aSc'ning **ichki** ma'lumot modeli — mobil viewer'dan mustaqil tasdiq

`template/mobile/asctt.jar` — J2ME (MIDP-1.0) mobil ko'ruvchi. Uning ichida `sk/asc/me/rozvrhy/Rok.class` — bu aSc'ning **ichki obyekt modelining bevosita ko'chirmasi** (maydon nomlari slovakcha). Bu XML eksportdan mustaqil tasdiq beradi.

`Rok` ("Yil" = butun hujjat) maydonlari:

| Slovakcha | Inglizcha | Izoh |
|---|---|---|
| `hOd` / `hDo` | first / last period | Soatlar diapazoni (0-soat qo'llab-quvvatlanadi) |
| `ndni` | number of days | Kunlar soni |
| `ntyzdnov` | number of weeks | Sikldagi haftalar |
| `ntermov` | number of terms | Choraklar |
| `denNazov[]` | day names | Kun nomlari massivi |
| `predmety` | subjects | |
| `ucitelia` | teachers | |
| `triedy` | classes | |
| `triedyDeleniaSkupin: short[][]` | class → divisions → groups | **Bo'linish tuzilmasi ikki o'lchovli massiv** |
| `ucebne` | classrooms | |
| `hodiny` | lessons | |
| `hodinyUoffset` | lesson → teachers offset | Ichkarida darslar↔o'qituvchilar bog'lanishi offset jadvali orqali |
| `hodinyKoffset` | lesson → cards offset | |
| `hodinyToffset` | lesson → classes offset | |
| `filter` | | Ko'rinish filtri |

Aksessorlar (ichki API yuzasi):
`getHodinaDlzka` (lesson length = `periodspercard`), `getHodinaKariet` (cards count), `getHodinaPredmet`, `getHodinaTriedy → int[]`, `getHodinaUcitelia → int[]`, `getHodinaDelenieVTriede(class, lesson)` (dars qaysi bo'linishga tegishli), `getHodinaSkupinyVTriede`, `getKartaHodina` (card → lesson), `getKartaUcebne(card, ...) → int[]`, **`isKartaDWT(day, week, term, …)`** — kartochkaning **kun/hafta/chorak** bo'yicha ko'rinishi (`cards.days/weeks/terms` bitmask'larining ichki ekvivalenti), `getTriedaNDeleni` (sinfdagi bo'linishlar soni), `getTriedaDelenieSkupin`, `getTriedaSkupinaNazov(class, division, group)`, `getTriedaSeminaroveDelenie`, `getTriedaZiakSeminarovaSkupina(class, student)`, `getUcitelFarba` (rang).

`PaintKarta` (chizilgan kartochka) maydonlari: `dlzka` (uzunlik), `hodina` (lesson), `karta` (card), `text`, `farby` (ranglar), `prvaSkupina` (birinchi guruh), `seminar`.

**Xulosa:** ichki modelda ham `lesson` va `card` ajratilgan, `class → division → group` uch pog'onali, kartochka `(day, week, term, period)` bo'yicha indekslanadi. Bizning sxema (6-bo'lim) shu tuzilmaga to'liq mos.

Slovakcha↔inglizcha lug'at: `trieda`=class, `ucitel`=teacher, `ucebna`=classroom, `predmet`=subject, `ziak`=student, `hodina`=lesson, `karta`=card, `skupina`=group, `delenie`=division, `dlzka`=length, `nazov`=name, `skratka`=short, `farba`=color, `den`=day, `tyzden`=week, `term`=term, `rok`=year/document, `zvon`=bell.

### 4.11 Standart qo'ng'iroq jadvali — `resources/def/def_zvon.dat`

912 baytli binar fayl (`zvon` = qo'ng'iroq). Tuzilishi: little-endian int32 massiv, 3 marta takrorlangan (3 ta preset). Har bir preset: `count = 7`, keyin `(hour, minute)` juftliklari:

```
7:10–7:55, 8:00–8:45, 9:00–9:45, 11:00–11:45, 12:00–12:45,
13:00–13:45, 14:00–14:45, 15:00–15:45, 16:00–16:45,
17:00–17:45, 18:00–18:45, 20:00–20:44…
```

Ya'ni **bir nechta "bells" preseti** ma'lumotlar modelida mavjud (`period.bell_set` ustunimiz shuni qamrab oladi).

### 4.12 LUSD integratsiyasi — sxema yo'q

`template/lusd/KeyStore/` — faqat GnuPG kalitlari (`ascpublic.txt`, `pubring.gpg`, `secring.gpg`, `trustdb.gpg`), uid `aSc s.r.o. <lusd@asc.sk>`. Germaniya (Hessen) LUSD tizimiga uzatiladigan faylni **imzolash/shifrlash** uchun. Hech qanday maydon ta'rifi yo'q.

---

## 5. `.roz` fayl formati — nima aniqlandi

### 5.1 Konteynerlar

| Kengaytma | Tuzilishi |
|---|---|
| `.roz` | Asosiy binar fayl |
| `.ziptt` | Oddiy ZIP arxiv, ichida bitta `tt.roz` |
| `.zipsubst` | O'rinbosarlik ma'lumotlari (ZIP) |
| `.rox` | `resources/tutorial.rox` — `.roz` bilan bir xil format |

### 5.2 Fayl sarlavhasi

```
offset 0x00: 44 01 00 00      uint32 LE = 324   → format/versiya raqami
offset 0x04: 05 'ASCTT'       length-prefixed ASCII magic
offset 0x0A: 00 00 00 00 0a 00 00 80   
offset 0x12: 95 b1 02 67      uint32 LE = 1728123285 → Unix timestamp (2024-10-05)
offset 0x1c: 0a 00 00 80
offset 0x20: a0 a2 35 67      uint32 LE = 1731497632 → Unix timestamp (2024-11-13, oxirgi saqlash)
...
oxiri:       09 'ASCTT_END'
```

### 5.3 Aniqlangan qoidalar

1. **Satrlar length-prefixed**: `[1 bayt uzunlik][baytlar]`. Kodirovka — **OS codepage** (rus fayli CP1251, slovak fayli CP1250). Unicode emas. Uzun satrlar uchun boshqa prefiks bo'lishi mumkin (tekshirilmadi — barcha topilgan satrlar < 128 bayt).
2. **Siqilmagan, shifrlanmagan.** Butun fayl entropiyasi 2.1 bit/bayt; 51% `0x00`, 33% `0xFF`. `0xFFFFFFFF` = "yo'q/bo'sh ID" sentineli.
3. **Yozuv (record) tuzilishi**: qat'iy o'lchamli int maydonlar bloki → 8 baytli hash/GUID → length-prefixed satrlar → ichki matritsalar.
   Grade yozuvi misoli:
   ```
   0b 00 00 00   id = 11
   0b 00 00 00   grade = 11
   00 01 00 00 00 0a 00 00 00 00 00 00 00
   ff 99 33      RGB rangi (#FF9933)
   02 00 00 00 02 00 00 00 02 00 01 00
   f1 20 c8 d2 dd f8 46 86   8-baytli hash
   00 00 00 00 00 00 02
   0a "Паралл. 11"     short
   0c "Параллель 11"   name
   ```
4. **Ichki bo'limlar `CLASSTT` … `CLASSTT_END`** — har bir sinf uchun alohida blok (real faylda 30 ta, har biri ~2.7 KB). Ichida sinf nomi + qisqartmasi va sinfning jadval matritsasi.
5. **Global bo'limlar**: `ASCTT` … `ASCTT_END`; oxirida `Master` bloki (hisobotlar: `Общее`, `Учителя`, `Кабинеты`, `Предметы`, `Ученики`) va chop etish dizaynlari.
6. **Custom fields** `#<langID>` kalit-satri sifatida saqlanadi (4.7-bo'limga qarang).
7. **Chop etish formulalari** alohida obfuskatsiya bilan: `"86,40,84,100,105,112,112,109,33,79,98,110,102,40,"` — har bir bayt kodi **+1** siljitilgan (`84,100,105,112,112,109,33,79,98,110,102` − 1 = `School Name`).
8. **O'qituvchi yozuvi ichida time-off matritsasi** joylashgan: `name`/`short` dan keyin ~1400 bayt, 4 baytli katak qiymatlari (`0x04`, `0x06`, ...), keyin `firstname`/`lastname`.
9. **Bell times satr sifatida saqlanmaydi** (`HH:MM` shabloni topilmadi) — ehtimol daqiqalarda int sifatida.

### 5.4 Nima aniqlanmadi

- Entity bo'limlarining aniq chegaralari va sarlavhalari (magic marker yo'q, faqat `CLASSTT`).
- `lessons` va `cards` yozuvlarining aniq tuzilishi (satr yo'q — faqat raqamlar; qaysi offsetda ekanini pozitsiyaviy tahlil bilan aniqlash mumkin, lekin bunga to'liq decoder kerak).
- Time-off katak qiymatlari lug'ati (`0x04` = ?, `0x06` = ?).
- Cheklovlar (constraints) bloki qayerda va qanday kodlangan.
- `0x144` (324) versiya raqamining semantikasi.

### 5.5 XULOSA — migratsiya strategiyasi

**`.roz` faylini to'g'ridan-to'g'ri parse qilish TAVSIYA ETILMAYDI.** Sabab: format hujjatlanmagan, versiyaga bog'liq, codepage-ga bog'liq, cheklovlar bloki noma'lum.

**Tavsiya etiladigan yo'l:** aSc TimeTables'da `File → Export → XML` (`asctt2012` formati) orqali eksport qilib, o'sha XML'ni import qilish. Bu format to'liq hujjatlangan (`template/xmlexport/asctt2012.xml`) va barcha asosiy ma'lumotlarni beradi. Cheklovlar va o'rinbosarlik ma'lumotlari qo'lda ko'chiriladi.

### 5.6 Real maktab hajmi — `Зиё интелект 2024 - 2025.roz`

| Ko'rsatkich | Qiymat |
|---|---|
| Fayl hajmi | 397 083 bayt (`.ziptt` ichida ~20 KB gacha siqiladi) |
| Maktab nomi | `зиё Интелект` |
| O'quv yili | `2024/2025` |
| **Grades (parallellar)** | **20** (`Параллель 1` … `Параллель 20`) |
| **Subjects (fanlar)** | **43** (Она тили, Адибиёт, Рус тили, Математика, Алгебра, Геометрия, Информатика, Физика, История, Химия, Биология, География, Тасвирий санъат, Чизмачилик, Мусика, Жисмоний тарбия, Турк тили, Технология, Робототехнтика, Синф соати, Тарбия, Хукук, Табиий Фан, …) — o'zbek va rus tillarida dublikatlar bilan |
| **Teachers (o'qituvchilar)** | **44** (1 tasi `Вакант` = bo'sh o'rin) |
| **Classes (sinflar)** | **30** (1 А…В, 2 А…Г, 3 А…Г, 4 А…Д, 5 А Узб/Б/В, 6 А Узб/Г/Д, 7 А Узб/Д/Ж/З, 8 А Узб/Г/З, 9 А Узб) |
| **Groups (guruhlar)** | **150** (har sinfda 5 ta: Весь класс, 1 группа, 2 группа, Мальчики, Девочки) |
| **Divisions** | 3 (entireclass, 2-lik bo'linish, jins bo'yicha bo'linish) |
| **Classrooms (xonalar)** | **0** — maktab xonalarni umuman ishlatmagan |
| **Students** | **0** — o'quvchilar kiritilmagan |
| Chop etish dizaynlari | `report_header`, `class_row1..3`, `teacher_row1..2`, `teacher2_row1..2`, `internal_table`, `internal_table_teacher` |
| Boshqa fayllar | `... 2 чет.roz` (388 KB), `... 3 чет.roz` (386 KB) — har chorak uchun **alohida fayl** (aSc'da termsdefs ishlatilmagan!) |

Taqqoslash uchun `demos/Demo1.roz` (aSc namunasi): 20 grade, ~25 fan, ~40 o'qituvchi, **33 sinf** (5.A…8.H), ~7 nomli xona (S106, S107, GR502, GR504, S108–S110) + har sinf uchun uy xonasi, 3 ta chorak (`Tretina 1/2/3`).

> **Muhim xulosa loyiha uchun:** real maktab har chorak uchun **yangi fayl** yaratgan. Bizning tizimda bu `academic_year` + `term` orqali bitta bazada saqlanishi kerak — bu aSc'ga nisbatan bevosita ustunlik.

---

## 6. PostgreSQL / EF Core uchun tavsiya etilgan sxema

Prinsiplar:
- **Multi-tenant**: har bir jadvalda `school_id` (tenant discriminator) → EF Core global query filter.
- **Academic year scoping**: barcha jadval ma'lumotlari `academic_year_id` ga bog'lanadi (yildan yilga nusxalash uchun).
- Barcha PK — `uuid` (`gen_random_uuid()`), lekin aSc ID'lari `external_id`/`partner_id` da saqlanadi.
- Bitmask'lar `bit varying` emas, **`boolean[]`** yoki `int` bitmask sifatida — bu yerda o'qilishi oson bo'lgan `varchar` + `CHECK` tanlangan (aSc bilan bir xil, debug qulay).
- Ko'p-ko'pga munosabatlar alohida join jadvallar (aSc'dagi `classids="1,2,3"` ro'yxatlari normalizatsiya qilinadi).

```sql
-- ============================================================
-- 0. TENANT / SCOPE
-- ============================================================
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE school (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code            varchar(64)  NOT NULL,          -- #3957 School code
    name            varchar(256) NOT NULL,          -- #1166
    short_name      varchar(64),
    district        varchar(128),                   -- #3974
    address_street  varchar(256),                   -- #3149
    address_city    varchar(128),                   -- #3150
    headmaster      varchar(256),                   -- #3975
    logo_path       varchar(512),                   -- #4055
    country_code    char(2),
    timezone        varchar(64)  NOT NULL DEFAULT 'Asia/Tashkent',
    created_at      timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT uq_school_code UNIQUE (code)
);

CREATE TABLE academic_year (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id   uuid NOT NULL REFERENCES school(id) ON DELETE CASCADE,
    name        varchar(32) NOT NULL,               -- #1167 "2024/2025"
    starts_on   date NOT NULL,                      -- #3976 Valid from
    ends_on     date NOT NULL,                      -- #3977 Valid to
    is_active   boolean NOT NULL DEFAULT false,
    days_per_week   smallint NOT NULL DEFAULT 6 CHECK (days_per_week BETWEEN 1 AND 14),
    weeks_in_cycle  smallint NOT NULL DEFAULT 1 CHECK (weeks_in_cycle BETWEEN 1 AND 12),
    terms_count     smallint NOT NULL DEFAULT 4 CHECK (terms_count BETWEEN 1 AND 12),
    CONSTRAINT uq_ay UNIQUE (school_id, name),
    CONSTRAINT ck_ay_dates CHECK (ends_on > starts_on)
);
CREATE INDEX ix_ay_school ON academic_year(school_id) WHERE is_active;

-- ============================================================
-- 1. TIME STRUCTURE
-- ============================================================
CREATE TABLE period (                                -- aSc: periods
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    period_no       smallint NOT NULL,               -- 0 = "nolinchi soat"
    name            varchar(64),
    short           varchar(16),
    start_time      time,
    end_time        time,
    is_break        boolean NOT NULL DEFAULT false,  -- #3438
    bell_set        smallint NOT NULL DEFAULT 1,     -- #3753 Print in bells
    print_in_summary        boolean NOT NULL DEFAULT true,   -- #3749
    print_in_teacher_tt     boolean NOT NULL DEFAULT true,   -- #3751
    print_in_class_tt       boolean NOT NULL DEFAULT true,   -- #3752
    print_in_classroom_tt   boolean NOT NULL DEFAULT true,   -- #3872
    CONSTRAINT uq_period UNIQUE (academic_year_id, period_no, bell_set),
    CONSTRAINT ck_period_time CHECK (end_time IS NULL OR start_time IS NULL OR end_time > start_time)
);

CREATE TABLE week_day (                              -- aSc: days
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    day_no          smallint NOT NULL,               -- 0-based
    name            varchar(32) NOT NULL,
    short           varchar(8)  NOT NULL,
    CONSTRAINT uq_weekday UNIQUE (academic_year_id, day_no)
);

-- Bitmask ta'riflari. mask - '0'/'1' satri, uzunligi tegishli o'lchamga teng.
CREATE TABLE days_def (                              -- aSc: daysdefs
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    mask            varchar(14) NOT NULL CHECK (mask ~ '^[01]+$'),
    name            varchar(64),
    short           varchar(16),
    CONSTRAINT uq_daysdef UNIQUE (academic_year_id, mask)
);

CREATE TABLE weeks_def (                             -- aSc: weeksdefs
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    mask            varchar(12) NOT NULL CHECK (mask ~ '^[01]+$'),
    name            varchar(64),
    short           varchar(16),
    CONSTRAINT uq_weeksdef UNIQUE (academic_year_id, mask)
);

CREATE TABLE terms_def (                             -- aSc: termsdefs
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    mask            varchar(12) NOT NULL CHECK (mask ~ '^[01]+$'),
    name            varchar(64),
    short           varchar(16),
    starts_on       date,
    ends_on         date,
    CONSTRAINT uq_termsdef UNIQUE (academic_year_id, mask)
);

-- ============================================================
-- 2. RESOURCES
-- ============================================================
CREATE TABLE subject (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    name            varchar(128) NOT NULL,
    short           varchar(24)  NOT NULL,
    color           char(7) CHECK (color ~ '^#[0-9A-Fa-f]{6}$'),
    needs_homework  boolean NOT NULL DEFAULT false,  -- #1018
    can_be_joined   boolean NOT NULL DEFAULT true,   -- #2107
    max_students    smallint,                        -- #3511
    distribution    smallint NOT NULL DEFAULT 2      -- #3720: 0=none,1=low,2=medium,3=ideal,4=ideal/no-cons
                      CHECK (distribution BETWEEN 0 AND 4),
    minutes_per_week smallint,                       -- #3574
    is_temporary    boolean NOT NULL DEFAULT false,  -- #4025
    picture_path    varchar(512),
    external_id     varchar(64),                     -- aSc id / partner_id
    CONSTRAINT uq_subject_short UNIQUE (academic_year_id, short),
    CONSTRAINT uq_subject_name  UNIQUE (academic_year_id, name)
);
CREATE INDEX ix_subject_ext ON subject(academic_year_id, external_id);

CREATE TABLE building (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    name            varchar(128) NOT NULL,
    short           varchar(24)  NOT NULL,
    CONSTRAINT uq_building UNIQUE (academic_year_id, short)
);

CREATE TABLE building_transfer (        -- #3091, #3482: binolar orasida ko'chish
    from_building_id uuid NOT NULL REFERENCES building(id) ON DELETE CASCADE,
    to_building_id   uuid NOT NULL REFERENCES building(id) ON DELETE CASCADE,
    periods_needed   smallint NOT NULL DEFAULT 1,
    minutes_needed   smallint,
    PRIMARY KEY (from_building_id, to_building_id)
);

CREATE TABLE classroom (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    building_id     uuid REFERENCES building(id) ON DELETE SET NULL,
    name            varchar(128) NOT NULL,
    short           varchar(24)  NOT NULL,
    capacity        smallint CHECK (capacity IS NULL OR capacity > 0),
    needs_supervision boolean NOT NULL DEFAULT false, -- #3169
    is_shared       boolean NOT NULL DEFAULT false,   -- #1068
    external_id     varchar(64),
    CONSTRAINT uq_classroom_short UNIQUE (academic_year_id, short)
);

CREATE TABLE classroom_nearby (         -- #3171 Nearby classrooms
    classroom_id        uuid NOT NULL REFERENCES classroom(id) ON DELETE CASCADE,
    nearby_classroom_id uuid NOT NULL REFERENCES classroom(id) ON DELETE CASCADE,
    PRIMARY KEY (classroom_id, nearby_classroom_id),
    CHECK (classroom_id <> nearby_classroom_id)
);

CREATE TABLE teacher (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    name            varchar(256) NOT NULL,
    short           varchar(24)  NOT NULL,
    first_name      varchar(128),
    last_name       varchar(128),
    gender          char(1) CHECK (gender IN ('M','F')),
    color           char(7) CHECK (color ~ '^#[0-9A-Fa-f]{6}$'),
    email           varchar(256),
    mobile          varchar(64),
    contract_periods smallint,                      -- #3313 shartnoma soatlari
    is_vacancy      boolean NOT NULL DEFAULT false, -- "Вакант"
    can_substitute  boolean NOT NULL DEFAULT true,  -- #1472
    substitution_min smallint,                      -- #2948
    points_offset   integer NOT NULL DEFAULT 0,     -- #2706
    is_resource     boolean NOT NULL DEFAULT false, -- #2700
    external_id     varchar(64),
    CONSTRAINT uq_teacher_short UNIQUE (academic_year_id, short)
);
CREATE INDEX ix_teacher_ext ON teacher(academic_year_id, external_id);

-- ============================================================
-- 3. STUDENT STRUCTURE
-- ============================================================
CREATE TABLE grade (                                 -- aSc: grades (parallel)
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    grade_no        smallint NOT NULL,               -- aSc'da PK
    name            varchar(64) NOT NULL,
    short           varchar(16),
    CONSTRAINT uq_grade UNIQUE (academic_year_id, grade_no)
);

CREATE TABLE school_class (                          -- aSc: classes ("class" - SQL reserved word emas, lekin aniqlik uchun)
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    grade_id        uuid REFERENCES grade(id) ON DELETE SET NULL,
    class_teacher_id uuid REFERENCES teacher(id) ON DELETE SET NULL,
    name            varchar(64) NOT NULL,
    short           varchar(24) NOT NULL,
    language        varchar(32),                     -- #3956 (roz custom field)
    print_prefix    varchar(32),                     -- #3487
    external_id     varchar(64),
    CONSTRAINT uq_class_short UNIQUE (academic_year_id, short)
);
CREATE INDEX ix_class_grade ON school_class(grade_id);

CREATE TABLE class_home_classroom (                  -- aSc: classes.classroomids (RO'YXAT!)
    class_id        uuid NOT NULL REFERENCES school_class(id) ON DELETE CASCADE,
    classroom_id    uuid NOT NULL REFERENCES classroom(id) ON DELETE CASCADE,
    priority        smallint NOT NULL DEFAULT 0,
    PRIMARY KEY (class_id, classroom_id)
);

CREATE TABLE class_division (                        -- aSc: groups.divisiontag ni normalizatsiya qilish
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    class_id        uuid NOT NULL REFERENCES school_class(id) ON DELETE CASCADE,
    division_tag    smallint NOT NULL,               -- 0 = entire class
    name            varchar(64),                     -- "Jins bo'yicha", "Til bo'yicha"
    CONSTRAINT uq_division UNIQUE (class_id, division_tag)
);

CREATE TABLE student_group (                         -- aSc: groups
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    class_id        uuid NOT NULL REFERENCES school_class(id) ON DELETE CASCADE,
    division_id     uuid NOT NULL REFERENCES class_division(id) ON DELETE CASCADE,
    name            varchar(64) NOT NULL,
    is_entire_class boolean NOT NULL DEFAULT false,
    student_count   smallint,
    external_id     varchar(64),
    CONSTRAINT uq_group UNIQUE (class_id, division_id, name)
);
-- Har bir sinfda aynan bitta "butun sinf" guruhi bo'lishi shart:
CREATE UNIQUE INDEX uq_group_entire ON student_group(class_id) WHERE is_entire_class;

CREATE TABLE student (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    class_id        uuid REFERENCES school_class(id) ON DELETE SET NULL,
    name            varchar(256) NOT NULL,
    first_name      varchar(128),
    last_name       varchar(128),
    number          varchar(32),                     -- #3678 jurnal raqami
    gender          char(1) CHECK (gender IN ('M','F')),
    email           varchar(256),
    mobile          varchar(64),
    external_id     varchar(64),
    CONSTRAINT uq_student_number UNIQUE (academic_year_id, class_id, number)
);
CREATE INDEX ix_student_class ON student(class_id);

CREATE TABLE student_group_member (                  -- aSc: groups.studentids
    student_id      uuid NOT NULL REFERENCES student(id) ON DELETE CASCADE,
    group_id        uuid NOT NULL REFERENCES student_group(id) ON DELETE CASCADE,
    is_locked       boolean NOT NULL DEFAULT false,  -- #3627 Student is locked into this group
    PRIMARY KEY (student_id, group_id)
);

CREATE TABLE student_subject (                       -- aSc: studentsubjects
    student_id      uuid NOT NULL REFERENCES student(id) ON DELETE CASCADE,
    subject_id      uuid NOT NULL REFERENCES subject(id) ON DELETE CASCADE,
    seminar_group   smallint,                        -- #3646 seksiya raqami
    importance      smallint NOT NULL DEFAULT 2      -- 0=should-not,1=optional,2=must
                      CHECK (importance BETWEEN 0 AND 2),
    alternate_for_subject_id uuid REFERENCES subject(id) ON DELETE SET NULL, -- #3310
    PRIMARY KEY (student_id, subject_id)
);
CREATE INDEX ix_studsubj_subject ON student_subject(subject_id);

-- ============================================================
-- 4. LESSONS & CARDS
-- ============================================================
CREATE TABLE lesson (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    subject_id      uuid NOT NULL REFERENCES subject(id) ON DELETE RESTRICT,
    periods_per_card smallint NOT NULL DEFAULT 1 CHECK (periods_per_card BETWEEN 1 AND 8),
    periods_per_week numeric(5,2) NOT NULL CHECK (periods_per_week > 0),
    days_def_id     uuid REFERENCES days_def(id)  ON DELETE SET NULL,
    weeks_def_id    uuid REFERENCES weeks_def(id) ON DELETE SET NULL,
    terms_def_id    uuid REFERENCES terms_def(id) ON DELETE SET NULL,
    seminar_group   smallint,
    capacity        smallint,                        -- #3496
    classroom_count smallint NOT NULL DEFAULT 1,     -- #2554 nechta xona kerak
    duration_minutes smallint,
    external_id     varchar(64),
    CONSTRAINT ck_lesson_cards CHECK (periods_per_week >= periods_per_card)
);
CREATE INDEX ix_lesson_subject ON lesson(subject_id);
CREATE INDEX ix_lesson_ay ON lesson(academic_year_id);

CREATE TABLE lesson_class (                          -- aSc: lessons.classids
    lesson_id  uuid NOT NULL REFERENCES lesson(id) ON DELETE CASCADE,
    class_id   uuid NOT NULL REFERENCES school_class(id) ON DELETE CASCADE,
    PRIMARY KEY (lesson_id, class_id)
);
CREATE INDEX ix_lessonclass_class ON lesson_class(class_id);

CREATE TABLE lesson_group (                          -- aSc: lessons.groupids
    lesson_id  uuid NOT NULL REFERENCES lesson(id) ON DELETE CASCADE,
    group_id   uuid NOT NULL REFERENCES student_group(id) ON DELETE CASCADE,
    PRIMARY KEY (lesson_id, group_id)
);
CREATE INDEX ix_lessongroup_group ON lesson_group(group_id);

CREATE TABLE lesson_teacher (                        -- aSc: lessons.teacherids
    lesson_id  uuid NOT NULL REFERENCES lesson(id) ON DELETE CASCADE,
    teacher_id uuid NOT NULL REFERENCES teacher(id) ON DELETE CASCADE,
    PRIMARY KEY (lesson_id, teacher_id)
);
CREATE INDEX ix_lessonteacher_teacher ON lesson_teacher(teacher_id);

CREATE TABLE lesson_allowed_classroom (              -- aSc: lessons.classroomids (RUXSAT ETILGAN)
    lesson_id    uuid NOT NULL REFERENCES lesson(id) ON DELETE CASCADE,
    classroom_id uuid NOT NULL REFERENCES classroom(id) ON DELETE CASCADE,
    priority     smallint NOT NULL DEFAULT 0,
    PRIMARY KEY (lesson_id, classroom_id)
);

CREATE TABLE lesson_student (                        -- seminar/elective uchun (aSc 2008 lessons.studentids)
    lesson_id  uuid NOT NULL REFERENCES lesson(id) ON DELETE CASCADE,
    student_id uuid NOT NULL REFERENCES student(id) ON DELETE CASCADE,
    PRIMARY KEY (lesson_id, student_id)
);

CREATE TABLE card (                                  -- aSc: cards
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    lesson_id       uuid NOT NULL REFERENCES lesson(id) ON DELETE CASCADE,
    period_no       smallint NOT NULL,               -- boshlanish pozitsiyasi
    day_no          smallint NOT NULL,               -- aSc `days` bitmask'i o'rniga aniq kun
    weeks_mask      varchar(12) NOT NULL DEFAULT '1' CHECK (weeks_mask ~ '^[01]+$'),
    terms_mask      varchar(12) NOT NULL DEFAULT '1' CHECK (terms_mask ~ '^[01]+$'),
    is_locked       boolean NOT NULL DEFAULT false,  -- #1618
    CONSTRAINT uq_card_pos UNIQUE (lesson_id, day_no, period_no, weeks_mask, terms_mask)
);
CREATE INDEX ix_card_lesson ON card(lesson_id);
CREATE INDEX ix_card_slot   ON card(day_no, period_no);

CREATE TABLE card_classroom (                        -- aSc: cards.classroomids (TAYINLANGAN)
    card_id      uuid NOT NULL REFERENCES card(id) ON DELETE CASCADE,
    classroom_id uuid NOT NULL REFERENCES classroom(id) ON DELETE CASCADE,
    PRIMARY KEY (card_id, classroom_id)
);
CREATE INDEX ix_cardroom_room ON card_classroom(classroom_id);

-- Kolliziyalarni tez tekshirish uchun denormalizatsiyalangan READ MODEL.
-- Har bir card × har bir egallangan soat × har bir resurs uchun bitta qator.
CREATE TABLE timetable_slot (
    id              bigserial PRIMARY KEY,
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    card_id         uuid NOT NULL REFERENCES card(id) ON DELETE CASCADE,
    lesson_id       uuid NOT NULL REFERENCES lesson(id) ON DELETE CASCADE,
    day_no          smallint NOT NULL,
    period_no       smallint NOT NULL,
    week_no         smallint NOT NULL DEFAULT 0,
    term_no         smallint NOT NULL DEFAULT 0,
    subject_id      uuid NOT NULL,
    class_id        uuid,
    group_id        uuid,
    teacher_id      uuid,
    classroom_id    uuid
);
CREATE INDEX ix_slot_teacher ON timetable_slot(academic_year_id, teacher_id, day_no, period_no, week_no, term_no);
CREATE INDEX ix_slot_class   ON timetable_slot(academic_year_id, class_id,   day_no, period_no, week_no, term_no);
CREATE INDEX ix_slot_room    ON timetable_slot(academic_year_id, classroom_id, day_no, period_no, week_no, term_no);
CREATE INDEX ix_slot_group   ON timetable_slot(academic_year_id, group_id,   day_no, period_no, week_no, term_no);

-- ============================================================
-- 5. TIME-OFF & CONSTRAINTS
-- ============================================================
CREATE TYPE tt_owner_kind AS ENUM ('teacher','class','classroom','subject','group','student','grade','global');
CREATE TYPE tt_availability AS ENUM ('allowed','not_recommended','forbidden');  -- #1270/#1271/#3500

CREATE TABLE time_off (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    owner_kind      tt_owner_kind NOT NULL,
    owner_id        uuid NOT NULL,                   -- polimorf FK (EF Core'da TPH/discriminator)
    day_no          smallint NOT NULL,
    period_no       smallint NOT NULL,
    weeks_mask      varchar(12) NOT NULL DEFAULT '1',
    terms_mask      varchar(12) NOT NULL DEFAULT '1',
    availability    tt_availability NOT NULL DEFAULT 'forbidden',
    CONSTRAINT uq_timeoff UNIQUE (owner_kind, owner_id, day_no, period_no, weeks_mask, terms_mask)
);
CREATE INDEX ix_timeoff_owner ON time_off(academic_year_id, owner_kind, owner_id);

CREATE TABLE constraint_def (                        -- #3071 Constraints
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    kind            varchar(64) NOT NULL,            -- 'MaxGapsPerDay', 'MaxConsecutivePeriods', ...
    owner_kind      tt_owner_kind NOT NULL,          -- #3028 Global / #3029 Only for
    importance      smallint NOT NULL DEFAULT 1      -- #3067: 0=Low, 1=Normal, 2=High, 3=Strict
                      CHECK (importance BETWEEN 0 AND 3),
    allow_relaxation boolean NOT NULL DEFAULT true,  -- #3072
    is_enabled      boolean NOT NULL DEFAULT true,   -- #3311 Disabled
    params          jsonb NOT NULL DEFAULT '{}'::jsonb,  -- {"max":2,"positions":[...]}
    note            text
);
CREATE INDEX ix_constraint_kind ON constraint_def(academic_year_id, kind) WHERE is_enabled;
CREATE INDEX ix_constraint_params ON constraint_def USING gin (params);

CREATE TABLE constraint_scope (                      -- cheklov kimga qo'llaniladi (0..N obyekt)
    constraint_id   uuid NOT NULL REFERENCES constraint_def(id) ON DELETE CASCADE,
    target_kind     tt_owner_kind NOT NULL,
    target_id       uuid NOT NULL,
    PRIMARY KEY (constraint_id, target_kind, target_id)
);
CREATE INDEX ix_cscope_target ON constraint_scope(target_kind, target_id);

CREATE TABLE card_relation (                         -- #1400 Card relationships
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    kind            varchar(64) NOT NULL,            -- 'MustFollow','CannotFollow','SameDay','NotSameDay',...
    ordered         boolean NOT NULL DEFAULT false,  -- #3474 in specified order
    importance      smallint NOT NULL DEFAULT 1,
    is_enabled      boolean NOT NULL DEFAULT true,
    params          jsonb NOT NULL DEFAULT '{}'::jsonb
);

CREATE TABLE card_relation_member (
    relation_id     uuid NOT NULL REFERENCES card_relation(id) ON DELETE CASCADE,
    lesson_id       uuid NOT NULL REFERENCES lesson(id) ON DELETE CASCADE,
    side            char(1) NOT NULL DEFAULT 'A' CHECK (side IN ('A','B')),  -- "A must be before B"
    ord             smallint NOT NULL DEFAULT 0,
    PRIMARY KEY (relation_id, lesson_id, side)
);

-- ============================================================
-- 6. SUBSTITUTION (o'rinbosarlik) MODULI
-- ============================================================
CREATE TABLE absence_reason (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       uuid NOT NULL REFERENCES school(id) ON DELETE CASCADE,
    name            varchar(128) NOT NULL,
    short           varchar(16),
    is_approved     boolean NOT NULL DEFAULT true,   -- #2694 / #2695
    CONSTRAINT uq_absreason UNIQUE (school_id, name)
);

CREATE TABLE substitution_type (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       uuid NOT NULL REFERENCES school(id) ON DELETE CASCADE,
    name            varchar(128) NOT NULL,           -- "paid","unpaid","joined"
    is_paid         boolean NOT NULL DEFAULT false,
    CONSTRAINT uq_substtype UNIQUE (school_id, name)
);

CREATE TABLE absence (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    owner_kind      tt_owner_kind NOT NULL CHECK (owner_kind IN ('teacher','class','classroom')),
    owner_id        uuid NOT NULL,
    reason_id       uuid REFERENCES absence_reason(id) ON DELETE SET NULL,
    date_from       date NOT NULL,
    date_to         date NOT NULL,
    period_from     smallint,                        -- NULL = butun kun (#1454)
    period_to       smallint,
    note            text,
    CONSTRAINT ck_absence_dates CHECK (date_to >= date_from)
);
CREATE INDEX ix_absence_owner ON absence(academic_year_id, owner_kind, owner_id, date_from, date_to);

CREATE TABLE substitution (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    absence_id      uuid REFERENCES absence(id) ON DELETE CASCADE,
    on_date         date NOT NULL,
    day_no          smallint NOT NULL,
    period_no       smallint NOT NULL,
    original_card_id uuid REFERENCES card(id) ON DELETE SET NULL,
    substitute_teacher_id uuid REFERENCES teacher(id) ON DELETE SET NULL,
    substitute_classroom_id uuid REFERENCES classroom(id) ON DELETE SET NULL,
    subject_id      uuid REFERENCES subject(id) ON DELETE SET NULL,
    type_id         uuid REFERENCES substitution_type(id) ON DELETE SET NULL,
    is_cancelled    boolean NOT NULL DEFAULT false,  -- #1476
    is_supervision  boolean NOT NULL DEFAULT false,  -- #1471 Duty
    note            text,
    CONSTRAINT uq_subst UNIQUE (on_date, period_no, original_card_id)
);
CREATE INDEX ix_subst_date ON substitution(academic_year_id, on_date);
CREATE INDEX ix_subst_teacher ON substitution(substitute_teacher_id, on_date);

CREATE TABLE daily_remark (                          -- #3795 Daily remark
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    on_date         date NOT NULL,
    text            text NOT NULL,
    CONSTRAINT uq_remark UNIQUE (academic_year_id, on_date)
);

CREATE TABLE holiday (                               -- #3882 Holidays
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    academic_year_id uuid NOT NULL REFERENCES academic_year(id) ON DELETE CASCADE,
    date_from       date NOT NULL,
    date_to         date NOT NULL,
    name            varchar(128) NOT NULL
);

-- ============================================================
-- 7. CUSTOM FIELDS (EAV) — aSc'ning #langID mexanizmi analogi
-- ============================================================
CREATE TABLE custom_field (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       uuid NOT NULL REFERENCES school(id) ON DELETE CASCADE,
    owner_kind      tt_owner_kind NOT NULL,
    code            varchar(64) NOT NULL,
    label           varchar(128) NOT NULL,
    data_type       varchar(16) NOT NULL DEFAULT 'text'
                      CHECK (data_type IN ('text','int','decimal','bool','date','enum')),
    CONSTRAINT uq_cf UNIQUE (school_id, owner_kind, code)
);

CREATE TABLE custom_field_value (
    field_id        uuid NOT NULL REFERENCES custom_field(id) ON DELETE CASCADE,
    owner_id        uuid NOT NULL,
    value           text,
    PRIMARY KEY (field_id, owner_id)
);
CREATE INDEX ix_cfv_owner ON custom_field_value(owner_id);

-- ============================================================
-- 8. PRINT DESIGNS
-- ============================================================
CREATE TABLE print_design (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    school_id       uuid NOT NULL REFERENCES school(id) ON DELETE CASCADE,
    name            varchar(128) NOT NULL,
    kind            varchar(32) NOT NULL,            -- 'class','teacher','classroom','summary','report'
    definition      jsonb NOT NULL,
    is_default      boolean NOT NULL DEFAULT false,
    CONSTRAINT uq_design UNIQUE (school_id, name)
);

CREATE TABLE print_design_assignment (               -- #3156 Apply this design to:
    design_id       uuid NOT NULL REFERENCES print_design(id) ON DELETE CASCADE,
    target_kind     tt_owner_kind NOT NULL,
    target_id       uuid NOT NULL,
    PRIMARY KEY (design_id, target_kind, target_id)
);
```

### 6.1 EF Core uchun eslatmalar

1. **Global query filter** — `modelBuilder.Entity<T>().HasQueryFilter(e => e.SchoolId == _tenant.SchoolId)`. `academic_year_id` orqali bog'langan jadvallar uchun `school_id` ni denormalizatsiya qilish (yoki `AcademicYear.SchoolId` bo'yicha filtr) tavsiya etiladi — JOIN'siz filtr tezroq.
2. **Polimorf FK** (`time_off.owner_id`, `constraint_scope.target_id`, `custom_field_value.owner_id`) — DB darajasida FK qo'yib bo'lmaydi. Yechim: (a) shadow property + `HasDiscriminator`, yoki (b) har bir owner turi uchun alohida jadval (`teacher_time_off`, `class_time_off`, ...). Ishlash muhim bo'lsa (b) tanlang.
3. **`timetable_slot`** — bu **read model**, `card` o'zgarganda trigger yoki domain event orqali qayta quriladi. Kolliziya tekshiruvi (`o'qituvchi bir vaqtda ikki joyda`) shu jadvalda `GROUP BY ... HAVING count(*)>1` bilan bir so'rovda bajariladi.
4. **`jsonb` + GIN indeks** cheklov parametrlari uchun — cheklov turlari juda xilma-xil (yuqoridagi 4.2 jadvalda 50+ tur), har biriga ustun ochish mumkin emas.
5. **Bitmask'lar** `varchar` sifatida saqlanadi (aSc bilan mos, debug qulay). Generator ichida `int` bitmask'ga aylantiriladi. `CHECK (mask ~ '^[01]+$')` + uzunlikni `academic_year.days_per_week`/`weeks_in_cycle`/`terms_count` bilan tekshiruvchi trigger tavsiya etiladi.
6. **Import mapping**: aSc XML'dagi `id` qiymatlari `external_id` ustuniga yoziladi; import vaqtida `(academic_year_id, external_id)` bo'yicha lookup qilinadi. `partner_id` — uchinchi tizim (SIS) uchun alohida ustun sifatida qo'shilishi mumkin.
7. **`ON DELETE`**: `lesson.subject_id` uchun `RESTRICT` — fanni o'chirish darslarni jimgina yo'q qilmasligi kerak (aSc `#1758` da xuddi shunday ogohlantiradi).

### 6.2 Migratsiya tartibi (aSc XML → PostgreSQL)

`asctt2012.xml` importida tartib muhim (aSc'ning `handlestudentsafterlessons` opsiyasi shuni ko'rsatadi):

1. `periods`, `daysdefs`, `weeksdefs`, `termsdefs`
2. `subjects`, `teachers`, `classrooms`, `grades`
3. `classes` (→ `grades`, `teachers`, `classrooms` ga bog'lanadi)
4. `groups` (→ `classes`; `divisiontag` dan `class_division` yaratiladi)
5. `lessons` (→ subjects/classes/groups/teachers/classrooms/*defs)
6. `cards` (→ lessons)
7. `students` (→ classes)
8. `studentsubjects`, `groups.studentids` (→ students)
9. `timetable_slot` read model'ini qayta qurish
