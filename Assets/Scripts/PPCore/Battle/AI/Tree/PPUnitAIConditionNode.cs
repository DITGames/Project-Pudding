/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAIConditionNode.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief 条件を評価して枝を選ぶ分岐ノード
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // 条件を評価して、成立側・不成立側のどちらの枝へ進むかを決めるノード
    // ユニット条件とパーティ条件を AND で束ねて 1 つの問いにする
    // 不成立側の枝は繋がなくてよく、その場合は「この枝では行動が決まらない」扱いになり、
    // 親の優先度リストが次の候補へ進む（「N: 次の条件へ」の書き方になる）
    [Serializable]
    [PPTypeMenuName("制御/条件分岐")]
    public sealed class PPUnitAIConditionNode : PPUnitAINode
    {
        // 接続口の番号。エディタからの接続操作で使う
        private const int PortMatched = 0;
        private const int PortUnmatched = 1;

        [Header("条件")]
        [Label("ユニット条件", true)]
        [SerializeReference]
        [SerializeField] private List<PPUnitConditionValidator> mUnitConditions = new();
        [Label("パーティ条件", true)]
        [SerializeReference]
        [SerializeField] private List<PPPartyConditionValidator> mPartyConditions = new();
        // 条件の判定結果を反転する。「〜でない場合」を条件クラスを増やさずに書くためのもの
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 成立側・不成立側それぞれに繋がる子ノードの ID
        [Header("枝")]
        [Label("成立したとき")]
        [SerializeField] private string mMatchedId = "";
        [Label("成立しなかったとき")]
        [SerializeField] private string mUnmatchedId = "";

        protected override string DefaultNodeName => "条件分岐";

        public override IReadOnlyList<PPUnitAINodePort> Ports
            => new[]
            {
                new PPUnitAINodePort("成立", ToSingle(mMatchedId), false),
                new PPUnitAINodePort("不成立", ToSingle(mUnmatchedId), false),
            };

        // 条件を評価し、対応する枝へ進む
        // 枝が未接続の場合は不成立として扱い、親の次の候補へ処理を渡す
        // aContext : 評価 1 回分の入力
        // return : 進んだ枝の結果。枝が無ければ Failed
        public override PPUnitAINodeResult Evaluate(PPUnitAIEvalContext aContext)
        {
            bool isMatched = EvaluateConditions(aContext) != mIsInvert;
            var next = aContext.ResolveNode(isMatched ? mMatchedId : mUnmatchedId);

            return next == null ? PPUnitAINodeResult.Failed : next.Evaluate(aContext);
        }

        // 指定した接続口へ子ノードを繋ぐ。既に繋がっていた場合は置き換える
        // aPortIndex : 接続口の番号
        // aChildId : 繋ぐ子ノードの ID
        public override void ConnectChild(int aPortIndex, string aChildId)
        {
            if (aPortIndex == PortMatched) mMatchedId = aChildId;
            else if (aPortIndex == PortUnmatched) mUnmatchedId = aChildId;
        }

        // 指定した接続口の接続を外す
        // aPortIndex : 接続口の番号
        // aChildId : 外す子ノードの ID
        public override void DisconnectChild(int aPortIndex, string aChildId)
        {
            if (aPortIndex == PortMatched && mMatchedId == aChildId) mMatchedId = "";
            else if (aPortIndex == PortUnmatched && mUnmatchedId == aChildId) mUnmatchedId = "";
        }

        // ユニット条件とパーティ条件を AND で評価する
        // どちらも空なら「条件なし」とみなして成立する
        // aContext : 評価 1 回分の入力
        // return : 全ての条件を満たす場合 true
        private bool EvaluateConditions(PPUnitAIEvalContext aContext)
        {
            if (!PPUnitConditionValidator.EvaluateAll(mUnitConditions, aContext.Unit, aContext.Snapshot))
                return false;

            foreach (var condition in mPartyConditions)
            {
                if (condition == null) continue;
                if (!condition.Evaluate(aContext.Snapshot)) return false;
            }
            return true;
        }

        // 単一の接続先を接続口の形式へ揃える。未接続なら空の並びを返す
        // aChildId : 接続先の ID
        // return : 接続口が持つ子ノード ID の並び
        private static IReadOnlyList<string> ToSingle(string aChildId)
            => string.IsNullOrEmpty(aChildId) ? Array.Empty<string>() : new[] { aChildId };
    }
}
