/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleSkill.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief バトルスキル定義のベースクラス
 * =====================================*/
using System;
using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    public class PPBattleSkill : BattleSkill
    {
        public PPBattleSkill(string aSkillId, string aDisplayName, ITargetResolver aDefaultResolver,
            Action<BattleUnit, List<BattleUnit>, BattleContext> aEffect)
            : base(aSkillId, aDisplayName, aDefaultResolver, aEffect)
        {
            
        }
    }
}