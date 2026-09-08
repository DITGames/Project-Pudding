/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPTurnDurationConditionDefinition.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief 指定ターン数が経過すると切れる持続条件のデータ定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 指定ターン数が経過すると自然に切れる持続条件の定義
    // 大半のバフ・デバフ・状態異常はこちらを使う
    [Serializable]
    [PPTypeMenuName("ターン経過")]
    public class PPTurnDurationConditionDefinition : PPDurationConditionDefinition
    {
        [Header("ターン経過")]
        [Label("期間")][Min(1)]
        [SerializeField]protected int mDuration = 3;

        public int Duration => mDuration;

        public override IDurationCondition CreateDurationCondition() => new TurnDurationCondition(mDuration);

        public override string BuildString() => $"{mDuration}ターン";
    }
}
