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
    // 属性とスキル種別を持たせたダメージ情報
    // 基底の DamageInfo に対して、属性相性の判定に必要な情報と、
    // その結果（弱点・耐性）を追加する
    // 弱点／耐性フラグは演出側がダメージ表示を出し分けるために使う
    public class PPDamageInfo : DamageInfo
    {
        // スキル種別
        public PPSkillCategory Category { get; set; }
        // 攻撃属性。相性判定の攻撃側として使われる
        public PPTypeAttribute Attribute { get; set; }

        // 弱点を突いたか。相性判定の結果として設定される
        public bool IsWeaknessHit { get; set; } = false;
        // 耐性で軽減されたか。相性判定の結果として設定される
        public bool IsResistHit { get; set; } = false;

        // aSource : ダメージの発生元ユニット
        // aTarget : ダメージを受けるユニット
        // aAmount : 初期ダメージ量
        // aCategory : スキル種別
        // aAttribute : 攻撃属性
        // aSourceAbility : 発生源のスキル定義やエフェクト
        public PPDamageInfo(BattleUnit aSource, BattleUnit aTarget, float aAmount, PPSkillCategory aCategory,
            PPTypeAttribute aAttribute = PPTypeAttribute.Normal, object aSourceAbility = null)
            : base(aSource, aTarget, aAmount, aSourceAbility)
        {
            Category = aCategory;
            Attribute = aAttribute;
        }
    }
}
