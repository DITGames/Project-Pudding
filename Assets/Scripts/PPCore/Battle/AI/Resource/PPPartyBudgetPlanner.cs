/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyBudgetPlanner.cs
 * @author hqrse
 * @date 2026/08/10
 * @brief パーティ単位の予算計画。保険・取り置き・支出枠とλを算出する
 * =====================================*/

using UnityEngine;

namespace PPCore
{
    // 1 ティック分の予算計画を組み立てる
    // 行動を選んだ結果として消費が決まるのではなく、先に支出方針を決めてから中身を埋めるための層
    // これが無いとテンポ（リソースを回す速さ）が AI の意思ではなく副作用になる
    public class PPPartyBudgetPlanner
    {
        // λ 係数の上限。極端な値でスケールが壊れるのを防ぐ
        private const float MaxLambdaScale = 3f;
        // 溢れ嫌悪度の上限
        private const float MaxOverflowAversion = 3f;
        // 保険として確保できる回復スキルの最大回数
        private const int MaxInsuranceCount = 3;

        // 予算計画を組み立てる
        // aSnap : パーティ状況スナップショット
        // aDoctrine : 解決済みの作戦
        // aProfile : AI プロファイル
        // aTracker : 収入トラッカー
        // return : このティックの予算計画
        public virtual PPPartyBudgetPlan Plan(PPPartyAIContext aSnap, PPAIDoctrine aDoctrine,
            PPPartyAIProfileDefinition aProfile, PPIncomeTracker aTracker)
        {
            int count = PPTypeAttributeDefinition.TypeCount;
            var lambda = new float[count];
            var max = new float[count];
            var allowance = new float[count];
            var insurance = new float[count];

            // 保険は瀕死になったとき用の備えなので、既に危機なら解除して全額を使えるようにする
            // 溜めたまま味方が落ちるのは強いのではなく誤動作
            bool isInsuranceReleased = aSnap.IsCrisis;
            var insuranceCost = (aProfile.IsUseInsurance && !isInsuranceReleased)
                ? ResolveInsuranceCost(aSnap, aProfile)
                : null;

            float spendCap = Mathf.Clamp01(aDoctrine.SpendCapRatio);

            for (int i = 0; i < count; i++)
            {
                var type = (PPTypeAttribute)i;
                max[i] = aSnap.ResourcePool.Max(type);
                insurance[i] = insuranceCost?.Get(i) ?? 0f;

                // 保険と取り置きを先に差し引く。ここで引いた分は λ 判定の対象外となり、
                // 行動選択ノイズによって食い潰されることもない（ハード制約として働く）
                float reserved = insurance[i] + aDoctrine.ReserveOf(type);
                float usable = Mathf.Max(0f, aSnap.Current(type) - reserved);

                allowance[i] = usable * spendCap;
                lambda[i] = ResolveLambda(type, aSnap, aDoctrine, aProfile, aTracker);
            }

            var budget = new PPResourceBudget(aSnap.ResourcePool, allowance);
            return new PPPartyBudgetPlan(budget, lambda, max, allowance, insurance,
                aProfile.IsUseLambda, isInsuranceReleased);
        }

        // リソース 1 点あたりの価値を求める
        // 残量が乏しいほど高く、収入が速いほど安く、満タンに近いほど 0 へ落ちる
        // 上限を超えた収入は破棄されるため、満タン付近の 1 点は文字通り無価値になる。
        // これが「溜まりきる前に必ず吐く」という、プレイヤーから読み取れる挙動を生む
        // a : 対象の属性
        // aSnap : パーティ状況スナップショット
        // aDoctrine : 解決済みの作戦
        // aProfile : AI プロファイル
        // aTracker : 収入トラッカー
        // return : 0 以上の値付け
        protected virtual float ResolveLambda(PPTypeAttribute a, PPPartyAIContext aSnap,
            PPAIDoctrine aDoctrine, PPPartyAIProfileDefinition aProfile, PPIncomeTracker aTracker)
        {
            float max = aSnap.ResourcePool.Max(a);
            float fill = max > 0f ? Mathf.Clamp01(aSnap.Current(a) / max) : 0f;

            // 希少性と溢れ圧をひとつの減衰でまとめて表現する
            // 指数が大きいほど満タン付近での落ち方が急になり、溢れを強く嫌うようになる
            float aversion = Mathf.Clamp(aProfile.OverflowAversion, 0f, MaxOverflowAversion);
            float scarcity = Mathf.Pow(1f - fill, 1f + aversion);

            // 収入が見込めるほどリソースは安くなる。収入が読めないうちは割り引かない
            // 溜めを行わないティア（ザコ）は先を読まないため、収入による割引も掛けない
            float incomeDiscount = 1f;
            if (aProfile.IsUseBanking)
            {
                float gain = aTracker.ConservativeGain(a, Mathf.Clamp01(aProfile.Caution));
                if (gain > 0f) incomeDiscount = 1f / (1f + gain);
            }

            float scale = Mathf.Clamp(aProfile.LambdaScale, 0f, MaxLambdaScale);
            float multiplier = Mathf.Clamp(aDoctrine.LambdaMultiplier, 0f, MaxLambdaScale);

            return scarcity * incomeDiscount * scale * multiplier;
        }

        // 保険として取り置くコストを求める
        // パーティが実際に持っている最も安い回復スキルのコストを基準にするため、
        // 編成やコスト調整が変わっても取り置きの意味がずれない
        // 回復スキルを 1 つも持たない場合は null（取り置きなし）を返す
        // aSnap : パーティ状況スナップショット
        // aProfile : AI プロファイル
        // return : 取り置くコスト。不要なら null
        protected virtual PPResourceCost ResolveInsuranceCost(PPPartyAIContext aSnap, PPPartyAIProfileDefinition aProfile)
        {
            int count = Mathf.Clamp(aProfile.InsuranceCount, 0, MaxInsuranceCount);
            if (count <= 0)
                return null;

            PPResourceCost cheapest = null;
            foreach (var unit in aSnap.AliveMembers)
            {
                foreach (var skill in unit.Skills)
                {
                    if (skill.SourceDefinition is not PPSkillDefinition def)
                        continue;
                    if ((def.BattleSkillRole & PPBattleSkillRole.Heal) == 0)
                        continue;
                    if (def.Cost == null || def.Cost.IsFree)
                        continue;

                    if (cheapest == null || def.Cost.Total < cheapest.Total)
                    {
                        cheapest = def.Cost;
                    }
                }
            }

            if (cheapest == null)
                return null;

            // 指定回数分に増やす。属性の内訳はそのまま保つため、
            // 火属性の回復スキルなら火を取り置くことになる
            var amounts = new PPResourceAmount[PPTypeAttributeDefinition.TypeCount];
            for (int i = 0; i < PPTypeAttributeDefinition.TypeCount; i++)
            {
                amounts[i] = new PPResourceAmount
                {
                    Type = (PPTypeAttribute)i,
                    Amount = cheapest.Get(i) * count,
                };
            }
            return PPResourceCost.From(amounts);
        }
    }
}
