# Test NextMind Desktop V3 jednym kliknięciem

Dwukrotnie kliknij `TEST-V3.cmd` w głównym katalogu projektu.

Launcher sam:

1. Wykryje działający NextMind Desktop i poprosi o normalne zamknięcie przez ikonę przy zegarze → **Exit** lub Control Center → **Exit NextMind Desktop**.
2. Poczeka — nigdy nie używa `taskkill`, `Stop-Process` ani End task.
3. Skompiluje aktualny kod V3 do nowego podkatalogu `artifacts\NextMindDesktop-v3-current\build-*`. Każdy test dostaje świeży build; przy błędzie launcher zatrzyma się i nie uruchomi starszego pliku.
4. Utworzy nową konfigurację w `%TEMP%\NextMindDesktop-V3-Test`.
5. Sprawdzi marker skompilowanego runtime `NEXTMIND-DESKTOP-V3-LIVE-PREVIEW-2`.
6. Uruchomi V3 z osobnym testowym mutexem, bez nadpisywania `%LOCALAPPDATA%\NextMind\Desktop` i bez automatycznej zmiany autostartu.
7. Automatycznie otworzy Control Center z tytułem **V3 Live Preview 2**, aby było jasne, że testujesz właściwy build.

Podczas testu nie przełączaj ręcznie **Start with Windows**. Po zakończeniu V3 launcher pokaże ścieżkę normalnej wersji:

`%LOCALAPPDATA%\NextMind\Desktop\app\NextMindDesktop.exe`

Nie uruchamiaj do tego testu `tools\start.ps1` ani starego ZIP-a.

## Hotfix Visual Calibration — szybka checklista

Control Center otworzy się automatycznie. Przewiń do **Visual Calibration**:

1. Przesuń **Glow intensity** i **Panel opacity** — efekt powinien być natychmiastowy na otwartych widgetach i strefach.
2. Sprawdź, że nagłówki, tekst, przyciski i samo Control Center nie zmieniają przezroczystości ani intensywności glow.
3. Wybierz próbki **Electric Blue**, **Cyan**, **Violet** i **Amber** myszą oraz klawiaturą; aktywna próbka ma wyraźną ramkę.
4. Kliknij **Reset Style** — ma pokazać ustawienia domyślne bez zapisu.
5. Zamknij Control Center bez **Apply Appearance** — ostatnio zapisany wygląd powinien wrócić.
6. Otwórz Control Center ponownie, ustaw wygląd i kliknij **Apply Appearance**; po ponownym otwarciu wartości powinny zostać zachowane.

Jeśli widzisz standardową rozwijaną listę `ComboBox` zamiast czterech stale widocznych próbek, nie testujesz buildu **V3 Live Preview 2**. Zamknij tę instancję normalnie i ponownie użyj wyłącznie `TEST-V3.cmd`.

