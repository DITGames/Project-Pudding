/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ReactionContext.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief トリガー発生時のコンテキスト
 * =====================================*/
 
namespace CommandBattleCore
{
    public readonly struct ReactionContext
    {
        // 引き金
        public ReactionTrigger Trigger { get; }
        // トリガーを起こしたユニット
        public BattleUnit Instigator { get; }
        // トリガー対象
        public BattleUnit Subject { get; }
        // ダメージ系トリガーで使用される
        public DamageInfo Damge { get; }
        // トリガーの追加データ
        public object Payload { get; }

        public ReactionContext(ReactionTrigger aTrigger, BattleUnit aInstigator, BattleUnit aSubject,
            DamageInfo aDamage = null, object aPayload = null)
        {
            Trigger = aTrigger;
            Instigator = aInstigator;
            Subject = aSubject;
            Damge = aDamage;
            Payload = aPayload;
        }
    }
}