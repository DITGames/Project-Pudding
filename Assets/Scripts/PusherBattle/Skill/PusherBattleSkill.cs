/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PusherBattleSkill.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief Pusherのバトルスキルベース
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;

namespace PusherBattle
{
    public class PusherBattleSkill : BattleSkill
    {
        public PusherBattleSkill(string aSkillId, string aDisplayName, ITargetResolver aDefaultResolver,
            Action<BattleUnit, List<BattleUnit>, BattleContext> aEffect)
            : base(aSkillId, aDisplayName, aDefaultResolver, aEffect)
        {
            
        }
    }
}