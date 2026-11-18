using UnityEngine;
using UnityEngine.UI;

namespace VRLocomotion
{
    /// <summary>
    /// Przycisk menu w przestrzeni 3D: obrazek UI + BoxCollider (trigger), w który celuje promień <see cref="UiPointer"/>.
    /// Co robi przycisk, określają pola: opcja + krok, preset, zamknięcie menu albo ocena komfortu na mecie.
    /// </summary>
    public class MenuButton : MonoBehaviour
    {
        [Tooltip("Indeks opcji z ComfortSettings (-1 = brak).")]
        public int option = -1;
        [Tooltip("Krok zmiany opcji: -1 (poprzednia wartość) albo +1 (następna).")]
        public int delta = 1;
        [Tooltip("Indeks presetu (-1 = brak).")]
        public int preset = -1;
        public bool closesMenu;
        [Tooltip("Ocena komfortu na mecie 0..3 (-1 = brak).")]
        public int rating = -1;

        public Image background;
        public Color normalColor = new Color(0.18f, 0.22f, 0.3f, 1f);
        public Color hoverColor = new Color(0.25f, 0.55f, 0.95f, 1f);

        public void SetHover(bool hover)
        {
            if (background != null) background.color = hover ? hoverColor : normalColor;
        }

        public void Click()
        {
            if (rating >= 0)
            {
                if (CourseTimer.Instance != null) CourseTimer.Instance.SetRating(rating);
                return;
            }
            if (ComfortMenu.Instance != null) ComfortMenu.Instance.OnButton(this);
        }
    }
}
