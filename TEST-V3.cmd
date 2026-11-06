@echo off
setlocal EnableExtensions DisableDelayedExpansion
title NextMind Desktop V3 - Future Workspace test
set "VERIFY_ONLY=0"
if /I "%~1"=="--verify-only" set "VERIFY_ONLY=1"
if not "%~2"=="" goto :usage
if not "%~1"=="" if /I not "%~1"=="--verify-only" goto :usage

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
set "PROJECT=%ROOT%\src\NextMind.Desktop.App\NextMind.Desktop.App.csproj"
set "THEME=%ROOT%\src\NextMind.Desktop.App\Themes\FutureTheme.xaml"
set "BUILD_DIR=%ROOT%\artifacts\NextMindDesktop-v3-current\build-%RANDOM%-%RANDOM%"
set "V3_EXE=%BUILD_DIR%\NextMindDesktop.exe"
set "NORMAL_EXE=%LOCALAPPDATA%\NextMind\Desktop\app\NextMindDesktop.exe"

echo ============================================================
echo  NEXTMIND DESKTOP V3 // FUTURE WORKSPACE
echo ============================================================
echo.
if "%VERIFY_ONLY%"=="1" goto :verify_process
call :is_running
if errorlevel 1 goto :prerequisites
echo NextMind Desktop jest uruchomiony.
echo Zamknij go normalnie: ikona przy zegarze - Exit,
echo albo Control Center - Exit NextMind Desktop.
echo Nie uzywaj End task. Launcher czeka i nie konczy procesu.
:wait
timeout /t 2 /nobreak >nul
call :is_running
if not errorlevel 1 goto :wait
goto :prerequisites

:verify_process
call :is_running
if errorlevel 1 (echo Running instance check: none detected.) else (echo Running instance check: detected.)

:prerequisites
if not exist "%PROJECT%" goto :missing
if not exist "%THEME%" goto :missing_theme
findstr /C:"NEXTMIND FUTURE WORKSPACE V3" "%THEME%" >nul 2>&1
if errorlevel 1 goto :missing_theme
where dotnet >nul 2>&1
if errorlevel 1 goto :missing_dotnet

echo Building current V3 Release source...
echo V3 marker: %THEME%
call dotnet build "%PROJECT%" -c Release --nologo -v minimal -m:1 -p:BuildInParallel=false "-p:OutDir=%BUILD_DIR%"
if errorlevel 1 goto :build_failed
if not exist "%V3_EXE%" goto :missing_output

set "CONFIG_BASE=%TEMP%\NextMindDesktop-V3-Test"
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
echo Build V3: %V3_EXE%
echo Isolated config: %TEST_CONFIG%
echo FutureTheme.xaml validated.
if "%VERIFY_ONLY%"=="1" goto :verified

echo Starting isolated V3. Do not toggle Start with Windows during the test.
start "" /wait "%V3_EXE%"
set "EXIT_CODE=%ERRORLEVEL%"
echo.
echo V3 test ended with code %EXIT_CODE%.
echo To return to the normal version, run:
echo   %NORMAL_EXE%
echo Test data remains in:
echo   %TEST_CONFIG%
pause
exit /b %EXIT_CODE%

:verified
echo V3 VERIFICATION PASSED - application was not started.
exit /b 0
:usage
echo Usage: TEST-V3.cmd [--verify-only]
exit /b 2
:missing
echo ERROR: App project not found. V3 was not started.
goto :failed
:missing_theme
echo ERROR: Current source does not contain the V3 FutureTheme marker. V3 was not started.
goto :failed
:missing_dotnet
echo ERROR: .NET 8 SDK was not found. V3 was not started.
goto :failed
:build_failed
echo ERROR: Release build failed. V3 was not started.
goto :failed
:missing_output
echo ERROR: Current build did not create %V3_EXE%. V3 was not started.
goto :failed
:config_failed
echo ERROR: Isolated config directory could not be created. V3 was not started.
:failed
if "%VERIFY_ONLY%"=="0" pause
exit /b 1
:is_running
tasklist /FI "IMAGENAME eq NextMindDesktop.exe" /NH 2>nul | find /I "NextMindDesktop.exe" >nul
exit /b %ERRORLEVEL%

