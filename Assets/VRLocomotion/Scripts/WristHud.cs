using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace VRLocomotion
{
    /// <summary>
    /// Mały wyświetlacz nad lewym kontrolerem: bieżące ustawienia, prędkość sztucznego ruchu,
    /// siła winiety i czas przejścia trasy. Wystarczy unieść lewą rękę, żeby go odczytać.
    /// </summary>
    public class WristHud : MonoBehaviour
    {
        public Text text;
        public ComfortVignette vignette;
        public float refreshInterval = 0.1f;

        float next;
        readonly StringBuilder sb = new StringBuilder(256);

        void Update()
        {
            if (text == null || Time.unscaledTime < next) return;
            next = Time.unscaledTime + refreshInterval;

            var s = ComfortSettings.Instance;
            var loco = LocomotionController.Instance;
            var course = CourseTimer.Instance;
            sb.Length = 0;

            if (s != null)
            {
                sb.Append("<b>").Append(ComfortSettings.Name(s.moveMode)).Append("</b>");
                if (s.moveMode != MoveMode.Teleport) sb.Append("  ").Append(s.OptionValue(ComfortSettings.OptSpeed));
                if (s.seated) sb.Append("  <color=#9CF>siedząc</color>");
                sb.Append('\n');
                sb.Append("Obrót: ").Append(ComfortSettings.Name(s.turnMode));
                if (s.turnMode == TurnMode.Snap) sb.Append(' ').Append(s.OptionValue(ComfortSettings.OptSnapAngle));
                else if (s.turnMode == TurnMode.Smooth) sb.Append(' ').Append(s.OptionValue(ComfortSettings.OptTurnSpeed));
                sb.Append("   Winieta: ").Append(ComfortSettings.Name(s.vignette)).Append('\n');
            }
            if (loco != null)
            {
                sb.Append("Ruch: ").Append(loco.LinearSpeed.ToString("0.0").Replace('.', ',')).Append(" m/s  ");
                sb.Append("Obrót: ").Append(loco.AngularSpeed.ToString("0")).Append("°/s");
                if (vignette != null && vignette.Strength > 0.01f)
                    sb.Append("  <color=#FC6>W ").Append(Mathf.RoundToInt(vignette.Strength * 100f)).Append("%</color>");
                sb.Append('\n');
            }
            if (course != null) sb.Append(course.StatusLine);

            text.text = sb.ToString();
        }
    }
}
