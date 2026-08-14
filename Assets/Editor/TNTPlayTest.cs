using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TNTGame.Core;

namespace TNTGame.EditorTools
{
    /// <summary>
    /// Automated smoke tests for the core loop, run in two modes:
    ///
    /// TNT/Run Play Test — opens Level_01, enters Play mode, places all charges
    /// on bottom-row blocks, detonates, waits for the score, then restarts and
    /// verifies the level returns to its initial state.
    ///
    /// TNT/Run Progression Test — plays Level_01 to the score screen, checks the
    /// earned stars were saved, follows NEXT LEVEL into Level_02 (verifying its
    /// grid, TNT count and persistent audio), then opens the level select and
    /// jumps back to Level_01.
    ///
    /// Headless:
    /// Unity -batchmode -projectPath &lt;path&gt; -executeMethod TNTGame.EditorTools.TNTPlayTest.BuildAndPlayTest
    /// (no -quit; the test exits the editor itself with code 0 on success, 1 on failure).
    ///
    /// Test state lives in SessionState so it survives the domain reload that
    /// happens when Play mode is entered.
    /// </summary>
    public static class TNTPlayTest
    {
        private const string Level1ScenePath = "Assets/Scenes/Level_01.unity";
        private const string RunningKey = "TNT.PlayTest.Running";
        private const string PhaseKey = "TNT.PlayTest.Phase";
        private const string DeadlineKey = "TNT.PlayTest.Deadline";
        private const string ModeKey = "TNT.PlayTest.Mode";
        private const string WasMaximizedKey = "TNT.PlayTest.GameViewWasMaximized";

        private enum Phase
        {
            WaitForLevel = 0,
            PlaceCharges = 1,
            WaitForScore = 2,
            Restarting = 3,
            AwaitingNextLevel = 4,
            BackToLevel1 = 5,
            InLevelSelect = 6
        }

        private enum TestMode
        {
            Restart = 0,
            Progression = 1
        }

        /// <summary>Thrown by Fail to abort the current tick; caught in Tick.</summary>
        private sealed class TestFailedException : Exception
        {
            public TestFailedException(string message) : base(message) { }
        }

        [MenuItem("TNT/Run Play Test")]
        public static void BuildAndPlayTest()
        {
            StartTest(TestMode.Restart);
        }

        [MenuItem("TNT/Run Progression Test")]
        public static void RunProgressionTest()
        {
            StartTest(TestMode.Progression);
        }

        private static void StartTest(TestMode mode)
        {
            if (!File.Exists(Level1ScenePath))
                TNTSceneBuilder.BuildAll();

            // Deterministic state: music starts on, no saved progress.
            PlayerPrefs.DeleteKey("TNT.MusicOn");
            ProgressManager.DeleteStars("skybound");
            ProgressManager.DeleteStars("handsoftime");
            PlayerPrefs.Save();

            EditorSceneManager.OpenScene(Level1ScenePath);
            SessionState.SetBool(RunningKey, true);
            SessionState.SetInt(PhaseKey, (int)Phase.WaitForLevel);
            SessionState.SetInt(ModeKey, (int)mode);
            SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 120f);
            EditorApplication.update += Tick;
            EnsureGameView();
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// The UI raycasts need a real screen area: with no visible Game view
        /// the canvas has no valid size and every click misses. A squashed or
        /// tab-hidden Game view is just as bad — it stops repainting, so the
        /// canvas keeps a stale layout while Screen reports a degenerate size
        /// (e.g. 2940x40) and every raycast misses. Opening/focusing a Game
        /// view is not enough either, so maximize it: a maximized view stays
        /// visible and repaints for the whole run, keeping Screen and the
        /// canvas layout consistent at whatever size the window provides.
        /// The view is also reset to Free Aspect: with a fixed-resolution
        /// entry selected the canvas lays out at that resolution while
        /// Screen keeps reporting the window size, and clicks miss buttons
        /// that are visibly under the pointer.
        /// TickPhase re-asserts this until Screen looks sane, since the new
        /// size can take a repaint to apply after play mode starts; the
        /// previous maximized state is restored in Finish. Batch mode always
        /// has a virtual screen, so it needs none of this.
        /// </summary>
        private static void EnsureGameView()
        {
            if (Application.isBatchMode)
                return;

            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
                return;

            var gameView = EditorWindow.GetWindow(gameViewType);
            if (!gameView.maximized)
            {
                SessionState.SetBool(WasMaximizedKey, false);
                gameView.maximized = true;
            }

            SelectFreeAspect(gameView, gameViewType);
        }

        /// <summary>
        /// Selects "Free Aspect" in the Game view's size dropdown via the
        /// internal GameViewSizes API (there is no public API for it).
        /// Best-effort: if the internals change, the current selection stays
        /// and the test still runs against whatever size is active.
        /// </summary>
        private static void SelectFreeAspect(EditorWindow gameView, Type gameViewType)
        {
            try
            {
                var sizesType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");
                object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType)
                    .GetProperty("instance").GetValue(null);
                object group = sizesType.GetProperty("currentGroup").GetValue(sizes);
                var getSize = group.GetType().GetMethod("GetGameViewSize");

                int freeAspect = -1;
                for (int i = 0; i < 32; i++)
                {
                    object size;
                    try { size = getSize.Invoke(group, new object[] { i }); }
                    catch { break; } // walked past the end of the list
                    if ((bool)size.GetType().GetProperty("isFreeAspectRatio").GetValue(size))
                    {
                        freeAspect = i;
                        break;
                    }
                }

                if (freeAspect >= 0)
                    gameViewType.GetProperty("selectedSizeIndex").SetValue(gameView, freeAspect);
                else
                    Debug.LogWarning("[TNT] No Free Aspect entry found in the Game view sizes.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TNT] Could not reset the Game view to Free Aspect: {e.Message}");
            }
        }

        /// <summary>
        /// A screen below this in either axis means the Game view has not
        /// applied a real render surface yet (or is hidden) — UI positions
        /// read from it would be off-screen. 100px is far below any usable
        /// size, including the headless virtual screen.
        /// </summary>
        private static bool ScreenIsDegenerate()
        {
            return !Application.isBatchMode && (Screen.width < 100 || Screen.height < 100);
        }

        [InitializeOnLoadMethod]
        private static void ResumeAfterDomainReload()
        {
            if (SessionState.GetBool(RunningKey, false))
                EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(RunningKey, false))
            {
                EditorApplication.update -= Tick;
                return;
            }

            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0f))
                    Fail("Timed out.");

                TickPhase();
            }
            catch (TestFailedException)
            {
                // Fail() already logged the reason and finished the run; the
                // throw just stops the current tick from running on with a
                // known-bad state (and producing misleading follow-up errors).
            }
        }

        private static void TickPhase()
        {
            GameManager gm = GameManager.Instance;
            switch ((Phase)SessionState.GetInt(PhaseKey, 0))
            {
                case Phase.WaitForLevel:
                {
                    if (gm == null || gm.State != LevelState.Placing || gm.BlockCount == 0)
                        return; // scene still initialising

                    if (ScreenIsDegenerate())
                    {
                        // The maximized Game view can take a repaint to report
                        // its new size after play mode starts — re-assert and
                        // wait a tick rather than clicking against a stale
                        // canvas layout.
                        EnsureGameView();
                        return;
                    }

                    Expect(gm.Data.levelId == "skybound", $"Test must start in Level 1, found '{gm.Data.levelId}'.");
                    Expect(gm.BlockCount == gm.Data.columns * gm.Data.rows,
                        $"Expected {gm.Data.columns * gm.Data.rows} blocks, found {gm.BlockCount}.");
                    Expect(gm.TntRemaining == gm.Data.tntCount, "TNT count should start full.");
                    Expect(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1,
                        "Exactly one AudioListener must be present.");

                    // Music toggle via the HUD button (real EventSystem clicks).
                    Expect(AudioManager.Instance != null, "AudioManager missing from the scene.");
                    Expect(AudioManager.Instance.IsMusicOn, "Music should default to on.");
                    ClickButton("MusicButton");
                    Expect(!AudioManager.Instance.IsMusicOn, "Music button should toggle music off.");
                    ClickButton("MusicButton");
                    Expect(AudioManager.Instance.IsMusicOn, "Music button should toggle music back on.");

                    // Visual verification artifact (read back after the run).
                    ScreenCapture.CaptureScreenshot(Path.Combine("Logs", "shot_start.png"));
                    SessionState.SetInt(PhaseKey, (int)Phase.PlaceCharges);
                    break;
                }
                case Phase.PlaceCharges:
                {
                    // Attach all charges across the bottom row.
                    for (int i = 0; i < gm.Data.tntCount; i++)
                    {
                        int blockIndex = i * (gm.Data.columns - 1) / Math.Max(1, gm.Data.tntCount - 1);
                        Vector2 pos = gm.Blocks[blockIndex].transform.position;
                        Expect(gm.TryPlaceCharge(pos), $"Charge {i + 1} should be placeable.");
                    }
                    Expect(gm.TntRemaining == 0, "All charges should be consumed.");
                    Expect(!gm.TryPlaceCharge(Vector2.zero), "A charge beyond the limit must be rejected.");

                    gm.Detonate();
                    Expect(gm.State == LevelState.Detonating, "State should be Detonating after Detonate().");
                    SessionState.SetInt(PhaseKey, (int)Phase.WaitForScore);
                    break;
                }
                case Phase.WaitForScore:
                {
                    if (gm.State != LevelState.Scored)
                        return; // physics still settling

                    Debug.Log($"[TNT] Blast result: {gm.ScorePercent:0.0}% destroyed, {gm.Stars} star(s).");
                    Expect(gm.ScorePercent > 10f, "Blast should destroy a measurable share of the building.");
                    Expect(gm.Stars >= 1 && gm.Stars <= 3, "Stars must be within 1..3.");

                    // Result panel (logo + stars) visible at this point.
                    ScreenCapture.CaptureScreenshot(Path.Combine("Logs", "shot_result.png"));

                    if ((TestMode)SessionState.GetInt(ModeKey, 0) == TestMode.Progression)
                    {
                        Expect(ProgressManager.GetStars("skybound") > 0,
                            "Completing Level 1 should save its stars.");
                        ClickButton("NextLevelButton");
                        SessionState.SetInt(PhaseKey, (int)Phase.AwaitingNextLevel);
                    }
                    else
                    {
                        ClickButton("RestartButton");
                        SessionState.SetInt(PhaseKey, (int)Phase.Restarting);
                    }
                    break;
                }
                case Phase.Restarting:
                {
                    // Until the reload finishes we still see the old, scored instance.
                    if (gm == null || gm.State != LevelState.Placing || gm.BlockCount == 0)
                        return;

                    Expect(gm.TntRemaining == gm.Data.tntCount, "TNT count should be restored after restart.");
                    Expect(gm.BlockCount == gm.Data.columns * gm.Data.rows, "All blocks should be back after restart.");
                    Expect(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1,
                        "Exactly one AudioListener must be present after restart.");
                    Expect(AudioManager.Instance != null && AudioManager.Instance.IsMusicOn,
                        "Music preference should persist across the restart.");
                    Pass();
                    break;
                }
                case Phase.AwaitingNextLevel:
                {
                    // Until the scene swap finishes we still see the Level 1 instance.
                    if (gm == null || gm.Data.levelId != "handsoftime"
                        || gm.State != LevelState.Placing || gm.BlockCount == 0)
                        return;

                    Expect(SceneManager.GetActiveScene().name == "Level_02",
                        "Next Level should load the Level_02 scene.");
                    Expect(gm.Data.columns == 6 && gm.Data.rows == 7,
                        $"Hands of Time should use a 6x7 grid, found {gm.Data.columns}x{gm.Data.rows}.");
                    Expect(gm.BlockCount == 42, $"Expected 42 blocks, found {gm.BlockCount}.");
                    Expect(gm.Data.tntCount == 5, $"Hands of Time should allow 5 TNT, found {gm.Data.tntCount}.");
                    Expect(gm.TntRemaining == 5, "Level 2 TNT count should start full.");
                    Expect(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1,
                        "Exactly one AudioListener must be present in Level 2.");
                    Expect(AudioManager.Instance != null && AudioManager.Instance.IsMusicOn,
                        "Music preference should persist into Level 2.");

                    ScreenCapture.CaptureScreenshot(Path.Combine("Logs", "shot_level2.png"));

                    // Level select: opens modally, both entries unlocked by now.
                    ClickButton("LevelsButton");
                    Expect(GameObject.Find("LevelSelectPanel") != null,
                        "Level select panel should be active after clicking LEVELS.");
                    var level1Entry = GameObject.Find("LevelEntry_Skybound");
                    var level2Entry = GameObject.Find("LevelEntry_HandsOfTime");
                    Expect(level1Entry != null && level2Entry != null, "Both level entries must exist.");
                    Expect(level1Entry.GetComponent<Button>().interactable,
                        "Level 1 entry must stay unlocked.");
                    Expect(level2Entry.GetComponent<Button>().interactable,
                        "Level 2 entry should be unlocked after completing Level 1.");

                    SessionState.SetInt(PhaseKey, (int)Phase.InLevelSelect);
                    break;
                }
                case Phase.InLevelSelect:
                {
                    // One tick after opening: freshly activated UI needs a canvas
                    // update before its screen positions are raycastable.
                    Expect(GameObject.Find("LevelSelectPanel") != null,
                        "Level select panel should still be open.");
                    ScreenCapture.CaptureScreenshot(Path.Combine("Logs", "shot_levelselect.png"));
                    ClickButton("LevelEntry_Skybound");
                    SessionState.SetInt(PhaseKey, (int)Phase.BackToLevel1);
                    break;
                }
                case Phase.BackToLevel1:
                {
                    if (gm == null || gm.Data.levelId != "skybound"
                        || gm.State != LevelState.Placing || gm.BlockCount == 0)
                        return;

                    Expect(gm.BlockCount == gm.Data.columns * gm.Data.rows,
                        "Level 1 should be fully rebuilt when re-entered.");
                    Expect(gm.TntRemaining == gm.Data.tntCount,
                        "Level 1 TNT count should start full when re-entered.");
                    Pass();
                    break;
                }
            }
        }

        /// <summary>
        /// Clicks a HUD button through the real UI path: raycasts at the button's
        /// screen position (fails if something else, e.g. the result panel,
        /// intercepts the click) and then fires a genuine pointer click through
        /// the EventSystem.
        /// </summary>
        private static void ClickButton(string buttonName)
        {
            var buttonGo = GameObject.Find(buttonName);
            Expect(buttonGo != null, $"{buttonName} not found in the scene.");

            // A button may have become visible this very frame (e.g. the level
            // select panel); force the canvas to catch up before reading
            // screen positions, or the raycast below can miss everything.
            Canvas.ForceUpdateCanvases();

            Expect(Screen.width > 0 && Screen.height > 0,
                "Screen size is zero — open a Game view (or run headless) before running the test.");

            var eventSystem = EventSystem.current;
            Expect(eventSystem != null, "No EventSystem in the scene.");

            var raycaster = UnityEngine.Object.FindFirstObjectByType<GraphicRaycaster>();
            Expect(raycaster != null, "No GraphicRaycaster in the scene.");

            var pointer = new PointerEventData(eventSystem)
            {
                position = buttonGo.transform.position, // overlay canvas: world == screen
                button = PointerEventData.InputButton.Left
            };

            var results = new List<RaycastResult>();
            raycaster.Raycast(pointer, results);
            Expect(results.Count > 0,
                $"Nothing hit at {buttonName}'s position ({pointer.position.x:0},{pointer.position.y:0}; screen {Screen.width}x{Screen.height}).");

            GameObject clickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(results[0].gameObject);
            Expect(clickTarget == buttonGo,
                $"{buttonName} blocked by '{results[0].gameObject.name}' — no overlay may intercept clicks.");

            ExecuteEvents.Execute(buttonGo, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void Expect(bool condition, string message)
        {
            if (!condition)
                Fail(message);
        }

        private static void Pass()
        {
            Debug.Log("[TNT] Play test PASSED.");
            Finish(0);
        }

        private static void Fail(string reason)
        {
            Debug.LogError($"[TNT] Play test FAILED: {reason}");
            Finish(1);
            throw new TestFailedException(reason);
        }

        private static void Finish(int exitCode)
        {
            SessionState.SetBool(RunningKey, false);
            EditorApplication.update -= Tick;

            if (EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();

            // Give the user's Game view its previous dock state back.
            if (!Application.isBatchMode && !SessionState.GetBool(WasMaximizedKey, true))
            {
                SessionState.EraseBool(WasMaximizedKey);
                var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType != null)
                    EditorWindow.GetWindow(gameViewType).maximized = false;
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }
    }
}
