using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Aeternal.Core;
using Aeternal.Map;

namespace Aeternal.UI
{
    /// <summary>
    /// Esc pauses the game: time stops, sound pauses, and a menu shows where the run stands
    /// (region, floor, health, gold, deck) with Resume, Save Game, Settings, Controls,
    /// Save &amp; Main Menu, Give Up and Save &amp; Quit. Esc goes back from a sub-page, or resumes.
    /// The run is saved on the map and as each fight begins; leaving at a point that isn't
    /// saved (mid-fight, or with a merchant or event open) asks first. Give Up ends the run
    /// and deletes its save. Attaches itself to every scene except the main menu (the scene
    /// with a RunStarter); needs no scene wiring. Other scripts that read keys directly check
    /// <see cref="IsPaused"/> so nothing happens behind the menu.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        const float ButtonHeight = 66f;
        const float ToastSeconds = 2.4f;

        static readonly Color Gold = new Color(1f, 0.8f, 0.35f, 1f);
        static readonly Color ButtonColor = new Color(0.1f, 0.09f, 0.11f, 0.92f);
        static readonly Color PrimaryColor = new Color(0.55f, 0.36f, 0.12f, 0.95f);
        static readonly Color DangerColor = new Color(0.45f, 0.1f, 0.09f, 0.95f);

        static readonly (string key, string action)[] ControlsList =
        {
            ("Esc", "Pause / go back"),
            ("Left click", "Play a card, pick a map node"),
            ("Hover a card", "Enlarge it"),
            ("Z", "Shift to another deck (in battle)"),
            ("Space", "Shift page: upgrade Shift effects (on the map)"),
            ("X", "Guard: spend AP for a small shield (in battle)"),
            ("Click / Space / Enter", "Continue story text"),
        };

        /// <summary>True while the pause menu is open.</summary>
        public static bool IsPaused { get; private set; }

        GameObject panel;
        TMP_Text heading;
        TMP_Text summary;
        TMP_Text toast;
        GameObject mainPage, settingsPage, controlsPage, confirmPage;
        readonly List<GameObject> pages = new List<GameObject>();
        GameObject saveButton, giveUpButton;
        TMP_Text mainMenuText, quitText;
        TMP_Text volumeLabel, fullscreenLabel;
        TMP_Text confirmText, confirmYesText;
        Action onConfirm;
        Coroutine toastRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachIfPlayable();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachIfPlayable();

        static void AttachIfPlayable()
        {
            if (FindFirstObjectByType<RunStarter>() != null || FindFirstObjectByType<PauseMenu>() != null) return;
            new GameObject("Pause Menu").AddComponent<PauseMenu>();
        }

        void Start()
        {
            Build();
            panel.SetActive(false);
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (IsPaused)
            {
                // Esc backs out of a sub-page first, like most games.
                if (!mainPage.activeSelf) ShowPage(mainPage);
                else SetPaused(false);
                return;
            }
            // An open deck view or Shift page closes on Esc itself; don't also pause.
            if (FindFirstObjectByType<CardGridPanel>() != null || FindFirstObjectByType<ShiftPanel>() != null) return;
            SetPaused(true);
        }

        void OnDestroy()
        {
            // Leaving the scene while paused must not leave the next scene frozen.
            if (IsPaused) Unfreeze();
        }

        // ------------------------------------------------------------------
        // Pausing
        // ------------------------------------------------------------------

        void SetPaused(bool paused)
        {
            if (paused)
            {
                IsPaused = true;
                Time.timeScale = 0f;
                AudioListener.pause = true;
                Refresh();
                ShowPage(mainPage);
            }
            else
            {
                Unfreeze();
            }
            panel.SetActive(paused);
            if (!paused) toast.text = "";
        }

        static void Unfreeze()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        void ShowPage(GameObject page)
        {
            foreach (var p in pages) p.SetActive(p == page);
            heading.text = page == settingsPage ? "SETTINGS" : page == controlsPage ? "CONTROLS" : "PAUSED";
            summary.gameObject.SetActive(page == mainPage);
        }

        /// <summary>Updates the run summary and which buttons apply (no run: no save or give up).</summary>
        void Refresh()
        {
            var run = ActiveRun;
            saveButton.SetActive(run != null);
            giveUpButton.SetActive(run != null);
            mainMenuText.text = run != null ? "Save & Main Menu" : "Main Menu";
            quitText.text = run != null ? "Save & Quit to Desktop" : "Quit to Desktop";
            summary.text = RunSummary(run);
        }

        static RunManager ActiveRun
        {
            get
            {
                var run = RunManager.Instance;
                return run != null && run.HasActiveRun ? run : null;
            }
        }

        static bool InFight(RunManager run) => run != null && SceneManager.GetActiveScene().name == run.combatSceneName;

        static string RunSummary(RunManager run)
        {
            if (run == null) return "";
            var parts = new List<string>();
            var map = run.CurrentMap;
            if (!string.IsNullOrEmpty(map.regionName)) parts.Add(map.regionName);
            var node = run.Map != null ? run.Map.Get(run.CurrentMapNodeId) : null;
            if (node != null) parts.Add($"Floor {node.floor + 1}/{run.Map.FloorCount}");
            int hp = run.CarriedHealth;
            string health = hp < 0 ? "full" : run.PlayerMaxHealth > 0 ? $"{hp}/{run.PlayerMaxHealth}" : hp.ToString();
            if (!InFight(run)) parts.Add($"HP {health}");
            parts.Add($"{run.Gold} Gold");
            parts.Add($"{run.Aether} Aether");
            parts.Add($"Deck {run.RunDeck.Count}");
            string line = string.Join("   ·   ", parts);
            if (InFight(run) && run.CurrentEncounter != null)
                line += $"\n<size=85%><color=#DDDDDD>In battle: {run.CurrentEncounter.displayName}</color></size>";
            return line;
        }

        // ------------------------------------------------------------------
        // Saving and leaving
        // ------------------------------------------------------------------

        /// <summary>
        /// Why leaving now would lose something, or null if the latest progress is saved. A
        /// fight is saved as it begins; a node on the map is saved once it is done.
        /// </summary>
        static string UnsavedProgress(RunManager run)
        {
            if (run == null) return null;
            if (InFight(run)) return "This fight isn't saved.\nOn Continue it starts over from the beginning.";
            var mapView = FindFirstObjectByType<RegionMapView>();
            if (mapView != null && mapView.Busy) return "What's open on the map isn't finished.\nOn Continue you'll be back before it.";
            return null;
        }

        void SaveGame()
        {
            var run = ActiveRun;
            if (run == null) return;
            if (UnsavedProgress(run) != null)
            {
                // Saving the middle of a fight or a node isn't supported: the checkpoint stands.
                Toast(InFight(run) ? "Saved at the start of this fight" : "Saved at your last step on the map");
                return;
            }
            run.Save();
            Toast("Game saved");
        }

        void Toast(string message)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ShowToast(message));
        }

        IEnumerator ShowToast(string message)
        {
            toast.text = message;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / ToastSeconds)
            {
                toast.alpha = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f;
                yield return null;
            }
            toast.text = "";
            toastRoutine = null;
        }

        /// <summary>Leaves now, or first asks when that would lose unsaved progress.</summary>
        void Leave(string question, string yesLabel, Action leave)
        {
            string unsaved = UnsavedProgress(ActiveRun);
            if (unsaved == null)
            {
                leave();
                return;
            }
            Confirm($"{question}\n\n<size=80%><color=#DDDDDD>{unsaved}</color></size>", yesLabel, leave);
        }

        void ToMainMenu()
        {
            var run = RunManager.Instance;
            SetPaused(false);
            // The save stays on disk for Continue; the menu starts with no run in memory.
            if (run != null) run.UnloadRun();
            SceneManager.LoadScene(run != null ? run.mainMenuSceneName : "MainMenu");
        }

        void GiveUp()
        {
            Confirm("Give up this run?\n\n<size=80%><color=#DDDDDD>The run ends and its save is deleted.\nThis can't be undone.</color></size>",
                "Give Up", () =>
                {
                    var run = RunManager.Instance;
                    if (run != null) run.ResetRun();
                    ToMainMenu();
                });
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Confirm(string question, string yesLabel, Action yes)
        {
            confirmText.text = question;
            confirmYesText.text = yesLabel;
            onConfirm = yes;
            ShowPage(confirmPage);
            heading.text = "ARE YOU SURE?";
        }

        // ------------------------------------------------------------------
        // Building blocks
        // ------------------------------------------------------------------

        void Build()
        {
            var canvasGo = new GameObject("Pause Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600; // above every other panel, the card hover preview included
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // The dim layer catches every click, so nothing behind the menu can be pressed.
            panel = new GameObject("Paused", typeof(RectTransform), typeof(Image));
            var dimRect = (RectTransform)panel.transform;
            dimRect.SetParent(canvasGo.transform, false);
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = dimRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(Outline));
            var boxRect = (RectTransform)box.transform;
            boxRect.SetParent(dimRect, false);
            Place(boxRect, new Vector2(0f, 0f), new Vector2(760f, 920f));
            box.GetComponent<Image>().color = new Color(0.06f, 0.05f, 0.07f, 0.97f);
            var edge = box.GetComponent<Outline>();
            edge.effectColor = Gold;
            edge.effectDistance = new Vector2(2f, -2f);

            heading = MakeText(dimRect, "PAUSED", 56f);
            heading.fontStyle = FontStyles.Bold;
            heading.color = Gold;
            Place(heading.rectTransform, new Vector2(0f, 395f), new Vector2(700f, 80f));

            summary = MakeText(dimRect, "", 26f);
            summary.color = new Color(1f, 1f, 1f, 0.85f);
            Place(summary.rectTransform, new Vector2(0f, 320f), new Vector2(720f, 70f));

            // Main page.
            mainPage = Page(dimRect, new Vector2(0f, -30f));
            MakeButton(mainPage.transform, "Resume", () => SetPaused(false), PrimaryColor);
            saveButton = MakeButton(mainPage.transform, "Save Game", SaveGame).gameObject;
            MakeButton(mainPage.transform, "Settings", () => { RefreshSettings(); ShowPage(settingsPage); });
            MakeButton(mainPage.transform, "Controls", () => ShowPage(controlsPage));
            mainMenuText = Label(MakeButton(mainPage.transform, "Main Menu", () => Leave("Go to the main menu?", "Main Menu", ToMainMenu)));
            quitText = Label(MakeButton(mainPage.transform, "Quit to Desktop", () => Leave("Quit the game?", "Quit", Quit)));
            giveUpButton = MakeButton(mainPage.transform, "Give Up", GiveUp, DangerColor).gameObject;

            // Settings.
            settingsPage = Page(dimRect, new Vector2(0f, 0f));
            volumeLabel = Stepper(settingsPage.transform,
                () => { GameSettings.ChangeVolume(-GameSettings.VolumeStep); RefreshSettings(); },
                () => { GameSettings.ChangeVolume(GameSettings.VolumeStep); RefreshSettings(); });
            fullscreenLabel = Label(MakeButton(settingsPage.transform, "", () =>
                fullscreenLabel.text = GameSettings.FullscreenLabel(GameSettings.ToggleFullscreen())));
            MakeButton(settingsPage.transform, "Back", () => ShowPage(mainPage));

            // Controls.
            controlsPage = Page(dimRect, new Vector2(0f, 0f));
            foreach (var (key, action) in ControlsList)
            {
                var row = MakeText(controlsPage.transform, $"<color=#{ColorUtility.ToHtmlStringRGB(Gold)}><b>{key}</b></color>   {action}", 30f);
                AddSize(row.gameObject, 640f, 52f);
            }
            MakeButton(controlsPage.transform, "Back", () => ShowPage(mainPage));

            // A yes/no question, for leaving with unsaved progress and for giving up.
            confirmPage = Page(dimRect, new Vector2(0f, 0f));
            confirmText = MakeText(confirmPage.transform, "", 34f);
            AddSize(confirmText.gameObject, 660f, 260f);
            confirmYesText = Label(MakeButton(confirmPage.transform, "Yes", () => onConfirm?.Invoke(), DangerColor));
            MakeButton(confirmPage.transform, "Cancel", () => ShowPage(mainPage), PrimaryColor);

            toast = MakeText(dimRect, "", 28f);
            toast.color = Gold;
            Place(toast.rectTransform, new Vector2(0f, -415f), new Vector2(700f, 50f));

            var hint = MakeText(dimRect, "Esc: back / resume", 24f);
            hint.color = new Color(1f, 1f, 1f, 0.55f);
            Place(hint.rectTransform, new Vector2(0f, -495f), new Vector2(600f, 40f));
        }

        void RefreshSettings()
        {
            volumeLabel.text = GameSettings.VolumeLabel;
            fullscreenLabel.text = GameSettings.FullscreenLabel(Screen.fullScreen);
        }

        GameObject Page(Transform parent, Vector2 position)
        {
            var go = new GameObject("Page", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.spacing = 12f;
            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            pages.Add(go);
            return go;
        }

        static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static TMP_Text Label(Button button) => button.GetComponentInChildren<TMP_Text>();

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
            AddSize(label.gameObject, 296f, ButtonHeight);
            MakeButton(row.transform, "+", up, width: 76f);
            return label;
        }

        static Button MakeButton(Transform parent, string label, Action onClick, Color? color = null, float width = 480f)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            go.transform.SetParent(parent, false);
            bool plain = color == null;
            go.GetComponent<Image>().color = color ?? ButtonColor;
            var edge = go.GetComponent<Outline>();
            edge.effectColor = new Color(Gold.r, Gold.g, Gold.b, plain ? 0.45f : 0.9f);
            edge.effectDistance = new Vector2(2f, -2f);
            AddSize(go, width, ButtonHeight);

            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(0.65f, 0.62f, 0.58f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            go.AddComponent<HoverScale>();

            var text = MakeText(go.transform, label, 34f);
            if (!plain) text.fontStyle = FontStyles.Bold;
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
