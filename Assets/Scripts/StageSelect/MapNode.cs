using System.Collections.Generic;
using UnityEngine;

namespace StageSelect
{
    /// <summary>マス目の種類。</summary>
    public enum NodeType
    {
        Monster,   // 通常戦闘
        Elite,     // エリート戦
        Rest,      // 休憩（焚き火）
        Shop,      // ショップ
        Treasure,  // 宝箱
        Event,     // イベント
        Boss       // ボス
    }

    /// <summary>マップ上の1マス。前後のマスへの参照を持つ有向グラフのノード。</summary>
    public class MapNode
    {
        public readonly int Floor;   // 下から数えた段数（0 が最下段）
        public readonly int Column;  // 左から数えた列

        public NodeType Type;

        /// <summary>Content 内でのローカル座標。MapGenerator.Layout が設定する。</summary>
        public Vector2 LocalPosition;

        public readonly List<MapNode> Next = new List<MapNode>();
        public readonly List<MapNode> Prev = new List<MapNode>();

        public MapNode(int floor, int column)
        {
            Floor = floor;
            Column = column;
        }

        public void Connect(MapNode to)
        {
            if (to == null || Next.Contains(to)) return;
            Next.Add(to);
            to.Prev.Add(this);
        }
    }

    /// <summary>生成されたマップ全体。</summary>
    public class MapGraph
    {
        public readonly int Width;
        public readonly int Height;      // ボスを除いた段数
        public readonly MapNode[,] Grid; // [floor, column]、未使用マスは null
        public MapNode Boss;

        public MapGraph(int width, int height)
        {
            Width = width;
            Height = height;
            Grid = new MapNode[height, width];
        }

        public MapNode Get(int floor, int column)
        {
            if (floor < 0 || floor >= Height || column < 0 || column >= Width) return null;
            return Grid[floor, column];
        }

        public MapNode GetOrCreate(int floor, int column)
        {
            var node = Get(floor, column);
            if (node == null)
            {
                node = new MapNode(floor, column);
                Grid[floor, column] = node;
            }
            return node;
        }

        /// <summary>ボスを除く全ノード。</summary>
        public IEnumerable<MapNode> AllNodes()
        {
            for (int f = 0; f < Height; f++)
                for (int c = 0; c < Width; c++)
                    if (Grid[f, c] != null)
                        yield return Grid[f, c];
        }

        /// <summary>ボスを含む全ノード。</summary>
        public IEnumerable<MapNode> AllNodesWithBoss()
        {
            foreach (var n in AllNodes()) yield return n;
            if (Boss != null) yield return Boss;
        }

        public List<MapNode> FloorNodes(int floor)
        {
            var list = new List<MapNode>();
            for (int c = 0; c < Width; c++)
                if (Get(floor, c) != null)
                    list.Add(Grid[floor, c]);
            return list;
        }
    }
}
