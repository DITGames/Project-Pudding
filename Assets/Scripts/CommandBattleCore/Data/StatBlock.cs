/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file StatBlock.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 基礎ステータスのまとまり
 * =====================================*/
using System;

namespace CommandBattleCore
{
    [Serializable]
    public struct StatBlock
    {
        [Label("最大HP")]
        public float MaxHP;
        [Label("攻撃力")]
        public float Attack;
        [Label("防御力")]
        public float Defense;
        [Label("素早さ")]
        public float Speed;
    }
}