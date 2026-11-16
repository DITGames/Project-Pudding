/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitSkillConditions.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニット条件（スキル）
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // ユニット条件: 同種グループ・タグで絞り込んだスキルを持っているか
    // 発動可否は見ないため、「攻撃役かどうか」「回復役かどうか」という役割の判定になる
    // 判断ツリーの入口で役割ごとに枝を分けるのに使う
    [Serializable]
    [PPTypeMenuName("スキル/所持している")]
    public sealed class PPUnitHasSkillCondition : PPUnitConditionValidator, IPPUnitAISkillFilterOwner
    {
        [Label("対象スキル")]
        [SerializeField] private PPUnitAISkillFilter mFilter = new();
        // 反転すると「そのスキルを持っていない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 保持しているスキルの絞り込み条件。エディタの診断から参照する
        public PPUnitAISkillFilter Filter => mFilter;

        // 絞り込みに合致するスキルを所持しているかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
            => PPUnitAISkillQuery.HasAny(aUnit, mFilter) != mIsInvert;

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string filter = mFilter.ToDisplayString();
            mDescription = mIsInvert ? $"{filter} のスキルを持っていない" : $"{filter} のスキルを持っている";
        }
    }

    // ユニット条件: 指定タグのスキルを持っているか
    // 発動可否は見ないため、「大技を持っている担当がいるか」のような編成上の役割判定に使う
    // 今すぐ撃てるかまで見たい場合は PPUnitSkillCastableCondition を使う
    [Serializable]
    [PPTypeMenuName("スキル/指定タグを保持")]
    public sealed class PPUnitHasSkillTagCondition : PPUnitConditionValidator
    {
        [Label("対象スキルタグ", true)]
        [SerializeField] private List<PPSkillTagDefinition> mTags = new();
        // 反転すると「そのタグのスキルを持っていない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 指定タグのスキルを持っているかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
            => PPSkillTagUtility.HasTaggedSkill(aUnit, mTags) != mIsInvert;

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string tags = PPSkillTagUtility.ToDisplayString(mTags);
            mDescription = mIsInvert ? $"{tags} のスキルを持っていない" : $"{tags} のスキルを持っている";
        }
    }

    // ユニット条件: 同種グループ・タグで絞り込んだスキルを今すぐ発動できるか
    // クールダウン・使用回数・スキルゲージ残量まで含めて判定する
    // 「所持している中で最も強いものに限る」を有効にすると、
    // 下位スキルで妥協せず本命が撃てるときだけ成立する（待ちの判断と組み合わせて使う）
    [Serializable]
    [PPTypeMenuName("スキル/発動できる")]
    public sealed class PPUnitCanCastSkillCondition : PPUnitConditionValidator, IPPUnitAISkillFilterOwner
    {
        [Label("対象スキル")]
        [SerializeField] private PPUnitAISkillFilter mFilter = new();
        // 絞り込んだ中で最も AI スコアの高いスキルが発動できる場合だけ成立させるか
        [Label("最も強いものに限る")]
        [SerializeField] private bool mIsStrongestOnly = false;

        // 保持しているスキルの絞り込み条件。エディタの診断から参照する
        public PPUnitAISkillFilter Filter => mFilter;
        // 反転すると「そのスキルが撃てない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 絞り込みに合致するスキルが発動可能かを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            bool isCastable = mIsStrongestOnly
                ? IsStrongestCastable(aUnit, aSnapShot)
                : PPUnitAISkillQuery.SelectCastable(aUnit, mFilter,
                    PPUnitAISkillSelectRule.HighestAIScore, aSnapShot.Context, aSnapShot.Ledger).Skill != null;

            return isCastable != mIsInvert;
        }

        // 絞り込んだ中で最も強いスキルが、今まさに発動できるかを判定する
        // 所持している最強スキルと、今撃てる中の最強スキルが同一かどうかで見る
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 最強スキルが発動可能なら true
        private bool IsStrongestCastable(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            var strongest = PPUnitAISkillQuery.SelectStrongest(aUnit, mFilter);
            if (strongest == null) return false;

            var (_, castable) = PPUnitAISkillQuery.SelectCastable(aUnit, mFilter,
                PPUnitAISkillSelectRule.HighestAIScore, aSnapShot.Context, aSnapShot.Ledger);
            return ReferenceEquals(castable, strongest);
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string filter = mFilter.ToDisplayString();
            string scope = mIsStrongestOnly ? "で最も強いスキル" : " のスキル";
            mDescription = mIsInvert ? $"{filter}{scope}が撃てない" : $"{filter}{scope}が撃てる";
        }
    }

    // ユニット条件: 指定タグのスキルを今すぐ発動できるか
    // クールダウン・使用回数・スキルゲージ残量まで含めて判定する
    // ゲージ残量まで見るため、この条件を発動条件に入れると
    // 「溜めてから撃つ」判断が成立しなくなる点に注意（その場合は保持判定の方を使う）
    [Serializable]
    [PPTypeMenuName("スキル/指定タグが発動可能")]
    public sealed class PPUnitSkillCastableCondition : PPUnitConditionValidator
    {
        [Label("対象スキルタグ", true)]
        [SerializeField] private List<PPSkillTagDefinition> mTags = new();
        // 反転すると「そのタグのスキルを撃てない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 指定タグのスキルが発動可能かを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
            => PPSkillTagUtility.HasCastableTaggedSkill(aUnit, mTags, aSnapShot.Context) != mIsInvert;

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string tags = PPSkillTagUtility.ToDisplayString(mTags);
            mDescription = mIsInvert ? $"{tags} のスキルを撃てない" : $"{tags} のスキルを撃てる";
        }
    }

    // ユニット条件: 指定ティック数だけ待てば、今撃てるものより強いスキルへ手が届くか
    //
    // 見積もりは PPUnitGaugeForecast に委ねる
    // 手持ちのコインゲージで通常攻撃を何回撃てるかを数え、
    // その回数ぶんのスキルゲージ回復で上位スキルのコストへ届くかを見る
    // 「今は弱いスキルを撃たずに通常攻撃で溜める」という判断の根拠になる
    //
    // 今撃てるスキルが 1 つも無い場合も、上位スキルへ届くなら成立する（溜める価値があるため）
    [Serializable]
    [PPTypeMenuName("スキル/待てば強いスキルが撃てる")]
    public sealed class PPUnitStrongerSkillReachableCondition : PPUnitConditionValidator, IPPUnitAISkillFilterOwner
    {
        [Label("対象スキル")]
        [SerializeField] private PPUnitAISkillFilter mFilter = new();
        [Label("待てるティック数")]
        [SerializeField] private int mAllowedWaitTicks = 3;
        // この差より AI スコアが上でなければ「強い」とみなさない
        // わずかな差のために手を止めるのを防ぐ
        [Label("必要なAIスコア差")]
        [SerializeField] private float mRequiredScoreGain = 0.01f;
        // 反転すると「待っても届かない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 保持しているスキルの絞り込み条件。エディタの診断から参照する
        public PPUnitAISkillFilter Filter => mFilter;

        // 待てば今より強いスキルへ届くかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            return IsReachable(aUnit, aSnapShot) != mIsInvert;
        }

        // 待機で手が届く上位スキルがあるかを調べる
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 届く上位スキルがあれば true
        private bool IsReachable(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            var rules = aSnapShot.Context.Rules as PPBattleRules;

            // 今撃てる中で最も強いものを基準にする。1 つも撃てないなら基準スコアは持たない
            var (_, current) = PPUnitAISkillQuery.SelectCastable(aUnit, mFilter,
                PPUnitAISkillSelectRule.HighestAIScore, aSnapShot.Context, aSnapShot.Ledger);
            float currentScore = current?.AIScore ?? float.NegativeInfinity;

            foreach (var (skill, definition) in PPUnitAISkillQuery.Enumerate(aUnit, mFilter))
            {
                // 今のゲージで足りているものは「待つ対象」ではない
                if (PPGaugeUtility.CanPay(aUnit.ExtraParameters.SkillGauge, definition.SkillGaugeCost)) continue;
                // クールダウンや使用回数で撃てないものは、ゲージが溜まっても撃てないので除く
                if (!skill.IsReady) continue;
                // 待つ価値があるだけ強くなければ見送る
                if (definition.AIScore - currentScore < mRequiredScoreGain) continue;

                int ticks = PPUnitGaugeForecast.EstimateTicksToReach(aUnit, rules, definition.SkillGaugeCost,
                    Mathf.Max(1, mAllowedWaitTicks));
                if (ticks >= 0) return true;
            }
            return false;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string filter = mFilter.ToDisplayString();
            mDescription = mIsInvert
                ? $"{mAllowedWaitTicks}ティック待っても {filter} の強いスキルに届かない"
                : $"{mAllowedWaitTicks}ティック待てば {filter} の強いスキルが撃てる";
        }
    }
}
