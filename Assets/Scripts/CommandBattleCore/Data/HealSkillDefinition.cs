/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file HealSkillDefinition.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief 回復系スキルのベース定義
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CommandBattleCore
{
    // 対象の HP を回復するスキルの定義
    // 回復量はスキルパワーそのままで、術者のパラメータには影響されない
    [CreateAssetMenu(fileName = "HealSkillDefinition", menuName = "CommandBattleCore/HealSkillDefinition")]
    public class HealSkillDefinition : SkillDefinition
    {
        // 対象全員をスキルパワー分だけ回復する効果を組み立てる
        // 戦闘不能のユニットは BattleUnit.ApplyHeal 側で弾かれるため、蘇生にはならない
        // return : 効果本体のデリゲート
        protected override Action<BattleUnit, List<BattleUnit>, BattleContext> BuildEffect()
        {
            return (src, targets, ctx) =>
            {
                foreach (var tgt in targets)
                {
                    tgt.ApplyHeal(mPower);
                }
            };
        }
    }
}
