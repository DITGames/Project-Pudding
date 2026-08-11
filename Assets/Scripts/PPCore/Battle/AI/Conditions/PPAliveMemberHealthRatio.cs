/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPALiveMemverHealthRatio.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief パーティ状況条件 : ユニットのHP割合と対象ユニット数
 * =====================================*/

using System;
using CommandBattleCore;
using Unity.VisualScripting;
using UnityEngine;

namespace PPCore
{
    [Serializable]
    [PPTypeMenuName("パーティ状態/HP割合を満たすユニットが〇体")]
    public sealed class PPAliveMemberHealthRatio : PPPartyConditionValidator
    {
        [PercentLabel("HP割合")] public float HpThreshold = 0.5f;
        [Label("HP比較")] public PPCompareOp HpOp = PPCompareOp.GreaterOrEqual;
        [Label("許容値")][EditCondition(nameof(IsEqualOp), true, false)] public float Torelance = 0f;
        [Label("ユニット数")] public int UnitCount = 1;
        [Label("ユニット比較")] public PPCompareOp UnitOp = PPCompareOp.GreaterOrEqual;
        
        private bool IsEqualOp
            => HpOp == PPCompareOp.Equal || HpOp == PPCompareOp.NotEqual;

        // パーティの生存メンバー各ユニットに対して指定条件を満たしているか
        // aSnapShot : 評価対象のパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPPartyAIContext aSnapShot)
        {
            int count = 0;
            // HP条件を満たしているユニット数をカウント
            foreach (var unit in aSnapShot.AliveMembers)
            {
                if (PPConditionMath.Compare(unit.Parameters.Hp.Ratio, HpOp, HpThreshold, Torelance))
                {
                    count++;
                }
            }
            
            return PPConditionMath.Compare(count, UnitOp, UnitCount, 0);
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            var prefix = "HP割合が" + $"{HpThreshold * 100f:0.#}%";
            var op = GetOpString(HpOp);
            var unit = $"ユニットが{UnitCount}体" + GetOpString(UnitOp);
            mDescription = $"{prefix} {op} {unit}";
            if (IsEqualOp)
            {
                mDescription += $" HP許容値({Torelance * 100f:0.#}%)";
            }
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