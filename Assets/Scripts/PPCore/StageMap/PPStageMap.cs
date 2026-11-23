/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageMap.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ステージマップの生成
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;
using CustomConsole;

namespace PPCore
{
    // マスの種類。アセットへ数値で保存されるため、値は明示的に振り、追加するときは末尾へ足す
    public enum PPStageNodeType
    {
        [InspectorName("通常戦")]
        Battle = 0,
        [InspectorName("エリート")]
        Elite = 1,
        [InspectorName("ボス")]
        Boss = 2,
        [InspectorName("強化")]
        Upgrade = 3,
        [InspectorName("ユニット獲得")]
        Recruit = 4,
        // 最下段の始点。ステージを始めた時点で通過済みとして扱い、イベントは起きない
        [InspectorName("スタート")]
        Start = 5,
    }

    // マップ上の 1 マス。前後のマスへの参照を持つ有向グラフのノード
    public class PPStageMapNode
    {
        /* 状態 */

        // 最下段を 0 とした段と、左から数えた列。ボスは段数と同じ段に置く
        public readonly Vector2Int Key;
        public readonly List<PPStageMapNode> Next = new();
        public readonly List<PPStageMapNode> Prev = new();

        public PPStageNodeType Type;
        // 通常戦・エリートのマスの遭遇。ボスとバトル以外のマスでは null
        public PPEncounterDefinition Encounter;
        // バトルのマスで戦う敵。通常戦・エリートでは遭遇の敵パーティ、ボスではボスの定義。バトル以外のマスでは null
        public PPPartyDefinition Enemy;
        // 表示位置のゆらぎ。-1〜1 で持ち、表示側が間隔を掛ける
        public Vector2 Jitter;

        /* プロパティ */

        public int Floor => Key.x;
        public int Column => Key.y;
        public bool IsBattle => Type is PPStageNodeType.Battle or PPStageNodeType.Elite or PPStageNodeType.Boss;

        /* 生成 */

        public PPStageMapNode(int aFloor, int aColumn)
        {
            Key = new Vector2Int(aFloor, aColumn);
        }

        /* 操作 */

        public void Connect(PPStageMapNode aTo)
        {
            if (aTo == null || Next.Contains(aTo)) return;
            Next.Add(aTo);
            aTo.Prev.Add(this);
        }
    }

    // 生成されたマップ全体。マスは [段, 列] の格子に置き、使わない格子は null
    public class PPStageMap
    {
        /* 状態 */

        public readonly int Width;
        // ボスの段を含まない段数
        public readonly int Height;
        public PPStageMapNode Boss;
        // 最下段の始点の 1 マス
        public PPStageMapNode Start;

        private readonly PPStageMapNode[,] mGrid;

        /* 生成 */

        public PPStageMap(int aWidth, int aHeight)
        {
            Width = aWidth;
            Height = aHeight;
            mGrid = new PPStageMapNode[aHeight, aWidth];
        }

        /* 問い合わせ */

        // 段と列からマスを返す。ボスの段も引ける。無ければ null
        public PPStageMapNode Get(Vector2Int aKey)
        {
            if (Boss != null && aKey == Boss.Key) return Boss;
            if (aKey.x < 0 || aKey.x >= Height || aKey.y < 0 || aKey.y >= Width) return null;
            return mGrid[aKey.x, aKey.y];
        }

        public PPStageMapNode GetOrCreate(int aFloor, int aColumn)
        {
            var node = Get(new Vector2Int(aFloor, aColumn));
            if (node != null) return node;

            node = new PPStageMapNode(aFloor, aColumn);
            mGrid[aFloor, aColumn] = node;
            return node;
        }

        // ボスを除く全マスを、下の段から左の列の順に返す
        public IEnumerable<PPStageMapNode> AllNodes()
        {
            for (int f = 0; f < Height; f++)
            {
                for (int c = 0; c < Width; c++)
                {
                    if (mGrid[f, c] != null) yield return mGrid[f, c];
                }
            }
        }

        public IEnumerable<PPStageMapNode> AllNodesWithBoss()
        {
            foreach (var node in AllNodes()) yield return node;
            if (Boss != null) yield return Boss;
        }

        public List<PPStageMapNode> FloorNodes(int aFloor)
        {
            var list = new List<PPStageMapNode>();
            for (int c = 0; c < Width; c++)
            {
                var node = Get(new Vector2Int(aFloor, c));
                if (node != null) list.Add(node);
            }
            return list;
        }
    }

    // マップ定義とシードからマップを作る。同じ定義とシードからは同じマップになる
    // 経路は最下段の 1 マスから上へ交差しないように引き、最上段からボスの 1 マスへ集める。マスの種類と敵は段ごとの規則と重みで決める
    public static class PPStageMapGenerator
    {
        /* 定数 */

        private const string LogTag = "StageMap";
        // 規則に合う種類が引けなかったときに引き直す回数
        private const int PickAttemptCount = 30;

        /* 生成 */

        public static PPStageMap Generate(PPStageMapDefinition aDefinition, int aSeed)
        {
            var random = new System.Random(aSeed);
            var map = new PPStageMap(aDefinition.Width, aDefinition.Height);

            // すべての経路を最下段の中央の 1 マスから始め、そこから枝分かれさせる
            int startColumn = (map.Width - 1) / 2;
            for (int i = 0; i < aDefinition.PathCount; i++) CreatePath(map, startColumn, random);
            map.Start = map.Get(new Vector2Int(0, startColumn));

            AssignTypes(map, aDefinition, random);
            CreateBoss(map, aDefinition);

            foreach (var node in map.AllNodes())
            {
                node.Jitter = new Vector2((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f);
            }
            return map;
        }

        /* 経路 */

        private static void CreatePath(PPStageMap aMap, int aStartColumn, System.Random aRandom)
        {
            int x = aStartColumn;
            var current = aMap.GetOrCreate(0, x);

            for (int floor = 0; floor < aMap.Height - 1; floor++)
            {
                int nextX = ChooseNextColumn(aMap, floor, x, aRandom);
                var next = aMap.GetOrCreate(floor + 1, nextX);
                current.Connect(next);
                current = next;
                x = nextX;
            }
        }

        // 真上と斜め上から、既存の経路と交差しない列を選ぶ。選べなければ真上へ進む
        private static int ChooseNextColumn(PPStageMap aMap, int aFloor, int aX, System.Random aRandom)
        {
            var candidates = new List<int>(3);
            for (int d = -1; d <= 1; d++)
            {
                int nx = aX + d;
                if (nx < 0 || nx >= aMap.Width) continue;
                if (WouldCross(aMap, aFloor, aX, nx)) continue;
                candidates.Add(nx);
            }
            if (candidates.Count == 0) return aX;
            return candidates[aRandom.Next(candidates.Count)];
        }

        // (段, x) → (段+1, nx) は、隣の (段, nx) → (段+1, x) と必ず交差するので弾く
        private static bool WouldCross(PPStageMap aMap, int aFloor, int aX, int aNx)
        {
            if (aNx == aX) return false;
            var neighbor = aMap.Get(new Vector2Int(aFloor, aNx));
            if (neighbor == null) return false;

            foreach (var next in neighbor.Next)
            {
                if (next.Column == aX) return true;
            }
            return false;
        }

        /* マスの種類 */

        // 最下段はスタート、決め打ちの段はその種類、それ以外は重みで引く。バトルのマスには遭遇と敵も決める
        private static void AssignTypes(PPStageMap aMap, PPStageMapDefinition aDefinition, System.Random aRandom)
        {
            WarnMismatchedEncounters(aDefinition);
            foreach (var node in aMap.AllNodes())
            {
                if (node.Floor == 0) node.Type = PPStageNodeType.Start;
                else if (node.Floor == aMap.Height - 1) node.Type = aDefinition.TopFloorType;
                else if (node.Floor == aDefinition.RecruitFloor) node.Type = PPStageNodeType.Recruit;
                else node.Type = PickType(aMap, node, aDefinition, aRandom);

                if (node.Type == PPStageNodeType.Elite)
                {
                    node.Encounter = PickEncounter(aDefinition.EliteEncounters, node.Floor, aRandom);
                    // エリートの候補が無い段では通常戦にする
                    if (node.Encounter == null) node.Type = PPStageNodeType.Battle;
                }
                if (node.Type == PPStageNodeType.Battle)
                {
                    node.Encounter = PickEncounter(aDefinition.NormalEncounters, node.Floor, aRandom);
                    if (node.Encounter == null)
                    {
                        CustomConsoleLog.Error(LogTag, $"{node.Floor} 段目に出せる通常敵がありません。マップ定義 {aDefinition.name} の「通常敵」に、この段を含む遭遇を足してください");
                    }
                }
                node.Enemy = node.Encounter != null ? node.Encounter.Party : null;
            }
        }

        // 通常敵の欄にエリートの遭遇が、エリートの欄にエリートでない遭遇が置かれていたら警告する。候補からは外さない
        private static void WarnMismatchedEncounters(PPStageMapDefinition aDefinition)
        {
            WarnMismatchedEncounters(aDefinition, aDefinition.NormalEncounters, false, "通常敵");
            WarnMismatchedEncounters(aDefinition, aDefinition.EliteEncounters, true, "エリート");
        }

        private static void WarnMismatchedEncounters(PPStageMapDefinition aDefinition, IReadOnlyList<PPStageEncounterEntry> aEntries, bool aIsElite, string aListName)
        {
            foreach (var entry in aEntries)
            {
                if (entry == null || entry.Encounter == null || entry.Encounter.IsElite == aIsElite) continue;
                CustomConsoleLog.Warning(LogTag, $"マップ定義 {aDefinition.name} の「{aListName}」にある遭遇 {entry.Encounter.name} は、エリートの設定が{(aIsElite ? "オフ" : "オン")}です。遭遇の「エリート」か、置く欄を直してください");
            }
        }

        // 重みで種類を引き、序盤の特殊なマス・同じ経路での連続・同じ段の隣り合わせを避ける
        private static PPStageNodeType PickType(PPStageMap aMap, PPStageMapNode aNode, PPStageMapDefinition aDefinition, System.Random aRandom)
        {
            for (int attempt = 0; attempt < PickAttemptCount; attempt++)
            {
                var type = Roll(aDefinition, aRandom);
                if (!IsSpecial(type)) return type;

                if (aNode.Floor < aDefinition.SpecialMinFloor) continue;
                // 最上段と同じ種類を、その 1 つ下の段に置かない
                if (type == aDefinition.TopFloorType && aNode.Floor == aMap.Height - 2) continue;
                if (HasParentOfType(aNode, type)) continue;
                if (HasNeighborOfType(aMap, aNode, type)) continue;
                return type;
            }
            return PPStageNodeType.Battle;
        }

        private static PPStageNodeType Roll(PPStageMapDefinition aDefinition, System.Random aRandom)
        {
            float total = aDefinition.BattleWeight + aDefinition.EliteWeight + aDefinition.UpgradeWeight + aDefinition.RecruitWeight;
            if (total <= 0f) return PPStageNodeType.Battle;

            float v = (float)aRandom.NextDouble() * total;
            if ((v -= aDefinition.BattleWeight) < 0f) return PPStageNodeType.Battle;
            if ((v -= aDefinition.EliteWeight) < 0f) return PPStageNodeType.Elite;
            if ((v -= aDefinition.UpgradeWeight) < 0f) return PPStageNodeType.Upgrade;
            return PPStageNodeType.Recruit;
        }

        private static bool IsSpecial(PPStageNodeType aType) => aType != PPStageNodeType.Battle;

        private static bool HasParentOfType(PPStageMapNode aNode, PPStageNodeType aType)
        {
            foreach (var prev in aNode.Prev)
            {
                if (prev.Type == aType) return true;
            }
            return false;
        }

        // 同じ段の左右のマス。左から順に種類を決めるため、実際に効くのは左隣だけ
        private static bool HasNeighborOfType(PPStageMap aMap, PPStageMapNode aNode, PPStageNodeType aType)
        {
            for (int d = -1; d <= 1; d += 2)
            {
                var neighbor = aMap.Get(new Vector2Int(aNode.Floor, aNode.Column + d));
                if (neighbor != null && neighbor.Type == aType) return true;
            }
            return false;
        }

        // 段に出てくる候補から、重みで 1 つ選ぶ。候補が無ければ null
        private static PPEncounterDefinition PickEncounter(IReadOnlyList<PPStageEncounterEntry> aEntries, int aFloor, System.Random aRandom)
        {
            float total = 0f;
            foreach (var entry in aEntries)
            {
                if (entry != null && entry.IsAvailable(aFloor)) total += entry.Weight;
            }
            if (total <= 0f) return null;

            float v = (float)aRandom.NextDouble() * total;
            PPEncounterDefinition last = null;
            foreach (var entry in aEntries)
            {
                if (entry == null || !entry.IsAvailable(aFloor)) continue;
                last = entry.Encounter;
                if ((v -= entry.Weight) < 0f) return entry.Encounter;
            }
            // 小数の誤差で最後まで引けなかったときは、最後の候補にする
            return last;
        }

        /* ボス */

        // 最上段のすべてのマスから、中央の列に置いたボスへつなぐ
        private static void CreateBoss(PPStageMap aMap, PPStageMapDefinition aDefinition)
        {
            var boss = new PPStageMapNode(aMap.Height, (aMap.Width - 1) / 2)
            {
                Type = PPStageNodeType.Boss,
                Enemy = aDefinition.Boss,
            };
            aMap.Boss = boss;
            if (boss.Enemy == null)
            {
                CustomConsoleLog.Error(LogTag, $"マップ定義 {aDefinition.name} の「ボス」が未設定です。敵パーティ定義を割り当ててください");
            }

            foreach (var top in aMap.FloorNodes(aMap.Height - 1)) top.Connect(boss);
        }
    }
}
