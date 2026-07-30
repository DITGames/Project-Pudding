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
    /// <summary>
    /// ユニット個体ごとの行動スコア倍率。
    /// <para>
    /// AI がロール別に算出したスコアへ最後に掛かる。
    /// ロール（<see cref="PPUnitRole"/>）が「どの行動を優先するか」の大枠を決めるのに対し、
    /// こちらは「同じ攻撃役でもこの個体は特に攻撃を好む」といった個体差を表現する。
    /// すべて 1 なら補正なし。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class PPUnitActionScoreModifier
    {
        /// <summary>攻撃行動のスコア倍率。</summary>
        [Label("攻撃倍率")]public float Attack = 1f;
        /// <summary>支援行動のスコア倍率。</summary>
        [Label("サポート倍率")] public float Support = 1f;
        /// <summary>回復行動のスコア倍率。</summary>
        [Label("回復倍率")] public float Heal = 1f;
    }
}
