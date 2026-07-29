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
    /// <summary>
    /// 属性とスキル種別を持たせたダメージ情報。
    /// <para>
    /// 基底の <see cref="DamageInfo"/> に対して、属性相性の判定に必要な情報と、
    /// その結果（弱点・耐性）を追加する。
    /// 弱点／耐性フラグは演出側がダメージ表示を出し分けるために使う。
    /// </para>
    /// </summary>
    public class PPDamageInfo : DamageInfo
    {
        /// <summary>スキル種別。</summary>
        public PPSkillCategory Category { get; set; }
        /// <summary>攻撃属性。相性判定の攻撃側として使われる。</summary>
        public PPTypeAttribute Attribute { get; set; }

        /// <summary>弱点を突いたか。相性判定の結果として設定される。</summary>
        public bool IsWeaknessHit { get; set; } = false;
        /// <summary>耐性で軽減されたか。相性判定の結果として設定される。</summary>
        public bool IsResistHit { get; set; } = false;

        /// <param name="aSource">ダメージの発生元ユニット。</param>
        /// <param name="aTarget">ダメージを受けるユニット。</param>
        /// <param name="aAmount">初期ダメージ量。</param>
        /// <param name="aCategory">スキル種別。</param>
        /// <param name="aAttribute">攻撃属性。</param>
        /// <param name="aSourceAbility">発生源のスキル定義やエフェクト。</param>
        public PPDamageInfo(BattleUnit aSource, BattleUnit aTarget, float aAmount, PPSkillCategory aCategory,
            PPTypeAttribute aAttribute = PPTypeAttribute.Normal, object aSourceAbility = null)
            : base(aSource, aTarget, aAmount, aSourceAbility)
        {
            Category = aCategory;
            Attribute = aAttribute;
        }
    }
}
