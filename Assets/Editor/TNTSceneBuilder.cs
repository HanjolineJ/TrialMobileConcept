using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        private const string AudioPath = "Assets/Audio";
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
            Sprite blockSprite = CreateSprite($"{ArtPath}/Block.png", 64, ShadeBlock);
            Sprite tntSprite = CreateSprite($"{ArtPath}/TNT.png", 64, ShadeTnt);
            Sprite starFilledSprite = CreateSprite($"{ArtPath}/StarFilled.png", 64, StarShade(new Color(1f, 0.85f, 0.15f)));
            Sprite starEmptySprite = CreateSprite($"{ArtPath}/StarEmpty.png", 64, StarShade(new Color(0.45f, 0.45f, 0.5f)));
            Sprite ringSprite = CreateSprite($"{ArtPath}/Ring.png", 64, ShadeRing);
            Sprite pixelSprite = CreateSprite($"{ArtPath}/Pixel.png", 4, (x, y) => Color.white);
            Sprite musicOnSprite = CreateSprite($"{ArtPath}/MusicOn.png", 64, (x, y) => ShadeMusicNote(x, y, false));
            Sprite musicOffSprite = CreateSprite($"{ArtPath}/MusicOff.png", 64, (x, y) => ShadeMusicNote(x, y, true));

            // --- Prefabs ---
            GameObject blockPrefab = CreateBlockPrefab(blockSprite);
            GameObject chargePrefab = CreateChargePrefab(tntSprite);

            // --- Level data ---
            LevelData levelData = CreateLevelData(blockPrefab);

            // --- Scene ---
            BuildScene(levelData, chargePrefab, ringSprite, pixelSprite, starFilledSprite, starEmptySprite,
                musicOnSprite, musicOffSprite);

            // --- Project settings ---
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

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

        private static Color ShadeBlock(float x, float y)
        {
            const float border = 5f;
            bool isBorder = x < border || x >= 64f - border || y < border || y >= 64f - border;
            return isBorder ? new Color(0.72f, 0.70f, 0.68f) : new Color(0.94f, 0.92f, 0.88f);
        }

        private static Color ShadeTnt(float x, float y)
        {
            if (y < 7f || y >= 57f)
                return new Color(0.42f, 0.07f, 0.05f); // dark top/bottom bands
            if (x >= 12f && x < 20f)
                return new Color(0.95f, 0.35f, 0.25f); // highlight stripe
            return new Color(0.78f, 0.16f, 0.12f);     // red body
        }

        private static Color ShadeRing(float x, float y)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f));
            return d >= 24f && d <= 30f ? Color.white : Color.clear;
        }

        /// <summary>
        /// Placeholder music-toggle icon: a white quaver (ellipse head + stem).
        /// The "off" variant carves out a diagonal band, reading as a slash.
        /// </summary>
        private static Color ShadeMusicNote(float x, float y, bool slashed)
        {
            float hx = (x - 26f) / 12f, hy = (y - 20f) / 9f;
            bool head = hx * hx + hy * hy <= 1f;
            bool stem = x >= 35f && x < 41f && y >= 20f && y <= 52f;
            if (!head && !stem)
                return Color.clear;
            if (slashed && Mathf.Abs(x - y) < 6f)
                return Color.clear;
            return Color.white;
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
            Sprite pixelSprite, Sprite starFilledSprite, Sprite starEmptySprite,
            Sprite musicOnSprite, Sprite musicOffSprite)
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
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            cameraGo.AddComponent<AudioListener>();

            // Ground — static collider so debris lands instead of falling forever.
            GameObject ground = CreateWorldSprite("Ground", pixelSprite, new Color(0.24f, 0.25f, 0.29f),
                new Vector3(0f, -0.5f, 0f), new Vector3(16f, 1f, 1f), 0);
            ground.AddComponent<BoxCollider2D>().size = Vector2.one; // 16x1 world units after scale

            // Demolition line marker (LevelSetup snaps it to the configured Y).
            GameObject line = CreateWorldSprite("DemolitionLine", pixelSprite, new Color(0.9f, 0.2f, 0.2f, 0.9f),
                new Vector3(0f, levelData.demolitionLineY, 0f), new Vector3(16f, 0.05f, 1f), 1);

            // Placement indicator (hidden until the player touches).
            GameObject indicator = CreateWorldSprite("PlacementIndicator", ringSprite, new Color(0.2f, 1f, 0.3f, 0.65f),
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

            // Persistent music player (survives the scene reload on restart).
            // Null tracks are tolerated: AudioManager warns and plays silence.
            var audioGo = new GameObject("AudioManager");
            var audio = audioGo.AddComponent<AudioManager>();
            SetRef(audio, "musicTrack", AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioPath}/Music_MainTheme.mp3"));
            SetRef(audio, "ambienceTrack", AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioPath}/Music_Ambience.mp3"));

            // UI.
            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();

            BuildCanvas(gm, starFilledSprite, starEmptySprite, musicOnSprite, musicOffSprite);

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

        private static void BuildCanvas(GameManager gm, Sprite starFilledSprite, Sprite starEmptySprite,
            Sprite musicOnSprite, Sprite musicOffSprite)
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
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(420f, 110f));

            // HUD: restart button (top-right).
            Button restartButton = CreateButton("RestartButton", canvasGo.transform, "RESTART", 34,
                new Color(0.28f, 0.31f, 0.38f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(300f, 110f));

            // HUD: music toggle (top-right, left of restart). Icon shows the state.
            Button musicButton = CreateButton("MusicButton", canvasGo.transform, "", 34,
                new Color(0.28f, 0.31f, 0.38f), new Vector2(1f, 1f), new Vector2(-380f, -40f), new Vector2(110f, 110f));
            Object.DestroyImmediate(musicButton.transform.Find("Label").gameObject);
            Image musicIcon = CreateIcon(musicButton.transform, musicOnSprite);

            // HUD: detonate button (bottom centre).
            Button detonateButton = CreateButton("DetonateButton", canvasGo.transform, "DETONATE", 46,
                new Color(0.85f, 0.3f, 0.1f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(460f, 170f));

            // Result panel (hidden until scoring finishes).
            var panelGo = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            CreateText("ResultTitle", panelGo.transform, "LEVEL COMPLETE", 64,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 430f), new Vector2(900f, 130f));
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
            SetRef(ui, "musicButton", musicButton);
            SetRef(ui, "musicIcon", musicIcon);
            SetRef(ui, "musicOnSprite", musicOnSprite);
            SetRef(ui, "musicOffSprite", musicOffSprite);
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
            TextAnchor alignment, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
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
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, int fontSize,
            Color color, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;

            Text text = CreateText("Label", go.transform, label, fontSize,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            return button;
        }

        /// <summary>Adds a centred icon image to a button, padded in from the edges.</summary>
        private static Image CreateIcon(Transform parent, Sprite sprite)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(25f, 25f);
            rect.offsetMax = new Vector2(-25f, -25f);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            return image;
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
