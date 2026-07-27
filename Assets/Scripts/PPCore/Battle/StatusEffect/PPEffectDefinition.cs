/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPEffectDefinition.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief PPCore固有のエフェクトデータ定義
 * =====================================*/
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    public abstract class PPEffectDefinition : ScriptableObject
    {
        [Header("エフェクト")]
        [Label("エフェクトID")]
        [SerializeField]protected string mEffectId;
        [Label("表示名")]
        [SerializeField]protected string mDisplayName;
        [Label("期間")]
        [SerializeField]protected int mDuration = 3;
        [Label("スタックポリシー")]
        [SerializeField]protected StatusEffectStackPolicy mStackPolicy = StatusEffectStackPolicy.Refresh;
        [Label("最大スタック")] protected int mMaxStack = 1;
        
        public string EffectId => mEffectId;
        public string DisplayName => mDisplayName;
        public int Duration => mDuration;
        public StatusEffectStackPolicy StackPolicy => mStackPolicy;
        
        public abstract StatusEffect CreateRuntimeStatusEffect(BattleUnit aSource, BattleUnit aTarget, BattleContext aContext);
        protected abstract void ConfigureEffect(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext);
    }
}