/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPoisonEffectDefinition.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief 毎ターンダメージを与える毒のStatusEffect定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 毎ターン一定ダメージを与える毒の定義
    // パラメータを変化させるのではなく Tick でダメージを与えるタイプの状態異常の実装例
    [Serializable]
    [PPTypeMenuName("StatusEffect付与/毒")]
    public class PPPoisonEffectDefinition : PPEffectDefinition
    {
        [Header("毒")]
        [Label("毎ターンダメージ量")]
        [SerializeField]protected float mDamagePerTurn = 5f;
        [Label("属性")]
        [SerializeField]protected PPTypeAttribute mAttribute = PPTypeAttribute.Normal;

        public override PPEffectCategory Category => PPEffectCategory.Poison;
        public override StatusEffectTag Tags
            => StatusEffectTag.Ailment | StatusEffectTag.Debuff | StatusEffectTag.Periodic;

        // ターン更新のたびに固定ダメージを与える振る舞いを積む
        // aEffect : 設定対象のエフェクト
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        protected override void ConfigureBehaviours(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new PPPeriodicDamageBehaviour(mDamagePerTurn, mAttribute));
        }

        // ダメージ量と持続ターン数からエフェクト ID を組み立てる
        protected override string BuildAutoEffectId()
            => $"Poison_{mAttribute}_{mDamagePerTurn}_{mDuration}";

        public override string BuildString()
            => $"毒：{mDamagePerTurn}/ターン（{mAttribute}）";
    }
}
