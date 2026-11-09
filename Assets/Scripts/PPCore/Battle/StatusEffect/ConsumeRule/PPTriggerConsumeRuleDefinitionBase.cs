/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPTriggerConsumeRuleDefinitionBase.cs
 * @author hqrse
 * @date 2026/11/10
 * @brief きっかけごとのスタック消費ルール定義の共通基底
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 「きっかけが起きたら、確率でスタックを消費する」ルール定義の共通基底
    // きっかけは派生クラスが決めるため、型選択の段階できっかけを選ぶ形になる
    // 消費量・消費確率はきっかけを問わず共通のため、ここにまとめている
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
        // ChanceTriggerConsumeRule は乱数を引けない経路（BattleContext を伴わない被ダメージなど）では消費しないため、
        // 確率 1 のルールがその経路で消費されなくなるのを避けている
        // return : 生成された消費ルール
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
        // return : 要約文字列
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
}
