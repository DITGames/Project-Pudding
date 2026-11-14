/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPDamagedConsumeRuleDefinition.cs
 * @author hqrse
 * @date 2026/11/10
 * @brief ダメージを受けたときにスタックを消費する消費ルールのデータ定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // ダメージを受けたときにスタックを消費するルールの定義
    // 「次に受けるダメージだけ変化させる」効果に使う
    // 既定では攻撃によるダメージだけを対象にし、毒などの継続ダメージや反撃のダメージでは消費しない
    [Serializable]
    [PPTypeMenuName("被ダメージ時")]
    public class PPDamagedConsumeRuleDefinition : PPTriggerConsumeRuleDefinitionBase
    {
        [Header("被ダメージの条件")]
        [Label("ダメージ種別を限定する")]
        [SerializeField]protected bool mIsFilterDamageReason = true;
        [Label("対象ダメージ種別")]
        [EditCondition(nameof(mIsFilterDamageReason))]
        [SerializeField]protected DamageReasonKind mDamageReason = DamageReasonKind.Attack;
        // 種別を限定しない場合は反撃のダメージも常に対象になるため、限定するときだけ編集できる
        [Label("反撃のダメージも含める")]
        [EditCondition(nameof(mIsFilterDamageReason))]
        [SerializeField]protected bool mIsIncludeReactionDamage = false;

        protected override StatusEffectConsumeTrigger Trigger => StatusEffectConsumeTrigger.Damaged;

        // 限定しない場合は null を返し、種別を問わず消費させる
        protected override DamageReasonKind? DamageReasonFilter => mIsFilterDamageReason ? mDamageReason : null;

        protected override bool IsIncludeReactionDamage => mIsIncludeReactionDamage;

        // 種別を限定している場合は、その種別と反撃を含めるかも添える
        // return : 表示文字列
        protected override string BuildTriggerString()
            => DamageReasonFilter.HasValue
                ? $"{ToDisplayName(Trigger)}（{ToDisplayName(mDamageReason)}{(mIsIncludeReactionDamage ? "・反撃含む" : string.Empty)}）"
                : ToDisplayName(Trigger);
    }
}
