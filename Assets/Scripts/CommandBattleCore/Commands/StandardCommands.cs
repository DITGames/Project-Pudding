/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file StandardCommands.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 基本のコマンド実装
 * =====================================*/
using System;
using UnityEngine;

namespace CommandBattleCore
{
    // 通常攻撃コマンド
    public class AttackCommand : BattleCommandBase
    {
        public static Func<BattleUnit, BattleUnit, float> DamageFormula { get; set; } =
            (src, tgt) =>
                Mathf.Max(1f, src.Parameters.Attack.CurrentValue - tgt.Parameters.Defense.CurrentValue * 0.5f);
        
        public AttackCommand(BattleUnit aSource, ITargetResolver aResolver) : base(aSource, aResolver) {}
        
        public override void Execute(BattleContext aContext)
        {
            var resolver = aContext.Rules.HitResolver;
            foreach (var target in aContext.ResolveTargets(Source, TargetResolver))
            {
                float raw = DamageFormula(Source, target);
                var info = new DamageInfo(Source, target, raw, DamageTags.Physical, this);
                
                var hit = resolver.Resolve(Source, target, info, aContext);

                if (hit == HitResult.Miss)
                {
                    info.IsMiss = true;
                    info.Amount = 0f;
                }
                if (hit == HitResult.Critical)
                {
                    info.IsCritical = true;
                    info.Amount *= aContext.Rules.CriticalMultiplier;
                }
                
                target.ApplyDamage(info);
            }
        }
    }

    // スキルコマンド
    // リソースの消費などはコアでは行わない
    public class SkillCommand : BattleCommandBase
    {
        public BattleSkill Skill { get; }

        public SkillCommand(BattleUnit aSource, BattleSkill aSkill, ITargetResolver aResolverOverride = null)
            : base(aSource, aResolverOverride ?? aSkill.DefaultTargetResolver)
        {
            Skill = aSkill;
        }

        public override void Execute(BattleContext aContext)
        {
            // 先にターゲット解決
            var targets = aContext.ResolveTargets(Source, TargetResolver);
            if (targets.Count == 0)
            {
                aContext.NotifyCastFailed(Source, Skill, CastFailReason.InvalidTarget);
                return;
            }
            
            // 発動可能?
            var validation = aContext.Rules.CastValidator.Validate(Source, Skill, aContext);
            if (!validation.CanCast)
            {
                aContext.NotifyCastFailed(Source, Skill, validation.Reason);
                return;
            }
            
            // プロジェクトに合わせてコストの消費
            
            // スキル実行
            Skill.Execute(Source, TargetResolver.Resolve(Source, aContext), aContext);
            Skill.NotifyUsed();
        }
    }

    public interface IItemEffect
    {
        void Use(BattleUnit aSource, System.Collections.Generic.List<BattleUnit> aTargets, BattleContext aContext);
    }

    // アイテム使用コマンド
    public class ItemCommand : BattleCommandBase
    {
        public IItemEffect Item { get; }

        public ItemCommand(BattleUnit aSource, IItemEffect aItem, ITargetResolver aResolver) : base(aSource, aResolver)
            => Item = aItem;

        public override void Execute(BattleContext aContext)
            => Item.Use(Source, TargetResolver.Resolve(Source, aContext), aContext);
    }

    public class SwapCommand : BattleCommandBase
    {
        public BattleUnit ReserveUnit { get; }

        public SwapCommand(BattleUnit aOutUnit, BattleUnit aInUnit) : base(aOutUnit, new SelfResolver())
        {
            ReserveUnit = aInUnit;
        }

        public override void Execute(BattleContext aContext)
        {
            if ((Source.CurrentRestrictions & ActionRestriction.CannotSwap) != 0) return;
            aContext.GetParty(Source.Side).SwapMember(Source, ReserveUnit);
        }
    }

    public class EscapeCommand : BattleCommandBase
    {
        public static Func<BattleUnit, BattleContext, bool> EscapeFormula { get; set; } = (_, _) => true;

        public EscapeCommand(BattleUnit aSource) : base(aSource, new SelfResolver()){}

        public override void Execute(BattleContext aContext)
        {
            if ((Source.CurrentRestrictions & ActionRestriction.CannotEscape) != 0) return;
            if (EscapeFormula(Source, aContext))
                aContext.EscapeRequested = true;
        }
    }
}