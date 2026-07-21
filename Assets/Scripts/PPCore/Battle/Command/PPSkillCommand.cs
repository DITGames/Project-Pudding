/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillCommand.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief リソース消費を行うスキルコマンド
 * =====================================*/
using CommandBattleCore;

namespace PPCore
{
    public class PPSkillCommand : SkillCommand
    {
        public PPSkillCommand(BattleUnit aSource, BattleSkill aSkill, ITargetResolver aResolverOverride = null)
            : base(aSource, aSkill, aResolverOverride)
        {
        }

        public override void Execute(BattleContext aContext)
        {
            var targets = aContext.ResolveTargets(Source, TargetResolver);
            if (targets.Count == 0)
            {
                aContext.NotifyCastFailed(Source, Skill, CastFailReason.InvalidTarget);
                return;
            }
            
            var validation = aContext.Rules.CastValidator.Validate(Source, Skill, aContext);
            if (!validation.CanCast)
            {
                aContext.NotifyCastFailed(Source, Skill, validation.Reason);
                return;
            }
            
            var cost = (Skill.SourceDefinition as PPSkillDefinition)?.Cost ?? PPResourceCost.Free;
            if (!cost.IsFree)
            {
                if (aContext.GetParty(Source.Side) is not PPBattleParty party ||
                    !party.ResourcePool.TryPay(cost))
                {
                    aContext.NotifyCastFailed(Source, Skill, CastFailReason.NotEnoughResource);
                    return;
                }
            }
            
            Skill.Execute(Source, targets, aContext);
            Skill.NotifyUsed();
        }
    }
}