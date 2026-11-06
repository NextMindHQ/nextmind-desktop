# NextMind Desktop V3 — Test Results

Data ostatniej pełnej weryfikacji: 2026-11-06. Gałąź: `codex/v3-public-integration`, utworzona z publicznego `main` `22eef168`.

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
- Identyfikacja runtime testowego: PASS — marker jest dostępny wyłącznie w jawnie izolowanym buildzie testowym. Produkcyjny Control Center nie zawiera etykiet preview.
- Responsywny Clock & Date: PASS automatyczny — typografia rośnie dla rozmiarów compact/default/large, zachowuje budżet wysokości, reaguje natychmiast w prawdziwym komponencie WPF i daje identyczne metryki dla równoważnej geometrii przy 96/144/192 DPI.
- Responsive Clock & Date: zaakceptowany manualnie przez użytkownika.
- Future Window Chrome: PASS automatyczny — Control Center używa borderless `WindowChrome`, zachowuje `CanResize`, 44 DIP caption i 7 DIP natywną ramkę resize; maximize/restore przełącza stan okna.
- Ciemne przewijanie: PASS automatyczny — pionowe i poziome offsety działają, thumb pozostaje interaktywny, komendy stron odpowiadają orientacji, a narożnik `ScrollViewer` nie używa bieli systemowej.
- Resize grips: PASS automatyczny — widgety i strefy używają jawnego przezroczystego szablonu `Thumb`; istniejące handlery drag/resize pozostały podłączone.
- Publiczny punkt bazowy: PASS, 364/364 testy.
- Pełny zestaw integracyjny: PASS, 432/432 testy, 0 pominiętych.
- Release build: PASS, 0 błędów i 0 ostrzeżeń.
- Regresje odzyskane z publicznego `main`: PASS — wyszukiwanie w strefie, wolne miejsce i snapping, smart expand, pozycjonowanie i zamykanie menu oraz przywracanie układu.
- Integracja snappingu: PASS — ustawienie V3 steruje natywnym snappingiem publicznego `main`, Alt omija snapping elementów i siatki, a po zakończeniu ruchu element nie jest przyciągany drugi raz.
- Koegzystencja funkcji V3: PASS — profile, globalne wyszukiwanie, widgety, Quick Note, personalizacja, schemat V5 i izolacja pojedynczej instancji.
- Launcher `--verify-only`, świeże unikalne katalogi buildu i konfiguracji oraz wymuszony błąd builda: PASS bez startowania V3.
- Setup ZIP w izolowanym katalogu: PASS — manifest i payload zweryfikowane, poprzednia aplikacja przeniesiona do backupu, receipt i backup manifest utworzone, syntetyczne `config.json`, `ManagedItems` i log zachowały identyczne SHA-256.
- Instalator i deinstalator: PASS w Windows PowerShell 5.1 — ścieżki względne backupu nie wymagają API z PowerShell 7, a niestandardowy katalog instalacji jest odczytywany z lokalizacji zainstalowanego skryptu/receipt zamiast kierować operację na domyślną instalację.
- Portable ZIP: PASS — obecne EXE, marker i self-contained `coreclr.dll`; brak spakowanej konfiguracji i logów; test ścieżek potwierdza odrębność `Desktop-Portable` od `Desktop`.
- README: PASS w lokalnym renderze desktop 1440 px i mobile 390 px; grafiki skalują się bez poziomego przepełnienia.

## Manualne potwierdzenia i pozostałe ograniczenia

- Użytkownik potwierdził codzienne działanie V3 na Windows 11, prawidłowy restart i zachowanie danych.
- Użytkownik zaakceptował responsywny Clock & Date oraz kierunek wizualny Future Workspace.
- W izolowanej instancji demonstracyjnej sprawdzono wizualnie Control Center, pasek tytułowy, ciemne scrollbary, widgety i personalizację.
- Pełny test wielomonitor/DPI, długotrwały profil CPU/RAM/GPU i każda kombinacja drag-and-drop pozostają poza zakresem audytu prezentacji.
- Finalne lokalne paczki kandydujące są odtwarzane skryptem `release/BUILD-V3.ps1` z bieżącego commita integracyjnego. Przed publikacją należy odtworzyć je ponownie z dokładnego commita na `main` i potwierdzić publiczne CI.

