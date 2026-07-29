# TimerTray (Windows)

Lekka aplikacja do **odliczania czasu** z **ikoną w zasobniku (tray)**, 
**autostartem**, **balonikiem + fallback (MessageBox)** oraz **pewnym dźwiękiem**.

## Funkcje
- Odliczanie z okna i sterowanie z traya (szybkie starty: 5m, 25m)
- Po zakończeniu: **dźwięk** + **powiadomienie** (balonik) + **fallback MessageBox**
- Ikona w zasobniku systemowym
- Autostart: skrót `.lnk` w folderze Autostartu (tworzony automatycznie; można wyłączyć w menu)
- Zamykanie krzyżykiem minimalizuje do traya (aplikacja dalej działa)
- Pozycja menu „🔔 Test powiadomienia” do szybkiego sprawdzenia dźwięku/powiadomień

## Kompilacja (Visual Studio lub CLI)
Wymagany **.NET SDK 8.0+**.

### Visual Studio
1. Otwórz rozwiązanie (`TimerTray.csproj`).
2. Uruchom (F5/CTRL+F5) – ikona pojawi się w trayu.
3. Profil publikacji: **Folder**, **Self-contained**, **win-x64** (lub `win-arm64`), **Single file**.

### CLI
```powershell
cd TimerTray

dotnet publish -c Release -r win-x64 --self-contained true   -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false
```
EXE: `bin\Release
et8.0-windows\win-x64\publish\TimerTray.exe`

## Użycie
- Uruchom `TimerTray.exe`. Pierwsze uruchomienie doda skrót do Autostartu.
- Ikona w trayu → dwuklik: pokazuje okno. Menu zawiera test i szybkie timery.
- Po upływie czasu: dźwięk **alarm.wav** (wgrany z aplikacją). Jeśli plik zostanie usunięty, użyty będzie fallback (beepy / dźwięk systemowy).

## Ustawienia systemowe
- Jeśli nie widzisz baloników, sprawdź **Ustawienia → System → Powiadomienia** oraz wyłącz **Asystenta koncentracji / Do Not Disturb**.

## Deinstalacja
- Z menu traya odznacz **Autostart** (usunięcie skrótu w folderze Startup).
- Wyjście z aplikacji → usuń `TimerTray.exe` i folder.
