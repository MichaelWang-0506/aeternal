using UnityEngine;

namespace Aeternal.UI
{
    /// <summary>
    /// Player settings shared by the main menu and the pause menu, saved on this machine.
    /// </summary>
    public static class GameSettings
    {
        const string VolumeKey = "Aeternal.MasterVolume";
        public const float VolumeStep = 0.1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplySaved()
        {
            AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        }

        /// <summary>Master volume, 0-1 in steps of 10%.</summary>
        public static float Volume => AudioListener.volume;

        /// <summary>Raises or lowers the master volume by delta and saves it.</summary>
        public static void ChangeVolume(float delta)
        {
            float volume = Mathf.Clamp01(Mathf.Round((AudioListener.volume + delta) * 10f) / 10f);
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat(VolumeKey, volume);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Toggles fullscreen. Screen.fullScreen applies at the end of the frame, so this
        /// returns the requested state for labels to show.
        /// </summary>
        public static bool ToggleFullscreen()
        {
            bool fullscreen = !Screen.fullScreen;
            Screen.fullScreen = fullscreen;
            return fullscreen;
        }

        public static string VolumeLabel => $"Volume: {Mathf.RoundToInt(AudioListener.volume * 100f)}%";

        public static string FullscreenLabel(bool fullscreen) => $"Fullscreen: {(fullscreen ? "On" : "Off")}";
    }
}
