/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAIProfileDefinition.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニット単位の判断ツリー
 * =====================================*/

using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ユニット 1 体分の判断ツリーを持つ ScriptableObject
    // PPUnitDefinition へアタッチし、そのユニットが毎ティック「何をするか」を決める
    //
    // 中身は条件と行動をつないだ木そのもので、スコアや重みのような間接的なパラメータは持たない
    // 「条件を満たしたら即実行、実行できなければ次の候補へ」だけで動くため、
    // アセットを読めばそのユニットの振る舞いがそのまま読める
    //
    // ノードは入れ子ではなくフラットなリストで保持し、親子関係はノード ID の参照で表す
    // ノードエディタ（Window > Unit AI Tree）で「作ってから繋ぐ」「一旦切り離す」を成立させるための構成で、
    // 評価時は根から ID を辿って木として読む
    [CreateAssetMenu(fileName = "PPUnitAIProfileDefinition", menuName = "Project-Pudding/AI/PPUnitAIProfileDefinition")]
    public class PPUnitAIProfileDefinition : ScriptableObject
    {
        [Header("表示")]
        [Label("説明")]
        [SerializeField][Multiline] protected string mDescription = "";

        [Header("判断ツリー")]
        // 評価を開始するノードの ID。ノードエディタから設定する
        [Label("ルートノードID")]
        [SerializeField] protected string mRootNodeId = "";
        // ツリーを構成する全ノード。並び順に意味は無く、接続は ID の参照で表す
        [Label("ノード", true)]
        [SerializeReference]
        [SerializeField] protected List<PPUnitAINode> mNodes = new();

        // ID からノードを引くための索引。初回アクセス時に組み立てる
        private Dictionary<string, PPUnitAINode> mNodeMap;

        public string Description => mDescription;
        public string RootNodeId => mRootNodeId;
        public IReadOnlyList<PPUnitAINode> Nodes => mNodes;

        // 評価を開始するノード。未設定・見つからない場合は null
        public PPUnitAINode Root => FindNode(mRootNodeId);

        // ID からノードを引く
        // aNodeId : 引くノードの ID
        // return : 該当ノード。未接続（ID が空）や見つからない場合は null
        public PPUnitAINode FindNode(string aNodeId)
        {
            if (string.IsNullOrEmpty(aNodeId)) return null;

            mNodeMap ??= BuildNodeMap();
            return mNodeMap.TryGetValue(aNodeId, out var node) ? node : null;
        }

        // ルートから木を評価して、このティックの行動を決める
        // aContext : 評価 1 回分の入力
        // return : 確定した行動。ルート未設定・どの枝も成立しない場合は Failed
        public virtual PPUnitAINodeResult Evaluate(PPUnitAIEvalContext aContext)
        {
            var root = Root;
            return root == null ? PPUnitAINodeResult.Failed : root.Evaluate(aContext);
        }

        // ID 索引を捨てて、次のアクセスで組み立て直させる
        // ノードエディタがノードを追加・削除した際に呼ぶ
        public void InvalidateNodeMap() => mNodeMap = null;

        // ID 未採番のノードへ ID を振る
        // 手でリストへ追加したノードや、古いアセットを開いた場合の取りこぼしを埋める
        public void EnsureNodeIds()
        {
            foreach (var node in mNodes)
            {
                node?.EnsureNodeId();
            }
            InvalidateNodeMap();
        }

        // ID からノードを引く索引を組み立てる
        // ID が重複している場合は先勝ちとし、後続は引けなくなる（エディタ側で採番するため通常は起きない）
        // return : 組み立てた索引
        private Dictionary<string, PPUnitAINode> BuildNodeMap()
        {
            var map = new Dictionary<string, PPUnitAINode>();
            foreach (var node in mNodes)
            {
                if (node == null || string.IsNullOrEmpty(node.NodeId)) continue;

                map.TryAdd(node.NodeId, node);
            }
            return map;
        }

        // アセットの読み込み・インスペクタ編集のたびに索引を捨てる
        // ノードを差し替えたのに古い索引を引き続けるのを防ぐ
        protected virtual void OnValidate() => InvalidateNodeMap();
    }
}
