/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleSide.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 陣営の定義
 * =====================================*/

namespace CommandBattleCore
{
    // ユニットが属する陣営
    // BattleContext.GetParty / BattleContext.GetOpponentParty の引数になり、
    // ターゲット解決で「味方」「敵」を判断する基準になる
    public enum BattleSide
    {
        // プレイヤー側
        Ally,
        // 敵側
        Enemy,
    }
}
