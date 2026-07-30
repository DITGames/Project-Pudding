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
    /// <summary>
    /// プッシャー（物理）側がコイン獲得を知らせるためのインターフェース。
    /// <para>
    /// これを挟むことで、バトル側は落ちたコインをどう検出しているかを知らずに済む。
    /// 実装例は <c>CoinDropCounter</c>、購読側は <see cref="PPCoinResourceBridge"/>。
    /// </para>
    /// </summary>
    public interface IPPCoinGainNotifier
    {
        /// <summary>コイン獲得時(コインの属性, 枚数)</summary>
        event Action<PPTypeAttribute, int> OnCoinGained;
    }
}
