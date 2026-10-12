/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPDamageScaleBehaviourDefinition.cs
 * @author hqrse
 * @date 2026/09/03
 * @brief 被ダメージを増減させる振る舞いのデータ定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 被ダメージを倍率で増減させる振る舞いの定義（防御姿勢・被ダメージ半減/無効など）
    [Serializable]
    [PPTypeMenuName("被ダメージ倍率")]
    public class PPDamageScaleBehaviourDefinition : PPStatusEffectBehaviourDefinition
    {
        [Header("被ダメージ倍率")]
        // 1で等倍、0.5で半減、0で無効化、1.5で1.5倍
        [Label("倍率")][Min(0)]
        [SerializeField]protected float mScale = 0.5f;
        [Label("適用順")]
        [SerializeField]protected int mOrder = 0;
        // 毒などの状態異常によるダメージには倍率を掛けない（被ダメージアップなど）
        // 既定は false で、防御姿勢などは従来どおりすべてのダメージに掛かる
        [Label("状態異常ダメージには掛けない")]
        [SerializeField]protected bool mIsExcludeStatusEffectDamage = false;

        // aEffect : 組み立て先のエフェクト
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        public override void ConfigureBehaviour(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new DamageScaleBehaviour(mScale, mOrder, mIsExcludeStatusEffectDamage));
        }

        public override string BuildString()
            => $"被ダメージ倍率：{mScale}倍{(mIsExcludeStatusEffectDamage ? "（状態異常ダメージを除く）" : string.Empty)}";
    }
}
