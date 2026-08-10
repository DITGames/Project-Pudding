/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTickLedger.cs
 * @author hqrse
 * @date 2026/08/10
 * @brief 同一ティック内で採用済みの行動が及ぼす効果を記録する帳簿
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    // 同じティックで既に採用した行動の効果を記録しておく帳簿
    // リソースが共有である以上、行動は 1 体ずつ独立には決まらない。
    // 対象の取り合いも同じで、帳簿が無いと 3 体が同じ敵をオーバーキルしたり、
    // 複数のヒーラーが同じ味方を重ねて回復したりする
    // PPResourceBudget がリソースについてやっていることを、対象の HP と状態について行う
    public sealed class PPTickLedger
    {
        // 対象ごとの、この ティックで与える予定のダメージ量
        private readonly Dictionary<BattleUnit, float> mPlannedDamage = new();
        // 対象ごとの、このティックで回復する予定の量
        private readonly Dictionary<BattleUnit, float> mPlannedHeal = new();
        // 対象ごとの、このティックで付与する予定の StatusEffect 識別子
        private readonly Dictionary<BattleUnit, HashSet<string>> mPlannedStatus = new();

        // 記録をすべて破棄する。思考の開始時に呼ぶ
        public void Clear()
        {
            mPlannedDamage.Clear();
            mPlannedHeal.Clear();
            mPlannedStatus.Clear();
        }

        // 採用が決まった候補の効果を記録する
        // aCandidate : 採用された候補
        public void Record(PPActionCandidate aCandidate)
        {
            if (aCandidate?.Target == null)
                return;

            var target = aCandidate.Target;
            var estimate = aCandidate.Estimate;

            if (estimate.Damage > 0f)
            {
                mPlannedDamage.TryGetValue(target, out float damage);
                mPlannedDamage[target] = damage + estimate.Damage;
            }
            if (estimate.Heal > 0f)
            {
                mPlannedHeal.TryGetValue(target, out float heal);
                mPlannedHeal[target] = heal + estimate.Heal;
            }
            if (!string.IsNullOrEmpty(estimate.StatusEffectId))
            {
                if (!mPlannedStatus.TryGetValue(target, out var ids))
                {
                    ids = new HashSet<string>();
                    mPlannedStatus[target] = ids;
                }
                ids.Add(estimate.StatusEffectId);
            }
        }

        // 対象へ与える予定のダメージ量を取得する
        // aTarget : 対象ユニット
        public float PlannedDamage(BattleUnit aTarget)
            => aTarget != null && mPlannedDamage.TryGetValue(aTarget, out float value) ? value : 0f;

        // 対象を回復する予定の量を取得する
        // aTarget : 対象ユニット
        public float PlannedHeal(BattleUnit aTarget)
            => aTarget != null && mPlannedHeal.TryGetValue(aTarget, out float value) ? value : 0f;

        // 対象へ指定の StatusEffect を付与する予定があるかを判定する
        // aTarget : 対象ユニット
        // aEffectId : 判定する識別子
        public bool IsStatusPlanned(BattleUnit aTarget, string aEffectId)
            => aTarget != null
            && !string.IsNullOrEmpty(aEffectId)
            && mPlannedStatus.TryGetValue(aTarget, out var ids)
            && ids.Contains(aEffectId);
    }
}
