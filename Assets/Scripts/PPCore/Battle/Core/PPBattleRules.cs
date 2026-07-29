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
    /// <summary>
    /// Project-Pudding 固有のルール値を追加したバトルルール。
    /// <para>
    /// 基底の <see cref="BattleRules"/> が持つ差し替え可能な判定インターフェース群に加えて、
    /// 属性相性のダメージ倍率と AI の危機判定閾値という、本作固有の調整値を保持する。
    /// バトル組み立て時に <see cref="BattleContext.Rules"/> へこの型を差し込むことで有効になる。
    /// </para>
    /// </summary>
    public class PPBattleRules : BattleRules
    {
        /// <summary>AI の低HP判定閾値。HP 割合がこれを下回ると危機的状況として扱う。</summary>
        public float CrisisHpRatio = 0.25f;
        /// <summary>弱点属性でヒットしたときのダメージ倍率。</summary>
        public float WeaknessMultiplier = 1.5f;
        /// <summary>耐性属性でヒットしたときのダメージ倍率。</summary>
        public float ResistanceMultiplier = 0.75f;
    }
}
