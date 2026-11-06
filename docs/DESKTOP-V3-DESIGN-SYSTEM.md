# NextMind Desktop V3 — Design System

V3 używa jednego słownika `Themes/FutureTheme.xaml`. Kierunek: głęboki granat/czerń, grafitowe szkło, elektryczny błękit i turkus, chłodna biel tekstu. Amber i czerwień pozostają semantyczne.

Wspólne komponenty obejmują holograficzny panel, przyciski primary/secondary/danger, textbox, toggle, slider, menu, tooltip, nagłówek, wynik wyszukiwania, kontener widgetu i element strefy. Wszystkie powierzchnie pobierają kolory przez `DynamicResource`.

Personalizacja globalna: glow 0–100%, opacity 55–100%, akcent Electric Blue/Cyan/Violet/Amber, animacje oraz Resource Saver. Resource Saver zeruje dekoracyjny glow i czas ruchu, nie usuwa focusu ani stanów semantycznych. Brak ciągłych animacji, shaderów i nowych zależności.

Grafika koncepcyjna nie była dostępna lokalnie; implementacja opiera się na dostarczonej specyfikacji tekstowej.

