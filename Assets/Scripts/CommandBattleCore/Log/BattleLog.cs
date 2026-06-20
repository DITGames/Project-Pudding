/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleLog.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトル用ログ定義
 * =====================================*/
using System.Collections.Generic;

namespace CommandBattleCore
{
    public enum BattleLogType
    {
        Action, Damage, Heal, StatusEffect, UnitDefeated, Swap, Escape, Custom,
    }

    public record BattleLogEntry
    {
        public BattleLogType LogType { get; private set; }
        public BattleUnit Unit { get; private set; }
        public BattleUnit Target { get; private set; }
        public string Description { get; private set; }
        public float TimeStamp { get; private set; }

        public BattleLogEntry(BattleLogType aType, BattleUnit aSource, BattleUnit aTarget, string aDescription,
            float aTimeStamp)
        {
            LogType = aType;
            Unit = aSource;
            Target = aTarget;
            Description = aDescription;
            TimeStamp = aTimeStamp;
        }
    }

    public interface IBattleLogger
    {
        void Log(BattleLogEntry entry);
    }

    public class DefaultBattleLogger : IBattleLogger
    {
        private readonly List<BattleLogEntry> mHistory = new();
        public IReadOnlyList<BattleLogEntry> History => mHistory;
        public void Log(BattleLogEntry entry) => mHistory.Add(entry);
    }
}