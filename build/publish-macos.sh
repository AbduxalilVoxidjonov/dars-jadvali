#!/usr/bin/env bash
# =============================================================================
#  Dars Jadvali Tuzuvchi — macOS uchun dastur yig'ish
# =============================================================================
#  Nima qiladi:
#    1. `src/DarsJadvali.Desktop` (Avalonia) loyihasini self-contained qilib
#       yig'adi (osx-arm64 va/yoki osx-x64).
#    2. Natijadan qo'lda to'liq `DarsJadvali.app` bundle yasaydi.
#    3. Ad-hoc (bepul, sertifikatsiz) imzo qo'yadi — "damaged app" xatosining
#       oldini oladi.
#    4. Har bir arxitektura uchun DMG obrazi yasaydi.
#
#  Ishlatish:
#    bash build/publish-macos.sh
#    bash build/publish-macos.sh --arch arm64
#    bash build/publish-macos.sh --arch both --output /tmp/chiqarish
#    bash build/publish-macos.sh --no-dmg
#
#  Parametrlar:
#    --arch arm64|x64|both   Qaysi arxitektura (standart: both)
#    --output <papka>        Natija papkasi (standart: <loyiha ildizi>/publish)
#    --dmg                   DMG yasalsin (standart — allaqachon yoqilgan)
#    --no-dmg                DMG yasalmasin (faqat .app)
#    -h | --help             Yordam
#
#  Talab: .NET 8 (yoki undan yuqori) SDK, macOS.
# =============================================================================

set -euo pipefail

# --- Ranglar (terminal qo'llab-quvvatlasa) -----------------------------------
if [[ -t 1 ]]; then
    C_RESET=$'\033[0m'; C_CYAN=$'\033[36m'; C_YELLOW=$'\033[33m'
    C_GREEN=$'\033[32m'; C_RED=$'\033[31m';  C_GRAY=$'\033[90m'
else
    C_RESET=""; C_CYAN=""; C_YELLOW=""; C_GREEN=""; C_RED=""; C_GRAY=""
fi

xabar()      { printf '%s\n' "$*"; }
qadam()      { printf '%s>> %s%s\n' "$C_YELLOW" "$*" "$C_RESET"; }
muvaffaq()   { printf '%s   %s%s\n' "$C_GREEN" "$*" "$C_RESET"; }
sarlavha()   { printf '%s%s%s\n' "$C_CYAN" "$*" "$C_RESET"; }
kulrang()    { printf '%s%s%s\n' "$C_GRAY" "$*" "$C_RESET"; }
xato()       { printf '%sXATO: %s%s\n' "$C_RED" "$*" "$C_RESET" >&2; exit 1; }

# --- Doimiylar ----------------------------------------------------------------
APP_NAME="DarsJadvali"                 # ishga tushuvchi fayl va .app nomi
APP_DISPLAY="Dars Jadvali Tuzuvchi"    # foydalanuvchi ko'radigan nom
VOLUME_NAME="Dars Jadvali Tuzuvchi"    # DMG diskining nomi

# --- Yo'llar ------------------------------------------------------------------
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
DESKTOP_PROJECT="$PROJECT_ROOT/src/DarsJadvali.Desktop"
PLIST_TEMPLATE="$SCRIPT_DIR/Info.plist.template"

# --- Parametrlarni o'qish -----------------------------------------------------
ARCH="both"
OUTPUT=""
MAKE_DMG=1

yordam() {
    sed -n '2,28p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --arch)
            [[ $# -ge 2 ]] || xato "--arch dan keyin qiymat kerak (arm64 | x64 | both)."
            ARCH="$2"; shift 2 ;;
        --arch=*)
            ARCH="${1#*=}"; shift ;;
        --output|-o)
            [[ $# -ge 2 ]] || xato "--output dan keyin papka yo'li kerak."
            OUTPUT="$2"; shift 2 ;;
        --output=*)
            OUTPUT="${1#*=}"; shift ;;
        --dmg)     MAKE_DMG=1; shift ;;
        --no-dmg)  MAKE_DMG=0; shift ;;
        -h|--help) yordam; exit 0 ;;
        *) xato "Noma'lum parametr: $1  (yordam uchun: --help)" ;;
    esac
done

case "$ARCH" in
    arm64|x64|both) ;;
    *) xato "--arch qiymati faqat arm64, x64 yoki both bo'lishi mumkin (berilgan: $ARCH)." ;;
esac

if [[ -z "$OUTPUT" ]]; then
    OUTPUT="$PROJECT_ROOT/publish"
fi
mkdir -p "$OUTPUT"
OUTPUT="$(cd -- "$OUTPUT" && pwd)"

# --- Muhitni tekshirish -------------------------------------------------------
if [[ "$(uname -s)" != "Darwin" ]]; then
    xato "Bu skript faqat macOS'da ishlaydi (.app bundle, codesign, hdiutil kerak)."
fi

command -v dotnet >/dev/null 2>&1 || xato ".NET SDK topilmadi. O'rnating: https://dotnet.microsoft.com/download/dotnet/8.0"
command -v codesign >/dev/null 2>&1 || xato "codesign topilmadi. Xcode Command Line Tools o'rnating: xcode-select --install"

[[ -d "$DESKTOP_PROJECT" ]] || xato "Avalonia loyihasi topilmadi: $DESKTOP_PROJECT"
[[ -f "$PLIST_TEMPLATE"  ]] || xato "Info.plist shabloni topilmadi: $PLIST_TEMPLATE"

if [[ "$MAKE_DMG" -eq 1 ]] && ! command -v hdiutil >/dev/null 2>&1; then
    xabar "${C_YELLOW}Ogohlantirish: hdiutil topilmadi — DMG yasash o'chirildi.${C_RESET}"
    MAKE_DMG=0
fi

SDK_VERSION="$(dotnet --version)"

# --- Versiya raqami (Directory.Build.props dan) -------------------------------
VERSION="1.0.0"
if [[ -f "$PROJECT_ROOT/Directory.Build.props" ]]; then
    parsed="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT_ROOT/Directory.Build.props" | head -1 || true)"
    if [[ -n "$parsed" ]]; then
        VERSION="$parsed"
    fi
fi

# --- Ikonka (ixtiyoriy) -------------------------------------------------------
# Agar quyidagi yo'llardan birida .icns fayl bo'lsa, u bundle ichiga qo'shiladi.
ICON_SOURCE=""
for kandidat in \
    "$SCRIPT_DIR/AppIcon.icns" \
    "$PROJECT_ROOT/assets/AppIcon.icns" \
    "$DESKTOP_PROJECT/Assets/AppIcon.icns" \
    "$DESKTOP_PROJECT/Assets/DarsJadvali.icns"
do
    if [[ -f "$kandidat" ]]; then
        ICON_SOURCE="$kandidat"
        break
    fi
done

# --- Sarlavha -----------------------------------------------------------------
sarlavha "========================================================="
sarlavha " $APP_DISPLAY — macOS uchun yig'ish"
sarlavha "========================================================="
xabar    " .NET SDK versiyasi : $SDK_VERSION"
xabar    " Loyiha             : $DESKTOP_PROJECT"
xabar    " Dastur versiyasi   : $VERSION"
xabar    " Natija papkasi     : $OUTPUT"
xabar    " Arxitektura        : $ARCH"
if [[ "$MAKE_DMG" -eq 1 ]]; then
    xabar " DMG yasalsinmi     : ha"
else
    xabar " DMG yasalsinmi     : yo'q"
fi
if [[ -n "$ICON_SOURCE" ]]; then
    xabar " Ikonka             : $ICON_SOURCE"
else
    xabar " Ikonka             : topilmadi (standart ikonka ishlatiladi)"
fi
xabar ""

# --- Yig'iladigan arxitekturalar ro'yxati -------------------------------------
if [[ "$ARCH" == "both" ]]; then
    ARCHS=(arm64 x64)
else
    ARCHS=("$ARCH")
fi

# Xulosa jadvali uchun to'planadigan qatorlar: "arch|app yo'li|app hajmi|dmg yo'li|dmg hajmi"
NATIJALAR=()

hajm() {
    # Papka yoki fayl hajmini odam o'qiydigan ko'rinishda qaytaradi
    du -sh "$1" 2>/dev/null | awk '{print $1}'
}

# =============================================================================
#  Asosiy sikl
# =============================================================================
for a in "${ARCHS[@]}"; do
    RID="osx-$a"
    STAGE="$OUTPUT/.stage-$RID"          # dotnet publish natijasi (vaqtinchalik)
    APP_DIR="$OUTPUT/$RID/$APP_NAME.app"

    kulrang "---------------------------------------------------------"
    qadam "[$RID] 1/6  Eski natijalar tozalanmoqda..."
    rm -rf "$STAGE" "$OUTPUT/$RID"
    mkdir -p "$STAGE" "$OUTPUT/$RID"

    qadam "[$RID] 2/6  dotnet publish (self-contained)..."
    # DIQQAT: PublishSingleFile Avalonia .app bundle uchun YOQILMAYDI —
    # bundle ichida keraksiz va native kutubxonalar bilan muammo tug'diradi.
    if ! dotnet publish "$DESKTOP_PROJECT" \
            -c Release \
            -r "$RID" \
            --self-contained true \
            -p:PublishSingleFile=false \
            -o "$STAGE"; then
        xato "[$RID] yig'ish muvaffaqiyatsiz tugadi. Yuqoridagi xabarlarni o'qing."
    fi

    if [[ ! -f "$STAGE/$APP_NAME" ]]; then
        xato "[$RID] ishga tushuvchi fayl topilmadi: $STAGE/$APP_NAME
Tekshiring: .csproj da <AssemblyName>$APP_NAME</AssemblyName> turibdimi?"
    fi
    muvaffaq "publish tayyor: $(hajm "$STAGE")"

    qadam "[$RID] 3/6  .app bundle yig'ilmoqda..."
    mkdir -p "$APP_DIR/Contents/MacOS"
    mkdir -p "$APP_DIR/Contents/Resources"

    # publish natijasini to'liq Contents/MacOS ichiga ko'chiramiz
    # (nuqta bilan boshlanadigan fayllar ham ko'chsin)
    ( shopt -s dotglob nullglob; cp -R "$STAGE"/* "$APP_DIR/Contents/MacOS/" )

    if [[ -n "$ICON_SOURCE" ]]; then
        cp "$ICON_SOURCE" "$APP_DIR/Contents/Resources/AppIcon.icns"
    fi
    muvaffaq "bundle tuzilmasi yaratildi"

    qadam "[$RID] 4/6  Info.plist yozilmoqda..."
    ICON_BLOCK_FILE="$STAGE.iconkey"
    if [[ -n "$ICON_SOURCE" ]]; then
        {
            printf '    <key>CFBundleIconFile</key>\n'
            printf '    <string>AppIcon</string>\n'
        } > "$ICON_BLOCK_FILE"
    else
        : > "$ICON_BLOCK_FILE"
    fi

    # __VERSION__ almashtiriladi, __ICON_KEY__ qatori ikonka bloki bilan (yoki
    # bo'shliq bilan) almashtiriladi.
    sed -e "s/__VERSION__/$VERSION/g" \
        -e "/__ICON_KEY__/r $ICON_BLOCK_FILE" \
        -e "/__ICON_KEY__/d" \
        "$PLIST_TEMPLATE" > "$APP_DIR/Contents/Info.plist"
    rm -f "$ICON_BLOCK_FILE"

    if command -v plutil >/dev/null 2>&1; then
        plutil -lint "$APP_DIR/Contents/Info.plist" >/dev/null \
            || xato "[$RID] Info.plist noto'g'ri tuzilgan (plutil -lint xato berdi)."
    fi
    printf 'APPL????' > "$APP_DIR/Contents/PkgInfo"
    muvaffaq "Info.plist tayyor (versiya $VERSION)"

    qadam "[$RID] 5/6  Ad-hoc imzo qo'yilmoqda..."
    chmod +x "$APP_DIR/Contents/MacOS/$APP_NAME"
    # Kengaytmasiz native kutubxonalar ham bajariluvchi bo'lishi kerak emas,
    # lekin .dylib larning ruxsatlari o'zgarmasin.
    xattr -cr "$APP_DIR" 2>/dev/null || true

    # `--deep` eskirgan deb hisoblanadi, lekin sertifikatsiz (ad-hoc) imzo uchun
    # hozircha eng ishonchli usul — bundle ichidagi barcha .dylib lar ham imzolanadi.
    if ! codesign --force --deep --sign - "$APP_DIR" 2>&1; then
        xato "[$RID] codesign muvaffaqiyatsiz tugadi."
    fi
    codesign --verify --deep --strict "$APP_DIR" 2>/dev/null \
        || xabar "${C_YELLOW}   Ogohlantirish: codesign --verify ogohlantirish berdi (ad-hoc imzo uchun normal).${C_RESET}"
    muvaffaq "ad-hoc imzo qo'yildi"

    APP_SIZE="$(hajm "$APP_DIR")"
    DMG_PATH="-"
    DMG_SIZE="-"

    if [[ "$MAKE_DMG" -eq 1 ]]; then
        qadam "[$RID] 6/6  DMG obrazi yasalmoqda..."
        DMG_PATH="$OUTPUT/$APP_NAME-$VERSION-macos-$a.dmg"
        rm -f "$DMG_PATH"
        # DIQQAT: -srcfolder ga to'g'ridan-to'g'ri .app berilsa, hdiutil bundle'ni
        # oddiy papka deb hisoblab, uning ICHIDAGI fayllarni (Contents/) DMG
        # ildiziga chiqarib yuboradi. Shuning uchun .app turgan PAPKANI beramiz —
        # o'sha papkada faqat .app bor, ya'ni DMG ildizida DarsJadvali.app ko'rinadi.
        if ! hdiutil create \
                -volname "$VOLUME_NAME" \
                -srcfolder "$OUTPUT/$RID" \
                -ov -format UDZO \
                "$DMG_PATH" >/dev/null; then
            xato "[$RID] DMG yasash muvaffaqiyatsiz tugadi."
        fi
        DMG_SIZE="$(hajm "$DMG_PATH")"
        muvaffaq "DMG tayyor: $DMG_PATH ($DMG_SIZE)"
    else
        kulrang "[$RID] 6/6  DMG o'tkazib yuborildi (--no-dmg)."
    fi

    qadam "[$RID] Vaqtinchalik fayllar tozalanmoqda..."
    rm -rf "$STAGE"

    NATIJALAR+=("$a|$APP_DIR|$APP_SIZE|$DMG_PATH|$DMG_SIZE")
done

# =============================================================================
#  Xulosa
# =============================================================================
xabar ""
sarlavha "========================================================="
sarlavha " YAKUN — nima yig'ildi"
sarlavha "========================================================="
printf ' %-8s  %-8s  %-8s  %s\n' "ARXIT." "APP" "DMG" "FAYL"
printf ' %-8s  %-8s  %-8s  %s\n' "------" "----" "----" "----"
for qator in "${NATIJALAR[@]}"; do
    IFS='|' read -r a app_yol app_hajm dmg_yol dmg_hajm <<< "$qator"
    if [[ "$dmg_yol" == "-" ]]; then
        printf ' %-8s  %-8s  %-8s  %s\n' "$a" "$app_hajm" "-" "$app_yol"
    else
        printf ' %-8s  %-8s  %-8s  %s\n' "$a" "$app_hajm" "$dmg_hajm" "$dmg_yol"
        printf ' %-8s  %-8s  %-8s  %s\n' "" "" "" "$app_yol"
    fi
done

xabar ""
sarlavha "---------------------------------------------------------"
sarlavha " QAYSI FAYL QAYSI MAC UCHUN"
sarlavha "---------------------------------------------------------"
xabar " arm64  ->  Apple Silicon (M1, M2, M3, M4) protsessorli Mac'lar"
xabar "            2020-yil oxiridan keyin chiqqan deyarli barcha Mac'lar"
xabar " x64    ->  Intel protsessorli Mac'lar (eski modellar)"
xabar ""
xabar " Foydalanuvchi qaysi Mac ekanini bilmasa:"
xabar "   Apple menyusi () -> \"About This Mac\" -> \"Chip\" yoki \"Processor\" qatori"
xabar "   \"Apple M...\" yozilgan bo'lsa -> arm64"
xabar "   \"Intel...\"  yozilgan bo'lsa -> x64"

xabar ""
sarlavha "---------------------------------------------------------"
sarlavha " MUHIM: FOYDALANUVCHIGA BERILADIGAN KO'RSATMA"
sarlavha "---------------------------------------------------------"
xabar "${C_YELLOW} Dastur Apple sertifikati bilan imzolanmagan (sertifikat pullik)."
xabar " Shuning uchun BIRINCHI marta ochishda quyidagicha qilish SHART:${C_RESET}"
xabar ""
xabar "   1. DMG faylni ikki marta bosib oching."
xabar "   2. Ichidagi \"$APP_NAME.app\" ni Applications (Dasturlar) papkasiga tashlang."
xabar "   3. Finder'da Applications papkasini oching."
xabar "   4. \"$APP_NAME\" ustiga ${C_GREEN}O'NG TUGMA${C_RESET} bosing -> ${C_GREEN}Open${C_RESET} (Ochish) ni tanlang."
xabar "   5. Chiqqan oynada yana ${C_GREEN}Open${C_RESET} tugmasini bosing."
xabar ""
xabar " Shundan keyin dastur odatdagidek ikki marta bosish bilan ochilaveradi."
xabar ""
xabar "${C_YELLOW} Oddiy ikki marta bosilsa \"unidentified developer\" / \"cannot be opened\""
xabar " xatosi chiqadi — bu NORMAL, dasturda nosozlik yo'q. Yuqoridagi"
xabar " o'ng tugma -> Open usuli aynan shuning uchun kerak.${C_RESET}"
xabar ""
xabar " Agar macOS baribir ochmasa:"
xabar "   System Settings -> Privacy & Security -> pastga tushing ->"
xabar "   \"$APP_NAME was blocked...\" yonidagi \"Open Anyway\" tugmasi."

xabar ""
sarlavha "---------------------------------------------------------"
xabar " Ma'lumotlar bazasi foydalanuvchi Mac'ida shu yerda yaratiladi:"
xabar "   ~/Library/Application Support/DarsJadvali/darsjadvali.db"
xabar ""
xabar " Apple Developer sertifikati olinsa, to'liq imzo va notarizatsiya uchun:"
xabar "   bash build/sign-macos.sh --help"
xabar ""
