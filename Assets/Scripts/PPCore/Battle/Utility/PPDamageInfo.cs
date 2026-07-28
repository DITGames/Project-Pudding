/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPDamageInfo.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief PPCore固有のダメージ情報
 * =====================================*/
using CommandBattleCore;

namespace PPCore
{
    public class PPDamageInfo : DamageInfo
    {
        // スキル種別
        public PPSkillCategory Category { get; set; }
        // 属性
        public PPTypeAttribute Attribute { get; set; }
        
        // 弱点？
        public bool IsWeaknessHit { get; set; } = false;
        // 耐性？
        public bool IsResistHit { get; set; } = false;

        public PPDamageInfo(BattleUnit aSource, BattleUnit aTarget, float aAmount, PPSkillCategory aCategory,
            PPTypeAttribute aAttribute = PPTypeAttribute.Normal, object aSourceAbility = null)
            : base(aSource, aTarget, aAmount, aSourceAbility)
        {
            Category = aCategory;
            Attribute = aAttribute;
        }
    }
}