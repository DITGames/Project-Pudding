/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file StatBlock.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 基礎ステータスのまとまり
 * =====================================*/
using System;
using AttributeUtility;

namespace CommandBattleCore
{
    // ユニット定義がインスペクタ上で基礎ステータスを設定するための構造体
    // ランタイムの ParameterSet と違い修飾子の仕組みを持たない、素の数値の入れ物
    [Serializable]
    public struct StatBlock
    {
        [Label("最大HP")]
        public float MaxHP;
        [Label("攻撃力")]
        public float Attack;
        [Label("防御力")]
        public float Defense;
        // 行動順の決定に使う
        [Label("素早さ")]
        public float Speed;
    }
}
