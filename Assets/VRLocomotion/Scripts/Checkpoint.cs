using UnityEngine;
using UnityEngine.UI;

namespace VRLocomotion
{
    public enum GateState { Idle, Next, Passed }

    /// <summary>
    /// Bramka trasy (start, punkt kontrolny, meta). Przejście wykrywa <see cref="CourseTimer"/> geometrycznie:
    /// odcinek między pozycjami gracza z dwóch klatek przecina linię bramki w kierunku jej osi Z.
    /// Dzięki temu działa także przy teleporcie „przez” bramkę - bez triggerów i bez Rigidbody.
    /// </summary>
    public class Checkpoint : MonoBehaviour
    {
        [Tooltip("Połowa szerokości bramki (m).")]
        public float halfWidth = 2f;
        public Renderer[] indicators;
        public Material idleMaterial;
        public Material nextMaterial;
        public Material passedMaterial;
        public Text label;
        [Tooltip("Miejsce powrotu po upadku do wody (za bramką).")]
        public Transform respawnPoint;

        public GateState State { get; private set; }

        public Transform RespawnPoint => respawnPoint != null ? respawnPoint : transform;

        public void SetState(GateState state)
        {
            State = state;
            Material m = state == GateState.Passed ? passedMaterial : state == GateState.Next ? nextMaterial : idleMaterial;
            if (m == null || indicators == null) return;
            foreach (var r in indicators)
                if (r != null) r.sharedMaterial = m;
        }

        /// <summary>Czy ruch z a do b (punkty na poziomie stóp) przeciął bramkę w kierunku do przodu.</summary>
        public bool Crossed(Vector3 a, Vector3 b)
        {
            Vector3 n = LocoRig.Flat(transform.forward);
            Vector3 p0 = transform.position;
            float da = Vector3.Dot(a - p0, n);
            float db = Vector3.Dot(b - p0, n);
            if (!(da < 0f && db >= 0f)) return false;

            float t = da / (da - db);
            Vector3 c = Vector3.Lerp(a, b, t);
            float lateral = Vector3.Dot(c - p0, LocoRig.Flat(transform.right));
            return Mathf.Abs(lateral) <= halfWidth && Mathf.Abs(c.y - p0.y) < 3f;
        }
    }
}
