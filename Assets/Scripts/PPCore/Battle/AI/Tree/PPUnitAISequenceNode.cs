/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAISequenceNode.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief 同じティックの中で子を順に消化する連携ノード
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // 同じティックの中で、子を上から順に 1 手ずつ消化していくノード
    //
    // 1 ティックに複数回行動できるユニットは、2 手目も根から評価し直すため同じ枝を再び引きうる
    // そのままでは「1 手目バフ → 2 手目攻撃」を意図して書けないため、
    // 一度採用した子を同じティックの中では飛ばす、という並び方をこのノードで表現する
    //
    // ティックが変われば先頭へ戻る。行動回数が 1 のユニットでは常に先頭の子だけが使われる
    // 子をすべて採用し終えた状態でまだ行動回数が残っている場合は不成立となり、親の次の候補へ処理が渡る
    //
    // 採用済みかどうかの記録はストラテジストが 1 回の思考分だけ持つ（仮押さえ台帳と同じ寿命）
    // ノードの評価がバトルの状態を変えない、という約束を守るため、
    // 採用の記録はここでは行わず、行動が確定した時点でストラテジストが書き込む
    [Serializable]
    [PPTypeMenuName("制御/連携")]
    public sealed class PPUnitAISequenceNode : PPUnitAINode
    {
        // 採用済みの記録に使うキーの接尾辞。ノード自身の通過記録と区別するために付ける
        private const string AdoptedKeySuffix = "#seq:";

        // 接続している子ノードの ID。並び順がそのまま消化する順番になる
        [Header("子ノード")]
        [Label("上から順に消化する", true)]
        [SerializeField] private List<string> mChildIds = new();

        protected override string DefaultNodeName => "連携";

        // 接続している子ノードの ID。エディタのサマリ表示から参照する
        public IReadOnlyList<string> ChildIds => mChildIds;

        public override IReadOnlyList<PPUnitAINodePort> Ports
            => new[] { new PPUnitAINodePort("子ノード", mChildIds, true) };

        // 何手ぶんの連携かを示す
        public override string Summary => $"{mChildIds.Count} 件を同じティック内で順に消化";

        // まだ採用していない子を上から順に評価し、最初に確定した結果を返す
        // aContext : 評価 1 回分の入力
        // return : 確定した行動。採用できる子が無ければ Failed
        protected override PPUnitAINodeResult EvaluateCore(PPUnitAIEvalContext aContext)
        {
            int depth = aContext.Path.Count;
            // 引き返した枝の通過記録を残さないため、子を試す前の長さを覚えておく
            int visitedMark = aContext.VisitedNodeIds.Count;

            for (int i = 0; i < mChildIds.Count; i++)
            {
                // この思考で既に採った枝は飛ばす。これが「連携」の本体になる
                if (aContext.IsAdopted(AdoptedKey(i))) continue;

                var child = aContext.ResolveNode(mChildIds[i]);
                if (child == null) continue;

                aContext.Path.Add(i);
                var result = child.Evaluate(aContext);
                if (result.IsDecided)
                {
                    // 確定した枝だけを採用済みとして積む。次の手ではここが飛ばされる
                    aContext.PushVisited(AdoptedKey(i));
                    return result;
                }

                aContext.Path.RemoveRange(depth, aContext.Path.Count - depth);
                aContext.TrimVisited(visitedMark);
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

        // 接続先の子ノード ID を対応表に従って置き換える
        // aMap : 対応表
        public override void RemapChildIds(IReadOnlyDictionary<string, string> aMap) => RemapChildIds(mChildIds, aMap);

        // 子ノードとの接続を外す
        // aPortIndex : 接続口の番号
        // aChildId : 外す子ノードの ID
        public override void DisconnectChild(int aPortIndex, string aChildId) => mChildIds.Remove(aChildId);

        // 子ノードの並び順を指定どおりに揃える
        // エディタ上の配置（上にあるものほど先に消化する）をそのまま順番へ反映するために使う
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

        // 指定した添字の枝が採用済みかを表すキー
        // aIndex : 対象の枝の添字
        // return : 採用済みの記録に使うキー
        private string AdoptedKey(int aIndex) => mNodeId + AdoptedKeySuffix + aIndex;
    }
}
