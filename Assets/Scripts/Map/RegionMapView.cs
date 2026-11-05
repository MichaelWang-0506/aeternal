using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Aeternal.Core;
using Aeternal.FX;
using Aeternal.UI;

namespace Aeternal.Map
{
    /// <summary>
    /// The branching map screen (Slay the Spire style) for a region with MapData.branchingMap:
    /// floors climbing to the boss, the path taken in gold, and the nodes reachable next
    /// pulsing. Fights load combat; events, rests and treasure resolve here. Shows the region's
    /// opening narration on the first visit. Replaces the map scene's old static layout and
    /// attaches itself; needs no scene wiring.
    /// </summary>
    public class RegionMapView : MonoBehaviour
    {
        static readonly string[] OldSceneRoots = { "Game World", "RunMapCanvas", "Canvas" };
        static readonly Color Gold = new Color(1f, 0.82f, 0.36f, 1f);

        const float MapHeight = 760f;
        const float NodeSpacing = 270f;
        const float NodeSize = 96f;
        const float BossSize = 150f;

        RunManager run;
        RectTransform mapArea;
        StoryOverlay overlay;
        TMP_Text infoText;
        readonly List<(RectTransform rect, float phase)> pulsing = new List<(RectTransform, float)>();
        bool busy;

        /// <summary>Whether a node (event, merchant, rest, ...) or the deck view is still open.</summary>
        public bool Busy => busy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachIfMap();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachIfMap();

        static void AttachIfMap()
        {
            var run = RunManager.Instance;
            if (run == null || run.Map == null) return;
            if (SceneManager.GetActiveScene().name != run.mapSceneName) return;
            if (FindFirstObjectByType<RegionMapView>() != null) return;
            new GameObject("Region Map").AddComponent<RegionMapView>().run = run;
        }

        IEnumerator Start()
        {
            foreach (var rootName in OldSceneRoots)
            {
                var old = GameObject.Find(rootName);
                if (old != null) old.SetActive(false);
            }
            Build();
            Redraw();

            var map = run.CurrentMap;
            if (!run.OpeningShown && map.openingNarrative != null && map.openingNarrative.Count > 0)
            {
                run.OpeningShown = true;
                busy = true;
                overlay.CoverScreen();
                yield return overlay.PlayNarration(map.openingNarrative);
                yield return overlay.FadeBackToGame();
                busy = false;
            }
            run.OpeningShown = true;
        }

        void Update()
        {
            // GDD: Space opens the Shift page before combat.
            if (!busy && !PauseMenu.IsPaused && Input.GetKeyDown(KeyCode.Space)) ShowShift();
            foreach (var (rect, phase) in pulsing)
            {
                if (rect == null) continue;
                float s = 1f + 0.08f * Mathf.Sin(Time.time * 3.5f + phase);
                rect.localScale = new Vector3(s, s, 1f);
            }
        }

        // ------------------------------------------------------------------
        // Layout
        // ------------------------------------------------------------------

        void Build()
        {
            var canvasGo = new GameObject("Map Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            var style = BattlefieldStyle.Load();
            var bg = Stretch(Rect("Background", root));
            var bgImage = bg.gameObject.AddComponent<Image>();
            bgImage.color = new Color(0.07f, 0.06f, 0.08f, 1f);
            if (style.backgroundSprite != null)
            {
                var art = Stretch(Rect("Art", bg));
                var artImage = art.gameObject.AddComponent<Image>();
                artImage.sprite = style.backgroundSprite;
                artImage.color = new Color(0.3f, 0.3f, 0.36f, 1f);
                artImage.preserveAspect = false;
            }
            var vignette = Stretch(Rect("Vignette", bg)).gameObject.AddComponent<Image>();
            vignette.sprite = ProceduralSprites.Vignette();
            vignette.color = new Color(0f, 0f, 0f, 0.85f);
            vignette.raycastTarget = false;

            var title = Text(root, run.CurrentMap.regionName.ToUpperInvariant(), 64f, Gold);
            title.fontStyle = FontStyles.Bold;
            Place(title.rectTransform, new Vector2(0f, 480f), new Vector2(1200f, 80f));
            var subtitle = Text(root, "Choose your path", 28f, new Color(1f, 1f, 1f, 0.7f));
            subtitle.fontStyle = FontStyles.Italic;
            Place(subtitle.rectTransform, new Vector2(0f, 425f), new Vector2(1200f, 40f));

            infoText = Text(root, "", 26f, Color.white);
            infoText.alignment = TextAlignmentOptions.TopLeft;
            var info = infoText.rectTransform;
            info.anchorMin = info.anchorMax = info.pivot = new Vector2(0f, 1f);
            info.anchoredPosition = new Vector2(40f, -36f);
            info.sizeDelta = new Vector2(520f, 160f);

            var legend = Text(root, "Legend\n<size=80%>Portrait  Enemy\n<color=#FFD24D>Gold ring</color>  Elite\n?  Event\nFlame  Rest\nGem  Treasure\n$  Merchant</size>", 22f, new Color(1f, 1f, 1f, 0.7f));
            legend.alignment = TextAlignmentOptions.BottomRight;
            var legendRect = legend.rectTransform;
            legendRect.anchorMin = legendRect.anchorMax = legendRect.pivot = new Vector2(1f, 0f);
            legendRect.anchoredPosition = new Vector2(-40f, 36f);
            legendRect.sizeDelta = new Vector2(320f, 230f);

            var deckButton = MakeButton(root, "View Deck", ShowDeck);
            deckButton.anchorMin = deckButton.anchorMax = deckButton.pivot = new Vector2(0f, 1f);
            deckButton.anchoredPosition = new Vector2(40f, -240f);
            deckButton.sizeDelta = new Vector2(240f, 64f);

            var shiftButton = MakeButton(root, "Shift (Space)", ShowShift);
            shiftButton.anchorMin = shiftButton.anchorMax = shiftButton.pivot = new Vector2(0f, 1f);
            shiftButton.anchoredPosition = new Vector2(296f, -240f);
            shiftButton.sizeDelta = new Vector2(240f, 64f);

            mapArea = Rect("Map", root);
            mapArea.anchorMin = mapArea.anchorMax = mapArea.pivot = new Vector2(0.5f, 0.5f);
            mapArea.anchoredPosition = new Vector2(0f, -50f);
            mapArea.sizeDelta = new Vector2(1200f, MapHeight);

            overlay = StoryOverlay.Create(transform);
        }

        void Redraw()
        {
            pulsing.Clear();
            foreach (Transform child in mapArea) Destroy(child.gameObject);

            var map = run.Map;
            var available = new HashSet<int>(run.AvailableNodes().Select(n => n.id));
            var lines = Rect("Paths", mapArea);
            Stretch(lines);
            var nodesLayer = Rect("Nodes", mapArea);
            Stretch(nodesLayer);

            foreach (var node in map.nodes)
            {
                foreach (int nextId in node.next)
                {
                    var next = map.Get(nextId);
                    bool walked = node.visited && next.visited;
                    bool open = node.id == run.CurrentMapNodeId && available.Contains(nextId);
                    var color = walked ? Gold : open ? new Color(1f, 1f, 1f, 0.85f) : new Color(1f, 1f, 1f, 0.22f);
                    Line(lines, Position(node), Position(next), walked || open ? 6f : 4f, color);
                }
            }
            foreach (var node in map.nodes) MakeNode(nodesLayer, node, available.Contains(node.id));

            int hp = run.CarriedHealth;
            string health = hp < 0 ? "full" : run.PlayerMaxHealth > 0 ? $"{hp}/{run.PlayerMaxHealth}" : hp.ToString();
            string shards = run.CollectsStormShards || run.StormShards.Count > 0
                ? $"\nStorm Shards: <color=#FFD24D>{run.StormShards.Count}</color> <size=75%>(sealed until the boss's blessing)</size>"
                : "";
            infoText.text = $"Health: {health}\nGold: <color=#FFD24D>{run.Gold}</color>   Aether: <color=#9FE3FF>{run.Aether}</color>\n" +
                            $"Deck: {run.RunDeck.Count} cards{shards}";
        }

        Vector2 Position(RunMapNode node)
        {
            int count = run.Map.Floor(node.floor).Count();
            float x = (node.order - (count - 1) * 0.5f) * NodeSpacing;
            // A little sideways wobble per node, fixed by its id, so floors don't look ruled.
            x += ((node.id * 37) % 7 - 3) * 9f;
            float y = -MapHeight * 0.5f + 40f + node.floor * ((MapHeight - 80f) / Mathf.Max(1, run.Map.FloorCount - 1));
            return new Vector2(x, y);
        }

        void MakeNode(Transform parent, RunMapNode node, bool available)
        {
            bool boss = node.type == MapNodeType.Boss;
            bool here = node.id == run.CurrentMapNodeId;
            float size = boss ? BossSize : NodeSize;

            var go = new GameObject($"Node {node.id}", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Position(node);
            rect.sizeDelta = new Vector2(size, size);
            var image = go.GetComponent<Image>();
            image.sprite = ProceduralSprites.Disc();
            image.color = NodeColor(node.type);
            if (node.type == MapNodeType.Elite || boss || here)
            {
                var ring = go.AddComponent<Outline>();
                ring.effectColor = here ? Color.white : Gold;
                ring.effectDistance = new Vector2(4f, -4f);
            }

            AddIcon(rect, node, size);

            bool cleared = node.visited && !here;
            var label = Text(rect, cleared ? "Cleared" : NodeLabel(node), boss ? 26f : 20f, Color.white);
            label.fontStyle = FontStyles.Bold;
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color32(0, 0, 0, 255);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -4f);
            labelRect.sizeDelta = new Vector2(240f, 30f);

            var button = go.GetComponent<Button>();
            button.interactable = available;
            button.onClick.AddListener(() => Choose(node));

            var group = go.AddComponent<CanvasGroup>();
            if (cleared)
            {
                group.alpha = 0.45f;
            }
            else if (!available && !here)
            {
                group.alpha = 0.55f;
            }
            if (available) pulsing.Add((rect, node.id * 0.9f));
        }

        void AddIcon(RectTransform node, RunMapNode data, float size)
        {
            if (data.IsFight && data.encounter != null && data.encounter.sprite != null)
            {
                var icon = Rect("Portrait", node);
                icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0.5f, 0.5f);
                icon.sizeDelta = new Vector2(size * 0.95f, size * 0.95f);
                var image = icon.gameObject.AddComponent<Image>();
                image.sprite = data.encounter.sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                return;
            }
            if (data.type == MapNodeType.Event || data.type == MapNodeType.Shop)
            {
                var mark = Text(node, data.type == MapNodeType.Shop ? "$" : "?", size * 0.6f, data.type == MapNodeType.Shop ? Gold : Color.white);
                mark.fontStyle = FontStyles.Bold;
                Stretch(mark.rectTransform);
                return;
            }
            Sprite sprite = data.type == MapNodeType.Rest ? ProceduralSprites.Drop()
                : data.type == MapNodeType.Treasure ? ProceduralSprites.Crystal() : null;
            if (sprite == null) return;
            var glyph = Rect("Icon", node);
            glyph.anchorMin = glyph.anchorMax = glyph.pivot = new Vector2(0.5f, 0.5f);
            glyph.sizeDelta = new Vector2(size * 0.55f, size * 0.55f);
            var img = glyph.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = data.type == MapNodeType.Rest ? new Color(1f, 0.55f, 0.15f) : Gold;
        }

        static Color NodeColor(MapNodeType type)
        {
            switch (type)
            {
                case MapNodeType.Enemy: return new Color(0.42f, 0.16f, 0.13f, 1f);
                case MapNodeType.Elite: return new Color(0.45f, 0.3f, 0.08f, 1f);
                case MapNodeType.Boss: return new Color(0.55f, 0.1f, 0.1f, 1f);
                case MapNodeType.Event: return new Color(0.32f, 0.22f, 0.5f, 1f);
                case MapNodeType.Rest: return new Color(0.42f, 0.26f, 0.1f, 1f);
                case MapNodeType.Treasure: return new Color(0.12f, 0.34f, 0.38f, 1f);
                case MapNodeType.Shop: return new Color(0.42f, 0.34f, 0.08f, 1f);
                default: return Color.gray;
            }
        }

        static string NodeLabel(RunMapNode node)
        {
            switch (node.type)
            {
                case MapNodeType.Boss: return node.encounter != null ? node.encounter.displayName.ToUpperInvariant() : "BOSS";
                case MapNodeType.Elite: return "Elite";
                case MapNodeType.Enemy: return "Enemy";
                case MapNodeType.Event: return "Event";
                case MapNodeType.Rest: return "Rest";
                case MapNodeType.Treasure: return "Treasure";
                case MapNodeType.Shop: return "Merchant";
                default: return "";
            }
        }

        // ------------------------------------------------------------------
        // Choosing a node
        // ------------------------------------------------------------------

        void Choose(RunMapNode node)
        {
            if (busy) return;
            if (!run.EnterMapNode(node)) return;
            if (node.IsFight) return; // combat is loading

            switch (node.type)
            {
                case MapNodeType.Event:
                    busy = true;
                    RegionEventPanel.Show(run, MaxHealth(), Resolved);
                    break;
                case MapNodeType.Rest:
                    run.HealBetweenFights(run.CurrentMap.restHeal, MaxHealth());
                    StartCoroutine(Announce("Rest", $"You rest by a sheltered fire and recover {run.CurrentMap.restHeal} health."));
                    break;
                case MapNodeType.Treasure:
                    StartCoroutine(Announce("Treasure", OpenTreasure()));
                    break;
                case MapNodeType.Shop:
                    busy = true;
                    ShopPanel.Show(run, Resolved);
                    break;
            }
        }

        /// <summary>A Storm Shard while the god's deck is sealed, otherwise a card for the deck.</summary>
        string OpenTreasure()
        {
            var map = run.CurrentMap;
            if (run.CollectsStormShards)
            {
                var shard = map.stormShardPool.Where(c => c != null).OrderBy(_ => Random.value).First();
                run.AddStormShard(shard);
                return $"Among the rocks, a shard of the storm: <color=#FFD24D>{CardName(shard)}</color>. " +
                       $"It is sealed in the {map.blessingDeck.deckId} deck until its blessing.";
            }
            var card = map.cardRewardPool.Where(c => c != null).OrderBy(_ => Random.value).FirstOrDefault();
            if (card == null) return "The chest is empty.";
            run.AddCardToDeck(card);
            return $"You find <color=#FFD24D>{CardName(card)}</color> and add it to your deck.";
        }

        static string CardName(Aeternal.Cards.CardData card) => string.IsNullOrEmpty(card.id) ? card.name : card.id;

        IEnumerator Announce(string title, string text)
        {
            busy = true;
            yield return overlay.ShowTitle(title, text + "\n\nClick to continue");
            yield return overlay.FadeBackToGame();
            Resolved();
        }

        void Resolved()
        {
            busy = false;
            run.Save();
            Redraw();
        }

        int MaxHealth() => run.PlayerMaxHealth > 0 ? run.PlayerMaxHealth : int.MaxValue;

        /// <summary>The Shift page: Shift effects per deck and their Aether upgrades.</summary>
        void ShowShift()
        {
            if (busy) return;
            busy = true;
            ShiftPanel.Show(run, () =>
            {
                busy = false;
                run.Save();
                Redraw();
            });
        }

        /// <summary>The deck as card faces, with the sealed Storm Shards in their own section.</summary>
        void ShowDeck()
        {
            if (busy) return;
            busy = true;
            var sections = new List<CardGridPanel.Section>
            {
                new CardGridPanel.Section
                {
                    title = $"Deck  ({run.RunDeck.Count} cards)",
                    cards = run.RunDeck.OrderBy(c => c.costAP).ThenBy(c => c.name).ToList(),
                },
            };
            if (run.CollectsStormShards || run.StormShards.Count > 0)
            {
                string deck = run.CurrentMap.blessingDeck != null ? run.CurrentMap.blessingDeck.deckId : "god's";
                sections.Add(new CardGridPanel.Section
                {
                    title = $"Storm Shards  ({run.StormShards.Count})  <size=70%><color=#FFD24D>sealed in the {deck} deck until its blessing</color></size>",
                    cards = run.StormShards.ToList(),
                    dimmed = true,
                });
            }
            CardGridPanel.Show("YOUR DECK", sections, null, () => busy = false);
        }

        // ------------------------------------------------------------------
        // Building blocks
        // ------------------------------------------------------------------

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static RectTransform MakeButton(Transform parent, string label, System.Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.12f, 0.95f);
            var edge = go.GetComponent<Outline>();
            edge.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.6f);
            edge.effectDistance = new Vector2(2f, -2f);
            go.GetComponent<Button>().onClick.AddListener(() => onClick());
            go.AddComponent<HoverScale>();
            var text = Text(go.transform, label, 28f, Color.white);
            Stretch(text.rectTransform);
            return (RectTransform)go.transform;
        }

        static void Line(Transform parent, Vector2 from, Vector2 to, float width, Color color)
        {
            var rect = Rect("Path", parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            var delta = to - from;
            rect.anchoredPosition = from;
            rect.sizeDelta = new Vector2(delta.magnitude, width);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        static TMP_Text Text(Transform parent, string content, float size, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }
    }
}
