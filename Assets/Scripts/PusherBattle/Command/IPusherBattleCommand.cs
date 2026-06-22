/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPusherBattleCommand.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief Pusherのコマンドインターフェース
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PusherBattle
{
    public interface IPusherBattleCommand
    {
        public int CoinCost { get; }
    }

    // Pusherの通常攻撃コマンドベース
    public class PusherBattleAttackCommand : AttackCommand, IPusherBattleCommand
    {
        public int CoinCost {get; private set;}
        
        public PusherBattleAttackCommand(BattleUnit aSource, ITargetResolver aResolver, int aCost)
            : base(aSource, aResolver)
        {
            CoinCost = aCost;
        }

        public override void Execute(BattleContext aContext)
        {
            var hitResolver = aContext.Rules.HitResolver;

            if (aContext.GetParty(Source.Side) is not PusherBattleParty party)
            {
                Debug.Log("パーティがプロジェクトと一致しません");
                return;
            }

            List<DamageInfo> damages = new();
            
            foreach (var target in aContext.ResolveTargets(Source, TargetResolver))
            {
                float raw = DamageFormula(Source, target);
                var damageInfo = new DamageInfo(Source, target, raw, DamageTags.Physical, this);

                var hitInfo = hitResolver.Resolve(Source, target, damageInfo, aContext);

                if (hitInfo == HitResult.Miss)
                {
                    damageInfo.IsMiss = true;
                }
                if(hitInfo == HitResult.Critical)
                {
                    damageInfo.IsCritical = true;
                    damageInfo.Amount *= aContext.Rules.CriticalMultiplier;
                }
                
                damages.Add(damageInfo);
            }

            // CastValidatorを通して実行可能かチェックされるが念のためコスト消費ができた場合のみ攻撃実行
            if (party.ResourcePool.TryConsumeCoin(CoinCost))
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
