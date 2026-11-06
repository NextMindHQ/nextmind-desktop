@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0INSTALL-V3.ps1"
set "RC=%ERRORLEVEL%"
echo.
if "%RC%"=="0" (echo NextMind Desktop V3 installation completed.) else (echo Installation was not completed. Existing data was left in place.)
pause
exit /b %RC%
