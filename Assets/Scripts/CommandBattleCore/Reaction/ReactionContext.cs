/* =====================================
 * Copyright hqrse. All rights reserved.
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
        }
    }
}
