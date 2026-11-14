/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file ReactionContext.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief トリガー発生時のコンテキスト
 * =====================================*/

namespace CommandBattleCore
{
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
}
