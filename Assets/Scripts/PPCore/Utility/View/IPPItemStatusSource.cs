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
    public interface IPPItemStatusSource
    {
        string DisplayName { get; }
        int Count { get; }
        int Cost { get; }
        bool IsUsable { get; }
        event Action Changed;
    }
}