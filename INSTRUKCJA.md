# VR Lokomocja – lokomocja i komfort na Meta Quest 3S (Unity 6.3 LTS)

Projekt pokazuje i porównuje **sposoby poruszania się w VR** oraz **techniki ograniczania choroby symulatorowej**. Gracz przechodzi tor przeszkód złożony z pięciu odcinków. Każdy odcinek sprawdza inny aspekt lokomocji. Ustawienia zmienia się w goglach, a tablica wyników za metą zestawia czas, sposób przejścia i ocenę komfortu z kolejnych prób.

- **Lokomocja:** teleport (łuk balistyczny), ruch płynny gałką, oba naraz.
- **Obrót:** skokowy (30° / 45° / 90°), płynny (45 / 90 / 135 °/s), wyłączony.
- **Komfort:** winieta (tunneling) w trzech siłach, mrugnięcie przy teleporcie i obrocie, przelot „dash”, tryb siedzący, kierunek ruchu wg głowy albo kontrolera, trzy prędkości.
- **Wejście: wyłącznie kontrolery Touch przez `OVRInput`**. Śledzenie dłoni jest wyłączone (*Controllers Only*).
- **Grafika: tylko prymitywy Unity** i panele UI. Nie ma modeli, tekstur ani plików dźwiękowych, bo dźwięki są syntetyzowane w kodzie.
- **Z Meta XR SDK** używane są tylko `OVRCameraRig`, `OVRManager` i `OVRInput` (pakiet *Meta XR Core SDK*).
- **Scena powstaje automatycznie** z menu `VR Lokomocja`, a **Panel kontrolny** pokazuje, co jeszcze trzeba ustawić i ma zakładkę z optymalizacją buildu.
- **Uruchamianie: Build & Run albo Patch & Run przez USB.**

---

## Spis treści

1. [Wymagania](#1-wymagania)
2. [Zawartość projektu](#2-zawartość-projektu)
3. [Pierwsze uruchomienie](#3-pierwsze-uruchomienie)
4. [Menu „VR Lokomocja” i Panel kontrolny](#4-menu-vr-lokomocja-i-panel-kontrolny)
5. [Build & Run przez USB](#5-build--run-przez-usb)
6. [Optymalizacja buildu: profile DEV i RELEASE](#6-optymalizacja-buildu-profile-dev-i-release)
7. [Debugowanie przez USB](#7-debugowanie-przez-usb)
8. [Instrukcja obsługi w goglach](#8-instrukcja-obsługi-w-goglach)
9. [Jak to działa](#9-jak-to-działa)
10. [Dostrajanie i rozbudowa](#10-dostrajanie-i-rozbudowa)
11. [Rozwiązywanie problemów](#11-rozwiązywanie-problemów)

---

## 1. Wymagania

| Element | Wersja / uwagi |
|---|---|
| Unity | **6000.3.24f1** (Unity 6.3 LTS) z modułem **Android Build Support** (razem z *OpenJDK* i *Android SDK & NDK Tools*). Moduł dodasz w Unity Hub › Installs › ⚙ › Add modules. |
| Gogle | Meta Quest 3S z aktualnym Horizon OS i **włączonym trybem deweloperskim** |
| Kabel | USB-C **z transmisją danych** (sam kabel do ładowania nie wystarczy) |

Pakiety z `Packages/manifest.json` instalują się same przy pierwszym otwarciu projektu:

| Pakiet | Wersja | Rola |
|---|---|---|
| `com.meta.xr.sdk.core` | 207.0.1 | `OVRCameraRig`, `OVRManager`, **`OVRInput`** |
| `com.unity.xr.openxr` | 1.18.0 | Backend OpenXR (zalecany przez Meta) |
| `com.unity.render-pipelines.universal` | 17.3.0 | URP |
| `com.unity.inputsystem` | 1.18.0 | Zależność OpenXR. Projekt go nie używa, bo całe wejście idzie przez `OVRInput`. |
| `com.unity.ugui` | 2.0.0 | Menu, tablice i napisy (Canvas + Text) |

**Interaction SDK** (`com.meta.xr.sdk.interaction.ovr`) nie jest potrzebne. Możesz je dodać, ale wydłuży build.

> Jeśli Package Manager zgłosi brak którejś wersji, wybierz w **Window › Package Manager** wersję proponowaną dla Twojego edytora.

---

## 2. Zawartość projektu

```
unity_vr_lokomocja/
├─ INSTRUKCJA.md                          ← ten plik
├─ README.md
├─ Packages/manifest.json                 ← lista pakietów
├─ ProjectSettings/ProjectVersion.txt     ← 6000.3.24f1 (resztę ustawień Unity tworzy sam)
└─ Assets/VRLocomotion/
   ├─ Editor/
   │  ├─ VRLocomotionSetup.cs             ← menu „VR Lokomocja”, konfiguracja projektu, lista kontrolna, pierwszy start
   │  ├─ VRLocomotionSceneBuilder.cs      ← generator sceny: tor przeszkód, bramki, tablice, menu, gracz
   │  ├─ VRLocomotionBuild.cs             ← profile DEV/RELEASE, Build/Patch And Run, narzędzia adb
   │  └─ VRLocomotionWindow.cs            ← Panel kontrolny (3 zakładki)
   ├─ Scripts/
   │  ├─ ComfortSettings.cs               ← wszystkie ustawienia lokomocji i komfortu, presety, zapis w goglach
   │  ├─ LocomotionController.cs          ← ruch płynny, obrót, wykonanie teleportu, kolizje, tryb siedzący
   │  ├─ TeleportPointer.cs               ← łuk teleportu, sprawdzanie celu, znacznik
   │  ├─ ComfortVignette.cs               ← winieta (tunneling) zależna od prędkości ruchu
   │  ├─ ScreenFader.cs                   ← zaciemnienie obrazu („mrugnięcie”)
   │  ├─ ComfortMenu.cs                   ← menu komfortu w goglach + skróty A/B/X/Y
   │  ├─ UiPointer.cs, MenuButton.cs      ← promień z prawego kontrolera i przyciski 3D
   │  ├─ CourseTimer.cs                   ← trasa: bramki, czas, statystyki, upadki do wody, wyniki, ocena komfortu
   │  ├─ Checkpoint.cs, HazardZone.cs     ← bramka, strefa wody
   │  ├─ TeleportBlocker.cs               ← „tu nie wolno teleportować” (woda)
   │  ├─ Toast.cs, WristHud.cs            ← komunikaty przed graczem, wyświetlacz na lewym kontrolerze
   │  ├─ ControllerFollower.cs            ← przypina modele kontrolerów do rigu
   │  ├─ LocoRig.cs                       ← dostęp do rigu, głowy i kontrolerów
   │  ├─ Haptics.cs, Sfx.cs               ← wibracje i dźwięki syntetyzowane w kodzie
   ├─ Scenes/VRLokomocja.unity            ← tworzona przez menu (krok 2)
   ├─ Materials/*.mat                     ← tworzone automatycznie (URP Lit / Unlit, nakładka przezroczysta)
   └─ Settings/VRLokomocja_URP*.asset     ← tworzone automatycznie, jeśli projekt nie ma URP
```

---

## 3. Pierwsze uruchomienie

### Krok 1: otwarcie projektu
1. Unity Hub › **Add › Add project from disk** › wskaż folder `unity_vr_lokomocja`.
2. Otwórz projekt w **6000.3.24f1**. Pierwszy import trwa kilka minut.
3. Mogą pojawić się okna Meta SDK (telemetria, propozycja restartu z nowym systemem wejścia). Zaakceptuj domyślne odpowiedzi; restart edytora jest normalny.

### Krok 2: Szybki start (automatycznie)
Po imporcie pojawi się okno **VR Lokomocja** z przyciskiem **Szybki start**. To samo jest w górnym menu: **VR Lokomocja › 0. Szybki start (konfiguracja + scena)**.

| Co | Ustawienie |
|---|---|
| Player (Android) | IL2CPP, **ARM64**, min. API 32, **Vulkan**, Linear, orientacja pozioma, `com.vrlokomocja.quest` |
| Grafika | **URP**: MSAA 4×, bez HDR, bez cieni i świateł dodatkowych |
| XR | **XR Plug-in Management › Android › OpenXR** + **Meta XR Feature** + profile kontrolerów Touch |
| Meta | **Hand Tracking Support = Controllers Only** |
| Build | profil **DEV** (szybki Build & Run, IL2CPP „Faster (smaller) builds”, Development + Script Debugging) |
| Scena | `Assets/VRLocomotion/Scenes/VRLokomocja.unity`: tor przeszkód, **OVRCameraRig** z **CharacterControllerem**, **Tracking Origin = Floor Level**, scena w buildzie |

Na koniec pojawia się propozycja **przełączenia platformy na Android**. Zgódź się; trwa to kilka minut i robisz to tylko raz.

### Krok 3: Project Setup Tool (jedyny obowiązkowy krok ręczny)
**Meta › Tools › Project Setup Tool** (albo przycisk w Panelu kontrolnym). Na zakładce **Android** kliknij **Fix All**, potem **Apply All**, i powtarzaj, aż znikną pozycje *Required*.

### Krok 4: sprawdzenie
**VR Lokomocja › Panel kontrolny** › zakładka **1. Kontrola projektu**. Wszystkie punkty powinny mieć status `[OK]`; przy błędnych jest przycisk naprawy.

> **Building Blocks nie są potrzebne.** Generator wstawia prefab `OVRCameraRig` (to samo, co blok *Camera Rig*) i dodaje do niego `CharacterController`. Bloku *Controller Tracking* nie dodawaj, bo kontrolery mają własne schematyczne modele z prymitywów.

---

## 4. Menu „VR Lokomocja” i Panel kontrolny

### Menu w górnym pasku

| Pozycja | Działanie |
|---|---|
| **0. Szybki start (konfiguracja + scena)** | punkty 1 i 2 naraz, a potem propozycja przełączenia na Android |
| **1. Konfiguruj projekt dla Quest 3S** | same ustawienia projektu (tabela w kroku 2) |
| **2. Generuj scenę z trasą** | tworzy albo odświeża scenę. Obiekty `Environment`, `Course`, `Player`, `UI` i światło powstają od nowa, a rig i Twoje własne obiekty zostają. |
| **3. Build And Run (USB)** | pełny build DEV, instalacja i uruchomienie na goglach |
| **4. Patch And Run (USB, szybki)** | szybki build: na gogle trafiają tylko zmienione pliki |
| **Panel kontrolny (kontrola, build, gogle)** | okno z zakładkami (niżej) |
| Otwórz scenę / Otwórz instrukcję | skróty |

### Panel kontrolny: zakładki

**1. Kontrola projektu** to lista kontrolna: platforma, Player Settings, URP, OpenXR, Meta XR Feature, tryb „tylko kontrolery”, scena w buildzie, jeden rig bez dodatkowych kamer i Floor Level. Każdy punkt ma przycisk naprawy. Ręczny jest tylko **Project Setup Tool**, oznaczony `[RĘCZNIE]`.

**2. Build i optymalizacja** to tabela ustawień buildu z wartościami obecnymi i docelowymi dla profili **DEV** i **RELEASE**, przyciski *Zastosuj profil*, przełączniki (Script Debugging, Profiler, czyszczenie logcat) i uruchamianie (Build And Run, Patch And Run, Patch And Run tylko dla skryptów, wersja RELEASE). Na dole widać czas ostatniego buildu. Szczegóły w [punkcie 6](#6-optymalizacja-buildu-profile-dev-i-release).

**3. Gogle (USB / ADB)** pokazuje stan połączenia, pozwala zainstalować ostatni APK **bez budowania**, uruchomić, zatrzymać i odinstalować aplikację, pobrać log Unity z gogli do pliku i otworzyć okno Android Logcat.

---

## 5. Build & Run przez USB

### Przygotowanie gogli (jednorazowo)
1. Aplikacja **Meta Horizon** na telefonie › Urządzenia › Quest 3S › **Ustawienia gogli › Tryb dewelopera** › włącz.
2. Podłącz gogle kablem USB-C i załóż je. Pojawi się pytanie **„Zezwolić na debugowanie USB?”**. Zaznacz *Zawsze zezwalaj z tego komputera* i potwierdź.
3. Panel kontrolny › **3. Gogle** › **Sprawdź połączenie** powinno pokazać zielony punkt i model gogli.

### Uruchamianie

| Sposób | Kiedy |
|---|---|
| **VR Lokomocja › 3. Build And Run (USB)** | pierwszy raz i po większych zmianach (scena, ustawienia, pakiety) |
| **VR Lokomocja › 4. Patch And Run (USB, szybki)** | kolejne iteracje |
| Panel › 2 › **Patch And Run – skrypty** | gdy zmieniasz tylko kod C# |
| Panel › 3 › **Zainstaluj ostatni APK** | ponowna instalacja bez budowania |
| **File › Build Profiles › Android › Build And Run** | standardowa droga Unity; ustawienia z profilu DEV są już wpisane |

APK ląduje w `Builds/VRLokomocja.apk` (RELEASE: `Builds/VRLokomocja_release.apk`). Aplikacja uruchamia się sama; później znajdziesz ją w **Biblioteka › Nieznane źródła**.

---

## 6. Optymalizacja buildu: profile DEV i RELEASE

| Ustawienie | DEV (szybki Build & Run) | RELEASE (ładowanie / wydajność) | Dlaczego |
|---|---|---|---|
| Development Build | tak | nie | logi, profiler, debugger |
| Script Debugging | tak (przełącznik) | nie | debugger C# przez USB |
| IL2CPP Compiler Configuration | **Debug** | Release | Debug = najkrótsza kompilacja C++ |
| IL2CPP Code Generation | **OptimizeSize** („Faster (smaller) builds”) | OptimizeSpeed | mniej generowanego kodu = szybszy build |
| Managed Stripping Level | Low | Low | bezpieczne dla refleksji w Meta SDK |
| Kompresja APK | **LZ4** | LZ4HC | LZ4 szybciej się pakuje, LZ4HC daje mniejszy plik |
| symbols.zip | brak | brak | pakowanie symboli wydłuża build |
| Minify (R8) | nie | nie | zbędny krok Gradle |
| Splash screen Unity | nie | nie | **szybszy start aplikacji** |
| Incremental GC | tak | tak | mniej przycięć klatek |
| Strip Unused Mesh Components | tak | tak | mniejsze dane, szybsze ładowanie |
| Kompresja tekstur | ASTC | ASTC | natywny format GPU Questa |
| Optimized Frame Pacing | nie | nie | w VR tempo klatek ustala kompozytor Meta |
| App Bundle (AAB) | nie | nie | przez USB instaluje się APK |

Stałe dla obu profili: **IL2CPP + tylko ARM64**. Mała scena z prymitywów, bez tekstur i wypiekanego światła, oraz URP bez cieni i HDR dają mało wariantów shaderów, więc aplikacja szybko się buduje i ładuje.

**Szybka iteracja:** pierwszy build zawsze trwa najdłużej (IL2CPP + Gradle), więc nie usuwaj folderu `Library`. Pierwszy *Patch And Run* przygotowuje środowisko na goglach; kolejne są szybsze. Przełączanie DEV ↔ RELEASE wymusza ponowną kompilację IL2CPP.

---

## 7. Debugowanie przez USB

- **Logi:** w profilu DEV `Debug.Log` trafia do **Console** (wybierz gogle z listy połączeń u góry okna), do okna **Android Logcat** (Panel › 3) albo do pliku `Logs/quest_logcat.txt` (Panel › 3 › *Pobierz log Unity z gogli*). Wpisy projektu mają prefiks `[VRLokomocja]`.
- **Debugger C#:** przy włączonym Script Debugging w Rider lub Visual Studio użyj *Attach to Unity Process* i wybierz urządzenie Android.
- **Profiler:** włącz *Autoconnect Profiler* w Panelu › 2 albo wybierz gogle w oknie Profiler.
- **Na żywo w goglach:** wyświetlacz na lewym kontrolerze pokazuje prędkość sztucznego ruchu i obrotu oraz siłę winiety (`W 60%`), co ułatwia dostrajanie.

---

## 8. Instrukcja obsługi w goglach

### Sterowanie (kontrolery Touch Plus)

| Wejście | Akcja |
|---|---|
| **Lewa gałka** | ruch płynny (tryby „Płynna” i „Teleport + płynna”) |
| **Prawa gałka ↑** | celowanie teleportem; **puszczenie gałki = skok**, **grip** = anulowanie |
| **Lewa gałka ↑** | celowanie teleportem, tylko w trybie „Teleport” |
| **Prawa gałka ← →** | obrót (skokowy albo płynny) |
| **Menu** (lewy kontroler) | otwiera / zamyka **menu komfortu** przed Tobą; ruch jest wtedy zablokowany |
| **Trigger** (prawy) | wybór w menu i przy ocenie komfortu na mecie |
| **A** | następny tryb lokomocji |
| **B** | następny poziom winiety (z podglądem) |
| **Y** | tryb siedzący wł./wył. |
| **X** | powrót na start, nowa próba |

Każda zmiana skrótem pokazuje krótki komunikat przed Tobą. Unieś lewą rękę: nad kontrolerem jest mały wyświetlacz z bieżącymi ustawieniami, prędkością, siłą winiety i czasem trasy.

### Menu komfortu

Dziesięć opcji ze strzałkami `<` `>` i trzy presety. Pod listą wyświetla się opis ostatnio zmienionej opcji, czyli do czego służy i jaki ma wpływ na komfort. Opcje, które w danym trybie nic nie zmieniają (np. kąt obrotu skokowego przy obrocie płynnym), są wyszarzone.

| Opcja | Wartości |
|---|---|
| Lokomocja | Teleport / Płynna / Teleport + płynna |
| Kierunek ruchu | wg głowy / wg kontrolera (lewego) |
| Prędkość ruchu | 1,5 / 2,5 / 4,0 m/s |
| Obrót | skokowy / płynny / wyłączony |
| Kąt obrotu skokowego | 30° / 45° / 90° |
| Prędkość obrotu płynnego | 45 / 90 / 135 °/s |
| Przejście teleportu | natychmiast / mrugnięcie / dash |
| Winieta | wył. / słaba / średnia / silna |
| Mrugnięcie przy obrocie | tak / nie |
| Tryb siedzący | tak (+45 cm) / nie |

| Preset | Ustawienia |
|---|---|
| **Maks. komfort** | tylko teleport z mrugnięciem, obrót skokowy 45° z mrugnięciem, silna winieta |
| **Zrównoważony** | teleport + ruch płynny 1,5 m/s wg głowy, obrót skokowy 45°, średnia winieta |
| **Swoboda** | tylko ruch płynny 2,5 m/s wg kontrolera, obrót płynny 90°/s, bez winiety |

Ustawienia zapisują się w goglach i zostają po ponownym uruchomieniu aplikacji.

### Trasa (widok z góry)

```
 z 96   ┌──────────── TABLICA WYNIKÓW ────────────┐
 z 92.6         [ ocena komfortu po próbie ]
 z 88   ═══════════════ META ════════════════
 z 69-85  ~~~~~ woda ~~~ (wyspa) ~~ (wyspa) ~~~~~   5. WYSPY – tylko teleport
 z 67   ═════════════ PUNKT 3 ═══════════════
 z 53-65  ~~~~~ woda ~~~~ ║ kładka ║ ~~~~~~~~~~~   4. KŁADKA – precyzja
 z 51   ═════════════ PUNKT 2 ═══════════════
 z 34-50       rampa ↗ podest ↘ schody           3. RAMPA I SCHODY – zmiana wysokości
 z 32   ═════════════ PUNKT 1 ═══════════════
 z 16-31  |  ▬▬▬▬▬     |     ▬▬▬▬▬  |            2. SLALOM – kierunek ruchu, obrót
 z 4-16   |  linie co 2 m           |            1. PROSTA – prędkość, winieta
 z 4    ═════════════ START ═════════════════
 z 0     [STEROWANIE]   ( TY + kompas )   [JAK TESTOWAĆ]
```

- **Bramki:** żółty pasek = następna bramka do przejścia, zielony = zaliczona. Stoper rusza na bramce START.
- **Woda:** wpadnięcie przenosi do ostatniego punktu kontrolnego (z mrugnięciem). Na wodzie nie da się wylądować teleportem, bo łuk jest wtedy czerwony.
- **Wyspy** są za wysokie, żeby wejść na nie ruchem płynnym. To celowo pokazuje, że teleport pokonuje przeszkody nie do przejścia „pieszo”. W trybie „Płynna” naciśnij **A**.
- **Kompas na starcie** (znaczniki co 30°) pokazuje, o ile obraca obrót skokowy.
- **Meta:** oceń komfort, wskazując jeden z czterech przycisków (bez objawów / lekki dyskomfort / wyraźny dyskomfort / mdłości). Potem **X**, zmień ustawienia i spróbuj ponownie.

### Tablica wyników

Za metą jest lista ostatnich 7 prób (najnowsze u góry). Każda próba ma czas, liczbę teleportów, metry ruchu płynnego, liczbę obrotów skokowych (+ stopnie obrotu płynnego), upadki do wody, ocenę komfortu, datę i skrót użytych ustawień. Wyniki zostają w goglach; usuwa je dopiero odinstalowanie aplikacji.

### Propozycje ćwiczeń

1. **Teleport vs ruch płynny:** przejdź trasę presetem *Maks. komfort*, potem *Swoboda*. Porównaj czas i ocenę komfortu.
2. **Winieta:** na odcinku 1 przejdź prostą z prędkością 4 m/s bez winiety i z silną winietą.
3. **Kierunek ruchu:** na slalomie idź „wg kontrolera”, patrząc w bok, potem „wg głowy”.
4. **Obrót:** kilka razy obróć się w miejscu obrotem płynnym 45°/s, a potem skokowym 45°. Który jest przyjemniejszy?
5. **Przejścia teleportu:** porównaj natychmiast, mrugnięcie i dash na wyspach.
6. **Tryb siedzący:** usiądź, włącz **Y** i przejdź trasę.

> **Bezpieczeństwo:** choroba symulatorowa narasta z czasem i może utrzymywać się po zdjęciu gogli. Przy pierwszych objawach (pocenie, ślinienie, mdłości, zawroty głowy) przerwij test i odpocznij. Testy z ruchem płynnym bez winiety najlepiej robić krótko i na końcu.

---

## 9. Jak to działa

### 9.1 Architektura

```
OVRManager ──► OVRInput.Update() co klatkę
                    │
   ┌────────────────┼──────────────────────────────┬───────────────────────┐
   ▼                ▼                              ▼                       ▼
ComfortMenu    TeleportPointer               LocomotionController     CourseTimer
☰ menu, A/B/X/Y  prawa gałka ↑ → łuk         lewa gałka → ruch        bramki, woda,
   │             puść → TeleportTo() ──────► prawa ← → → obrót         statystyki, wyniki
   │                                          CharacterController
   ▼                                          LinearSpeed / AngularSpeed
ComfortSettings ◄──── odczyt przez wszystkie skrypty     │
(zapis PlayerPrefs)                                       ▼
                                          ComfortVignette (winieta), ScreenFader (mrugnięcie)
```

### 9.2 Gracz = OVRCameraRig + CharacterController
Korzeń `OVRCameraRig` stoi na podłodze (*Floor Level*). `LocomotionController` co klatkę ustawia kapsułę `CharacterController` **pod głową** (środek kapsuły = pozycja głowy w poziomie, wysokość = od podłogi do oczu + 10 cm). Dzięki temu kolizje działają także wtedy, gdy gracz chodzi fizycznie po pokoju. Ruch płynny i grawitacja idą przez jedno wywołanie `CharacterController.Move`, więc gracz wchodzi na rampę i schody (stopień do 30 cm, zbocze do 40°) i spada z krawędzi.

Teleport i obrót zmieniają pozycję rigu bezpośrednio. Przy `Physics.autoSyncTransforms = false` (domyślnie) `CharacterController` przy następnym `Move` przywróciłby starą pozycję, dlatego metoda `SetRigPose` na moment wyłącza kontroler.

Rig jest na warstwie **Ignore Raycast**, więc łuk teleportu i wskaźnik menu nie trafiają we własną kapsułę gracza.

### 9.3 Ruch płynny
```csharp
Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
float amount = Mathf.InverseLerp(deadZone, 1f, stick.magnitude);       // martwa strefa z przeskalowaniem
Transform basis = kierunekWgGlowy ? centerEyeAnchor : leftControllerAnchor;
Vector3 move = (Flat(basis.forward) * stick.y + Flat(basis.right) * stick.x) * amount * speed;
```
`Flat()` rzutuje kierunek na płaszczyznę poziomą, żeby patrzenie w górę nie spowalniało ruchu.

### 9.4 Obrót
Obrót zawsze odbywa się **wokół głowy** (`RotateAroundHead`), a nie wokół środka rigu, więc gracz nie „przesuwa się” przy obrocie, gdy stoi z boku pokoju.
- **Skokowy:** po wychyleniu gałki ponad 0,7 obrót o stały kąt; kolejny dopiero po powrocie gałki do środka. Opcjonalnie z mrugnięciem.
- **Płynny:** kąt = wychylenie × prędkość × `Time.deltaTime`.
- Gdy prawa gałka celuje teleportem (wychylenie bardziej w przód niż w bok), obrót jest wstrzymany.

### 9.5 Teleport
`TeleportPointer` rysuje **łuk balistyczny**: punkt startowy w kontrolerze, prędkość 9 m/s wzdłuż jego osi, grawitacja; co 25 ms `Physics.Raycast` po odcinku łuku. Cel jest poprawny (łuk niebieski), gdy:
1. powierzchnia jest pochylona najwyżej o **30°**,
2. obiekt nie ma komponentu `TeleportBlocker` (woda),
3. `Physics.CheckCapsule` nad punktem nie wykrywa przeszkody (miejsce na gracza, 1,5 m).

Inaczej łuk jest czerwony, a puszczenie gałki daje tylko krótki dźwięk. Zmiana na poprawny cel daje lekką wibrację.

`LocomotionController.TeleportTo` przestawia rig tak, żeby **głowa** wypadła nad celem, bo gracz nie zawsze stoi w środku swojego pokoju. Przejścia:
- **natychmiast:** obraz przeskakuje,
- **mrugnięcie:** `ScreenFader` zaciemnia obraz w 0,08 s, rig się przestawia, obraz rozjaśnia się w 0,18 s,
- **dash:** szybki przelot (18 m/s, maks. 0,35 s) z płynnym startem i hamowaniem.

### 9.6 Winieta komfortu (tunneling)
Ruch obrazu na peryferiach siatkówki najsilniej wywołuje wrażenie ruchu własnego (wekcja). Gdy błędnik go nie potwierdza, pojawia się konflikt sensoryczny i ryzyko choroby symulatorowej. Winieta zakrywa peryferia tylko wtedy, gdy porusza nas sztuczna lokomocja.

`ComfortVignette` buduje w kodzie **pierścień** (48 segmentów, 3 okręgi) tuż przed oczami, przypięty do `CenterEyeAnchor`. Wewnętrzna krawędź jest przezroczysta, zewnętrzna czarna, a przejście daje tekstura-gradient. Siła winiety:
```
siła = poziom (0 / 0,35 / 0,6 / 0,85) × min(1, max(prędkość / 1,5 m/s, prędkość obrotu / 60 °/s))
kąt otwarcia = lerp(75°, 18°, siła)
```
`LocomotionController` mierzy tylko **sztuczny** ruch, czyli przesunięcia i obroty rigu, a nie ruchy głowy. Natychmiastowe skoki (teleport, obrót skokowy) są pomijane, bo nie są ruchem widzianym przez oko. Winieta zamyka się szybko (4/s) i otwiera wolniej (2/s), żeby nie migała.

Materiał nakładek (`Materials/ComfortOverlay.mat`, URP Unlit, przezroczysty, kolejka *Overlay*, bez zapisu głębi) jest assetem tworzonym przez generator. Dzięki temu wariant shadera z przezroczystością trafia do buildu.

### 9.7 Tryb siedzący
Przesuwa `TrackingSpace` rigu o 45 cm w górę. Oczy osoby siedzącej znajdują się wtedy mniej więcej na wysokości oczu osoby stojącej, a kapsuła kolizji odpowiednio rośnie.

### 9.8 Trasa i wyniki
Bramki i woda nie używają triggerów. `CourseTimer` co klatkę bierze punkt pod głową na poziomie stóp i sprawdza:
- czy odcinek od poprzedniej klatki **przeciął linię następnej bramki** w kierunku do przodu (działa też przy teleporcie „nad” bramką),
- czy stopy są w prostopadłościanie `HazardZone` (woda), co powoduje powrót do ostatniego punktu z mrugnięciem.

Statystyki próby to różnice liczników `LocomotionController` między startem a metą. Lista wyników jest zapisywana jako JSON w `PlayerPrefs` (`loco.v1.results`).

### 9.9 Menu w VR bez Interaction SDK
Panel menu to world-space Canvas. Każdy przycisk ma `Image`, `BoxCollider` ustawiony jako trigger i wysunięty przed panel oraz komponent `MenuButton`. `UiPointer` robi `Physics.Raycast` z prawego kontrolera z `QueryTriggerInteraction.Collide`, podświetla trafiony przycisk i klika go triggerem. Łuk teleportu używa `QueryTriggerInteraction.Ignore`, więc menu go nie blokuje.

---

## 10. Dostrajanie i rozbudowa

Parametry są w Inspectorze obiektu `Player`. **Uwaga:** „Generuj scenę z trasą” tworzy obiekty `Player`, `Course` i `UI` od nowa, więc trwałe zmiany wpisuj w kodzie (domyślne wartości pól albo `VRLocomotionSceneBuilder.cs`).

| Co | Gdzie |
|---|---|
| Domyślne ustawienia przy pierwszym uruchomieniu | `Player` › **Comfort Settings** (później obowiązują wartości zapisane w goglach) |
| Dostępne prędkości, kąty, prędkości obrotu | tablice `Speeds`, `SnapAngles`, `TurnSpeeds` w `ComfortSettings.cs` |
| Martwa strefa gałek, próg obrotu skokowego, grawitacja, rozmiar kapsuły | **Locomotion Controller** |
| Czasy mrugnięcia, prędkość dash | **Locomotion Controller** › *Blink Out/In Time*, *Dash Speed* |
| Zasięg łuku, maks. nachylenie celu, korekta kąta łuku | **Teleport Pointer** › *Launch Speed*, *Max Slope*, *Aim Euler* |
| Kształt i reakcja winiety | **Comfort Vignette** › *Open/Closed Angle*, *Feather*, *Level Strength*, *Full At Speed* |
| Odległość menu, wysokość | **Comfort Menu** › *Distance*, *Height Offset* |
| Wibracje (lub ich wyłączenie) | **Haptics** › *Master Amplitude* |
| Kolory | `Assets/VRLocomotion/Materials` |

**Nowa opcja w menu:** dodaj pole w `ComfortSettings`, nazwę i opis w `OptionNames` / `OptionHints`, obsługę w `OptionValue`, `Step`, `Load` i `Save`, zwiększ `OptionCount` i wygeneruj scenę ponownie, bo generator tworzy tyle wierszy menu, ile jest opcji.

**Nowy odcinek trasy:** w `VRLocomotionSceneBuilder.BuildEnvironment` zbuduj przeszkody, w `BuildCourse` dodaj bramkę `Gate(...)` na właściwej pozycji listy i tablicę `Sign(...)`, a jeśli jest woda, także `Water(...)` i `Hazard(...)`.

**Pomysły na rozszerzenie:**
- teleport z wyborem kierunku patrzenia (obrót gałką w trakcie celowania),
- „wspinaczka”: chwytanie uchwytów gripem i przesuwanie rigu ręką,
- ruch „ramionami” (arm swinger): prędkość z ruchu kontrolerów w górę i w dół,
- ankieta SSQ (Simulator Sickness Questionnaire) zamiast czterostopniowej oceny,
- eksport wyników do pliku CSV (`Application.persistentDataPath`) do analizy w arkuszu.

---

## 11. Rozwiązywanie problemów

Zacznij od **Panel kontrolny › 1. Kontrola projektu**. Wskazuje większość problemów i ma przyciski naprawy.

| Problem | Rozwiązanie |
|---|---|
| Brak menu **VR Lokomocja** | Błędy kompilacji. Sprawdź Console; zwykle to brak pakietu Meta XR Core (Package Manager). |
| Gogle niewidoczne / „unauthorized” | Kabel z transmisją danych, tryb dewelopera, potwierdzenie „Zezwól na debugowanie USB” w goglach; na Windows sterownik z Meta Quest Developer Hub. |
| Aplikacja startuje jako płaskie okno 2D | Nie włączono OpenXR + Meta XR Feature (Panel › 1) albo brak `OVRCameraRig`. Uruchom Project Setup Tool › Fix All. |
| Czarny ekran / błędy URP | Panel › 1 › URP *Ustaw*, potem Project Setup Tool › Fix All. |
| Jestem pod podłogą / za wysoko | Panel › 1 › *Tracking Origin = Floor Level*; w goglach ustaw na nowo granicę i wysokość podłogi. Sprawdź, czy nie jest włączony tryb siedzący (**Y**). |
| Ruch płynny nie działa | Tryb „Teleport” – naciśnij **A**. Albo otwarte menu (ruch jest wtedy zablokowany) – zamknij przyciskiem Menu. |
| Gracz „przykleja się” do ścian, nie wchodzi na rampę | Kapsuła ma pod sobą przeszkodę albo za mały *Step Offset*. Sprawdź **Locomotion Controller** i czy rig ma `CharacterController`. |
| Teleport „przesuwa się” po starcie | Rig był przestawiony w scenie. Wygeneruj scenę ponownie (rig wraca na start). |
| Łuk wychodzi za nisko / za wysoko | **Teleport Pointer** › *Aim Euler* (np. X = −20, żeby celować wyżej). To samo dla wskaźnika menu w **Ui Pointer**. |
| Winieta niewidoczna | Poziom „wył.” albo stoisz w miejscu (winieta działa tylko w czasie sztucznego ruchu). Naciśnij **B**, żeby zobaczyć podgląd. Jeśli nadal nic, sprawdź, czy `ComfortOverlay.mat` ma *Surface Type = Transparent*. |
| Menu otwiera się w dziwnym miejscu | Otwiera się 70 cm przed głową, w kierunku patrzenia. Zamknij i otwórz ponownie, patrząc przed siebie. |
| Wyniki zniknęły | Odinstalowanie aplikacji (także *Odinstaluj* w Panelu › 3) usuwa `PlayerPrefs`. |
| Patch And Run się nie udaje | Zrób raz pełne **Build And Run**, potem patchuj. |

---

### Przydatne linki
- [Meta – konfiguracja projektu Unity](https://developers.meta.com/horizon/documentation/unity/unity-project-setup/)
- [Meta – OVRInput](https://developers.meta.com/horizon/documentation/unity/unity-ovrinput/)
- [Meta – wytyczne projektowe: lokomocja (przegląd)](https://developers.meta.com/horizon/design/locomotion-overview/)
- [Meta – komfort i użyteczność lokomocji](https://developers.meta.com/horizon/design/locomotion-comfort-usability/)
- [Meta – dobre praktyki lokomocji](https://developers.meta.com/horizon/design/locomotion-best-practices/)
- [Unity – CharacterController](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/CharacterController.html)
- [Unity – Android application patching](https://docs.unity3d.com/Manual/android-AppPatching.html)
