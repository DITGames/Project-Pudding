/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPAttackSkillEffectDefinition.cs
 * @author hqrse
 * @date 2026/08/06
 * @brief 攻撃型スキルエフェクトの定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 属性相性を考慮したダメージを 1 対象に与えるスキルエフェクト
    // 属性・威力・カテゴリを自身で持つため、1 スキルに複数登録すれば複数属性の複合攻撃になる
    [Serializable]
    [PPTypeMenuName("ダメージ")]
    public class PPAttackSkillEffectDefinition : PPSkillEffectDefinition
    {
        [Label("属性")]
        [SerializeField] private PPTypeAttribute mAttribute = PPTypeAttribute.Normal;
        [PercentLabel("威力", 0, 10)]
        [SerializeField] private float mPower = 1.2f;
        [Label("種別")]
        [SerializeField] private PPSkillCategory mCategory = PPSkillCategory.Physical;

        // aSource : スキル発動者
        // aTarget : ダメージを与える対象
        // aSourceSkill : この効果を保有するスキル定義。ダメージ情報の発生源表示等に使う
        // aContext : バトルコンテキスト
        public override void Apply(BattleUnit aSource, BattleUnit aTarget, PPSkillDefinition aSourceSkill, BattleContext aContext)
        {
            var attribute = PPDamageUtility.ResolveAttribute(mAttribute, aSource);
            float dmg = PPDamageUtility.ResolveAttackSkillDamage(aSource, aTarget, mPower, mCategory);
            var damageInfo = PPDamageUtility.CreateDamageInfo(aSource, aTarget, dmg, mCategory, attribute, aSourceSkill, aContext);
            aTarget.ApplyDamage(damageInfo, aContext);
        }

        public override string BuildString()
            => $"ダメージ：{mAttribute} / {mCategory} / 威力{mPower:0%}";
    }
}
