using UnityEngine;

namespace VRLocomotion
{
    /// <summary>
    /// Strefa „wody”: gdy stopy gracza znajdą się w tym prostopadłościanie, <see cref="CourseTimer"/>
    /// przenosi go do ostatniego punktu kontrolnego. Sprawdzane geometrycznie (bez triggerów).
    /// </summary>
    public class HazardZone : MonoBehaviour
    {
        [Tooltip("Rozmiar strefy (m), środek w pozycji obiektu, bez obrotu.")]
        public Vector3 size = new Vector3(10f, 0.4f, 10f);

        public bool Contains(Vector3 point)
        {
            Vector3 d = point - transform.position;
            return Mathf.Abs(d.x) <= size.x * 0.5f && Mathf.Abs(d.y) <= size.y * 0.5f && Mathf.Abs(d.z) <= size.z * 0.5f;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.35f);
            Gizmos.DrawCube(transform.position, size);
        }
    }
}
