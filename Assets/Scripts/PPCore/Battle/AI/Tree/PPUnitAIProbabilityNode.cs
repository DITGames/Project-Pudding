/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAIProbabilityNode.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief 指定確率で子へ進む確率ゲートノード
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // 指定した確率でだけ子へ進むノード
    //
    // 抽選ノードが「複数の枝からどれか 1 つを引く」のに対して、こちらは 1 本の枝を通すかどうかだけを決める
    // 「たまに挑発してくる」「3 割の確率で溜める」のような、揺らぎを足すためのもの
    // 外れた場合は不成立となり、親の次の候補へ処理が渡る
    //
    // 乱数はシード管理・再現性のため、行動するユニット自身の供給元を経由する
    [Serializable]
    [PPTypeMenuName("制御/確率ゲート")]
    public sealed class PPUnitAIProbabilityNode : PPUnitAINode
    {
        [Header("確率")]
        // 子へ進む確率。0 なら決して通さず、1 なら常に通す
        [Label("通す確率")]
        [Range(0f, 1f)]
        [SerializeField] private float mProbability = 0.5f;

        [Header("枝")]
        [Label("通ったとき")]
        [SerializeField] private string mChildId = "";

        protected override string DefaultNodeName => "確率ゲート";

        // 子へ進む確率。エディタのサマリ表示から参照する
        public float Probability => mProbability;

        // 通す確率を百分率で示す
        public override string Summary => $"{Mathf.RoundToInt(mProbability * 100f)}% で通す";

        public override IReadOnlyList<PPUnitAINodePort> Ports
            => new[]
            {
                new PPUnitAINodePort("通過", string.IsNullOrEmpty(mChildId)
                    ? Array.Empty<string>()
                    : new[] { mChildId }, false),
            };

        // 抽選に通った場合だけ子を評価する
        // aContext : 評価 1 回分の入力
        // return : 子の評価結果。外れた場合と枝が無い場合は Failed
        protected override PPUnitAINodeResult EvaluateCore(PPUnitAIEvalContext aContext)
        {
            var child = aContext.ResolveNode(mChildId);
            if (child == null) return PPUnitAINodeResult.Failed;

            // 確率が振り切れている設定では乱数を消費しない
            // 消費するとシードの進み方が設定値に左右され、他の抽選の出目まで変わってしまうため
            if (mProbability <= 0f) return PPUnitAINodeResult.Failed;
            if (mProbability < 1f && aContext.Unit.ResolveRandom(aContext.Battle).NextFloat() >= mProbability)
                return PPUnitAINodeResult.Failed;

            return child.Evaluate(aContext);
        }

        // 指定した接続口へ子ノードを繋ぐ。既に繋がっていた場合は置き換える
        // aPortIndex : 接続口の番号。このノードは 1 口のみ
        // aChildId : 繋ぐ子ノードの ID
        public override void ConnectChild(int aPortIndex, string aChildId) => mChildId = aChildId;

        // 接続先の子ノード ID を対応表に従って置き換える
        // aMap : 対応表
        public override void RemapChildIds(IReadOnlyDictionary<string, string> aMap)
            => mChildId = RemapChildId(mChildId, aMap);

        // 指定した接続口の接続を外す
        // aPortIndex : 接続口の番号
        // aChildId : 外す子ノードの ID
        public override void DisconnectChild(int aPortIndex, string aChildId)
        {
            if (mChildId == aChildId) mChildId = "";
        }
    }
}
