using System;
using UnityEngine;

namespace VRLocomotion
{
    /// <summary>Sposób przemieszczania się gałkami.</summary>
    public enum MoveMode { Teleport, Smooth, Both }

    /// <summary>Według czego liczony jest kierunek ruchu płynnego.</summary>
    public enum MoveDirection { Head, Controller }

    /// <summary>Obrót prawą gałką.</summary>
    public enum TurnMode { Snap, Smooth, Off }

    /// <summary>Jak wygląda sam skok teleportu.</summary>
    public enum TeleportTransition { Instant, Blink, Dash }

    /// <summary>Siła winiety (zawężenia pola widzenia) w czasie sztucznego ruchu.</summary>
    public enum VignetteLevel { Off, Low, Medium, High }

    /// <summary>
    /// Wszystkie ustawienia lokomocji i komfortu w jednym miejscu.
    /// Menu w goglach (ComfortMenu) zmienia je przez <see cref="Step"/>, a pozostałe skrypty tylko je odczytują.
    /// Ustawienia są zapamiętywane w goglach (PlayerPrefs), więc przetrwają ponowne uruchomienie aplikacji.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ComfortSettings : MonoBehaviour
    {
        public static ComfortSettings Instance { get; private set; }

        public static readonly float[] Speeds = { 1.5f, 2.5f, 4f };
        public static readonly float[] SnapAngles = { 30f, 45f, 90f };
        public static readonly float[] TurnSpeeds = { 45f, 90f, 135f };

        [Header("Ruch")]
        public MoveMode moveMode = MoveMode.Both;
        public MoveDirection moveDirection = MoveDirection.Head;
        [Tooltip("Indeks w tablicy Speeds: 0 = 1,5 m/s, 1 = 2,5 m/s, 2 = 4 m/s.")]
        [Range(0, 2)] public int speedIndex;

        [Header("Obrót")]
        public TurnMode turnMode = TurnMode.Snap;
        [Tooltip("Indeks w tablicy SnapAngles: 0 = 30°, 1 = 45°, 2 = 90°.")]
        [Range(0, 2)] public int snapAngleIndex = 1;
        [Tooltip("Indeks w tablicy TurnSpeeds: 0 = 45°/s, 1 = 90°/s, 2 = 135°/s.")]
        [Range(0, 2)] public int turnSpeedIndex = 1;

        [Header("Komfort")]
        public TeleportTransition teleportTransition = TeleportTransition.Blink;
        public VignetteLevel vignette = VignetteLevel.Medium;
        [Tooltip("Krótkie zaciemnienie obrazu przy obrocie skokowym.")]
        public bool blinkOnSnapTurn;
        [Tooltip("Tryb siedzący: kamera podniesiona o seatedHeightOffset.")]
        public bool seated;
        public float seatedHeightOffset = 0.45f;

        [Header("Zapis")]
        [Tooltip("Zapamiętuj ustawienia w goglach (PlayerPrefs).")]
        public bool persist = true;

        /// <summary>Wywoływane po każdej zmianie ustawień.</summary>
        public event Action Changed;

        public float MoveSpeed => Speeds[Mathf.Clamp(speedIndex, 0, Speeds.Length - 1)];
        public float SnapAngle => SnapAngles[Mathf.Clamp(snapAngleIndex, 0, SnapAngles.Length - 1)];
        public float TurnSpeed => TurnSpeeds[Mathf.Clamp(turnSpeedIndex, 0, TurnSpeeds.Length - 1)];

        // ------------------------------------------------------------------ opcje menu

        public const int OptMoveMode = 0, OptDirection = 1, OptSpeed = 2, OptTurnMode = 3, OptSnapAngle = 4,
            OptTurnSpeed = 5, OptTransition = 6, OptVignette = 7, OptBlinkSnap = 8, OptSeated = 9;

        public const int OptionCount = 10;

        static readonly string[] OptionNames =
        {
            "Lokomocja", "Kierunek ruchu", "Prędkość ruchu", "Obrót", "Kąt obrotu skokowego",
            "Prędkość obrotu płynnego", "Przejście teleportu", "Winieta", "Mrugnięcie przy obrocie", "Tryb siedzący"
        };

        static readonly string[] OptionHints =
        {
            "Teleport: skok w wybrane miejsce, najmniej mdłości. Płynna: ruch gałką jak w grach, bardziej immersyjny, ale łatwiej o chorobę symulatorową. Oba: lewa gałka idzie, prawa w przód celuje teleportem.",
            "Głowa: idziesz tam, gdzie patrzysz. Kontroler: idziesz tam, gdzie wskazuje lewy kontroler – możesz rozglądać się w trakcie ruchu.",
            "Im szybciej, tym większy konflikt między wzrokiem a błędnikiem. 1,5 m/s to mniej więcej szybki marsz.",
            "Skokowy: obraz obraca się od razu o stały kąt (najbardziej komfortowy). Płynny: obraca się w sposób ciągły (bardziej naturalny, mniej komfortowy). Wyłączony: obracasz się fizycznie.",
            "Mniejszy kąt to precyzja, większy to mniej kliknięć. 90° pozwala szybko się odwrócić.",
            "Wolny obrót płynny jest zwykle gorzej tolerowany niż szybki lub skokowy – sprawdź na sobie.",
            "Natychmiast: obraz przeskakuje. Mrugnięcie: krótkie zaciemnienie maskuje skok. Dash: szybki przelot – widać ruch, ale bardzo krótko.",
            "Winieta zawęża pole widzenia w czasie sztucznego ruchu i obrotu. Peryferia oka najsilniej odbierają ruch, więc zakrycie ich zmniejsza mdłości.",
            "Obrót skokowy z krótkim zaciemnieniem – mózg nie widzi „przeskoku” obrazu.",
            "Podnosi kamerę o 45 cm, żeby grać na siedząco z wysokością oczu stojącej osoby."
        };

        public static readonly string[] PresetNames = { "Maks. komfort", "Zrównoważony", "Swoboda" };

        static readonly string[] PresetHints =
        {
            "Maks. komfort: tylko teleport z mrugnięciem, obrót skokowy 45° z mrugnięciem, silna winieta. Dobry na pierwsze minuty w VR.",
            "Zrównoważony: teleport + wolny ruch płynny (1,5 m/s) wg głowy, obrót skokowy 45°, średnia winieta.",
            "Swoboda: tylko ruch płynny 2,5 m/s wg kontrolera, płynny obrót 90°/s, bez winiety. Dla osób odpornych na chorobę symulatorową."
        };

        public static string OptionName(int i) => i >= 0 && i < OptionCount ? OptionNames[i] : "?";
        public static string OptionHint(int i) => i >= 0 && i < OptionCount ? OptionHints[i] : "";
        public static string PresetHint(int i) => i >= 0 && i < PresetHints.Length ? PresetHints[i] : "";

        public string OptionValue(int i)
        {
            switch (i)
            {
                case OptMoveMode: return Name(moveMode);
                case OptDirection: return moveDirection == MoveDirection.Head ? "wg głowy" : "wg kontrolera";
                case OptSpeed: return MoveSpeed.ToString("0.0").Replace('.', ',') + " m/s";
                case OptTurnMode: return Name(turnMode);
                case OptSnapAngle: return SnapAngle.ToString("0") + "°";
                case OptTurnSpeed: return TurnSpeed.ToString("0") + "°/s";
                case OptTransition: return Name(teleportTransition);
                case OptVignette: return Name(vignette);
                case OptBlinkSnap: return blinkOnSnapTurn ? "tak" : "nie";
                case OptSeated: return seated ? "tak (+" + Mathf.RoundToInt(seatedHeightOffset * 100f) + " cm)" : "nie";
                default: return "?";
            }
        }

        /// <summary>Czy opcja ma w tej chwili znaczenie (np. kąt skoku przy obrocie płynnym - nie).</summary>
        public bool OptionRelevant(int i)
        {
            switch (i)
            {
                case OptDirection:
                case OptSpeed: return moveMode != MoveMode.Teleport;
                case OptSnapAngle:
                case OptBlinkSnap: return turnMode == TurnMode.Snap;
                case OptTurnSpeed: return turnMode == TurnMode.Smooth;
                case OptTransition: return moveMode != MoveMode.Smooth;
                default: return true;
            }
        }

        /// <summary>Zmienia opcję i o krok (delta = -1 / +1) z zawijaniem.</summary>
        public void Step(int i, int delta)
        {
            switch (i)
            {
                case OptMoveMode: moveMode = (MoveMode)Wrap((int)moveMode + delta, 3); break;
                case OptDirection: moveDirection = (MoveDirection)Wrap((int)moveDirection + delta, 2); break;
                case OptSpeed: speedIndex = Wrap(speedIndex + delta, Speeds.Length); break;
                case OptTurnMode: turnMode = (TurnMode)Wrap((int)turnMode + delta, 3); break;
                case OptSnapAngle: snapAngleIndex = Wrap(snapAngleIndex + delta, SnapAngles.Length); break;
                case OptTurnSpeed: turnSpeedIndex = Wrap(turnSpeedIndex + delta, TurnSpeeds.Length); break;
                case OptTransition: teleportTransition = (TeleportTransition)Wrap((int)teleportTransition + delta, 3); break;
                case OptVignette: vignette = (VignetteLevel)Wrap((int)vignette + delta, 4); break;
                case OptBlinkSnap: blinkOnSnapTurn = !blinkOnSnapTurn; break;
                case OptSeated: seated = !seated; break;
                default: return;
            }
            NotifyChanged();
        }

        public void ApplyPreset(int preset)
        {
            switch (preset)
            {
                case 0: // maksymalny komfort
                    moveMode = MoveMode.Teleport;
                    teleportTransition = TeleportTransition.Blink;
                    turnMode = TurnMode.Snap;
                    snapAngleIndex = 1;
                    blinkOnSnapTurn = true;
                    vignette = VignetteLevel.High;
                    speedIndex = 0;
                    moveDirection = MoveDirection.Head;
                    break;
                case 1: // zrównoważony
                    moveMode = MoveMode.Both;
                    teleportTransition = TeleportTransition.Blink;
                    turnMode = TurnMode.Snap;
                    snapAngleIndex = 1;
                    blinkOnSnapTurn = false;
                    vignette = VignetteLevel.Medium;
                    speedIndex = 0;
                    moveDirection = MoveDirection.Head;
                    break;
                default: // swoboda
                    moveMode = MoveMode.Smooth;
                    teleportTransition = TeleportTransition.Instant;
                    turnMode = TurnMode.Smooth;
                    turnSpeedIndex = 1;
                    blinkOnSnapTurn = false;
                    vignette = VignetteLevel.Off;
                    speedIndex = 1;
                    moveDirection = MoveDirection.Controller;
                    break;
            }
            NotifyChanged();
        }

        /// <summary>Krótki opis bieżącej konfiguracji (do tablicy wyników i HUD).</summary>
        public string ShortSummary()
        {
            string move;
            switch (moveMode)
            {
                case MoveMode.Teleport: move = "Teleport (" + Name(teleportTransition) + ")"; break;
                case MoveMode.Smooth: move = "Płynna " + OptionValue(OptSpeed); break;
                default: move = "Tel.+płynna " + OptionValue(OptSpeed); break;
            }
            string turn = turnMode == TurnMode.Snap ? "skok " + OptionValue(OptSnapAngle)
                : turnMode == TurnMode.Smooth ? "płynny " + OptionValue(OptTurnSpeed) : "bez obrotu";
            return move + " | " + turn + " | winieta: " + Name(vignette);
        }

        public static string Name(MoveMode m) =>
            m == MoveMode.Teleport ? "Teleport" : m == MoveMode.Smooth ? "Płynna" : "Teleport + płynna";

        public static string Name(TurnMode m) =>
            m == TurnMode.Snap ? "skokowy" : m == TurnMode.Smooth ? "płynny" : "wyłączony";

        public static string Name(TeleportTransition t) =>
            t == TeleportTransition.Instant ? "natychmiast" : t == TeleportTransition.Blink ? "mrugnięcie" : "dash";

        public static string Name(VignetteLevel v) =>
            v == VignetteLevel.Off ? "wył." : v == VignetteLevel.Low ? "słaba" : v == VignetteLevel.Medium ? "średnia" : "silna";

        // ------------------------------------------------------------------ cykl życia i zapis

        void Awake()
        {
            Instance = this;
            if (persist) Load();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void NotifyChanged()
        {
            if (persist) Save();
            if (Changed != null) Changed();
        }

        const string P = "loco.v1.";

        void Load()
        {
            if (!PlayerPrefs.HasKey(P + "moveMode")) return; // pierwsze uruchomienie - wartości z Inspectora
            moveMode = (MoveMode)Wrap(PlayerPrefs.GetInt(P + "moveMode", (int)moveMode), 3);
            moveDirection = (MoveDirection)Wrap(PlayerPrefs.GetInt(P + "moveDirection", (int)moveDirection), 2);
            speedIndex = Wrap(PlayerPrefs.GetInt(P + "speed", speedIndex), Speeds.Length);
            turnMode = (TurnMode)Wrap(PlayerPrefs.GetInt(P + "turnMode", (int)turnMode), 3);
            snapAngleIndex = Wrap(PlayerPrefs.GetInt(P + "snapAngle", snapAngleIndex), SnapAngles.Length);
            turnSpeedIndex = Wrap(PlayerPrefs.GetInt(P + "turnSpeed", turnSpeedIndex), TurnSpeeds.Length);
            teleportTransition = (TeleportTransition)Wrap(PlayerPrefs.GetInt(P + "transition", (int)teleportTransition), 3);
            vignette = (VignetteLevel)Wrap(PlayerPrefs.GetInt(P + "vignette", (int)vignette), 4);
            blinkOnSnapTurn = PlayerPrefs.GetInt(P + "blinkSnap", blinkOnSnapTurn ? 1 : 0) == 1;
            seated = PlayerPrefs.GetInt(P + "seated", seated ? 1 : 0) == 1;
        }

        void Save()
        {
            PlayerPrefs.SetInt(P + "moveMode", (int)moveMode);
            PlayerPrefs.SetInt(P + "moveDirection", (int)moveDirection);
            PlayerPrefs.SetInt(P + "speed", speedIndex);
            PlayerPrefs.SetInt(P + "turnMode", (int)turnMode);
            PlayerPrefs.SetInt(P + "snapAngle", snapAngleIndex);
            PlayerPrefs.SetInt(P + "turnSpeed", turnSpeedIndex);
            PlayerPrefs.SetInt(P + "transition", (int)teleportTransition);
            PlayerPrefs.SetInt(P + "vignette", (int)vignette);
            PlayerPrefs.SetInt(P + "blinkSnap", blinkOnSnapTurn ? 1 : 0);
            PlayerPrefs.SetInt(P + "seated", seated ? 1 : 0);
            PlayerPrefs.Save();
        }

        static int Wrap(int v, int n) => ((v % n) + n) % n;
    }
}
