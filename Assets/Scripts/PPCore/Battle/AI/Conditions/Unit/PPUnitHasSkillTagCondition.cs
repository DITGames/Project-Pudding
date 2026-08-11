/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitHasSkillTagCondition.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief ユニット条件 : 指定タグのスキルを保持している
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // ユニット条件: 指定タグのスキルを持っているか
    // 発動可否は見ないため、「大技を持っている担当がいるか」のような編成上の役割判定に使う
    // 今すぐ撃てるかまで見たい場合は PPUnitSkillCastableCondition を使う
    [Serializable]
    [PPTypeMenuName("スキル/指定タグを保持")]
    public sealed class PPUnitHasSkillTagCondition : PPUnitConditionValidator
    {
        [Label("対象スキルタグ", true)]
        [SerializeField] private List<PPSkillTagDefinition> mTags = new();
        // 反転すると「そのタグのスキルを持っていない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 指定タグのスキルを持っているかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
            => PPSkillTagUtility.HasTaggedSkill(aUnit, mTags) != mIsInvert;

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string tags = PPSkillTagUtility.ToDisplayString(mTags);
            mDescription = mIsInvert ? $"{tags} のスキルを持っていない" : $"{tags} のスキルを持っている";
        }
    }
}
