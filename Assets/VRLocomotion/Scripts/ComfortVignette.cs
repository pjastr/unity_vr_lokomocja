using UnityEngine;

namespace VRLocomotion
{
    /// <summary>
    /// Winieta komfortu (tunneling): w czasie sztucznego ruchu i obrotu peryferia obrazu ciemnieją,
    /// a widoczny pozostaje środek pola widzenia. Siła zależy od ustawienia (słaba / średnia / silna)
    /// i od bieżącej prędkości ruchu - w bezruchu winieta znika.
    ///
    /// Technika: pierścień (siatka generowana w kodzie) tuż przed oczami, przypięty do CenterEyeAnchor.
    /// Wewnętrzna krawędź jest przezroczysta, zewnętrzna czarna - przejście daje tekstura-gradient.
    /// Kąt otwarcia „okna” zmienia się, przesuwając wierzchołki pierścienia.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class ComfortVignette : MonoBehaviour
    {
        public Material overlayMaterial;
        public Color color = Color.black;

        [Header("Kształt (kąty od środka pola widzenia)")]
        [Tooltip("Kąt, od którego zaczyna się ciemnienie przy sile 0.")]
        public float openAngle = 75f;
        [Tooltip("Kąt, od którego zaczyna się ciemnienie przy sile 1.")]
        public float closedAngle = 18f;
        [Tooltip("Szerokość miękkiego przejścia (stopnie).")]
        public float feather = 15f;

        [Header("Siła dla poziomów: wył. / słaba / średnia / silna")]
        public float[] levelStrength = { 0f, 0.35f, 0.6f, 0.85f };

        [Header("Reakcja na ruch")]
        [Tooltip("Prędkość ruchu (m/s), przy której winieta osiąga pełną siłę poziomu.")]
        public float fullAtSpeed = 1.5f;
        [Tooltip("Prędkość obrotu (°/s), przy której winieta osiąga pełną siłę poziomu.")]
        public float fullAtTurnSpeed = 60f;
        public float closeSpeed = 4f;
        public float openSpeed = 2f;

        /// <summary>Bieżąca siła 0..1.</summary>
        public float Strength { get; private set; }

        float previewUntil;
        Mesh mesh;
        MeshRenderer meshRenderer;
        Material material;
        Transform attachedTo;
        Vector3[] vertices;
        float distance;
        float shownStrength = -1f;
        const int Segments = 48;

        /// <summary>Pokazuje winietę przez chwilę bez ruchu (podgląd przy zmianie ustawienia w menu).</summary>
        public void Preview(float seconds = 1.5f) => previewUntil = Time.unscaledTime + seconds;

        void LateUpdate()
        {
            var s = ComfortSettings.Instance;
            var loco = LocomotionController.Instance;
            int level = s != null ? (int)s.vignette : 0;
            float max = levelStrength != null && level < levelStrength.Length ? levelStrength[level] : 0f;

            float motion = 0f;
            if (loco != null)
                motion = Mathf.Max(loco.LinearSpeed / Mathf.Max(0.01f, fullAtSpeed), loco.AngularSpeed / Mathf.Max(0.01f, fullAtTurnSpeed));
            float target = max * Mathf.Clamp01(motion);
            if (Time.unscaledTime < previewUntil) target = max;

            float rate = target > Strength ? closeSpeed : openSpeed;
            Strength = Mathf.MoveTowards(Strength, target, rate * Time.unscaledDeltaTime);

            if (!EnsureMesh()) return;
            bool visible = Strength > 0.01f;
            meshRenderer.enabled = visible;
            if (visible && Mathf.Abs(Strength - shownStrength) > 0.002f) UpdateShape(Strength);
        }

        bool EnsureMesh()
        {
            Transform head = LocoRig.Head;
            if (head == null || overlayMaterial == null) return false;
            if (meshRenderer != null && attachedTo == head) return true;

            if (meshRenderer == null)
            {
                var go = new GameObject("ComfortVignette");
                mesh = new Mesh { name = "VignetteRing" };
                mesh.MarkDynamic();
                vertices = new Vector3[Segments * 3];
                var uv = new Vector2[Segments * 3];
                var tris = new int[Segments * 12];
                for (int i = 0; i < Segments; i++)
                {
                    uv[i] = new Vector2(0f, 0.5f);                // krawędź wewnętrzna - przezroczysta
                    uv[Segments + i] = new Vector2(1f, 0.5f);     // koniec przejścia - czarna
                    uv[2 * Segments + i] = new Vector2(1f, 0.5f); // daleko na zewnątrz - czarna
                }
                int k = 0;
                for (int ring = 0; ring < 2; ring++)
                {
                    for (int i = 0; i < Segments; i++)
                    {
                        int a = ring * Segments + i;
                        int b = ring * Segments + (i + 1) % Segments;
                        int c = (ring + 1) * Segments + i;
                        int d = (ring + 1) * Segments + (i + 1) % Segments;
                        tris[k++] = a; tris[k++] = c; tris[k++] = b;
                        tris[k++] = b; tris[k++] = c; tris[k++] = d;
                    }
                }
                mesh.vertices = vertices;
                mesh.uv = uv;
                mesh.triangles = tris;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                meshRenderer = go.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;

                material = new Material(overlayMaterial);
                var gradient = BuildGradient();
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", gradient);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", gradient);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                meshRenderer.sharedMaterial = material;
            }

            attachedTo = head;
            distance = ScreenFader.OverlayDistance(head, 0.15f);
            meshRenderer.transform.SetParent(head, false);
            meshRenderer.transform.localPosition = Vector3.zero;
            meshRenderer.transform.localRotation = Quaternion.identity;
            meshRenderer.transform.localScale = Vector3.one;
            shownStrength = -1f;
            return true;
        }

        void UpdateShape(float strength)
        {
            float inner = Mathf.Lerp(openAngle, closedAngle, strength);
            float mid = Mathf.Min(inner + feather, 84f);
            inner = Mathf.Min(inner, mid - 1f);
            float rIn = distance * Mathf.Tan(inner * Mathf.Deg2Rad);
            float rMid = distance * Mathf.Tan(mid * Mathf.Deg2Rad);
            float rOut = distance * Mathf.Tan(87f * Mathf.Deg2Rad);

            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 z = new Vector3(0f, 0f, distance);
                vertices[i] = dir * rIn + z;
                vertices[Segments + i] = dir * rMid + z;
                vertices[2 * Segments + i] = dir * rOut + z;
            }
            mesh.vertices = vertices;
            mesh.bounds = new Bounds(new Vector3(0f, 0f, distance), new Vector3(rOut * 2f, rOut * 2f, 0.1f));
            shownStrength = strength;
        }

        static Texture2D BuildGradient()
        {
            const int w = 64;
            var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false)
            {
                name = "VignetteGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[w];
            for (int i = 0; i < w; i++)
            {
                float t = i / (w - 1f);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.SmoothStep(0f, 1f, t) * 255f);
                px[i] = new Color32(255, 255, 255, alpha);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
