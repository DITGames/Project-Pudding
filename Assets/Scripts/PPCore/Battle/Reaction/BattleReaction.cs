/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file BattleReaction.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief リアクション（トリガー種別・文脈・リアクション情報）
 * =====================================*/

namespace PPCore
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

    // 特定の出来事に反応して割り込み行動を発生させるリアクション（反撃など）。BattleUnit.Reactions に登録する
    // 連鎖を止めるかは ShouldReact で ReactionContext.IsReactionDamage / ReactionDepth を見て判断する
    public interface IBattleReaction
    {
        // 反応するトリガー種別
        ReactionTrigger Trigger { get; }

        // 反応として積むコマンドを構築する
        BattleCommandBase BuildReaction(BattleUnit aOwner, ReactionContext aReactContext, BattleContext aContext);

        // この状況で実際に反応すべきかを判定する
        // トリガー種別の一致は呼び出し側で済んでいるため、ここでは条件（残 HP・確率など）だけを見る
        bool ShouldReact(BattleUnit aOwner, ReactionContext aReactContext, BattleContext aContext);
    }

    // リアクション判定へ渡される、トリガー発生時の状況（何が起きたか・誰が起こしたか・誰に起きたか）
    // 反撃なら Instigator が反撃先になる
    public readonly struct ReactionContext
    {
        /* 状態 */

        // 引き金となった出来事の種別
        public ReactionTrigger Trigger { get; }
        // トリガーを起こしたユニット。ターン系トリガーなど、主体が無い場合は null
        public BattleUnit Instigator { get; }
        // トリガーの対象となったユニット。ターン系トリガーでは null
        public BattleUnit Subject { get; }
        // ダメージ系トリガーで使用されるダメージ情報。それ以外では null
        public DamageInfo Damage { get; }
        // トリガーの追加データ。ターン系では BattleContext が入る
        public object Payload { get; }
        // BattleManager.DispatchReactions が求めた引き金の深さ。未設定なら null
        private readonly int? mReactionDepth;

        /* プロパティ */

        // 引き金になったダメージがリアクションによるものなら true。ダメージ以外のトリガーでは常に false
        // ダメージ以外のトリガーで連鎖を止めたい場合は ReactionDepth を見る
        public bool IsReactionDamage => Damage?.IsReaction ?? false;
        // 引き金の出来事が起きたときのリアクションの連鎖の深さ（0 = 通常の行動やターン経過の中で起きた）
        // DispatchReactions が積むリアクションの深さはこの値 + 1 になる
        public int ReactionDepth => mReactionDepth ?? Damage?.Reaction?.Depth ?? 0;

        /* 生成 */

        public ReactionContext(ReactionTrigger aTrigger, BattleUnit aInstigator, BattleUnit aSubject,
            DamageInfo aDamage = null, object aPayload = null)
        {
            Trigger = aTrigger;
            Instigator = aInstigator;
            Subject = aSubject;
            Damage = aDamage;
            Payload = aPayload;
            mReactionDepth = null;
        }

        // WithReactionDepth 用の複製コンストラクタ
        private ReactionContext(in ReactionContext aSource, int aReactionDepth)
        {
            Trigger = aSource.Trigger;
            Instigator = aSource.Instigator;
            Subject = aSource.Subject;
            Damage = aSource.Damage;
            Payload = aSource.Payload;
            mReactionDepth = aReactionDepth;
        }

        /* 操作 */

        // 引き金の深さを設定した複製を作る。BattleManager.DispatchReactions がリアクションへ渡す前に使う
        public ReactionContext WithReactionDepth(int aReactionDepth)
            => new ReactionContext(this, aReactionDepth);
    }

    // リアクションとして発生した行動・ダメージに付く情報。DispatchReactions がコマンドに設定し、ApplyDamage がダメージへ引き継ぐ
    // 「何によるダメージか」（DamageInfo.Reason）とは独立した軸として扱う
    public class ReactionDamageInfo
    {
        // 連鎖の深さ。1 = 通常の行動へのリアクション、2 = リアクションへのリアクション …
        public int Depth { get; }
        // 発生元のリアクション
        public IBattleReaction Reaction { get; }

        // aDepth : 連鎖の深さ。1 未満は 1 に丸める
        // aReaction : 発生元のリアクション
        public ReactionDamageInfo(int aDepth, IBattleReaction aReaction)
        {
            Depth = aDepth < 1 ? 1 : aDepth;
            Reaction = aReaction;
        }
    }
}
