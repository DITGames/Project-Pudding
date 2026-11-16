/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStatusEffectConsumeRule.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief ステータスエフェクトのスタック消費ルール（ランタイムと定義）
 * =====================================*/

using System;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // スタックを消費するきっかけ
    // 持続条件（寿命）とは別の軸で、「何をしたら減るか」を表す
    public enum StatusEffectConsumeTrigger
    {
        // ターンが経過したとき。毎ターン少しずつ弱まる効果など
        [InspectorName("ターン経過")]
        TurnEnded,
        // 行動したとき。溜め状態を行動で使い切る表現など
        [InspectorName("行動時")]
        Acted,
        // 攻撃行動をしたとき。次の攻撃だけ強化する表現など
        [InspectorName("攻撃時")]
        Attacked,
        // ダメージを受けたとき。次に受けるダメージだけ変化させる表現など
        [InspectorName("被ダメージ時")]
        Damaged,
    }

    // 「どのきっかけで何スタック消費するか」を決めるルール
    // BattleUnit がきっかけの発生ごとに問い合わせ、スタックが 0 になるとエフェクトは除去される
    public interface IStatusEffectConsumeRule
    {
        // このきっかけで消費するスタック数を返す
        int ResolveConsumeStacks(StatusEffectConsumeTrigger aTrigger, StatusEffectContext aContext, DamageInfo aDamage);
    }

    // 指定したきっかけが起きたら決まった数だけ消費するルール
    // 「行動したら全部使い切る」「ダメージを受けたら1つ減る」といった確定的な消費に使う
    public class TriggerConsumeRule : IStatusEffectConsumeRule
    {
        // 消費のきっかけ
        protected readonly StatusEffectConsumeTrigger mTrigger;
        // 1 回あたりの消費スタック数
        protected readonly int mStacks;
        // true なら残りスタックをすべて消費する
        protected readonly bool mIsConsumeAll;
        // 対象とするダメージの種類。null なら種類を問わない
        // Damaged のきっかけでのみ意味を持つ（毒の継続ダメージでは消費させたくない場合などに使う）
        protected readonly DamageReasonKind? mDamageReasonFilter;
        // true ならリアクション（反撃など）として発生したダメージも対象にする
        // 種類を限定している場合（mDamageReasonFilter が null でない場合）のみ意味を持つ
        protected readonly bool mIsIncludeReactionDamage;

        public TriggerConsumeRule(StatusEffectConsumeTrigger aTrigger, int aStacks = 1, bool aIsConsumeAll = false,
            DamageReasonKind? aDamageReasonFilter = null, bool aIsIncludeReactionDamage = false)
        {
            mTrigger = aTrigger;
            mStacks = aStacks;
            mIsConsumeAll = aIsConsumeAll;
            mDamageReasonFilter = aDamageReasonFilter;
            mIsIncludeReactionDamage = aIsIncludeReactionDamage;
        }

        public virtual int ResolveConsumeStacks(StatusEffectConsumeTrigger aTrigger, StatusEffectContext aContext,
            DamageInfo aDamage)
        {
            if (aTrigger != mTrigger) return 0;
            if (!MatchesDamageReason(aDamage)) return 0;
            return mIsConsumeAll ? aContext.Stacks : mStacks;
        }

        // ダメージの種類が対象に合致するかを判定する
        // 被ダメージ以外のきっかけでは常に合致とみなす
        protected bool MatchesDamageReason(DamageInfo aDamage)
        {
            if (mDamageReasonFilter == null) return true;
            if (mTrigger != StatusEffectConsumeTrigger.Damaged) return true;

            // 種類を限定しているのに情報が無い場合は、意図しない消費を避けて合致しない扱いにする
            if (aDamage == null) return false;
            // 反撃などのダメージは、含める指定がある場合だけ対象にする
            if (aDamage.IsReaction && !mIsIncludeReactionDamage) return false;
            return DamageReasonKindUtility.Matches(aDamage.Reason, mDamageReasonFilter.Value);
        }
    }

    // 指定したきっかけが起きたときに確率で消費するルール
    // 確率は付与からの経過ターン数に応じて増減させられる（時間が経つほど解けやすい状態異常など）
    public sealed class ChanceTriggerConsumeRule : TriggerConsumeRule
    {
        // 経過ターン数 0 の時点での消費確率（0～1）
        private readonly float mBaseChance;
        // 経過ターン 1 つあたりの確率の増分。負の値なら経つほど解けにくくなる
        private readonly float mChancePerElapsedTurn;

        public ChanceTriggerConsumeRule(StatusEffectConsumeTrigger aTrigger, float aBaseChance,
            float aChancePerElapsedTurn = 0f, int aStacks = 1, bool aIsConsumeAll = false,
            DamageReasonKind? aDamageReasonFilter = null, bool aIsIncludeReactionDamage = false)
            : base(aTrigger, aStacks, aIsConsumeAll, aDamageReasonFilter, aIsIncludeReactionDamage)
        {
            mBaseChance = aBaseChance;
            mChancePerElapsedTurn = aChancePerElapsedTurn;
        }

        public override int ResolveConsumeStacks(StatusEffectConsumeTrigger aTrigger, StatusEffectContext aContext,
            DamageInfo aDamage)
        {
            if (aTrigger != mTrigger) return 0;
            if (!MatchesDamageReason(aDamage)) return 0;

            // 乱数を引けない状況（BattleContext を伴わない経路）では消費しない側へ倒す
            // 勝手に効果が切れるより、切れないまま残る方が復帰しやすいため
            var random = aContext.Battle?.Rules?.RandomProvider;
            if (random == null) return 0;

            float chance = Mathf.Clamp01(mBaseChance + mChancePerElapsedTurn * aContext.Effect.ElapsedTurns);
            if (random.NextFloat() >= chance) return 0;

            return mIsConsumeAll ? aContext.Stacks : mStacks;
        }
    }

    // 「何をきっかけに、何スタック消費するか」をインスペクタ上で組み立てるためのデータ定義
    [Serializable]
    public abstract class PPStatusEffectConsumeRuleDefinition
    {
        // この定義から消費ルールのランタイムインスタンスを生成する
        // return : 生成された消費ルール
        public abstract IStatusEffectConsumeRule CreateConsumeRule();

        // フィールドラベルに表示する、この消費ルールの内容を要約した文字列を組み立てる
        public abstract string BuildString();

        // ダメージ種別の日本語表示名を得る
        protected static string ToDisplayName(DamageReasonKind aKind)
            => aKind switch
            {
                DamageReasonKind.Attack => "攻撃",
                DamageReasonKind.StatusEffect => "継続ダメージ",
                DamageReasonKind.Other => "その他",
                _ => aKind.ToString(),
            };

        // きっかけの日本語表示名を得る
        protected static string ToDisplayName(StatusEffectConsumeTrigger aTrigger)
            => aTrigger switch
            {
                StatusEffectConsumeTrigger.TurnEnded => "ターン経過",
                StatusEffectConsumeTrigger.Acted => "行動",
                StatusEffectConsumeTrigger.Attacked => "攻撃",
                StatusEffectConsumeTrigger.Damaged => "被ダメージ",
                _ => aTrigger.ToString(),
            };
    }

    // 「きっかけが起きたら、確率でスタックを消費する」ルール定義の共通基底
    // きっかけは派生クラスが決め、消費量・消費確率はここにまとめる
    [Serializable]
    public abstract class PPTriggerConsumeRuleDefinitionBase : PPStatusEffectConsumeRuleDefinition
    {
        [Label("全て消費する")]
        [SerializeField]protected bool mIsConsumeAll = true;
        // 全て消費する場合は参照されない
        [Label("消費スタック数")][Min(1)]
        [SerializeField]protected int mConsumeStacks = 1;
        // 付与直後（経過ターン 0）の消費確率（0～1）。1 なら必ず消費する
        [Label("消費確率")][Range(0f, 1f)]
        [SerializeField]protected float mBaseChance = 1f;
        // 経過ターン 1 つあたりの確率の増分。負の値にすると経つほど解けにくくなる
        [Label("1ターンあたりの増分")][Range(-1f, 1f)]
        [SerializeField]protected float mChancePerElapsedTurn = 0f;

        // 消費のきっかけ
        protected abstract StatusEffectConsumeTrigger Trigger { get; }

        // 対象とするダメージの種類。null なら種類を問わない。被ダメージ時のきっかけだけが上書きする
        protected virtual DamageReasonKind? DamageReasonFilter => null;

        // 種類を限定する場合に、リアクション（反撃など）のダメージも対象にするか。被ダメージ時のきっかけだけが上書きする
        protected virtual bool IsIncludeReactionDamage => false;

        // 確率が常に 1 以上になり、乱数を引く必要が無いか
        // 増分が負だと経過ターンで 1 を下回るため、確定扱いにしない
        protected bool IsCertain => Mathf.Clamp01(mBaseChance) >= 1f && mChancePerElapsedTurn >= 0f;

        // 確定なら乱数を引かない TriggerConsumeRule を、そうでなければ ChanceTriggerConsumeRule を生成する
        // 乱数を引けない経路でも、確率 1 のルールが消費されるようにするため
        public override IStatusEffectConsumeRule CreateConsumeRule()
        {
            int stacks = Mathf.Max(1, mConsumeStacks);
            if (IsCertain)
            {
                return new TriggerConsumeRule(Trigger, stacks, mIsConsumeAll, DamageReasonFilter, IsIncludeReactionDamage);
            }

            return new ChanceTriggerConsumeRule(Trigger, Mathf.Clamp01(mBaseChance), mChancePerElapsedTurn,
                stacks, mIsConsumeAll, DamageReasonFilter, IsIncludeReactionDamage);
        }

        // ラベルに出す要約を組み立てる。例: 「攻撃時に全消費」「行動時に33%で全消費 毎ターン+10%」
        // 確定の場合は確率と増分を省く
        public override string BuildString()
        {
            string amount = mIsConsumeAll ? "全消費" : $"{Mathf.Max(1, mConsumeStacks)}消費";
            if (IsCertain)
            {
                return $"{BuildTriggerString()}時に{amount}";
            }

            string curve = Mathf.Approximately(mChancePerElapsedTurn, 0f)
                ? string.Empty
                : $" 毎ターン{mChancePerElapsedTurn:+0.##%;-0.##%}";
            return $"{BuildTriggerString()}時に{Mathf.Clamp01(mBaseChance):P0}で{amount}{curve}";
        }

        // きっかけ部分の表示文字列を組み立てる。条件を添えたい派生クラスが上書きする
        // return : 表示文字列
        protected virtual string BuildTriggerString() => ToDisplayName(Trigger);
    }

    // ターンが経過したときにスタックを消費するルールの定義
    // 「毎ターン少しずつ弱まる」効果に使う
    [Serializable]
    [PPTypeMenuName("ターン経過時")]
    public class PPTurnEndedConsumeRuleDefinition : PPTriggerConsumeRuleDefinitionBase
    {
        protected override StatusEffectConsumeTrigger Trigger => StatusEffectConsumeTrigger.TurnEnded;
    }

    // 行動したときにスタックを消費するルールの定義
    // 「行動したら溜めを使い切る」や、確率と組み合わせて「行動しようとするたびに一定確率で解ける」混乱などに使う
    [Serializable]
    [PPTypeMenuName("行動時")]
    public class PPActedConsumeRuleDefinition : PPTriggerConsumeRuleDefinitionBase
    {
        protected override StatusEffectConsumeTrigger Trigger => StatusEffectConsumeTrigger.Acted;
    }

    // 攻撃行動をしたときにスタックを消費するルールの定義
    // 「次の攻撃だけ強化する」といった効果に使う
    [Serializable]
    [PPTypeMenuName("攻撃時")]
    public class PPAttackedConsumeRuleDefinition : PPTriggerConsumeRuleDefinitionBase
    {
        protected override StatusEffectConsumeTrigger Trigger => StatusEffectConsumeTrigger.Attacked;
    }

    // ダメージを受けたときにスタックを消費するルールの定義（「次に受けるダメージだけ変化させる」効果に使う）
    // 既定では攻撃によるダメージだけを対象にし、継続ダメージや反撃のダメージでは消費しない
    [Serializable]
    [PPTypeMenuName("被ダメージ時")]
    public class PPDamagedConsumeRuleDefinition : PPTriggerConsumeRuleDefinitionBase
    {
        [Header("被ダメージの条件")]
        [Label("ダメージ種別を限定する")]
        [SerializeField]protected bool mIsFilterDamageReason = true;
        [Label("対象ダメージ種別")]
        [EditCondition(nameof(mIsFilterDamageReason))]
        [SerializeField]protected DamageReasonKind mDamageReason = DamageReasonKind.Attack;
        // 種別を限定しない場合は反撃のダメージも常に対象になるため、限定するときだけ編集できる
        [Label("反撃のダメージも含める")]
        [EditCondition(nameof(mIsFilterDamageReason))]
        [SerializeField]protected bool mIsIncludeReactionDamage = false;

        protected override StatusEffectConsumeTrigger Trigger => StatusEffectConsumeTrigger.Damaged;

        // 限定しない場合は null を返し、種別を問わず消費させる
        protected override DamageReasonKind? DamageReasonFilter => mIsFilterDamageReason ? mDamageReason : null;

        protected override bool IsIncludeReactionDamage => mIsIncludeReactionDamage;

        // 種別を限定している場合は、その種別と反撃を含めるかも添える
        // return : 表示文字列
        protected override string BuildTriggerString()
            => DamageReasonFilter.HasValue
                ? $"{ToDisplayName(Trigger)}（{ToDisplayName(mDamageReason)}{(mIsIncludeReactionDamage ? "・反撃含む" : string.Empty)}）"
                : ToDisplayName(Trigger);
    }
}
