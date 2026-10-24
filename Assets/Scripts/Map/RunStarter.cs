using UnityEngine;
using UnityEngine.SceneManagement;
using Aeternal.Core;
using Aeternal.Encounters;

namespace Aeternal.Map
{
    /// <summary>
    /// Hook for a main-menu "Play / New Run" button. Starts a fresh run on the given map
    /// and loads the map scene. Ensures a RunManager exists (creates one if the scene has none).
    /// Wire StartRun() to the button's OnClick.
    /// </summary>
    public class RunStarter : MonoBehaviour
    {
        [Tooltip("The region/map this run begins on.")]
        public MapData startingMap;

        [Tooltip("Scene to load after starting the run (the map).")]
        public string mapSceneName = "MapScene";

        public void StartRun()
        {
            if (startingMap == null)
            {
                Debug.LogError("[RunStarter] No startingMap assigned.");
                return;
            }

            var run = RunManager.Instance;
            if (run == null)
            {
                var go = new GameObject("RunManager");
                run = go.AddComponent<RunManager>();
            }

            run.StartRun(startingMap);
            SceneManager.LoadScene(mapSceneName);
        }
    }
}
