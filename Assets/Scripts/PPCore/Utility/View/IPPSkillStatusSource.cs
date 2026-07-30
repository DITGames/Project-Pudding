/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPSkillStatusSource.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief スキル情報読み取りインターフェース
 * =====================================*/
using System;

namespace PPCore
{
    /// <summary>
    /// UI がスキルの表示情報を読み取るためのインターフェース。
    /// スキル名とコストに加えて、ボタンを押せるかどうか（<see cref="IsCastable"/>）と
    /// 残クールダウンを露出させ、メニュー側が発動可否を自前で判定せずに済むようにする。
    /// </summary>
    public interface IPPSkillStatusSource
    {
        /// <summary>UI 表示名。</summary>
        string DisplayName { get; }
        /// <summary>消費リソース。</summary>
        PPResourceCost Cost { get; }
        /// <summary>今このスキルを発動できるか。ボタンの有効・無効に使う。</summary>
        bool IsCastable { get; }
        /// <summary>残りクールダウンターン数。</summary>
        int CooldownRemaining { get; }
        /// <summary>表示内容が変化したときに発火する。</summary>
        event Action Changed;
    }
}
