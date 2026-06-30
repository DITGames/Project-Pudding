/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPUnitStatusSource.cs
 * @author hqrse
 * @date 2026/06/2５
 * @brief PPユニット情報読み取りインターフェース
 * =====================================*/
using System;

namespace PPCore
{
    public interface IPPUnitStatusSource
    {
        string DisplayName { get; }
        float CurrentHP { get; }
        float MaxHP { get; }

        event Action Changed;
    }
}