using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TNTGame.Core;

namespace TNTGame.EditorTools
{
    /// <summary>
    /// Automated smoke test for the core loop: opens Level_01, enters Play mode,
    /// places all three charges on bottom-row blocks, detonates, waits for the
    /// score, then restarts and verifies the level returns to its initial state.
    ///
    /// Run via the TNT menu, or headless:
    /// Unity -batchmode -projectPath &lt;path&gt; -executeMethod TNTGame.EditorTools.TNTPlayTest.BuildAndPlayTest
    /// (no -quit; the test exits the editor itself with code 0 on success, 1 on failure).
    ///
    /// Test state lives in SessionState so it survives the domain reload that
    /// happens when Play mode is entered.
    /// </summary>
    public static class TNTPlayTest
    {
        private const string ScenePath = "Assets/Scenes/Level_01.unity";
        private const string RunningKey = "TNT.PlayTest.Running";
        private const string PhaseKey = "TNT.PlayTest.Phase";
        private const string DeadlineKey = "TNT.PlayTest.Deadline";

        private enum Phase
        {
            WaitForLevel = 0,
            PlaceCharges = 1,
            WaitForScore = 2,
            Restarting = 3
        }

        [MenuItem("TNT/Run Play Test")]
        public static void BuildAndPlayTest()
        {
            if (!File.Exists(ScenePath))
                TNTSceneBuilder.BuildAll();

            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool(RunningKey, true);
            SessionState.SetInt(PhaseKey, (int)Phase.WaitForLevel);
            SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 120f);
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
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

            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0f))
            {
                Fail("Timed out.");
                return;
            }

            GameManager gm = GameManager.Instance;
            switch ((Phase)SessionState.GetInt(PhaseKey, 0))
            {
                case Phase.WaitForLevel:
                {
                    if (gm == null || gm.State != LevelState.Placing || gm.BlockCount == 0)
                        return; // scene still initialising

                    int expected = gm.Data.columns * gm.Data.rows;
                    Expect(gm.BlockCount == expected, $"Expected {expected} blocks, found {gm.BlockCount}.");
                    Expect(gm.TntRemaining == gm.Data.tntCount, "TNT count should start full.");
                    SessionState.SetInt(PhaseKey, (int)Phase.PlaceCharges);
                    break;
                }
                case Phase.PlaceCharges:
                {
                    // Attach all three charges across the bottom row.
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

                    gm.RestartLevel();
                    SessionState.SetInt(PhaseKey, (int)Phase.Restarting);
                    break;
                }
                case Phase.Restarting:
                {
                    // Until the reload finishes we still see the old, scored instance.
                    if (gm == null || gm.State != LevelState.Placing || gm.BlockCount == 0)
                        return;

                    Expect(gm.TntRemaining == gm.Data.tntCount, "TNT count should be restored after restart.");
                    Expect(gm.BlockCount == gm.Data.columns * gm.Data.rows, "All blocks should be back after restart.");
                    Pass();
                    break;
                }
            }
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
        }

        private static void Finish(int exitCode)
        {
            SessionState.SetBool(RunningKey, false);
            EditorApplication.update -= Tick;

            if (EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();

            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }
    }
}
