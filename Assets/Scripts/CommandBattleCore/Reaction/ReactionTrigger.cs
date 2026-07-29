/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ReactionTrigger.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief リアクションの発動タイミング
 * =====================================*/

namespace CommandBattleCore
{
    /// <summary>
    /// リアクション（反撃など）が反応する出来事の種類。
    /// <see cref="BattleManager.DispatchReactions"/> がこの種別を突き合わせて、
    /// 一致する <see cref="IBattleReaction"/> だけを発火させる。
    /// </summary>
    public enum ReactionTrigger
    {
        /// <summary>ダメージを受けたとき。反撃・とげ等。</summary>
        OnDamaged,
        /// <summary>回復を受けたとき。</summary>
        OnHealed,
        /// <summary>ユニットが撃破されたとき。</summary>
        OnUnitDefeated,
        /// <summary>ステータスエフェクトが付与されたとき。</summary>
        OnStatusAdded,
        /// <summary>ターンが開始したとき。</summary>
        OnTurnStarted,
        /// <summary>ターンが終了したとき。</summary>
        OnTurnEnded,
    }
}
