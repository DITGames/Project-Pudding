/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleResult.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトルのリザルト定義
 * =====================================*/

namespace CommandBattleCore
{
    public enum BattleResultType
    {
        InProgress, Victory, Defeat, Draw, Escaped, Custom,
    }

    public class BattleResult
    {
        public BattleResultType Type { get; }
        
        public BattleResult(BattleResultType type)
        {
            Type = type;
        }

        public static readonly BattleResult InProgress = new(BattleResultType.InProgress);
    }
}
