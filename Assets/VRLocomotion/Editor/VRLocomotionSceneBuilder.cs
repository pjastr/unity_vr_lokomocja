using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VRLocomotion.EditorTools
{
    /// <summary>
    /// Generator sceny „tor przeszkód do testowania lokomocji”. Wszystko z prymitywów Unity + OVRCameraRig z Meta XR Core SDK.
    /// Ponowne uruchomienie tworzy od nowa obiekty Environment, Course, Player, UI i światło; rig i Twoje własne obiekty zostają.
    ///
    /// Układ trasy (oś Z = do przodu, start w punkcie 0,0,0):
    ///   z  0      start (kompas na podłodze, tablice ze sterowaniem)
    ///   z  4      bramka START
    ///   z  4-16   1. Prosta z liniami co 2 m
    ///   z 16-31   2. Slalom (ścianki na zmianę)
    ///   z 32      PUNKT 1
    ///   z 34-50   3. Rampa w górę, podest, schody w dół
    ///   z 51      PUNKT 2
    ///   z 53-65   4. Kładka nad wodą
    ///   z 67      PUNKT 3
    ///   z 69-85   5. Wyspy - tylko teleport
    ///   z 88      META, dalej tablica wyników i ocena komfortu
    /// </summary>
    public static class VRLocomotionSceneBuilder
    {
        static readonly string[] GeneratedRoots = { "Directional Light", "Environment", "Course", "Player", "UI" };
        static readonly Color PanelBg = new Color(0.05f, 0.06f, 0.09f, 0.9f);
        static readonly Color SkyColor = new Color(0.62f, 0.74f, 0.88f);
        const string RigPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
        const int IgnoreRaycastLayer = 2;

        static Font font;

        class Mats
        {
            public Material floor, path, startPad, compass, compassMain, line, rail, wall, ramp, platform, stairs;
            public Material water, island, bridge, post, gateIdle, gateNext, gatePassed;
            public Material arcValid, arcInvalid, pointer, overlay, ctrlBody, ctrlLeft, ctrlRight, ctrlTip;
        }

        // =====================================================================

        public static bool GenerateScene(bool interactive, out string info)
        {
            info = "";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            bool exists = File.Exists(VRLocomotionSetup.ScenePath);
            if (exists && interactive && !EditorUtility.DisplayDialog("VR Lokomocja",
                    "Scena już istnieje. Wygenerować ją od nowa?\n\n" +
                    "• Environment, Course, Player, UI i światło zostaną utworzone od nowa (zmiany w nich przepadną).\n" +
                    "• OVRCameraRig i Twoje własne obiekty zostaną.",
                    "Generuj", "Anuluj"))
                return false;

            VRLocomotionSetup.EnsureUrp();
            Mats m = CreateMaterials();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Scene scene;
            if (exists)
            {
                scene = EditorSceneManager.OpenScene(VRLocomotionSetup.ScenePath, OpenSceneMode.Single);
                foreach (var go in scene.GetRootGameObjects())
                    if (GeneratedRoots.Contains(go.name)) Object.DestroyImmediate(go);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            var sb = new StringBuilder();
            OVRCameraRig rig = EnsureRig(sb);
            RemoveStrayCameras(rig, sb);
            var mgr = Object.FindAnyObjectByType<OVRManager>(FindObjectsInactive.Include);
            if (mgr != null && VRLocomotionSetup.SetEnum(mgr, "FloorLevel", "_trackingOriginType", "m_TrackingOriginType", "trackingOriginType"))
                sb.AppendLine("• OVRManager: Tracking Origin = Floor Level");
            SetupRig(rig, sb);

            BuildLighting();
            BuildEnvironment(m);
            var course = BuildCourse(m);
            var ui = BuildUI(m);
            BuildPlayer(m, course, ui);

            VRLocomotionSetup.EnsureFolder(VRLocomotionSetup.Root + "/Scenes");
            EditorSceneManager.SaveScene(scene, VRLocomotionSetup.ScenePath);
            VRLocomotionSetup.AddSceneToBuild();
            sb.AppendLine("• Zapisano " + VRLocomotionSetup.ScenePath + " (dodana do buildu)");
            info = sb.ToString();

            if (interactive)
                EditorUtility.DisplayDialog("VR Lokomocja", "Scena gotowa.\n\n" + info + "\n" + VRLocomotionSetup.ChecklistSummary(), "OK");
            return true;
        }

        // =====================================================================
        // RIG
        // =====================================================================

        static OVRCameraRig EnsureRig(StringBuilder sb)
        {
            var rigs = Object.FindObjectsByType<OVRCameraRig>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (rigs.Length > 0)
            {
                sb.AppendLine("• OVRCameraRig: zachowany istniejący (" + rigs[0].name + ")" +
                              (rigs.Length > 1 ? " – UWAGA: w scenie jest " + rigs.Length + " rigów, zostaw jeden!" : ""));
                return rigs[0];
            }

            GameObject prefab = FindRigPrefab(out string path);
            GameObject go;
            if (prefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                sb.AppendLine("• OVRCameraRig: dodany z Meta XR Core SDK (" + path + ")");
            }
            else
            {
                go = new GameObject("OVRCameraRig");
                go.AddComponent<OVRCameraRig>();
                sb.AppendLine("• [!] Nie znaleziono prefabu OVRCameraRig – utworzono pusty rig. " +
                              "Lepiej: usuń go i dodaj Building Block „Camera Rig” (Meta › Tools › Building Blocks).");
            }
            if (Object.FindAnyObjectByType<OVRManager>(FindObjectsInactive.Include) == null) go.AddComponent<OVRManager>();
            return go.GetComponent<OVRCameraRig>();
        }

        static GameObject FindRigPrefab(out string path)
        {
            path = RigPrefabPath;
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            if (p != null && p.GetComponent<OVRCameraRig>() != null) return p;

            GameObject fallback = null;
            foreach (var guid in AssetDatabase.FindAssets("OVRCameraRig t:Prefab", new[] { "Packages/com.meta.xr.sdk.core" }))
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(candidatePath);
                if (go == null || go.GetComponent<OVRCameraRig>() == null) continue;
                if (go.name == "OVRCameraRig") { path = candidatePath; return go; }
                if (fallback == null) { fallback = go; path = candidatePath; }
            }
            return fallback;
        }

        static void RemoveStrayCameras(OVRCameraRig rig, StringBuilder sb)
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rig != null && cam.transform.IsChildOf(rig.transform)) continue;
                sb.AppendLine("• Usunięto kamerę spoza rigu: " + cam.name);
                Object.DestroyImmediate(cam.gameObject);
            }
        }

        /// <summary>Rig na starcie, CharacterController (kolizje i grawitacja), warstwa Ignore Raycast, kolor nieba.</summary>
        static void SetupRig(OVRCameraRig rig, StringBuilder sb)
        {
            if (rig == null) return;
            rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            rig.gameObject.layer = IgnoreRaycastLayer;

            var cc = rig.GetComponent<CharacterController>();
            if (cc == null) cc = rig.gameObject.AddComponent<CharacterController>();
            cc.radius = 0.2f;
            cc.height = 1.7f;
            cc.center = new Vector3(0f, 0.87f, 0f);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 40f;
            cc.skinWidth = 0.02f;
            cc.minMoveDistance = 0f;

            foreach (var cam in rig.GetComponentsInChildren<Camera>(true))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = SkyColor;
                if (cam.nearClipPlane > 0.05f) cam.nearClipPlane = 0.05f; // nakładki komfortu leżą tuż przed oczami
                EditorUtility.SetDirty(cam);
            }
            EditorUtility.SetDirty(rig.gameObject);
            sb.AppendLine("• Rig: CharacterController (kolizje, grawitacja), warstwa Ignore Raycast, tło w kolorze nieba");
        }

        // =====================================================================
        // MATERIAŁY
        // =====================================================================

        static Mats CreateMaterials()
        {
            VRLocomotionSetup.EnsureFolder(VRLocomotionSetup.MaterialsFolder);
            return new Mats
            {
                floor = Mat("Floor", new Color(0.36f, 0.4f, 0.36f)),
                path = Mat("Path", new Color(0.62f, 0.6f, 0.54f)),
                startPad = Mat("StartPad", new Color(0.25f, 0.55f, 0.9f)),
                compass = Mat("Compass", new Color(0.95f, 0.95f, 0.95f), unlit: true),
                compassMain = Mat("CompassMain", new Color(1f, 0.75f, 0.2f), unlit: true),
                line = Mat("FloorLine", new Color(0.95f, 0.95f, 0.9f)),
                rail = Mat("Rail", new Color(0.85f, 0.45f, 0.15f)),
                wall = Mat("Wall", new Color(0.78f, 0.78f, 0.82f)),
                ramp = Mat("Ramp", new Color(0.9f, 0.62f, 0.25f)),
                platform = Mat("Platform", new Color(0.55f, 0.55f, 0.6f)),
                stairs = Mat("Stairs", new Color(0.8f, 0.55f, 0.3f)),
                water = Mat("Water", new Color(0.12f, 0.4f, 0.75f), 0.85f),
                island = Mat("Island", new Color(0.35f, 0.68f, 0.3f)),
                bridge = Mat("Bridge", new Color(0.55f, 0.38f, 0.22f)),
                post = Mat("Post", new Color(0.25f, 0.27f, 0.3f)),
                gateIdle = Mat("GateIdle", new Color(0.85f, 0.85f, 0.85f), unlit: true),
                gateNext = Mat("GateNext", new Color(1f, 0.8f, 0.15f), unlit: true),
                gatePassed = Mat("GatePassed", new Color(0.25f, 0.9f, 0.35f), unlit: true),
                arcValid = Mat("TeleportValid", new Color(0.2f, 0.85f, 1f), unlit: true),
                arcInvalid = Mat("TeleportInvalid", new Color(1f, 0.25f, 0.2f), unlit: true),
                pointer = Mat("Pointer", new Color(1f, 1f, 1f), unlit: true),
                overlay = OverlayMat("ComfortOverlay"),
                ctrlBody = Mat("ControllerBody", new Color(0.12f, 0.12f, 0.14f), 0.6f),
                ctrlLeft = Mat("ControllerLeft", new Color(0.15f, 0.8f, 0.95f), unlit: true),
                ctrlRight = Mat("ControllerRight", new Color(1f, 0.55f, 0.15f), unlit: true),
                ctrlTip = Mat("ControllerTip", new Color(1f, 1f, 1f), unlit: true),
            };
        }

        static Material Mat(string name, Color color, float smoothness = 0.2f, bool unlit = false)
        {
            string path = VRLocomotionSetup.MaterialsFolder + "/" + name + ".mat";
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader != null ? shader : Shader.Find("Standard")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (!unlit && mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Przezroczysty, nieoświetlony materiał nakładek komfortu (winieta, mrugnięcie). Tworzony jako asset,
        /// żeby wariant shadera z przezroczystością trafił do buildu. Rysowany na końcu (kolejka Overlay), obie strony.
        /// </summary>
        static Material OverlayMat(string name)
        {
            Material mat = Mat(name, Color.black, unlit: true);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);   // Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);       // Alpha
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_ZTest")) mat.SetFloat("_ZTest", (float)CompareFunction.Always);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Overlay;
            mat.enableInstancing = false;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // =====================================================================
        // ŚWIATŁO I OTOCZENIE
        // =====================================================================

        static void BuildLighting()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.97f, 0.9f);
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.66f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.44f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.22f, 0.22f);
        }

        static void BuildEnvironment(Mats m)
        {
            var env = new GameObject("Environment").transform;

            // Podłoga 40 x 115 m (płaszczyzna Unity ma 10 x 10 m).
            Prim(PrimitiveType.Plane, "Floor", env, new Vector3(0f, 0f, 45f), new Vector3(4f, 1f, 11.5f), m.floor, true);

            // ---- Start: krążek i kompas (co 30°) - widać, o ile obraca obrót skokowy.
            Prim(PrimitiveType.Cylinder, "StartPad", env, new Vector3(0f, 0.004f, 0f), new Vector3(1.2f, 0.003f, 1.2f), m.startPad);
            for (int i = 0; i < 12; i++)
            {
                float yaw = i * 30f;
                bool main = i % 3 == 0;
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                Prim(PrimitiveType.Cube, "Compass_" + yaw, env, dir * 1.6f + Vector3.up * 0.005f, new Vector3(0.05f, 0.006f, main ? 0.5f : 0.3f),
                    main ? m.compassMain : m.compass, false, Quaternion.Euler(0f, yaw, 0f));
                FloorLabel(env, dir * 2.15f + Vector3.up * 0.01f, yaw, yaw.ToString("0") + "°", main ? 0.35f : 0.25f);
            }

            // ---- Ścieżka na ziemi (bez kolidera - pod spodem jest podłoga).
            Prim(PrimitiveType.Cube, "Path_A", env, new Vector3(0f, 0.003f, 18f), new Vector3(3f, 0.004f, 30f), m.path);

            // ---- 1. Prosta: barierki i linie co 2 m.
            Rail(env, new Vector3(-2f, 0f, 4.6f), new Vector3(-2f, 0f, 16f), 1f, m.rail);
            Rail(env, new Vector3(2f, 0f, 4.6f), new Vector3(2f, 0f, 16f), 1f, m.rail);
            for (int i = 1; i <= 5; i++)
            {
                float z = 4f + i * 2f;
                Prim(PrimitiveType.Cube, "Line_" + (i * 2) + "m", env, new Vector3(0f, 0.006f, z), new Vector3(3.9f, 0.004f, 0.05f), m.line);
                FloorLabel(env, new Vector3(-1.45f, 0.012f, z + 0.25f), 0f, (i * 2) + " m", 0.3f);
            }

            // ---- 2. Slalom: ścianki na zmianę z lewej i prawej.
            Rail(env, new Vector3(-2.5f, 0f, 16f), new Vector3(-2.5f, 0f, 31.5f), 1f, m.rail);
            Rail(env, new Vector3(2.5f, 0f, 16f), new Vector3(2.5f, 0f, 31.5f), 1f, m.rail);
            Rail(env, new Vector3(-2.55f, 0f, 16f), new Vector3(-1.95f, 0f, 16f), 1f, m.rail); // łączniki z barierkami prostej
            Rail(env, new Vector3(1.95f, 0f, 16f), new Vector3(2.55f, 0f, 16f), 1f, m.rail);
            SlalomWall(env, 20f, -2.5f, 0.7f, m);
            SlalomWall(env, 24f, -0.7f, 2.5f, m);
            SlalomWall(env, 28f, -2.5f, 0.7f, m);

            // ---- 3. Rampa (z 34-40, w górę o 1 m), podest (z 40-44), schody w dół (z 44-49, 6 stopni po ~17 cm).
            Ramp(env, new Vector3(0f, 0f, 34f), new Vector3(0f, 1f, 40.3f), 3f, m.ramp);
            Prim(PrimitiveType.Cube, "Platform", env, new Vector3(0f, 0.5f, 42f), new Vector3(3f, 1f, 4f), m.platform, true);
            for (int i = 0; i < 5; i++)
            {
                float top = 1f - (i + 1) / 6f;
                Prim(PrimitiveType.Cube, "Step_" + (i + 1), env, new Vector3(0f, top * 0.5f, 44.5f + i), new Vector3(3f, top, 1f), m.stairs, true);
            }
            Rail(env, new Vector3(-1.55f, 1f, 40f), new Vector3(-1.55f, 1f, 44f), 0.9f, m.rail);
            Rail(env, new Vector3(1.55f, 1f, 40f), new Vector3(1.55f, 1f, 44f), 0.9f, m.rail);

            // ---- 4. Woda z kładką (z 53-65). Woda na całą szerokość - nie da się jej obejść.
            Water(env, "Water_Bridge", new Vector3(0f, 0f, 59f), new Vector2(40f, 12f), m.water);
            var bridge = new[] { new Vector3(0f, 0f, 52.6f), new Vector3(0f, 0f, 58.5f), new Vector3(1.6f, 0f, 62f), new Vector3(1.6f, 0f, 65.4f) };
            for (int i = 0; i < bridge.Length - 1; i++) Beam(env, "Bridge_" + (i + 1), bridge[i], bridge[i + 1], 0.8f, 0.25f, m.bridge);
            Prim(PrimitiveType.Cube, "BridgeJoint_1", env, new Vector3(0f, 0.125f, 58.5f), new Vector3(0.9f, 0.25f, 0.9f), m.bridge, true);
            Prim(PrimitiveType.Cube, "BridgeJoint_2", env, new Vector3(1.6f, 0.125f, 62f), new Vector3(0.9f, 0.25f, 0.9f), m.bridge, true);

            // ---- 5. Woda z wyspami (z 69-85) - wyspy są za wysokie, żeby na nie wejść: tylko teleport.
            Water(env, "Water_Islands", new Vector3(0f, 0f, 77f), new Vector2(40f, 16f), m.water);
            var islands = new[] { new Vector3(-1.2f, 0f, 71.2f), new Vector3(1.5f, 0f, 74.8f), new Vector3(-1.3f, 0f, 78.4f), new Vector3(1.2f, 0f, 82f) };
            for (int i = 0; i < islands.Length; i++)
            {
                var isl = Prim(PrimitiveType.Cylinder, "Island_" + (i + 1), env, islands[i] + Vector3.up * 0.3f, new Vector3(2f, 0.3f, 2f), m.island, true);
                UseMeshCollider(isl); // płaski wierzch zamiast zaokrąglonej kapsuły
            }

            // ---- Meta: ścieżka.
            Prim(PrimitiveType.Cube, "Path_Finish", env, new Vector3(0f, 0.003f, 90f), new Vector3(4f, 0.004f, 10f), m.path);

            foreach (var r in env.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            foreach (var t in env.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        }

        static void SlalomWall(Transform parent, float z, float xFrom, float xTo, Mats m)
        {
            Prim(PrimitiveType.Cube, "SlalomWall_" + z, parent, new Vector3((xFrom + xTo) * 0.5f, 0.6f, z),
                new Vector3(Mathf.Abs(xTo - xFrom), 1.2f, 0.3f), m.wall, true);
        }

        static void Rail(Transform parent, Vector3 from, Vector3 to, float height, Material mat)
        {
            Vector3 d = to - from;
            float len = new Vector2(d.x, d.z).magnitude;
            var rot = Quaternion.LookRotation(new Vector3(d.x, 0f, d.z).normalized, Vector3.up);
            Vector3 mid = (from + to) * 0.5f;
            Prim(PrimitiveType.Cube, "Rail", parent, new Vector3(mid.x, from.y + height * 0.5f, mid.z), new Vector3(0.1f, height, len), mat, true, rot);
        }

        /// <summary>Rampa: skośna płyta, której wierzch przechodzi przez punkty bottom i top.</summary>
        static void Ramp(Transform parent, Vector3 bottom, Vector3 top, float width, Material mat)
        {
            const float thickness = 0.2f;
            Vector3 d = top - bottom;
            float len = d.magnitude;
            var rot = Quaternion.LookRotation(d.normalized, Vector3.up);
            Vector3 topCenter = (bottom + top) * 0.5f;
            Vector3 center = topCenter - rot * Vector3.up * (thickness * 0.5f);
            Prim(PrimitiveType.Cube, "Ramp", parent, center, new Vector3(width, thickness, len), mat, true, rot);
        }

        /// <summary>Belka kładki od a do b (wierzch na wysokości height).</summary>
        static void Beam(Transform parent, string name, Vector3 a, Vector3 b, float width, float height, Material mat)
        {
            Vector3 d = b - a;
            d.y = 0f;
            var rot = Quaternion.LookRotation(d.normalized, Vector3.up);
            Vector3 mid = (a + b) * 0.5f;
            Prim(PrimitiveType.Cube, name, parent, new Vector3(mid.x, height * 0.5f, mid.z), new Vector3(width, height, d.magnitude + 0.1f), mat, true, rot);
        }

        /// <summary>Woda: cienka płyta z koliderem (nie da się na niej wylądować teleportem) + strefa powrotu.</summary>
        static void Water(Transform parent, string name, Vector3 center, Vector2 size, Material mat)
        {
            var go = Prim(PrimitiveType.Cube, name, parent, new Vector3(center.x, 0f, center.z), new Vector3(size.x, 0.06f, size.y), mat, true);
            go.AddComponent<TeleportBlocker>();
        }

        static void UseMeshCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null && !(c is MeshCollider)) Object.DestroyImmediate(c);
            var mc = go.GetComponent<MeshCollider>();
            if (mc == null) mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        }

        // =====================================================================
        // TRASA: bramki, strefy wody, tablice, wyniki
        // =====================================================================

        class CourseRefs
        {
            public CourseTimer timer;
            public Transform startPoint;
        }

        static CourseRefs BuildCourse(Mats m)
        {
            var root = new GameObject("Course");
            var refs = new CourseRefs();

            var start = new GameObject("StartPoint").transform;
            start.SetParent(root.transform, false);
            start.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            refs.startPoint = start;

            var gates = new List<Checkpoint>
            {
                Gate(root.transform, "Gate_0_Start", new Vector3(0f, 0f, 4f), 1.9f, "START", m),
                Gate(root.transform, "Gate_1", new Vector3(0f, 0f, 32f), 2.4f, "PUNKT 1", m),
                Gate(root.transform, "Gate_2", new Vector3(0f, 0f, 51f), 2.6f, "PUNKT 2", m),
                Gate(root.transform, "Gate_3", new Vector3(0.8f, 0f, 67f), 3f, "PUNKT 3", m),
                Gate(root.transform, "Gate_4_Meta", new Vector3(0f, 0f, 88f), 4f, "META", m),
            };

            var hazards = new List<HazardZone>
            {
                Hazard(root.transform, "Hazard_Bridge", new Vector3(0f, -0.1f, 59f), new Vector3(40f, 0.44f, 12f)),
                Hazard(root.transform, "Hazard_Islands", new Vector3(0f, -0.1f, 77f), new Vector3(40f, 0.44f, 16f)),
            };

            // ---- tablice przy starcie
            var signs = new GameObject("Signs").transform;
            signs.SetParent(root.transform, false);
            Vector3 eye0 = new Vector3(0f, 1.6f, 0f);
            Board(signs, "Board_Controls", new Vector3(-2.3f, 1.55f, 1.3f), eye0, new Vector2(900f, 980f), 0.0012f, ControlsText);
            Board(signs, "Board_HowTo", new Vector3(2.3f, 1.55f, 1.3f), eye0, new Vector2(900f, 980f), 0.0012f, HowToText);

            // ---- tablice odcinków (po lewej przed wejściem)
            Sign(signs, "Sign_1", new Vector3(-2.8f, 1.6f, 5.4f), new Vector3(0f, 1.6f, 2.5f), Section1Text);
            Sign(signs, "Sign_2", new Vector3(-3.3f, 1.6f, 17.2f), new Vector3(0f, 1.6f, 14.5f), Section2Text);
            Sign(signs, "Sign_3", new Vector3(-2.6f, 1.6f, 33.4f), new Vector3(0f, 1.6f, 30.5f), Section3Text);
            Sign(signs, "Sign_4", new Vector3(-2.6f, 1.6f, 52.2f), new Vector3(0f, 1.6f, 49.5f), Section4Text);
            Sign(signs, "Sign_5", new Vector3(-2.8f, 1.6f, 68.2f), new Vector3(0f, 1.6f, 65.5f), Section5Text);

            // ---- tablica wyników i ocena komfortu za metą
            Vector3 finishEye = new Vector3(0f, 1.6f, 88.5f);
            Vector3 boardPos = new Vector3(0f, 2.6f, 96f);
            var board = Panel("ResultsBoard", root.transform, boardPos, Face(boardPos, finishEye), new Vector2(1900f, 1100f), 0.0024f, PanelBg);
            var resultsText = Txt(board, "Text", Vector2.zero, Vector2.one, 42, TextAnchor.UpperLeft, Color.white, "WYNIKI PRÓB", 40f);
            PostUnder(root.transform, boardPos, 1100f * 0.0024f, m.post, 2.2f);

            Vector3 ratingPos = new Vector3(0f, 1.15f, 92.6f);
            var rating = Panel("ComfortRating", root.transform, ratingPos, Face(ratingPos, finishEye), new Vector2(1400f, 330f), 0.0011f, PanelBg);
            AddPanelCollider(rating, new Vector2(1400f, 330f));
            Label(rating, "Title", new Vector2(0f, 50f), new Vector2(1360f, 80f), 46, TextAnchor.MiddleCenter, Color.white,
                "<b>Jak się czujesz po tej próbie?</b>  <size=30><color=#9AB>(celuj prawym kontrolerem, trigger)</color></size>");
            var ratingColors = new[]
            {
                new Color(0.2f, 0.5f, 0.25f), new Color(0.55f, 0.52f, 0.15f), new Color(0.65f, 0.38f, 0.1f), new Color(0.65f, 0.15f, 0.15f)
            };
            for (int i = 0; i < CourseTimer.RatingNames.Length; i++)
            {
                var b = Btn(rating, "Rating_" + i, new Vector2(-510f + i * 340f, 200f), new Vector2(320f, 150f), CourseTimer.RatingNames[i], 36);
                b.rating = i;
                b.normalColor = ratingColors[i];
                b.hoverColor = Color.Lerp(ratingColors[i], Color.white, 0.35f);
                b.SetHover(false);
            }

            var timer = root.AddComponent<CourseTimer>();
            timer.gates = gates.ToArray();
            timer.hazards = hazards.ToArray();
            timer.startPoint = start;
            timer.resultsText = resultsText;
            timer.ratingPanel = rating.gameObject;
            refs.timer = timer;

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return refs;
        }

        static Checkpoint Gate(Transform parent, string name, Vector3 pos, float halfWidth, string label, Mats m)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.identity);

            float w = halfWidth * 2f;
            Prim(PrimitiveType.Cube, "PostL", root.transform, new Vector3(-halfWidth - 0.12f, 1.3f, 0f), new Vector3(0.2f, 2.6f, 0.2f), m.post, true);
            Prim(PrimitiveType.Cube, "PostR", root.transform, new Vector3(halfWidth + 0.12f, 1.3f, 0f), new Vector3(0.2f, 2.6f, 0.2f), m.post, true);
            Prim(PrimitiveType.Cube, "TopBar", root.transform, new Vector3(0f, 2.7f, 0f), new Vector3(w + 0.44f, 0.2f, 0.2f), m.post);
            var strip = Prim(PrimitiveType.Cube, "Light", root.transform, new Vector3(0f, 2.55f, -0.11f), new Vector3(w, 0.1f, 0.02f), m.gateIdle);
            var floorLine = Prim(PrimitiveType.Cube, "FloorLine", root.transform, new Vector3(0f, 0.008f, 0f), new Vector3(w, 0.006f, 0.15f), m.gateIdle);

            Vector3 labelPos = pos + new Vector3(0f, 3.2f, 0f);
            var panel = Panel("Label", root.transform, labelPos, Quaternion.identity, new Vector2(800f, 170f), 0.0028f, PanelBg);
            var text = Txt(panel, "Text", Vector2.zero, Vector2.one, 100, TextAnchor.MiddleCenter, Color.white, "<b>" + label + "</b>");

            var respawn = new GameObject("RespawnPoint").transform;
            respawn.SetParent(root.transform, false);
            respawn.localPosition = new Vector3(0f, 0f, 1.5f);

            var cp = root.AddComponent<Checkpoint>();
            cp.halfWidth = halfWidth;
            cp.indicators = new Renderer[] { strip.GetComponent<Renderer>(), floorLine.GetComponent<Renderer>() };
            cp.idleMaterial = m.gateIdle;
            cp.nextMaterial = m.gateNext;
            cp.passedMaterial = m.gatePassed;
            cp.label = text;
            cp.respawnPoint = respawn;
            return cp;
        }

        static HazardZone Hazard(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var h = go.AddComponent<HazardZone>();
            h.size = size;
            return h;
        }

        static void Board(Transform parent, string name, Vector3 pos, Vector3 eye, Vector2 sizePx, float pixelSize, string content)
        {
            var panel = Panel(name, parent, pos, Face(pos, eye), sizePx, pixelSize, PanelBg);
            Txt(panel, "Text", Vector2.zero, Vector2.one, 34, TextAnchor.UpperLeft, Color.white, content, 30f);
            PostUnder(parent, pos, sizePx.y * pixelSize, null, 0f);
        }

        static void Sign(Transform parent, string name, Vector3 pos, Vector3 eye, string content)
        {
            var size = new Vector2(820f, 560f);
            const float px = 0.0016f;
            var panel = Panel(name, parent, pos, Face(pos, eye), size, px, PanelBg);
            Txt(panel, "Text", Vector2.zero, Vector2.one, 34, TextAnchor.UpperLeft, Color.white, content, 26f);
            PostUnder(parent, pos, size.y * px, null, 0f);
        }

        /// <summary>Słupek od ziemi do dolnej krawędzi tablicy (bez kolidera).</summary>
        static void PostUnder(Transform parent, Vector3 panelCenter, float panelHeight, Material mat, float minHeight)
        {
            float bottom = panelCenter.y - panelHeight * 0.5f;
            if (bottom < 0.05f) return;
            Prim(PrimitiveType.Cylinder, "Post", parent, new Vector3(panelCenter.x, bottom * 0.5f, panelCenter.z),
                new Vector3(0.06f, bottom * 0.5f, 0.06f), mat != null ? mat : AssetDatabase.LoadAssetAtPath<Material>(VRLocomotionSetup.MaterialsFolder + "/Post.mat"));
        }

        // ---------------------------------------------------------------- teksty tablic

        const string ControlsText =
            "<size=46><b>STEROWANIE</b></size>\n" +
            "<color=#9AB>kontrolery Touch (OVRInput)</color>\n\n" +
            "<b>Lewa gałka</b> – ruch płynny\n" +
            "<b>Prawa gałka ↑</b> – celuj teleportem,\n     puść gałkę = skok, <b>grip</b> = anuluj\n" +
            "<b>Prawa gałka ← →</b> – obrót\n\n" +
            "<b>Menu</b> (lewy) – ustawienia komfortu\n" +
            "<b>A</b> – następny tryb lokomocji\n" +
            "<b>B</b> – siła winiety\n" +
            "<b>Y</b> – tryb siedzący\n" +
            "<b>X</b> – powrót na start (nowa próba)\n\n" +
            "<color=#9AB>W trybie „Teleport” celować można\nteż lewą gałką. Unieś lewą rękę –\nna kontrolerze jest mały wyświetlacz.</color>";

        const string HowToText =
            "<size=46><b>JAK TESTOWAĆ</b></size>\n\n" +
            "1. Przejdź przez bramkę <b>START</b> –\n    rusza stoper.\n" +
            "2. Pokonaj 5 odcinków do <b>META</b>.\n" +
            "3. Na mecie oceń, jak się czujesz.\n" +
            "4. <b>X</b> – wróć, zmień ustawienia\n    (Menu) i spróbuj ponownie.\n\n" +
            "Tablica za metą porównuje próby:\n" +
            "czas, teleporty, metry ruchu płynnego,\n" +
            "obroty, upadki i Twoją ocenę.\n\n" +
            "<color=#FFB347><b>Gdy poczujesz mdłości – przerwij,\n" +
            "zdejmij gogle i odpocznij.</b></color>";

        const string Section1Text =
            "<size=44><b>1. PROSTA</b></size>\n\n" +
            "Linie co 2 m. Porównaj prędkości ruchu\n" +
            "płynnego <b>1,5 / 2,5 / 4 m/s</b> (Menu).\n\n" +
            "Przejdź raz z winietą i raz bez niej\n" +
            "(<b>B</b>). Czy czujesz różnicę?";

        const string Section2Text =
            "<size=44><b>2. SLALOM</b></size>\n\n" +
            "Kierunek ruchu <b>wg głowy</b> albo\n" +
            "<b>wg kontrolera</b>. Spróbuj patrzeć\n" +
            "w bok i iść prosto.\n\n" +
            "Porównaj obrót skokowy i płynny.";

        const string Section3Text =
            "<size=44><b>3. RAMPA I SCHODY</b></size>\n\n" +
            "Zmiana wysokości w ruchu płynnym\n" +
            "bywa najmniej komfortowa.\n\n" +
            "Teleportem: cel musi być płaski\n" +
            "(do 30°) i mieć miejsce nad sobą.";

        const string Section4Text =
            "<size=44><b>4. KŁADKA</b></size>\n\n" +
            "Wąska kładka nad wodą. Idź powoli\n" +
            "albo skacz krótkimi teleportami.\n\n" +
            "Na wodzie nie da się wylądować.\n" +
            "Upadek = powrót do punktu 2.";

        const string Section5Text =
            "<size=44><b>5. WYSPY</b></size>\n\n" +
            "<b>Tylko teleport!</b> Wyspy są za wysokie,\n" +
            "żeby na nie wejść ruchem płynnym.\n\n" +
            "Masz tryb „Płynna”? Naciśnij <b>A</b>\n" +
            "albo zmień tryb w Menu.";

        // =====================================================================
        // UI: menu komfortu, komunikaty
        // =====================================================================

        class UiRefs
        {
            public GameObject menuPanel;
            public Text[] labels;
            public Text[] values;
            public Text hint;
        }

        static UiRefs BuildUI(Mats m)
        {
            var ui = new GameObject("UI").transform;
            var refs = new UiRefs();

            // ---- menu komfortu (ustawiane przed graczem po otwarciu)
            var size = new Vector2(1000f, 1210f);
            var menu = Panel("ComfortMenu", ui, new Vector3(0f, 1.35f, 0.75f), Quaternion.identity, size, 0.0006f, new Color(0.04f, 0.05f, 0.08f, 0.94f));
            AddPanelCollider(menu, size);
            Label(menu, "Title", new Vector2(-60f, 55f), new Vector2(840f, 90f), 52, TextAnchor.MiddleLeft, Color.white, "<b>USTAWIENIA KOMFORTU</b>");
            var close = Btn(menu, "Close", new Vector2(420f, 55f), new Vector2(120f, 76f), "X", 44, false);
            close.closesMenu = true;

            refs.labels = new Text[ComfortSettings.OptionCount];
            refs.values = new Text[ComfortSettings.OptionCount];
            for (int i = 0; i < ComfortSettings.OptionCount; i++)
            {
                float y = 150f + i * 74f;
                var rowBg = Rect(menu, "Row_" + i, new Vector2(0f, y), new Vector2(970f, 68f));
                var img = rowBg.gameObject.AddComponent<Image>();
                img.color = i % 2 == 0 ? new Color(1f, 1f, 1f, 0.04f) : new Color(1f, 1f, 1f, 0f);
                img.raycastTarget = false;

                refs.labels[i] = Label(menu, "Label_" + i, new Vector2(-250f, y), new Vector2(460f, 68f), 34, TextAnchor.MiddleLeft, Color.white,
                    ComfortSettings.OptionName(i));
                var prev = Btn(menu, "Prev_" + i, new Vector2(65f, y), new Vector2(84f, 60f), "<", 44, false);
                prev.option = i;
                prev.delta = -1;
                refs.values[i] = Label(menu, "Value_" + i, new Vector2(250f, y), new Vector2(270f, 68f), 34, TextAnchor.MiddleCenter, Color.white, "…");
                var next = Btn(menu, "Next_" + i, new Vector2(435f, y), new Vector2(84f, 60f), ">", 44, false);
                next.option = i;
                next.delta = 1;
            }

            var hintBg = Rect(menu, "HintBackground", new Vector2(0f, 960f), new Vector2(970f, 170f));
            var hintImg = hintBg.gameObject.AddComponent<Image>();
            hintImg.color = new Color(0.15f, 0.3f, 0.5f, 0.35f);
            hintImg.raycastTarget = false;
            refs.hint = Label(menu, "Hint", new Vector2(0f, 960f), new Vector2(940f, 160f), 28, TextAnchor.UpperLeft, new Color(0.85f, 0.92f, 1f), "");

            Label(menu, "PresetsTitle", new Vector2(0f, 1072f), new Vector2(960f, 40f), 28, TextAnchor.MiddleCenter, new Color(0.7f, 0.75f, 0.85f), "Presety:");
            for (int p = 0; p < ComfortSettings.PresetNames.Length; p++)
            {
                var b = Btn(menu, "Preset_" + p, new Vector2(-325f + p * 325f, 1140f), new Vector2(310f, 80f), ComfortSettings.PresetNames[p], 32);
                b.preset = p;
            }

            menu.gameObject.SetActive(false);
            refs.menuPanel = menu.gameObject;

            // ---- komunikaty (Toast)
            var toast = Panel("Toast", ui, new Vector3(0f, 1.25f, 1.4f), Quaternion.identity, new Vector2(1150f, 230f), 0.0011f, PanelBg);
            var toastText = Txt(toast, "Text", Vector2.zero, Vector2.one, 44, TextAnchor.MiddleCenter, Color.white, "", 16f);
            var group = toast.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var t = toast.gameObject.AddComponent<Toast>();
            t.text = toastText;
            t.group = group;

            return refs;
        }

        // =====================================================================
        // GRACZ: ustawienia, lokomocja, teleport, nakładki komfortu, kontrolery
        // =====================================================================

        static void BuildPlayer(Mats m, CourseRefs course, UiRefs ui)
        {
            var player = new GameObject("Player");

            player.AddComponent<ComfortSettings>();
            player.AddComponent<Haptics>();

            var fader = player.AddComponent<ScreenFader>();
            fader.overlayMaterial = m.overlay;

            var vignette = player.AddComponent<ComfortVignette>();
            vignette.overlayMaterial = m.overlay;

            var loco = player.AddComponent<LocomotionController>();
            loco.startPoint = course.startPoint;
            loco.fader = fader;

            // ---- teleport: łuk + znacznik celu
            var tp = player.AddComponent<TeleportPointer>();
            tp.arc = Line(player.transform, "TeleportArc", m.arcValid, 0.012f);
            tp.validMaterial = m.arcValid;
            tp.invalidMaterial = m.arcInvalid;
            var marker = new GameObject("TeleportMarker").transform;
            marker.SetParent(player.transform, false);
            marker.localPosition = new Vector3(0f, 0.01f, 2f);
            var markerRenderers = new List<Renderer>();
            for (int i = 0; i < 16; i++)
            {
                float a = i * 360f / 16f;
                Vector3 p = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 0.32f;
                var seg = Prim(PrimitiveType.Cube, "Ring_" + i, marker, p, new Vector3(0.12f, 0.012f, 0.035f), m.arcValid, false, Quaternion.Euler(0f, a + 90f, 0f));
                markerRenderers.Add(seg.GetComponent<Renderer>());
            }
            markerRenderers.Add(Prim(PrimitiveType.Cube, "Arrow", marker, new Vector3(0f, 0f, 0.1f), new Vector3(0.04f, 0.012f, 0.28f), m.arcValid).GetComponent<Renderer>());
            markerRenderers.Add(Prim(PrimitiveType.Cube, "ArrowL", marker, new Vector3(-0.05f, 0f, 0.2f), new Vector3(0.03f, 0.012f, 0.13f), m.arcValid, false, Quaternion.Euler(0f, -40f, 0f)).GetComponent<Renderer>());
            markerRenderers.Add(Prim(PrimitiveType.Cube, "ArrowR", marker, new Vector3(0.05f, 0f, 0.2f), new Vector3(0.03f, 0.012f, 0.13f), m.arcValid, false, Quaternion.Euler(0f, 40f, 0f)).GetComponent<Renderer>());
            markerRenderers.Add(Prim(PrimitiveType.Cylinder, "Beam", marker, new Vector3(0f, 0.6f, 0f), new Vector3(0.015f, 0.6f, 0.015f), m.arcValid).GetComponent<Renderer>());
            tp.marker = marker;
            tp.markerRenderers = markerRenderers.ToArray();
            marker.gameObject.SetActive(false);

            // ---- wskaźnik menu
            var pointer = player.AddComponent<UiPointer>();
            pointer.line = Line(player.transform, "MenuPointer", m.pointer, 0.004f);
            pointer.dot = Prim(PrimitiveType.Sphere, "PointerDot", player.transform, new Vector3(0f, 1.2f, 1f), Vector3.one * 0.015f, m.pointer).transform;
            pointer.dot.gameObject.SetActive(false);

            // ---- menu komfortu
            var menu = player.AddComponent<ComfortMenu>();
            menu.panel = ui.menuPanel;
            menu.labelTexts = ui.labels;
            menu.valueTexts = ui.values;
            menu.hintText = ui.hint;
            menu.vignette = vignette;

            // ---- modele kontrolerów (z prymitywów) i wyświetlacz na lewym kontrolerze
            var left = Controller(player.transform, HandSide.Left, new Vector3(-0.25f, 1.0f, 0.35f), m.ctrlLeft, m);
            Controller(player.transform, HandSide.Right, new Vector3(0.25f, 1.0f, 0.35f), m.ctrlRight, m);

            var hud = Panel("WristHud", left, left.position + new Vector3(0f, 0.065f, -0.02f), Quaternion.Euler(45f, 0f, 0f),
                new Vector2(760f, 280f), 0.00026f, new Color(0.03f, 0.04f, 0.07f, 0.85f));
            var hudText = Txt(hud, "Text", Vector2.zero, Vector2.one, 32, TextAnchor.MiddleLeft, Color.white, "HUD", 16f);
            var wrist = hud.gameObject.AddComponent<WristHud>();
            wrist.text = hudText;
            wrist.vignette = vignette;

            foreach (var r in player.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        /// <summary>Schematyczny model kontrolera Touch z prymitywów (rękojeść + pierścień w kolorze ręki + końcówka).</summary>
        static Transform Controller(Transform parent, HandSide side, Vector3 editorPos, Material accent, Mats m)
        {
            var root = new GameObject(side == HandSide.Left ? "LeftController" : "RightController");
            root.transform.SetParent(parent, false);
            root.transform.position = editorPos;

            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            float s = side == HandSide.Left ? -1f : 1f;
            Prim(PrimitiveType.Cube, "Handle", model, new Vector3(0f, -0.035f, -0.025f), new Vector3(0.032f, 0.09f, 0.04f), m.ctrlBody, false, Quaternion.Euler(-25f, 0f, 0f));
            Prim(PrimitiveType.Cube, "Head", model, new Vector3(0f, 0.008f, 0f), new Vector3(0.05f, 0.022f, 0.06f), m.ctrlBody);
            Prim(PrimitiveType.Cylinder, "Ring", model, new Vector3(s * 0.006f, 0.022f, 0.012f), new Vector3(0.075f, 0.004f, 0.075f), accent, false, Quaternion.Euler(-35f, 0f, 0f));
            Prim(PrimitiveType.Cylinder, "Stick", model, new Vector3(0f, 0.024f, -0.008f), new Vector3(0.016f, 0.006f, 0.016f), m.ctrlTip);
            Prim(PrimitiveType.Sphere, "Tip", model, new Vector3(0f, 0.005f, 0.035f), Vector3.one * 0.012f, m.ctrlTip);

            var f = root.AddComponent<ControllerFollower>();
            f.side = side;
            f.visual = model.gameObject;
            return root.transform;
        }

        static LineRenderer Line(Transform parent, string name, Material mat, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, Vector3.forward);
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            lr.alignment = LineAlignment.View;
            lr.sharedMaterial = mat;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }

        // =====================================================================
        // FABRYKA PRYMITYWÓW I UI
        // =====================================================================

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 localScale,
            Material mat, bool keepCollider = false, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            // Pozycja i obrót w układzie rodzica (rodzice tworzeni przez generator stoją w zerze bez obrotu
            // albo, jak bramki i kontrolery, mają własny układ - wtedy pozycja jest lokalna).
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            return go;
        }

        /// <summary>Napis leżący na podłodze, czytelny dla kogoś stojącego w środku startu (góra napisu wskazuje kierunek yaw).</summary>
        static void FloorLabel(Transform parent, Vector3 pos, float yaw, string content, float height)
        {
            float px = 0.002f;
            var panel = Panel("FloorLabel_" + content, parent, pos, Quaternion.Euler(90f, yaw, 0f), new Vector2(height * 2.2f / px, height / px), px, Color.clear);
            Object.DestroyImmediate(panel.Find("Background").gameObject);
            Txt(panel, "Text", Vector2.zero, Vector2.one, Mathf.RoundToInt(height / px * 0.8f), TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.9f), "<b>" + content + "</b>", 0f);
        }

        /// <summary>Obrót panelu tak, by czytelna strona była zwrócona do oczu w punkcie eye.</summary>
        static Quaternion Face(Vector3 position, Vector3 eye)
        {
            Vector3 dir = position - eye;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            return Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        /// <summary>World-space Canvas z tłem. Rozmiar w pikselach, pixelSize = metry na piksel.</summary>
        static RectTransform Panel(string name, Transform parent, Vector3 position, Quaternion rotation, Vector2 sizePx,
            float pixelSize, Color background)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            rt.sizeDelta = sizePx;
            rt.localScale = Vector3.one * pixelSize;
            rt.SetPositionAndRotation(position, rotation);

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bgRt = (RectTransform)bg.transform;
            bgRt.SetParent(rt, false);
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var img = bg.GetComponent<Image>();
            img.color = background;
            img.raycastTarget = false;
            return rt;
        }

        /// <summary>Kolider-trigger całego panelu - promień wskaźnika zatrzymuje się na nim.</summary>
        static void AddPanelCollider(RectTransform panel, Vector2 sizePx)
        {
            var bc = panel.gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(sizePx.x, sizePx.y, 4f);
            bc.center = Vector3.zero;
        }

        static Text Txt(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, int fontSize,
            TextAnchor alignment, Color color, string content, float margin = 10f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
            SetupText(go.GetComponent<Text>(), fontSize, alignment, color, content);
            return go.GetComponent<Text>();
        }

        /// <summary>Prostokąt o środku (x, y) liczonym od środka górnej krawędzi panelu (y w dół).</summary>
        static RectTransform Rect(RectTransform parent, string name, Vector2 centerFromTop, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(centerFromTop.x, -centerFromTop.y);
            return rt;
        }

        static Text Label(RectTransform parent, string name, Vector2 centerFromTop, Vector2 size, int fontSize, TextAnchor alignment,
            Color color, string content)
        {
            var rt = Rect(parent, name, centerFromTop, size);
            var t = rt.gameObject.AddComponent<Text>();
            SetupText(t, fontSize, alignment, color, content);
            return t;
        }

        /// <summary>Przycisk: Image + BoxCollider (trigger, wysunięty przed panel) + MenuButton + napis.</summary>
        static MenuButton Btn(RectTransform parent, string name, Vector2 centerFromTop, Vector2 size, string label, int fontSize, bool richText = true)
        {
            var rt = Rect(parent, name, centerFromTop, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            var bc = rt.gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(size.x, size.y, 20f);
            bc.center = new Vector3(0f, 0f, -10f); // przed panelem (panel patrzy w +Z, gracz stoi po stronie -Z)
            var b = rt.gameObject.AddComponent<MenuButton>();
            b.background = img;
            img.color = b.normalColor;

            var text = Txt(rt, "Text", Vector2.zero, Vector2.one, fontSize, TextAnchor.MiddleCenter, Color.white, label, 2f);
            text.supportRichText = richText; // "<" i ">" bez rich textu - inaczej parser znaczników mógłby je „zjeść”
            text.fontStyle = FontStyle.Bold;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = fontSize;
            return b;
        }

        static void SetupText(Text t, int fontSize, TextAnchor alignment, Color color, string content)
        {
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = color;
            t.text = content;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
        }
    }
}
