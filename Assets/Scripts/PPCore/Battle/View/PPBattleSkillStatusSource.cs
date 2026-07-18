/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleSkillStatusSource.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief バトルスキル情報アダプタ
 * =====================================*/
using System;
using CommandBattleCore;

namespace PPCore
{
    public class PPBattleSkillStatusSource : IPPSkillStatusSource
    {
        private readonly BattleSkill mSkill;
        private readonly BattleUnit mOwner;
        private readonly BattleContext mContext;
        private readonly PPBattleParty mParty;
        public event Action Changed;

        public string DisplayName => mSkill.DisplayName;
        public PPResourceCost Cost => (mSkill.SourceDefinition as PPSkillDefinition)?.Cost ?? PPResourceCost.Free;
        public int CooldownRemaining => mSkill.RemainingCooldown;
        public bool IsCastable => mContext.Rules.CastValidator.Validate(mOwner, mSkill, mContext).CanCast;

        public PPBattleSkillStatusSource(BattleSkill aSkill, BattleUnit aOwner, BattleContext aContext)
        {
            mSkill = aSkill;
            mOwner = aOwner;
            mContext = aContext;
            
            mParty = aContext.GetParty(aOwner.Side) as PPBattleParty;
            if (mParty != null)
            {
                foreach(var t in Cost.RelevantTypes())
                {
                    mParty.ResourcePool.Pool(t).OnValueChanged += HandleChanged;
                }
            }
        }
        
        private void HandleChanged(IReadableParameter _) => Changed?.Invoke();

        // メニュー破棄時に呼び出す(購読によるメモリリーク防止用)
        public void Dispose()
        {
            if (mParty != null)
            {
                foreach(var t in Cost.RelevantTypes())
                {
                    mParty.ResourcePool.Pool(t).OnValueChanged -= HandleChanged;
                }
            }
        }
        
    }
}