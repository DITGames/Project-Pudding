/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPChanceTriggerConsumeRuleDefinition.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief きっかけが起きたときに確率で消費する消費ルールのデータ定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 指定したきっかけが起きたときに、確率でスタックを消費するルールの定義
    // 混乱のように「行動しようとするたびに一定確率で解ける」状態異常に使う
    // 確率は付与からの経過ターン数で変化させられるため、時間経過で解けやすくすることもできる
    // きっかけ・消費量・ダメージ種別の絞り込みは確定消費版と共通のため、そちらを継承している
    [Serializable]
    [PPTypeMenuName("スタック消費/きっかけ＋確率")]
    public class PPChanceTriggerConsumeRuleDefinition : PPTriggerConsumeRuleDefinition
    {
        [Header("消費確率")]
        // 付与直後（経過ターン 0）の消費確率
        [Label("基本確率")][Range(0f, 1f)]
        [SerializeField]protected float mBaseChance = 0.33f;
        // 経過ターン 1 つあたりの確率の増分。負の値にすると経つほど解けにくくなる
        [Label("1ターンあたりの増分")][Range(-1f, 1f)]
        [SerializeField]protected float mChancePerElapsedTurn = 0f;

        public override IStatusEffectConsumeRule CreateConsumeRule()
            => new ChanceTriggerConsumeRule(mTrigger, Mathf.Clamp01(mBaseChance), mChancePerElapsedTurn,
                Mathf.Max(1, mConsumeStacks), mIsConsumeAll, DamageReasonFilter);

        public override string BuildString()
        {
            string amount = mIsConsumeAll ? "全消費" : $"{Mathf.Max(1, mConsumeStacks)}消費";
            string curve = Mathf.Approximately(mChancePerElapsedTurn, 0f)
                ? string.Empty
                : $" 毎ターン{mChancePerElapsedTurn:+0.##%;-0.##%}";
            return $"{BuildTriggerString()}時に{Mathf.Clamp01(mBaseChance):P0}で{amount}{curve}";
        }
    }
}
