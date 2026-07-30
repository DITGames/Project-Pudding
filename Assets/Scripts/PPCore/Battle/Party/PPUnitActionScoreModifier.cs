/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitActionScoreModifier.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief ユニット個別の行動スコア倍率
 * =====================================*/
using System;
using CommandBattleCore;

namespace PPCore
{
    // ユニット個体ごとの行動スコア倍率
    // AI がロール別に算出したスコアへ最後に掛かる
    // ロール（PPUnitRole）が「どの行動を優先するか」の大枠を決めるのに対し、
    // こちらは「同じ攻撃役でもこの個体は特に攻撃を好む」といった個体差を表現する
    // すべて 1 なら補正なし
    [Serializable]
    public sealed class PPUnitActionScoreModifier
    {
        [Label("攻撃倍率")]public float Attack = 1f;
        [Label("サポート倍率")] public float Support = 1f;
        [Label("回復倍率")] public float Heal = 1f;
    }
}
