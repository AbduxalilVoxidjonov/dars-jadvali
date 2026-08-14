#!/usr/bin/env bash
# =====================================================================
#  Dars Jadvali — localhost test serveri (macOS / Linux / WSL)
#  Ishlatish:  bash build/run-web.sh
# =====================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
WEB_PROJECT="$PROJECT_ROOT/src/DarsJadvali.Web"

if ! command -v dotnet >/dev/null 2>&1; then
    echo "XATO: .NET SDK topilmadi." >&2
    echo "https://dotnet.microsoft.com/download/dotnet/8.0 dan .NET 8 SDK ni o'rnating." >&2
    exit 1
fi

if [ ! -d "$WEB_PROJECT" ]; then
    echo "XATO: Web loyihasi topilmadi: $WEB_PROJECT" >&2
    exit 1
fi

echo "========================================================="
echo " Dars Jadvali — localhost test serveri"
echo "========================================================="
echo ""
echo " Manzil: http://localhost:5080"
echo " Brauzerda shu manzilni oching."
echo " To'xtatish uchun: Ctrl + C"
echo ""

exec dotnet run --project "$WEB_PROJECT"
