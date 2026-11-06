@echo off
setlocal EnableExtensions DisableDelayedExpansion
title NextMind Desktop V2 - isolated test launcher

set "VERIFY_ONLY=0"
if /I "%~1"=="--verify-only" set "VERIFY_ONLY=1"
if not "%~2"=="" goto :usage
if not "%~1"=="" if /I not "%~1"=="--verify-only" goto :usage

set "REPO_ROOT=%~dp0"
if "%REPO_ROOT:~-1%"=="\" set "REPO_ROOT=%REPO_ROOT:~0,-1%"
set "APP_PROJECT=%REPO_ROOT%\src\NextMind.Desktop.App\NextMind.Desktop.App.csproj"
set "SEARCH_XAML=%REPO_ROOT%\src\NextMind.Desktop.App\SearchWindow.xaml"
set "V2_EXE=%REPO_ROOT%\src\NextMind.Desktop.App\bin\Release\net8.0-windows10.0.19041.0\NextMindDesktop.exe"
set "NORMAL_EXE=%LOCALAPPDATA%\NextMind\Desktop\app\NextMindDesktop.exe"

echo ============================================================
echo  NextMind Desktop V2 - bezpieczny test izolowany
echo ============================================================
echo.

if "%VERIFY_ONLY%"=="1" goto :verify_process_check

call :is_nextmind_running
if errorlevel 1 goto :prerequisites

echo NextMind Desktop jest obecnie uruchomiony.
echo.
echo Zamknij go normalnie, aby zapisac konfiguracje:
echo   1. Kliknij prawym przyciskiem ikone NextMind przy zegarze.
echo   2. Wybierz Exit.
echo.
echo Mozesz tez wybrac Control Center - Exit NextMind Desktop.
echo Nie uzywaj Menedzera zadan ani opcji End task.
echo Launcher bedzie czekal i ruszy automatycznie po zamknieciu.
echo Zamkniecie tego okna anuluje test.
echo.

:wait_for_close
timeout /t 2 /nobreak >nul
call :is_nextmind_running
if not errorlevel 1 goto :wait_for_close
echo Poprzednia instancja zostala zamknieta normalnie.
echo.
goto :prerequisites

:verify_process_check
call :is_nextmind_running
if errorlevel 1 goto :verify_no_running_instance
echo Running instance check: detected.
goto :prerequisites

:verify_no_running_instance
echo Running instance check: none detected.
goto :prerequisites

:prerequisites
if not exist "%APP_PROJECT%" goto :missing_project
if not exist "%SEARCH_XAML%" goto :missing_fix

findstr /C:"EventSetter Event=" "%SEARCH_XAML%" 2>nul | findstr /C:"Result_DoubleClick" >nul
if errorlevel 1 goto :missing_fix

where dotnet >nul 2>&1
if errorlevel 1 goto :missing_dotnet

echo Kompilowanie aktualnego kodu V2 w trybie Release...
echo Zrodlo: %APP_PROJECT%
echo.
call dotnet build "%APP_PROJECT%" -c Release --nologo -v minimal
if errorlevel 1 goto :build_failed

if not exist "%V2_EXE%" goto :missing_build_output

set "CONFIG_BASE=%TEMP%\NextMindDesktop-V2-Test"
if not exist "%CONFIG_BASE%" mkdir "%CONFIG_BASE%" >nul 2>&1
if not exist "%CONFIG_BASE%" goto :config_failed

:choose_config
set "TEST_CONFIG=%CONFIG_BASE%\session-%RANDOM%-%RANDOM%"
if exist "%TEST_CONFIG%" goto :choose_config
mkdir "%TEST_CONFIG%" >nul 2>&1
if not exist "%TEST_CONFIG%" goto :config_failed

set "NEXTMIND_DESKTOP_CONFIG_DIR=%TEST_CONFIG%"
set "NEXTMIND_FAKE_USER_DESKTOP="
set "NEXTMIND_ENABLE_MANAGED_MOVES="

echo.
echo Build V2: %V2_EXE%
echo Isolated config: %TEST_CONFIG%
echo Obecna instalacja i jej konfiguracja nie sa modyfikowane.
echo.

if "%VERIFY_ONLY%"=="1" goto :verified

echo Uruchamianie V2. To okno pozostanie otwarte do zakonczenia testu.
echo W V2 nie wlaczaj opcji Start with Windows.
echo.
start "" /wait "%V2_EXE%"
set "V2_EXIT_CODE=%ERRORLEVEL%"

echo.
echo ============================================================
echo  Test V2 zostal zakonczony. Kod wyjscia: %V2_EXIT_CODE%
echo ============================================================
echo.
echo Aby wrocic do normalnej wersji, uruchom:
echo   %NORMAL_EXE%
echo.
echo Konfiguracja normalnej wersji nie zostala zmieniona.
echo Dane tego testu pozostaly w:
echo   %TEST_CONFIG%
echo.
pause
exit /b %V2_EXIT_CODE%

:verified
echo VERIFICATION PASSED - V2 was not started.
exit /b 0

:usage
echo Uzycie: TEST-V2.cmd [--verify-only]
exit /b 2

:missing_project
echo.
echo BLAD: Nie znaleziono projektu aplikacji:
echo   %APP_PROJECT%
goto :failed

:missing_fix
echo.
echo BLAD: Biezacy kod nie zawiera oczekiwanej poprawki dwukliku wyszukiwarki.
echo V2 nie zostalo uruchomione.
goto :failed

:missing_dotnet
echo.
echo BLAD: Nie znaleziono polecenia dotnet. Zainstaluj .NET 8 SDK.
echo V2 nie zostalo uruchomione.
goto :failed

:build_failed
echo.
echo BLAD: Release build failed. V2 was not started.
echo Popraw bledy kompilacji i uruchom TEST-V2.cmd ponownie.
goto :failed

:missing_build_output
echo.
echo BLAD: Kompilacja nie utworzyla oczekiwanego pliku:
echo   %V2_EXE%
echo V2 nie zostalo uruchomione.
goto :failed

:config_failed
echo.
echo BLAD: Nie mozna utworzyc izolowanego katalogu konfiguracji w:
echo   %CONFIG_BASE%
echo V2 nie zostalo uruchomione.
goto :failed

:failed
if "%VERIFY_ONLY%"=="0" pause
exit /b 1

:is_nextmind_running
tasklist /FI "IMAGENAME eq NextMindDesktop.exe" /NH 2>nul | find /I "NextMindDesktop.exe" >nul
exit /b %ERRORLEVEL%

