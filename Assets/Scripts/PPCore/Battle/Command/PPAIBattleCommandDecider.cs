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
    /// <summary>
    /// 本作のコマンドを使うランダム AI。
    /// <para>
    /// 挙動は基底の <see cref="RandomAICommandDecider"/> と同じだが、
    /// 生成するコマンドがリソース消費に対応した <see cref="PPAttackCommand"/> /
    /// <see cref="PPSkillCommand"/> になる点が異なる。
    /// </para>
    /// <para>
    /// ユニット単位で動く簡易 AI で、<see cref="UnitDefinition.CreateRuntimeUnit"/> の既定値として使われる。
    /// 敵パーティの本来の思考は <see cref="PPPartyAIStrategistBase"/> が担う。
    /// </para>
    /// </summary>
    public class PPRandomAICommandDecider : AICommandDeciderBase
    {
        /// <summary>
        /// 通常攻撃を必ず候補に入れたうえで、今撃てるスキルを候補へ追加し、その中から等確率で選ぶ。
        /// </summary>
        /// <param name="aSelf">行動を決めるユニット。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>選ばれたコマンド。</returns>
        public override BattleCommandBase DecideCommand(BattleUnit aSelf, BattleContext aContext)
        {
            // PP専用の攻撃コマンドをベースで追加
            var options = new List<BattleCommandBase>
            {
                new PPAttackCommand(aSelf as PPBattleUnit, new RandomEnemyResolver())
            };

            foreach (var skill in aSelf.Skills)
            {
                if (aContext.Rules.CastValidator.Validate(aSelf, skill, aContext).CanCast)
                {
                    // 継承クラスを追加する予定
                    options.Add(new PPSkillCommand(aSelf, skill));
                }
            }

            return options[aContext.Rules.RandomProvider.NextInt(options.Count)];
        }
    }
}
