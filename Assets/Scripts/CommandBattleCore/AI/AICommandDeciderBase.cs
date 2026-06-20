/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AICommandDeciderBase.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief AIコマンドの基底
 * =====================================*/
using System.Collections.Generic;

namespace CommandBattleCore
{
    public abstract class AICommandDeciderBase : ICommandDecider
    {
        public abstract BattleCommandBase DecideCommand(BattleUnit aSelf, BattleContext aContext);
    }

    public class RandomAICommandDecider : AICommandDeciderBase
    {
        private static readonly System.Random Rng = new();

        public override BattleCommandBase DecideCommand(BattleUnit aSelf, BattleContext aContext)
        {
            var options = new List<BattleCommandBase>
            {
                new AttackCommand(aSelf, new RandomEnemyResolver())
            };

            foreach (var skill in aSelf.Skills)
            {
                // 選択可能なスキルのみ選択肢に入れる
                if (aContext.Rules.CastValidator.Validate(aSelf, skill, aContext).CanCast)
                {
                    options.Add(new SkillCommand(aSelf, skill));
                }
            }
            return options[aContext.Rules.RandomProvider.NextInt(options.Count)];
        }
    }
}