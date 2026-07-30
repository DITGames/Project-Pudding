/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTurnCountCondition.cs
 * @author hqrse
 * @date 2026/07/21
 * @brief パーティ状況条件 : 経過ターン数
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// パーティ状況条件: バトル開始からの経過ターン数。
    /// 「序盤は溜めて中盤から攻める」のような、盤面ではなく進行度で切り替わる戦術に使う。
    /// ターン数は整数のため許容誤差を取らず厳密に比較する。
    /// </summary>
    [PPConditionMenu("進行/経過ターン数", "Progress/TurnCount")]
    [CreateAssetMenu(fileName = "PPTurnCountCondition",
        menuName = "Project-Pudding/AI/Conditions/経過ターン数")]
    public sealed class PPTurnCountCondition : PPPartyConditionValidator
    {
        /// <summary>比較演算子。</summary>
        [Label("比較")] public PPCompareOp Op = PPCompareOp.Equal;
        /// <summary>閾値となるターン数。</summary>
        [Label("ターン数")] public int Threshold = 3;

        /// <summary>
        /// 経過ターン数を閾値と比較する。
        /// </summary>
        /// <param name="aSnapShot">評価対象のパーティ状況スナップショット。</param>
        /// <returns>条件を満たす場合 true。</returns>
        public override bool Evaluate(PPPartyAIContext aSnapShot)
         => PPConditionMath.Compare(aSnapShot.Context.TurnCount, Op, Threshold);

        /// <summary>設定内容から説明文を組み立てる。</summary>
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            var prefix = $"経過ターン数が{Threshold}ターン";
            var op = GetOpString(Op);
            mDescription = prefix + op;
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
