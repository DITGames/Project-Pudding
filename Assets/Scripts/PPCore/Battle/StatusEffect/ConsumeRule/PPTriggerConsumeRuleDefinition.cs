/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPTriggerConsumeRuleDefinition.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief きっかけが起きたら必ず消費する消費ルールのデータ定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 指定したきっかけが起きたら必ずスタックを消費するルールの定義
    // 「行動したら溜めを使い切る」「被ダメージ1回で消える」といった確定的な消費に使う
    [Serializable]
    [PPTypeMenuName("スタック消費/きっかけ")]
    public class PPTriggerConsumeRuleDefinition : PPStatusEffectConsumeRuleDefinition
    {
        [Header("スタック消費")]
        [Label("きっかけ")]
        [SerializeField]protected StatusEffectConsumeTrigger mTrigger = StatusEffectConsumeTrigger.Acted;
        [Label("全て消費する")]
        [SerializeField]protected bool mIsConsumeAll = true;
        // 全て消費する場合は参照されない
        [Label("消費スタック数")][Min(1)]
        [SerializeField]protected int mConsumeStacks = 1;

        // 以下 2 つはきっかけが「被ダメージ時」のときのみ意味を持つ
        // 既定では攻撃によるダメージだけを対象にし、毒などの継続ダメージでは消費しない
        [Label("ダメージ種別を限定する")]
        [SerializeField]protected bool mIsFilterDamageReason = true;
        [Label("対象ダメージ種別")]
        [SerializeField]protected DamageReason mDamageReason = DamageReason.Attack;

        // 限定しない場合は null を返し、種別を問わず消費させる
        protected DamageReason? DamageReasonFilter => mIsFilterDamageReason ? mDamageReason : null;

        public override IStatusEffectConsumeRule CreateConsumeRule()
            => new TriggerConsumeRule(mTrigger, Mathf.Max(1, mConsumeStacks), mIsConsumeAll, DamageReasonFilter);

        public override string BuildString()
        {
            string amount = mIsConsumeAll ? "全消費" : $"{Mathf.Max(1, mConsumeStacks)}消費";
            return $"{BuildTriggerString()}時に{amount}";
        }

        // きっかけ部分の表示文字列を組み立てる
        // 被ダメージ時にダメージ種別を限定している場合は、その種別も添える
        // return : 表示文字列
        protected string BuildTriggerString()
            => mTrigger == StatusEffectConsumeTrigger.Damaged && DamageReasonFilter.HasValue
                ? $"{ToDisplayName(mTrigger)}（{ToDisplayName(mDamageReason)}）"
                : ToDisplayName(mTrigger);
    }
}
