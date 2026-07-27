/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitActionScoreModifier.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief 
 * =====================================*/
using System;
using CommandBattleCore;

namespace PPCore
{
    [Serializable]
    public sealed class PPUnitActionScoreModifier
    {
        [Label("攻撃倍率")]public float Attack = 1f;
        [Label("サポート倍率")] public float Support = 1f;
        [Label("回復倍率")] public float Heal = 1f;
    }
}