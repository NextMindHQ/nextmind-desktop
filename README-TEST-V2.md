# Jak uruchomić test NextMind Desktop V2

Do uruchomienia testu służy jeden plik:

`TEST-V2.cmd`

Znajduje się on w głównym katalogu projektu. Nie trzeba otwierać PowerShella ani wpisywać żadnych poleceń.

## Uruchomienie

1. Dwukrotnie kliknij `TEST-V2.cmd`.
2. Jeżeli normalny NextMind Desktop nadal działa, launcher poprosi o jego zwykłe zamknięcie:
   - kliknij prawym przyciskiem ikonę NextMind obok zegara i wybierz **Exit**, albo
   - w Control Center wybierz **Exit NextMind Desktop**.
3. Nie używaj Menedżera zadań ani **End task**. Launcher sam zauważy normalne zamknięcie i będzie kontynuował.
4. Poczekaj na zakończenie kompilacji. Launcher zawsze kompiluje aktualny kod V2; nie używa starszego ZIP-a.
5. Po udanej kompilacji V2 uruchomi się automatycznie.

Jeśli kompilacja się nie powiedzie, zobaczysz czytelny komunikat, a V2 nie zostanie uruchomione. Istniejący starszy build również nie zostanie użyty.

## Co jest bezpiecznie odizolowane

- Każde uruchomienie otrzymuje nowy katalog konfiguracji w `%TEMP%\NextMindDesktop-V2-Test`.
- Launcher nie zapisuje niczego w `%LOCALAPPDATA%\NextMind\Desktop`, czyli katalogu normalnej instalacji i jej konfiguracji.
- Launcher uruchamia build bezpośrednio z bieżącego kodu projektu, po sprawdzeniu poprawki wyszukiwarki.
- Zmienne testowe obowiązują tylko wewnątrz launchera i uruchomionej przez niego V2.
- Tryb testowy nie rejestruje V2 automatycznie w autostarcie Windows.

W czasie testu V2 nie przełączaj ręcznie opcji **Start with Windows**. Ręczne kliknięcie tej opcji jest osobną operacją użytkownika i nie jest potrzebne do testów.

## Zakończenie testu

Zamknij V2 normalnie przez ikonę obok zegara (**Exit**) albo Control Center (**Exit NextMind Desktop**). Otwarte czarne okno launchera wyświetli wtedy:

- kod zakończenia V2,
- lokalizację izolowanych danych testowych,
- ścieżkę do normalnej, zainstalowanej wersji.

Normalną wersję można ponownie uruchomić z:

`%LOCALAPPDATA%\NextMind\Desktop\app\NextMindDesktop.exe`

Jej wcześniejsza konfiguracja pozostaje niezmieniona.

## Ważne

- Nie uruchamiaj `tools\start.ps1` do tego testu — ten starszy skrypt publikuje do katalogu normalnej instalacji.
- Nie uruchamiaj pliku ze starego ZIP-a; `TEST-V2.cmd` wybiera aktualny build z poprawką wyszukiwarki.
- Zamknięcie okna launchera podczas oczekiwania anuluje test i nie kończy działającej aplikacji.

