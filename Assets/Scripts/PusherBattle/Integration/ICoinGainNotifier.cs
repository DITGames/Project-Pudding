/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ICoinGainNotifier.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief プッシャーが実装するインターフェース
 * =====================================*/
using System;

namespace PusherBattle
{
    public interface ICoinGainNotifier
    {
        event Action<int> OnCainGained;
    }
}