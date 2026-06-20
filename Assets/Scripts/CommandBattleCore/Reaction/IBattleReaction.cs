/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IBattleReaction.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 条件成立で割り込み行動を発生させるリアクション
 * =====================================*/
 
namespace CommandBattleCore
{
    public interface IBattleReaction
    {
        // トリガー種別
        ReactionTrigger Trigger { get; }
        // 反応すべきかの判定
        bool ShouldReact(BattleUnit aOwner, ReactionContext aReactContext, BattleContext aContext);
        // 反応として積むコマンドを構築する
        BattleCommandBase BuildReaction(BattleUnit aOwner, ReactionContext aReactContext, BattleContext aContext);
    }
}
 