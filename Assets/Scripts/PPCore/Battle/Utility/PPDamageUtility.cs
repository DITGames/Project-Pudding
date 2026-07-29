/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPDamageUtility.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief 属性の継承解決・ダメージ計算・ダメージ情報生成
 * =====================================*/
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// ダメージ計算まわりの処理を集約した静的ユーティリティ。
    /// <para>
    /// 通常攻撃・攻撃スキル双方から呼ばれるため、計算式を 1 箇所にまとめて
    /// 実装間でダメージがずれないようにしている。
    /// 属性の解決から命中・クリティカル・属性相性の適用までを
    /// <see cref="CreateDamageInfo"/> が一括で行う。
    /// </para>
    /// </summary>
    public static class PPDamageUtility
    {
        /// <summary>
        /// 通常攻撃の基礎ダメージ量を求める。「攻撃力 - 防御力 × 0.5」で最低 1 を保証し、整数へ丸める。
        /// </summary>
        /// <param name="aSource">攻撃側ユニット。</param>
        /// <param name="aTarget">防御側ユニット。</param>
        /// <returns>丸め済みの基礎ダメージ量。</returns>
        public static float ResolveAttackDamage(BattleUnit aSource, BattleUnit aTarget)
        {
            var amount = Mathf.Max(1f, aSource.Parameters.Attack.CurrentValue - aTarget.Parameters.Defense.CurrentValue * 0.5f);
            return Mathf.RoundToInt(amount);
        }

        /// <summary>
        /// 攻撃スキルの基礎ダメージ量を求める。通常攻撃の式にスキルの威力を上乗せする。
        /// </summary>
        /// <param name="aSource">攻撃側ユニット。</param>
        /// <param name="aTarget">防御側ユニット。</param>
        /// <param name="aSkill">使用するスキルの定義。</param>
        /// <returns>丸め済みの基礎ダメージ量。</returns>
        public static float ResolveAttackSkillDamage(BattleUnit aSource, BattleUnit aTarget, PPSkillDefinition aSkill)
        {
            var amount = Mathf.Max(1f, aSource.Parameters.Attack.CurrentValue + aSkill.Power - aTarget.Parameters.Defense.CurrentValue * 0.5f);
            return Mathf.RoundToInt(amount);
        }

        /// <summary>
        /// 攻撃属性を解決する。
        /// 無属性（Normal）のスキルは使用者自身の属性を継承するため、
        /// 属性を持つユニットが通常攻撃するとその属性で相性判定が働く。
        /// </summary>
        /// <param name="aAttribute">スキル側で指定された属性。</param>
        /// <param name="aUnit">使用者。</param>
        /// <returns>解決された属性。使用者が属性を持たない場合は Normal。</returns>
        public static PPTypeAttribute ResolveAttribute(PPTypeAttribute aAttribute, BattleUnit aUnit)
        {
            if(aAttribute != PPTypeAttribute.Normal)
                return aAttribute;

            return (aUnit as PPBattleUnit)?.TypeAttribute ?? PPTypeAttribute.Normal;
        }

        /// <summary>
        /// ユニットの属性をそのまま解決するヘルパー。通常攻撃のように
        /// スキル側の属性指定が無いケースで使う。
        /// </summary>
        /// <param name="aUnit">使用者。</param>
        /// <returns>使用者の属性。持たない場合は Normal。</returns>
        public static PPTypeAttribute ResolveAttribute(BattleUnit aUnit)
        {
            return ResolveAttribute(PPTypeAttribute.Normal, aUnit);
        }

        /// <summary>
        /// ダメージ情報を組み立てる。
        /// 命中判定 → クリティカル補正 → 属性相性の適用 → 整数丸め、の順に処理する。
        /// ミスした場合はダメージを 0 にしてその時点で返すため、以降の補正は掛からない。
        /// </summary>
        /// <param name="aSource">攻撃側ユニット。</param>
        /// <param name="aTarget">防御側ユニット。</param>
        /// <param name="aRawAmount">補正前の基礎ダメージ量。</param>
        /// <param name="aCategory">スキル種別。</param>
        /// <param name="aAttribute">解決済みの攻撃属性。</param>
        /// <param name="aSourceAbility">発生源のスキル定義やエフェクト。</param>
        /// <param name="aContext">判定に使うバトルコンテキスト。</param>
        /// <returns>各種補正を適用済みのダメージ情報。</returns>
        public static PPDamageInfo CreateDamageInfo(
            BattleUnit aSource,
            BattleUnit aTarget,
            float aRawAmount,
            PPSkillCategory aCategory,
            PPTypeAttribute aAttribute,
            object aSourceAbility,
            BattleContext aContext)
        {
            var info = new PPDamageInfo(aSource, aTarget, aRawAmount, aCategory, aAttribute, aSourceAbility);

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

        /// <summary>
        /// 属性相性を判定してダメージ倍率を適用し、弱点／耐性フラグを立てる。
        /// 倍率は拡張ルール側にしか無いため、<see cref="PPBattleRules"/> が
        /// 差し込まれていない場合は何もしない（相性なしとして扱われる）。
        /// </summary>
        /// <param name="aInfo">補正対象のダメージ情報。</param>
        /// <param name="aTarget">防御側ユニット。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
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
    }
}
