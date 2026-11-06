# Test NextMind Desktop V3 jednym kliknięciem

Dwukrotnie kliknij `TEST-V3.cmd` w głównym katalogu projektu.

Launcher sam:

1. Wykryje działający NextMind Desktop i poprosi o normalne zamknięcie przez ikonę przy zegarze → **Exit** lub Control Center → **Exit NextMind Desktop**.
2. Poczeka — nigdy nie używa `taskkill`, `Stop-Process` ani End task.
3. Skompiluje aktualny kod V3 do `artifacts\NextMindDesktop-v3-current`. Przy błędzie zatrzyma się i nie uruchomi starszego pliku.
4. Utworzy nową konfigurację w `%TEMP%\NextMindDesktop-V3-Test`.
5. Uruchomi V3 bez nadpisywania `%LOCALAPPDATA%\NextMind\Desktop` i bez automatycznej zmiany autostartu.

Podczas testu nie przełączaj ręcznie **Start with Windows**. Po zakończeniu V3 launcher pokaże ścieżkę normalnej wersji:

`%LOCALAPPDATA%\NextMind\Desktop\app\NextMindDesktop.exe`

Nie uruchamiaj do tego testu `tools\start.ps1` ani starego ZIP-a.

