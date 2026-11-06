# Desktop V3 Appearance Live Preview Hotfix Design

## Intent

Visual Calibration ma dawać natychmiastowy, odwracalny podgląd wyłącznie na powierzchniach pulpitu. `Apply Appearance` zapisuje bieżący podgląd; zamknięcie Control Center cofa niezapisane zmiany. Konfiguracja pozostaje w schemacie V5.

## Constraints

- Nie używać `Window.Opacity`, nowych procesów, pollingu ani animacji w pętli.
- Control Center, nagłówki, tekst, przyciski i ikony zachowują stałą czytelność.
- Opacity dotyczy tylko tła treści stref i widgetów; glow oraz accent dotyczą dekoracji stref i widgetów.
- Podgląd nie zapisuje konfiguracji; zapis następuje wyłącznie po `Apply Appearance`.
- Reset jest podglądem do czasu zatwierdzenia.

## Approach

Wybrano rozdzielenie stałych zasobów UI od dynamicznych zasobów `Workspace*`. `FutureTheme.Apply` aktualizuje tylko zasoby powierzchni pulpitu, a XAML stref i widgetów wiąże z nimi osobne warstwy tła treści i dekoracji. Control Center korzysta wyłącznie ze stałych zasobów `Future*`.

Stan edycji obsługuje mała sesja podglądu: przechowuje kopię ostatnio zatwierdzonego wyglądu, emituje podgląd przy zmianie kontrolek, aktualizuje bazę po zatwierdzeniu i przy zamknięciu odtwarza bazę, jeśli po ostatnim zapisie były zmiany. Selektor akcentu będzie ciemną, klawiaturową listą czterech stale widocznych próbek z jednoznacznym stanem zaznaczenia.

Alternatywy odrzucone: lokalne nadpisywanie wszystkich zasobów w Control Center (łatwe do przeoczenia przy nowych kontrolkach) oraz modyfikowanie opacity całych okien (łamie czytelność nagłówków i kontrolek).

## Verification

Testy obejmą sesję preview/commit/rollback/reset, mapowanie zasobów workspace, stałe zasoby Control Center, oddzielenie tła treści od nagłówka oraz zachowanie schematu konfiguracji V5. Pełny zestaw testów i build Release zostaną uruchomione bez startowania V3.
