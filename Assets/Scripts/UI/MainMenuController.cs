using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Aeternal.Map;

namespace Aeternal.UI
{
    /// <summary>
    /// The title-screen menu: New Game, Continue (the saved run; disabled when there is none),
    /// Settings, Credits and Quit, under the scene's title. Drives the scene's RunStarter.
    /// Attaches itself to any scene with a RunStarter; needs no scene wiring. Settings are saved on this machine.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        const string KeptTitleName = "Title";
        const float TitleFloat = 8f;        // canvas units
        const float EntryStagger = 0.08f;   // seconds between menu entries fading in
        const float EntryFade = 0.35f;

        static readonly Color Gold = new Color(1f, 0.8f, 0.35f, 1f);
        static readonly Color ButtonColor = new Color(0.1f, 0.09f, 0.11f, 0.92f);
        static readonly Color PrimaryColor = new Color(0.55f, 0.36f, 0.12f, 0.95f);

        static readonly (string name, string role)[] CreditsList =
        {
            ("Sedat Unal", "Game Implementer"),
            ("Qibo Wang", "Game Designer"),
            ("Zack Huang", "Art Creator"),
        };

        RunStarter runStarter;
        RectTransform title;
        Vector2 titleRest;
        GameObject menu;
        GameObject settingsPanel;
        GameObject creditsPanel;
        GameObject tutorialPanel;
        TMP_Text volumeLabel;
        TMP_Text fullscreenLabel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachIfMenu();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachIfMenu();

        static void AttachIfMenu()
        {
            var starter = FindFirstObjectByType<RunStarter>();
            if (starter == null || FindFirstObjectByType<MainMenuController>() != null) return;
            var controller = new GameObject("Main Menu").AddComponent<MainMenuController>();
            controller.runStarter = starter;
        }

        void Start()
        {
            BuildCanvas();
            var titleGo = GameObject.Find(KeptTitleName);
            if (titleGo != null)
            {
                title = titleGo.transform as RectTransform;
                if (title != null) titleRest = title.anchoredPosition;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) ShowMenu();
            // The title drifts gently, like it's hanging in the void.
            if (title != null)
                title.anchoredPosition = titleRest + new Vector2(0f, Mathf.Sin(Time.unscaledTime * 0.9f) * TitleFloat);
        }

        /// <summary>Fades a column's entries in one after another.</summary>
        IEnumerator FadeInEntries(GameObject column)
        {
            var groups = new List<CanvasGroup>();
            foreach (Transform child in column.transform)
            {
                var group = child.GetComponent<CanvasGroup>();
                if (group == null) group = child.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                groups.Add(group);
            }
            for (int i = 0; i < groups.Count; i++)
            {
                StartCoroutine(Fade(groups[i]));
                yield return new WaitForSecondsRealtime(EntryStagger);
            }
        }

        static IEnumerator Fade(CanvasGroup group)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / EntryFade)
            {
                if (group == null) yield break;
                group.alpha = t;
                yield return null;
            }
            if (group != null) group.alpha = 1f;
        }

        void BuildCanvas()
        {
            var canvasGo = new GameObject("Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            // Main column, under the title.
            menu = Column("Menu", root, new Vector2(0f, -60f), 18f);
            MakeButton(menu.transform, "New Game", NewGame, primary: true);
            var continueButton = MakeButton(menu.transform, "Continue", null);
            continueButton.interactable = RunStarter.CanContinue && runStarter != null;
            continueButton.onClick.AddListener(() =>
            {
                if (!runStarter.ContinueRun()) continueButton.interactable = false;
            });
            MakeButton(menu.transform, "Settings", () => ShowPanel(settingsPanel));
            MakeButton(menu.transform, "Credits", () => ShowPanel(creditsPanel));
            MakeButton(menu.transform, "Quit", Quit);

            settingsPanel = Modal("Settings", root, "SETTINGS");
            var settings = Column("Rows", settingsPanel.transform, new Vector2(0f, -30f), 14f);
            volumeLabel = Stepper(settings.transform, () => ChangeVolume(-GameSettings.VolumeStep), () => ChangeVolume(GameSettings.VolumeStep));
            fullscreenLabel = MakeButton(settings.transform, "", ToggleFullscreen).GetComponentInChildren<TMP_Text>();
            MakeButton(settings.transform, "Back", ShowMenu);
            RefreshSettings();

            creditsPanel = Modal("Credits", root, "CREDITS");
            var credits = Column("Rows", creditsPanel.transform, new Vector2(0f, -20f), 22f);
            foreach (var (person, role) in CreditsList)
            {
                var line = MakeText(credits.transform, $"<b>{person}</b>\n<size=70%><color=#{ColorUtility.ToHtmlStringRGB(Gold)}>{role}</color></size>", 38f);
                AddSize(line.gameObject, 700f, 96f);
            }
            MakeButton(credits.transform, "Back", ShowMenu);

            // New Game: where to start. Nothing is forced; each choice says what it skips.
            tutorialPanel = Modal("New Game", root, "NEW GAME");
            var prompt = Column("Rows", tutorialPanel.transform, new Vector2(0f, -40f), 14f);
            MakeButton(prompt.transform, "Play Tutorial\n<size=55%><color=#DDDDDD>a guided fight, then the prologue</color></size>",
                () => StartAt(RunStarter.StartPoint.Tutorial), primary: true, height: 96f);
            MakeButton(prompt.transform, "Prologue\n<size=55%><color=#DDDDDD>the ruler's fall: the story from the start</color></size>",
                () => StartAt(RunStarter.StartPoint.Prologue), height: 96f);
            MakeButton(prompt.transform, "Skip Prologue\n<size=55%><color=#DDDDDD>begin in Zeusonia</color></size>",
                () => StartAt(RunStarter.StartPoint.FirstRegion), height: 96f);
            MakeButton(prompt.transform, "Back", ShowMenu);

            menu.SetActive(false); // so ShowMenu fades the entries in
            ShowMenu();
        }

        void NewGame()
        {
            if (runStarter != null) ShowPanel(tutorialPanel);
        }

        void StartAt(RunStarter.StartPoint start)
        {
            if (start == RunStarter.StartPoint.Tutorial) RunStarter.TutorialSeen = true;
            runStarter.StartRun(start);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void ShowMenu()
        {
            if (menu == null) return;
            bool appearing = !menu.activeSelf;
            menu.SetActive(true);
            settingsPanel.SetActive(false);
            creditsPanel.SetActive(false);
            tutorialPanel.SetActive(false);
            if (appearing) StartCoroutine(FadeInEntries(menu));
        }

        void ShowPanel(GameObject panel)
        {
            menu.SetActive(false);
            settingsPanel.SetActive(panel == settingsPanel);
            creditsPanel.SetActive(panel == creditsPanel);
            tutorialPanel.SetActive(panel == tutorialPanel);
            foreach (Transform child in panel.transform)
            {
                if (child.name == "Rows") StartCoroutine(FadeInEntries(child.gameObject));
            }
        }

        void ChangeVolume(float delta)
        {
            GameSettings.ChangeVolume(delta);
            RefreshSettings();
        }

        void ToggleFullscreen()
        {
            fullscreenLabel.text = GameSettings.FullscreenLabel(GameSettings.ToggleFullscreen());
        }

        void RefreshSettings()
        {
            volumeLabel.text = GameSettings.VolumeLabel;
            fullscreenLabel.text = GameSettings.FullscreenLabel(Screen.fullScreen);
        }

        // ------------------------------------------------------------------
        // Building blocks
        // ------------------------------------------------------------------

        static GameObject Column(string name, Transform parent, Vector2 position, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.spacing = spacing;
            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        /// <summary>A full-screen dimmed layer with a framed box and a title.</summary>
        static GameObject Modal(string name, Transform parent, string title)
        {
            var dim = new GameObject(name, typeof(RectTransform), typeof(Image));
            var dimRect = (RectTransform)dim.transform;
            dimRect.SetParent(parent, false);
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = dimRect.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(Outline));
            var boxRect = (RectTransform)box.transform;
            boxRect.SetParent(dimRect, false);
            boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(900f, 640f);
            box.GetComponent<Image>().color = new Color(0.06f, 0.05f, 0.07f, 0.97f);
            var edge = box.GetComponent<Outline>();
            edge.effectColor = Gold;
            edge.effectDistance = new Vector2(2f, -2f);

            var heading = MakeText(dimRect, title, 56f);
            heading.fontStyle = FontStyles.Bold;
            heading.color = Gold;
            var headingRect = heading.rectTransform;
            headingRect.anchorMin = headingRect.anchorMax = headingRect.pivot = new Vector2(0.5f, 0.5f);
            headingRect.anchoredPosition = new Vector2(0f, 250f);
            headingRect.sizeDelta = new Vector2(800f, 80f);
            return dim;
        }

        /// <summary>"[-]  label  [+]" row; returns the label.</summary>
        static TMP_Text Stepper(Transform parent, Action down, Action up)
        {
            var row = new GameObject("Stepper", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.spacing = 12f;
            MakeButton(row.transform, "-", down, width: 76f);
            var label = MakeText(row.transform, "", 34f);
            AddSize(label.gameObject, 296f, 76f);
            MakeButton(row.transform, "+", up, width: 76f);
            return label;
        }

        static Button MakeButton(Transform parent, string label, Action onClick, bool primary = false, float width = 480f,
            float height = 76f)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = primary ? PrimaryColor : ButtonColor;
            var edge = go.GetComponent<Outline>();
            edge.effectColor = new Color(Gold.r, Gold.g, Gold.b, primary ? 0.9f : 0.45f);
            edge.effectDistance = new Vector2(2f, -2f);
            AddSize(go, width, height);

            var button = go.GetComponent<Button>();
            // Tints multiply the image colour (and clamp at 1), so rest slightly dark and
            // light up on hover.
            var colors = button.colors;
            colors.normalColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(0.65f, 0.62f, 0.58f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.45f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            go.AddComponent<HoverScale>();

            var text = MakeText(go.transform, label, primary ? 40f : 36f);
            if (primary) text.fontStyle = FontStyles.Bold;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return button;
        }

        static TMP_Text MakeText(Transform parent, string content, float size)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        static void AddSize(GameObject go, float width, float height)
        {
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
        }
    }
}
