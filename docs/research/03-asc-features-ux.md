# 03. aSc TimeTables — Funksional imkoniyatlar, ekranlar va eksport tizimi

> **Manba:** `/Users/me/Projects/TimeTables/TimeTables` (aSc TimeTables 2017, build 2016).
> **Tekshirilgan artefaktlar:** `designs/*/def.xml` (10 ta chop etish dizayni), `template/Web/*.htm`,
> `template/excelexport/*.xml`, `template/Import Samples/`, `template/mobile/`, `pictures/` (174 fayl),
> `skins/` (25 ta kategoriya, ~500 ikonka), `resources/tips/tips_en.txt`, `resources/tutorial.rox`,
> `demos/Demo files description.txt`, `demos/Tutorial/` (17 mashq), `demos/International/` (43 davlat), `supl/`.
> **Cheklov:** `lang.asc` (to'liq lokalizatsiya lug'ati) va `template/xmlexport/` sxemasi alohida
> tadqiqot hujjatlarida — bu yerda faqat shablon tilini ochish uchun kerak bo'lgan ID'lar dekodlangan.

---

## 1. Modullar xaritasi

aSc TimeTables monolit desktop dastur bo'lsa-da, ichki tuzilishi 8 ta aniq moduldan iborat.
Har bir modulning mavjudligi resurs papkalari bilan tasdiqlangan.

| # | Modul | Vazifasi | Isbot (fayl/papka) |
|---|-------|----------|--------------------|
| M1 | **Ma'lumot kiritish (Basic data)** | Maktab, o'quv yili, sinflar, o'qituvchilar, fanlar, xonalar, binolar, guruh/bo'linmalar, o'quvchilar, seminarlar | `skins/dialogs/` (class/teacher/subject/room/student 16-128px), `skins/toolbar/` (class, teacher, subject, room, student, seminar), `skins/dlgribbon/` (add/edit/del/import/export) |
| M2 | **Dars (lessons) va yuklama** | Dars kartochkalari yaratish, hafta soati, davomiylik (1/2/3 soatlik blok), bir darsga bir necha o'qituvchi/xona, guruhga bo'lish | `skins/dialogs/lessons_16/32.png`, `skins/dlgribbon/lesson_add_32.png`, `skins/listicons/lessons_16.png`, Demo3 tavsifi ("Informatics use two teachers and two rooms on each lesson") |
| M3 | **Cheklovlar (constraints/relations)** | O'qituvchi/sinf/fan/xona uchun vaqt cheklovlari, kartochkalararo munosabatlar, muhimlik darajalari | `skins/relations/` (importance: strict/high/normal/low/optimize/alternate/disabled), `skins/clean/` (0thPeriod, 2nd_period, 2plus1, 2shifts, 8days, Saturday, consecutively, gaps, teacher_gaps, same_period, forcerequests, building, terms, weeks) |
| M4 | **Generatsiya (avto-tuzish)** | Algoritm ishga tushirish, strict/relax/mixed rejimlar, qisman generatsiya (View bo'yicha), progress + muvaffaqiyat/muvaffaqiyatsizlik animatsiyasi | `skins/toolbar/gener_*.png`, `skins/dlgribbon/play_strict_32, play_relax_32, stop_32`, `skins/gener/` (gener_rooms, gener_students, gener_whole, gener_whole_nostudents), `resources/avi/GENER.AVI`, `succ.avi`, `fail.avi` |
| M5 | **Qo'lda tahrirlash (drag & drop)** | Kartochkani "qo'lga olish", joylashtirish, lock/unlock, mumkin bo'lgan pozitsiyalarni yoritish, undo/redo (100 qadam), zoom, swap | `resources/tips/tips_en.txt` (to'liq UX bayoni — §4), `skins/actions/lock_16, unlock_16, must_be_32, no_32, overtime_32`, `skins/toolbar/lock_32, unlock_32, zoom_*` |
| M6 | **Tekshirish (verification/advisor)** | Ma'lumot to'liqligini tekshirish, ziddiyatlarni topish, "Advisor" — muammoni tushuntirish va tuzatishni taklif qilish | `skins/verification/` (bug_32, warning_32, class/teacher/room/subject/student_48), `skins/markup/advisor/` (stop, No_entry, fixit_40, magic_ward), `skins/markup/roz_kontrola_info.xaml` (`{#1269}` = "Verification of the timetable"), `roz_popischyby.xaml` (`{#1034}` = "Details") |
| M7 | **Chop etish / eksport** | Dizayn shablonlari (WYSIWYG print designer), PDF/printer, HTML (web), Excel (SpreadsheetML), XML, mobil (J2ME), CSV | `designs/`, `skins/printing/`, `skins/reports/` (24 ta hisobot turi), `skins/toolbar/preview_*` (13 ta), `template/Web/`, `template/excelexport/`, `template/xmlexport/`, `template/mobile/` |
| M8 | **O'rinbosarlik (substitution)** | Yo'q o'qituvchilar, kunlik almashtirish, nazorat (supervision), ballar/hisob, chop etish va veb-nashr | `skins/toolbar/subst/` (40+ ikonka), `skins/reports/subst/`, `skins/substitution/`, `template/Web/subst*.htm`, `template/excelexport/export_supl_template.xml`, `skins/markup/roz_supl*.xaml` |
| M9 | **Internet nashri / integratsiya** | EduPage'ga publish, server'dan ochish/saqlash, email/SMS yuborish, remote desktop yordami, ro'yxatdan o'tish/litsenziya | `skins/dialogs/ttonline/` (publish, send, send_mail, send_sms, showwebpage, viewtimetables, open_edupage), `skins/toolbar/` (edupage_32, publish_*, remotedesktop_*, live_support_*, register_*, buy_*) |

**Qo'shimcha ko'ndalang (cross-cutting) imkoniyatlar:**

- **Views (ko'rinishlar)** — jadvalni qism-qismga bo'lish: `skins/views/` → default, grid, whole, teachers, rooms, subjects, subjects_bystudent, subjects_columns, students, students_pending, supervisions, fila. Generatsiya faqat tanlangan View doirasida ham ishlaydi (tips_en.txt).
- **Terms / Weeks / Days** — semestr, hafta (juft/toq), kun bo'yicha variantli jadval: `skins/term_week_day/` da 2..5 ta bo'linish uchun rangli (ko'k/yashil/qizil) badge'lar, `_A` = "hammasi".
- **Custom fields** — foydalanuvchi qo'shgan maydonlar: `skins/markup/customfield_48.png`, Excel shablonlarida `{cf:tc}`, `{cf:sk}`, dizaynda `{#1035:Tlačivo - typ}`.
- **Skins / theming** — `skins/GreenNote`, `skins/PinkLady`, `skins/default/popis.xml` (§6).
- **Ko'p tillilik va RTL** — `def.xml` da `rtl="0"` atributi, `skins/flags/` da 50+ bayroq, arab/ibroniy tillar.
- **Demo/tutorial kutubxonasi** — `demos/Tutorial/Training_01..17.roz` (17 bosqichli o'quv kursi), `demos/International/` 43 davlat uchun namunaviy jadvallar, `demos/Demo1..4.roz` (murakkablik bo'yicha o'sib boruvchi).

---

## 2. Ekranlar ro'yxati

Ustuvorlik: **P0** = MVP uchun majburiy, **P1** = birinchi katta relizda, **P2** = keyinroq.

### 2.1 Kirish va loyiha boshqaruvi

| Ekran | Maqsad | Asosiy amallar | Ust. |
|---|---|---|---|
| **Start / Main screen** | Loyihani ochish, yangi yaratish, demo va tutorial'ga kirish | Yangi jadval, Ochish, Serverdan ochish, Backup'dan ochish, So'nggi fayllar, Demo fayllar, Video darslik, Yordam, Ro'yxatdan o'tish | P0 |
| **New timetable wizard** | Bo'sh loyihani bosqichma-bosqich sozlash | Maktab nomi/manzili, o'quv yili, kunlar soni, darslar soni, qo'ng'iroq vaqtlari, tanaffuslar, shanba/yakshanba, 0-dars, 2 smena | P0 |
| **Import wizard** | Tashqi manbadan ma'lumot olish | Excel/clipboard'dan yopishtirish, aSc XML import, o'quvchi tanlovlarini import | P1 |

### 2.2 Ma'lumot kiritish (Basic data)

| Ekran | Maqsad | Asosiy amallar | Ust. |
|---|---|---|---|
| **Teachers** | O'qituvchilar ro'yxati | CRUD, qisqa nom, rang, ikonka/avatar, maksimal soat, kunlik cheklov, "oynalar" (gaps) limiti, ish kunlari, sinf rahbari sifatida biriktirish | P0 |
| **Classes (sinflar)** | Sinflar ro'yxati | CRUD, qisqa nom, sinf rahbari, home classroom, kun/dars sonini alohida belgilash, grade | P0 |
| **Subjects (fanlar)** | Fanlar ro'yxati | CRUD, rang, ikonka (`pictures/` dan 100+ fan ikonkasi), xona talabi, ketma-ket qo'yish qoidalari, kuniga max soat | P0 |
| **Classrooms (xonalar)** | Xonalar va binolar | CRUD, sig'im, bino/qavat, xonalar guruhi (interchangeable rooms), band bo'lish cheklovi | P0 |
| **Groups / Divisions** | Sinfni guruhlarga bo'lish | Bo'linish sxemasi (1/2, 1/3, ...), guruh nomlari, sinflararo birlashtirish (2 ta sinfning o'g'il bolalari birga), Odd/Even (juft/toq hafta) guruhlari | P0 |
| **Lessons (darslar)** | Dars kartochkalarini yaratish | Fan + o'qituvchi(lar) + sinf/guruh(lar) + xona(lar) + hafta soati + bir dars davomiyligi; bir darsga bir necha o'qituvchi va xona; terms/weeks biriktirish | P0 |
| **Students** | O'quvchilar ro'yxati va tanlovlari | CRUD, sinfga biriktirish, guruhga/seminarga yozilish, import | P1 |
| **Seminars** | Tanlov fanlari (yuqori sinflar) | Seminar guruhlari, o'quvchi tanlovlari, avtomatik guruhlash, "seminar block" | P2 |
| **Bell times (qo'ng'iroq)** | Dars vaqtlari jadvali | Har bir dars uchun boshlanish/tugash, tanaffuslar, kunga xos vaqtlar (`resources/def/def_zvon.dat` — standart shablon) | P1 |
| **Terms / Weeks / Days** | Ko'p-davrli jadval | 2..5 ta semestr/hafta variantini yaratish, darsni ma'lum davrga biriktirish | P2 |
| **Custom fields** | Maydon qo'shish | Har bir obyekt turiga (sinf, o'qituvchi, ...) matn/son maydon qo'shish, chop etishda ishlatish | P2 |

### 2.3 Cheklovlar

| Ekran | Maqsad | Asosiy amallar | Ust. |
|---|---|---|---|
| **Time-off (bandlik) grid** | O'qituvchi/sinf/xona/fan uchun kun×dars matritsasi | Katakni bosib "mumkin / mumkin emas / iloji boricha emas" belgilash, ommaviy tanlash | P0 |
| **Card relationships** | Kartochkalar orasidagi qoidalar | "Bir kunda bo'lmasin", "ketma-ket", "bir vaqtda", "1-yarmida", muhimlik darajasi (strict / high / normal / low / optimize / alternate / off) | P1 |
| **Constraint gallery ("clean")** | Tayyor cheklov shablonlari | 0-dars, 2-darsdan boshlash, 2+1 model, 2 smena, 8 kunlik hafta, shanba, ketma-ketlik, bo'sh oynalar limiti, bir binoda qolish, bir xil dars vaqti | P1 |
| **Advanced parameters** | Algoritm sozlamalari | Optimizatsiya og'irliklari, qat'iylik darajasi, vaqt limiti | P2 |

### 2.4 Jadval (asosiy ish maydoni)

| Ekran | Maqsad | Asosiy amallar | Ust. |
|---|---|---|---|
| **Timetable grid (main)** | Asosiy drag-drop tahrir maydoni | Kartochkani sudrash, joylashtirish, o'chirish, lock/unlock, xona biriktirish, zoom (+/-), rang invert (*), CTRL bilan guruhdagi hamma kartani birga ko'chirish, SHIFT bilan mumkin pozitsiyalarni ko'rsatish, undo/redo | P0 |
| **Unplaced cards panel** | Joylashtirilmagan kartochkalar | Kartani qo'lga olish, qolgan sonini ko'rish (o'ng-past burchakdagi raqam), filtrlash | P0 |
| **View switcher** | Jadvalni turli kesimda ko'rish | Sinf bo'yicha / o'qituvchi bo'yicha / xona bo'yicha / fan bo'yicha / o'quvchi bo'yicha / umumiy (whole) / grid / nazorat (supervisions) | P0 |
| **Card info / problem dialog** | "Nega joylashmayapti?" | Tanlangan karta uchun to'siq bo'layotgan cheklovlar ro'yxati, har birini bosib manbaga o'tish | P1 |
| **Generation dialog** | Avto-generatsiya | Boshlash/to'xtatish, progress, joylashgan/qolgan kartalar, "relax"/"strict" rejim, natijani qabul/bekor qilish | P0 |
| **Test / Verification** | Generatsiyadan oldin ma'lumotni tekshirish | Barcha xatolar ro'yxati, jiddiylik darajasi, "Fix it" tugmasi, bitta sinfni alohida test qilish | P0 |
| **Advisor panel** | Interaktiv maslahat | Muammoni tabiiy tilda tushuntirish, yechim variantlari, yordam havolasi | P2 |
| **Statistics** | Yuklama tahlili | O'qituvchi soatlari, bo'sh oynalar, xona bandligi, kunlik taqsimot diagrammalari | P1 |
| **Compare timetables** | Ikki variantni solishtirish | Farqlarni ajratib ko'rsatish | P2 |

### 2.5 Chop etish va eksport

| Ekran | Maqsad | Asosiy amallar | Ust. |
|---|---|---|---|
| **Print preview** | Chop etishdan oldingi ko'rinish | Sahifalar orasida o'tish, zoom, dizayn tanlash, rang rejimi, filtr (qaysi sinflar/o'qituvchilar), o'lchamlar, qo'shimcha ustunlar, chiqish | P0 |
| **Report picker** | Hisobot turini tanlash | 24 ta tur: single_class, single_teacher, single_room, single_subject, single_student, single_lessons, summary_* (6 ta), poster_classes, poster_teachers, poster_classrooms, master, lessongrid, inspektion, custom | P0 |
| **Print designer** | Chop etish shablonini vizual tahrirlash | Obyekt qo'shish (matn / rasm / jadval / legenda), joylashuv (rect), shrift, rang, chegara, maydon bog'lash (`{#OBJ:#FIELD}`), legenda turi va ustunlari | P1 |
| **HTML export / Publish** | Veb uchun jadval nashri | Papka tanlash, qaysi ko'rinishlar (sinf/o'qituvchi/xona/o'quvchi/almashtirish), frame'li sayt yaratish, EduPage'ga yuklash | P1 |
| **Excel export** | Elektron jadvalga chiqarish | Shablon tanlash (contracts, supl, students_in_*, room_supervisions, mamlakatga xos) | P1 |
| **XML import/export** | Tizimlararo integratsiya | Basic data / lesson grid / lessons / timetable qismlarini alohida yoki birga | P1 |
| **Mobile export** | Telefon ilovasi | J2ME MIDlet yaratish (`asctt.jar`) — bugungi analogi: PWA/mobil ilova uchun JSON | P2 |

### 2.6 O'rinbosarlik (Substitution)

| Ekran | Maqsad | Asosiy amallar | Ust. |
|---|---|---|---|
| **Substitution main (kalendar)** | Kun tanlash va shu kunning holati | Bugun / oldingi / keyingi kun, hafta/oy/yil ko'rinishi, kun ichidagi jadval | P1 |
| **Missing teachers** | Yo'q o'qituvchilarni belgilash | O'qituvchi + sana oralig'i + sabab (kasallik, kurs, ta'til, xizmat safari), qisman kun, ommaviy nusxalash ("copy missings") | P1 |
| **Substitution assignment** | Har bir bo'sh darsga o'rinbosar tayinlash | Nomzodlar ro'yxati (raqobatchilar — "competitors"), tavsiya reytingi, o'zgartirish turi (o'rinbosarlik / birlashtirish / bekor qilish / darsni ko'chirish), xona o'zgartirish | P1 |
| **Supervision (navbatchilik)** | Tanaffusdagi navbatchilikni taqsimlash | Xona×kun×tanaffus matritsasi, o'qituvchi biriktirish | P2 |
| **Substitution reports** | Chop etish/nashr | Kunlik almashtirishlar, o'qituvchi bo'yicha, o'quvchilar uchun e'lon, yo'qliklar hisoboti, ballar hisoboti | P1 |
| **Substitution settings** | Qoidalar | Ball tizimi, ustunlik qoidalari, ustunlar tarkibi, kunlik izoh | P2 |

### 2.7 Tizim

| Ekran | Maqsad | Asosiy amallar | Ust. |
|---|---|---|---|
| **Settings (basic/advanced)** | Dastur sozlamalari | Til, tema (skin), avto-saqlash, backup, chop etish standarti | P0 |
| **License / Registration** | Litsenziya | Kalit kiritish, sotib olish, versiya cheklovlarini ko'rish | P1 |
| **Help / Tips / Video** | O'qitish | Kun maslahati (`resources/tips/`), video darslar, onlayn yordam, jonli qo'llab-quvvatlash | P1 |

---

## 3. Chop etish / eksport tizimi

### 3.1 Dizayn shabloni tili (`designs/<Name>/def.xml`)

Har bir dizayn — bitta papka: `def.xml` + shu papkadagi rasm fayllari (`.emf`, `.png`, `.jpg`).
`designs/general/SchoolLogo_template.png` — barcha dizaynlar uchun umumiy logo o'rni.

**Fayl tuzilishi:**

```xml
<>
  <head version="20" name="aSc Blue" rtl="0"/>
  <PrintObject       .../>   <!-- matn / rasm / jadval o'rni -->
  <PrintObjectLegend .../>   <!-- legenda yoki "lessons table" -->
  <TimeTableSettings .../>   <!-- jadval katakchalari uslubi -->
</>
```

**`m_nTyp` — obyekt turi:**

| Qiymat | Ma'nosi |
|---|---|
| `0` | **Timetable placeholder** — jadval to'ridining o'zi shu to'rtburchakka chiziladi |
| `1` | **Matn yoki rasm** — `m_text` bo'sh bo'lmasa matn, `m_strBmp` bo'lsa rasm |
| `3` | **Legenda / jadval** (`PrintObjectLegend`) |

**Joylashuv:** `m_rect.left/top/right/bottom` — sahifaga nisbatan **0..1 000 000 normallashtirilgan koordinata**
(ya'ni sahifa o'lchamiga bog'liq emas → A4/A3/portret/landshaft'ga bir xil moslashadi).
`m_align`: 0 = markaz, 7 = chapga, 8 = o'ngga. `m_bOnTop` — z-tartib.

**Matn ichidagi maydon bog'lash (data binding):**

| Sintaksis | Ma'nosi | Misol |
|---|---|---|
| `{#NNNN}` | Lokalizatsiya lug'atidagi matn | `{#1635}` → "Name" |
| `{#OBJ:#FIELD}` | **Obyekt.Maydon** qiymati | `{#1035:#1635}` → Class.Name |
| `{!#NNNN}` | Maydon **sarlavhasi** (tarjima qilingan yorliq) | `{!#1502}: {#1035:#1532}` → "Timetable: Ivanov" |
| `{#OBJ:MatnNomi}` | **Custom field** nomi bo'yicha | `{#1035:Tlačivo - typ}` |
| `{sum}` | Ustun yig'indisi (footer'da) | — |
| `{}` | Standart (avtomatik) sarlavha | — |

Dekodlangan asosiy ID'lar (chop etish uchun kerakli minimal to'plam):

| ID | Ma'nosi | ID | Ma'nosi |
|---|---|---|---|
| 1035 | Class (obyekt) | 1048 | Teacher (obyekt) |
| 3148 | School (obyekt) | 1092 | Classroom |
| 1635 | Name | 1532 | Class teacher |
| 1067 | Home classroom | 1502 | Timetable |
| 1166 | Name of the school | 1167 | Academic year |
| 4055 | School logo | 3205 | Registration name |
| 1021 | Subjects | 1075 | Lessons/week |
| 1266 | Count | 3956 | Language |

**Shrift:** `<font ratio="0.0844" weight="400" italic="0" faceName="Arial"/>` —
o'lcham **absolute emas, sahifa balandligiga nisbatan koeffitsient** (`ratio`).
Manfiy `ratio` = "avtomatik moslash" rejimi. `m_bAutoFont` — matnni maydonga sig'dirish.
`fontheader` / `fontfooter` — jadval sarlavhasi/pastki qatori uchun alohida shrift.

**Ranglar va chiziqlar:** `m_bgColor`, `m_fontColor`, `m_lineColor`,
`m_lineTop/Bottom/Left/Right/Middle` (0 = yo'q, 1..8 = qalinlik).

**`TimeTableSettings` — jadval to'ridining uslubi:**

| Atribut | Vazifasi |
|---|---|
| `m_ColorRectRowHeader` | Kun (qator) sarlavhasi — fon gradiyenti (`BackColor1`→`BackColor2`), shrift rangi, padding, fon rasmi |
| `m_ColorRectColumnHeader` | Dars raqami (ustun) sarlavhasi |
| `m_ColorRectCard` | Kartochka (dars) katakchasi |
| `m_ColorRectBreak` | Tanaffus ustuni |
| `m_ColorRectVnutro` | Jadval ichki foni |
| `m_nSirkaCiaryLesson` / `Okraj` / `Den` | Dars / tashqi chegara / kun ajratuvchi chiziq qalinligi |
| `m_bPrintFarebne` | Rangli bosib chiqarish |
| `m_nFarba1` / `m_nFarba2` / `m_nFarbaLeftOnly(Triangle)` | Kartochka rangi qaysi obyektdan olinadi (fan/o'qituvchi/xona) va qanday bo'linadi (yarmi/uchburchak) |
| `m_strFont` | Butun jadval uchun majburiy shrift (masalan "Brush Script MT" — Handwritten dizayni) |

**`PrintObjectLegend` — legenda va "lessons table":**

`m_LegendaType` qiymatlari (namunalardan aniqlangan):

| Qiymat | Legenda turi |
|---|---|
| `0` | **Fanlar** (rasm bilan ishlashi mumkin — `m_bUsePictures=1`) |
| `2` | **Xonalar** |
| `3` | **O'qituvchilar** |
| `8` | **Lessons table** — sinf/o'qituvchining to'liq dars ro'yxati jadvali |

Qo'shimcha: `m_bUseColors` (obyekt rangini ko'rsatish), `m_nColumns` (legendani necha ustunga yoyish).

`Reportdata` — jadval ko'rinishi:
`m_bPrintHeader/Footer`, `m_nDrawBorders`, `m_nDrawMainBorder`, `m_ColorHeader/Footer/Odd/Even`
(zebra qatorlar), `m_fontColorLegenda/Header/Footer`, `m_nHeaderHeight/FooterHeight`,
`m_nPaddingXY`, `m_bSummaryBellow` (yig'indi pastda), `m_bGroupBySubject`, `m_bMHodina`.

`columns` → `column nID nWidth strUserHeaderName strUserFooterName`:

| nID | Kontekst | Ma'nosi |
|---|---|---|
| 1, 2, 3, 6 | Legenda (o'qituvchi/xona/fan) | to'liq nom / qisqa nom / tartib raqami / qo'shimcha |
| 28000 | Lessons table | Fan |
| 28001 | Lessons table | O'qituvchi |
| 28004 | Lessons table | Soat (count) — footer `{sum}` |
| 28006 | Lessons table | Xona |
| 28008 | Lessons table | Guruh |

`nWidth` — foizda (jami ≈ 100).

### 3.2 Tayyor dizaynlar inventari

| Dizayn | Mo'ljallangan hisobot | Xususiyat |
|---|---|---|
| `Sample Blue` / `Sample Green` / `Sample Grey` | Umumiy sinf/o'qituvchi jadvali | Faqat rang sxemasi farq qiladi; minimal (2 ta matn obyekt + jadval) |
| `Sample Handwritten` | O'quvchiga beriladigan qo'lyozma uslubdagi jadval | `m_strFont="Brush Script MT"`, ingichka chiziqlar |
| `Sample Blue 2` | Maktab brendi bilan bezatilgan jadval | Logo (`picture1.emf`), fon (`picture2.emf`, `picture4.emf`), fan ikonkasi (`biology3.png`) |
| `Sample Blue 2 with Legends` | Devorga osiladigan poster | 3 ta legenda: o'qituvchilar (2 ustun), fanlar (rasm bilan), xonalar |
| `Sample Blue 2 with Lessons table` | Sinf jadvali + dars ro'yxati | `m_LegendaType=8` (fan/guruh/o'qituvchi/xona/soat + `{sum}`) + xonalar legendasi |
| `internal_table` | Rasmiy sinf blanki (ichki hujjat) | Jadval chapda, o'ngda vertikal "lessons table"; sarlavha, o'quv yili, imzo joylari |
| `internal_table_teacher` | Rasmiy o'qituvchi blanki | Xuddi shu, lekin o'qituvchi konteksti |
| `internal_table_sk` | Slovakiya rasmiy blanki (mamlakatga xos namuna) | To'liq bezatilgan: "Názov školy", "Trieda", "Školský rok", "triedny učiteľ" / "riaditeľ školy" imzo chiziqlari, custom field'lar |
| `general/` | Umumiy resurs | `SchoolLogo_template.png` |

**Muhim xulosa:** aSc'da chop etish = **erkin joylashtiriladigan obyektlar to'plami** (absolute-positioned canvas),
Word-uslubidagi oqim (flow) emas. Bu foydalanuvchiga to'liq erkinlik beradi va aynan shu narsa
uni raqobatchilardan ajratib turadi (rasmiy davlat blanklarini piksel aniqligida takrorlash imkoni).

### 3.3 HTML eksport (`template/Web/`)

Oddiy **token-almashtirish** shabloni (shablon dvigateli yo'q):

| Token | Ma'nosi |
|---|---|
| `{CHARSET}` | `<meta charset>` qatori |
| `{!#NNNN!}` | Lokalizatsiya matni (HTML variantida `!` bilan o'ralgan) |
| `{INSERTTABLE}` | Asosiy jadval HTML'i shu yerga qo'yiladi |
| `{MissingTeachers}` | Yo'q o'qituvchilar bloki |
| `{dayname}`, `{date}`, `{DateTime}` | Sana/kun |
| `{substStudentsHTML}`, `{substTeachersHTML}` | Sahifalararo havolalar |
| `{SchoolName}`, `{CREATEDBY}`, `{note}` | Pastki kolontitul |
| `{STUDENT}` | O'quvchi ismi (`student.htm`) |

Fayllar: `substindex.htm` (frameset: chapda kunlar ro'yxati, o'ngda kontent),
`substLeft.htm` (navigatsiya — kunlar), `substTeachers.htm` (o'qituvchilar uchun almashtirish),
`substStudents.htm` (o'quvchilar uchun e'lon), `student.htm` (bitta o'quvchi jadvali),
`subst.css` (`th` + `td.table_row1/row2` zebra).
`template/Web/fl/` — Flash-asosidagi interaktiv jadval ko'rgichi (**eskirgan, takrorlash shart emas**).

### 3.4 Excel eksport (`template/excelexport/`)

Format — **SpreadsheetML 2003** (`.xml`), ya'ni tayyor Excel fayl ichida marker'lar.
Shablon tili — qator-bandlari (row bands):

| Direktiva | Vazifasi |
|---|---|
| `{*header}` | Sarlavha qatori (bir marta) |
| `{*separator}` | Guruhlar orasidagi ajratuvchi qator (masalan yangi sinf yoki yangi oy) |
| `{*firstrow}` | Ma'lumotlar blokining birinchi qatori |
| `{*repeat}` | Takrorlanuvchi qator (asosiy shablon) |
| `{*lastrow}` | Oxirgi qator (chegara uchun) |
| `{#NNNN}` | Lokalizatsiya matni |
| `{field}` | Ma'lumot maydoni |
| `{cf:kod}` | Custom field |
| `{$N}`, `{$N/M}` | N-dars ustuni / N va M orasidagi tanaffus ustuni |

Tayyor shablonlar: `export_contracts_template.xml` (o'qituvchi yuklamasi/shartnoma:
`{teacher} {subject} {group} {count} {length} {total} {classrooms}`),
`export_supl_template.xml` (o'rinbosarlik ballari: `{date} {period} {class} {subject} {reason} {type} {points}`,
oyma-oy guruhlangan, `{totalpointsyear}`), `room_supervisions_template.xml`
(xona×kun×tanaffus navbatchiligi: `{spvision_N}`, `{spvision_N/M}`),
`students_in_groups/seminars/subjects.xml`, `oman_attendance_report.xml`,
`turkey/` + `turkey_teachers_template.xml` (mamlakatga xos rasmiy hisobotlar).

### 3.5 Yangi loyihada qanday takrorlash

| aSc'dagi mexanizm | Tavsiya etilgan zamonaviy ekvivalenti |
|---|---|
| `def.xml` absolute canvas | **JSON design descriptor** (`{type, rect:{l,t,r,b} 0..1 normalized, style, binding}`) + React-based visual designer |
| `{#OBJ:#FIELD}` binding | Nomlangan yo'llar: `{{class.name}}`, `{{school.logo}}`, `{{class.customFields.formType}}` |
| Chop etish (printer/PDF) | Server tarafda **HTML → PDF** (Chromium/Playwright yoki QuestPDF). Bir xil renderer preview va PDF uchun → WYSIWYG kafolatlanadi |
| `PrintObjectLegend` (4 tur) | Reusable "block" komponentlar: `TimetableGrid`, `EntityLegend(kind)`, `LessonsTable(columns)` |
| `TimeTableSettings` | CSS custom properties to'plami (`--card-bg`, `--header-gradient`, `--line-day`) — bitta tema obyekti |
| HTML eksport (frameset) | **Statik SPA** yoki server-rendered sahifalar; frameset o'rniga responsive layout + deep-link |
| SpreadsheetML shablon | **ClosedXML / EPPlus** + shu `{*repeat}` band mantiqini saqlab qolish (foydalanuvchi shablonni Excel'da tahrirlay olishi katta ustunlik) |
| J2ME MIDlet | PWA / Flutter mobil ilova + JSON API |
| `rtl="0"` | Dizayn darajasida `dir` qo'llab-quvvatlash (arab/ibroniy bozori uchun) |

---

## 4. Drag-drop jadval tahrirlash UX

Bu bo'lim `resources/tips/tips_en.txt` dagi rasmiy tavsifga asoslangan — aSc'ning eng qimmatli
UX bilimi shu yerda jamlangan.

### 4.1 "Karta qo'lda" (card-in-hand) modeli

aSc HTML5 drag-and-drop'dan farqli **"pick up / put down"** modelidan foydalanadi:

1. Foydalanuvchi kartochkani bosadi → karta "qo'lga olinadi" (kursorga yopishadi).
2. Kursor jadval ustida harakatlanadi → **kun va dars sarlavhalari real vaqtda rangga bo'yaladi**:
   - **Kulrang** — taqiqlangan pozitsiya (fan/sinf o'qitilmaydi yoki o'qituvchi band).
   - **Ko'k** — ruxsat etilgan, lekin yaxshi emas.
   - **Yashil** — bu karta uchun yaxshi pozitsiya.
3. Kerakli katakka bosiladi → joylashtiriladi.
4. Kartadan voz kechish: o'ng tugma yoki jadvaldan tashqariga bosish.

> Bu model sensorli ekranda ham, sichqonchada ham bir xil ishlaydi va uzun sudrash
> harakatidan ko'ra ishonchliroq. **Yangi loyihada shuni takrorlash tavsiya etiladi**
> (HTML5 DnD o'rniga click-select → click-place, ixtiyoriy sudrash qo'shimcha rejim sifatida).

### 4.2 Jonli fikr-mulohaza (live feedback)

| Signal | Mexanizm |
|---|---|
| **Ruxsat etilgan pozitsiyalar** | Karta qo'lda bo'lganda — kun/dars sarlavhalari 3 rangda |
| **Possible positions (preview)** | **SHIFT** bosib turilganda — kursor ostidagi (qo'lda bo'lmagan) karta uchun mumkin pozitsiyalar yoritiladi |
| **Nega joylashmaydi?** | Karta ustida o'ng tugma → **Info...** → to'siq bo'layotgan cheklovlar ro'yxati |
| **Joylashtirilmagan kartalar soni** | Kartaning o'ng-past burchagidagi raqam |
| **Lock ko'rsatkichi** | Qulflangan kartaning o'ng-past burchagida **kulrang chiziq** |

### 4.3 Kontekst menyular

- **Bo'sh katakda o'ng tugma** → shu pozitsiyaga qo'yish mumkin bo'lgan kartalar ro'yxati (reverse lookup — juda kuchli usul).
- **Karta ustida o'ng tugma** → O'chirish / Lock / Unlock / Xona biriktirish / Info.
- **Sinf sarlavhasida o'ng tugma** → shu sinfni test qilish.
- **Sinf sarlavhasida chap tugma** → shu sinfning kartalari bilan ishlash rejimiga o'tish.

### 4.4 Klaviatura va modifikatorlar

| Tugma | Amal |
|---|---|
| `SHIFT` (ushlab turish) | Kursor ostidagi karta uchun mumkin pozitsiyalarni yoritish |
| `CTRL` + bosish | Bir pozitsiyadagi **barcha guruh kartalarini birga** ko'chirish |
| `CTRL` + ustun sarlavhasi | Ko'p ustunli saralash |
| `+` / `-` | Zoom |
| `*` | Matn ranglarini invert qilish |
| Undo / Redo | **Oxirgi 100 amal** |

### 4.5 Lock (qulflash) semantikasi

- Qulflangan karta **generatsiya davomida joyidan qimirlamaydi**.
- Qo'lda yaratilgan "oyna" (bo'sh dars) ham qulflash orqali saqlanadi — algoritm o'zi oyna
  yaratmaydi, lekin qo'lda yaratilganini hurmat qiladi (Demo2 tavsifi).
- Vizual: kulrang burchak chizig'i (`skins/actions/lock_16.png`, `unlock_16.png`).

### 4.6 Yangi loyiha uchun texnik talablar

- Ziddiyat tekshiruvi **klient tarafda, <16 ms ichida** (60 fps) — server so'roviga chiqmasdan.
  Har bir karta uchun `Set<slotIndex>` shaklidagi oldindan hisoblangan bitmask.
- Uch darajali baholash: `forbidden | allowed | preferred` (aSc'dagi kulrang/ko'k/yashil).
- Undo/redo — komandalar stack'i (min. 100 qadam), har bir komanda serializable.
- Virtualizatsiya: 40+ sinf × 8 kun × 12 dars uchun ham silliq scroll.
- Ko'p foydalanuvchili rejim (yangi imkoniyat): kartochka darajasida optimistik lock.

---

## 5. O'rinbosarlik (substitution) moduli — talablar

> **Eslatma:** `supl/main.spl` fayli aslida **OpenPGP maxfiy kaliti** (modul litsenziyasi/imzosi uchun),
> mantiq emas. Modul tuzilishi `skins/toolbar/subst/` (40+ ikonka), `skins/reports/subst/`,
> `template/Web/subst*.htm`, `template/excelexport/export_supl_template.xml` va
> `skins/markup/roz_supl*.xaml` asosida tiklandi.

### 5.1 Ma'lumot modeli

| Obyekt | Maydonlar |
|---|---|
| **Absence (yo'qlik)** | O'qituvchi, boshlanish/tugash sanasi, dars oralig'i (to'liq kun yoki 3–5-darslar), **sabab** (`{#1464}` — kasallik, kurs, ta'til, xizmat safari, "hospitalization"), izoh |
| **SubstitutionRecord** | Sana, dars raqami, sinf, fan, asl o'qituvchi, **o'rinbosar**, **turi** (`{#1483}`), xona, **ball** (`{#2693}`) |
| **Substitution type** | O'rinbosarlik / Sinflarni birlashtirish / Bekor qilish (dars o'tkazilmaydi) / Ko'chirish (boshqa vaqtga) / Nazorat ostida qoldirish |
| **Supervision (navbatchilik)** | Sana, tanaffus (`N/M`), xona/koridor, mas'ul o'qituvchi |
| **DailyRemark** | Kunga umumiy izoh (`{note}` — chop etishda va veb'da chiqadi) |

### 5.2 Funksional talablar

**F1. Kalendar navigatsiyasi** — bugun / oldingi / keyingi kun, hafta, oy, yil ko'rinishi,
ixtiyoriy oraliq (`skins/listicons/timeinterval/` — day_prev/this/next, week_*, month_*, month_till_today, month_custom).

**F2. Yo'qliklarni kiritish** — o'qituvchini tanlash, sana oralig'i, sabab, qisman kun;
oldingi kundan nusxalash (`copy_missings_32.png`); ommaviy o'chirish (`patient_delete_32`).

**F3. Bo'sh darslarni avtomatik aniqlash** — yo'q o'qituvchining shu kundagi barcha darslari
avtomatik "hal qilinishi kerak" ro'yxatiga tushadi.

**F4. Nomzodlarni tavsiya qilish** (**modulning yuragi**) — har bir bo'sh dars uchun mumkin
bo'lgan o'rinbosarlar ro'yxati, saralash mezonlari:
- shu vaqtda bo'sh (oynasi bor) o'qituvchilar;
- shu fanni o'qita oladiganlar;
- shu sinfga darsi borlar / sinf rahbari;
- yig'ilgan ballari kam bo'lganlar (adolatli taqsimot);
- haftalik normadan oshib ketmaydiganlar (`overtime_32.png`).
`competitors_16/32.png` — "raqobatchilar" ro'yxati UI'si mavjudligini tasdiqlaydi.

**F5. Ball tizimi (points)** — har bir o'rinbosarlik turi uchun ball; oylik va yillik
yig'indi (`{totalpointsyear}`); Excel'ga eksport → maosh/ustama hisobiga asos.

**F6. Dars o'zgartirish amallari** — `change_lesson_32`, `remove_supl_32`, `select_next_32`,
`dayintimetable_32` (kunni asosiy jadval kontekstida ko'rish), `view_timetable_32`.

**F7. Nazorat/navbatchilik (supervision)** — xona×kun×tanaffus matritsasi,
`skins/supervision/uh{0..2}{0..2}.png` — 3×3 holat (bo'sh / qisman / to'la × normal / ogohlantirish / xato).

**F8. Chop etish va nashr:**
- `subst_daily` — kunlik almashtirishlar varag'i (o'qituvchilar xonasi uchun);
- `subst_teacher` — o'qituvchi bo'yicha;
- `subst_student` — o'quvchilar uchun e'lon (`{#2087}` = "Information for students");
- `summary_absence`, `summary_subst` — davr bo'yicha jamlanma;
- `calendar_month_2`, `calendar_year` — kalendar ko'rinish;
- HTML nashr: `substindex.htm` frameset (chapda kunlar, o'ngda kontent),
  o'qituvchi ↔ o'quvchi sahifalari o'zaro havolalangan;
- Excel: `export_supl_template.xml`.

**F9. Sozlamalar** — ustunlar tarkibi (`columns_32`), tavsiya qoidalari (`options_32`,
`customize_32`), yangi o'quv yiliga o'tish (`newyear_32`), backup (`backup_32`).

**F10. Wizard** — `subst/wizard_32.png`: yangi foydalanuvchi uchun bosqichma-bosqich sozlash.

### 5.3 Yangi loyiha uchun ustuvorlik

`P1`: F1, F2, F3, F4 (oddiy reyting), F8 (kunlik varaq + o'quvchi e'loni).
`P2`: F5 (ballar), F6 (murakkab amallar), F7 (navbatchilik), F9, F10.

---

## 6. UI mavzulari (skins)

**Tuzilishi:** `skins/` ikki xil narsani birlashtiradi:

1. **Ikonka kutubxonalari** (mavzudan qat'i nazar umumiy) — 23 ta funksional kategoriya:
   `toolbar/` (asosiy panel, 150+), `dialogs/` (dialog ikonkalari), `dlgribbon/` (ribbon tugmalari),
   `actions/`, `listicons/`, `views/`, `reports/`, `relations/`, `verification/`, `markup/`,
   `clean/` (cheklov shablonlari, 64px), `flags/` (50+ davlat), `gener/`, `supervision/`,
   `term_week_day/`, `printing/`, `configure/`, `substitution/`, `country/`, `mainscreen/`, `vendor/`.

2. **Haqiqiy mavzular (theme)** — `skins/default/` + `skins/GreenNote/` + `skins/PinkLady/`
   (oxirgi ikkitasi hozir bo'sh — `default` dan meros oladi).

**Mavzu deskriptori: `skins/default/popis.xml`** (Slovakcha "popis" = "tavsif"):

```xml
<skin name="Blue Book">
  <pozadie name="pozadie" fillcolor="#dde7ee"/>          <!-- fon -->
  <pozadie name="kto_suplovat" fillcolor="#ffffff"/>      <!-- "kim o'rinbosar" paneli -->
  <table name="tabulka_zosit">
    <pozadie name="table_header" fillcolor="#dde7ee"/>
    <img name="table_header_sort_up|down|mouseover" file="sort_*.bmp"/>
    <pozadie name="table_row|table_row_sel|table_row_1|table_row_2"
             resizeable="whole" img="..." x1="2" x2="15" y1="2" y2="15"/>  <!-- 9-slice -->
    <column name="stav_vyriesenia"
            value0="nezadane"     img0="unknown.bmp"        <!-- kiritilmagan -->
            value1="vyriesene"    img1="happy.bmp"          <!-- hal qilingan -->
            value2="kolizia"      img2="sad.bmp"            <!-- ziddiyat -->
            value3="chyba_ucebna" img3="nearly_happy.bmp"/> <!-- xona muammosi -->
  </table>
  <color name="pozadie_zosit" rgb="#FFFFFF"/>
  <splitter name="horizontal|vertical" width="2"><pozadie fillcolor="#dde7ee"/></splitter>
</skin>
```

**Muhim naqshlar:**

- **9-slice (scale-9) rasm cho'zish**: `x1/x2/y1/y2` — chekka piksellarni cho'zmasdan
  markazni cho'zish (CSS `border-image` ekvivalenti).
- **Holat → ikonka mapping'i** jadval ustuni darajasida deklarativ belgilanadi
  (`stav_vyriesenia` = "hal qilish holati"): bu yangi loyihada **status → badge** komponenti sifatida takrorlanadi.
- Zebra qatorlar (`table_row_1` / `table_row_2`) va tanlangan qator (`table_row_sel`) alohida.

**Yangi loyihada:** `popis.xml` o'rniga **CSS custom properties + design tokens** (JSON):
`--surface`, `--surface-alt`, `--row-odd`, `--row-even`, `--row-selected`, `--splitter`,
`--card-bg`, `--header-gradient-from/to`. Light/dark rejim + kamida 2 ta tayyor tema.
`skins/markup/*.xaml` — aSc'da tooltip/popup shablonlari ham tashqi fayl bo'lgan
(`roz_Seminar_tooltip.xaml`: `{Subject}` + `{Group}`); bu yangi loyihada oddiy React komponent bo'ladi.

---

## 7. Funksional backlog

### P0 — MVP (busiz mahsulot ishlamaydi)

| # | Feature |
|---|---|
| P0-01 | Loyiha (timetable document) CRUD: yaratish, ochish, saqlash, avto-backup |
| P0-02 | Setup wizard: maktab, o'quv yili, kunlar soni, darslar soni, tanaffuslar |
| P0-03 | Teachers CRUD (qisqa nom, rang, ikonka, max soat) |
| P0-04 | Classes CRUD (qisqa nom, sinf rahbari, home classroom) |
| P0-05 | Subjects CRUD (rang, ikonka kutubxonasi, xona talabi) |
| P0-06 | Classrooms CRUD (bino, sig'im, interchangeable rooms guruhi) |
| P0-07 | Groups/Divisions: sinfni 2/3/4 guruhga bo'lish, guruh nomlari |
| P0-08 | Lessons editor: fan + o'qituvchi(lar) + sinf/guruh + xona + hafta soati + davomiylik |
| P0-09 | Time-off grid: o'qituvchi/sinf/xona/fan uchun kun×dars bandlik matritsasi (3 holat) |
| P0-10 | Timetable grid renderer: kun×dars to'r, kartochka, guruh bo'linishi, zoom |
| P0-11 | Card-in-hand tahrirlash: qo'lga olish → joylashtirish → voz kechish |
| P0-12 | Live pozitsiya baholash: forbidden / allowed / preferred (kulrang/ko'k/yashil) |
| P0-13 | Card lock/unlock + vizual ko'rsatkich |
| P0-14 | Unplaced cards paneli + qolgan soni |
| P0-15 | Undo/redo (min. 100 qadam) |
| P0-16 | View switcher: sinf / o'qituvchi / xona / umumiy |
| P0-17 | Verification (test): ma'lumot to'liqligi + bajarilmas cheklovlarni topish |
| P0-18 | Generatsiya dvigateli: ishga tushirish/to'xtatish, progress, natijani qabul/rad |
| P0-19 | Chop etish: sinf jadvali, o'qituvchi jadvali, xona jadvali (tayyor 3 dizayn) |
| P0-20 | PDF eksport (print preview bilan bir xil renderer) |
| P0-21 | Til va tema sozlamalari (o'zbek/rus/ingliz) |

### P1 — Birinchi katta reliz

| # | Feature |
|---|---|
| P1-01 | Bell times editor (dars vaqtlari, tanaffuslar, kunga xos vaqtlar) |
| P1-02 | Students CRUD + sinfga/guruhga biriktirish |
| P1-03 | Card relationships: "bir kunda emas", "ketma-ket", "bir vaqtda" + muhimlik darajasi |
| P1-04 | Constraint gallery: 0-dars, 2 smena, shanba, gaps limiti, bir binoda qolish, kuniga max soat |
| P1-05 | Card info dialog: "nega joylashmayapti" — to'siq cheklovlar ro'yxati + manbaga o'tish |
| P1-06 | SHIFT bilan "possible positions" preview |
| P1-07 | Kontekst menyu: bo'sh katakda "shu yerga mumkin kartalar" reverse lookup |
| P1-08 | CTRL bilan guruh kartalarini birga ko'chirish |
| P1-09 | Statistics: o'qituvchi soatlari, oynalar, xona bandligi, kunlik taqsimot |
| P1-10 | Print designer (vizual shablon muharriri): obyekt qo'yish, maydon bog'lash, legenda |
| P1-11 | Legenda bloklari: o'qituvchilar / fanlar (rasm bilan) / xonalar / lessons table |
| P1-12 | Summary hisobotlar: barcha sinflar bir varaqda, barcha o'qituvchilar, master jadval |
| P1-13 | Poster hisobotlar (devorga osish uchun A3/A2) |
| P1-14 | HTML eksport: statik sayt (sinf/o'qituvchi/xona/o'quvchi jadvallari, deep-link) |
| P1-15 | Excel eksport shablon dvigateli (`{*header}/{*repeat}/{*separator}` bandlari) |
| P1-16 | O'qituvchi yuklamasi (contracts) hisoboti |
| P1-17 | XML import/export (basic data, lesson grid, lessons, timetable) |
| P1-18 | Excel/clipboard'dan import (sinflar, o'qituvchilar, fanlar ro'yxati) |
| P1-19 | Substitution: kalendar + yo'qliklarni kiritish + bo'sh darslarni aniqlash |
| P1-20 | Substitution: o'rinbosar nomzodlarini tavsiya qilish (reyting bilan) |
| P1-21 | Substitution: kunlik varaq chop etish + o'quvchilar uchun e'lon |
| P1-22 | Demo/tutorial fayllar kutubxonasi + bosqichma-bosqich o'quv rejimi |
| P1-23 | Litsenziya/ro'yxatdan o'tish oqimi |

### P2 — Keyingi bosqichlar

| # | Feature |
|---|---|
| P2-01 | Terms / Weeks (juft-toq hafta) / Days variantli jadval |
| P2-02 | Seminars: o'quvchi tanlovlari asosida avtomatik guruhlash |
| P2-03 | Custom fields (har bir obyekt turi uchun) + chop etishda ishlatish |
| P2-04 | Advisor: muammoni tabiiy tilda tushuntirish + "Fix it" avtomatik tuzatish |
| P2-05 | Compare timetables (ikki variant farqi) |
| P2-06 | Advanced generation parameters (og'irliklar, qat'iylik, vaqt limiti) |
| P2-07 | Substitution: ball tizimi + oylik/yillik hisobot Excel'ga |
| P2-08 | Supervision (tanaffus navbatchiligi) rejalashtirish + hisobot |
| P2-09 | Substitution: sinflarni birlashtirish / darsni ko'chirish amallari |
| P2-10 | Mobil ilova / PWA (o'qituvchi va o'quvchi uchun shaxsiy jadval + push) |
| P2-11 | Ko'p foydalanuvchili real-time hamkorlik (karta darajasida lock) |
| P2-12 | Onlayn nashr platformasi (EduPage analogi): rol asosida ko'rish, email/SMS xabar |
| P2-13 | Skin/tema muharriri (design tokens) + light/dark |
| P2-14 | RTL qo'llab-quvvatlash (arab/ibroniy) |
| P2-15 | Mamlakatga xos rasmiy blank shablonlari (O'zbekiston XTV formasi) |
| P2-16 | Attendance / davomat hisobotlari (Oman namunasi kabi) |
| P2-17 | Versiyalash: jadval variantlarini saqlash va qaytish |

---

## 8. Yangi loyiha uchun asosiy xulosalar

1. **Chop etish — aSc'ning eng kuchli tomoni va eng katta raqobat afzalligi.** 10 ta tayyor dizayn,
   normallashtirilgan koordinatalar, maydon bog'lash tili va 24 xil hisobot turi.
   Bunga MVP'da ham jiddiy resurs ajratish kerak — maktablar aynan "rasmiy blank"ni talab qiladi.

2. **"Card-in-hand" + uch rangli jonli fikr-mulohaza** — 20 yillik sinovdan o'tgan UX naqshi.
   HTML5 drag-and-drop'ni ko'r-ko'rona takrorlash o'rniga shuni implement qilish tavsiya etiladi.

3. **"Nega joylashmayapti?" (Info dialog)** — foydalanuvchi qoniqishini eng ko'p oshiradigan
   funksiya. Algoritm "yo'q" deganda **sababini ko'rsatishi shart**.

4. **Shablon tillari (Excel bandlari, HTML tokenlari)** foydalanuvchining o'zi shablonni
   tahrirlashiga imkon beradi — bu mamlakatga moslashuvni (O'zbekiston, 43 davlat namunasi kabi)
   kod yozmasdan hal qiladi.

5. **Substitution — alohida sotiladigan modul.** Uni MVP'ga qo'shmasdan, P1'da mustaqil
   funksional birlik sifatida qurish mantiqiy.

6. **Verification generatsiyadan oldin majburiy** — Demo tavsiflarida ta'kidlanganidek:
   "The most work to be done by a human is before starting the generation."
