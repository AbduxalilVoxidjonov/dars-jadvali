@echo off
rem =====================================================================
rem  Dars Jadvali Tuzuvchi - ESKI (WPF) versiyasini yig'ish
rem
rem  DIQQAT: bu fayl ESKI WPF dasturini (src\DarsJadvali.UI) yig'adi!
rem
rem  YANGI (Avalonia) dastur uchun BUNI EMAS, quyidagini ishlating:
rem      pwsh -NoProfile -ExecutionPolicy Bypass -File .\build\publish-windows.ps1
rem  (yoki PowerShell 7 bo'lmasa: powershell -NoProfile -ExecutionPolicy Bypass
rem   -File .\build\publish-windows.ps1)
rem
rem  Standart: win-x64 va win-x86, self-contained (.NET ichida).
rem  Natija: loyiha ildizidagi "publish\legacy-wpf" papkasi + ZIP arxivlar.
rem =====================================================================

setlocal

echo.
echo =====================================================================
echo  DIQQAT: bu ESKI (WPF) versiyani yigadi!
echo  Yangi (Avalonia) dastur uchun: build\publish-windows.ps1
echo =====================================================================
echo.
echo  Davom etilsinmi? Yangi versiya kerak bolsa - Ctrl+C bosing.
echo.
pause

echo.
echo =====================================================================
echo  Dars Jadvali Tuzuvchi (ESKI WPF) - yigilmoqda (x64 va x86)
echo =====================================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" -Runtime all

if errorlevel 1 (
    echo.
    echo XATO: yigish muvaffaqiyatsiz tugadi. Yuqoridagi xabarlarni o'qing.
    echo .NET 8 SDK o'rnatilganini tekshiring: https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

echo.
echo TAYYOR! "publish\legacy-wpf" papkasini oching.
echo Eslatma: bu ESKI WPF versiyasi edi.
echo.
pause
endlocal
