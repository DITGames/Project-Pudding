/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitGaugeConditions.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニット条件（ゲージ・行動回数）
 * =====================================*/

using System;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // ユニット条件: そのユニットのゲージ残量を絶対値で判定する
    // 「特定のスキルを撃つのに必要な量が溜まったか」のように、
    // 具体的なコスト量と突き合わせたい場合はこちらを使う（割合で見るなら PPUnitGaugeRatioCondition）
    [Serializable]
    [PPTypeMenuName("ゲージ/残量(絶対値)")]
    public sealed class PPUnitGaugeAmountCondition : PPUnitConditionValidator
    {
        [Label("対象ゲージ")]
        [SerializeField] private PPGaugeKind mKind = PPGaugeKind.Skill;
        [Label("比較")]
        [SerializeField] private PPCompareOp mOp = PPCompareOp.GreaterOrEqual;
        [Label("ゲージ量")]
        [SerializeField] private float mThreshold = 20f;
        // 等値判定の許容誤差。等値・非等値のときのみ表示される
        [Label("許容値")]
        [EditCondition("IsEqualOp", true, false)]
        [SerializeField] private float mTolerance = 1f;

        // 許容値の入力欄を出すかどうか（等値系の演算子でのみ意味を持つ）
        private bool IsEqualOp
            => mOp == PPCompareOp.Equal || mOp == PPCompareOp.NotEqual;

        // ゲージの現在値を閾値と比較する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
            => aUnit != null
            && PPConditionMath.Compare(aUnit.ExtraParameters.Gauge(mKind).Current, mOp, mThreshold, mTolerance);

        // 設定内容から説明文を組み立てる。等値系のときは許容値も併記する
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            mDescription = $"{PPGaugeUtility.ToDisplayString(mKind)}が{mThreshold}{GetOpString(mOp)}";

            if (IsEqualOp)
            {
                mDescription += $" 許容値({mTolerance})";
            }
        }

        // 説明文の語尾を自然な日本語にするため、等値系のみ表記を差し替える
        // aOp : 比較演算子
        protected override string GetOpString(PPCompareOp aOp)
            => aOp switch
            {
                PPCompareOp.Equal => "と等しい",
                PPCompareOp.NotEqual => "と等しくない",
                _ => base.GetOpString(aOp)
            };
    }

    // ユニット条件: そのユニットのゲージが上限に対して何割溜まっているか
    // 絶対量で見る PPUnitGaugeAmountCondition と違い、上限に対する充足率で判定する
    // 「ゲージが満タンに近いので大技を狙う」といった状況判断に使う
    // 閾値・許容値ともに 0～1 で扱う
    [Serializable]
    [PPTypeMenuName("ゲージ/残量(割合)")]
    public sealed class PPUnitGaugeRatioCondition : PPUnitConditionValidator
    {
        [Label("対象ゲージ")]
        [SerializeField] private PPGaugeKind mKind = PPGaugeKind.Skill;
        [Label("比較")]
        [SerializeField] private PPCompareOp mOp = PPCompareOp.GreaterOrEqual;
        [PercentLabel("割合")]
        [SerializeField] private float mThreshold = 1f;
        // 等値判定の許容誤差（0〜1）。等値・非等値のときのみ表示される
        [Label("許容値")]
        [EditCondition("IsEqualOp", true, false)]
        [SerializeField] private float mTolerance = 0.01f;

        // 許容値の入力欄を出すかどうか（等値系の演算子でのみ意味を持つ）
        private bool IsEqualOp
            => mOp == PPCompareOp.Equal || mOp == PPCompareOp.NotEqual;

        // ゲージの充足率を閾値と比較する
        // 上限が 0 の場合は ResourceParameter.Ratio が 0 を返すため、0 として扱われる
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
            => aUnit != null
            && PPConditionMath.Compare(aUnit.ExtraParameters.Gauge(mKind).Ratio, mOp, mThreshold, mTolerance);

        // 設定内容から説明文を組み立てる。等値系のときは許容値も併記する
        // 値は 0〜1 で保持しているが、説明文は読みやすさを優先してパーセント表記にする
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            mDescription = $"{PPGaugeUtility.ToDisplayString(mKind)}が{mThreshold * 100f:0.#}%{GetOpString(mOp)}";

            if (IsEqualOp)
            {
                mDescription += $" 許容値({mTolerance * 100f:0.#}%)";
            }
        }

        // 説明文の語尾を自然な日本語にするため、等値系のみ表記を差し替える
        // aOp : 比較演算子
        protected override string GetOpString(PPCompareOp aOp)
            => aOp switch
            {
                PPCompareOp.Equal => "と等しい",
                PPCompareOp.NotEqual => "と等しくない",
                _ => base.GetOpString(aOp)
            };
    }

    // ユニット条件: 撃つ予定のスキルを撃っても、残したいスキル（主スキル）の分のスキルゲージが残るか
    //
    // 支援型・回復型のツリーで「攻撃スキルは、支援や回復のゲージを食いつぶさない範囲でだけ撃つ」を書くためのもの
    // 割合や絶対量の閾値と違い、ユニット自身の所持スキルからコストを読むため、構成やゲージ上限が違っても成り立つ
    //
    // 残す量は、残したいスキルのうち所持していて使用回数が残っているもののコストの最大値
    // クールダウン中のものは残す量に含める（明けた時にすぐ撃てるよう溜めておくため）。該当が無ければ残す量は 0
    //
    // 撃つ予定のスキルは、スキル実行ノードと同じ選び方（PPUnitAISkillQuery.SelectCastable）で求める
    // 判定を揃えるため、絞り込みと選び方は隣に置いた実行ノードと同じ値にすること（条件からノードは参照できない）
    [Serializable]
    [PPTypeMenuName("ゲージ/主スキル分を残せる")]
    public sealed class PPUnitGaugeReserveCondition : PPUnitConditionValidator, IPPUnitAISkillFilterOwner
    {
        [Label("撃つ予定のスキル")]
        [SerializeField] private PPUnitAISkillFilter mSpendFilter = new();
        [Label("撃つ予定のスキルの選び方")]
        [SerializeField] private PPUnitAISkillSelectRule mSpendRule = PPUnitAISkillSelectRule.LowestGaugeCost;
        [Label("残したいスキル")]
        [SerializeField] private PPUnitAISkillFilter mReserveFilter = new(PPSkillGroup.Heal);
        // 反転すると「残らない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 残したいスキルの絞り込み条件。エディタの診断から参照する
        public PPUnitAISkillFilter Filter => mReserveFilter;

        // 撃つ予定のスキルを撃っても、残したいスキルの分のゲージが残るかを判定する
        // 撃つ予定のスキルが撃てない場合は成立しない
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            float spendCost = ResolveSpendCost(aUnit, aSnapShot);
            if (spendCost < 0f) return false;

            // 同じティックで既に積んだ行動の分は、台帳から差し引いた残量で見る
            float remaining = aSnapShot.Ledger != null
                ? aSnapShot.Ledger.RemainingSkillGauge(aUnit)
                : aUnit.ExtraParameters.SkillGauge.Current;
            // 浮動小数の誤差でちょうど残る場合を落とさないよう、発動可否の判定と同じ許容誤差を足す
            bool isReserved = remaining - spendCost + PPGaugeUtility.CompareEpsilon >= ResolveReserveCost(aUnit);
            return isReserved != mIsInvert;
        }

        // 撃つ予定のスキルのコストを求める
        // 選び方がランダムの場合は、乱数を使わず候補の最大コストで見る
        // 条件の評価で乱数を消費すると、実行ノードの抽選結果がずれるため
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 撃つ予定のスキルのコスト。撃てるスキルが無ければ -1
        private float ResolveSpendCost(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (mSpendRule != PPUnitAISkillSelectRule.Random)
            {
                var (_, definition) = PPUnitAISkillQuery.SelectCastable(aUnit, mSpendFilter, mSpendRule,
                    aSnapShot.Context, aSnapShot.Ledger);
                return definition != null ? definition.SkillGaugeCost : -1f;
            }

            var (_, highest) = PPUnitAISkillQuery.SelectCastable(aUnit, mSpendFilter,
                PPUnitAISkillSelectRule.HighestGaugeCost, aSnapShot.Context, aSnapShot.Ledger);
            return highest != null ? highest.SkillGaugeCost : -1f;
        }

        // 残す量を求める。所持している残したいスキルのうち、使用回数が残っているもののコストの最大値
        // aUnit : 判定対象のユニット
        // return : 残す量。該当するスキルが無ければ 0
        private float ResolveReserveCost(PPBattleUnit aUnit)
        {
            float reserve = 0f;
            foreach (var (skill, definition) in PPUnitAISkillQuery.Enumerate(aUnit, mReserveFilter))
            {
                // このバトルでもう撃てないものは残しても意味が無い
                if (skill.MaxUsesPerBattle > 0 && skill.UsesRemaining <= 0) continue;

                reserve = Mathf.Max(reserve, definition.SkillGaugeCost);
            }
            return reserve;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string spend = mSpendFilter.ToDisplayString();
            string reserve = mReserveFilter.ToDisplayString();
            mDescription = mIsInvert
                ? $"{spend} のスキルを撃つと {reserve} のスキル分が残らない"
                : $"{spend} のスキルを撃っても {reserve} のスキル分が残る";
        }
    }

    // ユニット条件: このティックで残っている行動回数
    //
    // 行動回数が 2 以上のユニットで「最後の 1 手は必ず攻撃で締める」のような書き分けに使う
    //
    // 残り回数は仮押さえ台帳を差し引いた値で見る
    // 同じティックで既に積んだ行動を数えないと、2 手目の判断が 1 手目と同じ値を見てしまい書き分けができない
    // 台帳が差し込まれていない場合は、行動回数の上限をそのまま残り回数として扱う
    [Serializable]
    [PPTypeMenuName("行動/残り行動回数")]
    public sealed class PPUnitRemainingActionCondition : PPUnitConditionValidator
    {
        [Label("比較")]
        [SerializeField] private PPCompareOp mOp = PPCompareOp.GreaterOrEqual;
        [Label("行動回数")]
        [SerializeField] private int mThreshold = 1;

        // 残り行動回数を閾値と比較する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            int total = aUnit.ResolveActionCount();
            int reserved = aSnapShot.Ledger?.ReservedCount(aUnit) ?? 0;
            return PPConditionMath.Compare(Mathf.Max(0, total - reserved), mOp, mThreshold);
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
            => mDescription = $"残り行動回数が{mThreshold}回{GetOpString(mOp)}";
    }
}
