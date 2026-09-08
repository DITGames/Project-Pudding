/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file IStatusEffectConsumeRule.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief ステータスエフェクトのスタック消費ルール
 * =====================================*/

using UnityEngine;

namespace CommandBattleCore
{
    // スタックを消費するきっかけ
    // 持続条件（IDurationCondition）が「寿命」を表すのに対し、こちらは「何をしたら減るか」を表す
    // スタック数は効果量の倍率としても使われるため、両者は別の軸として扱う
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
    // BattleUnit がきっかけの発生ごとに問い合わせ、返された数だけスタックを減らす
    // スタックが 0 になった時点でエフェクトは除去される
    public interface IStatusEffectConsumeRule
    {
        // このきっかけで消費するスタック数を返す
        // aTrigger : 発生したきっかけ
        // aContext : 対象エフェクトの状況（スタック数・経過ターン数・乱数へはここから辿る）
        // aDamage : Damaged のきっかけで渡されるダメージ情報。それ以外のきっかけでは null
        // return : 消費するスタック数。0 なら消費しない
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
        protected readonly DamageReason? mDamageReasonFilter;

        // aTrigger : 消費のきっかけ
        // aStacks : 1 回あたりの消費スタック数
        // aIsConsumeAll : 残り全部を消費するか
        // aDamageReasonFilter : 対象とするダメージの種類。null なら問わない
        public TriggerConsumeRule(StatusEffectConsumeTrigger aTrigger, int aStacks = 1, bool aIsConsumeAll = false,
            DamageReason? aDamageReasonFilter = null)
        {
            mTrigger = aTrigger;
            mStacks = aStacks;
            mIsConsumeAll = aIsConsumeAll;
            mDamageReasonFilter = aDamageReasonFilter;
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
        // aDamage : 判定するダメージ情報
        // return : 消費してよい場合 true
        protected bool MatchesDamageReason(DamageInfo aDamage)
        {
            if (mDamageReasonFilter == null) return true;
            if (mTrigger != StatusEffectConsumeTrigger.Damaged) return true;

            // 種類を限定しているのに情報が無い場合は、意図しない消費を避けて合致しない扱いにする
            return aDamage != null && aDamage.Reason == mDamageReasonFilter.Value;
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

        // aTrigger : 消費のきっかけ
        // aBaseChance : 経過ターン 0 の時点での消費確率（0～1）
        // aChancePerElapsedTurn : 経過ターン 1 つあたりの確率の増分
        // aStacks : 1 回あたりの消費スタック数
        // aIsConsumeAll : 残り全部を消費するか
        // aDamageReasonFilter : 対象とするダメージの種類。null なら問わない
        public ChanceTriggerConsumeRule(StatusEffectConsumeTrigger aTrigger, float aBaseChance,
            float aChancePerElapsedTurn = 0f, int aStacks = 1, bool aIsConsumeAll = false,
            DamageReason? aDamageReasonFilter = null)
            : base(aTrigger, aStacks, aIsConsumeAll, aDamageReasonFilter)
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
}
