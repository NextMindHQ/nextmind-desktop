# NextMind Desktop V3 — Test Results

Data: 2026-11-01. Gałąź: `forge/desktop-v3-future-workspace`.

## Automatyczne

- Migracja/personalizacja V5: PASS.
- Zasoby design systemu i Resource Saver: PASS.
- Mapowanie Control Center: PASS.
- Regresja wyszukiwarki, w tym pusty obszar dwukliku: PASS.
- Live Preview: PASS — podgląd nie zapisuje, Apply aktualizuje snapshot, Reset jest tymczasowy, a zamknięcie cofa do ostatniego Apply.
- Zakres zasobów: PASS — opacity/glow/accent aktualizują zasoby `Workspace*`; stałe zasoby Control Center, nagłówków, tekstu i kontrolek pozostają niezmienione.
- Ciemny selektor Accent: PASS — cztery próbki, stan zaznaczenia i focus klawiatury są częścią współdzielonego stylu WPF.
- Zgodność konfiguracji V5: PASS.
- Pełny zestaw: PASS, 313/313 testów, 0 pominiętych.
- Release build: PASS, 0 błędów i 0 ostrzeżeń.
- Launcher `--verify-only`, świeże unikalne katalogi buildu i konfiguracji oraz wymuszony błąd builda: PASS bez startowania V3.

## NOT TESTED

- Manualna ocena hotfixu Live Preview i selektora Accent na żywym pulpicie, drag-and-drop, wielomonitor/DPI i restart z V3.
- CPU, RAM, GPU i płynność interakcji na uruchomionej V3.
- Zrzuty ekranu — V3 nie została uruchomiona, a aktywna instancja nie została zatrzymana.

Nie należy uznawać V3 za w pełni ukończoną przed testem manualnym przez `TEST-V3.cmd`.

