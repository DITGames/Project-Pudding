/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPermanentDurationConditionDefinition.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief 自然には切れない永続の持続条件のデータ定義
 * =====================================*/

using System;
using CommandBattleCore;

namespace PPCore
{
    // ターン経過では切れない永続の持続条件の定義
    // 解除するにはアイテムや解除スキル（PPStatusCureSkillEffectDefinition）で明示的に除去する必要がある
    // 解除スキルからも消せないようにしたい場合は、エフェクト側のタグに Unremovable を立てる
    [Serializable]
    [PPTypeMenuName("永続")]
    public class PPPermanentDurationConditionDefinition : PPDurationConditionDefinition
    {
        public override IDurationCondition CreateDurationCondition() => new PermanentDurationCondition();

        public override string BuildString() => "永続";
    }
}
