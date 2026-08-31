/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitHasSkillCondition.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニット条件 : 指定条件のスキルを持っている
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ユニット条件: 同種グループ・タグで絞り込んだスキルを持っているか
    // 発動可否は見ないため、「攻撃役かどうか」「回復役かどうか」という役割の判定になる
    // 判断ツリーの入口で役割ごとに枝を分けるのに使う
    [Serializable]
    [PPTypeMenuName("スキル/所持している")]
    public sealed class PPUnitHasSkillCondition : PPUnitConditionValidator, IPPUnitAISkillFilterOwner
    {
        [Label("対象スキル")]
        [SerializeField] private PPUnitAISkillFilter mFilter = new();
        // 反転すると「そのスキルを持っていない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 保持しているスキルの絞り込み条件。エディタの診断から参照する
        public PPUnitAISkillFilter Filter => mFilter;

        // 絞り込みに合致するスキルを所持しているかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
            => PPUnitAISkillQuery.HasAny(aUnit, mFilter) != mIsInvert;

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string filter = mFilter.ToDisplayString();
            mDescription = mIsInvert ? $"{filter} のスキルを持っていない" : $"{filter} のスキルを持っている";
        }
    }
}
