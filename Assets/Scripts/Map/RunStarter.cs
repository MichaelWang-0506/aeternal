using UnityEngine;
using UnityEngine.SceneManagement;
using Aeternal.Core;
using Aeternal.Encounters;

namespace Aeternal.Map
{
    /// <summary>
    /// Hook for a main-menu "New Game" button. Starts a fresh run on the given map and enters
    /// it (the map scene, or straight into the first fight for a region without a map).
    /// The first time, it plays the tutorial first and the run starts when the tutorial ends.
    /// Continue resumes the saved run. Ensures a RunManager exists (creates one if the scene has none).
    /// Wire StartRun() to the button's OnClick.
    /// </summary>
    public class RunStarter : MonoBehaviour
    {
        const string TutorialSeenKey = "Aeternal.TutorialSeen";

        [Tooltip("The region/map this run begins on.")]
        public MapData startingMap;

        [Tooltip("Scene to load after starting the run (the map), for regions that show one.")]
        public string mapSceneName = "MapScene";

        [Tooltip("Played before the first run. Leave blank to never play it.")]
        public string tutorialSceneName = "TutorialScene";

        /// <summary>A run waiting for the tutorial to finish before it starts.</summary>
        static MapData pendingMap;
        static string pendingMapScene;

        /// <summary>Whether the player has finished (or skipped) the tutorial. Saved on this machine.</summary>
        public static bool TutorialSeen
        {
            get => PlayerPrefs.GetInt(TutorialSeenKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(TutorialSeenKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Where a new game begins.</summary>
        public enum StartPoint
        {
            Tutorial,       // the tutorial scene, then the starting map
            Prologue,       // the starting map (the story from the beginning)
            FirstRegion,    // the starting map's nextMap, skipping the prologue
        }

        /// <summary>Whether this starter can play a tutorial before the run.</summary>
        public bool HasTutorial => !string.IsNullOrEmpty(tutorialSceneName);

        /// <summary>Starts the run, playing the tutorial first if the player hasn't seen it.</summary>
        public void StartRun() => StartRun(!TutorialSeen && HasTutorial ? StartPoint.Tutorial : StartPoint.Prologue);

        public void StartRun(StartPoint start)
        {
            if (startingMap == null)
            {
                Debug.LogError("[RunStarter] No startingMap assigned.");
                return;
            }

            if (start == StartPoint.Tutorial && HasTutorial)
            {
                pendingMap = startingMap;
                pendingMapScene = mapSceneName;
                SceneManager.LoadScene(tutorialSceneName);
                return;
            }

            var map = start == StartPoint.FirstRegion && startingMap.nextMap != null ? startingMap.nextMap : startingMap;
            Begin(map, mapSceneName);
        }

        /// <summary>
        /// Starts the run held back for the tutorial, if there is one. Returns false when no
        /// run is waiting (the tutorial was opened on its own).
        /// </summary>
        public static bool StartPendingRun()
        {
            if (pendingMap == null) return false;
            var map = pendingMap;
            pendingMap = null;
            Begin(map, pendingMapScene);
            return true;
        }

        /// <summary>Whether there is a saved run for Continue.</summary>
        public static bool CanContinue => RunSave.Exists;

        /// <summary>
        /// Loads the saved run and resumes it: on the map, or at the start of the fight it was
        /// saved in. Returns false (and deletes the save) if it can't be loaded.
        /// </summary>
        public bool ContinueRun()
        {
            var data = RunSave.Read();
            var run = EnsureRunManager();
            if (data == null || startingMap == null || !run.Restore(data, RunCatalog.Build(startingMap)))
            {
                Debug.LogWarning("[RunStarter] The saved run couldn't be loaded; it has been discarded.");
                run.ResetRun();
                return false;
            }

            if (data.inCombat && run.CurrentEncounter != null) run.LoadCombat();
            else SceneManager.LoadScene(run.mapSceneName);
            return true;
        }

        static void Begin(MapData map, string mapScene)
        {
            var run = EnsureRunManager();
            run.mapSceneName = mapScene;
            run.StartRun(map);
            run.EnterRegion();
        }

        static RunManager EnsureRunManager()
        {
            var run = RunManager.Instance;
            if (run == null)
            {
                var go = new GameObject("RunManager");
                run = go.AddComponent<RunManager>();
            }
            return run;
        }
    }
}
