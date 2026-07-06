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
        public float AttackCost { get; }
    }

    // Pusherの通常攻撃コマンドベース
    public class PPBattleAttackCommand : AttackCommand, IPPBattleCommand
    {
        public float AttackCost {get; private set;}
        
        public PPBattleAttackCommand(PPBattleUnit aSource, ITargetResolver aResolver)
            : base(aSource, aResolver)
        {
            // バフ・デバフ込みでの攻撃コストを適用(時間経過でバフ切れたときに消費できず失敗する可能性がありそう)
            AttackCost = aSource.PPParameters.Get(PPParameterSet.ParameterIdAttackCost).CurrentValue;
        }

        public override void Execute(BattleContext aContext)
        {
            if (aContext.GetParty(Source.Side) is not PPBattleParty party)
            {
                Debug.Log("パーティがプロジェクトと一致しません");
                return;
            }

            List<DamageInfo> damages = new();
            
            foreach (var target in aContext.ResolveTargets(Source, TargetResolver))
            {
                float raw = DamageFormula(Source, target);
                var damageInfo = new DamageInfo(Source, target, raw, DamageTags.Physical, this);

                var hitInfo = aContext.ResolveHit(Source, target, damageInfo);

                if (hitInfo.mResult == HitResult.Miss)
                {
                    damageInfo.IsMiss = true;
                }
                if(hitInfo.mCriticalInfo.IsCritical)
                {
                    damageInfo.IsCritical = true;
                    damageInfo.Amount *= hitInfo.mCriticalInfo.CriticalMultiplier;
                }
                
                damages.Add(damageInfo);
            }

            // CastValidatorを通して実行可能かチェックされるが念のためコスト消費ができた場合のみ攻撃実行
            if (party.ResourcePool.TryConsumeResource(AttackCost))
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
