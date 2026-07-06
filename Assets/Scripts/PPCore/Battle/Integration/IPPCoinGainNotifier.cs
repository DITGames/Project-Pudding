/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPCoinGainNotifier.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief プッシャーが実装するコイン取得通知インターフェース
 * =====================================*/
using System;

namespace PPCore
{
    public interface IPPCoinGainNotifier
    {
        event Action<int> OnCoinGained;
    }
}