/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ReactionTrigger.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief リアクションの発動タイミング
 * =====================================*/

namespace CommandBattleCore
{
    // リアクション（反撃など）が反応する出来事の種類
    // BattleManager.DispatchReactions がこの種別を突き合わせて、一致する IBattleReaction だけを発火させる
    public enum ReactionTrigger
    {
        // ダメージを受けたとき。反撃・とげ等
        OnDamaged,
        // 回復を受けたとき
        OnHealed,
        // ユニットが撃破されたとき
        OnUnitDefeated,
        // ステータスエフェクトが付与されたとき
        OnStatusAdded,
        // ターンが開始したとき
        OnTurnStarted,
        // ターンが終了したとき
        OnTurnEnded,
    }
}
