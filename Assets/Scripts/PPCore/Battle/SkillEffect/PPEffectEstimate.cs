/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPEffectEstimate.cs
 * @author hqrse
 * @date 2026/08/10
 * @brief スキル効果の事前見積もり。AIの効用計算に使う
 * =====================================*/

namespace PPCore
{
    // スキル効果を実行せずに見積もった結果
    // AI が「この行動にどれだけの価値があるか」を判断するために使う
    // 効果の内部表現（威力・カテゴリ・エフェクト定義）を AI 側へ漏らさずに済むよう、
    // 必要な情報だけをこの型に詰めて受け渡す
    public readonly struct PPEffectEstimate
    {
        // 与ダメージの見込み量。命中率・クリティカル・属性相性は含めない
        public float Damage { get; }
        // 回復量の見込み
        public float Heal { get; }
        // 付与する StatusEffect の識別子。重複付与の判定に使う。付与しないなら null
        public string StatusEffectId { get; }

        private PPEffectEstimate(float aDamage, float aHeal, string aStatusEffectId)
        {
            Damage = aDamage;
            Heal = aHeal;
            StatusEffectId = aStatusEffectId;
        }

        // 効果なしを表す見積もり。推定に対応していない効果はこれを返す
        public static PPEffectEstimate None => default;

        // aDamage : 与ダメージの見込み量
        public static PPEffectEstimate FromDamage(float aDamage) => new(aDamage, 0f, null);

        // aHeal : 回復量の見込み
        public static PPEffectEstimate FromHeal(float aHeal) => new(0f, aHeal, null);

        // aStatusEffectId : 付与する StatusEffect の識別子
        public static PPEffectEstimate FromStatus(string aStatusEffectId) => new(0f, 0f, aStatusEffectId);

        // 別の見積もりと合算する
        // 1 スキルが複数の効果を持つ場合に、それらを 1 つへまとめるのに使う
        // 識別子は先に設定されている方を優先する（複数の付与効果を持つスキルは代表 1 件で判定する）
        // aOther : 合算する見積もり
        // return : 合算後の見積もり
        public PPEffectEstimate Merge(PPEffectEstimate aOther)
            => new(Damage + aOther.Damage,
                   Heal + aOther.Heal,
                   string.IsNullOrEmpty(StatusEffectId) ? aOther.StatusEffectId : StatusEffectId);
    }
}
