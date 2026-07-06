/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleSelectionContext.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief バトル中のコマンド選択途中の蓄積データ
 * =====================================*/
using System;
using CommandBattleCore;

namespace PPCore
{
    public class PPBattleSelectionContext
    {
        public BattleUnit Unit;
        public BattleSkill Skill;
        public BattleUnit Target;

        public Func<BattleUnit, BattleCommandBase> CommandBuilder;

        public TargetScope? TargetScope;

        // 完全なリセット
        public void Clear()
        {
            Unit = null; 
            Skill = null;
            Target = null;
            CommandBuilder = null;
            TargetScope = null;
        }

        // 選択したユニットのみ残してあとはリセットする
        public void ClearSelectionKeepingUnit()
        {
            Skill = null;
            Target = null;
            CommandBuilder = null;
            TargetScope = null;
        }
    }
}