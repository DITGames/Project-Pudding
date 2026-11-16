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

    // 特定の出来事に反応して割り込み行動を発生させるリアクション（反撃・カウンターなど）
    // ユニットの BattleUnit.Reactions に登録しておくと、
    // BattleManager.DispatchReactions がトリガー発生時に走査し、
    // ShouldReact が true を返したものについて BuildReaction のコマンドをキュー先頭へ割り込ませる
    // 反撃が反撃を呼ぶ連鎖を止めたい場合は ShouldReact で ReactionContext.IsReactionDamage を見て false を返すこと
    // 一定の深さまで連鎖を許す場合や、撃破などダメージ以外のトリガーで連鎖を止める場合は ReactionContext.ReactionDepth を見る
    // （引き金が起きたときの連鎖の深さ。ダメージ以外のトリガーでも、反撃の最中なら 1 以上になる）
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

    // リアクション判定へ渡される、トリガー発生時の状況
    // 「何が起きたか」「誰が起こしたか」「誰に起きたか」を 1 つにまとめて IBattleReaction へ渡す
    // 反撃なら Instigator が反撃先になる。発火のたびに生成されるため、値型（readonly struct）にしてある
    public readonly struct ReactionContext
    {
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

        // 引き金になったダメージがリアクション（反撃など）によるものなら true。ダメージ以外のトリガーでは常に false
        // 「反撃に反撃を返さない」を実装する際は ShouldReact でこれを見て false を返す
        // 撃破・回復などダメージ以外のトリガーで連鎖を止めたい場合は ReactionDepth を見る
        public bool IsReactionDamage => Damage?.IsReaction ?? false;
        // 引き金の出来事が起きたときのリアクションの連鎖の深さ（0 = 通常の行動やターン経過の中で起きた）
        // ダメージが引き金ならそのダメージの深さ、ダメージ以外のトリガーなら実行中のコマンドの深さで、
        // BattleManager.DispatchReactions がキューへ積むリアクションの深さはこの値 + 1 になる
        // 「深さ N まで許す」を実装する際は ShouldReact でこれが N 未満のときだけ true を返す
        // DispatchReactions を通さずに作ったコンテキストでは、引き金のダメージの深さ（無ければ 0）を返す
        public int ReactionDepth => mReactionDepth ?? Damage?.Reaction?.Depth ?? 0;

        // aTrigger : トリガー種別
        // aInstigator : トリガーを起こしたユニット
        // aSubject : トリガーの対象ユニット
        // aDamage : ダメージ情報。ダメージ系トリガーのみ
        // aPayload : 追加データ
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

        // 引き金の深さを設定した複製を作る。BattleManager.DispatchReactions がリアクションへ渡す前に使う
        // aReactionDepth : 引き金の深さ
        // return : 深さ以外は同じ内容のコンテキスト
        public ReactionContext WithReactionDepth(int aReactionDepth)
            => new ReactionContext(this, aReactionDepth);

        // WithReactionDepth 用の複製コンストラクタ
        // aSource : 複製元
        // aReactionDepth : 引き金の深さ
        private ReactionContext(in ReactionContext aSource, int aReactionDepth)
        {
            Trigger = aSource.Trigger;
            Instigator = aSource.Instigator;
            Subject = aSource.Subject;
            Damage = aSource.Damage;
            Payload = aSource.Payload;
            mReactionDepth = aReactionDepth;
        }
    }

    // リアクション（反撃・とげなど）として発生した行動・ダメージに付く情報
    // BattleManager.DispatchReactions がリアクションのコマンドに設定し、
    // そのコマンドの実行中に発生したダメージへ BattleUnit.ApplyDamage が引き継ぐ
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
