/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPWaveDefinition.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief ウェーブの定義(ノードの一覧と線の一覧・ウェーブノードが出す敵・定義の点検)
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ウェーブノードの中で 1 種類の敵を出す設定。回が始まってから「最初の待ち時間」後に 1 体目を出し、以降は間隔ごとに出す
    [Serializable]
    public class PPWaveSpawn
    {
        /* インスペクタ設定 */

        [Label("ユニット")]
        [SerializeField] private PPUnitDefinition mUnit;
        // マスの深さで割り振られたレベルは、ここに足される
        [Label("レベル")]
        [Min(1)]
        [SerializeField] private int mLevel = 1;
        // 0 なら、バトルが終わるまで出し続ける
        [Label("数(0 で無限)")]
        [Min(0)]
        [SerializeField] private int mCount = 5;
        [Label("間隔(秒)")]
        [Min(0.1f)]
        [SerializeField] private float mInterval = 3f;
        [Label("最初の待ち時間(秒)")]
        [Min(0f)]
        [SerializeField] private float mFirstDelay = 0f;

        /* プロパティ */

        public PPUnitDefinition Unit => mUnit;
        public int Level => Mathf.Max(1, mLevel);
        public int Count => Mathf.Max(0, mCount);
        public bool IsInfinite => mCount <= 0;
        public float Interval => Mathf.Max(0.1f, mInterval);
        public float FirstDelay => Mathf.Max(0f, mFirstDelay);

        /* 生成 */

        public PPWaveSpawn() { }

        public PPWaveSpawn(PPUnitDefinition aUnit, int aLevel, int aCount, float aInterval, float aFirstDelay = 0f)
        {
            mUnit = aUnit;
            mLevel = aLevel;
            mCount = aCount;
            mInterval = aInterval;
            mFirstDelay = aFirstDelay;
        }
    }

    // ノードの出口。線は、どのノードのどの出口から先へ進むかで持つ
    public enum PPWaveNodePort
    {
        // トリガーが起動したとき
        [InspectorName("起動したとき")]
        Fired,
        // ウェーブノードが回を始めたとき
        [InspectorName("出し始めたとき")]
        Started,
        [InspectorName("出し終えたとき")]
        Finished,
        [InspectorName("出した敵が全滅したとき")]
        Wiped,
        // 条件ノードの判定の結果
        [InspectorName("成り立つ")]
        True,
        [InspectorName("成り立たない")]
        False,
    }

    // ノードどうしをつなぐ線。出口のノードのある出口から、行き先のノードの入口へ進む
    [Serializable]
    public class PPWaveLink
    {
        /* インスペクタ設定 */

        [PPWaveNodeReference("出口のノード")]
        [SerializeField] private string mFromId;
        [Label("出口")]
        [SerializeField] private PPWaveNodePort mFromPort;
        [PPWaveNodeReference("行き先のノード")]
        [SerializeField] private string mToId;

        /* プロパティ */

        public string FromId => mFromId;
        public PPWaveNodePort FromPort => mFromPort;
        public string ToId => mToId;

        /* 生成 */

        public PPWaveLink() { }

        public PPWaveLink(string aFromId, PPWaveNodePort aFromPort, string aToId)
        {
            mFromId = aFromId;
            mFromPort = aFromPort;
            mToId = aToId;
        }
    }

    // バトル定義が持つウェーブ。ノードの一覧と、ノードどうしをつなぐ線の一覧で敵の出し方を組む
    [Serializable]
    public class PPWaveDefinition
    {
        /* インスペクタ設定 */

        [SerializeReference]
        [Label("ノード", true)]
        private List<PPWaveNode> mNodes = new();
        [Label("線", true)]
        [SerializeField] private List<PPWaveLink> mLinks = new();

        /* プロパティ */

        public IReadOnlyList<PPWaveNode> Nodes => mNodes;
        public IReadOnlyList<PPWaveLink> Links => mLinks;

        /* static 関数 */

        // 出口の表示名
        public static string GetPortName(PPWaveNodePort aPort) => aPort switch
        {
            PPWaveNodePort.Fired => "起動したとき",
            PPWaveNodePort.Started => "出し始めたとき",
            PPWaveNodePort.Finished => "出し終えたとき",
            PPWaveNodePort.Wiped => "出した敵が全滅したとき",
            PPWaveNodePort.True => "成り立つ",
            PPWaveNodePort.False => "成り立たない",
            _ => aPort.ToString(),
        };

        /* 生成 */

        public PPWaveDefinition() { }

        public PPWaveDefinition(IEnumerable<PPWaveNode> aNodes, IEnumerable<PPWaveLink> aLinks)
        {
            mNodes = new List<PPWaveNode>(aNodes);
            mLinks = new List<PPWaveLink>(aLinks);
        }

        /* 問い合わせ */

        // 識別子のノード。見つからなければ null
        public PPWaveNode FindNode(string aId)
        {
            if (string.IsNullOrEmpty(aId)) return null;
            foreach (var node in mNodes)
            {
                if (node != null && node.Id == aId) return node;
            }
            return null;
        }

        // 識別子のノードへ入ってくる線があるか
        public bool HasIncomingLinks(string aId)
        {
            foreach (var link in mLinks)
            {
                if (link != null && link.ToId == aId) return true;
            }
            return false;
        }

        // 出てくるユニットの種類。同じユニットは 1 度だけ並べる
        public List<PPUnitDefinition> CollectUnits()
        {
            var units = new List<PPUnitDefinition>();
            foreach (var node in mNodes)
            {
                if (node is not PPWaveSpawnNode spawnNode) continue;
                foreach (var spawn in spawnNode.Spawns)
                {
                    if (spawn?.Unit != null && !units.Contains(spawn.Unit)) units.Add(spawn.Unit);
                }
            }
            return units;
        }

        // その種類のユニットを出す設定のうち、最も高いレベル。出さなければ 0
        public int GetMaxLevel(PPUnitDefinition aUnit)
        {
            int level = 0;
            foreach (var node in mNodes)
            {
                if (node is not PPWaveSpawnNode spawnNode) continue;
                foreach (var spawn in spawnNode.Spawns)
                {
                    if (spawn?.Unit == aUnit) level = Mathf.Max(level, spawn.Level);
                }
            }
            return level;
        }

        /* 評価・判定 */

        // 定義の誤りを文章にして aProblems に足す。値は直さない
        public void Validate(List<string> aProblems)
        {
            var ids = new HashSet<string>();
            bool hasTrigger = false;
            for (int i = 0; i < mNodes.Count; i++)
            {
                var node = mNodes[i];
                if (node == null)
                {
                    aProblems.Add($"ノード {i + 1} の種類が選ばれていません");
                    continue;
                }
                if (node is PPWaveTriggerNode) hasTrigger = true;
                if (string.IsNullOrEmpty(node.Id)) aProblems.Add($"ノード {i + 1}「{node.DisplayName}」の識別子が空です");
                else if (!ids.Add(node.Id)) aProblems.Add($"ノード {i + 1}「{node.DisplayName}」の識別子が他のノードと重なっています。インスペクタの「識別子を振り直す」で直してください");
                if (node is PPWaveWipedTriggerNode wiped && FindNode(wiped.TargetId) is not PPWaveSpawnNode)
                {
                    aProblems.Add($"トリガー「{node.DisplayName}」の対象のウェーブが見つかりません");
                }
            }
            if (mNodes.Count > 0 && !hasTrigger) aProblems.Add("トリガーノードが 1 つもないため、どのノードも動きません");

            for (int i = 0; i < mLinks.Count; i++)
            {
                var link = mLinks[i];
                if (link == null) continue;
                var from = FindNode(link.FromId);
                var to = FindNode(link.ToId);
                if (from == null || to == null)
                {
                    aProblems.Add($"線 {i + 1} の{(from == null ? "出口" : "行き先")}のノードが見つかりません");
                    continue;
                }
                if (!from.HasOutput(link.FromPort)) aProblems.Add($"線 {i + 1}: ノード「{from.DisplayName}」には出口「{GetPortName(link.FromPort)}」がありません");
                if (to is PPWaveTriggerNode trigger && trigger.IgnoresInput) aProblems.Add($"線 {i + 1}: ノード「{to.DisplayName}」は入口からの線を受け付けません");
                if (from is PPWaveSpawnNode spawnNode && spawnNode.IsInfinite && link.FromPort is PPWaveNodePort.Finished or PPWaveNodePort.Wiped)
                {
                    aProblems.Add($"線 {i + 1}: ウェーブ「{from.DisplayName}」は数が無限の敵を出すため、出し終えることも全滅することもありません");
                }
            }

            var looped = FindInstantLoopNode();
            if (looped != null) aProblems.Add($"ノード「{looped.DisplayName}」を通る輪に待ちがないため、1 フレームに 1 周ずつ回り続けます。時間などのトリガーを挟んでください");
        }

        // 待ちを含まない線(ウェーブの出し始め・条件の結果・0 秒の時間トリガーの起動)だけでできた輪にあるノードを 1 つ返す。無ければ null
        private PPWaveNode FindInstantLoopNode()
        {
            // 0 = 未訪問, 1 = たどっている途中, 2 = 調べ終えた
            var marks = new Dictionary<PPWaveNode, int>();
            foreach (var node in mNodes)
            {
                if (node == null || marks.ContainsKey(node)) continue;
                var found = VisitInstant(node, marks);
                if (found != null) return found;
            }
            return null;
        }

        private PPWaveNode VisitInstant(PPWaveNode aNode, Dictionary<PPWaveNode, int> aMarks)
        {
            aMarks[aNode] = 1;
            foreach (var link in mLinks)
            {
                if (link == null || link.FromId != aNode.Id || !IsInstantOutput(aNode, link.FromPort)) continue;
                var next = FindNode(link.ToId);
                if (next == null) continue;
                int mark = aMarks.GetValueOrDefault(next);
                if (mark == 1) return next;
                if (mark == 0)
                {
                    var found = VisitInstant(next, aMarks);
                    if (found != null) return found;
                }
            }
            aMarks[aNode] = 2;
            return null;
        }

        // 動かされたその場で先へ進む出口か
        private static bool IsInstantOutput(PPWaveNode aNode, PPWaveNodePort aPort) => aNode switch
        {
            PPWaveSpawnNode => aPort == PPWaveNodePort.Started,
            PPWaveConditionNode => true,
            PPWaveElapsedTriggerNode elapsed => elapsed.Seconds <= 0f,
            _ => false,
        };
    }
}
