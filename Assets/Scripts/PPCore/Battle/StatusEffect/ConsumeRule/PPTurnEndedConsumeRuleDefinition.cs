/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPTurnEndedConsumeRuleDefinition.cs
 * @author hqrse
 * @date 2026/11/10
 * @brief ターンが経過したときにスタックを消費する消費ルールのデータ定義
 * =====================================*/

using System;
using CommandBattleCore;

namespace PPCore
{
    // ターンが経過したときにスタックを消費するルールの定義
    // 「毎ターン少しずつ弱まる」効果に使う
    [Serializable]
    [PPTypeMenuName("ターン経過時")]
    public class PPTurnEndedConsumeRuleDefinition : PPTriggerConsumeRuleDefinitionBase
    {
        protected override StatusEffectConsumeTrigger Trigger => StatusEffectConsumeTrigger.TurnEnded;
    }
}
