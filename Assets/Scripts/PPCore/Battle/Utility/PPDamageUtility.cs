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
    public static class PPDamageUtility
    {
        /* 通常攻撃の基礎ダメージ量解決 */
        public static float ResolveAttackDamage(BattleUnit aSource, BattleUnit aTarget)
        {
            var amount = Mathf.Max(1f, aSource.Parameters.Attack.CurrentValue - aTarget.Parameters.Defense.CurrentValue * 0.5f);
            return Mathf.RoundToInt(amount);
        }
        
        /* 攻撃スキルの基礎ダメージ量解決 */
        public static float ResolveAttackSkillDamage(BattleUnit aSource, BattleUnit aTarget, PPSkillDefinition aSkill)
        {
            var amount = Mathf.Max(1f, aSource.Parameters.Attack.CurrentValue + aSkill.Power - aTarget.Parameters.Defense.CurrentValue * 0.5f);
            return Mathf.RoundToInt(amount);
        }
        
        /* 属性解決 ノーマルタイプはユニットの属性に変換する */
        public static PPTypeAttribute ResolveAttribute(PPTypeAttribute aAttribute, BattleUnit aUnit)
        {
            if(aAttribute != PPTypeAttribute.Normal)
                return aAttribute;
            
            return (aUnit as PPBattleUnit)?.TypeAttribute ?? PPTypeAttribute.Normal;
        }

        /* ユニット->属性解決のヘルパー */
        public static PPTypeAttribute ResolveAttribute(BattleUnit aUnit)
        {
            return ResolveAttribute(PPTypeAttribute.Normal, aUnit);
        }

        /* ダメージ情報作成 */
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

        /* 属性相性の適用 */
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