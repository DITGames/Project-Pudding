/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitSkillCastableCondition.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief ユニット条件 : 指定タグのスキルが今発動可能
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ユニット条件: 指定タグのスキルを今すぐ発動できるか
    // クールダウン・使用回数・リソース残量まで含めて判定する
    // リソース残量まで見るため、この条件を実行者条件に入れると
    // 「溜めてから撃つ」戦術が成立しなくなる点に注意（その場合は保持判定の方を使う）
    [Serializable]
    [PPTypeMenuName("スキル/指定タグが発動可能")]
    public sealed class PPUnitSkillCastableCondition : PPUnitConditionValidator
    {
        [Label("対象スキルタグ", true)]
        [SerializeField] private List<PPSkillTagDefinition> mTags = new();
        // 反転すると「そのタグのスキルを撃てない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 指定タグのスキルが発動可能かを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
            => PPSkillTagUtility.HasCastableTaggedSkill(aUnit, mTags, aSnapShot.Context) != mIsInvert;

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string tags = PPSkillTagUtility.ToDisplayString(mTags);
            mDescription = mIsInvert ? $"{tags} のスキルを撃てない" : $"{tags} のスキルを撃てる";
        }
    }
}
