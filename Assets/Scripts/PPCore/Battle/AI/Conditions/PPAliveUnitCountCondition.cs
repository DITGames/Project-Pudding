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
    /// <summary>
    /// 生存数を数える対象。
    /// </summary>
    public enum PPAliveUnitCountConditionType
    {
        /// <summary>味方の生存数。</summary>
        [InspectorName("味方")]
        Ally,
        /// <summary>敵の生存数。</summary>
        [InspectorName("敵")]
        Enemy,
        /// <summary>敵味方を合わせた生存数。</summary>
        [InspectorName("全体")]
        All,
    }

    /// <summary>
    /// パーティ状況条件: 生存ユニット数。
    /// 「味方が 1 体になったら総攻撃」「敵が残り 1 体なら回復より攻撃」といった
    /// 頭数に基づく戦術の切り替えに使う。人数は整数のため厳密に比較する。
    /// </summary>
    [PPConditionMenu("パーティ状態/ユニット生存数", "Party/AliveUnitCount")]
    [CreateAssetMenu(fileName = "PPAllyAliveCountCondition",
        menuName = "Project-Pudding/AI/Conditions/ユニット生存数")]
    public sealed class PPAliveUnitCountCondition : PPPartyConditionValidator
    {
        /// <summary>数える対象（味方・敵・全体）。</summary>
        [Label("対象")] public PPAliveUnitCountConditionType ConditionType = PPAliveUnitCountConditionType.Ally;
        /// <summary>比較演算子。</summary>
        [Label("比較")] public PPCompareOp Op = PPCompareOp.GreaterOrEqual;
        /// <summary>閾値となるユニット数。</summary>
        [Label("ユニット数")] public int Threshold = 2;

        /// <summary>
        /// 対象の生存数を閾値と比較する。
        /// スナップショットが保持する生存リストの件数を使うため、思考中に数が変わることはない。
        /// </summary>
        /// <param name="aSnapShot">評価対象のパーティ状況スナップショット。</param>
        /// <returns>条件を満たす場合 true。</returns>
        public override bool Evaluate(PPPartyAIContext aSnapShot)
        => ConditionType switch
        {
            PPAliveUnitCountConditionType.Ally => PPConditionMath.Compare(aSnapShot.AliveMembers.Count, Op, Threshold),
            PPAliveUnitCountConditionType.Enemy => PPConditionMath.Compare(aSnapShot.AliveEnemies.Count, Op, Threshold),
            PPAliveUnitCountConditionType.All => PPConditionMath.Compare(aSnapShot.AliveMembers.Count + aSnapShot.AliveEnemies.Count, Op, Threshold),
            _ => false
        };

        /// <summary>設定内容から説明文を組み立てる。</summary>
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            var tgt = GetTargetString();
            var op = GetOpString(Op);
            var num = Threshold + "体";
            mDescription = tgt + num + op;
        }

        /// <summary>対象種別を説明文用の日本語へ変換する。</summary>
        private string GetTargetString()
            => ConditionType switch
            {
                PPAliveUnitCountConditionType.Ally => "味方の生存ユニット数が",
                PPAliveUnitCountConditionType.Enemy => "敵の生存ユニット数が",
                PPAliveUnitCountConditionType.All => "全体の生存ユニット数が",
                _ => ""
            };

        /// <summary>
        /// 説明文の語尾を調整する。
        /// 「〇体」で終わる文なので、等値のときは語尾を付けない方が自然になる。
        /// </summary>
        /// <param name="aOp">比較演算子。</param>
        protected override string GetOpString(PPCompareOp aOp)
            => aOp switch
            {
                PPCompareOp.Equal => "",
                PPCompareOp.NotEqual => "ではない",
                _ => base.GetOpString(aOp),
            };
    }
}
