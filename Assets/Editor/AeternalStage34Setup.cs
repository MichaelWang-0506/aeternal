using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using TMPro;
using Aeternal.Cards;
using Aeternal.Decks;
using Aeternal.Stats;
using Aeternal.Encounters;
using Aeternal.UI;
using Aeternal.Combat;

namespace Aeternal.EditorTools
{
    /// <summary>
    /// Stage 3 (content) + Stage 4 (Shifting) automation:
    ///  - creates a Zeus/Thunder deck of cards + DeckData with shift-in/out effects,
    ///  - authors the Steampunk deck's shift effects,
    ///  - adds a Bandit enemy for variety and updates the Steampunk City map,
    ///  - wires a Shift HUD (deck label + shift button) into PlayGameScene.
    /// Idempotent. Run: -executeMethod Aeternal.EditorTools.AeternalStage34Setup.RunAll
    /// </summary>
    public static class AeternalStage34Setup
    {
        const string SO_ROOT = "Assets/Scriptable Objects";
        const string ENC_DIR = SO_ROOT + "/Encounters";
        const string MAP_PATH = SO_ROOT + "/Maps/Steampunk City.asset";
        const string ZEUS_DIR = SO_ROOT + "/Cards/Zeus Deck";
        const string ZEUS_CARDS_DIR = ZEUS_DIR + "/Zeus Cards";
        const string ZEUS_DECK_PATH = ZEUS_DIR + "/Zeus Deck.asset";
        const string STEAMPUNK_DECK_PATH = SO_ROOT + "/Cards/First Deck/First Deck.asset";
        const string THUNDER_ART = "Assets/Images/Thunder";
        const string PLAY_SCENE = "Assets/Scenes/PlayGameScene.unity";

        [MenuItem("Aeternal/Stage 3-4/Run Full Setup")]
        public static void RunAll()
        {
            try
            {
                Debug.Log("[Stage34Setup] === START ===");
                var zeus = CreateZeusDeck();
                AuthorSteampunkShift();
                CreateBanditAndUpdateMap(zeus);
                WireShiftHUD();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Stage34Setup] === DONE (success) ===");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Stage34Setup] FAILED: {e}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------
        // Stage 3: Zeus/Thunder deck
        // ------------------------------------------------------------------
        static DeckData CreateZeusDeck()
        {
            EnsureFolder(SO_ROOT + "/Cards", "Zeus Deck");
            EnsureFolder(ZEUS_DIR, "Zeus Cards");

            var punch      = Card("Punch", "zeus_punch", "Deal 2 lightning damage.", 1, "punch", Dmg(2, DamageType.Lightning));
            var strike     = Card("Thunder Strike", "zeus_strike", "Deal 4 lightning damage.", 1, "ThunderStrike", Dmg(4, DamageType.Lightning));
            var combo      = Card("Combo Punch", "zeus_combo", "Deal 6 lightning damage.", 2, "combopunch", Dmg(6, DamageType.Lightning));
            var spear      = Card("Thunder Spear", "zeus_spear", "Deal 5 lightning damage, then move 1 space.", 2, "thunderspear", Dmg(5, DamageType.Lightning), Move(1));
            var armor      = Card("Thunder Armor", "zeus_armor", "Gain 6 shield.", 1, "thunderarmor", Shield(6));
            var teleport   = Card("Teleport", "zeus_teleport", "Move 2 spaces (to the opposite side).", 1, "teleport", Move(2));
            var rage       = Card("Thunder's Rage", "zeus_rage", "Deal 9 lightning damage.", 3, "thundersrage", Dmg(9, DamageType.Lightning));
            var gnaw       = Card("Gnaw", "zeus_gnaw", "Deal 2 lightning damage and Stun the enemy for 1 turn.", 1, "gnaw", Dmg(2, DamageType.Lightning), Stun(1));
            var extraction = Card("Extraction", "zeus_extraction", "Draw 2 cards and gain 1 Divine Focus.", 2, "Extraction", Draw(2), GainDF(1));

            var deck = CreateOrLoad<DeckData>(ZEUS_DECK_PATH);
            deck.deckId = "Zeus";
            deck.description = "Thunder deck: fast, aggressive lightning strikes.";
            deck.defaultCards = new List<CardEntry>
            {
                Entry(punch, 2), Entry(strike, 2), Entry(combo, 1), Entry(spear, 1),
                Entry(armor, 1), Entry(teleport, 1), Entry(rage, 1), Entry(gnaw, 1), Entry(extraction, 1),
            };
            // Shifting INTO Zeus zaps the enemy for 3; no shift-out penalty.
            deck.onShiftIn = new List<EffectPayload> { Dmg(3, DamageType.Lightning) };
            deck.onShiftOut = new List<EffectPayload>();
            EditorUtility.SetDirty(deck);
            AssetDatabase.SaveAssets();
            Debug.Log("[Stage34Setup] Zeus deck (9 cards) created.");
            return deck;
        }

        static void AuthorSteampunkShift()
        {
            var deck = AssetDatabase.LoadAssetAtPath<DeckData>(STEAMPUNK_DECK_PATH);
            if (deck == null) { Debug.LogWarning("[Stage34Setup] Steampunk deck not found."); return; }
            // Shifting INTO Steampunk raises a guard (3 shield).
            deck.onShiftIn = new List<EffectPayload> { Shield(3) };
            deck.onShiftOut = new List<EffectPayload>();
            EditorUtility.SetDirty(deck);
            Debug.Log("[Stage34Setup] Steampunk deck shift effects authored.");
        }

        // ------------------------------------------------------------------
        // Stage 3: Bandit enemy + map update
        // ------------------------------------------------------------------
        static void CreateBanditAndUpdateMap(DeckData zeus)
        {
            // Bandit stats
            var banditStats = CreateOrLoad<PlayerStats>(SO_ROOT + "/Bandit.asset");
            banditStats.maxHealth = 15;
            banditStats.maxAP = 3;
            banditStats.maxDF = 2;
            EditorUtility.SetDirty(banditStats);

            var bandit = CreateOrLoad<EncounterData>(ENC_DIR + "/Bandit Encounter.asset");
            bandit.encounterId = "bandit";
            bandit.displayName = "Bandit";
            bandit.isBoss = false;
            bandit.stats = banditStats;
            bandit.baseDamage = 4;
            bandit.damageType = DamageType.Physical;
            bandit.canAOE = false;
            bandit.turnsBetweenAttacks = 1;
            bandit.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Characters/bandit.png");
            EditorUtility.SetDirty(bandit);

            var goblin = AssetDatabase.LoadAssetAtPath<EncounterData>(ENC_DIR + "/Goblin Encounter.asset");
            var thanatos = AssetDatabase.LoadAssetAtPath<EncounterData>(ENC_DIR + "/Thanatos Encounter.asset");

            var map = AssetDatabase.LoadAssetAtPath<MapData>(MAP_PATH);
            if (map != null)
            {
                map.encounters = new List<EncounterData> { goblin, bandit, goblin, thanatos };
                map.shiftDecks = new List<DeckData> { zeus };
                EditorUtility.SetDirty(map);
                Debug.Log("[Stage34Setup] Map updated: Goblin, Bandit, Goblin, Thanatos + Zeus shift deck.");
            }
            else Debug.LogWarning("[Stage34Setup] Steampunk City map not found.");
        }

        // ------------------------------------------------------------------
        // Stage 4: Shift HUD in combat
        // ------------------------------------------------------------------
        static void WireShiftHUD()
        {
            var scene = EditorSceneManager.OpenScene(PLAY_SCENE, OpenSceneMode.Single);
            EnsureEventSystem();
            DestroyIfExists("ShiftHUDCanvas");

            var canvas = CreateOverlayCanvas("ShiftHUDCanvas", 90);

            var label = CreateText("DeckLabel", canvas.transform, "Deck: --", new Vector2(0, 470), new Vector2(900, 60), 34, TextAlignmentOptions.Center);
            var btn = CreateButton("ShiftButton", canvas.transform, "SHIFT (Z)", new Vector2(760, 380), new Vector2(240, 84), out _);

            var hudGO = new GameObject("ShiftHUD");
            var hud = hudGO.AddComponent<ShiftHUD>();
            hud.combat = UnityEngine.Object.FindFirstObjectByType<CombatManager>();
            hud.deckLabel = label;
            hud.shiftButton = btn;
            hud.root = canvas.gameObject;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Stage34Setup] Shift HUD wired into PlayGameScene.");
        }

        // ------------------------------------------------------------------
        // Card / effect builders
        // ------------------------------------------------------------------
        static CardData Card(string assetName, string id, string desc, int costAP, string artName, params EffectPayload[] effects)
        {
            var c = CreateOrLoad<CardData>($"{ZEUS_CARDS_DIR}/{assetName}.asset");
            c.id = id;
            c.description = desc;
            c.costAP = costAP;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{THUNDER_ART}/{artName}.png");
            if (sprite != null) c.artwork = sprite;
            c.effects = new List<EffectPayload>(effects);
            EditorUtility.SetDirty(c);
            return c;
        }

        static CardEntry Entry(CardData card, int count) => new CardEntry { card = card, count = count };

        static EffectPayload Dmg(int mag, DamageType type) =>
            new EffectPayload { effectType = EffectType.Damage, magnitude = mag, target = EffectTarget.Enemy, damageType = type };
        static EffectPayload Shield(int mag) =>
            new EffectPayload { effectType = EffectType.AddShield, magnitude = mag, target = EffectTarget.Self };
        static EffectPayload Move(int mag) =>
            new EffectPayload { effectType = EffectType.Move, magnitude = mag, target = EffectTarget.Self };
        static EffectPayload Draw(int mag) =>
            new EffectPayload { effectType = EffectType.DrawCards, magnitude = mag, target = EffectTarget.Self };
        static EffectPayload GainDF(int mag) =>
            new EffectPayload { effectType = EffectType.ModifyDF, magnitude = mag, target = EffectTarget.Self };
        static EffectPayload Stun(int dur) =>
            new EffectPayload
            {
                effectType = EffectType.ApplyStatus, magnitude = 1, durationRounds = dur, target = EffectTarget.Enemy,
                statusEffectData = new StatusEffectData { statusId = "Blackout", behavior = StatusEffectBehavior.Blackout, behaviorValue = 0 }
            };

        // ------------------------------------------------------------------
        // Shared helpers
        // ------------------------------------------------------------------
        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
                AssetDatabase.CreateFolder(parent, child);
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
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
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
            go.GetComponent<Image>().color = new Color(0.22f, 0.18f, 0.30f, 0.98f);
            labelText = CreateText(name + "Label", go.transform, label, Vector2.zero, size, 26, TextAlignmentOptions.Center);
            var lrt = labelText.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(8, 8); lrt.offsetMax = new Vector2(-8, -8);
            return go.GetComponent<Button>();
        }
    }
}
