/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPStatBlock.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief PP専用スタータスブロック
 * =====================================*/
using System;
using CommandBattleCore;

namespace PPCore
{
    [Serializable]
    public struct PPStatBlock
    {
        [Label("通常攻撃コスト")]
        public float AttackCost;
    }
}