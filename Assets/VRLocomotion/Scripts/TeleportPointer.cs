using UnityEngine;

namespace VRLocomotion
{
    /// <summary>
    /// Celowanie teleportem łukiem balistycznym (OVRInput):
    ///  - prawa gałka w przód (w trybie „Teleport” także lewa) - pojawia się łuk i znacznik celu,
    ///  - puszczenie gałki - skok (gdy cel jest poprawny), grip - anulowanie.
    /// Cel jest poprawny, gdy powierzchnia jest dość płaska, nie jest oznaczona <see cref="TeleportBlocker"/>
    /// i nad punktem jest miejsce na gracza.
    /// </summary>
    [DefaultExecutionOrder(30)]
    public class TeleportPointer : MonoBehaviour
    {
        public static TeleportPointer Instance { get; private set; }

        [Header("Wizualizacja (ustawia generator sceny)")]
        public LineRenderer arc;
        public Transform marker;
        public Renderer[] markerRenderers;
        public Material validMaterial;
        public Material invalidMaterial;

        [Header("Łuk")]
        [Tooltip("Prędkość początkowa „rzutu” - większa = dalszy zasięg.")]
        public float launchSpeed = 9f;
        public float gravity = 9.81f;
        public float timeStep = 0.025f;
        [Range(10, 200)] public int maxPoints = 90;
        [Tooltip("Korekta kierunku względem kontrolera (stopnie). Np. X = -20, gdy łuk wydaje się za nisko.")]
        public Vector3 aimEuler = Vector3.zero;

        [Header("Warunki celu")]
        [Range(0f, 60f)] public float maxSlope = 30f;
        [Tooltip("Promień sprawdzania wolnego miejsca nad celem.")]
        public float clearanceRadius = 0.15f;
        [Tooltip("Wysokość wolnego miejsca nad celem (m).")]
        public float clearanceHeight = 1.5f;
        [Tooltip("Warstwy trafiane przez łuk. Domyślnie wszystko poza Ignore Raycast (tam jest rig gracza).")]
        public LayerMask mask = ~(1 << 2);

        [Header("Gałka")]
        [Range(0.3f, 0.95f)] public float activateThreshold = 0.6f;
        [Range(0.05f, 0.5f)] public float releaseThreshold = 0.25f;

        public bool IsAiming { get; private set; }
        public bool IsValid { get; private set; }

        HandSide hand;
        Vector3 target;
        Vector3[] points;
        int pointCount;
        bool lastValid;

        void Awake()
        {
            Instance = this;
            points = new Vector3[Mathf.Max(2, maxPoints + 1)];
            Hide();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var s = ComfortSettings.Instance;
            var loco = LocomotionController.Instance;
            bool allowed = s != null && loco != null && s.moveMode != MoveMode.Smooth && !loco.InputLocked && !loco.Busy;
            if (!allowed)
            {
                if (IsAiming) Cancel();
                return;
            }

            if (!IsAiming)
            {
                if (StickForward(HandSide.Right)) Begin(HandSide.Right);
                else if (s.moveMode == MoveMode.Teleport && StickForward(HandSide.Left)) Begin(HandSide.Left);
                if (!IsAiming) return;
            }

            if (!LocoRig.IsConnected(hand) || OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, LocoRig.Mask(hand)))
            {
                Cancel();
                return;
            }

            UpdateArc();

            if (LocoRig.Stick(hand).magnitude < releaseThreshold)
            {
                bool ok = IsValid;
                Vector3 t = target;
                HandSide h = hand;
                Cancel();
                if (ok && loco.TeleportTo(t))
                {
                    Sfx.Play(Sfx.Teleport, 0.6f);
                    Haptics.Pulse(h, 0.5f, 0.08f);
                }
                else
                {
                    Sfx.Play(Sfx.Invalid, 0.4f);
                }
            }
        }

        bool StickForward(HandSide side)
        {
            if (!LocoRig.IsConnected(side)) return false;
            Vector2 v = LocoRig.Stick(side);
            return v.y > activateThreshold && v.y > Mathf.Abs(v.x);
        }

        void Begin(HandSide side)
        {
            hand = side;
            IsAiming = true;
            lastValid = false;
        }

        void Cancel()
        {
            IsAiming = false;
            IsValid = false;
            Hide();
        }

        void Hide()
        {
            if (arc != null) arc.enabled = false;
            if (marker != null) marker.gameObject.SetActive(false);
        }

        void UpdateArc()
        {
            Transform c = LocoRig.Controller(hand);
            if (c == null)
            {
                Cancel();
                return;
            }

            Vector3 p = c.position;
            Vector3 v = c.rotation * Quaternion.Euler(aimEuler) * Vector3.forward * launchSpeed;
            pointCount = 0;
            points[pointCount++] = p;
            IsValid = false;
            bool hitSomething = false;
            RaycastHit hit = default(RaycastHit);

            for (int i = 0; i < maxPoints && pointCount < points.Length; i++)
            {
                Vector3 next = p + v * timeStep;
                v += Vector3.down * (gravity * timeStep);
                Vector3 seg = next - p;
                float len = seg.magnitude;
                if (len > 1e-5f && Physics.Raycast(p, seg / len, out hit, len, mask, QueryTriggerInteraction.Ignore))
                {
                    points[pointCount++] = hit.point;
                    hitSomething = true;
                    break;
                }
                points[pointCount++] = next;
                p = next;
                if (p.y < -30f) break;
            }

            if (hitSomething)
            {
                IsValid = CheckTarget(hit);
                target = hit.point;
            }

            if (arc != null)
            {
                arc.enabled = true;
                arc.positionCount = pointCount;
                for (int i = 0; i < pointCount; i++) arc.SetPosition(i, points[i]);
                Material m = IsValid ? validMaterial : invalidMaterial;
                if (m != null && arc.sharedMaterial != m) arc.sharedMaterial = m;
            }

            if (marker != null)
            {
                marker.gameObject.SetActive(hitSomething);
                if (hitSomething)
                {
                    Vector3 up = IsValid ? Vector3.up : hit.normal;
                    Transform head = LocoRig.Head;
                    Vector3 look = head != null ? LocoRig.Flat(hit.point - head.position) : Vector3.forward;
                    Vector3 fwd = Vector3.ProjectOnPlane(look, up);
                    if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.Cross(up, Vector3.right);
                    marker.SetPositionAndRotation(hit.point + hit.normal * 0.01f, Quaternion.LookRotation(fwd, up));
                    if (markerRenderers != null)
                    {
                        Material m = IsValid ? validMaterial : invalidMaterial;
                        foreach (var r in markerRenderers)
                            if (r != null && m != null && r.sharedMaterial != m) r.sharedMaterial = m;
                    }
                }
            }

            if (IsValid != lastValid)
            {
                if (IsValid) Haptics.Pulse(hand, 0.25f, 0.03f);
                lastValid = IsValid;
            }
        }

        bool CheckTarget(RaycastHit hit)
        {
            if (Vector3.Angle(hit.normal, Vector3.up) > maxSlope) return false;
            if (hit.collider != null && hit.collider.GetComponentInParent<TeleportBlocker>() != null) return false;

            // Wolne miejsce nad celem (dolna kula kapsuły 20 cm nad podłogą - stopnie schodów nie przeszkadzają).
            Vector3 a = hit.point + Vector3.up * (0.2f + clearanceRadius);
            Vector3 b = hit.point + Vector3.up * Mathf.Max(0.3f + clearanceRadius, clearanceHeight - clearanceRadius);
            return !Physics.CheckCapsule(a, b, clearanceRadius, mask, QueryTriggerInteraction.Ignore);
        }
    }
}
