using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AdaptivePerformance.Editor;
using UnityEditor.AdaptivePerformance.Simulator.Editor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AdaptivePerformance;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TNTGame.Core;
using TNTGame.Gameplay;
using TNTGame.UI;
using Object = UnityEngine.Object;

namespace TNTGame.EditorTools
{
    /// <summary>
    /// One-shot bootstrapper for the vertical slice. Generates placeholder
    /// sprites, the Block/TNT prefabs, the LevelData asset and the complete
    /// Level_01 scene (camera, ground, demolition line, managers, canvas UI),
    /// then registers the scene in Build Settings and forces portrait.
    ///
    /// Runs automatically once after the scripts are first imported (guarded by
    /// the existence of the scene file); re-run manually via the TNT menu after
    /// deleting Assets/Scenes/Level_01.unity for a clean rebuild.
    /// </summary>
    [InitializeOnLoad]
    public static class TNTSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Level_01.unity";
        private const string ArtPath = "Assets/Art";
        private const string PrefabPath = "Assets/Prefabs/Gameplay";
        private const string DataPath = "Assets/Data";

        static TNTSceneBuilder()
        {
            // delayCall so the first build happens after the initial domain
            // reload and asset import have fully settled.
            EditorApplication.delayCall += AutoBuild;
        }

        private static void AutoBuild()
        {
            if (File.Exists(ScenePath) || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            try
            {
                BuildAll();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("[TNT] Auto-build failed. Fix the error above, then run menu: TNT > Build Level 01 Scene.");
            }
        }

        [MenuItem("TNT/Build Level 01 Scene")]
        public static void BuildAll()
        {
            EnsureFolder(ArtPath);
            EnsureFolder(PrefabPath);
            EnsureFolder(DataPath);

            // --- Placeholder sprites (procedurally rasterised PNGs) ---
            // Ninjago S6 "Skybound" sky-pirate palette: stormy blue-black sky,
            // teal ocean glow, weathered wood, cursed djinn gold.
            Sprite blockSprite = CreateSprite($"{ArtPath}/Block.png", 64, ShadeBlock);
            Sprite tntSprite = CreateSprite($"{ArtPath}/TNT.png", 64, ShadeTnt);
            Sprite starFilledSprite = CreateSprite($"{ArtPath}/StarFilled.png", 64, StarShade(new Color(0.90f, 0.70f, 0.25f))); // gold
            Sprite starEmptySprite = CreateSprite($"{ArtPath}/StarEmpty.png", 64, StarShade(new Color(0.16f, 0.22f, 0.32f)));  // dark blue
            Sprite ringSprite = CreateSprite($"{ArtPath}/Ring.png", 64, ShadeRing);
            Sprite pixelSprite = CreateSprite($"{ArtPath}/Pixel.png", 4, (x, y) => Color.white);
            Sprite backgroundSprite = CreateSprite($"{ArtPath}/Background.png", 64, ShadeBackground);
            Sprite seaSprite = CreateSprite($"{ArtPath}/Sea.png", 64, ShadeSea);

            // --- Prefabs ---
            GameObject blockPrefab = CreateBlockPrefab(blockSprite);
            GameObject chargePrefab = CreateChargePrefab(tntSprite);

            // --- Level data ---
            LevelData levelData = CreateLevelData(blockPrefab);

            // --- Scene ---
            BuildScene(levelData, chargePrefab, ringSprite, pixelSprite, backgroundSprite, seaSprite, starFilledSprite, starEmptySprite);

            // --- Project settings ---
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            EnsureAdaptivePerformanceProvider();

            AssetDatabase.SaveAssets();
            Debug.Log("[TNT] Level_01 built successfully. Press Play: drag on the building to place TNT, then hit DETONATE.");
        }

        // ------------------------------------------------------------------
        // Sprite generation
        // ------------------------------------------------------------------

        /// <summary>
        /// Rasterises a square sprite via a per-pixel shade function (with 2x2
        /// supersampling for smooth edges), writes it as PNG and imports it as
        /// a single sprite covering exactly one world unit.
        /// </summary>
        private static Sprite CreateSprite(string path, int size, Func<float, float, Color> shade)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color sum = Color.clear;
                    for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                        sum += shade(x + 0.25f + 0.5f * sx, y + 0.25f + 0.5f * sy);
                    texture.SetPixel(x, y, sum / 4f);
                }
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size; // sprite spans exactly 1 world unit
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Weathered wood plank: dark timber with grain, steel banding top and
        // bottom, a deep-navy border and a subtle gold-lit top edge.
        private static Color ShadeBlock(float x, float y)
        {
            const float border = 5f;
            if (y >= 64f - 3f)
                return new Color(0.80f, 0.62f, 0.28f); // gold top edge
            if (x < border || x >= 64f - border || y < border)
                return new Color(0.07f, 0.11f, 0.20f); // deep navy border
            if ((y >= 9f && y < 14f) || (y >= 50f && y < 55f))
                return new Color(0.24f, 0.27f, 0.33f); // steel banding
            // Wood grain: soft vertical streaks via stretched value noise.
            float grain = 0.82f + 0.36f * ValueNoise(x * 0.35f, y * 0.05f);
            return new Color(0.30f * grain, 0.22f * grain, 0.15f * grain);
        }

        // Cursed charge: a djinn coin — bright gold disc, darker rim and
        // embossed inner ring, pale highlight at the core.
        private static Color ShadeTnt(float x, float y)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f));
            if (d > 28f) return Color.clear;
            if (d > 23f) return new Color(0.45f, 0.32f, 0.10f); // dark rim
            if (d > 12f) return new Color(0.85f, 0.65f, 0.22f); // gold face
            if (d > 8f)  return new Color(0.55f, 0.40f, 0.12f); // embossed ring
            return new Color(0.95f, 0.82f, 0.45f);              // pale core
        }

        // Stormy sky over a dark sea: turbulent clouds above, a teal ocean
        // glow at the horizon (t ~ 0.26), waves below.
        private static Color ShadeBackground(float x, float y)
        {
            float t = y / 63f;
            const float horizon = 0.26f;

            if (t < horizon)
            {
                // Sea: deep storm blue with slow horizontal wave streaks.
                float waves = ValueNoise(x * 0.30f, y * 1.8f);
                return new Color(0.03f, 0.06f, 0.12f) + new Color(0.04f, 0.10f, 0.13f) * waves;
            }

            // Sky: blue-black at the top, slightly lighter storm blue low.
            float s = (t - horizon) / (1f - horizon);
            Color sky = Color.Lerp(new Color(0.05f, 0.08f, 0.16f), new Color(0.015f, 0.025f, 0.07f), s);

            // Turbulent clouds drifting through the sky region.
            float clouds = ValueNoise(x * 0.09f, y * 0.22f) * 0.6f + ValueNoise(x * 0.21f, y * 0.45f) * 0.4f;
            sky += new Color(0.09f, 0.13f, 0.20f) * clouds * (1f - s * 0.5f);

            // Teal glow hugging the horizon.
            float glow = Mathf.Max(0f, 1f - Mathf.Abs(t - horizon) / 0.10f);
            return sky + new Color(0.10f, 0.55f, 0.55f) * glow * glow * 0.45f;
        }

        // Stormy sea surface for the ground: dark waves with foam flecks.
        private static Color ShadeSea(float x, float y)
        {
            float waves = ValueNoise(x * 0.25f, y * 0.9f);
            Color sea = new Color(0.03f, 0.07f, 0.14f) + new Color(0.05f, 0.12f, 0.16f) * waves;
            float foam = ValueNoise(x * 0.8f, y * 2.6f);
            if (foam > 0.82f)
                sea += new Color(0.25f, 0.45f, 0.45f) * (foam - 0.82f) * 3f; // foam highlights
            return sea;
        }

        /// <summary>Deterministic smoothed value noise (lattice hash + bilinear).</summary>
        private static float ValueNoise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = Mathf.SmoothStep(0f, 1f, x - xi);
            float yf = Mathf.SmoothStep(0f, 1f, y - yi);
            float a = Hash01(xi, yi), b = Hash01(xi + 1, yi);
            float c = Hash01(xi, yi + 1), d = Hash01(xi + 1, yi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, xf), Mathf.Lerp(c, d, xf), yf);
        }

        private static float Hash01(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        private static Color ShadeRing(float x, float y)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f));
            return d >= 24f && d <= 30f ? Color.white : Color.clear;
        }

        private static Func<float, float, Color> StarShade(Color fill)
        {
            Vector2[] polygon = StarPolygon(32f, 32f, 29f, 12.5f);
            return (x, y) => InsidePolygon(polygon, x, y) ? fill : Color.clear;
        }

        /// <summary>Ten alternating outer/inner vertices of a five-pointed star, starting at the top.</summary>
        private static Vector2[] StarPolygon(float cx, float cy, float outerRadius, float innerRadius)
        {
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? outerRadius : innerRadius;
                points[i] = new Vector2(cx + Mathf.Cos(angle) * radius, cy + Mathf.Sin(angle) * radius);
            }
            return points;
        }

        /// <summary>Even-odd point-in-polygon test.</summary>
        private static bool InsidePolygon(Vector2[] polygon, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if (polygon[i].y > y != polygon[j].y > y &&
                    x < (polygon[j].x - polygon[i].x) * (y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                    inside = !inside;
            }
            return inside;
        }

        // ------------------------------------------------------------------
        // Prefabs and data
        // ------------------------------------------------------------------

        private static GameObject CreateBlockPrefab(Sprite sprite)
        {
            var go = new GameObject("Block");
            go.AddComponent<SpriteRenderer>().sprite = sprite;
            // Static so the building is stable before detonation; BuildingBlock
            // switches it to dynamic when the blast hits.
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            // Sprite is exactly 1 unit; the 0.5 scale below yields a 0.5-unit block.
            go.AddComponent<BoxCollider2D>().size = Vector2.one;
            go.AddComponent<BuildingBlock>();
            go.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabPath}/Block.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateChargePrefab(Sprite sprite)
        {
            var go = new GameObject("TNTCharge");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 5; // render above the building
            go.transform.localScale = new Vector3(0.35f, 0.35f, 1f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabPath}/TNTCharge.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static LevelData CreateLevelData(GameObject blockPrefab)
        {
            var data = ScriptableObject.CreateInstance<LevelData>();
            data.blockPrefab = blockPrefab.GetComponent<BuildingBlock>();
            // All other values keep the class defaults: 3 TNT, radius 2.5,
            // force 12, 40/75 star thresholds, 3 s settle, 5x6 grid,
            // origin (-1.1, 0.25), demolition line at y = 0.4.
            AssetDatabase.CreateAsset(data, $"{DataPath}/Level_01.asset");
            AssetDatabase.SaveAssets();

            // Re-load: the CreateInstance wrapper can be invalidated when the
            // import replaces the asset, and assigning it would serialize as
            // null. The loaded object is the authoritative persistent asset.
            return AssetDatabase.LoadAssetAtPath<LevelData>($"{DataPath}/Level_01.asset");
        }

        // ------------------------------------------------------------------
        // Scene construction
        // ------------------------------------------------------------------

        private static void BuildScene(LevelData levelData, GameObject chargePrefab, Sprite ringSprite,
            Sprite pixelSprite, Sprite backgroundSprite, Sprite seaSprite, Sprite starFilledSprite, Sprite starEmptySprite)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera — orthographic, framed on the building with headroom for debris.
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 2f, -10f);
            var cam = cameraGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.025f, 0.07f); // stormy blue-black
            // Programmatically created cameras get no AudioListener by default.
            cameraGo.AddComponent<AudioListener>();

            // Gradient sky backdrop: turbulent clouds over a dark sea horizon.
            CreateWorldSprite("Background", backgroundSprite, Color.white,
                new Vector3(0f, 2f, 5f), new Vector3(24f, 14f, 1f), -10);

            // Ground — stormy sea surface; static collider so debris lands
            // instead of falling forever.
            GameObject ground = CreateWorldSprite("Ground", seaSprite, Color.white,
                new Vector3(0f, -0.5f, 0f), new Vector3(16f, 1f, 1f), 0);
            ground.AddComponent<BoxCollider2D>().size = Vector2.one; // 16x1 world units after scale

            // Foam rim on the waterline.
            CreateWorldSprite("GroundRim", pixelSprite, new Color(0.30f, 0.70f, 0.65f, 0.9f),
                new Vector3(0f, 0.02f, 0f), new Vector3(16f, 0.05f, 1f), 1);

            // Demolition line marker — shimmering teal energy
            // (LevelSetup snaps it to the configured Y).
            GameObject line = CreateWorldSprite("DemolitionLine", pixelSprite, new Color(0.25f, 0.85f, 0.90f, 0.9f),
                new Vector3(0f, levelData.demolitionLineY, 0f), new Vector3(16f, 0.05f, 1f), 1);

            // Placement indicator (hidden until the player touches).
            GameObject indicator = CreateWorldSprite("PlacementIndicator", ringSprite, new Color(0.25f, 0.9f, 0.8f, 0.65f),
                Vector3.zero, new Vector3(0.7f, 0.7f, 1f), 10);
            indicator.SetActive(false);

            // Managers.
            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            gm.EditorSetLevelData(levelData);

            var setupGo = new GameObject("LevelSetup");
            var setup = setupGo.AddComponent<LevelSetup>();
            SetRef(setup, "demolitionLine", line.transform);

            var placementGo = new GameObject("TouchPlacementController");
            var placement = placementGo.AddComponent<TouchPlacementController>();
            SetRef(placement, "chargePrefab", chargePrefab);
            SetRef(placement, "indicator", indicator.GetComponent<SpriteRenderer>());
            SetRef(placement, "worldCamera", cam);
            SetColor(placement, "validColor", new Color(0.25f, 0.9f, 0.8f, 0.65f));   // teal
            SetColor(placement, "invalidColor", new Color(0.9f, 0.3f, 0.25f, 0.45f)); // warning red

            // Music — persistent AudioManager; auto-wires the first audio clip
            // found in Assets/Audio (see README there for licensing/replacement).
            var audioGo = new GameObject("AudioManager");
            audioGo.AddComponent<AudioSource>();
            var audio = audioGo.AddComponent<AudioManager>();
            AudioClip music = FindMusicClip();
            if (music != null)
                SetRef(audio, "musicTrack", music);
            else
                Debug.LogWarning("[TNT] No music file found in Assets/Audio — music will be silent. See Assets/Audio/README.md.");

            // UI.
            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();

            BuildCanvas(gm, starFilledSprite, starEmptySprite);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static GameObject CreateWorldSprite(string name, Sprite sprite, Color color,
            Vector3 position, Vector3 scale, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return go;
        }

        private static void BuildCanvas(GameManager gm, Sprite starFilledSprite, Sprite starEmptySprite)
        {
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f); // portrait
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // HUD: TNT counter (top-left).
            Text tntText = CreateText("TNTText", canvasGo.transform, "TNT: 3", 56,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(420f, 110f),
                new Color(0.95f, 0.87f, 0.68f)); // pale gold

            // HUD: restart button (top-right) — teal.
            Button restartButton = CreateButton("RestartButton", canvasGo.transform, "RESTART", 34,
                new Color(0.14f, 0.42f, 0.48f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(300f, 110f));

            // HUD: detonate button (bottom centre) — pirate gold, navy label.
            Button detonateButton = CreateButton("DetonateButton", canvasGo.transform, "DETONATE", 46,
                new Color(0.80f, 0.60f, 0.20f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(460f, 170f),
                new Color(0.05f, 0.08f, 0.15f));

            // Result panel (hidden until scoring finishes).
            var panelGo = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelImage = panelGo.GetComponent<Image>();
            panelImage.color = new Color(0.02f, 0.04f, 0.09f, 0.92f);
            // Must NOT block raycasts: the panel covers the whole screen and would
            // otherwise swallow every click, making the HUD buttons (restart!)
            // unresponsive while it is visible.
            panelImage.raycastTarget = false;

            CreateText("ResultTitle", panelGo.transform, "LEVEL COMPLETE", 64,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 430f), new Vector2(900f, 130f),
                new Color(0.45f, 0.75f, 0.95f)); // sky blue
            Text scoreText = CreateText("ScoreText", panelGo.transform, "Destroyed: 0%", 52,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(900f, 110f));

            var starImages = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var starGo = new GameObject($"Star{i + 1}", typeof(RectTransform), typeof(Image));
                starGo.transform.SetParent(panelGo.transform, false);
                var starRect = (RectTransform)starGo.transform;
                starRect.anchorMin = starRect.anchorMax = new Vector2(0.5f, 0.5f);
                starRect.anchoredPosition = new Vector2((i - 1) * 200f, 20f);
                starRect.sizeDelta = new Vector2(160f, 160f);
                var starImage = starGo.GetComponent<Image>();
                starImage.sprite = starEmptySprite;
                starImages[i] = starImage;
            }

            panelGo.SetActive(false);

            var ui = canvasGo.AddComponent<UIManager>();
            SetRef(ui, "tntCountText", tntText);
            SetRef(ui, "detonateButton", detonateButton);
            SetRef(ui, "restartButton", restartButton);
            SetRef(ui, "resultPanel", panelGo);
            SetRef(ui, "scoreText", scoreText);
            SetRef(ui, "starFilledSprite", starFilledSprite);
            SetRef(ui, "starEmptySprite", starEmptySprite);

            var so = new SerializedObject(ui);
            SerializedProperty stars = so.FindProperty("starImages");
            stars.arraySize = 3;
            for (int i = 0; i < 3; i++)
                stars.GetArrayElementAtIndex(i).objectReferenceValue = starImages[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------
        // UI helpers
        // ------------------------------------------------------------------

        // Legacy UI Text with the built-in font: renders everywhere without
        // importing TMP essentials first. Placeholder-grade; swap for TMP later.
        private static Text CreateText(string name, Transform parent, string content, int fontSize,
            TextAnchor alignment, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color ?? Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, int fontSize,
            Color color, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color? labelColor = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;

            Text text = CreateText("Label", go.transform, label, fontSize,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size, labelColor);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            return button;
        }

        /// <summary>Assigns a private [SerializeField] reference from editor code.</summary>
        private static void SetRef(Object target, string propertyName, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"[TNT] Property '{propertyName}' not found on {target.GetType().Name}.");
                return;
            }
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Registers the Adaptive Performance Simulator provider (editor/Standalone)
        /// so the package stops logging "No Provider was configured" at play time.
        /// Mirrors what the Project Settings UI does when the provider is ticked.
        /// Device builds would need a real provider package (none installed).
        /// </summary>
        private static void EnsureAdaptivePerformanceProvider()
        {
            const string apPath = "Assets/Adaptive Performance";
            const string apSettingsPath = apPath + "/Settings";
            EnsureFolder(apSettingsPath);

            // General settings container, stored as a build-config object.
            if (!EditorBuildSettings.TryGetConfigObject(AdaptivePerformanceGeneralSettings.k_SettingsKey,
                    out AdaptivePerformanceGeneralSettingsPerBuildTarget generalSettings) || generalSettings == null)
            {
                generalSettings = CreateSettingsAsset<AdaptivePerformanceGeneralSettingsPerBuildTarget>(
                    $"{apPath}/AdaptivePerformanceGeneralSettings.asset");
                EditorBuildSettings.AddConfigObject(AdaptivePerformanceGeneralSettings.k_SettingsKey, generalSettings, true);
            }

            // Per-platform settings for the editor (Standalone).
            AdaptivePerformanceGeneralSettings settings = generalSettings.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (settings == null)
            {
                settings = CreateSettingsAsset<AdaptivePerformanceGeneralSettings>(
                    $"{apSettingsPath}/AdaptivePerformanceSettings.asset");
                settings.InitManagerOnStart = true;
                generalSettings.SetSettingsForBuildTarget(BuildTargetGroup.Standalone, settings);
                EditorUtility.SetDirty(generalSettings);
            }

            // Manager holding the loader list.
            AdaptivePerformanceManagerSettings manager = settings.Manager;
            if (manager == null)
            {
                manager = CreateSettingsAsset<AdaptivePerformanceManagerSettings>(
                    $"{apSettingsPath}/AdaptivePerformanceManagerSettings.asset");
                settings.Manager = manager;
                EditorUtility.SetDirty(settings);
            }

            // The Simulator loader itself.
            if (manager.loaders == null)
                manager.loaders = new List<AdaptivePerformanceLoader>();
            if (!manager.loaders.Exists(loader => loader is SimulatorProviderLoader))
            {
                var loader = CreateSettingsAsset<SimulatorProviderLoader>($"{apSettingsPath}/SimulatorProviderLoader.asset");
                List<AdaptivePerformanceLoader> loaders = manager.loaders;
                loaders.Add(loader);
                manager.loaders = loaders;
                EditorUtility.SetDirty(manager);
            }
        }

        /// <summary>Creates (or loads) a settings ScriptableObject asset, returning the persistent object.</summary>
        private static T CreateSettingsAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            var instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            // Re-load so we hold the authoritative persistent object (the
            // CreateInstance wrapper may not survive the import intact).
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        /// <summary>Returns the first AudioClip under Assets/Audio, or null when none exists.</summary>
        private static AudioClip FindMusicClip()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Audio"))
                return null;

            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                    return clip;
            }
            return null;
        }

        /// <summary>Assigns a private [SerializeField] Color from editor code.</summary>
        private static void SetColor(Object target, string propertyName, Color value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"[TNT] Property '{propertyName}' not found on {target.GetType().Name}.");
                return;
            }
            property.colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
