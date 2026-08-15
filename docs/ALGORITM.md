# Generatsiya algoritmi — `DarsJadvali.Scheduling`

`src/DarsJadvali.Scheduling` — **sof algoritm yadrosi**. Unda EF Core ham, ma'lumotlar
bazasi ham, UI ham yo'q; `.csproj` da **bitta ham tashqi NuGet paketi yo'q** (faqat BCL) —
`src/DarsJadvali.Scheduling/DarsJadvali.Scheduling.csproj`.

Yadro o'z ichki modeli bilan ishlaydi: barcha resurslar `0..N-1` zich `int` indeks,
vaqt esa bitta tekis slot fazosi. EF entity'lari bilan bog'lanish faqat
`src/DarsJadvali.Application/Scheduling/SchedulingMapper.cs` orqali.

---

## 1. Vaqt modeli va `SlotMask`

`Model/TimeGrid.cs`:

```
dayIndex = week * DaysPerWeek + day
slot     = dayIndex * Periods + period
```

Ya'ni hafta, kun va dars soati **bitta butun songa** yig'iladi. Chegaralar:
`Periods ≤ 64`, `SlotCount = Weeks × DaysPerWeek × Periods ≤ 512`.

`Model/SlotMask.cs` — qat'iy o'lchamli bitset: **8 × `ulong` = 512 bit**,
`readonly struct` (64 bayt, stack'da, boxing yo'q). Bandlik va domen tekshiruvi shu
bilan `O(1)` bo'ladi — asosiy tezlik manbai shu.

---

## 2. Fazalar (pipeline)

`Pipeline/Scheduler.cs` — **6 ta raqamli faza** va ular orasida bitta xona tayinlash fazasi.
Faza nomlari `Pipeline/GenerationOptions.cs` dagi `GenerationPhase` enum'idan:

| # | Faza | Fayl | Nima qiladi |
|---|---|---|---|
| 0 | `Verify` | `Pipeline/Verifier.cs` | Ma'lumotni **generatsiyadan oldin** tekshiradi — 9 ta kod: `CARD_NO_DOMAIN`, `TEACHER_OVERLOADED`, `CLASS_OVERLOADED`, `TOO_FREQUENT`, `ROOM_SHORTAGE`, `ROOM_CAPACITY`, `LOCKED_CONFLICT`, `HALL_TEACHER`, `HALL_GROUP`. Standart holatda xato topilsa ham davom etadi (`ContinueOnVerifyFaults = true`) |
| 1 | `Propagate` | `Pipeline/Propagator.cs` | **AC-3 uslubidagi domain qisqartirish** + singleton (unit) propagatsiya. Domeni bitta slotdan iborat kartalar navbatga tushadi, qo'shnilarining domenidan mos slotlar o'chiriladi. Domen bo'shab qolsa `Feasible = false` va `Scheduler` hisobotga `PROPAGATION_FAILED` kodini qo'shadi (`Pipeline/Scheduler.cs:84`) |
| 2 | `Construct` | `Pipeline/Constructor.cs` | Randomized **MRV + degree + aging** bo'yicha karta tanlash (namuna hajmi `SampleSize = 20`), slot tanlash — eng kam soft jarima o'sishi + shovqin. Bitta kartaga ko'pi bilan `MaxSlotProbe = 96` slot sinaladi |
| 3 | `EjectionChain` | `Pipeline/EjectionChainRepair.cs` | "Kick out and reinsert": joylanmagan kartani majburan qo'yib, to'qnashganlarni chiqaradi va zanjir bo'ylab qayta joylaydi. Chuqurlik `EffectiveEjectionDepth`, bir qadamda ko'pi bilan `maxVictims = 3` karta chiqariladi |
| — | `Rooms` | `Rooms/RoomAssigner.cs` + `Rooms/HopcroftKarp.cs` | Har slot uchun "kartalar × ruxsat etilgan xonalar" bipartite grafi quriladi, **Hopcroft–Karp** (`O(E·√V)`) bilan maksimal moslik topiladi. Xona ro'yxati bo'sh bo'lsa faza **umuman ishlamaydi** |
| 4 | `Optimize` | `Pipeline/Optimizer.cs` | **Simulated annealing + tabu** gibridi. Qo'shnilik: `SingleMove`, `Swap`, `RoomChange`, `BlockSwap`, `KempeChain`. Hard invariant hech qachon buzilmaydi (`SolutionState.TryApply` kafolatlaydi) |
| 5 | `Relax` | `Pipeline/Relaxer.cs` | Yechim to'liq bo'lmasa: **qaysi cheklovni yumshatish yordam berishini** aytadi (`C-CYC-03`, `C-AVL-01/02/03`, `C-ROM-01/02`, `C-DBL-01`, `C-GBL-01/02`). **Faqat tashxis** — yechimni o'zgartirmaydi va cheklovni o'chirmaydi |

Faza 2 va 3 **restart tsikli** ichida: `EffectiveRestarts` marta takrorlanadi, har
restartda eng yaxshi natija saqlanadi va `Unplaced == 0` bo'lishi bilan tsikl uziladi.

> **Tartibga diqqat.** Xona tayinlash fazasi `GenerationPhase` enum'ida `6`-o'rinda
> turgani bilan **amalda 3 va 4 orasida**, restartlar tugab eng yaxshi yechim
> tiklangandan keyin bir marta ishlaydi (`Scheduler.cs:126-131`).

---

## 3. Cheklovlar

### 3.1 Hard (buzilishi mumkin emas)

`Constraints/HardRules.cs` — hard buzilishlar yechimdan **mustaqil** qayta hisoblab
tekshiriladi (bitmask'larga ishonilmaydi):

| Kod | Ma'nosi |
|---|---|
| `C-GBL-01` | O'qituvchi bir vaqtda ikkita darsda |
| `C-GBL-02` | Guruh bir vaqtda ikkita darsda |
| `C-GBL-03` | Xonada bir vaqtda ikkita dars (`RoomDef.ParallelLessons` dan oshsa) |
| `C-GBL-06` | Qulflangan karta o'z joyida turishi shart |
| `C-GBL-07` | Xona talab qiladigan kartaga xona tayinlanmagan |
| `C-GBL-08` | Bir sinfda **turli bo'linishlar** (`divisiontag`) bir vaqtda |
| `C-AVL-01..05` | Taqiqlangan pozitsiya (time-off "qizil" yoki kun cheklovi) |
| `C-ROM-01` | Ruxsat etilmagan xona |
| `C-ROM-02` | Xona sig'imi yetarli emas |
| `C-DBL-01` | Juft/uzun dars kun chegarasidan chiqib ketgan |

### 3.2 Soft (jarima bilan) va og'irliklar

`Constraints/ConstraintSet.CreateDefault()` — standart to'plam va **aniq og'irliklar**:

| Kod | Nomi | Og'irlik | Sinf |
|---|---|---|---|
| `C-CLS-01` | Sinf jadvalidagi oynalar | **800** | `ClassGapsConstraint` |
| `C-DST-05` | Fan bir kunda bir marta | **600** | `SubjectOncePerDayConstraint` |
| `C-DST-01` | Haftalik tekis taqsimot | **500** | `EquableDistributionConstraint` |
| `C-TCH-07/08` | O'qituvchining dars kunlari soni (bo'sh kun) | **400** | `TeacherDaysTaughtConstraint` |
| `C-TCH-10` | O'qituvchining ketma-ket darslari | **400** | `TeacherMaxConsecutiveConstraint` |
| `C-CLS-03` | Sinfning kunlik darslari (min/max) | **400** | `ClassDailyLoadConstraint` |
| `C-TCH-01` | O'qituvchining haftalik oynalari | **300** | `TeacherGapsPerWeekConstraint` |
| `C-TCH-02` | O'qituvchining kunlik oynalari | **300** | `TeacherGapsPerDayConstraint` |
| `C-TCH-14/15` | O'qituvchining kunlik yuki (min/max) | **300** | `TeacherDailyLoadConstraint` |
| `C-AVL-06` | "`?`" belgilangan pozitsiyalar | **100** | `QuestionMarkedPositionConstraint` |

Jarima taqsimoti hisobotda cheklov bo'yicha ajratib beriladi
(`PenaltyEvaluator.Breakdown`, `ScheduleGenerationReport.PenaltyBreakdown`).

### 3.3 Qo'llab-quvvatlanadigan resurs sozlamalari

`Model/Definitions.cs`: o'qituvchi uchun `MaxGapsPerDay/Week`, `MaxConsecutivePeriods`,
`Min/MaxPeriodsPerDay`, `Min/MaxDaysPerWeek`; sinf uchun `MaxGapsPerDay`,
`Min/MaxLessonsPerDay`; xona uchun `Capacity`, `ParallelLessons`; fan uchun
`OncePerDay`, `Distribution`; dars uchun `PeriodsPerWeek`, `PeriodsPerCard`,
`AllowedDays`, `AllowedRoomIds`, `Locked`, `SkipDistribution`.
`-1` qiymati — "cheklanmagan".

---

## 4. Sozlamalar va determinizm

`Pipeline/GenerationOptions.cs`:

| Parametr | Standart |
|---|---|
| `Seed` | `12345` |
| `Complexity` | `Normal` |
| `InitialTemperature` | `1500.0` |
| `CoolingRate` | `0.99995` |
| `TabuTenure` | `12` |
| `ProgressInterval` | 100 ms |
| `RunVerify` / `ContinueOnVerifyFaults` / `AllowRelaxation` | `true` |
| `TimeLimit` | `null` (faqat `CancellationToken`) |

`Complexity` byudjetni belgilaydi (`Restarts` / `MaxBacktracks` / `MaxOptimizeIterations` /
`EjectionMaxDepth` qo'lda berilmasa shundan olinadi):

| Complexity | Restart | Backtrack | SA iteratsiya | Ejection chuqurligi |
|---|---|---|---|---|
| `Small` | 1 | 2 000 | 20 000 | 2 |
| `Normal` | 4 | 20 000 | 200 000 | 4 |
| `Large` | 16 | 200 000 | 2 000 000 | 6 |
| `Huge` | 48 | 2 000 000 | 20 000 000 | 10 |

**Determinizm.** Tasodifiylik manbai — `Util/Xoshiro256SS.cs`. Har restart uchun
`Seed * 1000003 + r`, optimizatsiya uchun `Seed * 7919 + 13` urug'i ishlatiladi.
`Scheduler` hujjatida yozilganidek: bir xil `Seed` → **bayt-bayt bir xil natija**,
lekin **faqat `TimeLimit` berilmagan va bekor qilinmagan bo'lsa**. Vaqt chegarasi
qo'yilishi bilan natija mashina tezligiga bog'liq bo'lib qoladi.
Test: `tests/DarsJadvali.Scheduling.Tests/DeterminismTests.cs`.

**Anytime bekor qilish.** `Scheduler.Generate(..., CancellationToken)` — bekor
qilinganda ham **eng yaxshi topilgan yechim qaytadi**, istisno tashlanmaydi;
natijada `Cancelled = true` bo'ladi. Test: `CancellationTests.cs`.

Tekshirish nuqtalari: restart tsiklining boshi, `Constructor` ning har iteratsiyasi,
`EjectionChainRepair` ning har rekursiya kirishi, `Optimizer` da **har 1024
iteratsiyada** (`it & 0x3FF`). `Verifier`, `Propagator`, `RoomAssigner` va `Relaxer`
da tekshiruv **yo'q** — ular qisqa fazalar, lekin juda katta masalada bekor qilish
javobi shuncha kechikadi. `TimeLimit` ham faqat restart tsikli va `Optimizer` da
tekshiriladi, ya'ni bitta uzun `Construct` o'tishi chegaradan **oshib ketishi mumkin**.

**Progress.** `IProgress<GenerationProgress>` — faza, iteratsiya, joylashgan/jami karta,
joriy va eng yaxshi soft jarima, joylashmagan karta, sarflangan vaqt. Har faza
almashuvida bitta hisobot, `Optimizer` da esa `ProgressInterval` bilan cheklangan.

---

## 5. Application qatlami bilan bog'lanish

```
EF entity'lari
   │  ISchedulingStore.LoadAsync            (Infrastructure/Persistence/Scheduling)
   ▼
SchedulingInput
   │  ISchedulingMapper.BuildProblemAsync   (Application/Scheduling/SchedulingMapper.cs)
   ▼
Problem  ──►  Scheduler.Generate  ──►  Solution
   │
   │  SchedulingIdMap (yadro indeksi ↔ DB Id)
   ▼
Card + CardOccurrence                       (bitta tranzaksiyada)
```

Kirish nuqtasi — `IScheduleGenerationService.GenerateAsync`
(`Application/Scheduling/ScheduleGenerationService.cs`). Butun amal
`IUnitOfWork.ExecuteInTransactionAsync` ichida: xato yoki bekor qilinishda
**eski jadval joyida qoladi**.

> **Nom to'qnashuvi.** Yadrodagi `Card` — joylashtirilishi kerak bo'lgan **bo'lak**
> (joylashmagan bo'lishi mumkin). Bazadagi `Card` — **joylashtirilgan** yozuv.
> Farqni `SchedulingIdMap` yopadi.

---

## 6. Ishlash ko'rsatkichlari

Manba: `tests/DarsJadvali.Scheduling.Tests/BenchmarkTests.cs` +
`TestProblems.LargeSchool()`.

Stsenariy: **30 sinf × 150 guruh × 1170 karta**, 5 kun × 10 dars = 50 slot,
12 ta fan, har fanga 8 tadan o'qituvchi, uchta fan guruhlarga bo'lingan
(`1170 = 30 × (25 butun sinf + 14 guruh darsi)`).

Test **aynan shularni tasdiqlaydi**:

| Tekshiruv | Qiymat |
|---|---|
| Hard buzilishlar | **`= 0`** |
| Joylashuv | **`≥ 90%`** |
| Vaqt byudjeti (odatiy test) | `Complexity.Normal`, `TimeLimit = 4 s` |
| Vaqt byudjeti (to'liq o'lchov) | `DJ_BENCH=1` → `Complexity.Large`, `TimeLimit = 90 s` |
| Seed | `20240814` |

> **Halollik izohi.** Test **"100% joylashuv" va "0 soft jarima" ni talab QILMAYDI** —
> u `≥ 90%` joylashuv va `0` hard buzilishni tekshiradi. Soft jarima o'lchanadi va
> jurnalga chiqariladi, lekin unga chegara qo'yilmagan. Amaldagi natija mashina
> tezligiga va byudjetga bog'liq.

To'liq o'lchovni ko'rish:

```bash
DJ_BENCH=1 dotnet test --filter Category=Benchmark
```

---

## 7. Ma'lum cheklovlar

Quyidagilar **amalga oshirilmagan** — hujjat ularni "ishlaydi" deb ko'rsatmaydi:

1. **Tushlik oynasi (lunch break)** — yadroda bunday cheklov ham, `LessonDef`/`ClassDef`
   da mos maydon ham yo'q (`C-LUN-*` oilasi umuman qurilmagan).
2. **Binolar va binolararo ko'chish** — `RoomDef` da bino tushunchasi yo'q
   (`Model/Definitions.cs` da faqat `Capacity` va `ParallelLessons`), shuning uchun
   "binolar orasida yurish" jarimasi ham yo'q (`C-BLD-*`).
3. **Kartalararo munosabatlar** (bir dars boshqasidan keyin/oldin, bir vaqtda,
   zanjirli darslar, seminarlar) — `C-REL-*` oilasi qurilmagan; `Card` da bunday
   maydon yo'q.
4. **O'quvchi darajasidagi cheklovlar** (`C-STU-*`) — o'quvchilar faqat `StudentCount`
   soni sifatida modellashtirilgan.
5. **`TimeOff.Penalty` yadroga to'liq uzatilmaydi.** Yadroda "?" holati bitta bitmask
   (`Availability.Questioned`) va **qat'iy og'irlik** (`C-AVL-06`, `w = 100`) — qator
   bo'yicha turli jarimani ifodalab bo'lmaydi. Shu sababli
   `Application/Scheduling/SchedulingMapper.cs` `Penalty` ni faqat **daraja tanlashda**
   ishlatadi: `Penalty ≥ TimeOff.HardThreshold (1000)` → taqiqqa ko'tariladi, qolgan
   barcha musbat qiymatlar bitta "?" og'irligiga tushadi. Mapper buni foydalanuvchiga
   izoh sifatida ham qaytaradi ("jarima og'irligi yagona darajaga tushirildi").
6. **`Importance` darajasi jarima kattaligiga ta'sir qilmaydi.** `IConstraint.Importance`
   (`Low/Normal/High/Strict`) e'lon qilingan, lekin `ConstraintBase.Evaluate`
   (`Constraints/ConstraintBase.cs:22-31`) faqat `Weight` ga ko'paytiradi — `Importance`
   ning sonli qiymati hisobga umuman kirmaydi. U bitta joyda ishlatiladi:
   `IsHard => Importance == Importance.Strict` (`ConstraintBase.cs:16`), va `IsHard`
   bo'lgan cheklovni `PenaltyEvaluator` soft hisobdan **butunlay chiqarib tashlaydi**
   (`Evaluation/PenaltyEvaluator.cs:48, 60, 96`). Ya'ni soft cheklovni `Strict` qilish
   uni hard **qilmaydi** — shunchaki jarimasiz qoldiradi. Standart to'plamda
   (`ConstraintSet.CreateDefault()`) bironta ham `Strict` yo'q, shuning uchun bu tuzoq
   hozircha ishga tushmaydi; hard qoidalar `IConstraint` orqali emas,
   `Constraints/HardRules.cs` da alohida tekshiriladi.
7. **`IConstraint.AllowRelaxation` hech qayerda o'qilmaydi.** Xususiyat e'lon qilingan
   (`Constraints/IConstraint.cs:78`, standart `true`), lekin butun `Scheduling`
   loyihasida uni o'qiydigan joy yo'q — "yozib qo'yiladigan, ishlatilmaydigan" maydon.
   `Relaxer` faqat tavsiya beradi; hech bir cheklov avtomatik o'chirilmaydi va
   generatsiya qayta ishga tushmaydi.
8. **Sig'im chegarasi:** `SlotMask.Capacity = 512`, `Periods ≤ 64`. Ya'ni
   `Weeks × DaysPerWeek × Periods > 512` bo'lgan maktab qo'llab-quvvatlanmaydi —
   `TimeGrid` konstruktori istisno tashlaydi.
9. **A/B hafta cheklovlari yo'q.** `TimeGrid.Weeks > 1` texnik jihatdan ishlaydi,
   lekin toq/juft haftani farqlaydigan cheklov (`C-CYC-02`, `C-CYC-04..07`) yozilmagan.
10. **Parallellik yo'q** — `Scheduler.Generate` bitta oqimda ishlaydi
    (`GenerationOptions` da `Parallelism` sozlamasi mavjud emas).

---

Tegishli hujjatlar:
[`ARXITEKTURA.md`](ARXITEKTURA.md) · [`CONTRACT.md`](CONTRACT.md) ·
[`MIGRATSIYA.md`](MIGRATSIYA.md) · [`FOYDALANISH.md`](FOYDALANISH.md)
