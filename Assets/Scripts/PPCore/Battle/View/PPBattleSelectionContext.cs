/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleSelectionContext.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief PPバトル中のコマンド選択途中の蓄積データ
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    public class PPBattleSelectionContext
    {
        public BattleUnit Unit;
        public BattleSkill Skill;
        public BattleUnit Target;

        public void Clear()
        {
            Unit = null; 
            Skill = null;
            Target = null;
        }
    }
}