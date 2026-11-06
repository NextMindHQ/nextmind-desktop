# NextMind Desktop V3 — Test Results

Data: 2026-11-01. Gałąź: `forge/desktop-v3-future-workspace`.

## Automatyczne

- Migracja/personalizacja V5: PASS.
- Zasoby design systemu i Resource Saver: PASS.
- Mapowanie Control Center: PASS.
- Regresja wyszukiwarki, w tym pusty obszar dwukliku: PASS.
- Live Preview: PASS — podgląd nie zapisuje, Apply aktualizuje snapshot, Reset jest tymczasowy, a zamknięcie cofa do ostatniego Apply.
- Zakres zasobów: PASS — opacity/glow/accent aktualizują zasoby `Workspace*`; stałe zasoby Control Center, nagłówków, tekstu i kontrolek pozostają niezmienione.
- Struktura selektora Accent: PASS automatyczny — `ListBox`, cztery próbki, stan zaznaczenia i focus klawiatury są częścią współdzielonego stylu WPF; pełna ocena wizualna pozostaje manualna.
- Zgodność konfiguracji V5: PASS.
- Regresja Live Preview na rzeczywistym `DynamicResource` WPF: PASS — zasób tła, glow i accent zmieniają się przed Apply; zapis nie jest wywoływany; rollback przywraca bazę.
- Izolacja procesu testowego: PASS — `TEST-V3.cmd` używa osobnego mutexu tylko przy przekierowanej konfiguracji, więc stary proces nie przejmie nowego testu.
- Identyfikacja runtime: PASS — skompilowany marker i tytuł `V3 Live Preview 2`.
- Pełny zestaw: PASS, 317/317 testów, 0 pominiętych.
- Release build: PASS, 0 błędów i 0 ostrzeżeń.
- Launcher `--verify-only`, świeże unikalne katalogi buildu i konfiguracji oraz wymuszony błąd builda: PASS bez startowania V3.

## NOT TESTED

- Rzeczywisty test UI potwierdził uruchomienie `V3 Live Preview 2` oraz kontrolkę `ListBox` z opcjami Electric Blue, Cyan, Violet i Amber. Nie ogłaszamy wizualnego PASS: dalszą interakcję przerwało wejście użytkownika.
- Manualne potwierdzenie natychmiastowej zmiany widgetów/stref suwakiem i pełna wizualna ocena selektora nadal są wymagane.
- Drag-and-drop, wielomonitor/DPI i restart z V3 nadal wymagają testu manualnego.
- CPU, RAM, GPU i płynność interakcji na uruchomionej V3.
- Pełna seria zrzutów porównawczych przed/po regulacji suwaków nie została wykonana.

Nie należy uznawać hotfixu za ukończony, dopóki oba zgłoszone błędy nie przejdą ponownego testu manualnego przez `TEST-V3.cmd`.

