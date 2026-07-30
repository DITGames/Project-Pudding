/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPItemStatusSource.cs
 * @author hqrse
 * @date 2026/07/02
 * @brief アイテム状態ソース
 * =====================================*/
using System;

namespace PPCore
{
    /// <summary>
    /// UI がアイテムの表示情報を読み取るためのインターフェース。
    /// スキル用（<see cref="IPPSkillStatusSource"/>）とほぼ同じ構成だが、
    /// クールダウンの代わりに所持数を持つ。
    /// </summary>
    public interface IPPItemStatusSource
    {
        /// <summary>UI 表示名。</summary>
        string DisplayName { get; }
        /// <summary>所持数。</summary>
        int Count { get; }
        /// <summary>使用に必要なリソースコスト。</summary>
        PPResourceCost Cost { get; }
        /// <summary>今このアイテムを使用できるか。ボタンの有効・無効に使う。</summary>
        bool IsUsable { get; }
        /// <summary>表示内容が変化したときに発火する。</summary>
        event Action Changed;
    }
}
