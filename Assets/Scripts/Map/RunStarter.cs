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
    /// Ensures a RunManager exists (creates one if the scene has none).
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

        public void StartRun()
        {
            if (startingMap == null)
            {
                Debug.LogError("[RunStarter] No startingMap assigned.");
                return;
            }

            if (!TutorialSeen && !string.IsNullOrEmpty(tutorialSceneName))
            {
                pendingMap = startingMap;
                pendingMapScene = mapSceneName;
                SceneManager.LoadScene(tutorialSceneName);
                return;
            }

            Begin(startingMap, mapSceneName);
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

        static void Begin(MapData map, string mapScene)
        {
            var run = RunManager.Instance;
            if (run == null)
            {
                var go = new GameObject("RunManager");
                run = go.AddComponent<RunManager>();
            }

            run.mapSceneName = mapScene;
            run.StartRun(map);
            run.EnterRegion();
        }
    }
}
