# Foydalanish qo'llanmasi

Bu qo'llanma **Dars Jadvali Tuzuvchi** dasturidan birinchi marta foydalanayotganlar uchun.
Kompyuter bilimi talab qilinmaydi — bosqichlarni tartib bilan bajaring.

> **Eng muhim qoida: tartibni buzmang.**
> Har bir bosqich oldingisiga tayanadi. Masalan, biriktirma qo'shish uchun
> avval o'qituvchi, fan va sinf kiritilgan bo'lishi shart.

```
1. Fanlar  →  2. Sinflar  →  3. O'qituvchilar  →  4. Biriktirmalar
                                                        │
5. Hafta kunlari  →  6. O'qituvchi vaqti  →  7. Jadval tuzish  →  8. Chop etish
```

Asosiy ish 7-bosqichda — **Bosh sahifa** dagi jadval taxtasida bo'ladi: u yerda
avtomatik tuzish, kartalarni ko'chirish, bekor qilish va qulflash bir joyda.

---

## 0. Dasturni ishga tushirish

Dastur ochilganda ma'lumotlar bazasi avtomatik yaratiladi va
hafta kunlari (Dushanba–Shanba) hamda 7 ta dars soati bilan to'ldiriladi.

Chap tomonda menyu, o'ngda tanlangan bo'lim ko'rinadi.

Barcha ma'lumot kompyuteringizda saqlanadi — jadval tuzish uchun internet kerak emas:

| Tizim | Baza fayli |
|-------|-----------|
| Windows | `%LOCALAPPDATA%\DarsJadvali\darsjadvali.db` |
| macOS | `~/Library/Application Support/DarsJadvali/darsjadvali.db` |
| Linux | `~/.local/share/DarsJadvali/darsjadvali.db` |

Dastur sxemani yangilashdan oldin **avtomatik zaxira nusxa** oladi — u shu papkadagi
`backups/` ichiga `darsjadvali-YYYYMMDD-HHMMSS.db` nomi bilan tushadi. Oxirgi **10 tasi**
saqlanadi, eskilari o'zi o'chib boradi.

---

## 1-bosqich. Fanlar

**Menyu: Fanlar**

Maktabda o'qitiladigan barcha fanlarni kiriting.

| Maydon | Izoh |
|--------|------|
| **Nomi** | To'liq nom: `Matematika`, `Ona tili`, `Fizika` |
| **Kodi** | Qisqartma: `MAT`, `ONA`, `FIZ` — jadvalda joy tejaydi |
| **Rangi** | Jadvalda shu fan qaysi rangda ko'rinishi |

**Nima muhim:**
- **Kod takrorlanmasligi kerak** — har bir fanga o'ziga xos qisqartma bering.
- **Rang o'zi tanlanadi:** "Yangi fan" tugmasini bosganingizda dastur paletkadan hali
  ishlatilmagan rangni tanlab qo'yadi, ya'ni fanlar bir-biridan o'zidan farq qiladi.
  Xohlasangiz ro'yxatdan boshqa rangni qo'lda tanlashingiz mumkin.
- Fanni keyinroq o'chirsangiz, unga bog'liq biriktirmalar va darslar ham o'chadi.

---

## 2-bosqich. Sinflar

**Menyu: Sinflar**

| Maydon | Izoh |
|--------|------|
| **Nomi** | `5-A`, `9-B`, `11-V` |
| **Asosiy xona** | Sinf doimiy o'tiradigan xona raqami, masalan `203` |
| **O'quvchilar soni** | Ma'lumot uchun |

**Nima muhim:**
- **Sinf nomi takrorlanmasligi kerak.**
- **Asosiy xona** ni to'ldirish tavsiya etiladi: dastur bir vaqtda bitta xonaga
  ikki sinfni qo'yib yubormaydi (`ROOM_BUSY` xatosi). Xona kiritilmasa,
  xona bo'yicha tekshiruv o'tkazilmaydi.

---

## 3-bosqich. O'qituvchilar

**Menyu: O'qituvchilar**

| Maydon | Izoh |
|--------|------|
| **F.I.Sh.** | To'liq ism-sharif |
| **Telefon** | Majburiy emas |
| **Rangi** | O'qituvchi jadvalda qaysi rangda ko'rinadi |
| **Faol** | Belgilangan bo'lsa — ishlayapti |

**Nima muhim:**
- **Rang o'zi tanlanadi:** "Yangi o'qituvchi" tugmasi bosilganda paletkadan hali
  ishlatilmagan rang qo'yiladi; kerak bo'lsa qo'lda o'zgartiring.
- Ishdan ketgan yoki ta'tildagi o'qituvchini **o'chirmang** — **"Faol" belgisini oching**.
  Shunda uning tarixi va biriktirmalari saqlanib qoladi, lekin unga yangi dars
  qo'yib bo'lmaydi (`TEACHER_INACTIVE` xatosi).
- O'qituvchini butunlay o'chirish uning **barcha darslari va biriktirmalarini** ham o'chiradi.

---

## 4-bosqich. Biriktirmalar

**Menyu: Biriktirmalar**

Bu eng muhim bosqich: **kim, qaysi fandan, qaysi sinfda, haftasiga necha soat** dars beradi.

| Maydon | Izoh |
|--------|------|
| **O'qituvchi** | Ro'yxatdan tanlanadi |
| **Fan** | Ro'yxatdan tanlanadi |
| **Sinf** | Ro'yxatdan tanlanadi |
| **Haftalik soat** | O'quv rejasidagi soat, masalan `5` |

**Nima muhim:**
- Biriktirmasiz dars qo'yib bo'lmaydi (`NO_ASSIGNMENT` xatosi).
  Dastur "bu o'qituvchi bu sinfda bu fandan dars bermaydi" deb ogohlantiradi.
- **Haftalik soat** — avtomatik generatsiya uchun asosiy ko'rsatkich:
  generator aynan shuncha dars qo'yishga harakat qiladi.
- Bir xil uchlik (o'qituvchi + fan + sinf) **ikki marta** kiritilmaydi.
- Soat me'yoridan oshsa dastur **ogohlantiradi** (`WEEKLY_HOURS_EXCEEDED`),
  lekin taqiqlamaydi — kerak bo'lsa davom etishingiz mumkin.

**Tekshiruv:** har bir sinf uchun barcha fanlarning haftalik soatlari yig'indisi
kunlik dars soatlari x ish kunlari sonidan **oshmasligi** kerak.
Masalan 6 kun x 7 dars = haftasiga eng ko'pi bilan 42 soat.
Oshib ketsa, generator hammasini joylashtira olmaydi.

---

## 5-bosqich. Hafta kunlari

**Menyu: Hafta kunlari**

| Maydon | Izoh |
|--------|------|
| **Kun** | Dushanba … Yakshanba |
| **Ish kunimi** | Belgilangan bo'lsa — bu kuni dars bo'ladi |
| **Kuniga nechta dars** | Masalan `7` |

Standart holat: **Dushanba–Shanba — ish kuni**, **Yakshanba — dam olish kuni**.

**Nima muhim:**
- Ish kuni bo'lmagan kunga dars qo'yib bo'lmaydi (`DAY_INACTIVE` xatosi).
- **Kuniga nechta dars** — jadval jadvalidagi qatorlar soni.
  Undan katta raqamga dars qo'yib bo'lmaydi (`LESSON_OUT_OF_RANGE` xatosi).
- Kunlar har xil bo'lishi mumkin: masalan Dushanba–Juma 7 dars, Shanba 5 dars.

### Dars soatlari (vaqtlari)

Shu bo'limda har bir dars raqamining **aniq vaqti** ham sozlanadi:

```
1-dars   08:30 – 09:15
2-dars   09:25 – 10:10
3-dars   10:20 – 11:05
4-dars   11:15 – 12:00
5-dars   12:10 – 12:55
6-dars   13:05 – 13:50
7-dars   14:00 – 14:45
```

**Nima muhim:** bu vaqtlar keyingi bosqich — **o'qituvchi vaqti** tekshiruvi uchun ishlatiladi.
Maktabingizdagi haqiqiy qo'ng'iroq jadvaliga moslang.

---

## 6-bosqich. O'qituvchi vaqti

**Menyu: O'qituvchi vaqti**

Bu bosqich **majburiy emas**, lekin juda foydali: ba'zi o'qituvchilar faqat
ma'lum kunlarda yoki ma'lum soatlarda ishlaydi.

Bu yerda **vaqt yozilmaydi** — **dars soati raqamlari** belgilanadi.
Soatlarning aniq vaqti (08:30–09:15 va h.k.) 5-bosqichda sozlangan.

### Ekran qanday tuzilgan

- **Chapda** — o'qituvchilar ro'yxati. Kimni sozlamoqchi bo'lsangiz, shuni bosing.
- **O'ngda** — jadval: har bir **ish kuni** alohida qator, ustunlar esa
  **dars soatlari** (yuqorida raqami va vaqti yozilgan).

Har bir kun qatorida:

| Element | Izoh |
|---------|------|
| **Cheklov bor** | Belgilanmagan bo'lsa — o'sha kuni cheklov yo'q, o'qituvchi **barcha soatlarda** dars o'ta oladi |
| **Soat katakchalari** | "Cheklov bor" belgilangandagina faollashadi; belgilangan soatlarda **ishlaydi** |
| **Hammasi** | O'sha kunning barcha soatlarini belgilaydi |
| **Hech biri** | O'sha kunning barcha soat belgilarini olib tashlaydi |

Oxirida **Saqlash** ni bosing. **Bekor qilish** — saqlanmagan o'zgarishlarni qaytaradi.

### Qoida — eng ko'p chalkashadigan joy

- **"Cheklov bor" belgilanmagan kun** — o'qituvchi o'sha kuni **istalgan soatda**
  dars o'ta oladi. (Ekranda "cheklov yo'q — kun bo'yi ishlaydi" deb yozib turadi.)
- **"Cheklov bor" belgilangan kun** — o'qituvchi **FAQAT belgilangan soatlarda**
  dars o'ta oladi. Belgilanmagan soatlarga jadval tuzishda dars qo'yilmaydi.
- Cheklovga zid dars qo'ysangiz `TEACHER_UNAVAILABLE` xatosi chiqadi.

> "Cheklov bor" belgilangan, lekin **bironta ham soat tanlanmagan** kun —
> o'qituvchi o'sha kuni **umuman dars o'ta olmaydi** degani. Saqlashda dastur
> buni alohida so'rab tasdiqlatadi.

**Misollar:**

| Vazifa | Nima qilinadi |
|--------|---------------|
| "Dushanba faqat ertalab ishlaydi" | Dushanba: **Cheklov bor** → 1, 2, 3, 4-soatlarni belgilang |
| "Seshanba kuni umuman kelmaydi" | Seshanba: **Cheklov bor** → **Hech biri** (bironta soat belgilanmaydi) |
| "Payshanba tushdan keyin bandman" | Payshanba: **Cheklov bor** → faqat tushgacha bo'lgan soatlarni belgilang |
| "Cheklov yo'q" | "Cheklov bor" ni belgilamang |

---

## 7-bosqich. Jadval tuzish

> **Avval buni o'qing — dasturda jadval bilan ishlaydigan IKKITA ekran bor.**
>
> | Ekran | Qayerda | Nima uchun |
> |---|---|---|
> | **Jadval taxtasi** | **Bosh sahifa** ning pastki qismida | **Asosiy ish joyi.** Kartani ko'chirish, bekor qilish (undo), qulflash, kattalashtirish, joylashtirilmagan darslar paneli, avtomatik tuzish — hammasi shu yerda |
> | **Dars jadvali** | Chap menyudagi **"Dars jadvali"** | Eskiroq, soddaroq ekran: bitta sinf yoki o'qituvchi jadvalini ko'rish va katak-katak to'ldirish. **Bu ekranda bekor qilish (undo) ishlamaydi** |
>
> Quyidagi 7.1–7.5 bo'limlari **Bosh sahifadagi jadval taxtasi** haqida.

### 7.1 Kartani ko'chirish — "kartani qo'lga olish"

Karta — jadvaldagi bitta dars. Ko'chirish **sichqonchani bosib turib sudrash emas**,
balki **ikki marta bosish** bilan bo'ladi:

1. **Kartani bosing** — u "qo'lingizga" oladi va kursor ortidan yuradi.
2. **Kerakli katakni bosing** — karta o'sha yerga tushadi.

Karta qo'lda turganda jadval kataklari **rangga kiradi** — bu qayerga qo'yish
mumkinligini ko'rsatadi:

| Rang | Ma'nosi |
|---|---|
| **Yashil** | Yaxshi joy — hech qanday muammo yo'q |
| **Ko'k** | Qo'ysa bo'ladi, lekin ogohlantirish bor (fan shu kuni takrorlanadi, haftalik soat oshadi, oyna hosil bo'ladi) |
| **Kulrang** | **Bu yerga qo'yib bo'lmaydi** — sinf yoki o'qituvchi band, kun yopiq, karta qulflangan |

**Klaviatura yordamchilari:**

| Tugma | Nima qiladi |
|---|---|
| **SHIFT** (bosib turing) | Shu karta uchun **barcha mumkin bo'lgan joylarni** yoritib ko'rsatadi |
| **CTRL** (bosib kartani oling) | Bir katakdagi **bir nechta bog'liq kartani birga** oladi (juft dars, guruhlarga bo'lingan dars). Ulardan bittasi qulflangan bo'lsa — birga olish ishlamaydi |
| **ESC** | Qo'ldagi kartani qo'yib yuboradi, hech narsa o'zgarmaydi |

Kartani jadvaldan **olib qo'yish** uchun: karta ustida **o'ng tugma** →
**"Panelga olib qo'yish"**. Karta o'ngdagi "Joylashtirilmagan kartalar" paneliga qaytadi.

### 7.2 Bekor qilish va qaytarish (undo / redo)

| Tugma | Nima qiladi |
|---|---|
| **Ctrl + Z** | Oxirgi amalni bekor qiladi |
| **Ctrl + Y** (yoki **Ctrl + Shift + Z**) | Bekor qilinganni qaytaradi |

Tarix **100 qadam** saqlanadi — ekranda "12 / 100 qadam" ko'rinishida yozib turadi.
Bekor qilish ishlaydigan amallar: kartani ko'chirish, panelga olib qo'yish,
qulflash/qulfni ochish, CTRL bilan guruh ko'chirish (bu bitta qadam hisoblanadi).

> **Tarix qachon o'chadi:** siz jadvalni **qaytadan yuklaganingizda** — ya'ni
> Bosh sahifaga yangidan kirganingizda yoki **"Yangilash"** tugmasini bosganingizda.
> Kartani o'chirish tarixni o'chirmaydi.
>
> **Diqqat:** chap menyudagi eski **"Dars jadvali"** ekranida bekor qilish
> **umuman yo'q** — u yerda o'chirilgan dars Ctrl+Z bilan qaytmaydi.

### 7.3 Qulflash

Jadvalning bir qismini "qotirib qo'yish" kerak bo'lsa (masalan direktor tasdiqlagan
darslar), kartani **qulflang**:

- Karta ustida **o'ng tugma** → **"Qulflash"**. Qulfni ochish ham shu yerda.

Qulflangan karta:
- qo'lga olinmaydi va ko'chirilmaydi;
- **avtomatik tuzishda ham joyida qoladi** (agar "Qulflangan kartalar joyida qolsin"
  belgisi turgan bo'lsa — u standart holatda yoqilgan);
- qulf **bazaga yoziladi**, ya'ni dasturni yopib-ochsangiz ham saqlanib qoladi.

### 7.4 Ko'rinishni sozlash

| Imkoniyat | Qanday |
|---|---|
| **Kattalashtirish / kichraytirish** | **`+`** va **`−`** tugmalari yoki ekrandagi tugmalar. Oraliq: **50% dan 200% gacha**, 10% qadam bilan |
| **100% ga qaytarish** | **Ctrl + 0** |
| **Zichlik** | "Zich / Oddiy / Keng" — katak balandligini o'zgartiradi |
| **Ranglarni teskari qilish** | **`*`** tugmasi |
| **Smena tanlash** | Maktabda **ikki smena** bo'lsa yuqorida smena ro'yxati chiqadi va faqat o'sha smenaning sinflari hamda dars soatlari ko'rsatiladi. Bir smenali maktabda bu ro'yxat umuman ko'rinmaydi |

Jadval **virtualizatsiyalangan** — ekranda ko'rinmayotgan qatorlar chizilmaydi.
Shu sababli 30–40 sinfli maktabda ham silliq ishlaydi.

### 7.5 Joylashtirilmagan darslar paneli

O'ng tomonda **"Joylashtirilmagan kartalar"** paneli turadi. Unda o'quv rejasida
bor, lekin jadvalga hali tushmagan darslar ko'rinadi (haftalik soat to'lmagan darslar).

Joylashtirish: **panelda kartani bosing → jadvalda kerakli katakni bosing.**
Ekranda ham shu ko'rsatma yozib turadi.

Panel bo'shab qolsa — barcha darslar joylashgan, jadval tayyor.

### 7.6 Avtomatik tuzish

**Bosh sahifa** da **"Avtomatik tuzish"** bo'limi bor. U yerda quyidagilarni sozlaysiz:

| Sozlama | Ma'nosi |
|---|---|
| **Seed** (standart `12345`) | Tasodifiylik urug'i. **Bir xil seed + bir xil ma'lumot = bir xil jadval.** Boshqa variantni ko'rmoqchi bo'lsangiz raqamni o'zgartiring |
| **Qidiruv byudjeti** | `Kichik (tez)` · `Oddiy` · `Katta (sekinroq)` · `Juda katta (eng sekin)`. Qanchalik katta bo'lsa, dastur shuncha ko'p variantni sinab ko'radi va natija shuncha yaxshi bo'ladi — lekin uzoqroq ishlaydi |
| **Qulflangan kartalar joyida qolsin** | Standart: **yoqilgan**. Qulflangan darslar qimirlamaydi |
| **To'liq bo'lmasa ham saqlansin** | Standart: **yoqilgan**. Hamma dars joylashmasa ham, topilgan eng yaxshi jadval saqlanadi |

Jarayon davomida qaysi bosqichda ekani, necha karta joylashgani va jarima
ko'rsatkichi ko'rinib turadi. **"Bekor qilish"** tugmasi bilan istalgan paytda
to'xtatish mumkin — bunda **eski jadval joyida qoladi**, hech narsa buzilmaydi.

> **Diqqat:** avtomatik tuzish qulflanmagan barcha kartalarni o'chirib, qaytadan
> joylashtiradi. Qo'lda silliqlagan ishingizni saqlab qolish uchun **muhim
> darslarni oldindan qulflang** (7.3-bo'lim).

Natijada nechta dars joylashtirilgani va nechtasiga joy topilmagani yoziladi.

**Joy topilmasa nima qilish kerak:**

| Sabab | Yechim |
|-------|--------|
| Haftalik soatlar juda ko'p | Biriktirmalardagi soatlarni kamaytiring |
| Ish kunlari kam | Hafta kunlari bo'limida kun qo'shing yoki kunlik dars sonini oshiring |
| O'qituvchi vaqti juda tor | O'qituvchi vaqti cheklovlarini yumshating |
| Bitta o'qituvchida juda ko'p sinf | Yukni boshqa o'qituvchiga taqsimlang |
| Byudjet kichik | "Qidiruv byudjeti" ni `Katta` yoki `Juda katta` ga o'zgartiring |
| Aynan shu variant omadsiz | **Seed** raqamini o'zgartirib qayta urinib ko'ring |

**Maslahat:** avval avtomatik tuzing, keyin natijani ko'zdan kechirib qo'lda
silliqlang. Avtomatik tuzish barcha qat'iy qoidalarga rioya qiladi, lekin
"qulaylik" (og'ir fanlar ertalabga) bo'yicha inson qarori yaxshiroq bo'ladi.

---

## 8-bosqich. Chop etish va PDF qilib saqlash

Tayyor jadvalni chop etish yoki boshqalarga yuborish uchun PDF faylga saqlash mumkin.

**"PDF yuklab olish"** tugmasi ikki joyda bor:

| Qayerda | Nima saqlanadi |
|---------|----------------|
| **Dars jadvali** ekranida | Ekranda ochilgan jadval (tanlangan sinf yoki o'qituvchi bo'yicha) |
| **Bosh sahifa** da | Butun maktab jadvali — barcha sinflar bitta hujjatda |

### Chop etish dizaynlari

Chop etish **tayyor dizaynlar** asosida ishlaydi — sahifa o'lchami, yo'nalishi,
ranglari va sarlavhalari oldindan tayyorlangan. Hozir **4 ta dizayn** bor:

| Dizayn | Nima uchun |
|---|---|
| **Sinf jadvali — Ko'k** | Bitta sinfning haftalik jadvali, ko'k bezakli |
| **Sinf jadvali — Oq** | Xuddi shunday, lekin bezaksiz — qora-oq printer uchun tejamli |
| **O'qituvchi jadvali — Yashil** | Bitta o'qituvchining haftalik yuklamasi |
| **Maktab jamlanmasi** | Barcha sinflar bitta jamlanma varaqda |

Tugmani bosganingizda faylni qayerga saqlashni so'raydigan oyna ochiladi. Nom va
papkani tanlab **Saqlash** ni bosing.

Hujjatda o'zbek lotin harflari (`oʻ`, `gʻ`) to'g'ri chiqishi uchun kerakli shrift
dasturning o'ziga qo'shib yuborilgan — kompyuterga alohida shrift o'rnatish shart emas.

---

## Xato xabarlari lug'ati

| Xabar mazmuni | Nima qilish kerak |
|---------------|-------------------|
| **Bu kun ish kuni emas** | Hafta kunlari bo'limida kunni faol qiling |
| **Dars raqami noto'g'ri** | Kuniga dars sonini oshiring yoki boshqa katakni tanlang |
| **O'qituvchi faol emas** | O'qituvchilar bo'limida "Faol" belgisini qo'ying |
| **Biriktirma yo'q** | Biriktirmalar bo'limida o'qituvchi–fan–sinf uchligini qo'shing |
| **O'qituvchi band** | Boshqa soat tanlang yoki boshqa o'qituvchini qo'ying |
| **Sinfda dars bor** | Bu sinf uchun shu soat allaqachon band |
| **Xona band** | Boshqa xona yoki boshqa soat tanlang |
| **O'qituvchi bu vaqtda ishlamaydi** | O'qituvchi vaqti bo'limidagi cheklovni tekshiring |
| **Haftalik soatdan oshdi** (sariq) | Ogohlantirish — xohlasangiz davom etishingiz mumkin |
| **Bu fan shu kuni bor** (sariq) | Ogohlantirish — bir kunda ikki marta bo'lishi mumkin |

---

## Ma'lumotlarni zaxiralash

Barcha ma'lumot **bitta faylda**:

| Tizim | Yo'l |
|---|---|
| Windows | `%LOCALAPPDATA%\DarsJadvali\darsjadvali.db` |
| macOS | `~/Library/Application Support/DarsJadvali/darsjadvali.db` |
| Linux | `~/.local/share/DarsJadvali/darsjadvali.db` |

- **Avtomatik zaxira:** dastur sxemani yangilashdan oldin o'zi zaxira oladi —
  o'sha papkadagi `backups/` ichida `darsjadvali-YYYYMMDD-HHMMSS.db`. Oxirgi 10 tasi saqlanadi.
- **Qo'lda zaxira:** **dasturni yoping** va `darsjadvali.db` faylini ko'chiring.
- **Tiklash:** zaxira faylni `darsjadvali.db` nomi bilan o'sha joyga qaytaring
  (dastur yopiq holatda).
- **Hammasini tozalash:** faylni o'chiring — dastur keyingi ochilishida yangi baza yaratadi.

> **Muhim:** dastur ishlayotganda baza yonida `darsjadvali.db-wal` va `darsjadvali.db-shm`
> yordamchi fayllari paydo bo'ladi. **Nusxa olishdan oldin dasturni yoping** — aks holda
> ko'chirilgan fayl to'liq bo'lmasligi mumkin.

---

## Ma'lum cheklovlar

Quyidagilar **hozircha ishlamaydi** — dastur ularni va'da qilmaydi:

| Nima | Hozirgi holat |
|---|---|
| **Tushlik oynasi** | "Har sinfda 4–5-dars orasida tushlik bo'lsin" degan qoidani qo'yib bo'lmaydi. Vaqtincha yechim: tushlik soatini "ish kuni emas" qilib ajratish |
| **Bir nechta bino** | Dasturda faqat xonalar bor, **bino** tushunchasi yo'q. Shu sababli "o'qituvchi ikki bino orasida yugurmasin" degan qoida ham yo'q |
| **Darslar orasidagi bog'liqlik** | "Bu dars ana undan keyin bo'lsin", "bu ikkisi bir vaqtda bo'lsin" kabi qoidalar qo'yilmaydi |
| **Toq/juft hafta (A/B hafta)** | Ikki haftalik tsikl texnik jihatdan bor, lekin "toq haftada shu, juft haftada bu" degan qoida yozilmagan |
| **O'quvchi darajasidagi jadval** | O'quvchilar faqat **soni** sifatida hisobga olinadi; har bir o'quvchining alohida jadvali yo'q |
| **Ikkinchi smenaga taqsimlash** | Ikki smena **ko'rsatiladi va filtrlanadi**, lekin eski bazadan ko'chirishda barcha dars soatlari 1-smenaga tushadi. Ularni smenalarga bo'lish uchun ekran hali yo'q |
| **"Dars jadvali" ekranida bekor qilish** | Chap menyudagi eski ekranda **Ctrl+Z yo'q**. Bekor qilish kerak bo'lsa Bosh sahifadagi taxtadan foydalaning |
| **Sichqoncha bilan sudrash** | Karta **sudrab ko'chirilmaydi** — "bosing → bosing" usuli ishlatiladi (7.1-bo'lim). Bu ataylab shunday qilingan |

**O'qituvchi vaqti haqida:** "tavsiya etilmaydi" darajasidagi cheklovga qanday jarima
qo'ysangiz ham, avtomatik tuzish ularning **hammasini bir xil og'irlikda** hisoblaydi —
"biroz noqulay" bilan "juda noqulay" farqlanmaydi. Faqat eng yuqori jarima (1000)
to'liq **taqiq** sifatida qabul qilinadi. Generatsiya oxirida bu haqda xabar chiqadi.

---

## Yordam

Savol, taklif yoki xatolik haqida xabar:

**Abduxalil Voxidjonov** — Telegram: [@abduxalilvoxidjonov](https://t.me/abduxalilvoxidjonov)

Loyihani qo'llab-quvvatlash (Humo): **`9860 3501 4679 1495`**
