/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file StatusEffect.cs
 * @author hqrse
 * @date 2026/07/31
 * @brief ステータスエフェクト（本体・文脈・振る舞いの基底）
 * =====================================*/

using System;
using System.Collections.Generic;

namespace PPCore
{
    // ユニットに掛かる状態変化のランタイムインスタンス。状態異常・バフ・デバフ・防御状態をすべてこの型で表現する
    // 効き目は StatusEffectBehaviour が持ち、このクラスは付与元・名前・持続などの枠組みだけを持つ
    public sealed class StatusEffect
    {
        /* 状態 */

        // エフェクト ID。同一 ID が重ね掛け判定の単位になる
        public string EffectId { get; }
        // UI 表示名
        public string DisplayName { get; }
        // 汎用分類
        public StatusEffectTag Tags { get; private set; } = StatusEffectTag.None;
        // ゲーム固有の分類（PPEffectCategory）。ビット比較に使う
        public long Category { get; private set; }
        // 効果が切れる条件。未指定なら永続
        public IDurationCondition DurationCondition { get; set; }
        // スタックを消費するきっかけと量を決めるルール。未指定ならスタックは自然には減らない
        public IStatusEffectConsumeRule ConsumeRule { get; private set; }
        // 付与されてから効果が発生した回数。確率消費のカーブなどで参照する
        public int ElapsedTicks { get; private set; }
        // 同一 ID が重ねて付与されたときの挙動
        public StatusEffectStackPolicy StackPolicy { get; private set; } = StatusEffectStackPolicy.Refresh;
        // スタック数の上限
        public int MaxStacks { get; private set; } = 1;
        // 現在のスタック数
        public int CurrentStacks { get; private set; } = 1;
        // このエフェクトが課す行動制限
        public ActionRestriction Restriction { get; private set; } = ActionRestriction.None;
        // 行動が失敗する確率(0～1)。null なら ActionRestriction.CannotAct 時に無条件で失敗する(睡眠など)
        public float? ActionFailChance { get; private set; }
        // エフェクトを付与した側。ラムダで捕捉せずデータとして保持する
        public BattleUnit Source { get; private set; }
        // エフェクトがかかっているユニット。AttachTo 時に設定する
        public BattleUnit Owner { get; private set; }
        // 生成元の定義アセットなど
        public object SourceDefinition { get; private set; }
        // このエフェクトが持つ振る舞いの一覧。Orderの昇順で並ぶ
        private readonly List<StatusEffectBehaviour> mBehaviours = new();
        // 次の発生を待っているタイマーと、その予約先。待っていなければ null
        private PPBattleTimer mTickTimer;
        private PPBattleTimerScheduler mTickScheduler;

        /* プロパティ */

        public IReadOnlyList<StatusEffectBehaviour> Behaviours => mBehaviours;

        // 解除されるまでの残り秒数（バトル専用の時計）。自然には切れないものは -1
        // 次の発生までの残りに、その後の発生の分（間隔 × 残り回数）を足して求める
        public float RemainingSeconds
        {
            get
            {
                if (DurationCondition is not IntervalDurationCondition interval) return -1f;

                float untilNext = mTickScheduler != null ? mTickScheduler.GetRemaining(mTickTimer) : interval.Interval;
                return untilNext + Math.Max(0, interval.RemainingCount - 1) * interval.Interval;
            }
        }

        /* 生成 */

        public StatusEffect(string aEffectId, string aDisplayName, IDurationCondition aDurationCondition = null)
        {
            EffectId = aEffectId;
            DisplayName = aDisplayName;
            DurationCondition = aDurationCondition ?? new PermanentDurationCondition();
        }

        /* 組み立て */

        public StatusEffect WithTags(StatusEffectTag aTags) { Tags = aTags; return this; }

        public StatusEffect WithCategory(long aCategory) { Category = aCategory; return this; }

        public StatusEffect WithSource(BattleUnit aSource) { Source = aSource; return this; }

        public StatusEffect WithSourceDefinition(object aDefinition) { SourceDefinition = aDefinition; return this; }

        public StatusEffect WithRestriction(ActionRestriction aRestriction, float? aFailChance = null)
        {
            Restriction = aRestriction;
            ActionFailChance = aFailChance;
            return this;
        }

        public StatusEffect WithConsumeRule(IStatusEffectConsumeRule aRule) { ConsumeRule = aRule; return this; }

        public StatusEffect WithStacking(StatusEffectStackPolicy aPolicy, int aMaxStacks = 1)
        {
            StackPolicy = aPolicy;
            MaxStacks = aMaxStacks < 1 ? 1 : aMaxStacks;
            return this;
        }

        // 振る舞いを1つ追加する
        // aBehaviour : 追加する振る舞い
        public StatusEffect AddBehaviour(StatusEffectBehaviour aBehaviour)
        {
            if (aBehaviour == null) return this;
            mBehaviours.Add(aBehaviour);
            // 実行順を確定させておく(付与順に左右されないようにする)
            mBehaviours.Sort((x, y) => x.Order.CompareTo(y.Order));
            return this;
        }

        /* ライフサイクル */

        // 付与される。スタック数を1で初期化し、全Behaviourへ通知する
        internal void AttachTo(BattleUnit aOwner, BattleContext aContext)
        {
            Owner = aOwner;
            CurrentStacks = 1;
            ElapsedTicks = 0;
            var ctx = new StatusEffectContext(this, aOwner, aContext);
            foreach (var b in mBehaviours) b.OnApply(ctx);
        }

        // 除去される。全Behaviourへ通知したのちOwnerを外す
        internal void DetachFrom(BattleUnit aOwner, BattleContext aContext)
        {
            var ctx = new StatusEffectContext(this, aOwner, aContext);
            foreach (var b in mBehaviours) b.OnRemove(ctx);
            Owner = null;
        }

        // 次の発生を待つタイマーを覚える。除去や掛け直しのときに取り消せるようにする
        internal void SetTickTimer(PPBattleTimerScheduler aScheduler, PPBattleTimer aTimer)
        {
            mTickScheduler = aScheduler;
            mTickTimer = aTimer;
        }

        // 次の発生を待つタイマーを取り消す。待っていなければ何もしない
        internal void CancelTickTimer()
        {
            mTickScheduler?.Cancel(mTickTimer);
            mTickScheduler = null;
            mTickTimer = null;
        }

        // 効果が発生するたびに呼ばれる。全Behaviourへ通知する
        internal void Tick(BattleUnit aOwner, BattleContext aContext)
        {
            ElapsedTicks++;
            var ctx = new StatusEffectContext(this, aOwner, aContext);
            foreach (var b in mBehaviours)
            {
                b.OnTick(ctx);
                // 振る舞いの Tick（毒ダメージ等）で対象が撃破され、このエフェクトが除去されることがある
                // 除去済みのエフェクトから残りの振る舞いを走らせない
                if (Owner != aOwner) break;
            }
        }

        // 被ダメージ確定前に全Behaviourへ介入させる
        internal void NotifyIncomingDamage(BattleUnit aOwner, BattleContext aContext, DamageInfo aDamage)
        {
            var ctx = new StatusEffectContext(this, aOwner, aContext);
            foreach (var b in mBehaviours) b.ModifyIncomingDamage(ctx, aDamage);
        }

        // スタックを消費する。使い切った場合は true を返し、エフェクトの除去は呼び出し元（BattleUnit）が行う
        internal bool ConsumeStacks(int aStacks, BattleUnit aOwner, BattleContext aContext)
        {
            if (aStacks <= 0) return false;

            CurrentStacks -= aStacks;
            if (CurrentStacks <= 0)
            {
                CurrentStacks = 0;
                return true;
            }

            var ctx = new StatusEffectContext(this, aOwner, aContext);
            foreach (var b in mBehaviours) b.OnStackChanged(ctx);
            return false;
        }

        internal bool TryAddStack(BattleUnit aOwner, BattleContext aContext)
        {
            if (CurrentStacks >= MaxStacks) return false;
            CurrentStacks++;
            var ctx = new StatusEffectContext(this, aOwner, aContext);
            foreach (var b in mBehaviours) b.OnStackChanged(ctx);
            return true;
        }

        /* 問い合わせ */

        // 指定した種類の振る舞いを持っているかを調べる(UI・AIからの問い合わせ用)
        public bool HasBehaviour<T>() where T : StatusEffectBehaviour
        {
            foreach (var b in mBehaviours) if (b is T) return true;
            return false;
        }
    }

    public readonly struct StatusEffectContext
    {
        /* 状態 */

        // 対象のエフェクト本体
        public StatusEffect Effect { get; }
        // エフェクトがかかっているユニット
        public BattleUnit Owner { get; }
        // エフェクトを付与した側(不明な場合はnull)
        public BattleUnit Source { get; }
        // バトル全体の状況(乱数・ルールなど)。ApplyDamage(DamageInfo) 経由で渡された場合はnullになりうる
        public BattleContext Battle { get; }

        /* プロパティ */

        // 現在のスタック数
        public int Stacks => Effect.CurrentStacks;

        /* 生成 */

        public StatusEffectContext(StatusEffect aEffect, BattleUnit aOwner, BattleContext aBattle)
        {
            Effect = aEffect;
            Owner = aOwner;
            Source = aEffect?.Source;
            Battle = aBattle;
        }
    }

    // StatusEffect に差し込む振る舞いの基底
    // 必要なフックだけを override する。付与のたびに new して使う想定(インスタンスごとに状態を持ってよい)
    public abstract class StatusEffectBehaviour
    {
        /* プロパティ */

        // 同一フック内での実行順。小さいほど先に実行される
        public virtual int Order => 0;

        /* 操作 */

        // 被ダメージ確定前の介入
        public virtual void ModifyIncomingDamage(StatusEffectContext aContext, DamageInfo aDamage) { }

        /* イベントハンドラ */

        // 付与された瞬間
        public virtual void OnApply(StatusEffectContext aContext) { }

        // 除去された瞬間
        public virtual void OnRemove(StatusEffectContext aContext) { }

        // 効果が発生するたび（持続条件の発生間隔ごと）
        public virtual void OnTick(StatusEffectContext aContext) { }

        // スタック数が変化したとき
        public virtual void OnStackChanged(StatusEffectContext aContext) { }
    }
}
