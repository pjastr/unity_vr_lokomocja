using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace VRLocomotion
{
    /// <summary>
    /// Trasa testowa: bramki (start → punkty kontrolne → meta), strefy wody i tablica wyników.
    /// Każde przejście zapisuje czas, liczbę teleportów, metry ruchu płynnego, obroty, upadki do wody,
    /// użyte ustawienia i ocenę komfortu podaną przez gracza na mecie. Wyniki zostają w goglach (PlayerPrefs),
    /// więc można porównywać konfiguracje także między uruchomieniami.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class CourseTimer : MonoBehaviour
    {
        public static CourseTimer Instance { get; private set; }

        [Header("Trasa (ustawia generator sceny)")]
        [Tooltip("Kolejność: [0] = START, ostatnia = META.")]
        public Checkpoint[] gates;
        public HazardZone[] hazards;
        public Transform startPoint;

        [Header("Tablica wyników i ocena komfortu")]
        public Text resultsText;
        public GameObject ratingPanel;
        [Range(3, 12)] public int maxResults = 7;

        public static readonly string[] RatingNames = { "bez objawów", "lekki dyskomfort", "wyraźny dyskomfort", "mdłości" };
        static readonly string[] RatingColors = { "#7CE07C", "#E0D87C", "#FFB347", "#FF6B6B" };

        enum RunState { Idle, Running, Finished }

        [Serializable]
        class RunResult
        {
            public string date;
            public float time;
            public int teleports;
            public float smoothMeters;
            public int snapTurns;
            public float smoothTurnDegrees;
            public int falls;
            public int rating = -1;
            public string config;
        }

        [Serializable]
        class ResultList
        {
            public List<RunResult> runs = new List<RunResult>();
        }

        const string PrefsKey = "loco.v1.results";

        RunState state;
        int nextGate;
        float startTime, finishTime;
        int teleports0, snaps0, falls;
        float smooth0, turnDeg0;
        Vector3 lastFeet;
        bool hasLast;
        ResultList results = new ResultList();
        RunResult awaitingRating;

        /// <summary>Gracz właśnie skończył trasę i może ocenić komfort (wskaźnik jest wtedy aktywny).</summary>
        public bool AwaitingRating => awaitingRating != null;

        public float Elapsed => state == RunState.Running ? Time.time - startTime : state == RunState.Finished ? finishTime - startTime : 0f;

        public string StatusLine
        {
            get
            {
                switch (state)
                {
                    case RunState.Running:
                        return "Trasa: <b>" + FormatTime(Elapsed) + "</b>  (punkt " + Mathf.Max(0, nextGate - 1) + "/" + Mathf.Max(0, Gates - 2) + ")";
                    case RunState.Finished:
                        return "Meta: <b>" + FormatTime(Elapsed) + "</b>   X = nowa próba";
                    default:
                        return "Trasa: przejdź przez bramkę START";
                }
            }
        }

        int Gates => gates != null ? gates.Length : 0;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            Load();
            ResetGates();
            if (ratingPanel != null) ratingPanel.SetActive(false);
            RefreshBoard();
            Toast.Show("Witaj! Przejdź przez bramkę <b>START</b>.\nPrzycisk Menu (lewy) = ustawienia komfortu.", 5f);
        }

        void Update()
        {
            Vector3 feet;
            if (!LocoRig.TryGetFeet(out feet) || Gates < 2) return;
            var loco = LocomotionController.Instance;

            if (hasLast)
            {
                int idx = state == RunState.Running ? nextGate : 0;
                if (idx < Gates && gates[idx] != null && gates[idx].Crossed(lastFeet, feet)) OnGate(idx);

                if (hazards != null && loco != null && !loco.Busy)
                {
                    foreach (var h in hazards)
                    {
                        if (h == null || !h.Contains(feet)) continue;
                        Respawn();
                        break;
                    }
                }
            }
            lastFeet = feet;
            hasLast = true;
        }

        // ------------------------------------------------------------------ bramki

        void OnGate(int index)
        {
            if (index == 0)
            {
                StartRun();
                return;
            }
            if (index == Gates - 1)
            {
                FinishRun();
                return;
            }
            gates[index].SetState(GateState.Passed);
            nextGate = index + 1;
            gates[nextGate].SetState(GateState.Next);
            Sfx.Play(Sfx.Checkpoint, 0.7f);
            Haptics.PulseBoth(0.4f, 0.08f);
            Toast.Show("Punkt " + index + "/" + (Gates - 2) + "   <b>" + FormatTime(Elapsed) + "</b>", 1.6f);
        }

        void StartRun()
        {
            var loco = LocomotionController.Instance;
            state = RunState.Running;
            startTime = Time.time;
            nextGate = 1;
            falls = 0;
            if (loco != null)
            {
                teleports0 = loco.TeleportCount;
                snaps0 = loco.SnapTurnCount;
                smooth0 = loco.SmoothDistance;
                turnDeg0 = loco.SmoothTurnDegrees;
            }
            awaitingRating = null;
            if (ratingPanel != null) ratingPanel.SetActive(false);

            for (int i = 0; i < Gates; i++) gates[i].SetState(i == 0 ? GateState.Passed : i == 1 ? GateState.Next : GateState.Idle);
            Sfx.Play(Sfx.Start, 0.7f);
            Haptics.PulseBoth(0.5f, 0.1f);
            var s = ComfortSettings.Instance;
            Toast.Show("<b>START!</b>\n" + (s != null ? s.ShortSummary() : ""), 3f);
        }

        void FinishRun()
        {
            var loco = LocomotionController.Instance;
            state = RunState.Finished;
            finishTime = Time.time;
            gates[Gates - 1].SetState(GateState.Passed);

            var s = ComfortSettings.Instance;
            var r = new RunResult
            {
                date = DateTime.Now.ToString("dd.MM HH:mm"),
                time = finishTime - startTime,
                teleports = loco != null ? loco.TeleportCount - teleports0 : 0,
                smoothMeters = loco != null ? loco.SmoothDistance - smooth0 : 0f,
                snapTurns = loco != null ? loco.SnapTurnCount - snaps0 : 0,
                smoothTurnDegrees = loco != null ? loco.SmoothTurnDegrees - turnDeg0 : 0f,
                falls = falls,
                config = s != null ? s.ShortSummary() : "?"
            };
            results.runs.Insert(0, r);
            while (results.runs.Count > maxResults) results.runs.RemoveAt(results.runs.Count - 1);
            Save();

            awaitingRating = r;
            if (ratingPanel != null) ratingPanel.SetActive(true);
            RefreshBoard();
            Sfx.Play(Sfx.Finish, 0.8f);
            Haptics.PulseBoth(0.7f, 0.25f);
            Toast.Show("<b>META!</b>  " + FormatTime(r.time) + "\nOceń komfort na tablicy (celuj prawym kontrolerem, trigger).", 5f);
        }

        /// <summary>Ocena komfortu ostatniego przejścia (0 = bez objawów … 3 = mdłości).</summary>
        public void SetRating(int rating)
        {
            if (awaitingRating == null) return;
            awaitingRating.rating = Mathf.Clamp(rating, 0, RatingNames.Length - 1);
            awaitingRating = null;
            Save();
            if (ratingPanel != null) ratingPanel.SetActive(false);
            RefreshBoard();
            Sfx.Play(Sfx.Toggle, 0.6f);
            Toast.Show("Zapisano: " + RatingNames[Mathf.Clamp(rating, 0, RatingNames.Length - 1)] + "\nX = nowa próba (np. z innymi ustawieniami).", 3f);
        }

        void ResetGates()
        {
            for (int i = 0; i < Gates; i++)
                if (gates[i] != null) gates[i].SetState(i == 0 ? GateState.Next : GateState.Idle);
        }

        /// <summary>Nowa próba: powrót na start, trasa od zera (przycisk X).</summary>
        public void ResetRun()
        {
            state = RunState.Idle;
            nextGate = 0;
            awaitingRating = null;
            if (ratingPanel != null) ratingPanel.SetActive(false);
            ResetGates();
            hasLast = false;
            var loco = LocomotionController.Instance;
            if (loco != null) loco.PlaceAt(startPoint, true);
            Toast.Show("Powrót na start. Przejdź przez bramkę <b>START</b>.", 2.5f);
        }

        void Respawn()
        {
            var loco = LocomotionController.Instance;
            if (loco == null) return;
            Transform point = startPoint;
            if (state == RunState.Running && nextGate - 1 >= 0 && nextGate - 1 < Gates)
                point = gates[nextGate - 1].RespawnPoint;
            falls++;
            loco.PlaceAt(point, true);
            hasLast = false;
            Sfx.Play(Sfx.Splash, 0.8f);
            Haptics.PulseBoth(0.6f, 0.15f);
            Toast.Show("Plusk! Powrót do ostatniego punktu kontrolnego.", 2f);
        }

        // ------------------------------------------------------------------ tablica wyników

        void RefreshBoard()
        {
            if (resultsText == null) return;
            var sb = new StringBuilder(1024);
            sb.Append("<size=64><b>WYNIKI PRÓB</b></size>\n");
            sb.Append("<color=#9AB>najnowsze u góry · zapisywane w goglach</color>\n\n");
            if (results.runs.Count == 0)
            {
                sb.Append("Brak wyników. Przejdź trasę od bramki START do META.\n\n");
                sb.Append("Wskazówka: zrób kilka przejść w różnych ustawieniach (menu: przycisk Menu na lewym kontrolerze)\n");
                sb.Append("i porównaj czas oraz to, jak się czujesz.");
            }
            for (int i = 0; i < results.runs.Count; i++)
            {
                var r = results.runs[i];
                string rating = r.rating >= 0 && r.rating < RatingNames.Length
                    ? "<color=" + RatingColors[r.rating] + ">" + RatingNames[r.rating] + "</color>"
                    : "<color=#888>brak oceny</color>";
                sb.Append("<b>").Append(FormatTime(r.time)).Append("</b>   ");
                sb.Append("teleporty: ").Append(r.teleports);
                sb.Append("   płynnie: ").Append(Mathf.RoundToInt(r.smoothMeters)).Append(" m");
                sb.Append("   obroty: ").Append(r.snapTurns);
                if (r.smoothTurnDegrees > 1f) sb.Append(" + ").Append(Mathf.RoundToInt(r.smoothTurnDegrees)).Append("°");
                sb.Append("   upadki: ").Append(r.falls);
                sb.Append("   komfort: ").Append(rating).Append('\n');
                sb.Append("<size=34><color=#9AB>").Append(r.date).Append("  ·  ").Append(r.config).Append("</color></size>\n");
            }
            resultsText.text = sb.ToString();
        }

        public static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int m = (int)(seconds / 60f);
            float s = seconds - m * 60f;
            return m.ToString("00") + ":" + s.ToString("00.0").Replace('.', ',');
        }

        void Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(PrefsKey, "");
                if (!string.IsNullOrEmpty(json)) results = JsonUtility.FromJson<ResultList>(json) ?? new ResultList();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VRLokomocja] Nie udało się wczytać wyników: " + e.Message);
                results = new ResultList();
            }
            if (results.runs == null) results.runs = new List<RunResult>();
        }

        void Save()
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(results));
            PlayerPrefs.Save();
        }
    }
}
