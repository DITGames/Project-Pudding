/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPStatusEffectDefinition.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief ステータス異常のデータ定義
 * =====================================*/
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// 状態異常定義の抽象基底。
    /// <para>
    /// 生成の共通部分（種別・スタック設定・ターン制の持続条件）をここで組み立て、
    /// 毒ダメージのような固有の効果は派生側の <c>ConfigureEffect</c> に任せる。
    /// 派生の実装例は <see cref="PPPoisonEffectDefinition"/>。
    /// </para>
    /// </summary>
    public abstract class PPStatusEffectDefinition : PPEffectDefinition
    {
        /// <summary>状態異常の種別。解除スキルの対象判定に使われる。</summary>
        [Header("ステータスエフェクト")]
        [Label("カテゴリ")]
        [SerializeField]protected PPStatusEffectCategory mCategory = PPStatusEffectCategory.None;

        /// <summary>状態異常の種別。</summary>
        public PPStatusEffectCategory Category => mCategory;

        /// <summary>
        /// ターン経過で切れる状態異常のランタイム実体を生成する。
        /// 共通設定を反映したあと、派生側の設定処理を通して返す。
        /// </summary>
        /// <param name="aSource">エフェクトの付与元ユニット。</param>
        /// <param name="aTarget">付与される対象ユニット。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>生成されたステータスエフェクト。</returns>
        public override StatusEffect CreateRuntimeStatusEffect(BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            var effect = new PPStatusEffect(mEffectId, mDisplayName, new TurnDurationCondition(mDuration))
            {
                StackPolicy = mStackPolicy,
                MaxStacks = mMaxStack,
                Category = mCategory,
            };
            ConfigureEffect(effect, aSource, aTarget, aContext);
            return effect;
        }
    }
}
