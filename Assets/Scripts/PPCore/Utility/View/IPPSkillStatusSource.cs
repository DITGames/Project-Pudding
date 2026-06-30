/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPSkillStatusSource.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief PPスキル情報読み取りインターフェース
 * =====================================*/
using System;

namespace PPCore
{
    public interface IPPSkillStatusSource
    {
        string DisplayName { get; }
        int Cost { get; }
        bool IsCastable { get; }
        int CooldownRemaining { get; }
        event Action Changed;
    }
}