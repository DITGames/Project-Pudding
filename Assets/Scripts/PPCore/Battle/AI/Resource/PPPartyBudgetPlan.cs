/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyBudgetPlan.cs
 * @author hqrse
 * @date 2026/08/10
 * @brief 1ティック分の予算計画。使ってよい額とリソースの値付けを保持する
 * =====================================*/

namespace PPCore
{
    // 1 回の思考で使う予算計画
    // 「いくらまで使ってよいか（Budget）」と「リソース 1 点はいくらの価値があるか（λ）」の 2 つを持つ
    // λ があることで、全体最適という難しい問題が
    // 「効用 > λ × コスト」という行動ごとの独立した判定に分解される
    // λ を超える候補が 1 つも無ければ何も採用されず、結果としてリソースが貯まる。
    // 待機を表す特別な状態を持たなくてよいのはこのため
    public sealed class PPPartyBudgetPlan
    {
        // 属性ごとのリソース 1 点あたりの価値
        private readonly float[] mLambda;
        // 属性ごとの上限値。コストを割合へ正規化するのに使う
        private readonly float[] mMax;
        // 属性ごとの使ってよい額。デバッグ表示に使う
        private readonly float[] mAllowance;
        // 属性ごとの保険として取り置いた額。デバッグ表示に使う
        private readonly float[] mInsurance;

        // 思考中の仮想残量。採用のたびに減っていく
        public PPResourceBudget Budget { get; }
        // λ による判定を行うか。無効な場合は買えるものを買えるだけ買う
        public bool IsUseLambda { get; }
        // 危機状態により保険が解除されたか。デバッグ表示に使う
        public bool IsInsuranceReleased { get; }

        // aBudget : 仮想残量
        // aLambda : 属性ごとの λ
        // aMax : 属性ごとの上限値
        // aAllowance : 属性ごとの使ってよい額
        // aInsurance : 属性ごとの保険額
        // aIsUseLambda : λ 判定を行うか
        // aIsInsuranceReleased : 保険が解除されたか
        public PPPartyBudgetPlan(PPResourceBudget aBudget, float[] aLambda, float[] aMax,
            float[] aAllowance, float[] aInsurance, bool aIsUseLambda, bool aIsInsuranceReleased)
        {
            Budget = aBudget;
            mLambda = aLambda;
            mMax = aMax;
            mAllowance = aAllowance;
            mInsurance = aInsurance;
            IsUseLambda = aIsUseLambda;
            IsInsuranceReleased = aIsInsuranceReleased;
        }

        // 指定属性の λ を取得する
        // a : 対象の属性
        public float Lambda(PPTypeAttribute a) => mLambda[(int)a];

        // 指定属性の使ってよい額を取得する
        // a : 対象の属性
        public float Allowance(PPTypeAttribute a) => mAllowance[(int)a];

        // 指定属性の保険額を取得する
        // a : 対象の属性
        public float Insurance(PPTypeAttribute a) => mInsurance[(int)a];

        // コストを支払うことの「値段」を求める
        // コストは上限に対する割合へ正規化してから λ を掛ける
        // 正規化しないとコストの絶対値がそのまま効いてしまい、
        // プール上限を変えるたびに全プロファイルの調整をやり直すことになる
        // aCost : 評価するコスト。null または無コストなら 0
        // return : 効用と比較するための値段
        public float LambdaCostOf(PPResourceCost aCost)
        {
            if (aCost == null || aCost.IsFree)
                return 0f;

            float total = 0f;
            for (int i = 0; i < PPTypeAttributeDefinition.TypeCount; i++)
            {
                float need = aCost.Get(i);
                if (need <= 0f || mMax[i] <= 0f)
                    continue;

                total += mLambda[i] * (need / mMax[i]);
            }
            return total;
        }

        // デバッグ表示用に、基準リソースの状況を 1 行へまとめる
        public string BuildSummary()
        {
            var baseType = (PPTypeAttribute)PPTypeAttributeDefinition.BaseIndex;
            return $"λ={Lambda(baseType):0.###} 使用可={Allowance(baseType):0.#} 保険={Insurance(baseType):0.#}"
                 + (IsUseLambda ? "" : " (λ判定なし)")
                 + (IsInsuranceReleased ? " (保険解除)" : "");
        }
    }
}
