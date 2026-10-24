using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Aeternal.FX
{
    /// <summary>
    /// Fades every newly loaded scene in from black, so scene changes (menu, tutorial,
    /// fight to fight) don't cut abruptly. Lives for the whole session; needs no wiring.
    /// </summary>
    public class SceneFader : MonoBehaviour
    {
        const float FadeDuration = 0.45f;

        static SceneFader instance;
        Image cover;
        Coroutine fade;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("Scene Fader", typeof(RectTransform), typeof(Canvas));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            instance = go.AddComponent<SceneFader>();

            var coverGo = new GameObject("Cover", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)coverGo.transform;
            rect.SetParent(go.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            instance.cover = coverGo.GetComponent<Image>();
            instance.cover.color = Color.black;
            instance.cover.raycastTarget = false; // never blocks input, even mid-fade

            SceneManager.sceneLoaded += (scene, mode) => instance.FadeIn();
            instance.FadeIn();
        }

        void FadeIn()
        {
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeInRoutine());
        }

        IEnumerator FadeInRoutine()
        {
            cover.enabled = true;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / FadeDuration)
            {
                cover.color = new Color(0f, 0f, 0f, 1f - t * t);
                yield return null;
            }
            cover.enabled = false;
            fade = null;
        }
    }
}
