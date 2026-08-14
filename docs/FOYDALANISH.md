# Foydalanish qo'llanmasi

Bu qo'llanma **Dars Jadvali Tuzuvchi** dasturidan birinchi marta foydalanayotganlar uchun.
Kompyuter bilimi talab qilinmaydi — bosqichlarni tartib bilan bajaring.

> **Eng muhim qoida: tartibni buzmang.**
> Har bir bosqich oldingisiga tayanadi. Masalan, biriktirma qo'shish uchun
> avval o'qituvchi, fan va sinf kiritilgan bo'lishi shart.

```
1. Fanlar  →  2. Sinflar  →  3. O'qituvchilar  →  4. Biriktirmalar
                                                        │
5. Hafta kunlari  →  6. O'qituvchi vaqti  →  7. Jadval tuzish  →  8. PDF qilib saqlash
```

---

## 0. Dasturni ishga tushirish

Dastur ochilganda ma'lumotlar bazasi avtomatik yaratiladi va
hafta kunlari (Dushanba–Shanba) hamda 7 ta dars soati bilan to'ldiriladi.

Chap tomonda menyu, o'ngda tanlangan bo'lim ko'rinadi.

Barcha ma'lumot kompyuteringizda saqlanadi (internet kerak emas):

| Tizim | Baza fayli |
|-------|-----------|
| Windows | `%LOCALAPPDATA%\DarsJadvali\darsjadvali.db` |
| macOS | `~/Library/Application Support/DarsJadvali/darsjadvali.db` |

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

**Menyu: Dars jadvali**

Ekranda katakli jadval: yuqorida **hafta kunlari**, chapda **dars raqamlari**.

Yuqorida ikkita ko'rish rejimi bor:

| Rejim | Nima ko'rsatadi |
|-------|-----------------|
| **Sinf bo'yicha** | Tanlangan sinfning butun haftalik jadvali |
| **O'qituvchi bo'yicha** | Tanlangan o'qituvchining butun haftalik yuklamasi |

Rejimni tanlagach, yonidagi ro'yxatdan kerakli sinfni yoki o'qituvchini tanlaysiz.

### 7.1 Qo'lda joylashtirish

1. Yuqoridan **sinfni** tanlang.
2. Bo'sh katakni sichqonchaning **chap tugmasi** bilan bosing (masalan Dushanba, 1-dars).
3. **Fan** va **o'qituvchi** ni tanlang (ro'yxatda faqat shu sinfga biriktirilganlar chiqadi).
4. Kerak bo'lsa **xona** raqamini o'zgartiring.
5. **Qo'yish** ni bosing.

Dastur darhol tekshiradi. Xato bo'lsa **qizil** xabar chiqadi va dars qo'yilmaydi.
Ogohlantirish bo'lsa **sariq** xabar chiqadi — bunda "Baribir qo'yish" tugmasi orqali
davom etishingiz mumkin.

**Darsni o'chirish:** ikki xil usul bor —

- katakni **o'ng tugma** bilan bosing, yoki
- katakni chap tugma bilan tanlab, **"Katakni bo'shatish"** tugmasini bosing.

**Darsni ko'chirish:** darsni eski katakdan o'chirib, yangi katakka qaytadan qo'ying.
Katakni sudrab ko'chirish hozircha ishlamaydi.

**Butun jadvalni o'chirish:** yuqoridagi **"Jadvalni tozalash"** tugmasi.

### 7.2 Avtomatik tuzish

1. **Bosh sahifa** ga o'ting va **"Avtomatik tuzish"** tugmasini bosing.
2. Tasdiq oynasi chiqadi: *"Jadval avtomatik tuziladi. Mavjud jadval o'chirilib,
   qaytadan tuziladi. Davom etilsinmi?"* — **Ha** ni bossangiz jarayon boshlanadi,
   **Yo'q** ni bossangiz hech narsa o'zgarmaydi.
3. Jarayon davomida uning qay darajada bajarilgani ko'rinib turadi; kerak bo'lsa
   **"Bekor qilish"** tugmasi bilan to'xtatish mumkin.

> **Diqqat:** avtomatik tuzish har doim mavjud jadvalni to'liq o'chirib, boshidan
> tuzadi. Qo'lda qo'yilgan darslarni saqlab qolgan holda tuzish imkoniyati hozircha yo'q.
> Shuning uchun jadvalni avval avtomatik tuzib, keyin qo'lda silliqlagan ma'qul.

Natijada nechta dars joylashtirilgani va nechtasiga joy topilmagani yoziladi.

**Joy topilmasa nima qilish kerak:**

| Sabab | Yechim |
|-------|--------|
| Haftalik soatlar juda ko'p | Biriktirmalardagi soatlarni kamaytiring |
| Ish kunlari kam | Hafta kunlari bo'limida kun qo'shing yoki kunlik dars sonini oshiring |
| O'qituvchi vaqti juda tor | O'qituvchi vaqti cheklovlarini yumshating |
| Bitta o'qituvchida juda ko'p sinf | Yukni boshqa o'qituvchiga taqsimlang |

**Maslahat:** avtomatik tuzishdan keyin jadvalni ko'zdan kechiring va kerakli
darslarni qo'lda o'chirib-qo'yib "silliqlang". Avtomatik generatsiya barcha
qoidalarga rioya qiladi, lekin "qulaylik" (masalan og'ir fanlar ertalabga)
bo'yicha inson qarori yaxshiroq bo'ladi.

---

## 8-bosqich. PDF qilib saqlash

Tayyor jadvalni chop etish yoki boshqalarga yuborish uchun PDF faylga saqlash mumkin.

**"PDF yuklab olish"** tugmasi ikki joyda bor:

| Qayerda | Nima saqlanadi |
|---------|----------------|
| **Dars jadvali** ekranida | Ekranda ochilgan jadval (tanlangan sinf yoki o'qituvchi bo'yicha) |
| **Bosh sahifa** da | Butun maktab jadvali — barcha sinflar bitta hujjatda |

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

```
%LOCALAPPDATA%\DarsJadvali\darsjadvali.db
```

- **Zaxira nusxa:** dasturni yoping va shu faylni flesh-xotiraga ko'chiring.
- **Tiklash:** zaxira faylni o'sha joyga qaytaring (dastur yopiq holatda).
- **Hammasini tozalash:** faylni o'chiring — dastur keyingi ochilishida yangi baza yaratadi.

---

## Yordam

Savol, taklif yoki xatolik haqida xabar:

**Abduxalil Voxidjonov** — Telegram: [@abduxalilvoxidjonov](https://t.me/abduxalilvoxidjonov)

Loyihani qo'llab-quvvatlash (Humo): **`9860 3501 4679 1495`**
