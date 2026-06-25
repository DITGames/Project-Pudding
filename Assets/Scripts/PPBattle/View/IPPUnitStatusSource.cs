/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPUnitStatusSource.cs
 * @author hqrse
 * @date 2026/06/2５
 * @brief ユニット情報読み取りのインターフェース
 * =====================================*/
using System;

namespace PPBattle
{
    public interface IPPUnitStatusSource
    {
        string DisplayName { get; }
        float CurrentHP { get; }
        float MaxHP { get; }

        event Action Changed;
    }
}