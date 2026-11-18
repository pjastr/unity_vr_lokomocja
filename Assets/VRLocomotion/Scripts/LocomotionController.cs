using System;
using System.Collections;
using UnityEngine;

namespace VRLocomotion
{
    /// <summary>
    /// Serce projektu: przesuwa i obraca OVRCameraRig zgodnie z ustawieniami z <see cref="ComfortSettings"/>.
    ///  - ruch płynny lewą gałką (kierunek wg głowy albo lewego kontrolera),
    ///  - obrót prawą gałką: skokowy (opcjonalnie z mrugnięciem), płynny albo wyłączony,
    ///  - wykonanie teleportu (natychmiast / mrugnięcie / dash) - cel wybiera <see cref="TeleportPointer"/>,
    ///  - kolizje i grawitacja: CharacterController na korzeniu rigu, którego kapsuła co klatkę
    ///    podąża za głową (gracz może też chodzić fizycznie po pokoju),
    ///  - tryb siedzący: przesunięcie TrackingSpace w górę.
    /// Mierzy też prędkość sztucznego ruchu i obrotu - na tej podstawie działa winieta.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public class LocomotionController : MonoBehaviour
    {
        public static LocomotionController Instance { get; private set; }

        [Header("Kapsuła gracza (CharacterController na OVRCameraRig)")]
        public float capsuleRadius = 0.2f;
        public float minHeight = 0.6f;
        public float maxHeight = 2.3f;
        [Tooltip("Najwyższy stopień, na który można wejść ruchem płynnym (m).")]
        public float stepOffset = 0.3f;
        [Tooltip("Najbardziej strome zbocze, po którym można iść (stopnie).")]
        public float slopeLimit = 40f;

        [Header("Fizyka")]
        public float gravity = 9.81f;
        public float maxFallSpeed = 15f;

        [Header("Gałki")]
        [Range(0.05f, 0.5f)] public float deadZone = 0.15f;
        [Tooltip("Wychylenie gałki, przy którym następuje obrót skokowy.")]
        [Range(0.3f, 0.95f)] public float snapThreshold = 0.7f;
        [Tooltip("Gałka musi wrócić poniżej tej wartości, zanim możliwy będzie kolejny obrót skokowy.")]
        [Range(0.05f, 0.6f)] public float snapRearm = 0.3f;

        [Header("Przejścia")]
        public float blinkOutTime = 0.08f;
        public float blinkInTime = 0.18f;
        [Tooltip("Prędkość przelotu w trybie Dash (m/s).")]
        public float dashSpeed = 18f;
        public float maxDashTime = 0.35f;

        [Header("Odwołania (ustawia generator sceny)")]
        public Transform startPoint;
        public ScreenFader fader;

        /// <summary>Blokada gałek (np. gdy otwarte jest menu).</summary>
        public bool InputLocked { get; set; }

        /// <summary>Trwa przejście (teleport, mrugnięcie) - wejście jest ignorowane.</summary>
        public bool Busy { get; private set; }

        /// <summary>Prędkość sztucznego ruchu w poziomie (m/s) - bez fizycznego chodzenia.</summary>
        public float LinearSpeed { get; private set; }

        /// <summary>Prędkość sztucznego obrotu (°/s) - bez obrotów głowy.</summary>
        public float AngularSpeed { get; private set; }

        public bool IsGrounded => cc != null && cc.isGrounded;

        // Statystyki (CourseTimer liczy różnice między startem a metą).
        public int TeleportCount { get; private set; }
        public int SnapTurnCount { get; private set; }
        public float SmoothTurnDegrees { get; private set; }
        public float SmoothDistance { get; private set; }

        public event Action<Vector3> Teleported;

        OVRCameraRig rig;
        CharacterController cc;
        float verticalSpeed;
        bool turnArmed = true;
        Vector3 lastRigPos;
        float lastYaw;
        bool hasLast;
        bool skipMotionSample;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        bool EnsureRig()
        {
            if (rig != null && cc != null) return true;
            rig = LocoRig.Rig;
            if (rig == null || rig.centerEyeAnchor == null) return false;

            cc = rig.GetComponent<CharacterController>();
            if (cc == null) cc = rig.gameObject.AddComponent<CharacterController>();
            cc.radius = capsuleRadius;
            cc.stepOffset = stepOffset;
            cc.slopeLimit = slopeLimit;
            cc.skinWidth = 0.02f;
            cc.minMoveDistance = 0f;
            // Rig na warstwie "Ignore Raycast" - promienie teleportu i wskaźnika nie trafiają we własną kapsułę.
            rig.gameObject.layer = 2;
            hasLast = false;
            return true;
        }

        void Update()
        {
            if (!EnsureRig()) return;
            var s = ComfortSettings.Instance;

            ApplySeatedOffset(s);
            UpdateCapsule();

            Vector3 horizontal = Vector3.zero;
            if (!Busy && !InputLocked && s != null)
            {
                horizontal = ReadMove(s);
                HandleTurn(s);
            }

            if (Busy) return;

            // Grawitacja + ruch płynny w jednym wywołaniu Move.
            if (cc.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            else verticalSpeed = Mathf.Max(verticalSpeed - gravity * Time.deltaTime, -maxFallSpeed);

            Vector3 before = rig.transform.position;
            cc.Move((horizontal + Vector3.up * verticalSpeed) * Time.deltaTime);
            if (horizontal.sqrMagnitude > 0f)
            {
                Vector3 d = rig.transform.position - before;
                d.y = 0f;
                SmoothDistance += d.magnitude;
            }
        }

        void LateUpdate()
        {
            if (rig == null) return;
            MeasureMotion();
        }

        // ------------------------------------------------------------------ ruch płynny

        Vector3 ReadMove(ComfortSettings s)
        {
            if (s.moveMode == MoveMode.Teleport) return Vector3.zero;

            Vector2 stick = LocoRig.Stick(HandSide.Left);
            float mag = stick.magnitude;
            if (mag < deadZone) return Vector3.zero;

            // Martwa strefa z przeskalowaniem: ruch zaczyna się płynnie od zera.
            float amount = Mathf.InverseLerp(deadZone, 1f, Mathf.Min(mag, 1f));
            Vector2 dir2 = stick / mag * amount;

            Transform basis = rig.centerEyeAnchor;
            if (s.moveDirection == MoveDirection.Controller && LocoRig.IsConnected(HandSide.Left))
            {
                Transform c = LocoRig.Controller(HandSide.Left);
                if (c != null) basis = c;
            }

            Vector3 forward = LocoRig.Flat(basis.forward);
            Vector3 right = LocoRig.Flat(basis.right);
            return (forward * dir2.y + right * dir2.x) * s.MoveSpeed;
        }

        // ------------------------------------------------------------------ obrót

        void HandleTurn(ComfortSettings s)
        {
            // Gdy prawa gałka celuje teleportem, nie obracamy.
            var pointer = TeleportPointer.Instance;
            if (pointer != null && pointer.IsAiming) { turnArmed = false; return; }

            Vector2 r = LocoRig.Stick(HandSide.Right);
            bool sideways = Mathf.Abs(r.x) > Mathf.Abs(r.y);

            switch (s.turnMode)
            {
                case TurnMode.Snap:
                    if (turnArmed && sideways && Mathf.Abs(r.x) > snapThreshold)
                    {
                        turnArmed = false;
                        float angle = Mathf.Sign(r.x) * s.SnapAngle;
                        SnapTurnCount++;
                        Sfx.Play(Sfx.Turn, 0.35f);
                        if (s.blinkOnSnapTurn && fader != null) StartCoroutine(BlinkTurn(angle));
                        else RotateAroundHead(angle);
                    }
                    else if (r.magnitude < snapRearm)
                    {
                        turnArmed = true;
                    }
                    break;

                case TurnMode.Smooth:
                    if (sideways && Mathf.Abs(r.x) > deadZone)
                    {
                        float amount = Mathf.InverseLerp(deadZone, 1f, Mathf.Abs(r.x)) * Mathf.Sign(r.x);
                        float angle = amount * s.TurnSpeed * Time.deltaTime;
                        RotateAroundHead(angle);
                        SmoothTurnDegrees += Mathf.Abs(angle);
                    }
                    turnArmed = true;
                    break;

                default:
                    turnArmed = true;
                    break;
            }
        }

        IEnumerator BlinkTurn(float angle)
        {
            Busy = true;
            yield return fader.Fade(1f, blinkOutTime * 0.75f);
            RotateAroundHead(angle);
            yield return null;
            yield return fader.Fade(0f, blinkInTime * 0.75f);
            Busy = false;
        }

        /// <summary>Obrót rigu wokół pionowej osi przechodzącej przez głowę (głowa zostaje w miejscu).</summary>
        public void RotateAroundHead(float angle)
        {
            if (!EnsureRig()) return;
            Transform t = rig.transform;
            Vector3 pivot = rig.centerEyeAnchor.position;
            SetRigPose(Quaternion.AngleAxis(angle, Vector3.up) * (t.position - pivot) + pivot,
                Quaternion.AngleAxis(angle, Vector3.up) * t.rotation);
        }

        // ------------------------------------------------------------------ teleport i przestawianie gracza

        /// <summary>Teleport z bieżącym przejściem z ustawień. floorPoint = punkt na podłodze, w którym staną stopy.</summary>
        public bool TeleportTo(Vector3 floorPoint)
        {
            if (Busy || !EnsureRig()) return false;
            var s = ComfortSettings.Instance;
            TeleportTransition tr = s != null ? s.teleportTransition : TeleportTransition.Instant;
            StartCoroutine(TeleportRoutine(floorPoint, null, tr, true));
            return true;
        }

        /// <summary>Ustawia gracza w punkcie (np. start, punkt kontrolny) i obraca twarzą w podanym kierunku.</summary>
        public void PlaceAt(Transform point, bool blink = true)
        {
            if (point == null || !EnsureRig()) return;
            if (Busy) StopAllCoroutines();
            StartCoroutine(TeleportRoutine(point.position, point.forward,
                blink ? TeleportTransition.Blink : TeleportTransition.Instant, false));
        }

        public void ResetToStart() => PlaceAt(startPoint, true);

        IEnumerator TeleportRoutine(Vector3 floorPoint, Vector3? facing, TeleportTransition transition, bool countAsTeleport)
        {
            Busy = true;
            bool blink = transition == TeleportTransition.Blink && fader != null;
            if (blink) yield return fader.Fade(1f, blinkOutTime);

            if (transition == TeleportTransition.Dash)
            {
                Vector3 from = rig.transform.position;
                Vector3 to = RigPositionFor(floorPoint);
                float duration = Mathf.Clamp(Vector3.Distance(from, to) / Mathf.Max(1f, dashSpeed), 0.06f, maxDashTime);
                for (float t = 0f; t < duration; t += Time.deltaTime)
                {
                    // Pozycję docelową liczymy co klatkę - gracz może w tym czasie poruszyć głową.
                    to = RigPositionFor(floorPoint);
                    SetRigPose(Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration)), rig.transform.rotation);
                    yield return null;
                }
            }

            if (facing.HasValue)
            {
                float yaw = Vector3.SignedAngle(LocoRig.Flat(rig.centerEyeAnchor.forward), LocoRig.Flat(facing.Value), Vector3.up);
                RotateAroundHead(yaw);
            }
            SetRigPose(RigPositionFor(floorPoint), rig.transform.rotation);
            verticalSpeed = 0f;
            skipMotionSample = transition != TeleportTransition.Dash;

            if (countAsTeleport) TeleportCount++;
            if (Teleported != null) Teleported(floorPoint);

            if (blink)
            {
                yield return null; // jedna klatka w ciemności po przestawieniu
                yield return fader.Fade(0f, blinkInTime);
            }
            Busy = false;
        }

        /// <summary>Pozycja korzenia rigu, przy której głowa (w poziomie) i stopy (w pionie) wypadną w floorPoint.</summary>
        Vector3 RigPositionFor(Vector3 floorPoint)
        {
            Vector3 p = rig.transform.position;
            Vector3 head = rig.centerEyeAnchor.position;
            return new Vector3(p.x + floorPoint.x - head.x, floorPoint.y + 0.01f, p.z + floorPoint.z - head.z);
        }

        /// <summary>
        /// Przestawienie rigu z wyłączonym CharacterControllerem - inaczej kontroler przy najbliższym Move
        /// przywróciłby starą pozycję (Physics.autoSyncTransforms jest domyślnie wyłączone).
        /// </summary>
        void SetRigPose(Vector3 position, Quaternion rotation)
        {
            bool was = cc != null && cc.enabled;
            if (was) cc.enabled = false;
            rig.transform.SetPositionAndRotation(position, rotation);
            if (was) cc.enabled = true;
        }

        // ------------------------------------------------------------------ kapsuła, tryb siedzący

        void ApplySeatedOffset(ComfortSettings s)
        {
            Transform space = rig.trackingSpace;
            if (space == null) return;
            float wanted = s != null && s.seated ? s.seatedHeightOffset : 0f;
            Vector3 lp = space.localPosition;
            if (Mathf.Abs(lp.y - wanted) > 0.001f) space.localPosition = new Vector3(lp.x, wanted, lp.z);
        }

        /// <summary>Kapsuła stoi pod głową i sięga od podłogi do oczu (+10 cm).</summary>
        void UpdateCapsule()
        {
            Vector3 local = rig.transform.InverseTransformPoint(rig.centerEyeAnchor.position);
            float height = Mathf.Clamp(local.y + 0.1f, Mathf.Max(minHeight, capsuleRadius * 2f + 0.01f), maxHeight);
            cc.height = height;
            cc.center = new Vector3(local.x, height * 0.5f + cc.skinWidth, local.z);
        }

        // ------------------------------------------------------------------ pomiar ruchu (dla winiety)

        void MeasureMotion()
        {
            Vector3 p = rig.transform.position;
            float yaw = rig.transform.eulerAngles.y;
            float dt = Time.deltaTime;

            if (hasLast && dt > 0f)
            {
                Vector3 d = p - lastRigPos;
                d.y = 0f;
                float turn = Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));

                // Natychmiastowe skoki (teleport, obrót skokowy) to nie "ruch widoczny dla oka" - winieta ich nie dotyczy.
                bool jump = skipMotionSample || d.magnitude > 1f;
                LinearSpeed = jump ? 0f : d.magnitude / dt;
                AngularSpeed = turn > 15f ? 0f : turn / dt;
            }
            skipMotionSample = false;
            lastRigPos = p;
            lastYaw = yaw;
            hasLast = true;
        }
    }
}
