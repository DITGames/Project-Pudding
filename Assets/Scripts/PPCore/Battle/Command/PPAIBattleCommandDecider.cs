/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPAIBattleCommandDecider.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief バトルユニットコマンド選択ベース
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // ランダム
    public class PPRandomAICommandDecider : AICommandDeciderBase
    {
        public override BattleCommandBase DecideCommand(BattleUnit aSelf, BattleContext aContext)
        {
            // PP専用の攻撃コマンドをベースで追加
            var options = new List<BattleCommandBase>
            {
                new PPBattleAttackCommand(aSelf as PPBattleUnit, new RandomEnemyResolver())
            };

            foreach (var skill in aSelf.Skills)
            {
                if (aContext.Rules.CastValidator.Validate(aSelf, skill, aContext).CanCast)
                {
                    // 継承クラスを追加する予定
                    options.Add(new SkillCommand(aSelf, skill));
                }
            }
            
            return options[aContext.Rules.RandomProvider.NextInt(options.Count)];
        }
    }
}