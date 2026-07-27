/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleRules.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief 拡張バトルルール
 * =====================================*/
using CommandBattleCore;

namespace PPCore
{
    public class PPBattleRules : BattleRules
    {
        // AIの低HP判定閾値
        public float CrisisHpRatio = 0.25f;
        // 弱点属性ダメージ倍率
        public float WeaknessMultiplier = 1.5f;
        // 属性耐性ダメージ倍率
        public float ResistanceMultiplier = 0.75f;
    }
}