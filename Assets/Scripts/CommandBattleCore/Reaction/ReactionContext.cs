/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ReactionContext.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief トリガー発生時のコンテキスト
 * =====================================*/

namespace CommandBattleCore
{
    /// <summary>
    /// リアクション判定へ渡される、トリガー発生時の状況。
    /// <para>
    /// 「何が起きたか」「誰が起こしたか」「誰に起きたか」を 1 つにまとめて
    /// <see cref="IBattleReaction"/> へ渡す。反撃なら <see cref="Instigator"/> が反撃先になる。
    /// 発火のたびに生成されるため、値型（readonly struct）にしてある。
    /// </para>
    /// </summary>
    public readonly struct ReactionContext
    {
        /// <summary>引き金となった出来事の種別。</summary>
        public ReactionTrigger Trigger { get; }
        /// <summary>トリガーを起こしたユニット。ターン系トリガーなど、主体が無い場合は null。</summary>
        public BattleUnit Instigator { get; }
        /// <summary>トリガーの対象となったユニット。ターン系トリガーでは null。</summary>
        public BattleUnit Subject { get; }
        /// <summary>ダメージ系トリガーで使用されるダメージ情報。それ以外では null。</summary>
        public DamageInfo Damage { get; }
        /// <summary>トリガーの追加データ。ターン系では <see cref="BattleContext"/> が入る。</summary>
        public object Payload { get; }

        /// <param name="aTrigger">トリガー種別。</param>
        /// <param name="aInstigator">トリガーを起こしたユニット。</param>
        /// <param name="aSubject">トリガーの対象ユニット。</param>
        /// <param name="aDamage">ダメージ情報。ダメージ系トリガーのみ。</param>
        /// <param name="aPayload">追加データ。</param>
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
