/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file StatusEffect.cs
 * @author hqrse
 * @date 2026/07/31
 * @brief ステータスエフェクト本体(振る舞いの容器)
 * =====================================*/

using System.Collections.Generic;

namespace CommandBattleCore
{
    // ユニットに掛かる状態変化のランタイムインスタンス
    // 毒などの状態異常も、バフ・デバフも、防御状態も、すべてこの 1 つの型で表現する
    // 効き目そのものは StatusEffectBehaviour が持ち、このクラスは
    // 「誰が誰にかけた、何という名前の、いつまで続く効果か」だけを持つ
    public sealed class StatusEffect
    {
        // エフェクト ID。同一 ID が重ね掛け判定の単位になる
        public string EffectId { get; }
        // UI 表示名
        public string DisplayName { get; }

        // Coreが理解できる汎用分類
        public StatusEffectTag Tags { get; private set; } = StatusEffectTag.None;
        // ゲーム固有の分類。Coreは中身を解釈せずビット比較にのみ使う
        public long Category { get; private set; }

        // 効果が切れる条件。未指定なら永続
        public IDurationCondition DurationCondition { get; set; }
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
        // エフェクトがかかっているユニット。AttachTo時にCore側が設定する
        public BattleUnit Owner { get; private set; }
        // 生成元の定義アセットなど
        public object SourceDefinition { get; private set; }

        // このエフェクトが持つ振る舞いの一覧。Orderの昇順で並ぶ
        private readonly List<StatusEffectBehaviour> mBehaviours = new();
        public IReadOnlyList<StatusEffectBehaviour> Behaviours => mBehaviours;

        // aEffectId : エフェクト ID
        // aDisplayName : UI 表示名
        // aDurationCondition : 持続条件。null なら永続として扱う
        public StatusEffect(string aEffectId, string aDisplayName, IDurationCondition aDurationCondition = null)
        {
            EffectId = aEffectId;
            DisplayName = aDisplayName;
            DurationCondition = aDurationCondition ?? new PermanentDurationCondition();
        }

        /* ---- 組み立て(呼び出しを繋げられるよう自身を返す) ---- */

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

        // 指定した種類の振る舞いを持っているかを調べる(UI・AIからの問い合わせ用)
        public bool HasBehaviour<T>() where T : StatusEffectBehaviour
        {
            foreach (var b in mBehaviours) if (b is T) return true;
            return false;
        }

        /* ---- ライフサイクル(BattleUnitからのみ呼ばれる) ---- */

        // 付与される。スタック数を1で初期化し、全Behaviourへ通知する
        // aOwner : 付与先のユニット
        // aContext : バトルコンテキスト
        internal void AttachTo(BattleUnit aOwner, BattleContext aContext)
        {
            Owner = aOwner;
            CurrentStacks = 1;
            var ctx = new StatusEffectContext(this, aOwner, aContext);
            foreach (var b in mBehaviours) b.OnApply(ctx);
        }

        // 除去される。全Behaviourへ通知したのちOwnerを外す
        // aOwner : 除去元のユニット
        // aContext : バトルコンテキスト
        internal void DetachFrom(BattleUnit aOwner, BattleContext aContext)
        {
            var ctx = new StatusEffectContext(this, aOwner, aContext);
            foreach (var b in mBehaviours) b.OnRemove(ctx);
            Owner = null;
        }

        // 更新のたびに呼ばれる。全Behaviourへ通知する
        // aOwner : 対象のユニット
        // aContext : バトルコンテキスト
        internal void Tick(BattleUnit aOwner, BattleContext aContext)
        {
            var ctx = new StatusEffectContext(this, aOwner, aContext);
            foreach (var b in mBehaviours) b.OnTick(ctx);
        }

        // 被ダメージ確定前に全Behaviourへ介入させる
        // aOwner : ダメージを受けるユニット
        // aContext : バトルコンテキスト。呼び出し元がBattleContextを持たない場合はnull
        // aDamage : 介入対象のダメージ情報
        internal void NotifyIncomingDamage(BattleUnit aOwner, BattleContext aContext, DamageInfo aDamage)
        {
            var ctx = new StatusEffectContext(this, aOwner, aContext);
            foreach (var b in mBehaviours) b.ModifyIncomingDamage(ctx, aDamage);
        }

        // スタック数を1つ増やす
        // aOwner : 対象のユニット
        // aContext : バトルコンテキスト
        // return : 上限に達しておらず実際に増えた場合 true
        internal bool TryAddStack(BattleUnit aOwner, BattleContext aContext)
        {
            if (CurrentStacks >= MaxStacks) return false;
            CurrentStacks++;
            var ctx = new StatusEffectContext(this, aOwner, aContext);
            foreach (var b in mBehaviours) b.OnStackChanged(ctx);
            return true;
        }
    }
}
