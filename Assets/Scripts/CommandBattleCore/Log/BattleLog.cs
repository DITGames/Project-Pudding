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
        Action, Damage, Heal, StatusEffect, UnitDefeated, Swap, Escape, ActionBlocked, Custom,
    }

    public record BattleLogEntry
    {
        public BattleLogType LogType { get; protected set; }
        public BattleUnit Unit { get; protected set; }
        public BattleUnit Target { get; protected set; }
        public string Description { get; protected set; }
        public float TimeStamp { get; protected set; }

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
        protected readonly List<BattleLogEntry> mHistory = new();
        public IReadOnlyList<BattleLogEntry> History => mHistory;
        public virtual void Log(BattleLogEntry entry) => mHistory.Add(entry);
    }
}