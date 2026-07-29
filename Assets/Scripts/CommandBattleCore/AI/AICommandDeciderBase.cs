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
    /// <summary>
    /// AI によるコマンド決定の基底クラス。
    /// プレイヤー入力側の実装と型で区別を付けるための足場で、現状は追加の振る舞いを持たない。
    /// </summary>
    public abstract class AICommandDeciderBase : ICommandDecider
    {
        /// <summary>
        /// このユニットが取る行動を決める。派生側で実装する。
        /// </summary>
        /// <param name="aSelf">行動を決めるユニット。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>実行するコマンド。</returns>
        public abstract BattleCommandBase DecideCommand(BattleUnit aSelf, BattleContext aContext);
    }

    /// <summary>
    /// 通常攻撃と発動可能なスキルから完全にランダムで選ぶ、最も単純な AI。
    /// <see cref="UnitDefinition.CreateRuntimeUnit"/> で decider を指定しなかった場合の既定値になる。
    /// </summary>
    public class RandomAICommandDecider : AICommandDeciderBase
    {
        /// <summary>全インスタンスで共有する乱数。</summary>
        /// <remarks>
        /// 既知の未整理箇所: 実際の選択では <c>aContext.Rules.RandomProvider</c> を使っており、
        /// このフィールドは参照されていない。
        /// </remarks>
        protected static readonly System.Random Rng = new();

        /// <summary>
        /// 通常攻撃を必ず候補に入れたうえで、今撃てるスキルを候補へ追加し、
        /// その中から等確率で 1 つ選ぶ。候補が必ず 1 件以上あるため選択に失敗しない。
        /// </summary>
        /// <param name="aSelf">行動を決めるユニット。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>選ばれたコマンド。</returns>
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
