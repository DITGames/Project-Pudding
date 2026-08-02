using System.Collections.Generic;
using UnityEngine;

namespace StageSelect
{
    [System.Serializable]
    public class MapGenerationSettings
    {
        [Header("マップ形状")]
        public int width = 7;          // 列数
        public int height = 15;        // ボスを除いた段数
        public int pathCount = 6;      // 下から引く経路の本数

        [Header("固定配置")]
        public int treasureFloor = 8;  // 宝箱が必ず出る段
        public int minSpecialFloor = 5;// Elite / Rest / Shop が出始める段
        // 最上段（height - 1）は必ず休憩

        [Header("出現ウェイト")]
        public float monsterWeight = 45f;
        public float eventWeight = 22f;
        public float eliteWeight = 16f;
        public float restWeight = 12f;
        public float shopWeight = 5f;

        [Header("レイアウト")]
        public float spacingX = 130f;
        public float spacingY = 150f;
        public float jitter = 26f;     // 座標のゆらぎ（機械的に見せない為）
    }

    public static class MapGenerator
    {
        public static MapGraph Generate(MapGenerationSettings s, int seed)
        {
            var rng = new System.Random(seed);
            var graph = new MapGraph(s.width, s.height);

            // 1. 下から上へ経路を pathCount 本引く
            int firstStart = -1;
            for (int i = 0; i < s.pathCount; i++)
            {
                int x = rng.Next(s.width);
                if (i == 0) firstStart = x;
                // 最初の2本は始点を必ず分ける（1本道スタートを防ぐ）
                if (i == 1 && s.width > 1)
                    while (x == firstStart) x = rng.Next(s.width);

                CreatePath(graph, x, rng);
            }

            // 2. 各マスの種類を決める
            AssignTypes(graph, s, rng);

            // 3. ボスを最上段に追加
            CreateBoss(graph);

            // 4. 表示座標を決める
            Layout(graph, s, rng);

            return graph;
        }

        // ---------------------------------------------------------------
        // 経路生成
        // ---------------------------------------------------------------
        static void CreatePath(MapGraph g, int startColumn, System.Random rng)
        {
            int x = startColumn;
            var current = g.GetOrCreate(0, x);

            for (int floor = 0; floor < g.Height - 1; floor++)
            {
                int nextX = ChooseNextColumn(g, floor, x, rng);
                var next = g.GetOrCreate(floor + 1, nextX);
                current.Connect(next);
                current = next;
                x = nextX;
            }
        }

        static int ChooseNextColumn(MapGraph g, int floor, int x, System.Random rng)
        {
            var candidates = new List<int>(3);
            for (int d = -1; d <= 1; d++)
            {
                int nx = x + d;
                if (nx < 0 || nx >= g.Width) continue;
                if (WouldCross(g, floor, x, nx)) continue;
                candidates.Add(nx);
            }
            if (candidates.Count == 0) return x; // 詰んだら真上に逃がす
            return candidates[rng.Next(candidates.Count)];
        }

        /// <summary>
        /// (floor, x) → (floor+1, nx) と (floor, nx) → (floor+1, x) は必ず交差する。
        /// スレスパのマップは線が交差しないので、この組み合わせを弾く。
        /// </summary>
        static bool WouldCross(MapGraph g, int floor, int x, int nx)
        {
            if (nx == x) return false;
            var neighbor = g.Get(floor, nx);
            if (neighbor == null) return false;

            foreach (var n in neighbor.Next)
                if (n.Column == x) return true;

            return false;
        }

        // ---------------------------------------------------------------
        // 種類の割り当て
        // ---------------------------------------------------------------
        static void AssignTypes(MapGraph g, MapGenerationSettings s, System.Random rng)
        {
            foreach (var node in g.AllNodes())
            {
                if (node.Floor == 0) { node.Type = NodeType.Monster; continue; }              // 初段は必ず戦闘
                if (node.Floor == s.treasureFloor) { node.Type = NodeType.Treasure; continue; }
                if (node.Floor == g.Height - 1) { node.Type = NodeType.Rest; continue; }      // ボス直前は必ず休憩

                node.Type = PickType(g, node, s, rng);
            }
        }

        static NodeType PickType(MapGraph g, MapNode node, MapGenerationSettings s, System.Random rng)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var t = Roll(s, rng);

                // 序盤には特殊マスを置かない
                if (node.Floor < s.minSpecialFloor && IsSpecial(t)) continue;
                // ボス直前の段が休憩なので、その1つ下に休憩は置かない
                if (t == NodeType.Rest && node.Floor == g.Height - 2) continue;
                // 同じ経路で特殊マスが連続しない
                if (IsSpecial(t) && HasParentOfType(node, t)) continue;
                // 同じ段の隣に同種の特殊マスを置かない
                if (IsSpecial(t) && HasNeighborOfType(g, node, t)) continue;

                return t;
            }
            return NodeType.Monster; // 条件を満たせなければ通常戦闘に丸める
        }

        static bool IsSpecial(NodeType t)
            => t == NodeType.Elite || t == NodeType.Rest || t == NodeType.Shop;

        static bool HasParentOfType(MapNode node, NodeType t)
        {
            foreach (var p in node.Prev)
                if (p.Type == t) return true;
            return false;
        }

        static bool HasNeighborOfType(MapGraph g, MapNode node, NodeType t)
        {
            for (int d = -1; d <= 1; d += 2)
            {
                var n = g.Get(node.Floor, node.Column + d);
                if (n != null && n.Type == t) return true;
            }
            return false;
        }

        static NodeType Roll(MapGenerationSettings s, System.Random rng)
        {
            float total = s.monsterWeight + s.eventWeight + s.eliteWeight + s.restWeight + s.shopWeight;
            float v = (float)rng.NextDouble() * total;

            if ((v -= s.monsterWeight) < 0f) return NodeType.Monster;
            if ((v -= s.eventWeight) < 0f) return NodeType.Event;
            if ((v -= s.eliteWeight) < 0f) return NodeType.Elite;
            if ((v -= s.restWeight) < 0f) return NodeType.Rest;
            return NodeType.Shop;
        }

        // ---------------------------------------------------------------
        // ボス / レイアウト
        // ---------------------------------------------------------------
        static void CreateBoss(MapGraph g)
        {
            var boss = new MapNode(g.Height, (g.Width - 1) / 2) { Type = NodeType.Boss };
            g.Boss = boss;

            foreach (var top in g.FloorNodes(g.Height - 1))
                top.Connect(boss);
        }

        static void Layout(MapGraph g, MapGenerationSettings s, System.Random rng)
        {
            float centerColumn = (g.Width - 1) * 0.5f;

            foreach (var node in g.AllNodes())
            {
                float jx = ((float)rng.NextDouble() * 2f - 1f) * s.jitter;
                float jy = ((float)rng.NextDouble() * 2f - 1f) * s.jitter;
                node.LocalPosition = new Vector2(
                    (node.Column - centerColumn) * s.spacingX + jx,
                    node.Floor * s.spacingY + jy);
            }

            if (g.Boss != null)
                g.Boss.LocalPosition = new Vector2(0f, g.Height * s.spacingY);
        }
    }
}
