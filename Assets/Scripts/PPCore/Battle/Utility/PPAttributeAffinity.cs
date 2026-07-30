/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPAttributeAffinity.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief 属性同士の相性解決
 * =====================================*/
using UnityEngine;

namespace PPCore
{
    // 属性相性の判定結果。実際の倍率は PPBattleRules 側が持つ
    public enum PPAffinityResult
    {
        // 等倍
        Neutral,
        // 弱点を突いた（ダメージ増）
        Weak,
        // 耐性で受けられた（ダメージ減）
        Resist,
    }

    // 攻撃属性と防御属性から相性を判定する
    // 本作の相性は 2 系統ある
    // 1. 火 → 土 → 水 → 火 の三すくみ（一方通行。有利側が弱点、不利側が耐性）
    // 2. 光 ⇔ 闇 の相互弱点（どちらから攻撃しても弱点になる）
    // 判定のみを行い倍率は掛けない。倍率の適用は PPDamageUtility が担う
    public static class PPAttributeAffinity
    {
        // 相性を判定する
        // どちらかが Normal、または同属性同士の場合は等倍
        // 光闇ペアを三すくみより先に判定するため、相互弱点が正しく成立する
        // aAttackAttribute : 攻撃側の属性
        // aDefendAttribute : 防御側の属性
        // return : 相性の判定結果
        public static PPAffinityResult Resolve(PPTypeAttribute aAttackAttribute, PPTypeAttribute aDefendAttribute)
        {
            if(aAttackAttribute == PPTypeAttribute.Normal || aDefendAttribute == PPTypeAttribute.Normal)
                return PPAffinityResult.Neutral;
            if(aAttackAttribute == aDefendAttribute)
                return PPAffinityResult.Neutral;

            if(IsShineDarkPair(aAttackAttribute, aDefendAttribute))
                return PPAffinityResult.Weak;

            if(Beats(aAttackAttribute, aDefendAttribute))
                return PPAffinityResult.Weak;
            if(Beats(aDefendAttribute, aAttackAttribute))
                return PPAffinityResult.Resist;

            return PPAffinityResult.Neutral;
        }

        // 光と闇の組み合わせかを判定する。順序を問わないため相互弱点になる
        // aX : 片方の属性
        // aY : もう片方の属性
        private static bool IsShineDarkPair(PPTypeAttribute aX, PPTypeAttribute aY)
            => (aX == PPTypeAttribute.Shine && aY == PPTypeAttribute.Dark)
            || (aX == PPTypeAttribute.Dark && aY == PPTypeAttribute.Shine);

        // 三すくみで aX が aY に有利かを判定する
        // 順序に意味があるため、引数を入れ替えれば耐性側の判定に使える
        // aX : 攻める側の属性
        // aY : 受ける側の属性
        private static bool Beats(PPTypeAttribute aX, PPTypeAttribute aY)
            => (aX == PPTypeAttribute.Fire && aY == PPTypeAttribute.Earth)
            || (aX == PPTypeAttribute.Earth && aY == PPTypeAttribute.Water)
            || (aX == PPTypeAttribute.Water && aY == PPTypeAttribute.Fire);
    }
}
