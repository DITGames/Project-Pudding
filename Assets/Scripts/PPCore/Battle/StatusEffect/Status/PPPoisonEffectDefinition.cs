/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPoisonEffectDefinition.cs
 * @author hqrse
 * @date 2026/07/28
 * @brief 毎ターンダメージを与える毒のStatusEffect定義
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 毎ターン一定ダメージを与える毒の定義
    // パラメータを変化させるのではなく Tick でダメージを与えるタイプの状態異常の実装例
    [CreateAssetMenu(fileName = "PPPoisonEffectDefinition",
        menuName = "Project-Pudding/Effect/PPPoisonEffectDefinition")]
    public class PPPoisonEffectDefinition : PPStatusEffectDefinition
    {
        [Header("毒")]
        [Label("ダメージ量")]
        [SerializeField]protected float mDamagePerTurn = 5f;

        // ターン更新のたびに固定ダメージを与えるコールバックを仕込む
        // 付与元をダメージ情報に残すため、クロージャで aSource を捕捉している
        // aEffect : 設定対象のエフェクト
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        protected override void ConfigureEffect(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.OnTick = (unit, ctx) =>
            {
                var damageInfo = new PPDamageInfo(aSource, unit, mDamagePerTurn, PPSkillCategory.Debuff, PPTypeAttribute.Normal, this);
                unit.ApplyDamage(damageInfo);
            };
        }

        // ダメージ量と持続ターン数からエフェクト ID を組み立てる
        protected override string BuildAutoEffectId()
            => $"Poison_{mDamagePerTurn}_{mDuration}";
    }
}
