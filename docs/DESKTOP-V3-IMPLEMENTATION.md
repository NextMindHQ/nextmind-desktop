# NextMind Desktop V3 — Implementation

## Stan wejściowy

V2 zapewniało profile, strefy, layout assist, wyszukiwarkę, zegar, notatki i izolowany launcher. Zachowano całą semantykę oraz poprawkę dwukliku pustego obszaru wyszukiwarki.

## Zmiany

- Schemat config V5 z globalnym `AppearancePreferencesConfig` i migracją V4→V5.
- Wspólny `FutureTheme.xaml` oraz runtime `FutureTheme.Apply`.
- Nowe resizable/scrollable Control Center z kartami, widocznym aktywnym profilem i panelem personalizacji.
- Holograficzne strefy, HUD widgety, kategorie w command search oraz spójne dialogi/notatki.
- Resource Saver i wyłączanie animacji bez background pollingu.
- `TEST-V3.cmd` budujący bieżące źródło do osobnego artefaktu i konfiguracji `%TEMP%`.

Nie zmieniono silnika layoutu, Managed Items, operacji plikowych, autostartu, hotkey scope ani architektury lokalnej.

