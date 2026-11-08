using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Aeternal.EditorTools
{
    /// <summary>
    /// Play in the editor always starts at the main menu, whatever scene is open, so the game runs
    /// the way players see it. Aeternal > Play From Main Menu turns it off (checked: on), to test
    /// the open scene on its own. The choice is kept per machine (EditorPrefs).
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromMainMenu
    {
        const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        const string MenuPath = "Aeternal/Play From Main Menu";
        const string PrefKey = "Aeternal.PlayFromMainMenu";

        static PlayFromMainMenu() => EditorApplication.delayCall += Apply;

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
