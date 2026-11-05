using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Aeternal.Combat;
using Aeternal.Decks;
using Aeternal.Map;
using Aeternal.UI;

/// <summary>
/// Step-by-step tutorial running on top of a live combat. Each step shows one panel.
/// Steps with a gate keep Next locked until the player performs the action the panel asks
/// for (play a card, open the discard pile, end the turn), listened for on the real
/// combat systems. On those steps the slide's own overlays are hidden; with restrictToTarget
/// everything but the action's target is greyed out and blocked. Playing a card or ending
/// the turn is held, not performed: the game is dimmed and locked and Next is highlighted.
/// Pressing Next hides the tutorial, performs the held action so its animation plays out,
/// and only then shows the next step.
/// The Back / progress / Next / Skip bar is built at runtime.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    /// <summary>Action the player must perform before a step's Next button unlocks.</summary>
    public enum Gate { None, PlayCard, OpenAndCloseDiscardPile, EndTurn }

    [Serializable]
    public class Step
    {
        public GameObject panel;
        [Tooltip("Action the player must perform before Next unlocks.")]
        public Gate gate = Gate.None;
        [Tooltip("While the action is pending, only its target (hand, discard pile or End Turn) can be clicked.")]
        public bool restrictToTarget;
    }

    public Step[] steps;
    public string mainMenuSceneName = "MainMenu";

    [Header("Controls")]
    [Tooltip("Position of the control bar, anchored to the top-center of the screen.")]
    public Vector2 controlBarPosition = new Vector2(0f, -12f);
    public Color buttonColor = new Color(0.15f, 0.15f, 0.2f, 0.9f);
    [Tooltip("Next button colour once a step's action is done.")]
    public Color nextHighlightColor = new Color(1f, 0.78f, 0.2f, 1f);
    [Tooltip("Dims and locks the game once a step's action is done.")]
    public Color actionDoneDimColor = new Color(0f, 0f, 0f, 0.45f);
    [Tooltip("Greys out everything but the action's target while it is pending (matches the slide overlays).")]
    public Color spotlightColor = new Color(1f, 1f, 1f, 0.62f);
    [Tooltip("Extra space left clear around the action's target.")]
    public float spotlightPadding = 10f;
    [Tooltip("Width of the highlight frame drawn around the action's target (uses the Next highlight colour).")]
    public float highlightFrameWidth = 4f;
    [Tooltip("Opacity of an open popup (the discard pile list) on an action step. Below 1 lets the battlefield show through.")]
    [Range(0f, 1f)] public float popupOpacity = 0.85f;
    [Tooltip("Pause after a held action's animation has played out, before the next step appears.")]
    public float resultDelay = 1f;
    public KeyCode[] nextKeys = { KeyCode.Space, KeyCode.Return, KeyCode.RightArrow };
    public KeyCode backKey = KeyCode.LeftArrow;

    [Header("Debug")]
    [SerializeField] int currentIndex = 0;

    SceneController sceneController;
    DeckManager deckManager;
    TurnManager turnManager;
    DiscardPileButton discardPileButton;
    CardHandDisplay handDisplay;
    EndTurnButton endTurnButton;

    bool[] gateDone;
    // Per step: the dimming overlays and the text box (the child holding the step's text).
    List<GameObject>[] stepDims;
    GameObject[] stepTextBoxes;
    // Per action step: full-screen click blocker under the text box, and the panel's CanvasGroup.
    GameObject[] stepBlockers;
    CanvasGroup[] stepGroups;
    TutorialClickHole[] stepHoles;
    // Per action step: pooled grey panels covering everything outside the open areas.
    List<RectTransform>[] stepSpotlights;
    // Per action step: pooled bars drawing the highlight frame around each open area.
    List<RectTransform>[] stepFrames;
    RectTransform spotlightTarget;
    // A popup made opaque while it is open on an action step, and its original colour.
    Image boostedPopup;
    Color boostedPopupColor;
    bool discardSeenOpen;
    bool finished;

    // The action the player chose, held until they press Next.
    CardUI pendingCard;
    bool pendingEndTurn;
    // True while a held action plays out with the tutorial hidden.
    bool executing;
    Func<CardUI, bool> cardInterceptor;
    Func<bool> endTurnInterceptor;

    GameObject controlBar;
    GameObject inputBlocker;
    Button backButton;
    Button nextButton;
    TMP_Text nextLabel;
    TMP_Text progressText;
    TMP_Text hintText;

    void Awake()
    {
        currentIndex = 0;
        sceneController = FindFirstObjectByType<SceneController>();
        deckManager = FindFirstObjectByType<DeckManager>();
        turnManager = FindFirstObjectByType<TurnManager>();
        discardPileButton = FindFirstObjectByType<DiscardPileButton>();
        endTurnButton = FindFirstObjectByType<EndTurnButton>();
        foreach (var display in FindObjectsByType<CardHandDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!display.isDiscardPileDisplay) handDisplay = display;
        }
        gateDone = new bool[steps != null ? steps.Length : 0];
        CacheStepParts();
    }

    void CacheStepParts()
    {
        int count = steps != null ? steps.Length : 0;
        stepDims = new List<GameObject>[count];
        stepTextBoxes = new GameObject[count];
        stepBlockers = new GameObject[count];
        stepGroups = new CanvasGroup[count];
        stepHoles = new TutorialClickHole[count];
        stepSpotlights = new List<RectTransform>[count];
        stepFrames = new List<RectTransform>[count];
        for (int i = 0; i < count; i++)
        {
            stepDims[i] = new List<GameObject>();
            var panel = steps[i].panel;
            if (panel == null) continue;

            foreach (Transform child in panel.transform)
            {
                if (child.GetComponentInChildren<TMP_Text>(true) != null) stepTextBoxes[i] = child.gameObject;
                else stepDims[i].Add(child.gameObject);
            }

            if (steps[i].gate == Gate.None) continue;

            stepGroups[i] = panel.GetComponent<CanvasGroup>();
            if (stepGroups[i] == null) stepGroups[i] = panel.AddComponent<CanvasGroup>();
            stepBlockers[i] = MakeBlocker(panel.transform);
            stepHoles[i] = stepBlockers[i].AddComponent<TutorialClickHole>();
            stepSpotlights[i] = new List<RectTransform>();
            stepFrames[i] = new List<RectTransform>();
        }
    }

    RectTransform MakeOverlayRect(Transform blocker, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(blocker, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = Vector2.zero;
        go.GetComponent<Image>().raycastTarget = false;
        return rect;
    }

    /// <summary>The n-th rect of a pool, created on demand, shown and tinted.</summary>
    RectTransform Pooled(List<RectTransform> pool, int index, int n, string name, Color color)
    {
        while (pool.Count <= n) pool.Add(MakeOverlayRect(stepBlockers[index].transform, name));
        var rect = pool[n];
        rect.gameObject.SetActive(true);
        rect.GetComponent<Image>().color = color;
        return rect;
    }

    static void HideFrom(List<RectTransform> pool, int used)
    {
        for (int i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
    }

    GameObject MakeBlocker(Transform panel)
    {
        var go = new GameObject("Action Done Blocker", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(panel, false);
        go.transform.SetAsFirstSibling();
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = actionDoneDimColor;
        go.SetActive(false);
        return go;
    }

    /// <summary>
    /// Before the action the panel lets clicks through to the game, or, when the step
    /// restricts it, greys out and blocks everything but the action's target. After the
    /// action the blocker dims and locks everything except the tutorial controls (and the
    /// target, on steps that keep it highlighted).
    /// </summary>
    void ApplyActionLock(int index)
    {
        spotlightTarget = null;
        RestorePopup();
        if (stepGroups[index] == null) return;
        bool done = gateDone[index];
        var gate = steps[index].gate;
        // Without a target to leave open, restricting would lock the player out of the step.
        bool spotlight = steps[index].restrictToTarget && (!done || KeepsTargetHighlighted(gate));
        // A chosen card that has not been played yet is the one to keep highlighted.
        var target = !spotlight ? null
            : done && gate == Gate.PlayCard && pendingCard != null ? (RectTransform)pendingCard.transform
            : TargetFor(gate);
        bool block = done || target != null;

        stepGroups[index].blocksRaycasts = block;
        stepBlockers[index].SetActive(block);
        // With a spotlight, its panels do the dimming; the blocker itself only catches clicks.
        stepBlockers[index].GetComponent<Image>().color = done && target == null ? actionDoneDimColor : Color.clear;
        stepHoles[index].holes.Clear();
        if (target == null)
        {
            HideFrom(stepSpotlights[index], 0);
            HideFrom(stepFrames[index], 0);
            return;
        }

        spotlightTarget = target;
        UpdateSpotlight(index);
    }

    /// <summary>Whether the target stays highlighted after the action, for the whole step.</summary>
    static bool KeepsTargetHighlighted(Gate gate) => gate == Gate.PlayCard;

    /// <summary>Sets an open popup to popupOpacity so it reads clearly against the spotlight.</summary>
    void BoostPopup(RectTransform popup)
    {
        var image = popup.GetComponent<Image>();
        if (image == null || image == boostedPopup) return;
        RestorePopup();
        boostedPopup = image;
        boostedPopupColor = image.color;
        var boosted = image.color;
        boosted.a = popupOpacity;
        image.color = boosted;
    }

    void RestorePopup()
    {
        if (boostedPopup != null) boostedPopup.color = boostedPopupColor;
        boostedPopup = null;
    }

    /// <summary>
    /// Leaves the target, and any popup it has open (the discard pile list), clear and
    /// highlighted; greys out and blocks everything else. Once the step is done the target
    /// stays highlighted but is locked too, so the game only moves on via Next.
    /// </summary>
    void UpdateSpotlight(int index)
    {
        var space = (RectTransform)stepBlockers[index].transform;
        Rect full = space.rect;
        var gate = steps[index].gate;
        var open = new List<Rect> { Pad(LocalBounds(space, spotlightTarget), full) };
        var popup = OpenPopupFor(gate);
        if (popup != null)
        {
            open.Add(Pad(LocalBounds(space, popup), full));
            BoostPopup(popup);
        }
        else RestorePopup();

        bool done = gateDone[index];
        var holes = stepHoles[index].holes;
        holes.Clear();
        if (!done) holes.AddRange(open);

        DrawSpotlight(index, full, open, done ? actionDoneDimColor : spotlightColor);
        DrawFrames(index, open);
    }

    /// <summary>Draws a highlight frame just outside each open area.</summary>
    void DrawFrames(int index, List<Rect> holes)
    {
        var pool = stepFrames[index];
        float w = highlightFrameWidth;
        int used = 0;
        foreach (var hole in holes)
        {
            SetPanel(Pooled(pool, index, used++, "Highlight Frame", nextHighlightColor), hole.xMin - w, hole.yMax, hole.xMax + w, hole.yMax + w);
            SetPanel(Pooled(pool, index, used++, "Highlight Frame", nextHighlightColor), hole.xMin - w, hole.yMin - w, hole.xMax + w, hole.yMin);
            SetPanel(Pooled(pool, index, used++, "Highlight Frame", nextHighlightColor), hole.xMin - w, hole.yMin, hole.xMin, hole.yMax);
            SetPanel(Pooled(pool, index, used++, "Highlight Frame", nextHighlightColor), hole.xMax, hole.yMin, hole.xMax + w, hole.yMax);
        }
        HideFrom(pool, used);
    }

    /// <summary>
    /// Covers everything outside the holes with grey panels: the screen is cut into a grid
    /// along the hole edges, and each row's consecutive covered cells become one panel.
    /// </summary>
    void DrawSpotlight(int index, Rect full, List<Rect> holes, Color color)
    {
        var pool = stepSpotlights[index];
        var xs = new List<float> { full.xMin, full.xMax };
        var ys = new List<float> { full.yMin, full.yMax };
        foreach (var hole in holes)
        {
            xs.Add(hole.xMin); xs.Add(hole.xMax);
            ys.Add(hole.yMin); ys.Add(hole.yMax);
        }
        xs.Sort();
        ys.Sort();

        int used = 0;
        for (int row = 0; row < ys.Count - 1; row++)
        {
            float y0 = ys[row], y1 = ys[row + 1];
            if (y1 <= y0) continue;

            float runStart = float.NaN;
            for (int col = 0; col < xs.Count - 1; col++)
            {
                float x0 = xs[col], x1 = xs[col + 1];
                if (x1 <= x0) continue;

                var center = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
                bool open = holes.Exists(hole => hole.Contains(center));
                if (!open && float.IsNaN(runStart)) runStart = x0;
                else if (open && !float.IsNaN(runStart))
                {
                    SetPanel(Pooled(pool, index, used++, "Spotlight", color), runStart, y0, x0, y1);
                    runStart = float.NaN;
                }
            }
            if (!float.IsNaN(runStart)) SetPanel(Pooled(pool, index, used++, "Spotlight", color), runStart, y0, full.xMax, y1);
        }

        HideFrom(pool, used);
    }

    Rect Pad(Rect bounds, Rect full)
    {
        return Rect.MinMaxRect(
            Mathf.Max(full.xMin, bounds.xMin - spotlightPadding),
            Mathf.Max(full.yMin, bounds.yMin - spotlightPadding),
            Mathf.Min(full.xMax, bounds.xMax + spotlightPadding),
            Mathf.Min(full.yMax, bounds.yMax + spotlightPadding));
    }

    /// <summary>A popup the gate's target has opened, which must stay visible and usable.</summary>
    RectTransform OpenPopupFor(Gate gate)
    {
        if (gate != Gate.OpenAndCloseDiscardPile || discardPileButton == null) return null;
        var panel = discardPileButton.discardPileDisplayPanel;
        return panel != null && panel.activeInHierarchy ? panel.transform as RectTransform : null;
    }

    static void SetPanel(RectTransform panel, float x0, float y0, float x1, float y1)
    {
        panel.anchoredPosition = new Vector2(x0, y0);
        panel.sizeDelta = new Vector2(Mathf.Max(0f, x1 - x0), Mathf.Max(0f, y1 - y0));
    }

    /// <summary>Bounds of the target and its visible children, in the given rect's local space.</summary>
    static Rect LocalBounds(RectTransform space, RectTransform target)
    {
        var corners = new Vector3[4];
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        foreach (var rect in target.GetComponentsInChildren<RectTransform>())
        {
            rect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector2 local = space.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    /// <summary>The on-screen element the player has to use to complete a gate.</summary>
    RectTransform TargetFor(Gate gate)
    {
        switch (gate)
        {
            case Gate.PlayCard:
                return handDisplay != null ? handDisplay.handParent as RectTransform : null;
            case Gate.OpenAndCloseDiscardPile:
                return discardPileButton != null ? discardPileButton.transform as RectTransform : null;
            case Gate.EndTurn:
                return endTurnButton != null ? endTurnButton.transform as RectTransform : null;
            default:
                return null;
        }
    }

    void OnEnable()
    {
        if (deckManager != null) deckManager.OnCardRemovedFromHand += HandleCardPlayed;
        if (turnManager != null) turnManager.OnEnemyPhaseStart += HandleEnemyPhase;
        cardInterceptor = InterceptCardPlay;
        endTurnInterceptor = InterceptEndTurn;
        CardUI.PlayInterceptor = cardInterceptor;
        EndTurnButton.EndTurnInterceptor = endTurnInterceptor;
    }

    void OnDisable()
    {
        if (deckManager != null) deckManager.OnCardRemovedFromHand -= HandleCardPlayed;
        if (turnManager != null) turnManager.OnEnemyPhaseStart -= HandleEnemyPhase;
        if (CardUI.PlayInterceptor == cardInterceptor) CardUI.PlayInterceptor = null;
        if (EndTurnButton.EndTurnInterceptor == endTurnInterceptor) EndTurnButton.EndTurnInterceptor = null;
        RestorePopup();
    }

    /// <summary>Holds the first playable card clicked on a PlayCard step instead of playing it.</summary>
    bool InterceptCardPlay(CardUI card)
    {
        if (executing || finished || CurrentGate != Gate.PlayCard || gateDone[currentIndex]) return false;
        // Let an unplayable card go through so the game reports why it can't be played.
        if (card.resolver != null && card.playerResources != null &&
            !card.resolver.CanPlay(card.card, card.playerResources)) return false;
        pendingCard = card;
        CompleteGate(Gate.PlayCard);
        return true;
    }

    /// <summary>Holds End Turn on an EndTurn step instead of ending the turn.</summary>
    bool InterceptEndTurn()
    {
        if (executing || finished || CurrentGate != Gate.EndTurn || gateDone[currentIndex]) return false;
        pendingEndTurn = true;
        CompleteGate(Gate.EndTurn);
        return true;
    }

    void Start()
    {
        BuildControls();
        Show(0);
    }

    void Update()
    {
        if (finished) return;

        // Keep keyboard input for the tutorial: a button left selected by a click would
        // otherwise also fire on Space/Enter (e.g. ending the turn a second time).
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (CurrentGate == Gate.OpenAndCloseDiscardPile && !gateDone[currentIndex])
            PollDiscardPile();

        // The hand reflows and popups open and close, so keep the spotlight on its target.
        if (spotlightTarget != null) UpdateSpotlight(currentIndex);

        PulseNext();

        if (Aeternal.UI.PauseMenu.IsPaused) return;
        foreach (var key in nextKeys)
        {
            if (Input.GetKeyDown(key)) { Next(); break; }
        }
        if (Input.GetKeyDown(backKey)) Back();
    }

    Gate CurrentGate => steps != null && currentIndex < steps.Length ? steps[currentIndex].gate : Gate.None;

    bool CurrentUnlocked => CurrentGate == Gate.None || gateDone[currentIndex];

    bool HighlightNext => CurrentGate != Gate.None && gateDone[currentIndex];

    // Only the step that holds an action performs it; stepping Back keeps it for later.
    bool HoldsCard => CurrentGate == Gate.PlayCard && pendingCard != null;
    bool HoldsEndTurn => CurrentGate == Gate.EndTurn && pendingEndTurn;
    bool HasHeldAction => HoldsCard || HoldsEndTurn;

    /// <summary>
    /// Advance to the next step, or finish on the last one. Ignored while the step is gated.
    /// If the player chose an action on this step, it is performed first with the tutorial hidden.
    /// </summary>
    public void Next()
    {
        if (finished || executing || !CurrentUnlocked) return;
        if (HasHeldAction) StartCoroutine(PerformHeldActionThenAdvance());
        else Show(currentIndex + 1);
    }

    /// <summary>Return to the previous step. Gates already completed stay completed.</summary>
    public void Back()
    {
        if (finished || executing || currentIndex == 0) return;
        Show(currentIndex - 1);
    }

    /// <summary>
    /// Hides the tutorial, performs the held action, waits for it to play out (the card to
    /// resolve, or the enemy's turn to finish), then shows the next step.
    /// </summary>
    System.Collections.IEnumerator PerformHeldActionThenAdvance()
    {
        executing = true;
        int from = currentIndex;
        spotlightTarget = null;
        RestorePopup();
        if (steps[from].panel != null) steps[from].panel.SetActive(false);
        controlBar.SetActive(false);
        inputBlocker.SetActive(true);

        if (HoldsCard)
        {
            var card = pendingCard;
            pendingCard = null;
            card.OnClickPlayCard();
            // A played card removes itself; give up waiting if it could not be played.
            float waited = 0f;
            while (card != null && waited < 3f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }
        else if (HoldsEndTurn)
        {
            pendingEndTurn = false;
            if (turnManager != null)
            {
                turnManager.EndPlayerPhase();
                // Enemy phase, round end, then the next round's player phase.
                float waited = 0f;
                while (turnManager.currentState != TurnState.PlayerPhase && waited < 10f)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
            }
        }

        yield return new WaitForSeconds(resultDelay);

        inputBlocker.SetActive(false);
        controlBar.SetActive(true);
        executing = false;
        Show(from + 1);
    }

    /// <summary>
    /// Leave the tutorial (finished or skipped). If it was opened by New Game, the run starts;
    /// otherwise return to the main menu.
    /// </summary>
    public void Finish()
    {
        if (finished) return;
        finished = true;
        RunStarter.TutorialSeen = true;
        if (RunStarter.StartPendingRun()) return;
        if (sceneController != null) sceneController.LoadScene(mainMenuSceneName);
        else SceneManager.LoadScene(mainMenuSceneName);
    }

    void Show(int index)
    {
        if (finished) return;
        if (steps == null || index >= steps.Length)
        {
            Finish();
            return;
        }

        currentIndex = Mathf.Max(0, index);
        for (int i = 0; i < steps.Length; i++)
        {
            if (steps[i].panel != null) steps[i].panel.SetActive(i == currentIndex);
        }

        // Action steps replace the slide's own overlays with the spotlight; the text box stays.
        bool showDims = CurrentGate == Gate.None;
        foreach (var dim in stepDims[currentIndex]) dim.SetActive(showDims);
        if (stepTextBoxes[currentIndex] != null) stepTextBoxes[currentIndex].SetActive(true);
        ApplyActionLock(currentIndex);

        if (CurrentGate == Gate.OpenAndCloseDiscardPile) discardSeenOpen = false;
        RefreshControls();
    }

    void CompleteGate(Gate gate)
    {
        if (finished || CurrentGate != gate || gateDone[currentIndex]) return;
        gateDone[currentIndex] = true;
        // The text stays up; the tutorial only goes away when the player presses Next.
        ApplyActionLock(currentIndex);
        RefreshControls();
    }

    void HandleCardPlayed(Aeternal.Cards.CardData card) => CompleteGate(Gate.PlayCard);

    void HandleEnemyPhase() => CompleteGate(Gate.EndTurn);

    void PollDiscardPile()
    {
        var panel = discardPileButton != null ? discardPileButton.discardPileDisplayPanel : null;
        if (panel == null)
        {
            // Nothing to open in this scene; don't strand the player on this step.
            CompleteGate(Gate.OpenAndCloseDiscardPile);
            return;
        }

        if (panel.activeInHierarchy) discardSeenOpen = true;
        else if (discardSeenOpen) CompleteGate(Gate.OpenAndCloseDiscardPile);
    }

    void RefreshControls()
    {
        if (nextButton == null) return;

        bool last = currentIndex == steps.Length - 1;
        backButton.interactable = currentIndex > 0;
        nextButton.interactable = CurrentUnlocked;
        nextButton.image.color = HighlightNext ? nextHighlightColor : buttonColor;
        nextLabel.color = HighlightNext ? Color.black : Color.white;
        nextLabel.text = last ? "Finish" : "Next >";
        progressText.text = $"{currentIndex + 1} / {steps.Length}";
        if (!CurrentUnlocked) hintText.text = "Do it to continue";
        else if (HoldsCard) hintText.text = "Press Next to play it";
        else if (HoldsEndTurn) hintText.text = "Press Next to end the turn";
        else if (CurrentGate != Gate.None) hintText.text = "Done! Press Next >";
        else hintText.text = "";
    }

    void PulseNext()
    {
        if (nextButton == null) return;
        float scale = HighlightNext ? 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 6f) : 1f;
        nextButton.transform.localScale = new Vector3(scale, scale, 1f);
    }

    // ------------------------------------------------------------------
    // Runtime-built control bar
    // ------------------------------------------------------------------

    void BuildControls()
    {
        var canvasGO = new GameObject("Tutorial Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Invisible, full screen: swallows all clicks while a held action plays out.
        inputBlocker = new GameObject("Input Blocker", typeof(RectTransform), typeof(Image));
        inputBlocker.transform.SetParent(canvasGO.transform, false);
        var blockerRect = (RectTransform)inputBlocker.transform;
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = blockerRect.offsetMax = Vector2.zero;
        inputBlocker.GetComponent<Image>().color = Color.clear;
        inputBlocker.SetActive(false);

        // A single row in the top strip, which the slide panels leave clear.
        var row = new GameObject("Bar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        row.transform.SetParent(canvasGO.transform, false);
        controlBar = row;
        var barRect = (RectTransform)row.transform;
        barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.anchoredPosition = controlBarPosition;
        var fitter = row.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var horizontal = row.GetComponent<HorizontalLayoutGroup>();
        horizontal.childAlignment = TextAnchor.MiddleCenter;
        horizontal.childControlWidth = horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = horizontal.childForceExpandHeight = false;
        horizontal.spacing = 16f;

        MakeButton(row.transform, "Skip Tutorial", 220f, Finish);
        backButton = MakeButton(row.transform, "< Back", 160f, Back);
        progressText = MakeText(row.transform, "", 30f);
        AddLayoutSize(progressText.gameObject, 110f, 56f);
        nextButton = MakeButton(row.transform, "Next >", 160f, Next);
        nextLabel = nextButton.GetComponentInChildren<TMP_Text>();

        hintText = MakeText(row.transform, "", 26f);
        hintText.color = new Color(1f, 0.85f, 0.3f);
        AddLayoutSize(hintText.gameObject, 240f, 56f);
    }

    Button MakeButton(Transform parent, string label, float width, Action onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = buttonColor;
        AddLayoutSize(go, width, 56f);
        var button = go.GetComponent<Button>();
        button.onClick.AddListener(() => onClick());

        var text = MakeText(go.transform, label, 28f);
        var rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return button;
    }

    TMP_Text MakeText(Transform parent, string content, float size)
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

    static void AddLayoutSize(GameObject go, float width, float height)
    {
        var layout = go.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = height;
    }
}
