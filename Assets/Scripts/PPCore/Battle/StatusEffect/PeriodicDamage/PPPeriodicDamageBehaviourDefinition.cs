/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPeriodicDamageBehaviourDefinition.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief 毎ターン固定ダメージを与える振る舞いのデータ定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 毎ターン一定量のダメージを与える振る舞いの定義
    // ダメージ量が対象に依存しないため、雑魚敵にもボスにも同じだけ効く
    [Serializable]
    [PPTypeMenuName("継続ダメージ/固定")]
    public class PPPeriodicDamageBehaviourDefinition : PPStatusEffectBehaviourDefinition
    {
        [Header("固定継続ダメージ")]
        [Label("毎ターンダメージ量")][Min(0)]
        [SerializeField]protected float mDamagePerTurn = 5f;
        [Label("属性")]
        [SerializeField]protected PPTypeAttribute mAttribute = PPTypeAttribute.Normal;

        // aEffect : 組み立て先のエフェクト
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        public override void ConfigureBehaviour(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new PPPeriodicDamageBehaviour(mDamagePerTurn, mAttribute));
        }

        public override string BuildString()
            => $"継続ダメージ：{mDamagePerTurn}/ターン（{mAttribute}）";
    }
}
