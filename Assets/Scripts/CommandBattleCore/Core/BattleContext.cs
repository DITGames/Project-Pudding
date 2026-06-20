/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleContext.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトル開始のコンテキスト
 * =====================================*/
using System;
using System.Collections.Generic;

namespace CommandBattleCore
{
    // バトル報酬情報
    public class BattleReward
    {
        public int Experience { get; }
        public int Money { get; }
        public List<object> Items { get; } = new();
    }
    
    // バトル環境 拡張前提 
    public class BattleEnvironment
    {
        
    }
    
    // バトル開始コンテキスト
    public class BattleContext
    {
        // プレイヤーパーティ
        public BattleParty AllyParty { get; set; }
        // 敵パーティ
        public BattleParty EnemyParty { get; set; }
        // 報酬
        public BattleReward Reward { get; set; } = new();
        // 環境
        public BattleEnvironment Environment { get; set; } = new();
        // バトルルール
        public BattleRules Rules { get; set; } = new BattleRules();
        
        // 逃走フラグ DefaultBattleCheckerがチェックする
        public bool EscapeRequested { get; set; }
        
        public int TurnCount { get; set; }

        public event Action<BattleUnit, BattleSkill, CastFailReason> OnCastFailed;
        internal void NotifyCastFailed(BattleUnit aUnit, BattleSkill aSkill, CastFailReason aReason) 
            => OnCastFailed?.Invoke(aUnit, aSkill, aReason);

        public BattleParty GetParty(BattleSide aSide) =>
            aSide == BattleSide.Ally ? AllyParty : EnemyParty;
        
        public BattleParty GetOpponentParty(BattleSide aSide) =>
            aSide == BattleSide.Ally ? EnemyParty : AllyParty;

        public List<BattleUnit> ResolveTargets(BattleUnit aSource, ITargetResolver aTargetResolver)
        {
            var result = aTargetResolver.Resolve(aSource, this);
            foreach (var filter in Rules.TargetFilters)
                result = filter.Filter(aSource, result, this);
            return result;
        }
    }
}