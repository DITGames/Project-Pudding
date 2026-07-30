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
    // パーティ状況条件: バトル開始からの経過ターン数
    // 「序盤は溜めて中盤から攻める」のような、盤面ではなく進行度で切り替わる戦術に使う
    // ターン数は整数のため許容誤差を取らず厳密に比較する
    [PPConditionMenu("進行/経過ターン数", "Progress/TurnCount")]
    [CreateAssetMenu(fileName = "PPTurnCountCondition",
        menuName = "Project-Pudding/AI/Conditions/経過ターン数")]
    public sealed class PPTurnCountCondition : PPPartyConditionValidator
    {
        [Label("比較")] public PPCompareOp Op = PPCompareOp.Equal;
        [Label("ターン数")] public int Threshold = 3;

        // 経過ターン数を閾値と比較する
        // aSnapShot : 評価対象のパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPPartyAIContext aSnapShot)
         => PPConditionMath.Compare(aSnapShot.Context.TurnCount, Op, Threshold);

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            var prefix = $"経過ターン数が{Threshold}ターン";
            var op = GetOpString(Op);
            mDescription = prefix + op;
        }

        // 説明文の語尾を自然な日本語にするため、等値系のみ表記を差し替える
        // aOp : 比較演算子
        protected override string GetOpString(PPCompareOp aOp)
            => aOp switch
            {
                PPCompareOp.Equal => "と等しい",
                PPCompareOp.NotEqual => "と等しくない",
                _ => base.GetOpString(aOp)
            };
    }
}
