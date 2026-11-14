/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPAttackedConsumeRuleDefinition.cs
 * @author hqrse
 * @date 2026/11/10
 * @brief 攻撃したときにスタックを消費する消費ルールのデータ定義
 * =====================================*/

using System;
using CommandBattleCore;

namespace PPCore
{
    // 攻撃行動をしたときにスタックを消費するルールの定義
    // 「次の攻撃だけ強化する」といった効果に使う
    [Serializable]
    [PPTypeMenuName("攻撃時")]
    public class PPAttackedConsumeRuleDefinition : PPTriggerConsumeRuleDefinitionBase
    {
        protected override StatusEffectConsumeTrigger Trigger => StatusEffectConsumeTrigger.Attacked;
    }
}
