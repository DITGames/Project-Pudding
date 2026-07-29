/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IBattleReaction.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 条件成立で割り込み行動を発生させるリアクション
 * =====================================*/

namespace CommandBattleCore
{
    /// <summary>
    /// 特定の出来事に反応して割り込み行動を発生させるリアクション（反撃・カウンターなど）。
    /// <para>
    /// ユニットの <see cref="BattleUnit.Reactions"/> に登録しておくと、
    /// <see cref="BattleManager.DispatchReactions"/> がトリガー発生時に走査し、
    /// <see cref="ShouldReact"/> が true を返したものについて
    /// <see cref="BuildReaction"/> のコマンドをキュー先頭へ割り込ませる。
    /// </para>
    /// <para>
    /// 反撃が反撃を呼ぶ連鎖は <see cref="BattleManager.MaxReactionPerEvent"/> と
    /// リアクション実行中の抑止フラグで止まるため、実装側で気にする必要はない。
    /// </para>
    /// </summary>
    public interface IBattleReaction
    {
        /// <summary>反応するトリガー種別。</summary>
        ReactionTrigger Trigger { get; }

        /// <summary>
        /// この状況で実際に反応すべきかを判定する。
        /// トリガー種別の一致は呼び出し側で済んでいるため、ここでは条件（残 HP・確率など）だけを見る。
        /// </summary>
        /// <param name="aOwner">このリアクションを持つユニット。</param>
        /// <param name="aReactContext">トリガー発生時の状況。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>反応する場合 true。</returns>
        bool ShouldReact(BattleUnit aOwner, ReactionContext aReactContext, BattleContext aContext);

        /// <summary>
        /// 反応として積むコマンドを構築する。
        /// </summary>
        /// <param name="aOwner">このリアクションを持つユニット。</param>
        /// <param name="aReactContext">トリガー発生時の状況。反撃先は通常 Instigator。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>割り込ませるコマンド。null を返すと何も積まれない。</returns>
        BattleCommandBase BuildReaction(BattleUnit aOwner, ReactionContext aReactContext, BattleContext aContext);
    }
}
