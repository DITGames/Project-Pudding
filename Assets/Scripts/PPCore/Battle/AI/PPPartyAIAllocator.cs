/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyAIAllocator.cs
 * @author hqrse
 * @date 2026/08/10
 * @brief 予算の範囲で行動候補を採用する配分器
 * =====================================*/

using System.Collections.Generic;
using System.Linq;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 予算の範囲で「誰の何を採用するか」を決める配分器
    // 候補をユニットごとにまとめず、パーティ全体でフラットな 1 本のリストとして扱うのが要点。
    // ユニットごとにベストを 1 つ選んでから並べる作りだと、
    // そのベストが予算不足で買えなかったときにそのユニットは何もしなくなる。
    // 全候補を効用密度順に走査し、採用済みユニットを除外しながら進めれば、
    // 買えなかったユニットの次善候補が後段で自然に拾われる（特別な分岐が要らない）
    public class PPPartyAIAllocator
    {
        // AI の性格。行動数上限・ノイズ・実行順序を引く
        private readonly PPPartyAIProfileDefinition mProfile;
        // 効用の再評価に使う評価器
        private readonly PPActionUtilityEvaluator mEvaluator;

        // aProfile : AI プロファイル
        // aEvaluator : 効用評価器
        public PPPartyAIAllocator(PPPartyAIProfileDefinition aProfile, PPActionUtilityEvaluator aEvaluator)
        {
            mProfile = aProfile;
            mEvaluator = aEvaluator;
        }

        // 候補群から採用する行動を決める
        // aCandidates : 効用計算済みの全候補
        // aPlan : 予算計画
        // aDoctrine : 解決済みの作戦
        // aLedger : 同一ティック内の帳簿。採用のたびに更新される
        // aContext : バトルコンテキスト
        // return : 採用された行動の割り当て
        public virtual List<PPPartyActionAssignment> Allocate(List<PPActionCandidate> aCandidates,
            PPPartyBudgetPlan aPlan, PPAIDoctrine aDoctrine, PPTickLedger aLedger, BattleContext aContext)
        {
            var picks = new List<PPPartyActionAssignment>();
            if (aCandidates == null || aCandidates.Count == 0)
                return picks;

            float noiseAmplitude = Mathf.Clamp01(mProfile.ActionNoise);
            int maxActions = Mathf.Max(1, mProfile.MaxActionsPerTick);

            // 候補ごとにノイズ係数を 1 つだけ決め、採用順位と λ 判定の両方で同じ値を使う
            // 別々に引くと「順位は上位なのに λ 判定では落ちる」という一貫性のない挙動になる
            var scored = aCandidates
                .Select((c, i) => (candidate: c, index: i,
                    noise: 1f + RandomSigned(aContext) * noiseAmplitude))
                .ToList();

            // 効用密度（効用 ÷ コスト）の降順。同値は生成順を保ち、結果を乱数に依存させない
            var ordered = scored
                .OrderByDescending(t => Density(t.candidate) * t.noise)
                .ThenBy(t => t.index)
                .ToList();

            var actedUnits = new HashSet<PPBattleUnit>();
            // 本命が落ちたユニット。次善候補が採用された場合にフォールバックとして記録するために持つ
            var rejectedUnits = new HashSet<PPBattleUnit>();

            foreach (var (candidate, _, noise) in ordered)
            {
                if (picks.Count >= maxActions)
                {
                    candidate.RejectReason = PPActionRejectReason.ActionLimit;
                    continue;
                }
                if (actedUnits.Contains(candidate.Unit))
                {
                    candidate.RejectReason = PPActionRejectReason.UnitAlreadyActed;
                    continue;
                }

                // 先行する採用によって対象の状況が変わっているため効用を引き直す
                // 全体の再ソートまでは行わないが、これでオーバーキルと重複回復は防げる
                mEvaluator.Evaluate(candidate, aDoctrine, aLedger);
                if (candidate.Utility <= 0f)
                {
                    candidate.RejectReason = PPActionRejectReason.NoEffect;
                    continue;
                }

                // λ 判定。ノイズを乗せた効用で判定するため、
                // 「つい無駄遣いをする」「溜めるべきところで吐く」といったぶれが出る
                // 保険と取り置きは予算から除外済みなので、ここでのぶれには侵食されない
                if (aPlan.IsUseLambda)
                {
                    float perceived = candidate.Utility * noise;
                    if (perceived <= aPlan.LambdaCostOf(candidate.Cost))
                    {
                        candidate.RejectReason = PPActionRejectReason.BelowLambda;
                        rejectedUnits.Add(candidate.Unit);
                        continue;
                    }
                }

                if (!aPlan.Budget.CanAfford(candidate.Cost))
                {
                    candidate.RejectReason = PPActionRejectReason.NotEnoughBudget;
                    rejectedUnits.Add(candidate.Unit);
                    continue;
                }

                aPlan.Budget.TrySpend(candidate.Cost);
                aLedger.Record(candidate);
                actedUnits.Add(candidate.Unit);
                candidate.RejectReason = PPActionRejectReason.None;
                candidate.IsFallback = rejectedUnits.Contains(candidate.Unit);
                picks.Add(new PPPartyActionAssignment(
                    candidate.Unit, candidate.BuildCommand(aContext), RoleOrder(candidate.Role)));
            }

            return picks;
        }

        // 効用密度を求める。同じ効用ならコストの安い行動を優先する
        // 無コストの行動は割り算にならないため、密度に効用をそのまま使う
        // aCandidate : 対象の候補
        protected static float Density(PPActionCandidate aCandidate)
        {
            float total = aCandidate.Cost?.Total ?? 0f;
            return total <= 0f ? aCandidate.Utility : aCandidate.Utility / total;
        }

        // ロールごとの実行順序を引く。同一ティック内でどの行動を先に処理するかの並び順になる
        // aRole : 行動ロール
        protected int RoleOrder(PPBattleSkillRole aRole)
            => aRole switch
            {
                PPBattleSkillRole.Attack => mProfile.Order.Attack,
                PPBattleSkillRole.Support => mProfile.Order.Support,
                PPBattleSkillRole.Heal => mProfile.Order.Heal,
                _ => mProfile.Order.Default,
            };

        // -1〜1 の符号付き乱数を取得する
        // 乱数は必ずルール側のプロバイダを経由し、UnityEngine.Random は使わない
        // aContext : 乱数供給元を含むバトルコンテキスト
        protected static float RandomSigned(BattleContext aContext)
            => aContext.Rules.RandomProvider.NextFloat(-1f, 1f);
    }
}
