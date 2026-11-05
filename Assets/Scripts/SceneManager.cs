using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Key to press to reset the current scene")]
    public KeyCode resetKey = KeyCode.R;
    
    [Tooltip("Key to press to quit the game")]
    public KeyCode quitKey = KeyCode.Q;
    
    [Tooltip("Enable/disable scene reset functionality")]
    public bool enableReset = true;
    
    [Tooltip("Enable/disable quit functionality")]
    public bool enableQuit = true;

    void Start()
    {
        
    }

    void Update()
    {
        if (Aeternal.UI.PauseMenu.IsPaused) return;
        // Reset scene on R key press
        if (enableReset && Input.GetKeyDown(resetKey))
        {
            ResetScene();
        }
        // Quit game on Escape key press
        if (enableQuit && Input.GetKeyDown(quitKey))
        {
            Debug.Log("trigger");
            if (SceneManager.GetActiveScene().name.Equals("MainMenu")) {Debug.Log("triggerQuit"); QuitGame(); }
            else {Debug.Log("triggerReturnMainMenu"); LoadScene("MainMenu"); }
            
        }
    }
    
    /// <summary>
    /// Resets the current scene by reloading it
    /// </summary>
    public void ResetScene()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentSceneIndex);
    }
    
    /// <summary>
    /// Loads a scene by name
    /// </summary>
    /// <param name="sceneName">Name of the scene to load</param>
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
    
    /// <summary>
    /// Loads a scene by build index
    /// </summary>
    /// <param name="sceneIndex">Build index of the scene to load</param>
    public void LoadScene(int sceneIndex)
    {
        SceneManager.LoadScene(sceneIndex);
    }
    
    /// <summary>
    /// Loads the next scene in the build settings
    /// </summary>
    public void LoadNextScene()
    {
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            Debug.LogWarning("No next scene available. Already at the last scene.");
        }
    }
    
    /// <summary>
    /// Loads the previous scene in the build settings
    /// </summary>
    public void LoadPreviousScene()
    {
        int previousSceneIndex = SceneManager.GetActiveScene().buildIndex - 1;
        if (previousSceneIndex >= 0)
        {
            SceneManager.LoadScene(previousSceneIndex);
        }
        else
        {
            Debug.LogWarning("No previous scene available. Already at the first scene.");
        }
    }
    
    /// <summary>
    /// Quits the game (works in builds, stops play mode in editor)
    /// </summary>
    public void QuitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
    
    /// <summary>
    /// Reloads the current scene asynchronously
    /// </summary>
    public void ResetSceneAsync()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        StartCoroutine(LoadSceneAsyncCoroutine(currentSceneIndex));
    }
    
    /// <summary>
    /// Loads a scene asynchronously by name
    /// </summary>
    /// <param name="sceneName">Name of the scene to load</param>
    public void LoadSceneAsync(string sceneName)
    {
        StartCoroutine(LoadSceneAsyncCoroutine(sceneName));
    }
    
    /// <summary>
    /// Loads a scene asynchronously by build index
    /// </summary>
    /// <param name="sceneIndex">Build index of the scene to load</param>
    public void LoadSceneAsync(int sceneIndex)
    {
        StartCoroutine(LoadSceneAsyncCoroutine(sceneIndex));
    }
    
    private System.Collections.IEnumerator LoadSceneAsyncCoroutine(string sceneName)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }
    
    private System.Collections.IEnumerator LoadSceneAsyncCoroutine(int sceneIndex)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneIndex);
        
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }
}
