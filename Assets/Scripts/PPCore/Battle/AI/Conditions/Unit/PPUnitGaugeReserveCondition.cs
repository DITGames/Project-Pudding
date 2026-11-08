/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitGaugeReserveCondition.cs
 * @author hqrse
 * @date 2026/11/08
 * @brief ユニット条件 : スキルを撃っても主スキル分のゲージが残る
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
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
}
