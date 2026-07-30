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
    // 本作のコマンドを使うランダム AI
    // 挙動は基底の RandomAICommandDecider と同じだが、
    // 生成するコマンドがリソース消費に対応した PPAttackCommand / PPSkillCommand になる点が異なる
    // ユニット単位で動く簡易 AI で、UnitDefinition.CreateRuntimeUnit の既定値として使われる
    // 敵パーティの本来の思考は PPPartyAIStrategistBase が担う
    public class PPRandomAICommandDecider : AICommandDeciderBase
    {
        // 通常攻撃を必ず候補に入れたうえで、今撃てるスキルを候補へ追加し、その中から等確率で選ぶ
        // aSelf : 行動を決めるユニット
        // aContext : バトルコンテキスト
        // return : 選ばれたコマンド
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
