/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPDamageUtility.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief ダメージ計算と属性相性
 * =====================================*/

using UnityEngine;

namespace PPCore
{
    // ダメージ計算を集約した静的ユーティリティ。通常攻撃から呼ばれる
    // 属性の解決から命中・クリティカル・属性相性の適用までを CreateDamageInfo が一括で行う
    public static class PPDamageUtility
    {
        /* 生成 */

        // ダメージ情報を組み立てる。命中判定 → クリティカル補正 → 属性相性の適用 → 整数丸めの順に処理する
        // ミスした場合はダメージ 0 でその時点で返す
        public static PPDamageInfo CreateDamageInfo(
            BattleUnit aSource,
            BattleUnit aTarget,
            float aRawAmount,
            PPTypeAttribute aAttribute,
            DamageReasonBase aReason,
            BattleContext aContext)
        {
            var info = new PPDamageInfo(aSource, aTarget, aRawAmount, aAttribute, aReason);

            var hit = aContext.ResolveHit(aSource, aTarget, info);
            if (hit.mResult == HitResult.Miss)
            {
                info.IsMiss = true;
                info.Amount = 0;
                return info;
            }

            if (hit.mCriticalInfo.IsCritical)
            {
                info.IsCritical = true;
                info.Amount *= hit.mCriticalInfo.CriticalMultiplier;
            }

            ApplyAttributeAffinity(info, aTarget, aContext);

            info.Amount = Mathf.RoundToInt(info.Amount);
            return info;
        }

        /* 操作 */

        // 属性相性を判定してダメージ倍率を適用し、弱点／耐性フラグを立てる
        // 倍率は拡張ルール側にしか無いため、PPBattleRules が差し込まれていない場合は何もしない（相性なしとして扱われる）
        private static void ApplyAttributeAffinity(PPDamageInfo aInfo, BattleUnit aTarget, BattleContext aContext)
        {
            if(aContext.Rules is not PPBattleRules rules)
                return;

            var defendAtt = (aTarget as PPBattleUnit)?.TypeAttribute ?? PPTypeAttribute.Normal;

            switch (PPAttributeAffinity.Resolve(aInfo.Attribute, defendAtt))
            {
                case PPAffinityResult.Weak:
                    aInfo.IsWeaknessHit = true;
                    aInfo.Amount *= rules.WeaknessMultiplier;
                    break;
                case PPAffinityResult.Resist:
                    aInfo.IsResistHit = true;
                    aInfo.Amount *= rules.ResistanceMultiplier;
                    break;
            }
        }

        /* 問い合わせ */

        // 通常攻撃の基礎ダメージ量を求める。「攻撃力 - 防御力 × 0.5」で最低 1 を保証し、整数へ丸める
        public static float ResolveAttackDamage(BattleUnit aSource, BattleUnit aTarget)
        {
            var amount = Mathf.Max(1f, aSource.Parameters.Attack.CurrentValue - aTarget.Parameters.Defense.CurrentValue * 0.5f);
            return Mathf.RoundToInt(amount);
        }

        // 攻撃属性を解決する。無属性（Normal）を指定したときは使用者自身の属性を継承する
        public static PPTypeAttribute ResolveAttribute(PPTypeAttribute aAttribute, BattleUnit aUnit)
        {
            if(aAttribute != PPTypeAttribute.Normal)
                return aAttribute;

            return (aUnit as PPBattleUnit)?.TypeAttribute ?? PPTypeAttribute.Normal;
        }

        // ユニットの属性をそのまま解決するヘルパー。通常攻撃のように属性の指定が無いケースで使う
        public static PPTypeAttribute ResolveAttribute(BattleUnit aUnit)
        {
            return ResolveAttribute(PPTypeAttribute.Normal, aUnit);
        }
    }

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

    // 攻撃属性と防御属性から相性を判定する。火 → 土 → 水 → 火 の三すくみと、光 ⇔ 闇 の相互弱点がある
    // 判定のみを行い、倍率の適用は PPDamageUtility が担う
    public static class PPAttributeAffinity
    {
        // 三すくみで aX が aY に有利かを判定する
        // 順序に意味があるため、引数を入れ替えれば耐性側の判定に使える
        private static bool Beats(PPTypeAttribute aX, PPTypeAttribute aY)
            => (aX == PPTypeAttribute.Fire && aY == PPTypeAttribute.Earth)
            || (aX == PPTypeAttribute.Earth && aY == PPTypeAttribute.Water)
            || (aX == PPTypeAttribute.Water && aY == PPTypeAttribute.Fire);

        // 相性を判定する。どちらかが Normal、または同属性同士の場合は等倍
        // 光闇ペアを三すくみより先に判定する
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
        private static bool IsShineDarkPair(PPTypeAttribute aX, PPTypeAttribute aY)
            => (aX == PPTypeAttribute.Shine && aY == PPTypeAttribute.Dark)
            || (aX == PPTypeAttribute.Dark && aY == PPTypeAttribute.Shine);
    }
}
