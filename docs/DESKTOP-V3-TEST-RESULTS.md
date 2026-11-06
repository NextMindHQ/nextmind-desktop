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
- Responsywny Clock & Date: PASS automatyczny — typografia rośnie dla rozmiarów compact/default/large, zachowuje budżet wysokości, reaguje natychmiast w prawdziwym komponencie WPF i daje identyczne metryki dla równoważnej geometrii przy 96/144/192 DPI.
- Responsive Clock & Date: zaakceptowany manualnie przez użytkownika.
- Future Window Chrome: PASS automatyczny — Control Center używa borderless `WindowChrome`, zachowuje `CanResize`, 44 DIP caption i 7 DIP natywną ramkę resize; maximize/restore przełącza stan okna.
- Ciemne przewijanie: PASS automatyczny — pionowe i poziome offsety działają, thumb pozostaje interaktywny, komendy stron odpowiadają orientacji, a narożnik `ScrollViewer` nie używa bieli systemowej.
- Resize grips: PASS automatyczny — widgety i strefy używają jawnego przezroczystego szablonu `Thumb`; istniejące handlery drag/resize pozostały podłączone.
- Pełny zestaw: PASS, 330/330 testów, 0 pominiętych.
- Release build: PASS, 0 błędów i 0 ostrzeżeń.
- Launcher `--verify-only`, świeże unikalne katalogi buildu i konfiguracji oraz wymuszony błąd builda: PASS bez startowania V3.

## NOT TESTED

- V3 Live Preview 2 przeszło wstępny test manualny użytkownika: suwaki reagują natychmiast, selektor Accent jest czytelny, a ogólny wygląd został oceniony pozytywnie.
- Nowy pasek tytułowy Control Center, scrollbary i resize grips nie zostały jeszcze ocenione wizualnie na uruchomionej V3.
- Drag-and-drop, wielomonitor/DPI i restart z V3 nadal wymagają testu manualnego.
- CPU, RAM, GPU i płynność interakcji na uruchomionej V3.
- Pełna seria zrzutów porównawczych przed/po regulacji suwaków nie została wykonana.

Aktualny chrome i scrollbary należy uznać za gotowe do testu manualnego, nie za wizualnie zatwierdzone.

