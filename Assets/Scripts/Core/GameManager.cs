using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aeternal.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void LoadScene(string name)
        {
            SceneManager.LoadScene(name);
        }

        public void QuitGame()
        {
            Application.Quit();
        }
    }
}
