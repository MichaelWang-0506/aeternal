using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using TMPro;
using Aeternal.Core;
using Aeternal.Cards;
using Aeternal.Decks;
using Aeternal.Stats;
using Aeternal.Encounters;
using Aeternal.UI;
using Aeternal.Map;

namespace Aeternal.EditorTools
{
    /// <summary>
    /// One-shot editor automation that stands up the Stage 2 run loop:
    ///  - creates EncounterData + MapData assets (with correct GUIDs),
    ///  - wires PlayGameScene with a self-contained end-combat UI + CombatEndController,
    ///  - adds an "Enter Next Fight" node to MapScene,
    ///  - adds a "New Run" button + RunStarter to MainMenu.
    /// Idempotent: re-running rebuilds the generated objects/assets in place.
    /// Run from the menu (Aeternal/Stage 2/Run Full Setup) or headless via
    /// -executeMethod Aeternal.EditorTools.AeternalStage2Setup.RunAll
    /// </summary>
    public static class AeternalStage2Setup
    {
        const string SO_ROOT = "Assets/Scriptable Objects";
        const string ENC_DIR = SO_ROOT + "/Encounters";
        const string MAP_DIR = SO_ROOT + "/Maps";
        const string CARDS_DIR = SO_ROOT + "/Cards/First Deck/First Deck Cards";
        const string DECK_PATH = SO_ROOT + "/Cards/First Deck/First Deck.asset";
        const string MAP_PATH = MAP_DIR + "/Steampunk City.asset";

        const string PLAY_SCENE = "Assets/Scenes/PlayGameScene.unity";
        const string MAP_SCENE = "Assets/Scenes/MapScene.unity";
        const string MENU_SCENE = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Aeternal/Stage 2/Run Full Setup")]
        public static void RunAll()
        {
            try
            {
                Debug.Log("[Stage2Setup] === START ===");
                CreateEncountersAndMap();
                SetupPlayGameScene();
                SetupMapScene();
                SetupMainMenu();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Stage2Setup] === DONE (success) ===");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Stage2Setup] FAILED: {e}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------
        // Assets
        // ------------------------------------------------------------------
        static void CreateEncountersAndMap()
        {
            EnsureFolder(SO_ROOT, "Encounters");
            EnsureFolder(SO_ROOT, "Maps");

            var goblinStats = Load<PlayerStats>(SO_ROOT + "/Goblin.asset");
            var thanatosStats = Load<PlayerStats>(SO_ROOT + "/Thanatos.asset");

            var goblin = CreateOrLoad<EncounterData>(ENC_DIR + "/Goblin Encounter.asset");
            goblin.encounterId = "goblin";
            goblin.displayName = "Goblin";
            goblin.isBoss = false;
            goblin.stats = goblinStats;
            goblin.baseDamage = 3;
            goblin.damageType = DamageType.Physical;
            goblin.canAOE = false;
            goblin.turnsBetweenAttacks = 1;
            goblin.sprite = Load<Sprite>("Assets/Images/Characters/goblin.png");
            EditorUtility.SetDirty(goblin);

            var thanatos = CreateOrLoad<EncounterData>(ENC_DIR + "/Thanatos Encounter.asset");
            thanatos.encounterId = "thanatos";
            thanatos.displayName = "Thanatos";
            thanatos.isBoss = true;
            thanatos.stats = thanatosStats;
            thanatos.baseDamage = 6;
            thanatos.damageType = DamageType.Physical;
            thanatos.canAOE = true;
            thanatos.turnsBetweenAttacks = 1;
            thanatos.sprite = Load<Sprite>("Assets/Images/Characters/thanatos.png");
            EditorUtility.SetDirty(thanatos);

            var map = CreateOrLoad<MapData>(MAP_PATH);
            map.regionName = "Steampunk City";
            map.startingDeck = Load<DeckData>(DECK_PATH);
            map.encounters = new List<EncounterData> { goblin, goblin, goblin, thanatos };
            map.cardRewardPool = new List<CardData>
            {
                Load<CardData>(CARDS_DIR + "/Smash.asset"),
                Load<CardData>(CARDS_DIR + "/Disembowel.asset"),
                Load<CardData>(CARDS_DIR + "/Steam Mist.asset"),
                Load<CardData>(CARDS_DIR + "/Blood Reaper.asset"),
                Load<CardData>(CARDS_DIR + "/Sacrifice.asset"),
                Load<CardData>(CARDS_DIR + "/SkullSmash.asset"),
            };
            map.onRegionClearedScene = "MainMenu";
            EditorUtility.SetDirty(map);

            AssetDatabase.SaveAssets();
            Debug.Log("[Stage2Setup] Encounters + Map created/updated.");
        }

        // ------------------------------------------------------------------
        // PlayGameScene: end-of-combat UI
        // ------------------------------------------------------------------
        static void SetupPlayGameScene()
        {
            var scene = EditorSceneManager.OpenScene(PLAY_SCENE, OpenSceneMode.Single);
            EnsureEventSystem();
            DestroyIfExists("EndCombatCanvas");
            DestroyIfExists("CombatEndController");

            var canvas = CreateOverlayCanvas("EndCombatCanvas", 100);

            // Victory panel
            var victory = CreateFullScreenPanel("VictoryPanel", canvas.transform, new Color(0f, 0f, 0f, 0.85f));
            CreateText("Title", victory.transform, "VICTORY", new Vector2(0, 220), new Vector2(900, 120), 90, TextAlignmentOptions.Center);
            var vContinue = CreateButton("ContinueButton", victory.transform, "Continue", new Vector2(0, -120), new Vector2(320, 90), out _);

            // Defeat panel
            var defeat = CreateFullScreenPanel("DefeatPanel", canvas.transform, new Color(0.1f, 0f, 0f, 0.9f));
            CreateText("Title", defeat.transform, "DEFEAT", new Vector2(0, 220), new Vector2(900, 120), 90, TextAlignmentOptions.Center);
            var dContinue = CreateButton("ReturnButton", defeat.transform, "Return to Menu", new Vector2(0, -120), new Vector2(360, 90), out _);

            // Reward panel
            var reward = CreateFullScreenPanel("RewardPanel", canvas.transform, new Color(0f, 0f, 0f, 0.85f));
            CreateText("Title", reward.transform, "Choose a Reward", new Vector2(0, 320), new Vector2(1000, 120), 70, TextAlignmentOptions.Center);
            var rButtons = new Button[3];
            var rLabels = new TextMeshProUGUI[3];
            float[] xs = { -420, 0, 420 };
            for (int i = 0; i < 3; i++)
            {
                rButtons[i] = CreateButton($"RewardCard{i}", reward.transform, "Card", new Vector2(xs[i], 0), new Vector2(360, 320), out rLabels[i]);
                rLabels[i].fontSize = 30;
                rLabels[i].enableWordWrapping = true;
            }
            var skip = CreateButton("SkipButton", reward.transform, "Skip", new Vector2(0, -360), new Vector2(260, 80), out _);

            // Controller
            var ctrlGO = new GameObject("CombatEndController");
            var ctrl = ctrlGO.AddComponent<CombatEndController>();
            ctrl.victoryPanel = victory;
            ctrl.defeatPanel = defeat;
            ctrl.rewardPanel = reward;
            ctrl.rewardButtons = rButtons;
            ctrl.rewardLabels = rLabels;
            ctrl.skipRewardButton = skip;
            ctrl.victoryContinueButton = vContinue;
            ctrl.defeatContinueButton = dContinue;
            ctrl.fallbackSceneName = "MainMenu";

            victory.SetActive(false);
            defeat.SetActive(false);
            reward.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Stage2Setup] PlayGameScene wired with end-combat UI.");
        }

        // ------------------------------------------------------------------
        // MapScene: entry node
        // ------------------------------------------------------------------
        static void SetupMapScene()
        {
            var scene = EditorSceneManager.OpenScene(MAP_SCENE, OpenSceneMode.Single);
            EnsureEventSystem();
            DestroyIfExists("RunMapCanvas");

            // Fallback UI button (always works even if sprite nodes can't be found).
            var canvas = CreateOverlayCanvas("RunMapCanvas", 50);
            var btn = CreateButton("EnterFightButton", canvas.transform, "Enter Next Fight", new Vector2(0, -420), new Vector2(420, 84), out _);
            var node = btn.gameObject.AddComponent<MapNodeButton>();
            node.useNextEncounter = true;

            // Wire the actual world-space enemy sprites (3 Guards + Boss) as clickable nodes.
            var guards = FindAllByExactName("Guard");
            guards.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            for (int i = 0; i < guards.Count; i++)
                SetupSpriteNode(guards[i], i);

            var boss = GameObject.Find("Boss");
            if (boss != null) SetupSpriteNode(boss, guards.Count);
            else Debug.LogWarning("[Stage2Setup] 'Boss' sprite not found in MapScene.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Stage2Setup] MapScene wired: {guards.Count} guard node(s) + boss + fallback button.");
        }

        static void SetupSpriteNode(GameObject go, int index)
        {
            if (go.GetComponent<Collider2D>() == null)
            {
                var box = go.AddComponent<BoxCollider2D>();
                var sr = go.GetComponentInChildren<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    box.size = sr.sprite.bounds.size;
                    box.offset = Vector2.zero;
                }
            }
            var mn = go.GetComponent<MapNode2D>();
            if (mn == null) mn = go.AddComponent<MapNode2D>();
            mn.encounterIndex = index;
        }

        static List<GameObject> FindAllByExactName(string name)
        {
            var result = new List<GameObject>();
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) result.Add(t.gameObject);
            return result;
        }

        // ------------------------------------------------------------------
        // MainMenu: New Run button
        // ------------------------------------------------------------------
        static void SetupMainMenu()
        {
            var scene = EditorSceneManager.OpenScene(MENU_SCENE, OpenSceneMode.Single);
            EnsureEventSystem();
            DestroyIfExists("RunStarterCanvas");
            DestroyIfExists("RunStarter");

            var canvas = CreateOverlayCanvas("RunStarterCanvas", 50);
            var btn = CreateButton("NewRunButton", canvas.transform, "New Run", new Vector2(0, -300), new Vector2(360, 90), out _);

            var rsGO = new GameObject("RunStarter");
            var rs = rsGO.AddComponent<RunStarter>();
            rs.startingMap = Load<MapData>(MAP_PATH);
            rs.mapSceneName = "MapScene";

            // Persistent onClick -> RunStarter.StartRun (survives scene save)
            UnityEventTools.AddPersistentListener(btn.onClick, rs.StartRun);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Stage2Setup] MainMenu wired with New Run button + RunStarter.");
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
                AssetDatabase.CreateFolder(parent, child);
        }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) Debug.LogWarning($"[Stage2Setup] Missing asset at {path}");
            return a;
        }

        static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(a, path);
            }
            return a;
        }

        static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Debug.Log("[Stage2Setup] Created EventSystem (" + es.name + ").");
            }
        }

        static Canvas CreateOverlayCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            return c;
        }

        static GameObject CreateFullScreenPanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return go;
        }

        static TextMeshProUGUI CreateText(string name, Transform parent, string text, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = fontSize; t.alignment = align; t.color = Color.white;
            return t;
        }

        static Button CreateButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, out TextMeshProUGUI labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.24f, 0.98f);

            labelText = CreateText(name + "Label", go.transform, label, Vector2.zero, size, 28, TextAlignmentOptions.Center);
            var lrt = labelText.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(10, 10); lrt.offsetMax = new Vector2(-10, -10);

            return go.GetComponent<Button>();
        }
    }
}
