#!/usr/bin/env bash
# =============================================================================
#  Dars Jadvali Tuzuvchi — macOS uchun TO'LIQ IMZO va NOTARIZATSIYA
# =============================================================================
#  Bu skript KELAJAK uchun: Apple Developer Program obunasi (yiliga $99) va
#  "Developer ID Application" sertifikati bo'lgandagina ishlaydi.
#
#  Hozircha dastur `build/publish-macos.sh` tomonidan AD-HOC imzolanadi —
#  bu bepul, lekin foydalanuvchi birinchi ochishda o'ng tugma -> Open qilishi
#  kerak bo'ladi. Notarizatsiyadan keyin bu shart emas: dastur oddiy ikki
#  marta bosish bilan ochiladi.
#
#  Ishlatish:
#    bash build/sign-macos.sh \
#      --identity "Developer ID Application: Abduxalil Voxidjonov (TEAMID123)" \
#      --app publish/osx-arm64/DarsJadvali.app \
#      --apple-id pochta@example.com \
#      --team-id TEAMID123 \
#      --password "abcd-efgh-ijkl-mnop"
#
#  Parametrlar:
#    --identity <nom>    "Developer ID Application: ..." sertifikat nomi (MAJBURIY)
#    --app <yo'l>        Imzolanadigan .app bundle (MAJBURIY)
#    --apple-id <email>  Apple ID pochtasi (notarizatsiya uchun)
#    --team-id <id>      Apple Developer Team ID (10 belgili)
#    --password <parol>  App-specific parol (appleid.apple.com dan olinadi)
#    --no-notarize       Faqat imzolansin, notarizatsiya qilinmasin
#    -h | --help         Yordam
#
#  Agar --apple-id / --team-id / --password berilmasa, faqat imzolash bajariladi
#  va notarizatsiya qanday qilinishi haqida ko'rsatma chiqadi.
# =============================================================================

set -euo pipefail

if [[ -t 1 ]]; then
    C_RESET=$'\033[0m'; C_CYAN=$'\033[36m'; C_YELLOW=$'\033[33m'
    C_GREEN=$'\033[32m'; C_RED=$'\033[31m'
else
    C_RESET=""; C_CYAN=""; C_YELLOW=""; C_GREEN=""; C_RED=""
fi

xabar()    { printf '%s\n' "$*"; }
qadam()    { printf '%s>> %s%s\n' "$C_YELLOW" "$*" "$C_RESET"; }
muvaffaq() { printf '%s   %s%s\n' "$C_GREEN" "$*" "$C_RESET"; }
sarlavha() { printf '%s%s%s\n' "$C_CYAN" "$*" "$C_RESET"; }
xato()     { printf '%sXATO: %s%s\n' "$C_RED" "$*" "$C_RESET" >&2; exit 1; }

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"

IDENTITY=""
APP_PATH=""
APPLE_ID=""
TEAM_ID=""
PASSWORD=""
NOTARIZE=1

yordam() {
    sed -n '2,32p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --identity)   [[ $# -ge 2 ]] || xato "--identity dan keyin qiymat kerak.";  IDENTITY="$2"; shift 2 ;;
        --identity=*) IDENTITY="${1#*=}"; shift ;;
        --app)        [[ $# -ge 2 ]] || xato "--app dan keyin yo'l kerak.";         APP_PATH="$2"; shift 2 ;;
        --app=*)      APP_PATH="${1#*=}"; shift ;;
        --apple-id)   [[ $# -ge 2 ]] || xato "--apple-id dan keyin pochta kerak.";  APPLE_ID="$2"; shift 2 ;;
        --apple-id=*) APPLE_ID="${1#*=}"; shift ;;
        --team-id)    [[ $# -ge 2 ]] || xato "--team-id dan keyin qiymat kerak.";   TEAM_ID="$2"; shift 2 ;;
        --team-id=*)  TEAM_ID="${1#*=}"; shift ;;
        --password)   [[ $# -ge 2 ]] || xato "--password dan keyin parol kerak.";   PASSWORD="$2"; shift 2 ;;
        --password=*) PASSWORD="${1#*=}"; shift ;;
        --no-notarize) NOTARIZE=0; shift ;;
        -h|--help)    yordam; exit 0 ;;
        *) xato "Noma'lum parametr: $1  (yordam uchun: --help)" ;;
    esac
done

sarlavha "========================================================="
sarlavha " Dars Jadvali Tuzuvchi — macOS imzo va notarizatsiya"
sarlavha "========================================================="

# --- Sertifikat berilmagan bo'lsa: KO'RSATMA chiqarib, muvaffaqiyat bilan chiqamiz
if [[ -z "$IDENTITY" ]]; then
    xabar ""
    xabar "${C_YELLOW} Sertifikat (--identity) berilmadi — imzolash bajarilmadi.${C_RESET}"
    xabar " Bu XATO EMAS. Quyida nima kerakligi tushuntirilgan."
    xabar ""
    sarlavha "---------------------------------------------------------"
    sarlavha " HOZIRGI HOLAT"
    sarlavha "---------------------------------------------------------"
    xabar " Dastur \`build/publish-macos.sh\` da AD-HOC (sertifikatsiz) imzolanadi."
    xabar " Bu bepul va \"damaged app\" xatosini yo'qotadi, lekin foydalanuvchi"
    xabar " birinchi ochishda .app ga O'NG TUGMA -> Open -> Open qilishi kerak."
    xabar " Ko'pchilik maktab uchun shu yetarli."
    xabar ""
    sarlavha "---------------------------------------------------------"
    sarlavha " TO'LIQ IMZO UCHUN NIMA KERAK"
    sarlavha "---------------------------------------------------------"
    xabar " 1. Apple Developer Program obunasi — yiliga 99 AQSH dollari"
    xabar "    https://developer.apple.com/programs/"
    xabar ""
    xabar " 2. \"Developer ID Application\" sertifikati:"
    xabar "    Xcode -> Settings -> Accounts -> Manage Certificates -> +"
    xabar "    yoki developer.apple.com -> Certificates, Identifiers & Profiles"
    xabar ""
    xabar " 3. App-specific parol (notarizatsiya uchun):"
    xabar "    https://appleid.apple.com -> Sign-In and Security -> App-Specific Passwords"
    xabar ""
    xabar " 4. Team ID (10 belgili):"
    xabar "    https://developer.apple.com/account -> Membership details"
    xabar ""
    sarlavha "---------------------------------------------------------"
    sarlavha " SERTIFIKAT BO'LGANDA SHU BUYRUQNI ISHLATING"
    sarlavha "---------------------------------------------------------"
    xabar " Avval kompyuterdagi sertifikatlar ro'yxatini ko'ring:"
    xabar "   security find-identity -v -p codesigning"
    xabar ""
    xabar " So'ng:"
    xabar "   bash build/sign-macos.sh \\"
    xabar "     --identity \"Developer ID Application: Ism Familiya (TEAMID123)\" \\"
    xabar "     --app publish/osx-arm64/DarsJadvali.app \\"
    xabar "     --apple-id pochta@example.com \\"
    xabar "     --team-id TEAMID123 \\"
    xabar "     --password \"abcd-efgh-ijkl-mnop\""
    xabar ""
    xabar " Har bir arxitektura (osx-arm64 va osx-x64) uchun alohida bajariladi."
    xabar ""
    exit 0
fi

# --- Bu yerdan keyin sertifikat berilgan: haqiqiy imzolash --------------------
[[ "$(uname -s)" == "Darwin" ]] || xato "Bu skript faqat macOS'da ishlaydi."

if [[ -z "$APP_PATH" ]]; then
    xato "--app berilmadi. Imzolanadigan .app bundle yo'lini ko'rsating.
Masalan: --app publish/osx-arm64/DarsJadvali.app"
fi

# Nisbiy yo'lni loyiha ildiziga nisbatan ham qidiramiz
if [[ ! -d "$APP_PATH" && -d "$PROJECT_ROOT/$APP_PATH" ]]; then
    APP_PATH="$PROJECT_ROOT/$APP_PATH"
fi
[[ -d "$APP_PATH" ]] || xato ".app bundle topilmadi: $APP_PATH"
[[ "$APP_PATH" == *.app ]] || xato "Berilgan yo'l .app bilan tugashi kerak: $APP_PATH"

APP_PATH="$(cd -- "$APP_PATH" && pwd)"
APP_NAME="$(basename "$APP_PATH" .app)"
APP_PARENT="$(dirname "$APP_PATH")"

command -v codesign >/dev/null 2>&1 || xato "codesign topilmadi. xcode-select --install ni bajaring."
command -v xcrun    >/dev/null 2>&1 || xato "xcrun topilmadi. Xcode Command Line Tools o'rnating."

xabar " Sertifikat : $IDENTITY"
xabar " Bundle     : $APP_PATH"
xabar ""

# --- Sertifikat haqiqatan mavjudmi? ------------------------------------------
qadam "1/5  Sertifikat tekshirilmoqda..."
if ! security find-identity -v -p codesigning 2>/dev/null | grep -qF "$IDENTITY"; then
    xato "Bu sertifikat kompyuterda topilmadi: $IDENTITY
Mavjud sertifikatlar ro'yxati:
$(security find-identity -v -p codesigning 2>/dev/null || echo '  (ro'"'"'yxat bo'"'"'sh)')"
fi
muvaffaq "sertifikat topildi"

# --- Imzolash ----------------------------------------------------------------
qadam "2/5  Bundle imzolanmoqda (hardened runtime + timestamp)..."
xattr -cr "$APP_PATH" 2>/dev/null || true

# Avval ichkaridagi barcha native kutubxonalarni, keyin bundle'ning o'zini
# imzolaymiz (notarizatsiya uchun bu tartib MAJBURIY).
while IFS= read -r -d '' lib; do
    codesign --force --options runtime --timestamp --sign "$IDENTITY" "$lib"
done < <(find "$APP_PATH/Contents/MacOS" \( -name '*.dylib' -o -name '*.so' \) -type f -print0)

codesign --force --options runtime --timestamp \
    --sign "$IDENTITY" \
    "$APP_PATH"
muvaffaq "imzolandi"

qadam "3/5  Imzo tekshirilmoqda..."
codesign --verify --deep --strict --verbose=2 "$APP_PATH"
muvaffaq "imzo to'g'ri"

# --- Notarizatsiya ------------------------------------------------------------
if [[ "$NOTARIZE" -eq 0 ]]; then
    xabar ""
    muvaffaq "TAYYOR (faqat imzolash — --no-notarize berilgan)."
    xabar " Notarizatsiyasiz Gatekeeper baribir ogohlantirish beradi."
    exit 0
fi

if [[ -z "$APPLE_ID" || -z "$TEAM_ID" || -z "$PASSWORD" ]]; then
    xabar ""
    xabar "${C_YELLOW} Notarizatsiya o'tkazib yuborildi: --apple-id, --team-id va --password"
    xabar " uchalasi ham berilishi kerak.${C_RESET}"
    xabar ""
    xabar " Qo'lda bajarish uchun:"
    xabar "   ditto -c -k --keepParent \"$APP_PATH\" \"/tmp/$APP_NAME.zip\""
    xabar "   xcrun notarytool submit \"/tmp/$APP_NAME.zip\" \\"
    xabar "       --apple-id <pochta> --team-id <TEAMID> --password <app-parol> --wait"
    xabar "   xcrun stapler staple \"$APP_PATH\""
    exit 0
fi

ZIP_PATH="$APP_PARENT/$APP_NAME-notarize.zip"

qadam "4/5  Notarizatsiyaga yuborilmoqda (bir necha daqiqa vaqt oladi)..."
rm -f "$ZIP_PATH"
# DIQQAT: notarizatsiya uchun ZIP `ditto --keepParent` bilan yasalishi SHART —
# oddiy `zip` imzo metama'lumotlarini buzadi.
ditto -c -k --keepParent "$APP_PATH" "$ZIP_PATH"

if ! xcrun notarytool submit "$ZIP_PATH" \
        --apple-id "$APPLE_ID" \
        --team-id "$TEAM_ID" \
        --password "$PASSWORD" \
        --wait; then
    xabar ""
    xabar "${C_RED} Notarizatsiya muvaffaqiyatsiz tugadi.${C_RESET}"
    xabar " Batafsil sabab uchun (yuqoridagi id ni qo'ying):"
    xabar "   xcrun notarytool log <submission-id> --apple-id $APPLE_ID --team-id $TEAM_ID --password <parol>"
    rm -f "$ZIP_PATH"
    exit 1
fi
rm -f "$ZIP_PATH"
muvaffaq "notarizatsiyadan o'tdi"

qadam "5/5  Tiket bundle'ga yopishtirilmoqda (stapler)..."
xcrun stapler staple "$APP_PATH"
xcrun stapler validate "$APP_PATH"
muvaffaq "tiket yopishtirildi"

xabar ""
sarlavha "========================================================="
sarlavha " TAYYOR"
sarlavha "========================================================="
xabar " $APP_PATH"
xabar ""
xabar " Endi foydalanuvchi dasturni oddiy IKKI MARTA BOSISH bilan ochadi —"
xabar " o'ng tugma -> Open hiylasi kerak emas, ogohlantirish chiqmaydi."
xabar ""
xabar " Gatekeeper qarorini shu buyruq bilan tekshirib ko'rish mumkin:"
xabar "   spctl --assess --type execute --verbose \"$APP_PATH\""
xabar ""
xabar " DIQQAT: DMG imzolangan .app dan KEYIN qayta yasalishi kerak:"
xabar "   bash build/publish-macos.sh ... (DMG bosqichini qayta bajaring)"
xabar " yoki DMG ni ham alohida imzolang:"
xabar "   codesign --force --timestamp --sign \"$IDENTITY\" <fayl>.dmg"
xabar ""
