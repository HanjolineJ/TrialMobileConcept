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
    /// One-shot bootstrapper for the vertical slice. Generates the placeholder
    /// sprites, Block/charge prefabs, LevelData assets, the LevelCatalog and one
    /// complete scene per level theme (camera, backdrop, ground, demolition
    /// line, managers, canvas UI incl. result panel and level select), then
    /// registers all scenes in Build Settings and forces portrait.
    ///
    /// Level 1 "Skybound" (nautical/djinn palette) and Level 2 "Hands of Time"
    /// (clockwork/vortex palette) differ only through the LevelTheme values
    /// below; every asset lives in a per-theme folder so the levels can sit
    /// side by side in Build Settings.
    ///
    /// Runs automatically once after the scripts are first imported (guarded by
    /// the existence of the scene files); re-run manually via the TNT menu after
    /// deleting the generated scenes for a clean rebuild.
    /// </summary>
    [InitializeOnLoad]
    public static class TNTSceneBuilder
    {
        private const string ScenesPath = "Assets/Scenes";
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
            bool scenesExist = File.Exists(SceneFilePath(SkyboundTheme()))
                            && File.Exists(SceneFilePath(HandsOfTimeTheme()));
            if (scenesExist || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            try
            {
                BuildAll();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("[TNT] Auto-build failed. Fix the error above, then run menu: TNT > Build Level Scenes.");
            }
        }

        [MenuItem("TNT/Build Level Scenes")]
        public static void BuildAll()
        {
            EnsureFolder(ArtPath);
            EnsureFolder(PrefabPath);
            EnsureFolder(DataPath);

            LevelTheme[] themes = { SkyboundTheme(), HandsOfTimeTheme() };

            // --- Sprites, prefabs and level data, one set per theme ---
            var assets = new LevelAssets[themes.Length];
            for (int i = 0; i < themes.Length; i++)
                assets[i] = BuildLevelAssets(themes[i]);

            // --- Catalog (references the LevelData assets) ---
            LevelCatalog catalog = CreateCatalog(
                System.Array.ConvertAll(assets, a => a.Data));

            // --- Scenes (reference the catalog) ---
            for (int i = 0; i < themes.Length; i++)
                BuildScene(themes[i], assets[i], catalog);

            // --- Project settings ---
            EditorBuildSettings.scenes = System.Array.ConvertAll(themes,
                t => new EditorBuildSettingsScene(SceneFilePath(t), true));
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            AssetDatabase.SaveAssets();
            Debug.Log("[TNT] Level scenes built successfully. Press Play: drag on the building to place TNT, then hit DETONATE.");
        }

        private static string SceneFilePath(LevelTheme theme) => $"{ScenesPath}/{theme.SceneName}.unity";

        // ------------------------------------------------------------------
        // Level themes
        // ------------------------------------------------------------------

        /// <summary>Everything that distinguishes one level's generated content.</summary>
        private sealed class LevelTheme
        {
            public string LevelId;       // PlayerPrefs progress key
            public string DisplayName;   // level select label
            public string SceneName;     // scene file stem, used by navigation
            public string ArtDir;        // Assets/Art/<theme>
            public string PrefabDir;     // Assets/Prefabs/Gameplay/<theme>
            public string DataAssetName; // LevelData_<theme>

            // Grid / tuning
            public int TntCount;
            public int Columns;
            public int Rows;
            public Vector2 BuildOrigin;

            // World palette
            public Color Background;
            public Color BackdropBottom, BackdropTop;
            public Color Ground;
            public Color Line;
            public Color IndicatorValid, IndicatorInvalid;
            public Color BlockBody, BlockBorder;
            public Func<float, float, Color> ChargeShade;

            // UI palette
            public Color StarFilled, StarEmpty;
            public Color Button;
            public Color Cta, CtaLabel;
            public Color HudText;
            public Color PanelVeil;
            public Color MusicIcon;
        }

        /// <summary>Level 1 — Season 6 "Skybound": night blues, teal ocean glow, cursed gold.</summary>
        private static LevelTheme SkyboundTheme()
        {
            return new LevelTheme
            {
                LevelId = "skybound",
                DisplayName = "Skybound",
                SceneName = "Level_01",
                ArtDir = $"{ArtPath}/Skybound",
                PrefabDir = $"{PrefabPath}/Skybound",
                DataAssetName = "LevelData_Skybound",
                TntCount = 3,
                Columns = 5,
                Rows = 6,
                BuildOrigin = new Vector2(-1.1f, 0.25f),
                Background = new Color(0.04f, 0.09f, 0.18f),
                BackdropBottom = new Color(0.10f, 0.28f, 0.33f), // djinn-teal glow
                BackdropTop = new Color(0.03f, 0.06f, 0.14f),    // deep navy
                Ground = new Color(0.09f, 0.14f, 0.22f),
                Line = new Color(0.15f, 0.85f, 0.75f, 0.9f),
                IndicatorValid = new Color(0.25f, 0.9f, 0.8f, 0.65f),
                IndicatorInvalid = new Color(0.9f, 0.3f, 0.2f, 0.4f),
                BlockBody = new Color(0.16f, 0.26f, 0.40f),
                BlockBorder = new Color(0.30f, 0.62f, 0.66f),
                ChargeShade = ShadeCompass,
                StarFilled = new Color(0.95f, 0.75f, 0.20f),
                StarEmpty = new Color(0.50f, 0.42f, 0.24f),
                Button = new Color(0.13f, 0.22f, 0.36f),
                Cta = new Color(0.78f, 0.56f, 0.14f),
                CtaLabel = new Color(0.07f, 0.12f, 0.24f),
                HudText = new Color(0.90f, 0.70f, 0.25f),
                PanelVeil = new Color(0.02f, 0.06f, 0.14f, 0.88f),
                MusicIcon = new Color(0.90f, 0.70f, 0.25f),
            };
        }

        /// <summary>Level 2 — Season 7 "Hands of Time": copper, bronze, rust, steel blue, clockwork gold.</summary>
        private static LevelTheme HandsOfTimeTheme()
        {
            return new LevelTheme
            {
                LevelId = "handsoftime",
                DisplayName = "Hands of Time",
                SceneName = "Level_02",
                ArtDir = $"{ArtPath}/HandsOfTime",
                PrefabDir = $"{PrefabPath}/HandsOfTime",
                DataAssetName = "LevelData_HandsOfTime",
                TntCount = 5,
                Columns = 6,
                Rows = 7,
                BuildOrigin = new Vector2(-1.375f, 0.25f), // centred: 5 gaps x 0.55
                Background = new Color(0.10f, 0.07f, 0.05f),   // deep bronze
                BackdropBottom = new Color(0.45f, 0.22f, 0.10f), // rusty-orange horizon
                BackdropTop = new Color(0.06f, 0.09f, 0.14f),    // steel-blue sky
                Ground = new Color(0.30f, 0.18f, 0.10f),         // rusted metal
                Line = new Color(0.90f, 0.70f, 0.25f, 0.9f),     // clockwork gold
                IndicatorValid = new Color(0.95f, 0.75f, 0.30f, 0.65f),
                IndicatorInvalid = new Color(0.9f, 0.3f, 0.2f, 0.4f),
                BlockBody = new Color(0.25f, 0.35f, 0.45f),      // steel-blue plates
                BlockBorder = new Color(0.85f, 0.60f, 0.22f),    // clockwork-gold edges
                ChargeShade = ShadeGear,
                StarFilled = new Color(0.95f, 0.75f, 0.25f),
                StarEmpty = new Color(0.40f, 0.30f, 0.18f),
                Button = new Color(0.20f, 0.30f, 0.42f),         // steel blue
                Cta = new Color(0.72f, 0.42f, 0.16f),            // copper call to action
                CtaLabel = new Color(0.10f, 0.06f, 0.03f),
                HudText = new Color(0.90f, 0.70f, 0.30f),
                PanelVeil = new Color(0.08f, 0.05f, 0.03f, 0.88f),
                MusicIcon = new Color(0.90f, 0.70f, 0.30f),
            };
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

        // Building block: body plates edged with the theme's accent colour.
        private static Color ShadeBlock(float x, float y, Color body, Color border)
        {
            const float borderWidth = 5f;
            bool isBorder = x < borderWidth || x >= 64f - borderWidth || y < borderWidth || y >= 64f - borderWidth;
            return isBorder ? border : body;
        }

        /// <summary>Vertical gradient: theme glow colour at the bottom, dark sky at the top.</summary>
        private static Color ShadeBackdrop(float x, float y, Color bottom, Color top)
        {
            float t = y / 63f; // 0 at the bottom, 1 at the top
            return Color.Lerp(bottom, top, t);
        }

        /// <summary>
        /// The Skybound charge reads as the Cursed Compass: a cursed-gold rim
        /// around a night-blue face, with a glowing teal needle pointing north
        /// and a dim gold south half.
        /// </summary>
        private static Color ShadeCompass(float x, float y)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f));
            if (d >= 26f && d <= 31f)
                return new Color(0.85f, 0.62f, 0.16f); // cursed gold rim
            if (d > 26f)
                return Color.clear;
            if (d <= 4f)
                return new Color(0.95f, 0.78f, 0.30f); // bright gold centre pin

            // Needle: vertical diamond, teal glow to the north, dim gold south.
            float halfWidth = (1f - Mathf.Abs(y - 32f) / 20f) * 4.5f;
            if (halfWidth > 0f && Mathf.Abs(x - 32f) <= halfWidth)
                return y >= 32f ? new Color(0.25f, 0.90f, 0.80f) : new Color(0.55f, 0.42f, 0.18f);

            return new Color(0.07f, 0.12f, 0.24f); // night-blue face
        }

        /// <summary>
        /// The Hands of Time charge reads as a Chronosteel gear: a toothed
        /// bronze ring around a steel-blue hub, with a glowing clockwork-gold
        /// time vortex at the centre.
        /// </summary>
        private static Color ShadeGear(float x, float y)
        {
            float dx = x - 32f, dy = y - 32f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float angle = Mathf.Atan2(dy, dx);

            // Eight gear teeth: the outer radius breathes between 26 and 31.
            float outer = Mathf.Sin(angle * 8f) > 0f ? 31f : 26f;
            if (d > outer)
                return Color.clear;
            if (d >= 22f)
                return new Color(0.80f, 0.55f, 0.20f); // bronze teeth and rim
            if (d >= 16f)
                return new Color(0.25f, 0.35f, 0.45f); // steel-blue hub ring
            if (d <= 5f)
                return new Color(1f, 0.92f, 0.62f);    // bright time-glow heart

            // Vortex core: spiral bands of glowing gold and steel blue.
            float swirl = Mathf.Sin(angle * 3f + d * 0.55f);
            return swirl > 0f ? new Color(0.95f, 0.80f, 0.40f) : new Color(0.30f, 0.42f, 0.55f);
        }

        private static Color ShadeRing(float x, float y)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f));
            return d >= 24f && d <= 30f ? Color.white : Color.clear;
        }

        /// <summary>
        /// Placeholder music-toggle icon: a quaver (ellipse head + stem) in the
        /// theme's accent colour. The "off" variant carves out a diagonal band,
        /// reading as a slash.
        /// </summary>
        private static Color ShadeMusicNote(float x, float y, bool slashed, Color color)
        {
            float hx = (x - 26f) / 12f, hy = (y - 20f) / 9f;
            bool head = hx * hx + hy * hy <= 1f;
            bool stem = x >= 35f && x < 41f && y >= 20f && y <= 52f;
            if (!head && !stem)
                return Color.clear;
            if (slashed && Mathf.Abs(x - y) < 6f)
                return Color.clear;
            return color;
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

        /// <summary>All assets a generated scene needs, gathered in one bag.</summary>
        private sealed class LevelAssets
        {
            public LevelData Data;
            public GameObject ChargePrefab;
            public Sprite Ring, Pixel, Backdrop, StarFilled, StarEmpty, MusicOn, MusicOff;
        }

        private static LevelAssets BuildLevelAssets(LevelTheme theme)
        {
            EnsureFolder(theme.ArtDir);
            EnsureFolder(theme.PrefabDir);

            var assets = new LevelAssets();
            Sprite blockSprite = CreateSprite($"{theme.ArtDir}/Block.png", 64,
                (x, y) => ShadeBlock(x, y, theme.BlockBody, theme.BlockBorder));
            Sprite chargeSprite = CreateSprite($"{theme.ArtDir}/Charge.png", 64, theme.ChargeShade);
            assets.StarFilled = CreateSprite($"{theme.ArtDir}/StarFilled.png", 64, StarShade(theme.StarFilled));
            assets.StarEmpty = CreateSprite($"{theme.ArtDir}/StarEmpty.png", 64, StarShade(theme.StarEmpty));
            assets.Ring = CreateSprite($"{theme.ArtDir}/Ring.png", 64, ShadeRing);
            assets.Pixel = CreateSprite($"{theme.ArtDir}/Pixel.png", 4, (x, y) => Color.white);
            assets.Backdrop = CreateSprite($"{theme.ArtDir}/Backdrop.png", 64,
                (x, y) => ShadeBackdrop(x, y, theme.BackdropBottom, theme.BackdropTop));
            assets.MusicOn = CreateSprite($"{theme.ArtDir}/MusicOn.png", 64,
                (x, y) => ShadeMusicNote(x, y, false, theme.MusicIcon));
            assets.MusicOff = CreateSprite($"{theme.ArtDir}/MusicOff.png", 64,
                (x, y) => ShadeMusicNote(x, y, true, theme.MusicIcon));

            GameObject blockPrefab = CreateBlockPrefab(theme, blockSprite);
            assets.ChargePrefab = CreateChargePrefab(theme, chargeSprite);
            assets.Data = CreateLevelData(theme, blockPrefab);
            return assets;
        }

        private static GameObject CreateBlockPrefab(LevelTheme theme, Sprite sprite)
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

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{theme.PrefabDir}/Block.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateChargePrefab(LevelTheme theme, Sprite sprite)
        {
            var go = new GameObject("Charge");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 5; // render above the building
            go.transform.localScale = new Vector3(0.35f, 0.35f, 1f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{theme.PrefabDir}/Charge.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static LevelData CreateLevelData(LevelTheme theme, GameObject blockPrefab)
        {
            var data = ScriptableObject.CreateInstance<LevelData>();
            data.levelId = theme.LevelId;
            data.displayName = theme.DisplayName;
            data.sceneName = theme.SceneName;
            data.blockPrefab = blockPrefab.GetComponent<BuildingBlock>();
            data.tntCount = theme.TntCount;
            data.columns = theme.Columns;
            data.rows = theme.Rows;
            data.buildOrigin = theme.BuildOrigin;
            // Blast values (radius 2.5, force 12), the 40/75 star thresholds,
            // the 3 s settle timeout and the demolition line at y = 0.4 keep
            // the class defaults for both levels.

            string path = $"{DataPath}/{theme.DataAssetName}.asset";
            AssetDatabase.CreateAsset(data, path); // overwrites any existing asset
            AssetDatabase.SaveAssets();

            // Re-load: the CreateInstance wrapper can be invalidated when the
            // import replaces the asset, and assigning it would serialize as
            // null. The loaded object is the authoritative persistent asset.
            return AssetDatabase.LoadAssetAtPath<LevelData>(path);
        }

        private static LevelCatalog CreateCatalog(LevelData[] levels)
        {
            var catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            catalog.levels = levels;

            string path = $"{DataPath}/LevelCatalog.asset";
            AssetDatabase.CreateAsset(catalog, path); // overwrites any existing asset
            AssetDatabase.SaveAssets();

            // See CreateLevelData: only the re-loaded asset is authoritative.
            return AssetDatabase.LoadAssetAtPath<LevelCatalog>(path);
        }

        // ------------------------------------------------------------------
        // Scene construction
        // ------------------------------------------------------------------

        private static void BuildScene(LevelTheme theme, LevelAssets assets, LevelCatalog catalog)
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
            cam.backgroundColor = theme.Background;
            cameraGo.AddComponent<AudioListener>();

            // Backdrop — full-screen gradient quad in the theme's sky colours.
            CreateWorldSprite("Backdrop", assets.Backdrop, Color.white,
                new Vector3(0f, 2f, 0f), new Vector3(24f, 14f, 1f), -5);

            // Ground — static collider so debris lands instead of falling forever.
            GameObject ground = CreateWorldSprite("Ground", assets.Pixel, theme.Ground,
                new Vector3(0f, -0.5f, 0f), new Vector3(16f, 1f, 1f), 0);
            ground.AddComponent<BoxCollider2D>().size = Vector2.one; // 16x1 world units after scale

            // Demolition line marker (LevelSetup snaps it to the configured Y).
            GameObject line = CreateWorldSprite("DemolitionLine", assets.Pixel, theme.Line,
                new Vector3(0f, assets.Data.demolitionLineY, 0f), new Vector3(16f, 0.05f, 1f), 1);

            // Placement indicator (hidden until the player touches).
            GameObject indicator = CreateWorldSprite("PlacementIndicator", assets.Ring, theme.IndicatorValid,
                Vector3.zero, new Vector3(0.7f, 0.7f, 1f), 10);
            indicator.SetActive(false);

            // Managers.
            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            gm.EditorSetLevelData(assets.Data);
            gm.EditorSetCatalog(catalog);

            var setupGo = new GameObject("LevelSetup");
            var setup = setupGo.AddComponent<LevelSetup>();
            SetRef(setup, "demolitionLine", line.transform);

            var placementGo = new GameObject("TouchPlacementController");
            var placement = placementGo.AddComponent<TouchPlacementController>();
            SetRef(placement, "chargePrefab", assets.ChargePrefab);
            SetRef(placement, "indicator", indicator.GetComponent<SpriteRenderer>());
            SetRef(placement, "worldCamera", cam);
            SetColor(placement, "validColor", theme.IndicatorValid);
            SetColor(placement, "invalidColor", theme.IndicatorInvalid);

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

            BuildCanvas(theme, gm, assets, catalog);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, SceneFilePath(theme));
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

        private static void BuildCanvas(LevelTheme theme, GameManager gm, LevelAssets assets, LevelCatalog catalog)
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
            tntText.color = theme.HudText;

            // HUD: level select opener (top-left, below the counter).
            Button levelsButton = CreateButton("LevelsButton", canvasGo.transform, "LEVELS", 32,
                theme.Button, new Vector2(0f, 1f), new Vector2(40f, -170f), new Vector2(260f, 100f));

            // HUD: restart button (top-right).
            Button restartButton = CreateButton("RestartButton", canvasGo.transform, "RESTART", 34,
                theme.Button, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(300f, 110f));

            // HUD: music toggle (top-right, left of restart). Icon shows the state.
            Button musicButton = CreateButton("MusicButton", canvasGo.transform, "", 34,
                theme.Button, new Vector2(1f, 1f), new Vector2(-380f, -40f), new Vector2(110f, 110f));
            Object.DestroyImmediate(musicButton.transform.Find("Label").gameObject);
            Image musicIcon = CreateIcon(musicButton.transform, assets.MusicOn);

            // HUD: detonate button (bottom centre) — the theme's call to action.
            Button detonateButton = CreateButton("DetonateButton", canvasGo.transform, "DETONATE", 46,
                theme.Cta, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(460f, 170f));
            detonateButton.GetComponentInChildren<Text>().color = theme.CtaLabel;

            // Result panel (hidden until scoring finishes).
            var panelGo = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelGo.GetComponent<Image>().color = theme.PanelVeil;

            Text resultTitle = CreateText("ResultTitle", panelGo.transform, "LEVEL COMPLETE", 64,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 430f), new Vector2(900f, 130f));
            resultTitle.color = theme.HudText;
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
                starImage.sprite = assets.StarEmpty;
                starImages[i] = starImage;
            }

            // Result panel: onward navigation (next level / level select).
            Button nextLevelButton = CreateButton("NextLevelButton", panelGo.transform, "NEXT LEVEL", 36,
                theme.Cta, new Vector2(0.5f, 0.5f), new Vector2(-200f, -180f), new Vector2(360f, 120f));
            nextLevelButton.GetComponentInChildren<Text>().color = theme.CtaLabel;
            Button levelSelectButton = CreateButton("LevelSelectButton", panelGo.transform, "LEVEL SELECT", 36,
                theme.Button, new Vector2(0.5f, 0.5f), new Vector2(200f, -180f), new Vector2(360f, 120f));

            panelGo.SetActive(false);

            // Restart stays usable in every state: keep it above the full-screen
            // result overlay, both visually and for raycasts.
            restartButton.transform.SetAsLastSibling();

            // Level select overlay (modal; created last so it sits above all HUD).
            LevelSelectUI levelSelectUI = BuildLevelSelectPanel(theme, canvasGo.transform, assets, catalog);

            var ui = canvasGo.AddComponent<UIManager>();
            SetRef(ui, "tntCountText", tntText);
            SetRef(ui, "detonateButton", detonateButton);
            SetRef(ui, "restartButton", restartButton);
            SetRef(ui, "levelsButton", levelsButton);
            SetRef(ui, "musicButton", musicButton);
            SetRef(ui, "musicIcon", musicIcon);
            SetRef(ui, "musicOnSprite", assets.MusicOn);
            SetRef(ui, "musicOffSprite", assets.MusicOff);
            SetRef(ui, "resultPanel", panelGo);
            SetRef(ui, "nextLevelButton", nextLevelButton);
            SetRef(ui, "levelSelectButton", levelSelectButton);
            SetRef(ui, "levelSelectUI", levelSelectUI);
            SetRef(ui, "scoreText", scoreText);
            SetRef(ui, "starFilledSprite", assets.StarFilled);
            SetRef(ui, "starEmptySprite", assets.StarEmpty);

            var so = new SerializedObject(ui);
            SerializedProperty stars = so.FindProperty("starImages");
            stars.arraySize = 3;
            for (int i = 0; i < 3; i++)
                stars.GetArrayElementAtIndex(i).objectReferenceValue = starImages[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Builds the modal level select overlay: a full-screen veil with one
        /// entry row per catalog level. The controller lives on the canvas (not
        /// the panel) so its Start runs even though the panel starts inactive.
        /// </summary>
        private static LevelSelectUI BuildLevelSelectPanel(LevelTheme theme, Transform canvasTransform,
            LevelAssets assets, LevelCatalog catalog)
        {
            var panelGo = new GameObject("LevelSelectPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvasTransform, false);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelGo.GetComponent<Image>().color = theme.PanelVeil;

            Text title = CreateText("Title", panelGo.transform, "SELECT LEVEL", 64,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 430f), new Vector2(900f, 130f));
            title.color = theme.HudText;

            // One row per level, in catalog order (the play test finds them by name).
            var entries = new LevelSelectEntry[2];
            entries[0] = CreateLevelEntry("LevelEntry_Skybound", panelGo.transform,
                new Vector2(0f, 120f), theme, assets);
            entries[1] = CreateLevelEntry("LevelEntry_HandsOfTime", panelGo.transform,
                new Vector2(0f, -80f), theme, assets);

            Button closeButton = CreateButton("CloseButton", panelGo.transform, "CLOSE", 36,
                theme.Button, new Vector2(0.5f, 0.5f), new Vector2(0f, -320f), new Vector2(360f, 120f));

            panelGo.SetActive(false);

            var levelSelectUI = canvasTransform.gameObject.AddComponent<LevelSelectUI>();
            levelSelectUI.EditorSetCatalog(catalog);
            SetRef(levelSelectUI, "panel", panelGo);
            SetRef(levelSelectUI, "closeButton", closeButton);

            var so = new SerializedObject(levelSelectUI);
            SerializedProperty entriesProperty = so.FindProperty("entries");
            entriesProperty.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
                entriesProperty.GetArrayElementAtIndex(i).objectReferenceValue = entries[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return levelSelectUI;
        }

        /// <summary>One level select row: name label, three star slots and a LOCKED overlay.</summary>
        private static LevelSelectEntry CreateLevelEntry(string name, Transform parent, Vector2 anchoredPosition,
            LevelTheme theme, LevelAssets assets)
        {
            Button button = CreateButton(name, parent, "", 40,
                theme.Button, new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(700f, 160f));
            Object.DestroyImmediate(button.transform.Find("Label").gameObject);

            Text nameText = CreateText("NameText", button.transform, "LEVEL", 44,
                TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(320f, 160f));
            nameText.color = theme.HudText;

            var stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var starGo = new GameObject($"Star{i + 1}", typeof(RectTransform), typeof(Image));
                starGo.transform.SetParent(button.transform, false);
                var starRect = (RectTransform)starGo.transform;
                starRect.anchorMin = starRect.anchorMax = new Vector2(1f, 0.5f);
                starRect.anchoredPosition = new Vector2(-250f + i * 100f, 0f);
                starRect.sizeDelta = new Vector2(90f, 90f);
                stars[i] = starGo.GetComponent<Image>();
                stars[i].sprite = assets.StarEmpty;
            }

            Text lockText = CreateText("LockText", button.transform, "LOCKED", 40,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 160f));
            lockText.color = new Color(0.90f, 0.45f, 0.30f);
            lockText.gameObject.SetActive(false);

            var entry = button.gameObject.AddComponent<LevelSelectEntry>();
            SetRef(entry, "button", button);
            SetRef(entry, "nameText", nameText);
            SetRef(entry, "lockText", lockText);
            SetRef(entry, "starFilledSprite", assets.StarFilled);
            SetRef(entry, "starEmptySprite", assets.StarEmpty);

            var so = new SerializedObject(entry);
            SerializedProperty starProperty = so.FindProperty("starImages");
            starProperty.arraySize = 3;
            for (int i = 0; i < 3; i++)
                starProperty.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return entry;
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
