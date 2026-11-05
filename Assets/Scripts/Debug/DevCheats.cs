#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.SceneManagement;
using Aeternal.Combat;
using Aeternal.Stats;

namespace Aeternal.DevTools
{
    /// <summary>
    /// Developer cheats for playtesting. Spawns itself automatically on game start
    /// (no scene wiring required) and shows a small overlay while in combat:
    ///   - WIN (F1): instantly kills the enemy, triggering the normal victory flow
    ///     (victory/reward panels, run advancement) so the whole loop can be tested fast.
    ///   - LOSE (F2): kills the player, for testing the defeat flow.
    /// Compiled ONLY in the Editor and development builds — never ships in a release build.
    /// </summary>
    public class DevCheats : MonoBehaviour
    {
        private static DevCheats instance;

        private CombatManager combat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("DevCheats");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DevCheats>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            FindCombat();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            FindCombat();
        }

        private void FindCombat()
        {
            combat = FindFirstObjectByType<CombatManager>();
        }

        private void Update()
        {
            if (Aeternal.UI.PauseMenu.IsPaused) return;
            if (Input.GetKeyDown(KeyCode.F1)) ForceWin();
            if (Input.GetKeyDown(KeyCode.F2)) ForceLose();
        }

        private void OnGUI()
        {
            if (combat == null || combat.IsGameOver) return;

            const float w = 130f, h = 38f, pad = 10f;
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f, 0.9f);
            if (GUI.Button(new Rect(pad, Screen.height - h - pad, w, h), "DEV: WIN (F1)"))
                ForceWin();

            GUI.backgroundColor = new Color(0.8f, 0.25f, 0.2f, 0.9f);
            if (GUI.Button(new Rect(pad + w + 8f, Screen.height - h - pad, w, h), "DEV: LOSE (F2)"))
                ForceLose();
        }

        private void ForceWin()
        {
            if (combat == null || combat.IsGameOver || combat.enemy == null) return;
            Debug.Log("[DevCheats] Force WIN — killing enemy.");
            combat.enemy.GetComponent<HealthComponent>()?.Kill();
        }

        private void ForceLose()
        {
            if (combat == null || combat.IsGameOver || combat.playerHealth == null) return;
            Debug.Log("[DevCheats] Force LOSE — killing player.");
            combat.playerHealth.Kill();
        }
    }
}
#endif
