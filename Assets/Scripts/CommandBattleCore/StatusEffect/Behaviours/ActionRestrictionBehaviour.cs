/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file ActionRestrictionBehaviour.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief 行動制限（行動不能・沈黙など）を課す振る舞い
 * =====================================*/

using UnityEngine;

namespace CommandBattleCore
{
    // 掛かっている間、対象へ行動制限を課す振る舞い
    // 制限そのものは StatusEffect.Restriction / ActionFailChance が持ち、
    // BattleUnit.CurrentRestrictions と RollActionBlocked が参照する
    // 本振る舞いはその値を組み立てて設定する役割を持つ
    //
    // 失敗確率を経過ターン数で変化させられるため、
    // 「掛かった直後ほど動けない麻痺」「時間が経つほど目覚めやすい睡眠」なども表現できる
    // 1 つのエフェクトに複数の行動制限を積むと ActionFailChance が上書きし合うため、
    // 制限は 1 エフェクトにつき 1 つを想定している
    public sealed class ActionRestrictionBehaviour : StatusEffectBehaviour
    {
        // 課す行動制限
        private readonly ActionRestriction mRestriction;
        // true なら確率抽選せず必ず行動を阻害する（睡眠など）
        private readonly bool mIsAlwaysBlock;
        // 経過ターン 0 の時点での行動阻害確率（0～1）
        private readonly float mBaseChance;
        // 経過ターン 1 つあたりの確率の増分。負の値なら経つほど動けるようになる
        private readonly float mChancePerElapsedTurn;

        // aRestriction : 課す行動制限
        // aIsAlwaysBlock : 確率抽選せず必ず阻害するか
        // aBaseChance : 経過ターン 0 の時点での行動阻害確率
        // aChancePerElapsedTurn : 経過ターン 1 つあたりの確率の増分
        public ActionRestrictionBehaviour(ActionRestriction aRestriction, bool aIsAlwaysBlock = true,
            float aBaseChance = 1f, float aChancePerElapsedTurn = 0f)
        {
            mRestriction = aRestriction;
            mIsAlwaysBlock = aIsAlwaysBlock;
            mBaseChance = aBaseChance;
            mChancePerElapsedTurn = aChancePerElapsedTurn;
        }

        // 付与された時点で有効にする。行動判定は付与直後から走るため OnTick 任せにはしない
        public override void OnApply(StatusEffectContext aContext) => ApplyRestriction(aContext);
        // 経過ターン数で確率が変わるため、更新のたびに設定し直す
        public override void OnTick(StatusEffectContext aContext) => ApplyRestriction(aContext);

        // 現在の経過ターン数から確率を求め、エフェクトへ行動制限を設定する
        // aContext : 振る舞いの実行コンテキスト
        private void ApplyRestriction(StatusEffectContext aContext)
        {
            var effect = aContext.Effect;
            if (effect == null) return;

            // 確定阻害は ActionFailChance に null を入れる（RollActionBlocked が無条件で阻害と判定する）
            float? chance = mIsAlwaysBlock
                ? null
                : Mathf.Clamp01(mBaseChance + mChancePerElapsedTurn * effect.ElapsedTurns);

            // 他の振る舞いが積んだ制限を消さないよう、既存のフラグへ足す形で設定する
            effect.WithRestriction(effect.Restriction | mRestriction, chance);
        }
    }
}
