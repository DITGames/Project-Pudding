/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPBattleCommand.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief コマンドインターフェース
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    public interface IPPBattleCommand
    {
        public PPResourceCost AttackCost { get; }
    }

    // Pusherの通常攻撃コマンドベース
    public class PPAttackCommand : AttackCommand, IPPBattleCommand
    {
        public PPResourceCost AttackCost {get; private set;}
        
        public PPAttackCommand(PPBattleUnit aSource, ITargetResolver aResolver)
            : base(aSource, aResolver)
        {
            // バフ・デバフ込みでの攻撃コストを適用(時間経過でバフ切れたときに消費できず失敗する可能性がありそう)
            AttackCost = PPResourceCost.BaseCost(aSource.ExtraParameters.Get(PPParameterSet.ParameterIdAttackCost).CurrentValue);
        }

        public override void Execute(BattleContext aContext)
        {
            if (aContext.GetParty(Source.Side) is not PPBattleParty party)
            {
                Debug.Log("パーティがプロジェクトと一致しません");
                return;
            }

            List<PPDamageInfo> damages = new();
            var sourceAttribute = PPDamageUtility.ResolveAttribute(Source);
            
            foreach (var target in aContext.ResolveTargets(Source, TargetResolver))
            {
                float raw = PPDamageUtility.ResolveAttackDamage(Source, target);
                var damageInfo = PPDamageUtility.CreateDamageInfo(Source, target, raw, PPSkillCategory.Physical, sourceAttribute, this, aContext);
                damages.Add(damageInfo);
            }

            // CastValidatorを通して実行可能かチェックされるが念のためコスト消費ができた場合のみ攻撃実行
            if (party.ResourcePool.TryPay(AttackCost))
            {
                foreach (var damageInfo in damages) damageInfo.Target?.ApplyDamage(damageInfo);
            }
            else
            {
                Debug.Log("コストの消費に失敗しました");
            }
        }
    }
}
