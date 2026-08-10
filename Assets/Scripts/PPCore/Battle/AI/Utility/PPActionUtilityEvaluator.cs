/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPActionUtilityEvaluator.cs
 * @author hqrse
 * @date 2026/08/10
 * @brief 行動候補の効用を戦況から算出する
 * =====================================*/

using UnityEngine;

namespace PPCore
{
    // 行動候補の効用を求める
    // 効用 = 基礎AIスコア（スキル定義側の設定） × ロール別係数（作戦） × 対象状態係数（戦況）
    // 「満タンの味方を回復しない」「倒せる相手を優先する」「バフを重ね掛けしない」といった判断は、
    // どのパーティでも成り立つ当たり前の合理性なのでここ（コード側）に置く。
    // データ側に置くと、全プロファイルへ同じ設定を書き写すことになり、
    // 書き忘れた敵だけが間の抜けた挙動をする
    public class PPActionUtilityEvaluator
    {
        // 対象を倒しきれる場合に掛ける係数。削り切れない攻撃より明確に上位へ来るようにする
        protected const float KillBonus = 1.5f;
        // 既に付いている StatusEffect を重ね掛けする場合に掛ける係数
        // 0 にしないのは、更新目的での再付与に価値が残る場合があるため
        protected const float RedundantStatusFactor = 0.1f;

        // 候補の効用を求めて候補自身へ書き込む
        // aCandidate : 評価する候補
        // aDoctrine : 解決済みの作戦
        // aLedger : 同一ティック内の帳簿
        // return : 算出された効用
        public virtual float Evaluate(PPActionCandidate aCandidate, PPAIDoctrine aDoctrine, PPTickLedger aLedger)
        {
            float roleFactor = aDoctrine.Roles.Resolve(aCandidate.Role, 1f);
            float targetFactor = ResolveTargetFactor(aCandidate, aLedger);

            aCandidate.Utility = aCandidate.AIScore * roleFactor * targetFactor;
            return aCandidate.Utility;
        }

        // 対象の状態から効用の係数を求める
        // 対象を取らない行動（範囲攻撃・自己完結する支援など）は補正なしの 1 を返す
        // aCandidate : 評価する候補
        // aLedger : 同一ティック内の帳簿
        // return : 0 以上の係数
        protected virtual float ResolveTargetFactor(PPActionCandidate aCandidate, PPTickLedger aLedger)
        {
            var target = aCandidate.Target;
            if (target == null)
                return 1f;

            var estimate = aCandidate.Estimate;

            // 回復は欠けている分にしか価値がない
            // 既に他のユニットが回復する予定の分も差し引くことで、重複回復を避ける
            if (estimate.Heal > 0f)
            {
                float missing = target.Parameters.Hp.Max.CurrentValue
                              - target.Parameters.Hp.CurrentValue
                              - aLedger.PlannedHeal(target);
                if (missing <= 0f)
                    return 0f;

                return Mathf.Clamp01(Mathf.Min(estimate.Heal, missing) / Mathf.Max(1f, estimate.Heal));
            }

            // 攻撃は残 HP に対する削り率で測る
            // 既に倒れる予定の相手を殴っても価値は無く、倒しきれるなら上乗せする
            if (estimate.Damage > 0f)
            {
                float remain = target.Parameters.Hp.CurrentValue - aLedger.PlannedDamage(target);
                if (remain <= 0f)
                    return 0f;

                float ratio = estimate.Damage / remain;
                return ratio >= 1f ? KillBonus : ratio;
            }

            // 付与は既に付いているなら価値を落とす
            if (!string.IsNullOrEmpty(estimate.StatusEffectId))
            {
                bool isAlreadyApplied =
                    target.ActiveStatusEffects.Exists(e => e.EffectId == estimate.StatusEffectId)
                    || aLedger.IsStatusPlanned(target, estimate.StatusEffectId);

                return isAlreadyApplied ? RedundantStatusFactor : 1f;
            }

            return 1f;
        }
    }
}
