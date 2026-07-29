/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleSide.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 陣営の定義
 * =====================================*/

namespace CommandBattleCore
{
    /// <summary>
    /// ユニットが属する陣営。
    /// <see cref="BattleContext.GetParty"/> / <see cref="BattleContext.GetOpponentParty"/> の引数になり、
    /// ターゲット解決で「味方」「敵」を判断する基準になる。
    /// </summary>
    public enum BattleSide
    {
        /// <summary>プレイヤー側。</summary>
        Ally,
        /// <summary>敵側。</summary>
        Enemy,
    }
}
