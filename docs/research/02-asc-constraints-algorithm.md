# aSc TimeTables — Cheklovlar tizimi va jadval generatsiya algoritmi

> **Hujjat maqsadi:** aSc TimeTables (roz.exe, v2016) dasturining constraint model'ini va generation engine'ini
> teskari muhandislik (reverse engineering) yo'li bilan tiklash va yangi .NET/C# implementatsiyasi uchun
> to'liq texnik spetsifikatsiya berish.
>
> **Manbalar:**
> - `/Users/me/Projects/TimeTables/TimeTables/lang.asc` (4.7 MB, 60+ tilli string table) — **2738 ta ingliz string** ajratib olindi.
>   Format: `#<ID>` + `EN [text]`. Bu dasturning barcha UI matnlari — cheklovlar ro'yxatining eng ishonchli manbasi.
> - `/Users/me/Projects/TimeTables/TimeTables/resources/tips/tips_en.txt` (41 tip)
> - `/Users/me/Projects/TimeTables/TimeTables/roz.exe` — PE32 binary. **Packed/compressed**: `strings` bilan
>   hech qanday mazmunli identifikator topilmadi (faqat shovqin). Shu sabab algoritm rekonstruksiyasi
>   string table + public bilim asosida qilingan.
> - `resources/lang_q.asc`, `demos/Demo files description.txt`, `supl/main.spl`
>
> **Metodologiya:** `lang.asc` dagi string ID bloklari mantiqiy dialog'larga mos keladi
> (masalan `#3451..#3500` — "Grouped constraints" dialogining to'liq cheklov nomlari ro'yxati;
> `#1817..#1852` — verification fault xabarlari, ya'ni dastur **aslida tekshiradigan** shartlar ro'yxati).
> Verification fault matnlari eng qimmatli — ular constraint'ning **haqiqiy semantikasini** oshkor qiladi.

---

## 1. Cheklovlar katalogi

Jami **170 ta** aniq cheklov/parametr aniqlandi (13 ta kategoriyada). Har biri `C-<SCOPE>-<NN>` ID bilan.

**Ustunlar:** ID | Nomi (aSc UI matni) | Qamrovi | Parametrlari | H/S | Standart og'irlik (w)

`H` = hard (buzilmaydi), `S` = soft (jarima bilan), `H*` = odatda hard, lekin
"Allow relaxation" (`#3072`) yoqilganda soft'ga aylanadi.

Og'irliklar 0–1000 shkalada, aSc'ning `Importance` uch pog'onasiga moslangan
(`#3064 Normal`, `#3065 Low`, `#3066 High`, `#3073 Strict`):
`Low=10`, `Normal=100`, `High=500`, `Strict=∞ (hard)`.

---

### 1.1. Global / integrity (buzilmas asos) — `C-GBL`

| ID | Nomi | Qamrov | Parametrlar | H/S | w |
|---|---|---|---|---|---|
| C-GBL-01 | Teacher collision — `#1736`, `#1818 "Teacher %1 has two lessons at the same time"` | teacher | — | H | ∞ |
| C-GBL-02 | Class/Group collision — `#1738`, `#1817 "Class %1 shares two cards in the same position"` | class, group | — | H | ∞ |
| C-GBL-03 | Classroom collision — `#1737`, `#1827 "%1 two cards placed in the same classroom"` | classroom | — | H | ∞ |
| C-GBL-04 | Student collision (seminar/course model) — `#3503 "Students' requests conflict"` | student | — | H | ∞ |
| C-GBL-05 | Card must be placed in the timetable — `#3497` | card | — | H | ∞ |
| C-GBL-06 | Locked card must stay on its position — `#1843 "%1 locked on a wrong position"`, `#1846 (Locked)` | card | position | H | ∞ |
| C-GBL-07 | Card must have a classroom assigned — `#1828`, `#3499` | card | — | H* | ∞ |
| C-GBL-08 | Groups from different divisions cannot share a period — `#2640` (bir o'quvchi ikki joyda bo'la olmaydi) | class division | — | H | ∞ |
| C-GBL-09 | Class cannot be upgraded to full class — `#1847`, `#3490` (bo'lingan darslar to'liq darsga aylanmasin) | class | — | H | ∞ |
| C-GBL-10 | Lesson must exist in some week of cycle — `#1861` | lesson | weeks[] | H | ∞ |

---

### 1.2. Availability / Time-off — `C-AVL`

aSc'da har bir obyekt uchun **3 holatli** time-off matritsasi mavjud (`#18` tip):
`allowed (ko'k ✓)` / `question-marked (? — ruxsat, lekin yomon)` / `forbidden (qizil ✗)`.
Bu constraint model'ning markaziy elementi.

| ID | Nomi | Qamrov | Parametrlar | H/S | w |
|---|---|---|---|---|---|
| C-AVL-01 | Teacher not available at position — `#1752 "Wrong position %1, teacher not available"` | teacher | TimeOff mask[day][period] | H | ∞ |
| C-AVL-02 | Class not available at position — `#1751` | class | TimeOff mask | H | ∞ |
| C-AVL-03 | Subject not available at position — `#1753` | subject | TimeOff mask | H | ∞ |
| C-AVL-04 | Classroom not available at position — `#1900` | classroom | TimeOff mask | H | ∞ |
| C-AVL-05 | Forbidden position in Time-off — `#3468` | any | mask | H* | ∞ |
| C-AVL-06 | Question-marked position penalty — `#1744`, `#3500`, tip `#18` ("Blue color — allowed, but it is not good") | any | mask | S | 100 |
| C-AVL-07 | Max question marked periods per day — `#3470`, `#2743` | teacher, class, subject | n | S | 200 |
| C-AVL-08 | Max question marked periods per week — `#3469`, `#1020`, `#1883` | subject, class | n | S | 200 |
| C-AVL-09 | Card can not start on selected positions — `#3736` | card | mask | H* | 500 |
| C-AVL-10 | Card can not end on selected positions — `#3737` | card | mask | H* | 500 |
| C-AVL-11 | Cards must be on selected positions — `#4261` | card group | mask | H* | 500 |
| C-AVL-12 | Complementary lesson teacher not available — `#1849`, `#1850` | lesson | — | H | ∞ |

---

### 1.3. O'qituvchi cheklovlari (Teacher / Constraints dialogi `#1206`) — `C-TCH`

| ID | Nomi | Qamrov | Parametrlar | H/S | w |
|---|---|---|---|---|---|
| C-TCH-01 | Max number of windows (gaps) per week — `#1210`, `#1212`, `#3461`; fault `#1731`, `#1819` | teacher | maxGapsWeek | S | 300 |
| C-TCH-02 | Max windows per day — `#2662`, `#3460`; fault `#2028` | teacher | maxGapsDay | S | 300 |
| C-TCH-03 | Max gaps per all weeks — `#3878` | teacher | n | S | 300 |
| C-TCH-04 | Global default max windows for unlimited teachers — `#2526` | global | n | S | 150 |
| C-TCH-05 | Min gap length — `#3479` | teacher | n | S | 100 |
| C-TCH-06 | Max gap length — `#3480` | teacher | n | S | 200 |
| C-TCH-07 | Limit number of days taught — `#1211`, `#1213`, `#1214`, `#3458`; fault `#1734`, `#1820` | teacher | maxDays | S | 400 |
| C-TCH-08 | Min days per week — `#3459` | teacher | minDays | S | 200 |
| C-TCH-09 | Max/Min days per all weeks — `#3910`, `#3911` | teacher | n | S | 200 |
| C-TCH-10 | Max consecutive periods (exhaustion) — `#1217`, `#1218`, `#1219`, `#3472`; fault `#1851` | teacher | maxConsec | S | 400 |
| C-TCH-11 | Global default max consecutive periods — `#1352` | global | n | S | 200 |
| C-TCH-12 | Max 3 consecutive periods, or 2 doubles — `#4189` (maxsus kombinatsion variant) | teacher | — | S | 400 |
| C-TCH-13 | Max consecutive different lessons — `#4190` | teacher | n | S | 150 |
| C-TCH-14 | Max periods per day — `#3454`, `#2771` | teacher | max | S | 300 |
| C-TCH-15 | Min periods per day (empty day is OK — `#3452`) — `#3453`; fault `#2660` | teacher | min, emptyDayOk | S | 300 |
| C-TCH-16 | Max periods per week — `#3455` | teacher | max | S | 300 |
| C-TCH-17 | Min periods per week (empty week is OK — `#3457`) — `#3456` | teacher | min | S | 300 |
| C-TCH-18 | Max periods per all weeks/terms — `#3733` | teacher | n | S | 200 |
| C-TCH-19 | Min/Max periods+gaps per day (ish kuni uzunligi) — `#3463`, `#3464`, `#3266`, `#3267` | teacher | min,max,emptyDayOk | S | 250 |
| C-TCH-20 | Max free days between cards per week — `#3462`; `#3268` | teacher | n | S | 150 |
| C-TCH-21 | Max days per week including free days between cards — `#3465` | teacher | n | S | 150 |
| C-TCH-22 | Max free weeks between cards per year — `#3914` | teacher | n | S | 100 |
| C-TCH-23 | Max consecutive days — `#3471`, `#3285` | teacher | n | S | 150 |
| C-TCH-24 | Teacher must have lesson every day — `#1221` | teacher | — | S | 400 |
| C-TCH-25 | Max number of lessons per day requiring preparation — `#1384`, `#1882` (homework/prep balansi) | teacher, class | n | S | 200 |
| C-TCH-26 | Do not check exhaustion on Sat/Sun — `#2740`, `#2745` | global | flag | — | modifier |
| C-TCH-27 | Max consecutive free lessons (bo'sh darslar ketma-ketligi) — `#2741`, `#2742` | teacher | n | S | 200 |
| C-TCH-28 | Min/Max total minutes per week (kontrakt) — `#3629`, `#3630`, `#3313`, `#1250` | teacher | min,max | S | 250 |
| C-TCH-29 | Max different subjects per day — `#3971` | teacher | n | S | 100 |
| C-TCH-30 | Max different classrooms per day / per week — `#3290`, `#3291` | teacher | n | S | 150 |
| C-TCH-31 | Max different period numbers per week — `#3240` | teacher | n | S | 100 |
| C-TCH-32 | Max days with lesson on the same period — `#3478` | teacher, class | n | S | 100 |
| C-TCH-33 | Class teacher must teach this class in specific time every day — `#2722`; fault `#2723` | teacher+class | positions | S | 400 |
| C-TCH-34 | Class teacher occupied positions — `#3716` | teacher | mask | H* | 400 |

---

### 1.4. Sinf cheklovlari — `C-CLS`

| ID | Nomi | Qamrov | Parametrlar | H/S | w |
|---|---|---|---|---|---|
| C-CLS-01 | Class must not contain a window (gap) — fault `#1821`; `#4137..#4143` | class | allowGaps flag | H* | 800 |
| C-CLS-02 | Max gaps per day / per week for class — `#3460`, `#3461` | class | n | S | 400 |
| C-CLS-03 | Min/Max number of lessons per day — `#2650`, `#2651`, `#3453`, `#3454`; fault `#2308` | class | min,max | S | 400 |
| C-CLS-04 | Class must have lessons in this interval (education block) — `#2714`, `#3004`, `#2716` | class | [from,to] | H* | 600 |
| C-CLS-05 | Class must finish before or on lesson — `#2715`, `#2747` | class | lastPeriod | H* | 600 |
| C-CLS-06 | Class must start with this hour — `#2768` | class | firstPeriod | H* | 600 |
| C-CLS-07 | Allow arrival on second lesson — `#1385`, `#4113` | class | flag | — | modifier |
| C-CLS-08 | Allow the generator to place lessons on 0th period — `#1338`, `#4114`, `#4119` | class/global | flag | — | modifier |
| C-CLS-09 | Card expands teaching block causing collisions — fault `#1848`; `#1823 "out of the teaching block"` | class | — | H | ∞ |
| C-CLS-10 | Max days per week / Min days per week — `#3458`, `#3459`; fault `#3005` | class | n | S | 300 |
| C-CLS-11 | Class exceeded number of days taught — fault `#3005` | class | n | S | 300 |
| C-CLS-12 | Min/Max periods+gaps per day — `#3463`, `#3464` | class | min,max | S | 300 |
| C-CLS-13 | Max different subjects per day — `#3971` | class | n | S | 100 |

---

### 1.5. Tushlik (Lunch) — `C-LUN`

| ID | Nomi | Qamrov | Parametrlar | H/S | w |
|---|---|---|---|---|---|
| C-LUN-01 | Lunch break must be in following interval — `#2641`, `#2642`; fault `#2644 "Missing lunch break"` | class, teacher | [from,to] | H* | 700 |
| C-LUN-02 | Forbid placing other lessons after lunch if lunch is last hour in interval — `#2643` | class | flag | S | 300 |
| C-LUN-03 | Groups must have lunch at the same time — `#2712` | class groups | — | S | 300 |
| C-LUN-04 | Card can be over lunch — `#3545` | card | flag | — | modifier |

---

### 1.6. Taqsimot (Distribution / Spread) — `C-DST`

aSc'ning eng xarakterli soft-constraint oilasi. `#2027`: "Program distributes lessons equably.
E.g. If the lesson is taught twice a week, there has to be at least one-day gap between these two lessons."
`#2312`, `#2313`: 2 marta — ketma-ket kunlarda emas; 3 marta — 3 ketma-ket kunda emas.
Daraja sozlanadi: `#3727 No distribution checking` / `#3728 Low` / `#3729 Medium` / `#3730 Ideal`.

| ID | Nomi | Qamrov | Parametrlar | H/S | w |
|---|---|---|---|---|---|
| C-DST-01 | Equable distribution over the week — `#2027`, `#2306`, `#2312`, `#2313`, `#2719`; fault `#1739 "Inappropriate distribution"`, `#2720` | subject×class | level | S | 500 |
| C-DST-02 | Distribution mode: None/Low/Medium/Ideal — `#3727..#3730` | global | enum | — | modifier |
| C-DST-03 | Check distribution within week — `#3731`, `#1341` | global | flag | — | modifier |
| C-DST-04 | Need not be distributed equably (per-lesson opt-out) — `#1392`, `#2057` | lesson | flag | — | modifier |
| C-DST-05 | Can be only once per day — `#3888`; fault `#1824 "%1 more times per day"` | subject×class | — | H* | 600 |
| C-DST-06 | Can be more times per day — `#3889`, `#3890 "Can be e.g all 5 lessons in one day"` | subject×class | — | — | modifier |
| C-DST-07 | Not on consecutive days — fault `#1825 "%1 on consecutive days"` | subject×class | — | S | 400 |
| C-DST-08 | Manual settings for distribution: distribute cards into N days — `#2775`, `#2776`, `#3007` | subject×class | nDays | H* | 500 |
| C-DST-09 | In case of more lessons per day, these must be placed consecutively — `#2777` | subject×class | flag | S | 400 |
| C-DST-10 | Distribution of each subject separately vs. group of subjects — `#2995`, `#2996` | subject set | mode | — | modifier |
| C-DST-11 | The cards cannot be placed on two or three following days — `#2998` | card group | — | S | 400 |
| C-DST-12 | Not enough space for equable distribution — diagnostika `#2749` | class | — | (test) | — |
| C-DST-13 | Less cards before specified lesson than requested — `#2750` | class | n | S | 300 |
| C-DST-14 | Min/Max number of days that have lesson on marked positions — `#3076`, `#2925`, `#2926` | subject×class | n | S | 250 |
| C-DST-15 | More times per week than days (imkonsizlik ogohlantirishi) — `#4148` | lesson | — | (test) | — |

---

### 1.7. Qo'sh darslar va full/divided munosabati — `C-DBL`

| ID | Nomi | Qamrov | Parametrlar | H/S | w |
|---|---|---|---|---|---|
| C-DBL-01 | Double/triple lesson must occupy consecutive periods — `#1645 Double`, `#2704 Single`, `#3215` | card | length | H | ∞ |
| C-DBL-02 | Double lessons cannot span this break — `#2705`, `#2652`, `#2744` | card | breakPos | H* | 600 |
| C-DBL-03 | Break cannot be between group of lessons — `#2731` | card group | — | H* | 600 |
| C-DBL-04 | Can split one double lesson — `#4044` | lesson | flag | S | 300 |
| C-DBL-05 | Class cannot be completed because of double-lessons — diagnostika `#2658` | class | — | (test) | — |
| C-DBL-06 | Full and divided lessons cannot be the same day — `#1337`, `#1852` | subject×class | flag | S | 400 |
| C-DBL-07 | Divided lessons may not be on both sides of the full lesson — `#1347`; fault `#1740`, `#1829` | subject×class | — | S | 400 |
| C-DBL-08 | Full card found between divided cards — fault `#1829`, `#1830`, tekshiruv bosqichi `#1834` | subject×class | — | S | 400 |
| C-DBL-09 | Divided cards from one subject must be on one day — `#2769`; fault `#2772` | subject×class | — | S | 400 |
| C-DBL-10 | Divided cards placed on many positions in class — fault `#1822` | class | — | S | 300 |
| C-DBL-11 | Specify relationship between full and divided lessons — `#3006` (yuqoridagilarni sozlovchi dialog) | subject | enum | — | modifier |
| C-DBL-12 | Not enough single hours to fill gaps — diagnostika `#3502` | class | — | (test) | — |

---

### 1.8. Card relationships (kartalararo munosabatlar, `#1400`, `#2616`) — `C-REL`

Har bir munosabatning `Importance` (`#3063`) atributi bor: `Low/Normal/High`.
`A` va `B` — ikki karta to'plami (sections).

| ID | Nomi | Qamrov | Parametrlar | H/S | w |
|---|---|---|---|---|---|
| C-REL-01 | Two subjects cannot be placed on the same day — `#1390`, `#2033`, `#3486`; fault `#1867` | subject pair | — | S | importance |
| C-REL-02 | They cannot be placed consecutively on the same day — `#1391`, `#2032`, `#3475`; fault `#1866` | subject pair | — | S | importance |
| C-REL-03 | Cards must follow (in specified order) — `#3473`, `#3474`, `#2724`, `#2725` | card pair | ordered | S | importance |
| C-REL-04 | Cards must follow (arbitrary order) — `#2726` | card pair | — | S | importance |
| C-REL-05 | Two subjects must be in one day — `#2746` | subject pair | — | S | importance |
| C-REL-06 | Group of cards from different classes must be in one day — `#2748`; fault `#2754` | card group | — | S | importance |
| C-REL-07 | Can not be on the same period — `#3485`, `#3277` | A,B | — | S | importance |
| C-REL-08 | Must be on the same positions — `#3484`, `#3096` | A,B | — | H* | importance |
| C-REL-09 | Must be on the same days — `#3483` | A,B | — | H* | importance |
| C-REL-10 | A must be first in a day — `#3476` | A | — | S | importance |
| C-REL-11 | B must be last in a day — `#3477` | B | — | S | importance |
| C-REL-12 | Subject must be first or last — `#3757` | subject | — | S | importance |
| C-REL-13 | A lessons must be before B lessons in a day — `#3873`; `#3759 "A before or after B in a day"` | A,B | — | S | importance |
| C-REL-14 | Lessons A must be before lessons B in a week — `#3874` | A,B | — | S | importance |
| C-REL-15 | Days A must be before days B in a week — `#3875` | A,B | — | S | importance |
| C-REL-16 | There cannot be lesson in A section followed by lesson in B section — `#3278` | A,B | — | S | importance |
| C-REL-17 | No lesson in A on one day AND lesson in B on the next day — `#3087` | A,B | — | S | importance |
| C-REL-18 | Cannot be lesson in A together with lesson in B on the same day — `#2927` | A,B | — | S | importance |
| C-REL-19 | Gaps in A must be filled with B — `#3916` | A,B | — | S | importance |
| C-REL-20 | A lessons can be only on days of B lessons — `#3625` | A,B | — | S | importance |
| C-REL-21 | These subjects for groups of listed classes must start at the same time — `#2831` | subject×classes | — | H* | importance |
| C-REL-22 | The selected subjects have to be at the same time in all selected classes — `#3284` | subject×classes | — | H* | importance |
| C-REL-23 | This subject must be on the same period each day — `#3286`, `#3849`, `#4134`, `#4110` | subject×class | — | S | importance |
| C-REL-24 | Lesson placed at the same period in all selected days — `#3642` | lesson | days[] | H* | importance |
| C-REL-25 | Max number of lessons on the same period per week — `#3070` | class/teacher | n | S | importance |
| C-REL-26 | Can not be in the same term — `#3877` | A,B | — | S | importance |
| C-REL-27 | Grouped constraints (constraint'lar to'plamini bir obyektga bog'lash) — `#3326`, `#3719` | any | set | — | container |
| C-REL-28 | Exceptions allowed (istisnolar soni) — `#3097` | any relation | n | S | modifier |

---

### 1.9. Xona (Classroom) — `C-ROM`

| ID | Nomi | Qamrov | Parametrlar | H/S | w |
|---|---|---|---|---|---|
| C-ROM-01 | Lesson must be in one of its allowed classrooms — `#1418`, `#1780`, `#1914`, `#2309` | lesson | roomSet | H | ∞ |
| C-ROM-02 | Classroom capacity — `#3491`, `#3492`, `#2737`, `#3244`; fault `#3265 "Classroom capacity exceeded"` | classroom | capacity | H* | 700 |
| C-ROM-03 | Use room capacities (global switch) — `#3043` | global | flag | — | modifier |
| C-ROM-04 | Max over room capacity — `#3837` | classroom | n | S | 400 |
| C-ROM-05 | Number of lessons that can be in this classroom at the same time — `#3829` | classroom | n | H | ∞ |
| C-ROM-06 | Max classrooms on one period — `#3868` | global/set | n | S | 200 |
| C-ROM-07 | Max/Min periods per week in selected classrooms — `#3917`, `#3918` | classroom set | n | S | 200 |
| C-ROM-08 | Home classroom preference — `#1067`, `#1094` | class | room | S | 150 |
| C-ROM-09 | Subject's / Teacher's classrooms preference — `#3773`, `#3774` | subject/teacher | roomSet | S | 150 |
| C-ROM-10 | In nearby classroom before OR / AND after — `#3213`, `#3214` | lesson | mode | S | 150 |
| C-ROM-11 | Lesson capacity / Subject capacity — `#3496`, `#4226`, `#3511` | lesson, subject | n | H* | 500 |

---

### 1.10. Binolar (Buildings) — `C-BLD`

| ID | Nomi | Qamrov | Parametrlar | H/S | w |
|---|---|---|---|---|---|
| C-BLD-01 | Max transits between buildings per day — `#2969`; fault `#3009` | teacher | n | S | 400 |
| C-BLD-02 | Max transits between buildings per week — `#3481` | teacher | n | S | 400 |
| C-BLD-03 | Time for transfer between buildings — `#3482`, `#3091` (o'tish uchun kerakli period soni) | global | nPeriods | H | ∞ |
| C-BLD-04 | Class has to be in one building during the whole day — `#3090` | class | — | H* | 600 |
| C-BLD-05 | Max different buildings per day — `#4192` | teacher, class | n | S | 300 |

---

### 1.11. Ko'p haftalik / terms sikli — `C-CYC`

| ID | Nomi | Qamrov | Parametrlar | H/S | w |
|---|---|---|---|---|---|
| C-CYC-01 | Lesson will be placed in one of the selected weeks — `#3639` | lesson | weeks[] | H | ∞ |
| C-CYC-02 | Lesson will be placed in one of the selected terms — `#3638` | lesson | terms[] | H | ∞ |
| C-CYC-03 | Lesson will be placed in one of the selected days — `#3641`, `#3851` | lesson | days[] | H | ∞ |
| C-CYC-04 | Max weeks/terms on one period — `#4191` | lesson | n | S | 200 |
| C-CYC-05 | Max different positions per term — `#4186` | lesson | n | S | 150 |
| C-CYC-06 | Card doesn't have complement in other weeks — fault `#3033` | card | — | H* | 400 |
| C-CYC-07 | Max periods per all weeks/terms — `#3733` | teacher, class | n | S | 200 |

---

### 1.12. O'quvchi / Seminar (elective) modeli — `C-STU`

| ID | Nomi | Qamrov | Parametrlar | H/S | w |
|---|---|---|---|---|---|
| C-STU-01 | Student must have subject — `#3493` | student | subject | H | ∞ |
| C-STU-02 | Student should not have subject — `#3494` | student | subject | S | 300 |
| C-STU-03 | Student should have subject or alternative — `#3495`; `#3935 "Using alternative instead of preferred"` | student | subj, alt | S | 300 |
| C-STU-04 | Min/Max students assigned to a section — `#3466`, `#3467` | seminar section | min,max | H* | 500 |
| C-STU-05 | Max different sections — `#4266` | student | n | S | 200 |
| C-STU-06 | Min/Max seminars — `#4187`, `#4188` | student | n | S | 200 |
| C-STU-07 | Student must have seminar A in a term before seminar B — `#3864`, `#3865` | student | A,B | H* | 400 |
| C-STU-08 | Student must / cannot have these seminars in the same term — `#3866`, `#3867` | student | A,B | H* | 400 |
| C-STU-09 | Students of these seminars must have the same group / teacher — `#2978`, `#3767` | seminar set | — | H* | 400 |
| C-STU-10 | Student is locked into this group — `#3627` | student | group | H | ∞ |
| C-STU-11 | Min periods per week where all classes have the selected lesson — `#4260` | class set | n | S | 200 |
| C-STU-12 | Max cards / Min cards on one period — `#3098`, `#3451`, `#3189` | period | n | S | 200 |
| C-STU-13 | Max teachers on one period — `#3869` | period | n | S | 200 |

---

### 1.13. "Selected positions" bo'yicha maxsus cheklovlar — `C-POS`

(`#3057..#3095` bloki — pozitsiya maskasi ustidan agregat cheklovlar. Bu aSc'ning
eng moslashuvchan constraint oilasi.)

| ID | Nomi | Qamrov | Parametrlar | H/S | w |
|---|---|---|---|---|---|
| C-POS-01 | Min periods with education per day on selected positions (empty day is ok) — `#3057` | teacher, class | mask, n | S | 300 |
| C-POS-02 | Max periods with education per day on selected positions — `#3086` | teacher, class | mask, n | S | 300 |
| C-POS-03 | Min/Max periods with education per week on selected positions — `#3094`, `#3095` | teacher, class | mask, n | S | 300 |
| C-POS-04 | Max gaps per day on selected positions — `#3058` | teacher, class | mask, n | S | 300 |
| C-POS-05 | Max gaps per week on selected positions — `#3059` | teacher, class | mask, n | S | 300 |
| C-POS-06 | Max consecutive periods of education on selected positions — `#3060` | teacher, class | mask, n | S | 300 |

**Apply-to scope'lar** (`#3053`, `#3054`, `#3088`, `#3880`, `#3891`, `#3892`, `#3893`, `#3681`, `#3682`, `#3085`):
selected teachers / classes / subjects / groups / grades / students / globally.
Ya'ni har bir constraint **obyekt to'plamiga** biriktiriladi, alohida obyektga emas.

---

## 2. Hard vs Soft bo'linishi va og'irliklar

### 2.1. Qat'iy hard (hech qachon buzilmaydi)

Bular **domain/propagation darajasida** ushlab qolinadi, penalty'ga umuman kirmaydi:

```
C-GBL-01..C-GBL-10   resurs to'qnashuvlari, locked cards, division mantiqi
C-AVL-01..C-AVL-04   time-off "forbidden" (qizil ✗)
C-AVL-12             komplementar dars o'qituvchisi
C-DBL-01             double lesson uzluksizligi
C-ROM-01, C-ROM-05   ruxsat etilgan xonalar to'plami, xona sig'imi (soni)
C-BLD-03             binolar orasidagi o'tish vaqti
C-CYC-01..C-CYC-03   hafta/term/kun domeni
C-STU-01, C-STU-10
```

### 2.2. Soft (jarima bilan) va relaxation

`#3072 "Allow relaxation"` / `#2732 "Allow automatic relaxation of defined restrictions"` /
`#3972 "Allow relax"` / `#3081 "Test with RELAXATION"` — dastur cheklovlarni **avtomatik yumshata oladi**.
`#3224`: *"The generation was successful, however some constraints had to be relaxed"* →
`#3226 "Relaxed constraints"` ro'yxati foydalanuvchiga ko'rsatiladi. `#3073 "Strict"` — yumshatishni taqiqlash.

Bu **soft-constraint arxitekturasi**ning to'g'ridan-to'g'ri dalili: har bir constraint'da
`{ Importance: Low|Normal|High|Strict, AllowRelaxation: bool }` juftligi bor.

### 2.3. Og'irliklar iyerarxiyasi (tavsiya etilgan boshlang'ich qiymatlar)

| Daraja | w | Kimga |
|---|---|---|
| Strict / Hard | `∞` (`long.MaxValue/4`) | 2.1 bo'limi |
| Critical | 800 | C-CLS-01 (o'quvchi oynasi) |
| Very high | 700 | C-LUN-01, C-ROM-02 |
| High | 500–600 | C-DST-01, C-CLS-04..06, C-DBL-02/03, C-AVL-09..11 |
| Medium | 300–400 | C-TCH-01/02/07/10/14..17, C-DST-07/09, C-BLD-01/02 |
| Low | 100–200 | C-AVL-06, C-TCH-13/29/30/31, C-ROM-08/09 |
| Cosmetic | 10–50 | ixtiyoriy afzalliklar |

**Muhim printsip (aSc'dan):** `#2626` — *"Warning: This criterion complicates to generate the timetable"* —
UI foydalanuvchiga qaysi constraint qiyinlashtirishini aytadi. Bizda ham har bir constraint uchun
**tightness metrikasi** hisoblanishi kerak (3.6-bo'lim).

---

## 3. aSc generatsiya algoritmining rekonstruksiyasi

### 3.1. Topilgan dalillar (string'lar)

| String | ID | Nimani oshkor qiladi |
|---|---|---|
| `Complexity of generation` / `Small, Normal, Large, Huge` | 1336, 1348–1351 | 4 pog'onali qidiruv byudjeti (iteratsiya/restart soni) |
| `The complexity set the method of the generating` | 2527 | Complexity **algoritmni** o'zgartiradi, faqat vaqtni emas |
| `Generate different timetables` — *"A different timetable is generated every time. We recommend enabling this option"* | 1340, 1346 | **Randomized** algoritm + seed. O'chirilsa — deterministik |
| `Rating:` / `Collisions:` / `Order:` / `Conditions broken:` | 1811, 1812, 1814, 2760 | Real vaqtli **fitness/penalty** ko'rsatkichlari |
| `Cards left:` / `Remaining cards:` / `Pending` | 2770, 1785, 1746 | **Constructive** (karta-karta joylash) yadro |
| Progress bar + graph + statistics | 2296–2298 | Iterativ optimizatsiya, monoton bo'lmagan progress |
| `Do you wish to cancel generating or finish solving quickly?` | 1487 | **Anytime algorithm** + cancellation |
| `Improve` (Generate new / Improve) | 1612, 1613 | Mavjud yechimni **local search** bilan yaxshilash rejimi |
| `Optimize` / `Optional/Optimize` | 2976, 3309 | Post-optimization fazasi |
| `Heuristic` | 3635 | Evristik tanlov |
| `Allow relaxation` / `Relaxed constraints` | 3072, 3226 | **Constraint relaxation** mexanizmi |
| `Enable multiprocessor generator (dual-core...)` | 3164 | **Parallel restarts / portfolio** |
| `Allow network generators to help with this timetable` | 3077 | Taqsimlangan portfolio (bir nechta mashina) |
| `Draft generation` / `Draft` | 3718, 3744 | Tez, sifatsiz dastlabki yechim |
| `Analyze by generation` — *"generate for exactly one minute, then show which cards were causing the most problems"* | 3981, 3982 | Har karta uchun **konflikt hisoblagichi** (weight/aging — tabu/breakout dalili) |
| `The hardest cards are in red, the easiest in gray` | 3980 | Karta qiyinligi metrikasi = MRV/degree |
| `Hardest teacher:` / `Hardest class:` | 2758, 2759 | Obyekt darajasidagi qiyinlik reytingi |
| `Analyze by Extended tests` — *"find the smallest part of your timetable that still cannot be generated"* | 3984, 3985 | **MUS/IIS izlash** (minimal unsatisfiable subset) |
| `Verify specification` / `General verification` / `Detailed verification` | 1127, 1802, 1281 | Pre-processing tekshiruvi (yechim mavjudligini baholash) |
| `Class %1 alone can be generated` / `Teacher %1 alone can be generated` | 1786, 1787, 1798, 2779, 3963 | **Subproblem decomposition test** |
| `%1 can be generated with conditions ignored` / `Can be generated by ignoring the condition:` | 1832, 1833 | Constraint-by-constraint relaxation diagnostikasi |
| `checking the distribution` / `assigning classrooms` / `checking the number of windows of teachers` / `checking the number of permitted days` / `checking for full cards between divided cards` | 1834–1838 | **Fazalar ketma-ketligi** — ayniqsa *xona tayinlash alohida faza* |
| `Assigning classrooms` (alohida progress) | 1855 | Xona tayinlash — **ajratilgan** matching bosqichi |
| `Generation of the timetable without classrooms... the software will happily put 6 PE lessons at one period although you only have one Gym` | 3608 | Xonalar **birga** hal qilinadi, keyin emas (lekin alohida faza sifatida) |
| tip `#28`: *place the card → Info… → you will see the problems with given card* | tips | Har pozitsiya uchun **conflict explanation** saqlanadi |
| tip `#2051`: *"place the card in the conflicting position and press space bar to open the verification detail to see why the algorithm refused this position"* | 2051 | Position-level **feasibility oracle** mavjud |
| `#2062`: *"it starts from the 1st lesson (or 0. lesson...)... The algorithm needs not to have enough cards to generate the first lessons"* | 2062 | Joylash tartibi — **period bo'yicha oldinga**, chapdan o'ngga |

### 3.2. Xulosa: aSc pipeline'i

```
┌─ FAZA 0: VERIFY SPECIFICATION (#1127, #1802) ──────────────────────┐
│  • Har sinf/o'qituvchi/fan/xona uchun alohida-alohida yechim izlash │
│    (#1786, #1787, #1798, #2779) — decomposition test               │
│  • Cheklovlarni birma-bir o'chirib ko'rish (#1832, #1833)          │
│  • Arifmetik tekshiruvlar: darslar soni vs. bo'sh pozitsiyalar     │
│    (#4013, #4018, #4050), "more times per week than days" (#4148)  │
│  → Muvaffaqiyatsiz bo'lsa generatsiya boshlanmaydi (#3074)         │
└────────────────────────────────────────────────────────────────────┘
                              ↓
┌─ FAZA 1: PREPROCESSING / DOMAIN REDUCTION ────────────────────────┐
│  • Lessons → Cards (dars soni × uzunlik bo'yicha kartalarga bo'lish)│
│  • Har karta uchun domain = {(week, day, period)} bitset           │
│  • Time-off masks (teacher ∩ class ∩ subject ∩ classroom) kesishmasi│
│  • Locked cards → domain singleton                                 │
│  • Statik constraint propagation (AC-3 uslubi)                     │
└────────────────────────────────────────────────────────────────────┘
                              ↓
┌─ FAZA 2: CONSTRUCTIVE PLACEMENT (randomized backtracking) ────────┐
│  • Kartalarni qiyinlik bo'yicha tartiblash: MRV + degree           │
│    ("hardest cards in red" #3980)                                  │
│  • Period bo'yicha oldinga (#2062), lekin karta tanlash — dinamik   │
│  • Har joylashda forward-checking; domain bo'shasa → backtrack      │
│  • Backtrack chuqurligi cheklangan → **restart** (randomized)      │
│  • Xona tayinlash: bipartite matching alohida (#1855, #1836)       │
└────────────────────────────────────────────────────────────────────┘
                              ↓  (agar to'liq joylanmasa)
┌─ FAZA 3: EJECTION CHAIN / MIN-CONFLICTS REPAIR ───────────────────┐
│  • Joylanmagan kartani "zo'rlab" qo'yish → to'qnashgan kartalarni  │
│    olib tashlash → ularni qayta joylashga urinish (kick-out chain) │
│  • Har karta uchun konflikt-hisoblagichi oshiriladi (#3982)        │
│    → breakout/tabu mexanizmi, siklga tushib qolmaslik uchun        │
│  • `Cards left:` (#2770) monoton kamayadi/o'sadi — anytime         │
└────────────────────────────────────────────────────────────────────┘
                              ↓
┌─ FAZA 4: SOFT-CONSTRAINT OPTIMIZATION (#1613 "Improve", #2976) ───┐
│  • Feasible yechim topilgach: penalty'ni kamaytirish               │
│  • Neighborhood: single move, swap, block swap, Kempe chain        │
│  • Simulated annealing / tabu; `Rating:` (#1811) real vaqtda       │
│  • Complexity (#1336) → iteratsiya byudjeti va restart soni        │
└────────────────────────────────────────────────────────────────────┘
                              ↓  (agar hali ham to'liq emas)
┌─ FAZA 5: RELAXATION (#3072, #2732, #3224) ────────────────────────┐
│  • Eng "arzon" soft-constraint'larni vaqtincha o'chirish           │
│    (importance bo'yicha o'sish tartibida)                          │
│  • Yechim topilgach — buzilganlar ro'yxati ko'rsatiladi (#3226)    │
└────────────────────────────────────────────────────────────────────┘
```

**Parallellik:** `#3164` — ko'p yadroli generator. Bu deyarli aniq **portfolio approach**:
turli seed'lar bilan bir necha mustaqil urinish, eng yaxshisi olinadi.
`#3077` — tarmoq orqali ham shu portfolio kengaytiriladi.

### 3.3. Complexity pog'onalari (taxminiy tarjima)

| aSc | Restart soni | Faza 2 backtrack limiti | Faza 3/4 iteratsiya | Taxminiy vaqt (40 sinf) |
|---|---|---|---|---|
| Small (`#1348`) | 1–2 | 10k | 50k | 5–20 s |
| Normal (`#1349`) | 4–8 | 100k | 500k | 30–120 s |
| Large (`#1350`) | 16–32 | 1M | 5M | 3–15 min |
| Huge (`#1351`) | 64+ | 10M | 50M | 30 min – soatlar |

`#2766` bandi 3: *"you can raise the complexity to large or even huge. However this can extend the
generation time... this is only for tested schedules."*

---

## 4. Yangi C# implementatsiya spetsifikatsiyasi

### 4.1. Asosiy ma'lumot tuzilmalari

```csharp
// ---- Vaqt panjarasi -------------------------------------------------
// Global slot indeksi: slot = ((week * DayCount) + day) * PeriodCount + period
// Butun jadval uchun bitta tekis indeks fazosi — bitmask'lar shu ustida ishlaydi.
public sealed class TimeGrid
{
    public int Weeks { get; }        // ko'p haftalik sikl (C-CYC)
    public int Days { get; }         // 5..10
    public int Periods { get; }      // 0-chi darsni qo'shib, masalan 0..11
    public int SlotCount => Weeks * Days * Periods;
    public int SlotOf(int w, int d, int p) => ((w * Days) + d) * Periods + p;
}

// ---- Bitset (domain va busy maskalar uchun) -------------------------
// SlotCount odatda 1*5*12=60 .. 4*7*14=392 → 1..7 ta ulong. Stack'da saqlanadi.
public struct SlotMask   // fixed-size, 8 x ulong = 512 slot gacha
{
    private ulong _w0,_w1,_w2,_w3,_w4,_w5,_w6,_w7;
    public bool Test(int i);
    public void Set(int i);
    public void Clear(int i);
    public void And(in SlotMask o);
    public void AndNot(in SlotMask o);
    public int PopCount();                 // MRV uchun — domain hajmi
    public int FirstSet(int from);         // BitOperations.TrailingZeroCount
}
```

**Nega bitmask:** to'qnashuv tekshiruvi `O(1)` — bitta `&` amali.
40 sinf × 30 dars ≈ 1200 karta, 60 slot → butun `teacherBusy` matritsasi
`80 o'qituvchi × 8 ulong = 5 KB` — L1 cache'ga sig'adi.

```csharp
// ---- Resurs bandlik jadvallari -------------------------------------
public sealed class Occupancy
{
    public SlotMask[] TeacherBusy;    // [teacherId]
    public SlotMask[] ClassBusy;      // [classId]  — full class
    public SlotMask[] GroupBusy;      // [groupId]  — division guruhlari
    public SlotMask[] RoomBusy;       // [roomId]
    public byte[][]   RoomLoad;       // [roomId][slot] — sig'im > 1 bo'lgan xonalar (C-ROM-05)
    public byte[][]   StudentBusy;    // [studentId][slot] — seminar modeli
}

// ---- Karta (atomik joylashtiriladigan birlik) -----------------------
public sealed class Card
{
    public int Id;
    public int LessonId;
    public int Length;            // 1 = single, 2 = double, 3 = triple (C-DBL-01)
    public int[] TeacherIds;      // bir darsda bir necha o'qituvchi bo'lishi mumkin
    public int ClassId;
    public int[] GroupIds;        // bo'linish guruhlari
    public int[] AllowedRooms;    // C-ROM-01
    public int SubjectId;
    public int StudentCount;      // C-ROM-02
    public SlotMask Domain;       // preprocessing'dan keyingi ruxsat etilgan slotlar
    public SlotMask QuestionMarked; // C-AVL-06 — ruxsat, lekin jarimali
    public bool IsLocked;
    public int  PlacedSlot = -1;  // -1 = joylanmagan
    public int  PlacedRoom = -1;
    // statistika (evristikalar uchun)
    public int  ConflictCount;    // #3982 "hardest cards" — aging counter
    public double Difficulty;     // MRV+degree kompozit bahosi
}
```

```csharp
// ---- Cheklov modeli --------------------------------------------------
public enum Importance { Low = 10, Normal = 100, High = 500, Strict = int.MaxValue }

public interface IConstraint
{
    string  Id { get; }             // "C-TCH-01"
    Importance Importance { get; }
    bool    AllowRelaxation { get; } // #3072
    bool    IsHard => Importance == Importance.Strict;

    /// <summary>Faza 2/3: shu kartani shu slotga qo'yish MUMKINMI (hard tekshiruv).</summary>
    bool IsFeasible(in Solution s, Card c, int slot, int room);

    /// <summary>Faza 4: joriy yechimning shu constraint bo'yicha jarimasi.</summary>
    long Penalty(in Solution s);

    /// <summary>Inkremental delta — move'ni baholash uchun (O(1)..O(D)).</summary>
    long DeltaPenalty(in Solution s, in Move m);

    /// <summary>Faza 1: domain'ni oldindan qisqartirish.</summary>
    void Propagate(Solution s, Card c, ref SlotMask domain);
}
```

**Muhim:** `DeltaPenalty` — butun `Penalty` ni qayta hisoblamaydi. Masalan
`C-TCH-01` (max gaps) uchun faqat ta'sirlangan `(teacher, week, day)` qatorlari qayta hisoblanadi.
Bu local search tezligini `O(n)` dan `O(1)` ga tushiradi — kritik.

```csharp
// ---- Kunlik agregat keshlar (delta hisoblash uchun) ------------------
// Har (resurs, hafta, kun) uchun oldindan hisoblangan qiymatlar.
public struct DayStats
{
    public byte FirstPeriod, LastPeriod;  // 255 = bo'sh kun
    public byte Count;                    // darslar soni
    public byte Gaps;                     // oynalar
    public byte MaxConsecutive;
    public byte DistinctRooms, DistinctBuildings, DistinctSubjects;
}
// teacherDay[teacherId, week, day], classDay[classId, week, day]
```

### 4.2. Faza 0 — Verify specification

```
VerifySpecification():
  faults = []
  // Arifmetik (aSc #4013/#4018/#4050)
  foreach teacher t:  if lessonCount(t) > freeSlots(t.TimeOff): faults += Overloaded(t)
  foreach class c:    if lessonCount(c) > freeSlots(c.TimeOff): faults += Overloaded(c)
  foreach room r:     if demand(r)      > capacity(r):          faults += RoomShortage(r)
  // Har fan uchun: haftalik dars soni > kunlar soni bo'lsa va "once per day" yoqilgan (#4148)
  foreach (subj, cls): if perWeek > days && C-DST-05 active: faults += TooFrequent
  // Dekompozitsiya testi (#1786, #1787, #1798, #2779)
  foreach class c:    if !SolveSubproblem(cards(c), budget=Small): faults += ClassInfeasible(c)
  foreach teacher t:  if !SolveSubproblem(cards(t), budget=Small): faults += TeacherInfeasible(t)
  // Hall's theorem: har karta guruhi uchun domain birlashmasi >= guruh hajmi
  foreach resource R: if |⋃ domain(c) for c in cards(R)| < |cards(R)|: faults += HallViolation(R)
  return faults
```

Agar `faults` bo'sh bo'lmasa — generatsiya boshlanmaydi (aSc `#3074`).
Qo'shimcha: `AnalyzeByExtendedTests` (`#3985`) — **QuickXplain** algoritmi bilan
minimal unsatisfiable subset (MUS) izlash. Bu ixtiyoriy, lekin diagnostikada juda qimmatli.

### 4.3. Faza 1 — Preprocessing va domain reduction (AC-3)

```
BuildDomains():
  foreach card c:
      d = FullMask
      foreach t in c.TeacherIds: d &= t.AvailableMask
      d &= c.Class.AvailableMask
      d &= c.Subject.AvailableMask
      d &= Union(room.AvailableMask for room in c.AllowedRooms)
      d &= WeekTermDayMask(c)                       // C-CYC-01..03
      if c.Length > 1: d &= ConsecutiveFitMask(c.Length)  // C-DBL-01, C-DBL-02
      d &= EducationBlockMask(c.Class)              // C-CLS-04..06
      foreach constraint k applying to c: k.Propagate(sol, c, ref d)
      c.Domain = d
      if d.PopCount() == 0: throw Infeasible(c)

Propagate():                                        // AC-3 uslubi
  queue = all constraint-card arcs
  while queue not empty:
      (k, c) = queue.Dequeue()
      old = c.Domain
      k.Revise(ref c.Domain)
      if c.Domain != old:
          if c.Domain.PopCount() == 0: return INFEASIBLE
          if c.Domain.PopCount() == 1: AssignAndCascade(c)     // singleton propagation
          queue += arcs(neighbours(c))
```

Amaliy foyda: 40 sinflik maktabda domain'lar odatda **60–75% ga qisqaradi**,
bu Faza 2 ni bir necha barobar tezlashtiradi.

### 4.4. Faza 2 — Constructive placement (MRV + degree + randomized restarts)

```
Construct(rng, budget):
  unplaced = SortByDifficulty(allUnlockedCards)
  backtracks = 0
  while unplaced not empty:
      c = SelectCard(unplaced)              // MRV: min |Domain \ occupied|;
                                            // tie-break: max degree; tie: max ConflictCount
      candidates = FeasibleSlots(c)         // Domain & ~busy(teacher|class|group)
      if candidates empty:
          backtracks++
          if backtracks > budget.MaxBacktracks: return PARTIAL
          UndoLastK(rng.Next(1, 8))         // randomized partial backtrack
          continue
      // Slot tanlash: eng kam jarima + eng kam "boshqalarni cheklash" (LCV)
      slot = ArgMin(candidates, s => SoftCost(c, s) + Lambda * DomainDamage(c, s)
                                     + rng.NextDouble() * Noise)
      room = AssignRoom(c, slot)            // 4.5-bo'lim
      if room < 0: MarkSlotInfeasible(c, slot); continue
      Place(c, slot, room)
      unplaced.Remove(c)
  return COMPLETE

Difficulty(c) = w1 * (1 / c.Domain.PopCount())
              + w2 * degree(c)              // nechta constraint'ga kirgan
              + w3 * c.Length               // double lesson qiyinroq
              + w4 * |c.AllowedRooms|^-1
              + w5 * c.ConflictCount        // restart'lar orasida saqlanadi (aging)
```

`DomainDamage(c, s)` — LCV (least-constraining value): shu joylash boshqa kartalarning
domain'idan nechta slot olib tashlaydi. `PopCount` farqlari yig'indisi.

### 4.5. Xona tayinlash — alohida matching bosqichi

aSc buni alohida faza sifatida ko'rsatadi (`#1836`, `#1855`). Ikki rejim:

- **Inkremental (Faza 2 ichida):** karta joylashda darhol eng kam bandlikdagi ruxsat etilgan xona.
- **Global repair (har slot uchun):** bitta slotdagi barcha kartalar × ruxsat etilgan xonalar →
  **bipartite maximum matching** (Hopcroft–Karp, `O(E√V)`). Slotda 40 karta, 60 xona →
  mikrosekundlar. Agar to'liq matching yo'q bo'lsa — slot infeasible, karta boshqa joyga.

```
AssignRoomsForSlot(slot):
  build bipartite graph: cards(slot) ↔ rooms with capacity >= card.StudentCount
  m = HopcroftKarp(graph)
  if m.Size < cards(slot).Count: return false     // C-ROM-01 buzildi
  apply m
  return true
```

Sig'imi > 1 bo'lgan xonalar (`#3829`) uchun — **b-matching** (xona `k` marta ishlatilishi mumkin).

### 4.6. Faza 3 — Ejection chain (aSc'ning "kick out and reinsert" mexanizmi)

Bu aSc'ning eng muhim g'oyasi: joylanmagan kartani majburan qo'yib,
to'qnashganlarni chiqarib tashlash va zanjir bo'ylab qayta joylash.

```
EjectionChain(c, maxDepth, rng):
  best = null
  foreach slot in ShuffledFeasibleByHard(c, rng):
      victims = CardsConflictingAt(c, slot)      // teacher/class/group/room to'qnashuvlari
      if victims.Count > MaxVictims: continue    // odatda 1..3
      Save(); Place(c, slot); Remove(victims)
      ok = true
      foreach v in victims:
          if !TryReinsert(v, depth+1, maxDepth):    // rekursiv zanjir
              ok = false; break
      if ok: return true
      Restore()
      foreach v in victims: v.ConflictCount++     // #3982 aging — qayta tanlanmasin
  return false

TryReinsert(v, depth, maxDepth):
  if depth >= maxDepth: return false
  slots = FeasibleSlots(v)                        // bo'sh joy bormi
  if slots not empty: Place(v, Best(slots)); return true
  return EjectionChain(v, maxDepth, rng)          // yana zanjir
```

`maxDepth`: Small=2, Normal=4, Large=6, Huge=10.
`ConflictCount` — **breakout/aging** mexanizmi: siklga tushib qolmaslik uchun
"muammoli" kartalar og'irligi oshadi va ular oldinroq joylashtiriladi (keyingi restart'da).

### 4.7. Faza 4 — Soft optimization (SA + tabu gibrid)

```
Optimize(sol, budget, rng, ct):
  T  = T0                                        // simulated annealing harorati
  tabu = new TabuList(tenure: 8 + rng.Next(0, 12))
  best = sol.Clone(); bestCost = Evaluate(sol)
  cur  = bestCost
  for it = 0 .. budget.MaxIterations:
      ct.ThrowIfCancellationRequested()
      m = PickMove(sol, rng)                     // quyidagi neighborhood'dan
      if tabu.Contains(m) && !AspirationOk(m): continue
      d = DeltaPenalty(sol, m)                   // O(1)..O(Days) — inkremental
      if d <= 0 || rng.NextDouble() < Math.Exp(-d / T):
          Apply(sol, m); cur += d; tabu.Add(m.Inverse())
          if cur < bestCost: bestCost = cur; best = sol.Clone()
      T *= alpha                                 // geometrik sovutish, alpha ≈ 0.99995
      if it % ReheatPeriod == 0 && NoImprovement(): T = T0 * 0.5   // reheat
      if it % ProgressPeriod == 0: progress.Report(new(it, cur, bestCost, sol.Unplaced))
  return best
```

**Neighborhood (move turlari va tanlanish ehtimoli):**

| Move | Tavsif | p |
|---|---|---|
| `SingleMove` | bitta kartani bo'sh feasible slotga ko'chirish | 0.35 |
| `Swap` | ikki kartaning slotlarini almashtirish (bir xil resurs bilan bog'liq) | 0.30 |
| `RoomChange` | faqat xonani almashtirish (C-ROM-*, C-BLD-* uchun) | 0.10 |
| `BlockSwap` | bir sinfning ikki kunini butunlay almashtirish | 0.10 |
| `KempeChain` | ikki slot orasidagi to'qnashuv grafi komponentini almashtirish | 0.10 |
| `EjectionMove` | 4.6 zanjiri, lekin optimizatsiya rejimida | 0.05 |

**Kempe chain** — sinf jadvallarida ayniqsa samarali: `p1` va `p2` slotlaridagi kartalar
o'rtasida to'qnashuv grafi quriladi, bog'liq komponent butunlay almashtiriladi.
Bu hard constraint'larni saqlagan holda katta sakrash beradi.

**Guided local search (ixtiyoriy, lekin tavsiya etiladi):** har `N` iteratsiyada
eng ko'p buzilayotgan constraint'ning og'irligini vaqtincha oshirish — local optimum'dan chiqish.
Bu aSc'ning `#3982` "hardest cards" mexanizmiga to'g'ridan-to'g'ri mos keladi.

### 4.8. Faza 5 — Relaxation

```
RelaxAndRetry(sol, ct):
  relaxable = constraints.Where(k => k.AllowRelaxation)
                         .OrderBy(k => k.Importance)         // eng arzonidan
  relaxed = []
  foreach k in relaxable:
      k.Enabled = false; relaxed += k
      RebuildDomains()
      r = Construct(...) then EjectionChain(...)
      if r == COMPLETE:
          // orqaga qaytish: buzilmagan constraint'larni qayta yoqib ko'rish
          foreach k2 in relaxed.Reverse():
              k2.Enabled = true
              if !StillFeasible(sol): k2.Enabled = false else relaxed.Remove(k2)
          return (sol, relaxed)        // #3226 "Relaxed constraints" ro'yxati
  return FAILED
```

### 4.9. Portfolio / parallellik (`#3164`)

```csharp
public async Task<Solution> GenerateAsync(Problem p, GenOptions o, CancellationToken ct)
{
    int workers = o.Parallelism ?? Environment.ProcessorCount;
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    var tasks = Enumerable.Range(0, workers).Select(i => Task.Run(() =>
    {
        // Har worker — mustaqil seed. Deterministiklik: seed = o.Seed * 1000003 + i
        var rng = new Xoshiro256ss(unchecked(o.Seed * 1000003UL + (ulong)i));
        return RunPipeline(p.Clone(), o, rng, cts.Token);
    }, cts.Token)).ToArray();

    // Birinchi COMPLETE + soft-cost chegarasidan past yechim topilsa — hammasini to'xtatish.
    // Aks holda barchasini kutib, eng yaxshisini olish (anytime, #1487).
    var results = await Task.WhenAll(tasks);
    return results.Where(r => r != null).MinBy(r => (r.UnplacedCount, r.SoftCost));
}
```

**Determinizm:** bitta worker (`Parallelism = 1`) + qat'iy seed → bir xil natija.
Ko'p worker'da natija non-deterministik bo'ladi (kim birinchi tugatgani), shuning uchun
`GenOptions.Deterministic = true` bo'lsa — barcha worker'lar tugaydi va
natija `(unplaced, softCost, workerIndex)` bo'yicha barqaror tartiblanadi.

### 4.10. Progress va cancellation

```csharp
public readonly record struct GenProgress(
    GenPhase Phase,        // Verify, Preprocess, Construct, Repair, Optimize, Relax
    int      Iteration,
    int      PlacedCards,
    int      TotalCards,
    long     SoftCost,     // #1811 "Rating:"
    long     BestSoftCost,
    int      HardViolations,   // #1812 "Collisions:"
    int      RelaxedCount,     // #2760 "Conditions broken:"
    TimeSpan Elapsed);

// IProgress<GenProgress> — har ~100 ms da (iteratsiyada emas!) report qilinadi.
// CancellationToken: "cancel or finish quickly" (#1487) —
//   ct.Cancel()      → darhol joriy eng yaxshi yechimni qaytarish (anytime)
//   softStop = true  → joriy fazani tugatib chiqish
```

---

## 5. Baholash funksiyasi (evaluation / fitness)

### 5.1. Umumiy formula

Leksikografik ikki darajali maqsad — hard buzilishlar har doim ustun:

```
Cost(S) = H(S) * BigM  +  Σ_k  w_k * Σ_o  v_k(S, o)
```

- `H(S)` — hard constraint buzilishlari soni (Faza 2/3 da 0 bo'lishi kerak)
- `BigM` = 10^9 (soft jarimalarning maksimal yig'indisidan katta)
- `w_k` — `k` constraint'ning og'irligi (2.3-bo'lim jadvali)
- `o` — constraint qo'llanadigan obyekt (o'qituvchi, sinf, ...)
- `v_k(S, o)` — **buzilish kattaligi**, boolean emas

### 5.2. `v_k` funksiyalari (asosiylari)

```
// C-TCH-01/02 — oynalar
v_gaps(t, w, d)    = max(0, gaps(t,w,d) - maxGapsDay)
v_gapsWeek(t, w)   = max(0, Σ_d gaps(t,w,d) - maxGapsWeek)

// C-TCH-10 — ketma-ket darslar (kvadratik — chunki 2 ortiqcha 1 dan 4x yomon)
v_consec(t, w, d)  = Σ_run max(0, len(run) - maxConsec)^2

// C-CLS-03 / C-TCH-14..17 — kunlik yuk
v_load(o, w, d)    = max(0, count - max) + max(0, min - count)     // emptyDayOk bo'lsa count>0 sharti

// C-DST-01 — haftalik taqsimot. Ideal: darslar kunlar bo'yicha tekis, orasida bo'shliq.
// Kunlar orasidagi masofalarni ideal masofadan chetlanishi bilan jarimalash:
v_spread(subj, cls) = Σ_i (idealGap - actualGap_i)^2   where idealGap = Days / lessonsPerWeek
                    + PenaltySameDay * (extra lessons on same day)
                    + PenaltyAdjacent * (pairs on consecutive days)   // #2312, #2313

// C-AVL-06 — question-marked pozitsiyalar
v_qmark(c)         = 1 if c.PlacedSlot ∈ c.QuestionMarked else 0
v_qmarkDay(o,w,d)  = max(0, qmarkCount - maxQmarkPerDay)            // C-AVL-07

// C-BLD-01 — binolar orasidagi o'tishlar
v_transit(t, w, d) = max(0, transits(t,w,d) - maxTransitsDay)

// C-ROM-02 — sig'im
v_capacity(c)      = max(0, c.StudentCount - room.Capacity)         // odam soni bilan proporsional

// C-LUN-01 — tushlik
v_lunch(o, w, d)   = 1 if no free period in [lunchFrom, lunchTo] and day is not empty

// C-REL-* — munosabatlar: odatda boolean (buzildi/buzilmadi), w = Importance
v_rel              = 1 if violated else 0
```

**Kvadratik jarima printsipi:** `C-TCH-10` (ketma-ketlik) va `C-DST-01` (taqsimot) uchun
kvadrat ishlatiladi, chunki **bitta o'qituvchida 4 ta ortiqcha** — **to'rt o'qituvchida bittadan**
ortiqchadan yomonroq. Bu adolatlilikni (fairness) ta'minlaydi.
Qolgan constraint'lar uchun chiziqli.

### 5.3. Inkremental delta hisoblash

```csharp
long DeltaPenalty(Solution s, in Move m)
{
    long d = 0;
    // Faqat ta'sirlangan (resurs, hafta, kun) juftliklari qayta hisoblanadi.
    // SingleMove: 2 kun × (1 sinf + N o'qituvchi + M guruh + 1 xona)
    // Swap:       4 kun × ...
    foreach (var (res, w, day) in m.AffectedDays())
    {
        d -= _cache.DayCost(res, w, day);          // eski qiymat
        d += RecomputeDayCost(s, res, w, day, m);  // yangi qiymat
    }
    // Haftalik va global constraint'lar uchun alohida delta
    d += DeltaWeeklyConstraints(s, m);
    d += DeltaRelations(s, m);                     // faqat m ga tegishli C-REL-*
    return d;
}
```

Bu `Evaluate` ni har move'da to'liq chaqirmaslikni ta'minlaydi:
to'liq baholash `O(cards + constraints)` ≈ 5–20 µs, delta ≈ **50–200 ns**.
Farq **100x** — local search tezligini shu belgilaydi.

### 5.4. Kutilayotgan ishlash tezligi

Mos yozuvlar: **40 sinf × 30 dars/hafta ≈ 1200 karta, 80 o'qituvchi, 60 xona, 5 kun × 12 period = 60 slot.**

| Bosqich | Murakkablik | Kutilayotgan vaqt (1 yadro, .NET 8, modern CPU) |
|---|---|---|
| Faza 0 Verify | `O(C · S)` + subproblem testlar | 0.5–3 s |
| Faza 1 AC-3 | `O(A · d)` , A ≈ arclar soni | 50–300 ms |
| Faza 2 Construct | `O(N · S · B)` , B = backtrack limiti | 0.2–5 s |
| Faza 3 Ejection | `O(U · S · V^depth)` | 0.5–20 s (U = joylanmaganlar) |
| Faza 4 Optimize | `O(I)` , delta `O(1)` | 10^6–10^7 iter/min |
| **Jami (Normal)** | | **20–90 s**, 8 yadroda **5–20 s** |
| **Jami (Large)** | | 3–10 min |

Bitmask to'qnashuv tekshiruvi: **~2–5 ns** (bitta `ulong &` + `TrailingZeroCount`).
Bitta move baholash: **~150 ns**. Demak **~6–7 mln move/sekund** bitta yadroda.

**Xotira:** 1200 karta × (8 ulong domain + metadata) ≈ 200 KB;
occupancy jadvallari ≈ 50 KB; day-stats kesh ≈ 100 KB. **Jami < 1 MB** — to'liq L2 cache'da.

**Optimizatsiya eslatmalari:**
- `SlotMask` — `struct`, `[InlineArray]` yoki `fixed ulong[8]`; boxing bo'lmasin.
- `Solution.Clone()` — faqat `best` yaxshilanganda (kamdan-kam), `Span<T>.CopyTo` bilan.
- `System.Numerics.BitOperations.PopCount / TrailingZeroCount` — hardware intrinsics.
- Move obyektlari — `readonly record struct`, heap allocation nolga tushirilsin.
- RNG — `Xoshiro256**` (`System.Random.Shared` emas: seed nazorati kerak).
- `ArrayPool<T>` — undo stack uchun.

---

## 6. Test stsenariylari

### 6.1. Birlik testlari — har bir constraint uchun

Har `C-XXX-NN` uchun **3 ta test**: (a) qoniqtiruvchi holat → `Penalty == 0`,
(b) buzilgan holat → `Penalty == kutilgan qiymat`, (c) `DeltaPenalty` == `Evaluate(after) - Evaluate(before)`.

Oxirgisi eng muhim: **delta va to'liq baholash mos kelishi** invariantini
har 10 000 iteratsiyada debug rejimida assert qilish kerak. Bu eng ko'p uchraydigan xatolik manbai.

### 6.2. Hard constraint feasibility testlari

| # | Stsenariy | Kutilgan natija |
|---|---|---|
| T-H-01 | Bitta o'qituvchi, 2 sinf, bir vaqtda 2 dars | Hech qachon joylanmaydi (C-GBL-01) |
| T-H-02 | Bitta xona, 3 ta PE darsi, 1 ta sport zali | Uchtasi 3 xil slotda (C-ROM-01) |
| T-H-03 | Locked card noto'g'ri joyda | Verify FAULT qaytaradi (C-GBL-06) |
| T-H-04 | Double lesson, oxirgi period'dan boshlanishga urinish | Domain'dan chiqarilgan (C-DBL-01) |
| T-H-05 | Double lesson "long break" ustidan | `SpanBreak=false` → taqiqlangan (C-DBL-02) |
| T-H-06 | Ikki division guruhi bir slotda | Taqiqlangan (C-GBL-08) |
| T-H-07 | Time-off qizil ✗ pozitsiya | Domain'da yo'q (C-AVL-01..03) |
| T-H-08 | Bino o'tish vaqti 1 period, ketma-ket darslar turli binoda | Taqiqlangan (C-BLD-03) |
| T-H-09 | Xona sig'imi 25, sinf 30 o'quvchi, `UseCapacities=true` | Xona mos emas (C-ROM-02) |
| T-H-10 | Ko'p haftalik: dars faqat 2-haftada | Faqat 2-hafta slotlariga (C-CYC-01) |

### 6.3. Soft constraint optimizatsiya testlari

| # | Stsenariy | Kutilgan natija |
|---|---|---|
| T-S-01 | O'qituvchida 1-va-6 dars, oralig'i bo'sh | `gaps = 3`, `C-TCH-01` jarimasi ishlaydi (`#1215` misolidagi kabi) |
| T-S-02 | 8 ta ketma-ket dars, `maxConsec = 4` | `v = (8-4)^2 = 16` |
| T-S-03 | Fan haftada 2 marta, ikkalasi ham dushanba/seshanba | `C-DST-07` jarimasi > 0 |
| T-S-04 | Fan haftada 5 marta, 5 kun | `C-DST-01` jarimasi = 0 (ideal) |
| T-S-05 | Matematika 6-darsda, `?` belgilangan | `C-AVL-06` jarimasi = 100 |
| T-S-06 | Sinfda oyna (1-dars, bo'sh, 4-dars) | `C-CLS-01` = 800 (kritik) |
| T-S-07 | Tushlik oralig'i [4,6], barcha 4,5,6 band | `C-LUN-01` jarimasi |
| T-S-08 | Optimizatsiya 10^6 iteratsiya | `BestSoftCost` monoton kamayadi |
| T-S-09 | Kvadratik adolat: A o'qituvchida 4 ortiqcha vs 4 o'qituvchida 1 ortiqcha | Ikkinchisi arzonroq (16 vs 4) |

### 6.4. Algoritm invariantlari

| # | Tekshiruv |
|---|---|
| T-A-01 | **Determinizm:** bir xil seed + `Parallelism=1` → bayt-bayt bir xil yechim (10 marta) |
| T-A-02 | **Delta korrektligi:** `DeltaPenalty(m) == Evaluate(apply(m)) - Evaluate(s)` — 100k random move |
| T-A-03 | **Undo korrektligi:** `Apply(m); Undo(m)` → holat bayt-bayt asl holatga qaytadi |
| T-A-04 | **Hard invariant:** optimizatsiya davomida `H(S) == 0` hech qachon buzilmaydi |
| T-A-05 | **Cancellation:** `ct.Cancel()` → 100 ms ichida qaytadi, natija valid (anytime) |
| T-A-06 | **Monotonlik:** `BestSoftCost` hech qachon oshmaydi |
| T-A-07 | **Occupancy konsistentligi:** `TeacherBusy` bitmask'lari kartalar ro'yxatidan qayta hisoblangani bilan mos |
| T-A-08 | **Room matching:** har slot uchun xona tayinlash valid matching (Hopcroft–Karp natijasini brute-force bilan solishtirish, kichik holatlar) |
| T-A-09 | **Ejection chain terminatsiyasi:** `maxDepth` ga yetganda cheksiz rekursiya yo'q |
| T-A-10 | **Relaxation minimalligi:** qaytarilgan `relaxed` to'plami minimal (birortasini qaytarib yoqib bo'lmaydi) |

### 6.5. Integratsion / real ma'lumot testlari

| # | Stsenariy | Kutilgan |
|---|---|---|
| T-I-01 | Kichik maktab: 6 sinf, 12 o'qituvchi, 5 kun × 6 period | < 1 s, 100% joylashgan |
| T-I-02 | O'rta: 20 sinf, 45 o'qituvchi (aSc demo miqyosi, `#2478`: 45 teachers) | < 15 s |
| T-I-03 | Katta: 40 sinf, 80 o'qituvchi, 1200 karta | < 90 s (1 yadro), < 20 s (8 yadro) |
| T-I-04 | Ataylab imkonsiz: barcha o'qituvchilar 0 oyna talab qiladi (`#2463`) | Faza 0 FAULT, generatsiya boshlanmaydi |
| T-I-05 | Relaxation kerak: T-I-04 + `AllowRelaxation=true` | Yechim + buzilgan constraint'lar ro'yxati (`#3226`) |
| T-I-06 | Ko'p bino: 3 bino, `transitTime=1` | Barcha o'tishlar >= 1 period bo'sh |
| T-I-07 | Ko'p hafta: 2 haftalik sikl (A/B) | Har dars to'g'ri haftada |
| T-I-08 | "Improve" rejimi: mavjud yechimni yuklab optimizatsiya | `SoftCost` kamayadi, `H(S)` = 0 saqlanadi |
| T-I-09 | Qisman qulflangan jadval: 30% kartalar locked | Locked kartalar qimirlamaydi |
| T-I-10 | Regressiya: aSc `demos/Demo1.roz` ekvivalenti | Sifat aSc natijasidan yomon emas (soft cost taqqoslash) |

### 6.6. Fuzz / property-based testlar

- Tasodifiy masala generatori (sinf/o'qituvchi/constraint sonini randomlash) → **hech qachon crash bo'lmasin**,
  natija yoki `COMPLETE` (barcha hard qoniqtirilgan), yoki `PARTIAL` (joylanmaganlar ro'yxati bilan).
- **Metamorfik test:** constraint qo'shish `BestSoftCost` ni kamaytira olmaydi (monotonlik).
- **Metamorfik test:** o'qituvchilarni/sinflarni qayta nomlash (permutatsiya) natija sifatini o'zgartirmaydi.

---

## Ilova A — Rekonstruksiya qilingan hujjat manbalarining ID indeksi

| Blok | ID oralig'i | Mazmuni |
|---|---|---|
| Teacher / Constraints dialogi | `#1206–#1222` | Oynalar, kunlar, ketma-ketlik |
| Verification faults | `#1731–#1740`, `#1817–#1852` | Dastur **haqiqatda tekshiradigan** shartlar |
| Testing / diagnostics | `#1784–#1808`, `#3980–#3993` | Dekompozitsiya testi, MUS izlash |
| Complexity | `#1336`, `#1348–#1351`, `#2527` | Small / Normal / Large / Huge |
| Class conditions | `#2650–#2652`, `#2714–#2716`, `#2768` | Kunlik min/max, education block |
| Lunch | `#2641–#2644`, `#2712`, `#3545` | Tushlik oynasi |
| Distribution | `#2027`, `#2306–#2313`, `#2719–#2720`, `#2775–#2777`, `#3727–#3731` | Taqsimot |
| Grouped constraints (asosiy ro'yxat) | `#3451–#3500` | Cheklovlar nomlarining to'liq ro'yxati |
| Selected positions | `#3057–#3098` | Maska ustidan agregat cheklovlar |
| Card relationships (kengaytirilgan) | `#3864–#3919` | A/B seksiyalar munosabatlari |
| Buildings | `#2968–#2969`, `#3009`, `#3090–#3091`, `#3481–#3482`, `#4192` | Binolar |
| Advisor / school profile | `#4100–#4155` | Maktab turi bo'yicha standart cheklovlar to'plami |
| Relaxation | `#2732`, `#3072–#3073`, `#3081`, `#3224–#3226`, `#3972` | Yumshatish |

**Ishchi fayllar** (scratchpad, hujjat yozilgandan keyin o'chirilishi mumkin):
`/private/tmp/claude-501/-Users-me-Projects-TimeTables-TimeTables/5bbb8945-b767-4f98-917b-ea9279e39548/scratchpad/en_strings.txt`
(2738 ingliz string, `ID<TAB>text` formatida — qayta tahlil uchun saqlab qo'yish tavsiya etiladi).
