/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitEnemyPendingActionCondition.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief ユニット条件 : 敵の次の行動
 * =====================================*/

using System;
using AttributeUtility;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // ユニット条件: 実行待ちの行動に、指定条件に合う敵の行動が積まれているか
    //
    // 「相手が大技を構えたら防御に回る」「回復を撃たれる前に潰す」といった、
    // 相手の手を読んで動く判断のためのもの
    //
    // 行動はティック終了時にまとめて積まれるため、思考時点ではコマンド列が空になっている
    // そのため判定にはバトルの進行役が握っている実行待ちの行動（IPPPendingActionSource）が要る
    // 供給元が差し込まれていない場合は読む材料が無く、常に不成立になる
    //
    // 敵側の思考が自分より後に走った場合、その手はまだ計画に無い
    // 「必ず読める」ものではなく「読めたときだけ反応する」条件として使うこと
    [Serializable]
    [PPTypeMenuName("戦況/敵の次の行動")]
    public sealed class PPUnitEnemyPendingActionCondition : PPUnitConditionValidator, IPPUnitAISkillFilterOwner
    {
        // スキルで絞り込むか。false なら「何らかの行動が積まれているか」の判定になる
        [Label("スキルで絞り込む")]
        [SerializeField] private bool mIsFilterBySkill = true;
        [Label("対象スキル")]
        [EditCondition(nameof(mIsFilterBySkill), true, false)]
        [SerializeField] private PPUnitAISkillFilter mFilter = new();
        // 反転すると「そういう行動が積まれていない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 保持しているスキルの絞り込み条件。エディタの診断から参照する
        // 絞り込みを使わない設定のときは、未設定でも問題にならないよう null を返す
        public PPUnitAISkillFilter Filter => mIsFilterBySkill ? mFilter : null;

        // 相手陣営の実行待ちの行動に、条件へ合うものがあるかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            bool isFound = HasMatchedPending(aUnit, aSnapShot);
            return isFound != mIsInvert;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string body = mIsFilterBySkill
                ? $"敵が {mFilter.ToDisplayString()} のスキルを撃とうとしている"
                : "敵が行動を積んでいる";
            mDescription = mIsInvert ? $"{body} 状態ではない" : body;
        }

        // 相手陣営の実行待ちの行動を走査する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件へ合う行動が積まれていれば true
        private bool HasMatchedPending(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            var source = aSnapShot.PendingSource;
            if (source == null) return false;

            var opponent = aUnit.Side == BattleSide.Ally ? BattleSide.Enemy : BattleSide.Ally;
            foreach (var pending in source.EnumeratePending(opponent))
            {
                if (!mIsFilterBySkill) return true;

                // 通常攻撃のようにスキル定義を持たない行動は、スキルでの絞り込みには掛からない
                var definition = (pending.Command as SkillCommand)?.Skill?.SourceDefinition as PPSkillDefinition;
                if (mFilter.IsMatch(definition)) return true;
            }
            return false;
        }
    }
}
