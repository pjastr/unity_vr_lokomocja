using UnityEngine;
using UnityEngine.UI;

namespace VRLocomotion
{
    /// <summary>
    /// Menu komfortu w goglach (OVRInput):
    ///  - ☰ (lewy kontroler) - otwiera / zamyka menu przed graczem; w czasie otwarcia ruch jest zablokowany,
    ///  - promień z prawego kontrolera + trigger - przyciski „&lt;” „&gt;” przy każdej opcji i presety,
    /// Skróty działające bez menu:
    ///  - A - następny tryb lokomocji,  B - następny poziom winiety,
    ///  - Y - tryb siedzący wł./wył.,    X - powrót na start (nowa próba).
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class ComfortMenu : MonoBehaviour
    {
        public static ComfortMenu Instance { get; private set; }

        [Header("Panel menu (ustawia generator sceny)")]
        public GameObject panel;
        public Text[] labelTexts;
        public Text[] valueTexts;
        public Text hintText;
        public ComfortVignette vignette;

        [Header("Położenie po otwarciu")]
        public float distance = 0.7f;
        public float heightOffset = -0.12f;

        [Header("Kolory")]
        public Color labelColor = Color.white;
        public Color inactiveColor = new Color(1f, 1f, 1f, 0.35f);

        public bool IsOpen { get; private set; }

        const string DefaultHint = "Celuj prawym kontrolerem, trigger = wybór. Przycisk Menu na lewym kontrolerze zamyka menu.";

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (ComfortSettings.Instance != null) ComfortSettings.Instance.Changed -= Refresh;
        }

        void Start()
        {
            if (ComfortSettings.Instance != null) ComfortSettings.Instance.Changed += Refresh;
            Sfx.Warmup();
            SetOpen(false, true);
            Refresh();
        }

        void Update()
        {
            if (OVRInput.GetDown(OVRInput.Button.Start, OVRInput.Controller.LTouch))
            {
                SetOpen(!IsOpen, false);
                return;
            }
            if (!IsOpen) QuickKeys();
        }

        void QuickKeys()
        {
            var s = ComfortSettings.Instance;
            if (s == null) return;

            if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch)) // A
                QuickStep(s, ComfortSettings.OptMoveMode);
            else if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch)) // B
            {
                QuickStep(s, ComfortSettings.OptVignette);
                if (vignette != null) vignette.Preview(1.2f);
            }
            else if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch)) // Y
                QuickStep(s, ComfortSettings.OptSeated);
            else if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch)) // X
            {
                if (CourseTimer.Instance != null) CourseTimer.Instance.ResetRun();
                else if (LocomotionController.Instance != null) LocomotionController.Instance.ResetToStart();
            }
        }

        void QuickStep(ComfortSettings s, int option)
        {
            s.Step(option, 1);
            Sfx.Play(Sfx.Toggle, 0.5f);
            Toast.Show(ComfortSettings.OptionName(option) + ": <b>" + s.OptionValue(option) + "</b>");
        }

        public void SetOpen(bool open, bool silent)
        {
            IsOpen = open;
            var loco = LocomotionController.Instance;
            if (loco != null) loco.InputLocked = open;
            if (panel == null) return;

            if (open)
            {
                Transform head = LocoRig.Head;
                if (head != null)
                {
                    Vector3 fwd = LocoRig.Flat(head.forward);
                    panel.transform.SetPositionAndRotation(head.position + fwd * distance + Vector3.up * heightOffset,
                        Quaternion.LookRotation(fwd, Vector3.up));
                    var rig = LocoRig.Rig;
                    if (rig != null) panel.transform.SetParent(rig.transform, true); // menu jedzie razem z graczem
                }
                SetHint(DefaultHint);
                Refresh();
            }
            panel.SetActive(open);
            if (!silent) Sfx.Play(open ? Sfx.Toggle : Sfx.Click, 0.5f);
        }

        public void OnButton(MenuButton b)
        {
            var s = ComfortSettings.Instance;
            if (s == null) return;
            Sfx.Play(Sfx.Click, 0.6f);

            if (b.closesMenu)
            {
                SetOpen(false, true);
                return;
            }
            if (b.preset >= 0)
            {
                s.ApplyPreset(b.preset);
                SetHint("<b>" + ComfortSettings.PresetNames[Mathf.Clamp(b.preset, 0, ComfortSettings.PresetNames.Length - 1)] +
                        "</b>\n" + ComfortSettings.PresetHint(b.preset));
                if (vignette != null) vignette.Preview(1.2f);
                return;
            }
            if (b.option >= 0)
            {
                s.Step(b.option, b.delta >= 0 ? 1 : -1);
                SetHint("<b>" + ComfortSettings.OptionName(b.option) + "</b>\n" + ComfortSettings.OptionHint(b.option));
                if (b.option == ComfortSettings.OptVignette && vignette != null) vignette.Preview(1.5f);
            }
        }

        void SetHint(string text)
        {
            if (hintText != null) hintText.text = text;
        }

        public void Refresh()
        {
            var s = ComfortSettings.Instance;
            if (s == null) return;
            for (int i = 0; i < ComfortSettings.OptionCount; i++)
            {
                bool relevant = s.OptionRelevant(i);
                if (valueTexts != null && i < valueTexts.Length && valueTexts[i] != null)
                {
                    valueTexts[i].text = s.OptionValue(i);
                    valueTexts[i].color = relevant ? labelColor : inactiveColor;
                }
                if (labelTexts != null && i < labelTexts.Length && labelTexts[i] != null)
                    labelTexts[i].color = relevant ? labelColor : inactiveColor;
            }
        }
    }
}
