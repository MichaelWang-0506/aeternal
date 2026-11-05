using System.Collections.Generic;
using System.Linq;
using Aeternal.Encounters;

namespace Aeternal.Core
{
    /// <summary>What a map node holds.</summary>
    public enum MapNodeType { None, Enemy, Elite, Event, Rest, Treasure, Boss, Shop }

    /// <summary>One node of a region's branching map.</summary>
    public class RunMapNode
    {
        public int id;
        public int floor;
        public int order;                       // left-to-right position within the floor
        public MapNodeType type;
        public EncounterData encounter;         // for Enemy, Elite and Boss nodes
        public readonly List<int> next = new List<int>();
        public bool visited;

        public bool IsFight => type == MapNodeType.Enemy || type == MapNodeType.Elite || type == MapNodeType.Boss;
    }

    /// <summary>
    /// A Slay-the-Spire-style map for one region, generated per run from MapData: floors of
    /// 2-3 nodes climbing to a single boss. Each floor's node types follow MapData.floorPlan;
    /// nodes connect only to the floor above, without crossing, and every node is reachable.
    /// </summary>
    public class RunMap
    {
        public readonly List<RunMapNode> nodes = new List<RunMapNode>();
        public int FloorCount { get; private set; }

        public IEnumerable<RunMapNode> Floor(int floor) => nodes.Where(n => n.floor == floor).OrderBy(n => n.order);
        public RunMapNode Get(int id) => id >= 0 && id < nodes.Count ? nodes[id] : null;

        /// <summary>Rebuilds a saved map. Nodes must be in id order.</summary>
        public static RunMap Restore(int floorCount, IEnumerable<RunMapNode> nodes)
        {
            var map = new RunMap { FloorCount = floorCount };
            map.nodes.AddRange(nodes);
            return map;
        }

        public static RunMap Generate(MapData data, System.Random rng)
        {
            var map = new RunMap();
            var plan = data.floorPlan != null && data.floorPlan.Count > 0 ? data.floorPlan : MapFloorRule.DefaultPlan();
            map.FloorCount = plan.Count + 1; // + the boss floor

            for (int f = 0; f < plan.Count; f++)
            {
                var rule = plan[f];
                int count = rng.Next(rule.minNodes, rule.maxNodes + 1);
                var types = new List<MapNodeType>();
                for (int i = 0; i < count; i++) types.Add(rule.Pick(rng));
                // A floor's guaranteed types replace random nodes if they weren't rolled.
                Guarantee(types, rule.guaranteed, rule, rng);
                Guarantee(types, rule.alsoGuaranteed, rule, rng);

                for (int i = 0; i < count; i++)
                {
                    var node = new RunMapNode { id = map.nodes.Count, floor = f, order = i, type = types[i] };
                    node.encounter = PickEncounter(data, node.type, f, plan.Count, rng);
                    map.nodes.Add(node);
                }
            }
            map.nodes.Add(new RunMapNode
            {
                id = map.nodes.Count, floor = plan.Count, order = 0,
                type = MapNodeType.Boss, encounter = data.bossEncounter,
            });

            for (int f = 0; f < map.FloorCount - 1; f++) Connect(map.Floor(f).ToList(), map.Floor(f + 1).ToList(), rng);
            return map;
        }

        /// <summary>
        /// Makes sure the floor holds this type, replacing a random node that isn't one of the
        /// floor's other guaranteed types.
        /// </summary>
        static void Guarantee(List<MapNodeType> types, MapNodeType type, MapFloorRule rule, System.Random rng)
        {
            if (type == MapNodeType.None || types.Contains(type)) return;
            var free = new List<int>();
            for (int i = 0; i < types.Count; i++)
                if (types[i] != rule.guaranteed && types[i] != rule.alsoGuaranteed) free.Add(i);
            if (free.Count > 0) types[free[rng.Next(free.Count)]] = type;
        }

        /// <summary>
        /// Links each node to the node(s) above it at the same relative position, sometimes a
        /// neighbour too, then gives any unreached node a link from its nearest neighbour below.
        /// Positions only ever increase together, so paths don't cross.
        /// </summary>
        static void Connect(List<RunMapNode> below, List<RunMapNode> above, System.Random rng)
        {
            var reached = new HashSet<int>();
            for (int i = 0; i < below.Count; i++)
            {
                float t = below.Count == 1 ? 0.5f : (float)i / (below.Count - 1);
                int j = (int)System.Math.Round(t * (above.Count - 1));
                Link(below[i], above[j], reached);
                // Branch to the right neighbour now and then, for a choice of route.
                if (j + 1 < above.Count && rng.NextDouble() < 0.45 && (i + 1 >= below.Count || NextTarget(i + 1, below.Count, above.Count) > j))
                    Link(below[i], above[j + 1], reached);
            }
            foreach (var node in above)
            {
                if (reached.Contains(node.id)) continue;
                var nearest = below.OrderBy(b => System.Math.Abs(Relative(b.order, below.Count) - Relative(node.order, above.Count))).First();
                Link(nearest, node, reached);
            }
        }

        static int NextTarget(int i, int belowCount, int aboveCount)
        {
            float t = belowCount == 1 ? 0.5f : (float)i / (belowCount - 1);
            return (int)System.Math.Round(t * (aboveCount - 1));
        }

        static float Relative(int order, int count) => count == 1 ? 0.5f : (float)order / (count - 1);

        static void Link(RunMapNode from, RunMapNode to, HashSet<int> reached)
        {
            if (!from.next.Contains(to.id)) from.next.Add(to.id);
            reached.Add(to.id);
        }

        /// <summary>
        /// Normal enemies are listed easiest first; early floors draw only from the easiest,
        /// later floors from more of the list.
        /// </summary>
        static EncounterData PickEncounter(MapData data, MapNodeType type, int floor, int floors, System.Random rng)
        {
            List<EncounterData> pool;
            if (type == MapNodeType.Enemy) pool = data.normalEncounters;
            else if (type == MapNodeType.Elite) pool = data.eliteEncounters;
            else return null;
            if (pool == null || pool.Count == 0) return null;
            if (type == MapNodeType.Elite) return pool[rng.Next(pool.Count)];
            int reach = System.Math.Min(pool.Count, 1 + floor * pool.Count / System.Math.Max(1, floors - 1));
            return pool[rng.Next(System.Math.Max(1, reach))];
        }
    }
}
