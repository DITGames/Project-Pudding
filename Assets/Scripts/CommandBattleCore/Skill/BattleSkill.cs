/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleSkill.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief スキルのインスタンス
 * =====================================*/
using System;
using System.Collections.Generic;

namespace CommandBattleCore
{
    public class BattleSkill
    {
        // スキルID
        public string SkillId { get; }
        // UIへの表示名
        public string DisplayName { get; }
        // ターゲット解決インターフェース
        public ITargetResolver DefaultTargetResolver { get; }
        
        public Action<BattleUnit, List<BattleUnit>, BattleContext> Effect { get; }
        
        public BattleSkill(string aSkillId, string aDisplayName, ITargetResolver aDefaultResolver,
            Action<BattleUnit, List<BattleUnit>, BattleContext> aEffect)
        {
            SkillId = aSkillId;
            DisplayName = aDisplayName;
            DefaultTargetResolver = aDefaultResolver;
            Effect = aEffect;
        }
        
        // スキルのソースオブジェクト
        public object SourceDefinition { get; set; }

        public void Execute(BattleUnit aSource, List<BattleUnit> aTargets, BattleContext aContext) 
            => Effect?.Invoke(aSource, aTargets, aContext);

        // クールダウン
        public int MaxCooldown { get; set; } = 0; // クールダウンなし
        public int RemainingCooldown { get; protected internal set; } = 0;
        
        // 1戦闘あたりの最大使用可能回数
        public int MaxUsesPerBattle { get; set; } = 0; // 無制限
        public int UsesRemaining { get; protected internal set; } = 0;

        // クールダウンと使用回数でまとめて使用可能かチェック
        public bool IsReady =>
            RemainingCooldown <= 0 && (MaxUsesPerBattle == 0 || UsesRemaining > 0);
        
        public bool IsLimit => MaxUsesPerBattle > 0 && UsesRemaining <= MaxUsesPerBattle;
        
        public bool IsCooldown => RemainingCooldown > 0;

        public void ResetForBattle()
        {
            RemainingCooldown = 0;
            UsesRemaining = MaxUsesPerBattle;
        }

        public void NotifyUsed()
        {
            // 使用後にTick走るのでRemainingCooldownはMaxCooldown + 1にすべき
            if (MaxCooldown > 0) RemainingCooldown = MaxCooldown + 1;
            if (MaxUsesPerBattle > 0 && UsesRemaining > 0) UsesRemaining--;
        }

        public void TickCooldown()
        {
            if (RemainingCooldown > 0) RemainingCooldown--;
        }
    }
}