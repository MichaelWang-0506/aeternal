using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Aeternal.EditorTools
{
    /// <summary>
    /// Play in the editor always starts at the main menu, whatever scene is open, so the game runs
    /// the way players see it. Aeternal > Play From Main Menu turns it off (checked: on), to test
    /// the open scene on its own. The choice is kept per machine (EditorPrefs).
    /// It also opens the main menu scene once when the project is opened, so the editor (and a
    /// freshly unzipped copy, which would otherwise show an empty Untitled scene) starts there.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromMainMenu
    {
        const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        const string MenuPath = "Aeternal/Play From Main Menu";
        const string PrefKey = "Aeternal.PlayFromMainMenu";
        const string OpenedKey = "Aeternal.OpenedMainMenu"; // set once per editor session

        static PlayFromMainMenu()
        {
            EditorApplication.delayCall += Apply;
            EditorApplication.delayCall += OpenMainMenuOnStartup;
        }

        /// <summary>
        /// On the first load of an editor session, switches the open scene to the main menu.
        /// SessionState survives script reloads but not an editor restart, so this runs once
        /// per project open. Skipped in batch builds, in Play mode, and when the open scene
        /// has unsaved changes.
        /// </summary>
        static void OpenMainMenuOnStartup()
        {
            if (SessionState.GetBool(OpenedKey, false)) return;
            SessionState.SetBool(OpenedKey, true);
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;

            var active = EditorSceneManager.GetActiveScene();
            if (active.path == MainMenuPath || active.isDirty) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath) == null) return;
            EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        }

        static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
            Debug.Log(Enabled ? "[Aeternal] Play now starts from the main menu."
                              : "[Aeternal] Play now starts from the open scene.");
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        static void Apply()
        {
            var mainMenu = Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath) : null;
            if (Enabled && mainMenu == null) Debug.LogWarning($"[Aeternal] {MainMenuPath} not found; Play starts from the open scene.");
            EditorSceneManager.playModeStartScene = mainMenu;
        }
    }
}
