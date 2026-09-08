/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPeriodicHealBehaviourDefinition.cs
 * @author hqrse
 * @date 2026/09/03
 * @brief 毎ターン回復させる振る舞いのデータ定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 毎ターン一定量を回復させる振る舞いの定義（リジェネなど）
    [Serializable]
    [PPTypeMenuName("継続回復")]
    public class PPPeriodicHealBehaviourDefinition : PPStatusEffectBehaviourDefinition
    {
        [Header("継続回復")]
        [Label("毎ターン回復量")]
        [SerializeField]protected float mHealPerTurn = 5f;

        // aEffect : 組み立て先のエフェクト
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        public override void ConfigureBehaviour(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new PeriodicHealBehaviour(mHealPerTurn));
        }

        public override string BuildString()
            => $"継続回復：{mHealPerTurn}/ターン";
    }
}
