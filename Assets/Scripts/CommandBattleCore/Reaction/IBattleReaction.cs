/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file IBattleReaction.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 条件成立で割り込み行動を発生させるリアクション
 * =====================================*/

namespace CommandBattleCore
{
    // 特定の出来事に反応して割り込み行動を発生させるリアクション（反撃・カウンターなど）
    // ユニットの BattleUnit.Reactions に登録しておくと、
    // BattleManager.DispatchReactions がトリガー発生時に走査し、
    // ShouldReact が true を返したものについて BuildReaction のコマンドをキュー先頭へ割り込ませる
    // 反撃が反撃を呼ぶ連鎖を止めたい場合は ShouldReact で ReactionContext.IsReactionDamage を見て false を返すこと
    // BattleRules.MaxReactionPerEvent は想定外の連鎖に対する安全網であり、
    // 意図した連鎖の可否はリアクション側の判断に任せている
    // （一律に抑止すると、反撃の最中は「とげ」のような別のリアクションまで発火できなくなるため）
    public interface IBattleReaction
    {
        // 反応するトリガー種別
        ReactionTrigger Trigger { get; }

        // この状況で実際に反応すべきかを判定する
        // トリガー種別の一致は呼び出し側で済んでいるため、ここでは条件（残 HP・確率など）だけを見る
        // aOwner : このリアクションを持つユニット
        // aReactContext : トリガー発生時の状況
        // aContext : バトルコンテキスト
        // return : 反応する場合 true
        bool ShouldReact(BattleUnit aOwner, ReactionContext aReactContext, BattleContext aContext);

        // 反応として積むコマンドを構築する
        // aOwner : このリアクションを持つユニット
        // aReactContext : トリガー発生時の状況。反撃先は通常 Instigator
        // aContext : バトルコンテキスト
        // return : 割り込ませるコマンド。null を返すと何も積まれない
        BattleCommandBase BuildReaction(BattleUnit aOwner, ReactionContext aReactContext, BattleContext aContext);
    }
}
