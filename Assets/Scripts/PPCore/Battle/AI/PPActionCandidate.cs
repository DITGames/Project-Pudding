/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPActionCandidate.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief 戦略層が評価する行動候補
 * =====================================*/
using System;
using CommandBattleCore;

namespace PPCore
{
    public sealed class PPActionCandidate
    {
        public PPBattleUnit Unit;
        public PPBattleRole Role;
        public PPResourceCost Cost;
        public PPBattleSkill Skill;
        public PPBattleUnit Target;
        
        public Func<BattleContext, BattleCommandBase> BuildCommand;
        
        public float Score;
    }
}