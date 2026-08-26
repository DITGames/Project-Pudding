/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAISelectorNode.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief 子ノードを上から順に試す選択ノード
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // 子ノードを上から順に評価し、最初に行動が確定したものを採用するノード
    // 判断ツリーの背骨にあたる。並び順がそのまま優先度になる
    // どの子も確定しなければ自身も不成立となり、さらに上の階層へ戻る
    //
    // 待機コミット中は「待ちを宣言した枝より下」を評価しない
    // 溜めると決めたのに次のティックで下位の枝へ流れてしまうと、待ちが成立しないため
    // 逆に、宣言した枝より上（＝優先度が高い）はそのまま評価されるので、
    // 緊急度の高い行動を上に置いておけば自然に割り込める
    [Serializable]
    [PPTypeMenuName("制御/優先度リスト")]
    public sealed class PPUnitAISelectorNode : PPUnitAINode
    {
        // 接続している子ノードの ID。並び順がそのまま優先度になる
        [Header("子ノード")]
        [Label("上から順に試す", true)]
        [SerializeField] private List<string> mChildIds = new();

        protected override string DefaultNodeName => "優先度リスト";

        public override IReadOnlyList<PPUnitAINodePort> Ports
            => new[] { new PPUnitAINodePort("子ノード", mChildIds, true) };

        // 子を上から評価し、最初に確定した結果をそのまま返す
        // 確定した場合は道順（PPUnitAIEvalContext.Path）に自分の子の添字を残す
        // aContext : 評価 1 回分の入力
        // return : 確定した行動。どの子も確定しなければ Failed
        public override PPUnitAINodeResult Evaluate(PPUnitAIEvalContext aContext)
        {
            // 自分が木のどの深さに居るかは、ここまでに積まれた道順の長さでわかる
            int depth = aContext.Path.Count;
            bool isConstrained = aContext.IsOnCommitPath(depth);
            int limit = isConstrained ? aContext.CommitChildIndex(depth) : int.MaxValue;

            for (int i = 0; i < mChildIds.Count; i++)
            {
                var child = aContext.ResolveNode(mChildIds[i]);
                if (child == null) continue;
                // コミット中は、待ちを宣言した枝より下は見ない（割り込み指定のある枝だけは例外）
                if (i > limit && !child.IsInterrupt) continue;

                aContext.Path.Add(i);
                var result = child.Evaluate(aContext);
                if (result.IsDecided) return result;

                // 確定しなかった枝の道順は残さない
                aContext.Path.RemoveRange(depth, aContext.Path.Count - depth);
            }
            return PPUnitAINodeResult.Failed;
        }

        // 子ノードを末尾へ繋ぐ。既に繋がっている場合は何もしない
        // aPortIndex : 接続口の番号。このノードは 1 口のみ
        // aChildId : 繋ぐ子ノードの ID
        public override void ConnectChild(int aPortIndex, string aChildId)
        {
            if (string.IsNullOrEmpty(aChildId) || mChildIds.Contains(aChildId)) return;

            mChildIds.Add(aChildId);
        }

        // 子ノードとの接続を外す
        // aPortIndex : 接続口の番号
        // aChildId : 外す子ノードの ID
        public override void DisconnectChild(int aPortIndex, string aChildId) => mChildIds.Remove(aChildId);

        // 子ノードの並び順を指定どおりに揃える
        // エディタ上の配置（上にあるものほど優先）をそのまま優先度へ反映するために使う
        // aPortIndex : 接続口の番号
        // aOrderedChildIds : 並べ替え後の子ノード ID
        public override void ReorderChildren(int aPortIndex, IReadOnlyList<string> aOrderedChildIds)
        {
            mChildIds.Clear();
            foreach (var id in aOrderedChildIds)
            {
                if (!string.IsNullOrEmpty(id)) mChildIds.Add(id);
            }
        }
    }
}
