using UnityEngine;

/// <summary>
/// Apply a best-fit resolution for a target aspect once at game startup.
/// This runs exactly once per process via RuntimeInitializeOnLoadMethod.
/// </summary>
public static class ResolutionStartup
{
    // ----- CONFIGURE HERE -----
    private const int targetAspectW = 16;
    private const int targetAspectH = 9;
    private const FullScreenMode desiredMode = FullScreenMode.ExclusiveFullScreen;
    private const bool onlyInBuild = true; // if true, won't run inside the Editor
    private const bool respectPlayerChoice = true; // if true, will not override if player changed resolution in a previous run
    private const string prefsKey = "ResolutionStartup_Applied_v1";
    // --------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void ApplyOnceAtStartup()
    {
        if (onlyInBuild && Application.isEditor)
        {
            Debug.Log("[ResolutionStartup] Editor detected and onlyInBuild==true — skipping resolution change.");
            return;
        }

        if (respectPlayerChoice && PlayerPrefs.HasKey(prefsKey))
        {
            Debug.Log("[ResolutionStartup] Resolution previously applied and respectPlayerChoice==true — skipping.");
            return;
        }

        TryApplyBestFitResolution();
        PlayerPrefs.SetInt(prefsKey, 1);
        PlayerPrefs.Save();
    }

    private static void TryApplyBestFitResolution()
    {
        Resolution native = Screen.currentResolution;
        int nativeW = native.width;
        int nativeH = native.height;

        if (targetAspectW <= 0 || targetAspectH <= 0)
        {
            Debug.LogError("[ResolutionStartup] Invalid target aspect configuration.");
            return;
        }

        float targetAspect = (float)targetAspectW / targetAspectH;
        float nativeAspect = (float)nativeW / nativeH;

        int chosenW, chosenH;
        if (nativeAspect >= targetAspect)
        {
            // native is wider: use full height
            chosenH = nativeH;
            chosenW = Mathf.FloorToInt(chosenH * targetAspect);
        }
        else
        {
            // native is taller: use full width
            chosenW = nativeW;
            chosenH = Mathf.FloorToInt(chosenW / targetAspect);
        }

        chosenW = Mathf.Max(1, chosenW);
        chosenH = Mathf.Max(1, chosenH);

        // if already at desired state, no-op
        if (Screen.width == chosenW && Screen.height == chosenH && Screen.fullScreenMode == desiredMode)
        {
            Debug.Log($"[ResolutionStartup] Already at {chosenW}x{chosenH} ({targetAspectW}:{targetAspectH}) in mode {desiredMode}.");
            return;
        }

        Debug.Log($"[ResolutionStartup] Requesting resolution {chosenW}x{chosenH} ({targetAspectW}:{targetAspectH}). Native: {nativeW}x{nativeH}. Mode: {desiredMode}.");
        Screen.SetResolution(chosenW, chosenH, desiredMode);
    }

    /// <summary>
    /// Call this if you want to clear the "already applied" flag so the resolution will be reapplied next launch.
    /// Useful for testing from the Editor or giving the user a 'reset' button.
    /// </summary>
    public static void ResetAppliedFlag()
    {
        if (PlayerPrefs.HasKey(prefsKey))
        {
            PlayerPrefs.DeleteKey(prefsKey);
            PlayerPrefs.Save();
            Debug.Log("[ResolutionStartup] Cleared applied flag.");
        }
        else
        {
            Debug.Log("[ResolutionStartup] No applied flag to clear.");
        }
    }
}
