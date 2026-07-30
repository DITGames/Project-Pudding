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
    // UI がアイテムの表示情報を読み取るためのインターフェース
    // スキル用（IPPSkillStatusSource）とほぼ同じ構成だが、クールダウンの代わりに所持数を持つ
    public interface IPPItemStatusSource
    {
        // UI 表示名
        string DisplayName { get; }
        // 所持数
        int Count { get; }
        // 使用に必要なリソースコスト
        PPResourceCost Cost { get; }
        // 今このアイテムを使用できるか。ボタンの有効・無効に使う
        bool IsUsable { get; }
        // 表示内容が変化したときに発火する
        event Action Changed;
    }
}
