/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPActedConsumeRuleDefinition.cs
 * @author hqrse
 * @date 2026/11/10
 * @brief 行動したときにスタックを消費する消費ルールのデータ定義
 * =====================================*/

using System;
using CommandBattleCore;

namespace PPCore
{
    // 行動したときにスタックを消費するルールの定義
    // 「行動したら溜めを使い切る」や、確率と組み合わせて「行動しようとするたびに一定確率で解ける」混乱などに使う
    [Serializable]
    [PPTypeMenuName("行動時")]
    public class PPActedConsumeRuleDefinition : PPTriggerConsumeRuleDefinitionBase
    {
        protected override StatusEffectConsumeTrigger Trigger => StatusEffectConsumeTrigger.Acted;
    }
}
