/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitCanCastSkillCondition.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニット条件 : 指定条件のスキルが今発動できる
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ユニット条件: 同種グループ・タグで絞り込んだスキルを今すぐ発動できるか
    // クールダウン・使用回数・スキルゲージ残量まで含めて判定する
    // 「所持している中で最も強いものに限る」を有効にすると、
    // 下位スキルで妥協せず本命が撃てるときだけ成立する（待ちの判断と組み合わせて使う）
    [Serializable]
    [PPTypeMenuName("スキル/発動できる")]
    public sealed class PPUnitCanCastSkillCondition : PPUnitConditionValidator, IPPUnitAISkillFilterOwner
    {
        [Label("対象スキル")]
        [SerializeField] private PPUnitAISkillFilter mFilter = new();
        // 絞り込んだ中で最も AI スコアの高いスキルが発動できる場合だけ成立させるか
        [Label("最も強いものに限る")]
        [SerializeField] private bool mIsStrongestOnly = false;

        // 保持しているスキルの絞り込み条件。エディタの診断から参照する
        public PPUnitAISkillFilter Filter => mFilter;
        // 反転すると「そのスキルが撃てない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 絞り込みに合致するスキルが発動可能かを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            bool isCastable = mIsStrongestOnly
                ? IsStrongestCastable(aUnit, aSnapShot)
                : PPUnitAISkillQuery.SelectCastable(aUnit, mFilter,
                    PPUnitAISkillSelectRule.HighestAIScore, aSnapShot.Context, aSnapShot.Ledger).Skill != null;

            return isCastable != mIsInvert;
        }

        // 絞り込んだ中で最も強いスキルが、今まさに発動できるかを判定する
        // 所持している最強スキルと、今撃てる中の最強スキルが同一かどうかで見る
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 最強スキルが発動可能なら true
        private bool IsStrongestCastable(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            var strongest = PPUnitAISkillQuery.SelectStrongest(aUnit, mFilter);
            if (strongest == null) return false;

            var (_, castable) = PPUnitAISkillQuery.SelectCastable(aUnit, mFilter,
                PPUnitAISkillSelectRule.HighestAIScore, aSnapShot.Context, aSnapShot.Ledger);
            return ReferenceEquals(castable, strongest);
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string filter = mFilter.ToDisplayString();
            string scope = mIsStrongestOnly ? "で最も強いスキル" : " のスキル";
            mDescription = mIsInvert ? $"{filter}{scope}が撃てない" : $"{filter}{scope}が撃てる";
        }
    }
}
