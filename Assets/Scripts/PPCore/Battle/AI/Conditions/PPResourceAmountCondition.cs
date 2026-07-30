/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPResourceAmountCondition.cs
 * @author hqrse
 * @date 2026/07/21
 * @brief パーティ状況条件 : 指定リソース残量(絶対値)
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// パーティ状況条件: 指定属性のリソース残量を絶対値で判定する。
    /// 「特定のスキルを撃つのに必要な量が溜まったか」のように、
    /// 具体的なコスト量と突き合わせたい場合はこちらを使う（割合で見るなら
    /// <see cref="PPResourceRatioCondition"/>）。
    /// </summary>
    [PPConditionMenu("リソース/残量(絶対値)", "Resources/Amount")]
    [CreateAssetMenu(fileName = "PPResourceAmountCondition",
        menuName = "Project-Pudding/AI/Conditions/リソース残量(絶対値)")]
    public sealed class PPResourceAmountCondition : PPPartyConditionValidator
    {
        /// <summary>判定対象の属性。</summary>
        [Label("対象リソース")] public PPTypeAttribute mTypeAttribute = PPTypeAttribute.Normal;
        /// <summary>比較演算子。</summary>
        [Label("比較")] public PPCompareOp Op = PPCompareOp.GreaterOrEqual;
        /// <summary>閾値となるリソース量。</summary>
        [Label("リソース量")] public float Threshold = 20f;
        /// <summary>等値判定の許容誤差。等値・非等値のときのみ表示される。</summary>
        [Label("許容値")] [EditCondition("IsEqualOp", true, false)]public float Tolerance = 1f;

        /// <summary>許容値の入力欄を出すかどうか（等値系の演算子でのみ意味を持つ）。</summary>
        private bool IsEqualOp
            => Op == PPCompareOp.Equal || Op == PPCompareOp.NotEqual;

        /// <summary>
        /// 対象リソースの現在値を閾値と比較する。
        /// </summary>
        /// <param name="aSnapShot">評価対象のパーティ状況スナップショット。</param>
        /// <returns>条件を満たす場合 true。</returns>
        public override bool Evaluate(PPPartyAIContext aSnapShot)
         => PPConditionMath.Compare(aSnapShot.Current(mTypeAttribute), Op, Threshold, Tolerance);

        /// <summary>設定内容から説明文を組み立てる。等値系のときは許容値も併記する。</summary>
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            var resource = GetResourceTypeString(mTypeAttribute) + $"リソースが{Threshold}";
            var op = GetOpString(Op);
            mDescription = resource + op;

            if (Op == PPCompareOp.Equal || Op == PPCompareOp.NotEqual)
            {
                mDescription += $" 許容値({Tolerance})";
            }
        }

        /// <summary>説明文の語尾を自然な日本語にするため、等値系のみ表記を差し替える。</summary>
        /// <param name="aOp">比較演算子。</param>
        protected override string GetOpString(PPCompareOp aOp)
            => aOp switch
            {
                PPCompareOp.Equal => "と等しい",
                PPCompareOp.NotEqual => "と等しくない",
                _ => base.GetOpString(aOp)
            };
    }
}
