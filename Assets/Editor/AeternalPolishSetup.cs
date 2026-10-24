using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Aeternal.UI;

namespace Aeternal.EditorTools
{
    /// <summary>
    /// Visual-polish automation:
    ///  - adds live text overlays (name / AP cost / description) to the Card prefab, wired to
    ///    CardUI so what the player reads is generated from card data, never stale artwork text;
    ///  - adds a "Region Cleared" panel to PlayGameScene, shown when the boss is beaten.
    /// Idempotent. Run: -executeMethod Aeternal.EditorTools.AeternalPolishSetup.RunAll
    /// </summary>
    public static class AeternalPolishSetup
    {
        const string CARD_PREFAB = "Assets/Prefabs/Card.prefab";
        const string PLAY_SCENE = "Assets/Scenes/PlayGameScene.unity";

        [MenuItem("Aeternal/Polish/Run Full Setup")]
        public static void RunAll()
        {
            try
            {
                Debug.Log("[PolishSetup] === START ===");
                AddCardTextOverlays();
                AddRegionClearPanel();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[PolishSetup] === DONE (success) ===");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PolishSetup] FAILED: {e}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------
        // Card prefab: name / cost / description overlays
        // ------------------------------------------------------------------
        static void AddCardTextOverlays()
        {
            var root = PrefabUtility.LoadPrefabContents(CARD_PREFAB);
            try
            {
                var ui = root.GetComponent<CardUI>();
                if (ui == null) throw new Exception("Card prefab has no CardUI component");

                // Remove previously generated overlays so re-runs rebuild cleanly.
                DestroyChild(root.transform, "CardTextOverlay");

                var rootRT = root.GetComponent<RectTransform>();
                Vector2 cardSize = rootRT != null ? rootRT.sizeDelta : new Vector2(200, 300);

                // Container stretched over the whole card, ignoring raycasts so clicks
                // still reach the card button.
                var overlay = new GameObject("CardTextOverlay", typeof(RectTransform));
                overlay.transform.SetParent(root.transform, false);
                var ovRT = overlay.GetComponent<RectTransform>();
                ovRT.anchorMin = Vector2.zero; ovRT.anchorMax = Vector2.one;
                ovRT.offsetMin = Vector2.zero; ovRT.offsetMax = Vector2.zero;

                // Cost badge — top-left.
                var costGO = MakeStrip(overlay.transform, "CostBadge",
                    anchorMin: new Vector2(0, 1), anchorMax: new Vector2(0, 1),
                    pivot: new Vector2(0, 1), pos: new Vector2(4, -4), size: new Vector2(56, 30),
                    bg: new Color(0f, 0f, 0f, 0.75f));
                var costText = MakeText(costGO.transform, "CostText", "1 AP", 16, TextAlignmentOptions.Center, FontStyles.Bold);
                costText.color = new Color(1f, 0.9f, 0.4f);

                // Name strip — across the top.
                var nameGO = MakeStrip(overlay.transform, "NameStrip",
                    anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1),
                    pivot: new Vector2(0.5f, 1), pos: new Vector2(0, -38), size: new Vector2(-8, 26),
                    bg: new Color(0f, 0f, 0f, 0.6f));
                var nameText = MakeText(nameGO.transform, "NameText", "Card", 15, TextAlignmentOptions.Center, FontStyles.Bold);

                // Description strip — bottom ~35% of the card.
                float descHeight = Mathf.Max(70f, cardSize.y * 0.35f);
                var descGO = MakeStrip(overlay.transform, "DescriptionStrip",
                    anchorMin: new Vector2(0, 0), anchorMax: new Vector2(1, 0),
                    pivot: new Vector2(0.5f, 0), pos: new Vector2(0, 4), size: new Vector2(-8, descHeight),
                    bg: new Color(0f, 0f, 0f, 0.78f));
                var descText = MakeText(descGO.transform, "DescriptionText", "", 13, TextAlignmentOptions.Center, FontStyles.Normal);
                descText.enableWordWrapping = true;
                descText.overflowMode = TextOverflowModes.Ellipsis;
                var dtRT = descText.GetComponent<RectTransform>();
                dtRT.offsetMin = new Vector2(6, 4); dtRT.offsetMax = new Vector2(-6, -4);

                ui.nameText = nameText;
                ui.costText = costText;
                ui.descriptionText = descText;

                PrefabUtility.SaveAsPrefabAsset(root, CARD_PREFAB);
                Debug.Log("[PolishSetup] Card prefab: live text overlays added and wired.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void DestroyChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        static GameObject MakeStrip(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 pos, Vector2 size, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = bg;
            img.raycastTarget = false;
            return go;
        }

        static TextMeshProUGUI MakeText(Transform parent, string name, string text, float fontSize,
            TextAlignmentOptions align, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = fontSize; t.alignment = align; t.fontStyle = style;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        // ------------------------------------------------------------------
        // PlayGameScene: Region Cleared panel
        // ------------------------------------------------------------------
        static void AddRegionClearPanel()
        {
            var scene = EditorSceneManager.OpenScene(PLAY_SCENE, OpenSceneMode.Single);

            var canvasGO = GameObject.Find("EndCombatCanvas");
            var ctrl = UnityEngine.Object.FindFirstObjectByType<CombatEndController>();
            if (canvasGO == null || ctrl == null)
                throw new Exception("EndCombatCanvas / CombatEndController not found — run Stage 2 setup first.");

            var old = canvasGO.transform.Find("RegionClearPanel");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            var panel = new GameObject("RegionClearPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGO.transform, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.05f, 0.03f, 0.12f, 0.95f);

            var title = MakeText(panel.transform, "Title", "REGION CLEARED", 10, TextAlignmentOptions.Center, FontStyles.Bold);
            var tRT = title.GetComponent<RectTransform>();
            tRT.anchorMin = tRT.anchorMax = new Vector2(0.5f, 0.5f);
            tRT.sizeDelta = new Vector2(1200, 130); tRT.anchoredPosition = new Vector2(0, 200);
            title.fontSize = 84; title.color = new Color(1f, 0.85f, 0.35f);

            var sub = MakeText(panel.transform, "Subtitle",
                "Thanatos has fallen. The Steampunk City is free.", 10, TextAlignmentOptions.Center, FontStyles.Normal);
            var sRT = sub.GetComponent<RectTransform>();
            sRT.anchorMin = sRT.anchorMax = new Vector2(0.5f, 0.5f);
            sRT.sizeDelta = new Vector2(1200, 60); sRT.anchoredPosition = new Vector2(0, 90);
            sub.fontSize = 34;

            // Continue button
            var btnGO = new GameObject("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(panel.transform, false);
            var bRT = btnGO.GetComponent<RectTransform>();
            bRT.anchorMin = bRT.anchorMax = new Vector2(0.5f, 0.5f);
            bRT.sizeDelta = new Vector2(340, 90); bRT.anchoredPosition = new Vector2(0, -120);
            btnGO.GetComponent<Image>().color = new Color(0.25f, 0.2f, 0.35f, 0.98f);
            var bLabel = MakeText(btnGO.transform, "Label", "Continue", 10, TextAlignmentOptions.Center, FontStyles.Bold);
            bLabel.fontSize = 30;

            ctrl.regionClearPanel = panel;
            ctrl.regionClearContinueButton = btnGO.GetComponent<Button>();
            panel.SetActive(false);
            EditorUtility.SetDirty(ctrl);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PolishSetup] Region Cleared panel added to PlayGameScene.");
        }
    }
}
