# 05 — Mavjud algoritm va Application qatlami auditi

> **Hujjat maqsadi:** `DarsJadvali.Application` qatlamini (generatsiya, validatsiya, servislar, testlar)
> aSc TimeTables darajasidagi generatorga o'tish nuqtai nazaridan baholash.
> **Qamrov:** `src/DarsJadvali.Application/**`, `tests/DarsJadvali.Tests/**`.
> **Sana:** 2026-08-14. **Holat:** faqat audit — hech qanday fayl o'zgartirilmagan.

---

## 1. Mavjud generator tahlili

### 1.1 Umumiy tavsif

Yagona generator — `GreedyScheduleGenerator` (`src/DarsJadvali.Application/Generation/GreedyScheduleGenerator.cs`, 276 qator).
Bu **deterministik, backtracking'siz, "first-fit decreasing" (FFD) tipidagi ochko'z algoritm**.
Klassik terminologiyada bu *constructive heuristic* — qidiruv (search) umuman yo'q,
optimallashtirish (local search) ham yo'q.

### 1.2 Algoritm oqimi (pseudocode)

```
GenerateAsync(options, progress, ct):
    snapshot ← ScheduleSnapshot.LoadAsync(uow, options.ScheduleId)   # butun bazani xotiraga
    if options.ClearExisting:
        ClearScheduleAsync(snapshot)                                  # har bir yozuv alohida DELETE

    workDays ← snapshot.ActiveWorkDays
    if workDays boʻsh:  return Fail("faol ish kuni yoʻq")

    assignments ← OrderAssignments(snapshot.Assignments, seed)
        # WeeklyHoursCount boʻyicha KAMAYISH tartibida (FFD yadrosi)
        # seed bor boʻlsa — teng soatlilar orasida tasodifiy tartib
    if assignments boʻsh: return Fail("biriktirma yoʻq")

    # Har bir biriktirma uchun qolgan soatni hisoblash
    demand ← []
    for a in assignments:
        already ← COUNT(snapshot.Entries where teacher=a.T and subject=a.S and class=a.C)  # O(E) chiziqli!
        need ← max(0, a.WeeklyHoursCount − already)
        if need > 0: demand.add((a, need)); totalHours += need
    if totalHours == 0: return Success(0)

    maxIterations ← max(options.MaxIterations, totalHours)   # ← amalda hech qachon ishlamaydi

    for (assignment, hours) in demand:            # tartib QAT'IY, qayta tartiblanmaydi
        for i in 0..hours-1:
            if ct.IsCancellationRequested: break
            if ++iterations > maxIterations: break        # oʻlik shart

            entry ← TryPlace(snapshot, assignment, workDays, room, scheduleId)
            if entry == null:
                unplaced += (hours − i)          # QOLGAN BARCHA soat "joylashmadi" deb belgilanadi
                break                            # bu biriktirma butunlay tashlab yuboriladi
            snapshot.Add(entry)                  # faqat XOTIRAGA
            newEntries.add(entry); placed++
            progress.Report(...)                 # HAR BIR dars uchun (throttling yoʻq)

    for e in newEntries: uow.ScheduleEntries.AddAsync(e, CancellationToken.None)   # bittalab
    uow.SaveChangesAsync(CancellationToken.None)
    return Result(Success = (unplaced == 0 && !cancelled), placed, unplaced, messages, elapsed)


TryPlace(snapshot, assignment, workDays, room, scheduleId):
    fallback ← null
    for day in workDays:                          # HAR DOIM dushanbadan
        for lesson in 1..day.MaxLessonsPerDay:    # HAR DOIM 1-soatdan
            draft ← Draft(class, subject, teacher, day, lesson, room, scheduleId)
            conflicts ← snapshot.Validate(draft)  # 10 ta qoida
            if conflicts boʻsh:  return draft     # BIRINCHI mos joy — darhol qaytadi
            if fallback == null and conflicts.All(c => c.Severity == Warning):
                fallback ← draft                  # faqat ogohlantirishli joy — zaxira
    return fallback                               # hech nima boʻlmasa null
```

### 1.3 Murakkablik

| Bosqich | Murakkablik | Izoh |
|---|---|---|
| `ScheduleSnapshot.LoadAsync` | 8 ta to'liq jadval `SELECT *` | O'quv yili/variantga qaramay **hamma** yozuv o'qiladi, keyin xotirada filtrlanadi |
| `demand` hisobi | **O(A × E)** | Har bir biriktirma uchun barcha yozuvlar chiziqli sanaladi (`:77-78`) |
| `TryPlace` (bitta soat) | O(D × L × k) | D = kunlar, L = kundagi soatlar, k = slotdagi yozuvlar soni |
| `Validate` (bitta urinish) | O(k) + doimiy | Indekslangan (`_bySlot`, `_byClassDay`, `_byTriple`) — bu yaxshi |
| **Umumiy** | **O(H × D × L × k + A × E)** | H = jami soatlar. Amaliy: 3000 soat × 6 kun × 8 soat ≈ 144 000 `Validate` chaqiruvi |
| Bazaga yozish | **O(H) ta alohida INSERT** + O(E) ta DELETE | Bulk yo'q, tranzaksiya yo'q |

Xotira: butun baza (o'qituvchi, fan, sinf, biriktirma, kun, bandlik, slot, **barcha yillar yozuvlari**)
bir vaqtda RAM'da. Kichik maktabda muammo emas, lekin ko'p yillik arxivda o'sib boradi.

### 1.4 Backtracking, qayta urinish, muvaffaqiyatsizlik

- **Backtracking YO'Q.** Joylashtirilgan dars hech qachon ko'chirilmaydi yoki olib tashlanmaydi.
- **Restart YO'Q.** Bitta o'tish (single pass), qayta boshlash yo'q.
- **Local search YO'Q.** Ejection chain, swap, relaxation — hech biri yo'q.
- Muvaffaqiyatsizlikda (`:131-143`): biriktirmaning qolgan **barcha** soatlari
  `unplaced` ga qo'shiladi va `break` bilan tashlab yuboriladi. Foydalanuvchiga
  matnli xabar beriladi, lekin **sabab diagnostikasi yo'q** (qaysi cheklov to'sdi — noma'lum).
- Natija: qisman jadval baribir bazaga yoziladi (`Success=false` bo'lsa ham).

### 1.5 Determinizm va seed

- `RandomSeed` **faqat** teng `WeeklyHoursCount` li biriktirmalar tartibini aralashtiradi (`:241-248`).
- `TryPlace` ichida tasodifiylik **umuman yo'q** — slot tanlash har doim `(dushanba, 1-soat)` dan boshlab skanerlanadi.
- Demak: **har xil seed ko'pincha bir xil yoki deyarli bir xil jadval beradi.** "Random restart" ni
  amalga oshirish uchun asos yo'q.
- Seed berilganda barqaror tiebreaker'lar (`ClassGroupId`, `SubjectId`) tushib qoladi va
  boshlang'ich tartib repozitoriy qaytargan tartibga bog'lanadi → **takrorlanuvchanlik kafolatlanmaydi**.

### 1.6 CancellationToken va progress

- `ct` **faqat** ichki siklda tekshiriladi (`:117`), `TryPlace` ichida emas.
- Yozish bosqichi ataylab `CancellationToken.None` bilan bajariladi (`:159, :164`) —
  bekor qilinsa ham qisman natija saqlanadi. Bu qaror hujjatlashtirilgan, lekin
  **tranzaksiya yo'qligi** bilan birga xavfli (§3, K-04).
- `IProgress<GenerationProgress>` bor va har bir dars uchun `Report` chaqiriladi (`:150`) —
  throttling yo'q, 3000 darsda UI thread'ga 3000 marshalling.

---

## 2. Qo'llab-quvvatlanadigan cheklovlar matritsasi

Belgilar: ✅ to'liq · ⚠️ qisman/soft · ❌ yo'q

| # | Cheklov | Generatorda | Validatsiyada | Turi | Holat |
|---|---|---|---|---|---|
| 1 | O'qituvchi to'qnashuvi (`TEACHER_BUSY`) | ✅ | ✅ `ScheduleSnapshot.cs:221-228` | Hard | To'g'ri |
| 2 | Sinf to'qnashuvi (`CLASS_BUSY`) | ✅ | ✅ `:230-237` | Hard | To'g'ri |
| 3 | Xona bandligi (`ROOM_BUSY`) | ⚠️ | ⚠️ `:239-253` | Hard | Xona = sinfning yagona xonasi; Room entity yo'q |
| 4 | Kun faol emas (`DAY_INACTIVE`) | ✅ | ✅ `:173-184` | Hard | To'g'ri |
| 5 | Soat diapazoni (`LESSON_OUT_OF_RANGE`) | ✅ | ✅ `:186-194` | Hard | `LessonSlot` bilan bog'lanmagan |
| 6 | O'qituvchi faolligi (`TEACHER_INACTIVE`) | ✅ | ✅ `:196-206` | Hard | To'g'ri |
| 7 | Biriktirma mavjudligi (`NO_ASSIGNMENT`) | ✅ | ✅ `:208-215` | Hard | To'g'ri |
| 8 | O'qituvchi ish vaqti (`TEACHER_UNAVAILABLE`) | ⚠️ | ⚠️ `:255-260`, `:293-337` | Hard | `LessonSlot` bo'lmasa **jimgina o'tkazib yuboriladi** |
| 9 | Haftalik soat me'yori (`WEEKLY_HOURS_EXCEEDED`) | ⚠️ Warning | ⚠️ `:262-276` | Soft | Generator warning'li joyni fallback sifatida qabul qiladi |
| 10 | Fan kunda takrorlanishi (`SUBJECT_REPEATED_IN_DAY`) | ⚠️ Warning | ⚠️ `:278-288` | Soft | Bitta kunda 2 marta — faqat ogohlantirish |
| — | **Quyidagilar umuman yo'q** | | | | |
| 11 | Xona sig'imi (`StudentCount` vs sig'im) | ❌ | ❌ | Hard | `ClassGroup.StudentCount` bor, ishlatilmaydi |
| 12 | Xona turi/talabi (laboratoriya, sport zal) | ❌ | ❌ | Hard | Model yo'q |
| 13 | Sinf bandligi (sinfning o'z cheklovi) | ❌ | ❌ | Hard | Faqat o'qituvchida `TeacherAvailability` bor |
| 14 | Qo'sh darslar (double/block lessons) | ❌ | ❌ | Hard | Model ham, mantiq ham yo'q |
| 15 | Sinf uchun kunlik min/max darslar | ❌ | ❌ | Hard | Faqat `MaxLessonsPerDay` (global, kun bo'yicha) |
| 16 | O'qituvchi kunlik/haftalik max yuklama | ❌ | ❌ | Hard | Yo'q |
| 17 | Bo'shliqlar / oynalar (gaps) — sinf | ❌ | ❌ | Soft | Yo'q |
| 18 | Bo'shliqlar / oynalar (gaps) — o'qituvchi | ❌ | ❌ | Soft | Yo'q |
| 19 | Fan taqsimoti hafta bo'ylab (spread) | ❌ | ⚠️ (10-qoida qisman) | Soft | To'liq spread mantiqi yo'q |
| 20 | O'qituvchining bo'sh kuni (free day) | ❌ | ❌ | Soft | Yo'q |
| 21 | Tushlik oynasi (lunch window) | ❌ | ❌ | Hard/Soft | Yo'q |
| 22 | Bino almashish (building change) | ❌ | ❌ | Hard/Soft | Bino modeli yo'q |
| 23 | Qiyin fanlar ertalab (position preference) | ❌ | ❌ | Soft | Yo'q |
| 24 | Ketma-ket darslar chegarasi (max consecutive) | ❌ | ❌ | Soft | Yo'q |
| 25 | Bo'lingan guruhlar (split groups / podgruppa) | ❌ | ❌ | Hard | Model yo'q |
| 26 | Bir darsda bir nechta o'qituvchi (co-teaching) | ❌ | ❌ | Hard | Model yo'q |
| 27 | A/B hafta rotatsiyasi | ❌ | ❌ | Hard | Model yo'q |
| 28 | Qotirilgan (pinned/fixed) darslar | ❌ | ❌ | Hard | `ClearExisting=false` qisman o'rnini bosadi |
| 29 | Soft cheklov og'irliklari (weights) | ❌ | ❌ | — | Skorlash tizimi yo'q |

**Xulosa:** 29 tipik cheklovdan **7 tasi to'liq**, **4 tasi qisman**, **18 tasi umuman yo'q**.
aSc darajasi uchun qamrov ≈ 25 %.

### 2.1 Generator ↔ validatsiya izchilligi

Yaxshi tomon: **qoidalar takrorlanmaydi.** Generator o'zining tekshiruvlarini yozmagan —
u `ScheduleSnapshot.Validate()` ni chaqiradi (`GreedyScheduleGenerator.cs:208`), validator ham
o'shani chaqiradi (`ScheduleValidator.cs:39`). `LessonAvailabilityRules` ham bandlik qoidasining
yagona manbasi sifatida ishlatiladi (`ScheduleSnapshot.cs:311,320` va `IAvailabilityService.cs:149`).
Bu arxitektura qarori **to'g'ri va saqlanishi kerak.**

Yomon tomon: **siyosat (policy) izchil emas.**
`ScheduleService.PlaceAsync` Warning bo'lsa `force=false` da yozuvni **rad etadi**
(`IScheduleService.cs:126`), lekin generator xuddi shu Warning'li joyni fallback sifatida
**qabul qiladi** (`GreedyScheduleGenerator.cs:215-218`). Natijada generator qo'lda kiritishda
taqiqlangan jadvalni yaratadi.

---

## 3. Kritik kamchiliklar (jiddiylik bo'yicha)

### 🔴 Blocker

**K-01 · `GreedyScheduleGenerator.cs:184-223` — backtracking va local search umuman yo'q**
*Muammo:* birinchi mos slot darhol qat'iy band qilinadi, hech qachon qayta ko'rib chiqilmaydi.
*Qanday sindiradi:* real maktab ma'lumotida (zich biriktirmalar, o'qituvchi bandligi cheklovlari)
oxirgi biriktirmalarga joy qolmaydi va **jadval to'liq bitmaydi** — foydalanuvchi "N soat
joylashtirilmadi" xabarini oladi, garchi yechim mavjud bo'lsa ham. Bu FFD ning ma'lum
kamchiligi: greedy graf bo'yash NP-qiyin masalada optimal emas.
*Tuzatish:* konstruktiv bosqichdan keyin **min-conflicts / ejection chain** local search qo'shish;
tushib qolgan darsni majburan joylashtirib, to'qnashgan darsni "chiqarib yuborish" (eject) va
uni qayta joylashtirish zanjiri.

**K-02 · `GreedyScheduleGenerator.cs:186-220` — slot tanlash strategiyasi yo'q (pure first-fit)**
*Muammo:* har doim `(dushanba, 1-soat)` dan skanerlanadi; slot tanlashda hech qanday
evristika (LCV — least constraining value, domain size, balance) yo'q.
*Qanday sindiradi:* darslar hafta boshiga siqiladi, hafta oxiri bo'sh qoladi; sinf va
o'qituvchi yuklamasi kunlar bo'ylab notekis; oynalar (gaps) ko'payadi. Jadval texnik jihatdan
"konfliktsiz" bo'lsa ham **amalda foydalanishga yaroqsiz** bo'ladi.
*Tuzatish:* slotlarni skorlash funksiyasi bilan tartiblash (yuklama balansi, gap jarimasi,
spread bonusi) va eng yaxshi skorli slotni tanlash.

**K-03 · `GreedyScheduleGenerator.cs:274-275` — sifat metrikasi yo'q**
*Muammo:* `Fitness = placed / total`. Jadval "yaxshi"ligini o'lchaydigan hech narsa yo'q.
*Qanday sindiradi:* optimallashtirishning matematik asosi yo'q — nima yaxshilanayotganini
o'lchab bo'lmaydi, demak local search ham qo'shib bo'lmaydi. Bu K-01 ni to'sib turadi.
*Tuzatish:* og'irlikli soft-constraint skorlash: `cost = Σ wᵢ × violationᵢ`, inkremental
(delta) hisoblash bilan.

**K-04 · `GreedyScheduleGenerator.cs:49, 157-165, 257-272` — tranzaksiya yo'q → ma'lumot yo'qolishi**
*Muammo:* `ClearScheduleAsync` eski jadvalni **o'chirib va `SaveChangesAsync` bilan commit qilib**
yuboradi (`:267`), keyin yangi yozuvlar alohida saqlanadi (`:164`). Ikkalasi bitta tranzaksiyada emas.
*Qanday sindiradi:* generatsiya o'rtasida istisno (yoki dastur qulashi) yuz bersa — **eski jadval
o'chgan, yangisi yozilmagan**. Foydalanuvchi butun ishini yo'qotadi.
*Tuzatish:* `IUnitOfWork` ga `BeginTransactionAsync` qo'shish yoki butun generatsiyani bitta
`SaveChangesAsync` bilan yakunlash (delete + insert birga).

### 🟠 Yuqori

**K-05 · `ScheduleSnapshot.cs:294-300` — cheklov jimgina o'tkazib yuboriladi**
*Muammo:* `CheckAvailability` shu dars raqami uchun `LessonSlot` topilmasa `null` qaytaradi,
ya'ni "konflikt yo'q".
*Qanday sindiradi:* `WorkDay.MaxLessonsPerDay = 8`, lekin `LessonSlot` faqat 7 tagacha sozlangan
bo'lsa — **8-soatda o'qituvchi bandligi umuman tekshirilmaydi**. Generator o'qituvchini
ishlamaydigan vaqtga qo'yadi. `LessonSlot` jadvali bo'sh bo'lsa — bandlik butunlay e'tiborsiz.
*Tuzatish:* slot topilmasa `Error` darajali konflikt qaytarish (yoki `MaxLessonsPerDay` ni
`LessonSlot` soni bilan majburiy moslashtirish).

**K-06 · `GreedyScheduleGenerator.cs:215-218` ↔ `IScheduleService.cs:126` — siyosat qarama-qarshiligi**
*Muammo:* generator Warning'li slotni qabul qiladi, `PlaceAsync` esa uni rad etadi.
*Qanday sindiradi:* generatsiya `WEEKLY_HOURS_EXCEEDED` yoki `SUBJECT_REPEATED_IN_DAY` li
jadval yaratadi; foydalanuvchi keyin o'sha darsni qo'lda **bir katak surolmaydi** —
tizim "warning" deb to'sadi. UX ziddiyati.
*Tuzatish:* soft cheklovlarni skorga aylantirib, yagona `AcceptancePolicy` orqali boshqarish.

**K-07 · `ScheduleValidator.cs:21, 32` + `ScheduleSnapshot.cs:126-136` — har validatsiyada butun baza qayta o'qiladi**
*Muammo:* `ValidateAsync` **har bir chaqiruvda** `ScheduleSnapshot.LoadAsync` ni bajaradi —
bu 8 ta to'liq `SELECT *`.
*Qanday sindiradi:* UI'da drag & drop qilganda har bir siljish 8 ta to'liq jadval o'qishini
keltirib chiqaradi. 3000 yozuvli bazada interfeys sezilarli sekinlashadi. Bundan tashqari
`LoadAsync` **barcha yillar/variantlar** yozuvini o'qib, keyin xotirada filtrlaydi (`:136`).
*Tuzatish:* snapshot'ni scoped keshga chiqarish (invalidatsiya bilan); repozitoriyga
`GetByScheduleAsync(scheduleId)` predikатli metod qo'shish.

**K-08 · `ActiveScheduleResolver.cs:31-86` — "o'qish" metodi bazaga YOZADI**
*Muammo:* `GetActiveAsync` kerak bo'lsa `AcademicYear` va `Schedule` yaratadi, `IsActive`
bayroqlarini o'zgartiradi va `SaveChangesAsync` chaqiradi. U `ScheduleSnapshot.LoadAsync:123`
dan, ya'ni **har bir validatsiyadan** chaqiriladi.
*Qanday sindiradi:* toza validatsiya chaqiruvi bazani mutatsiya qiladi — CQS buzilishi.
Parallel chaqiruvlarda ikkita "Asosiy jadval" yaratilishi mumkin (race condition, unique
constraint yo'q). Read-only rejim/tranzaksiya ichida kutilmagan yozuv.
*Tuzatish:* `EnsureActiveAsync` (yozadi) va `TryGetActiveAsync` (faqat o'qiydi) ga bo'lish;
snapshot faqat ikkinchisini ishlatsin.

**K-09 · `GreedyScheduleGenerator.cs:77-78` — O(A × E) sanoq**
*Muammo:* har bir biriktirma uchun `snapshot.Entries.Count(...)` — to'liq chiziqli o'tish.
Holbuki `ScheduleSnapshot._byTriple` indeksi **allaqachon mavjud** (`:25`), lekin tashqariga chiqarilmagan.
*Qanday sindiradi:* 500 biriktirma × 3000 yozuv = 1.5 mln taqqoslash; `ClearExisting=false`
rejimida generatsiya boshlanishi sezilarli kechikadi.
*Tuzatish:* `ScheduleSnapshot` ga `CountFor(teacherId, subjectId, classGroupId)` metodini qo'shib,
`_byTriple` indeksidan foydalanish → O(1).

**K-10 · `GreedyScheduleGenerator.cs:94, 123-128` — `MaxIterations` o'lik kod**
*Muammo:* `maxIterations = Math.Max(options.MaxIterations, totalHours)`, `iterations` esa har bir
soat uchun bir marta oshadi, ya'ni **eng ko'pi bilan `totalHours` gacha yetadi**. Shart
`iterations > maxIterations` **hech qachon rost bo'lmaydi**.
*Qanday sindiradi:* foydalanuvchi `MaxIterations` ni o'zgartirsa hech narsa o'zgarmaydi —
"soxta sozlama". Kelajakdagi iterativ algoritmda ham chalg'ituvchi.
*Tuzatish:* olib tashlash yoki haqiqiy iteratsiya hisoblagichiga (local search qadamlariga) bog'lash.

### 🟡 O'rta

**K-11 · `GreedyScheduleGenerator.cs:157-165, 257-267` — bittalab INSERT/DELETE (N+1 yozuv)**
3000 dars = 3000 ta `AddAsync`, eski jadval = E ta `DeleteAsync`. Bulk yo'q.
*Tuzatish:* `AddRangeAsync` / `ExecuteDeleteAsync` (EF Core 8) qo'shish.

**K-12 · `GreedyScheduleGenerator.cs:236-255` — seed deyarli ta'sir qilmaydi**
Tasodifiylik faqat teng soatli biriktirmalar tartibida; slot tanlashda yo'q. Random restart
qurish imkonsiz. Seedsiz holatda ham boshlang'ich tartib repozitoriy tartibiga bog'liq →
takrorlanuvchanlik kafolatlanmaydi.
*Tuzatish:* markazlashgan `Random` ni butun algoritm bo'ylab uzatish; barqaror tiebreaker'ni
seed bilan birga saqlash.

**K-13 · `GreedyScheduleGenerator.cs:150-153` — progress throttling yo'q**
Har bir dars uchun `Report` + string interpolatsiya (`:152`). 3000 darsda 3000 UI marshalling
va 3000 ta ortiqcha string. *Tuzatish:* har 1 % da yoki 50 ms da bir marta xabar berish.

**K-14 · `ScheduleValidator.cs:47, 66` — konfliktlar matn bo'yicha deduplikatsiya qilinadi**
`HashSet<(Code, Message)>` — matni bir xil, lekin **turli yozuvlarga** tegishli konfliktlar
bittaga siqiladi. *Qanday sindiradi:* foydalanuvchi "3 ta muammo bor" deb o'ylaydi, aslida 30 ta.
*Tuzatish:* `Conflict` ga `EntryId` maydonini qo'shib, shu bo'yicha ajratish.

**K-15 · `ScheduleSnapshot.cs:110-111` — `ActiveWorkDays` har murojaatda yangi ro'yxat yaratadi**
Property ichida `Where + OrderBy + ToList`. Xususiyat ko'rinishida bo'lgani uchun chaqiruvchi
uni sikl ichida ishlatib qo'yishi mumkin. *Tuzatish:* konstruktorda bir marta hisoblash.

**K-16 · `ScheduleSnapshot.cs:339-341` — `IsSame` faqat `Id` bo'yicha**
Generatsiya paytida barcha yangi yozuvlarda `Id = 0`, draft'da `Id = null` — hozircha ishlaydi,
lekin `SaveChangesAsync` dan keyin snapshot'dagi nusxalar eskiradi (Id'lar yangilanmaydi).
Snapshot qayta ishlatilsa nozik xatolar chiqadi. *Tuzatish:* barqaror `Guid`/reference identifikator.

**K-17 · `GenerationOptions.cs:13, 16` — o'lik sozlamalar**
`PopulationSize` va `MutationRate` genetik algoritm uchun, lekin genetik algoritm yo'q.
Ommaviy API'da chalg'ituvchi. *Tuzatish:* algoritmga xos sozlamalarni alohida tur bilan berish.

**K-18 · `IAvailabilityService.cs:99-110` — o'chirib-qayta yozish, tranzaksiyasiz + kirish obyektini mutatsiya qiladi**
`item.TeacherId` va `item.Id = 0` chaqiruvchining obyektlarini o'zgartiradi (`:107-108`);
delete + insert bitta tranzaksiyada emas.

**K-19 · `IScheduleSetService.cs:149-163, 239-241` va `IScheduleService.cs:236-238` — ortiqcha o'qish**
Har safar `ScheduleEntries.GetAllAsync()` (barcha yillar, barcha variantlar) chaqirilib,
keyin xotirada `Where(e => e.ScheduleId == …)` qilinadi. `DuplicateAsync` yozuvlarni bittalab qo'shadi.

---

## 4. Application qatlami arxitektura muammolari

### 4.1 Interfeys va implementatsiya bitta faylda — 10 ta faylda

`Export/TimetableExportModelBuilder.cs`, `Services/IWorkDayService.cs`, `ITeacherService.cs`,
`IScheduleService.cs`, `ISubjectService.cs`, `IAssignmentService.cs`, `IAcademicYearService.cs`,
`IScheduleSetService.cs`, `IClassGroupService.cs`, `IAvailabilityService.cs`.

Eng yomoni: `Services/IScheduleSetService.cs` — 287 qator, ichida `IScheduleSetService` (60 qator)
va `ScheduleSetService` (227 qator). `IScheduleService.cs` da bundan tashqari `PlacementResult`
record'i ham bor; `IAvailabilityService.cs` da `TeacherDayAvailability` record'i.

*Muammo:* fayl nomi (`I…`) mazmuniga mos emas; kontrakt va detal bir joyda; git diff'lar aralashadi;
test uchun faqat interfeysni ko'rish qiyin.
*Tuzatish:* `Abstractions/IXxxService.cs` (kontrakt) + `Services/XxxService.cs` (implementatsiya) +
`Models/…` (DTO record'lar) ga ajratish.

### 4.2 Repozitoriy abstraksiyasi juda sodda

`IRepository<T>` (`Abstractions/IRepository.cs`) da faqat `GetAllAsync` bor — **predikat, sahifalash,
proyeksiya, bulk operatsiya yo'q**. Natijada barcha filtrlash xotirada bajariladi.
`GetAllAsync` 13 ta faylda 54+ marta chaqiriladi. Bu qatlamning tizimli **over-fetch** muammosi.

*Tuzatish:* `GetWhereAsync(Expression<Func<T,bool>>)`, `CountAsync(predicate)`, `AddRangeAsync`,
`DeleteWhereAsync` qo'shish. Bu Infrastructure bilan kelishilishi kerak (bu audit hududidan tashqarida).

### 4.3 Async/await — umuman to'g'ri

- `ConfigureAwait(false)` izchil qo'llangan.
- `async void` **yo'q**, `.Result` / `.Wait()` **yo'q**, `Task.Run` ustidan bloklash **yo'q**.
- `ct` deyarli hamma joyda uzatiladi (istisno: `GreedyScheduleGenerator.cs:159,164` — ataylab).
- ⚠️ Sikl ichida `await` (N+1) — 6 joyda: `IWorkDayService.cs:117`, `IScheduleService.cs:220`,
  `IAvailabilityService.cs:100`, `IScheduleSetService.cs:151`, `GreedyScheduleGenerator.cs:157, 260`.
  Bular ketma-ket `await` bo'lgani uchun to'g'ri (parallel emas), lekin samarasiz.

### 4.4 Mas'uliyat chegaralari

Yaxshi: `IScheduleSetService` (jadval variantlari) va `IScheduleService` (dars yozuvlari) aniq ajratilgan,
hujjatlangan. `ActiveScheduleResolver` "qaysi jadval?" mantiqining yagona manbasi.
`LessonAvailabilityRules` bandlik qoidasining yagona manbasi. `ScheduleSnapshot` — validatsiya
mantiqining yagona manbasi. Bu **kuchli tomon** va yangi generatorga ko'chirilishi kerak.

Muammoli: `ScheduleSnapshot` `internal` (`ScheduleSnapshot.cs:12`) — Application ichida qulay,
lekin generatorni alohida testlash/almashtirish uchun ochiq domen modeli kerak.
Shuningdek u bir vaqtda **repozitoriy** (yuklash), **model** (indekslar) va **qoidalar dvigateli**
(`Validate`) vazifasini bajaradi — uchta mas'uliyat.

### 4.5 Domen modelining yetishmovchiligi (generator uchun to'siq)

Audit hududidan tashqarida, lekin qayd etish shart: `Room`, `Building`, `SubjectGroup` (podgruppa),
`LessonBlock` (qo'sh dars), `ClassAvailability`, `Subject.Difficulty`, `Teacher.MaxDailyLoad`
entity/maydonlari **yo'q**. Ularsiz §2 dagi 18 ta cheklovni umuman qo'shib bo'lmaydi.

---

## 5. Test holati

### 5.1 Build va test natijasi (haqiqiy ishga tushirish)

SDK: **.NET 10.0.302** (`/usr/local/share/dotnet/dotnet`), maqsad framework: `net8.0`.

```
$ dotnet build DarsJadvali.sln -v q
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:05.19
```

```
$ dotnet test tests/DarsJadvali.Tests -v q
Passed!  - Failed: 0, Passed: 147, Skipped: 0, Total: 147, Duration: 679 ms
```

✅ **Build toza (0 xato, 0 ogohlantirish), 147 test — hammasi o'tadi.**

### 5.2 Qamrov taqsimoti

| Fayl | Testlar | Baho |
|---|---|---|
| `UpdateCheckerTests.cs` | 33 | ⚠️ Ikkilamchi funksiya, lekin eng ko'p test |
| `ScheduleValidatorTests.cs` | 21 | ✅ 10 ta qoidaning hammasi qamrab olingan |
| `PdfExportTests.cs` | 17 | ✅ Yaxshi |
| `ScheduleSetServiceTests.cs` | 17 | ✅ Yaxshi |
| `ScheduleServiceTests.cs` | 11 | ✅ Place/Move/Clear/force qamrab olingan |
| `RepositoryTests.cs` | 10 | ✅ |
| `AcademicYearServiceTests.cs` | 9 | ✅ |
| `LessonAvailabilityTests.cs` | 9 | ✅ |
| **`GreedyScheduleGeneratorTests.cs`** | **6** | 🔴 **Loyihaning eng murakkab qismi — eng kam test** |
| `DatabaseMigrationTests.cs` | 4 | ✅ |

**Qamrov o'lchash asbobi yo'q** — `coverlet`/`ReportGenerator` paketi `DarsJadvali.Tests.csproj` da
mavjud emas, ya'ni haqiqiy foizli qamrov noma'lum.

### 5.3 Generator testlari nimani tekshiradi

Bor: baxtli yo'l (4 biriktirma × 3 soat = 12 dars), generatsiyadan keyin `ValidateAllAsync`
konfliktsizligi, nofaol kunga qo'yilmasligi, `ClearExisting=true` ishlashi, `Name`/`Description`
bo'sh emasligi, `Elapsed >= 0`.

### 5.4 Yetishmayotgan testlar (generator uchun kritik)

1. 🔴 **Muvaffaqiyatsizlik yo'li** — joy topilmaydigan holat; `UnplacedCount` to'g'ri sanalishi
   (ayniqsa `:142` dagi `unplaced += hours - i - 1` arifmetikasi).
2. 🔴 **`CancellationToken`** — bekor qilinganda qisman natija saqlanishi va `Success=false` bo'lishi.
3. 🔴 **Determinizm** — bir xil seed → bir xil jadval; turli seed → turli jadval (hozir bu **o'tmaydi**).
4. 🔴 **Warning fallback** (`:215-218`) — generator warning'li jadval yaratishi (K-06 ni qayd etuvchi test).
5. 🔴 **Zich (tight) stsenariy** — o'qituvchi bandligi bilan cheklangan, yechim mavjud, lekin
   greedy topa olmaydigan holat. Bu K-01 ni isbotlaydigan regressiya testi.
6. 🟠 **`ClearExisting=false`** — inkremental generatsiya, mavjud darslar hisobga olinishi (`:77-78`).
7. 🟠 **`MaxIterations`** — hozir hech qanday ta'siri yo'q (K-10).
8. 🟠 **`LessonSlot` yo'q holat** — bandlik jimgina o'tkazib yuborilishi (K-05).
9. 🟠 **Ko'p variantli izolyatsiya** — 2 ta `Schedule` bo'lganda generator faqat maqsadli jadvalga tegishi.
10. 🟡 **Ishlash (performance) testi** — 40 sinf, 60 o'qituvchi, ~1500 soat: vaqt va xotira budjeti.
11. 🟡 **Progress semantikasi** — `Current <= Total`, monoton o'sishi, oxirgi hisobot yakuniy bo'lishi.
12. 🟡 **Tranzaksiya/rollback** — yozish o'rtasida xato bo'lsa eski jadval yo'qolmasligi (K-04).

---

## 6. Yangi generator uchun migratsiya rejasi

### 6.1 Saqlanadigan (o'zgartirmasdan yoki kengaytirib)

| Komponent | Sabab |
|---|---|
| `IScheduleGenerator` (`Generation/IScheduleGenerator.cs`) | Interfeys to'g'ri: `Name`, `Description`, `GenerateAsync(options, progress, ct)`. Yangi algoritm shu kontraktni implement qiladi → UI o'zgarmaydi |
| `GenerationProgress`, `GenerationResult` | Progress modeli yetarli; `GenerationResult` ga `Score`/`SoftViolations` qo'shiladi |
| `Conflict`, `ConflictSeverity`, `ConflictCodes` | Konflikt so'zligi barqaror; yangi kodlar qo'shiladi |
| `LessonAvailabilityRules` | Bandlik qoidasining yagona manbasi — namunali yechim, saqlanadi |
| `ScheduleSnapshot` **indekslash g'oyasi** (`_bySlot`, `_byClassDay`, `_byTriple`) | Inkremental baholash uchun aynan shunday indekslar kerak |
| `ScheduleValidator` (tashqi API) | UI uchun validatsiya nuqtasi; ichi qayta yoziladi |
| `ActiveScheduleResolver` mas'uliyati | "Qaysi jadval?" mantiqi markazlashgani to'g'ri (lekin CQS bo'yicha bo'linadi — K-08) |
| Barcha 147 mavjud test | Regressiya himoyasi sifatida saqlanadi |

### 6.2 Almashtiriladigan

| Komponent | Nima bilan |
|---|---|
| `GreedyScheduleGenerator` (butun sinf) | Ko'p bosqichli `LocalSearchScheduleGenerator` |
| `TryPlace` (`:184-223`) | Domen + evristik slot tanlash (`ISlotSelector`) |
| `Fitness` (`:274`) | Og'irlikli `ScheduleCostFunction` (inkremental delta bilan) |
| `ScheduleSnapshot.Validate` (`:163-291`) | `IConstraint` ro'yxati (`IHardConstraint` / `ISoftConstraint`) |
| `GenerationOptions` (`PopulationSize`, `MutationRate`) | `SearchOptions` (restart soni, vaqt budjeti, harorat, og'irliklar) |
| `IRepository<T>.GetAllAsync` ga tayanish | Predikatli/bulk metodlar |

### 6.3 Bosqichma-bosqich reja

**0-bosqich — Poydevor (blokerlarni ochish)**
1. K-04: generatsiyani bitta tranzaksiyaga o'rash (`IUnitOfWork.BeginTransactionAsync`).
2. K-05: `LessonSlot` yo'qligida jimgina o'tkazib yuborishni `Error` ga aylantirish.
3. K-08: `ActiveScheduleResolver` ni `Ensure…` / `TryGet…` ga bo'lish.
4. K-09, K-11, K-19: indeksdan foydalanish + bulk operatsiyalar.
5. Yetishmayotgan generator testlarini (§5.4, 1–6) **avval** yozish — regressiya to'ri.

**1-bosqich — Domen modeli (EF entity'laridan ajratilgan)**
- `Lesson` (karta): sinf(lar), fan, o'qituvchi(lar), davomiylik (1 yoki 2 soat), talab qilinadigan xona turi.
- `TimeSlot`: (kun, soat) → global indeks; `SlotIndex = day * L + lesson`.
- `Resource`: o'qituvchi / sinf / xona — har biri uchun `bool[] occupancy` yoki `ulong[] bitmask`.
- `SchedulingProblem` (o'zgarmas kirish) va `SchedulingState` (o'zgaruvchan yechim).
- **Muhim:** Domain qatlamiga `Room`, `Building`, `LessonBlock`, `ClassAvailability`,
  `Subject.Difficulty`, `Teacher.MaxDailyLoad` qo'shilishi kerak (boshqa agent hududi — kelishiladi).

**2-bosqich — Cheklovlar dvigateli**
```csharp
interface IHardConstraint { bool IsSatisfied(SchedulingState s, Placement p); string Code { get; } }
interface ISoftConstraint { double Penalty(SchedulingState s); double Delta(SchedulingState s, Move m); int Weight { get; } }
```
- Mavjud 10 ta qoida `IHardConstraint`/`ISoftConstraint` ga ko'chiriladi (mantiq o'zgarmaydi → testlar o'tadi).
- Yangilari qo'shiladi: gaps, kunlik min/max, spread, qo'sh dars, bo'sh kun, tushlik, xona sig'imi.
- **`Delta` majburiy** — butun jadvalni qayta hisoblash o'rniga faqat o'zgarishni baholash.

**3-bosqich — Boshlang'ich yechim: constraint propagation**
- Har bir `Lesson` uchun **domen** (mumkin bo'lgan slotlar to'plami, bitmask).
- Forward checking + AC-3 uslubidagi tarqatish: joylashtirish qo'shni domenlarni qisqartiradi.
- Tartiblash evristikasi: **MRV** (eng kichik domen) + **degree** (eng ko'p bog'liqlik),
  qiymat tanlash: **LCV** (eng kam cheklovchi slot).
- Domen bo'shab qolsa → darhol qaytish (hozirgi "oxirigacha urinib ko'rish" o'rniga).

**4-bosqich — Local search (aSc yadrosi)**
- **Min-conflicts + ejection chain:** joylashmagan darsni majburan qo'yish → to'qnashgan darsni
  chiqarish → uni qayta joylashtirish (zanjir uzunligi cheklangan, tabu ro'yxati bilan).
- **Simulated annealing / Great Deluge** soft cheklov skorini pasaytirish uchun.
- **Random restart:** vaqt budjeti ichida bir necha marta qayta boshlash, eng yaxshisini saqlash.
- **Relaxation:** hech qanday yechim topilmasa, eng kam og'irlikli soft cheklovni yumshatib qayta urinish.
- Seed butun jarayonga uzatiladi → to'liq takrorlanuvchanlik (K-12).

**5-bosqich — Diagnostika va UX**
- Muvaffaqiyatsizlikda **sabab**: "Aliyev Vali — 5-A, Matematika: 2 soat qo'yilmadi,
  chunki o'qituvchining bo'sh 3 slotini 7-B sinfi band qilgan" (konflikt manbasini ko'rsatish).
- `GenerationResult` ga `Score`, `HardViolations`, `SoftViolations`, `BottleneckResources` qo'shish.
- Progress throttling (K-13), bekor qilishda toza rollback.

**6-bosqich — Arxitektura tozalash**
- Interfeys/implementatsiya fayllarini ajratish (§4.1, 10 ta fayl).
- `IRepository<T>` ni predikatli/bulk metodlar bilan kengaytirish (§4.2).
- `ScheduleSnapshot` ni uchga bo'lish: `SnapshotLoader` (I/O) · `ScheduleModel` (indekslar) ·
  `ConstraintEngine` (qoidalar).

### 6.4 Xavflar

| Xavf | Ta'sir | Yumshatish |
|---|---|---|
| Domain entity'lari yetishmaydi (Room, Block, guruh) | 18 ta cheklovni qo'shib bo'lmaydi | Domain agenti bilan avval kelishish; 1-bosqichni blokerga aylantirish |
| Local search vaqt budjeti UI'ni muzlatadi | UX buzilishi | Background task + `IProgress` + qat'iy vaqt chegarasi |
| Yangi cheklovlar mavjud 147 testni buzadi | Regressiya | Yangi cheklovlarni **standart holatda o'chiq** (opt-in) qilib chiqarish |
| Inkremental `Delta` xatosi → skor "drift" | Yechim sifati jimgina yomonlashadi | Har N qadamda to'liq qayta hisoblash bilan solishtiruvchi debug-assert |

---

## Yakuniy baho

| O'lcham | Baho | Izoh |
|---|---|---|
| Kod sifati (uslub, nullability, async) | 🟢 8/10 | Toza, hujjatlangan, 0 ogohlantirish |
| Arxitektura ajratilishi | 🟡 6/10 | Yagona-manba prinsipi yaxshi; fayl tashkiloti va repozitoriy abstraksiyasi zaif |
| Validatsiya to'liqligi | 🟡 5/10 | 10 ta qoida ishonchli, lekin 18 tasi yo'q |
| **Generator algoritmi** | 🔴 **2/10** | Backtracking, local search, skorlash, diagnostika — hech biri yo'q |
| Test qamrovi | 🟡 6/10 | 147 test o'tadi, lekin generator uchun atigi 6 ta |
| **aSc darajasiga yaqinlik** | 🔴 **~15 %** | Cheklovlar 25 %, algoritm 10 % |

**Asosiy xulosa:** mavjud kod **poydevor sifatida yaxshi** (validatsiya yagona manbadan,
indekslash to'g'ri, testlar bor), lekin `GreedyScheduleGenerator` ni **evolyutsion yaxshilab
bo'lmaydi** — u printsipial ravishda qidiruvsiz algoritm. Uni saqlab qolib (tezkor "qoralama"
rejimi sifatida), yonida yangi `LocalSearchScheduleGenerator` qurish tavsiya etiladi:
`IScheduleGenerator` interfeysi buni o'zgarishsiz qo'llab-quvvatlaydi.
