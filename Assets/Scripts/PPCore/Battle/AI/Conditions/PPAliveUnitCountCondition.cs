/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPAllyAliveCountCondition.cs
 * @author hqrse
 * @date 2026/07/21
 * @brief パーティ状況条件 : 味方ユニット生存数
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 生存数を数える対象
    public enum PPAliveUnitCountConditionType
    {
        // 味方の生存数
        [InspectorName("味方")]
        Ally,
        // 敵の生存数
        [InspectorName("敵")]
        Enemy,
        // 敵味方を合わせた生存数
        [InspectorName("全体")]
        All,
    }

    // パーティ状況条件: 生存ユニット数
    // 「味方が 1 体になったら総攻撃」「敵が残り 1 体なら回復より攻撃」といった
    // 頭数に基づく戦術の切り替えに使う。人数は整数のため厳密に比較する
    [PPConditionMenu("パーティ状態/ユニット生存数", "Party/AliveUnitCount")]
    [CreateAssetMenu(fileName = "PPAllyAliveCountCondition",
        menuName = "Project-Pudding/AI/Conditions/ユニット生存数")]
    public sealed class PPAliveUnitCountCondition : PPPartyConditionValidator
    {
        [Label("対象")] public PPAliveUnitCountConditionType ConditionType = PPAliveUnitCountConditionType.Ally;
        [Label("比較")] public PPCompareOp Op = PPCompareOp.GreaterOrEqual;
        [Label("ユニット数")] public int Threshold = 2;

        // 対象の生存数を閾値と比較する
        // スナップショットが保持する生存リストの件数を使うため、思考中に数が変わることはない
        // aSnapShot : 評価対象のパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPPartyAIContext aSnapShot)
        => ConditionType switch
        {
            PPAliveUnitCountConditionType.Ally => PPConditionMath.Compare(aSnapShot.AliveMembers.Count, Op, Threshold),
            PPAliveUnitCountConditionType.Enemy => PPConditionMath.Compare(aSnapShot.AliveEnemies.Count, Op, Threshold),
            PPAliveUnitCountConditionType.All => PPConditionMath.Compare(aSnapShot.AliveMembers.Count + aSnapShot.AliveEnemies.Count, Op, Threshold),
            _ => false
        };

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            var tgt = GetTargetString();
            var op = GetOpString(Op);
            var num = Threshold + "体";
            mDescription = tgt + num + op;
        }

        // 対象種別を説明文用の日本語へ変換する
        private string GetTargetString()
            => ConditionType switch
            {
                PPAliveUnitCountConditionType.Ally => "味方の生存ユニット数が",
                PPAliveUnitCountConditionType.Enemy => "敵の生存ユニット数が",
                PPAliveUnitCountConditionType.All => "全体の生存ユニット数が",
                _ => ""
            };

        // 説明文の語尾を調整する
        // 「〇体」で終わる文なので、等値のときは語尾を付けない方が自然になる
        // aOp : 比較演算子
        protected override string GetOpString(PPCompareOp aOp)
            => aOp switch
            {
                PPCompareOp.Equal => "",
                PPCompareOp.NotEqual => "ではない",
                _ => base.GetOpString(aOp),
            };
    }
}
