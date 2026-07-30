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
    // ダメージ計算まわりの処理を集約した静的ユーティリティ
    // 通常攻撃・攻撃スキル双方から呼ばれるため、計算式を 1 箇所にまとめて実装間でダメージがずれないようにしている
    // 属性の解決から命中・クリティカル・属性相性の適用までを CreateDamageInfo が一括で行う
    public static class PPDamageUtility
    {
        // 通常攻撃の基礎ダメージ量を求める。「攻撃力 - 防御力 × 0.5」で最低 1 を保証し、整数へ丸める
        // aSource : 攻撃側ユニット
        // aTarget : 防御側ユニット
        // return : 丸め済みの基礎ダメージ量
        public static float ResolveAttackDamage(BattleUnit aSource, BattleUnit aTarget)
        {
            var amount = Mathf.Max(1f, aSource.Parameters.Attack.CurrentValue - aTarget.Parameters.Defense.CurrentValue * 0.5f);
            return Mathf.RoundToInt(amount);
        }

        // 攻撃スキルの基礎ダメージ量を求める。通常攻撃の式にスキルの威力を上乗せする
        // aSource : 攻撃側ユニット
        // aTarget : 防御側ユニット
        // aSkill : 使用するスキルの定義
        // return : 丸め済みの基礎ダメージ量
        public static float ResolveAttackSkillDamage(BattleUnit aSource, BattleUnit aTarget, PPSkillDefinition aSkill)
        {
            var amount = Mathf.Max(1f, aSource.Parameters.Attack.CurrentValue + aSkill.Power - aTarget.Parameters.Defense.CurrentValue * 0.5f);
            return Mathf.RoundToInt(amount);
        }

        // 攻撃属性を解決する
        // 無属性（Normal）のスキルは使用者自身の属性を継承するため、
        // 属性を持つユニットが通常攻撃するとその属性で相性判定が働く
        // aAttribute : スキル側で指定された属性
        // aUnit : 使用者
        // return : 解決された属性。使用者が属性を持たない場合は Normal
        public static PPTypeAttribute ResolveAttribute(PPTypeAttribute aAttribute, BattleUnit aUnit)
        {
            if(aAttribute != PPTypeAttribute.Normal)
                return aAttribute;

            return (aUnit as PPBattleUnit)?.TypeAttribute ?? PPTypeAttribute.Normal;
        }

        // ユニットの属性をそのまま解決するヘルパー。通常攻撃のようにスキル側の属性指定が無いケースで使う
        // aUnit : 使用者
        // return : 使用者の属性。持たない場合は Normal
        public static PPTypeAttribute ResolveAttribute(BattleUnit aUnit)
        {
            return ResolveAttribute(PPTypeAttribute.Normal, aUnit);
        }

        // ダメージ情報を組み立てる
        // 命中判定 → クリティカル補正 → 属性相性の適用 → 整数丸め、の順に処理する
        // ミスした場合はダメージを 0 にしてその時点で返すため、以降の補正は掛からない
        // aSource : 攻撃側ユニット
        // aTarget : 防御側ユニット
        // aRawAmount : 補正前の基礎ダメージ量
        // aCategory : スキル種別
        // aAttribute : 解決済みの攻撃属性
        // aSourceAbility : 発生源のスキル定義やエフェクト
        // aContext : 判定に使うバトルコンテキスト
        // return : 各種補正を適用済みのダメージ情報
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

        // 属性相性を判定してダメージ倍率を適用し、弱点／耐性フラグを立てる
        // 倍率は拡張ルール側にしか無いため、PPBattleRules が差し込まれていない場合は何もしない（相性なしとして扱われる）
        // aInfo : 補正対象のダメージ情報
        // aTarget : 防御側ユニット
        // aContext : バトルコンテキスト
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
