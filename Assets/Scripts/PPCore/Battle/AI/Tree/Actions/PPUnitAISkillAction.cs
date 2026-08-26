/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAISkillAction.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief スキルを発動する行動
 * =====================================*/

using System;
using AttributeUtility;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 絞り込んだ所持スキルの中から 1 つを選んで発動する行動
    // 「攻撃グループで一番強いものを撃つ」「回復タグのスキルを撃つ」のように、
    // どのスキルを指すかは絞り込みと選び方の組み合わせで表現する
    // 発動できるスキルが無い、または対象が居ない場合は組み立てに失敗し、次の候補へ処理が渡る
    [Serializable]
    [PPTypeMenuName("スキル発動")]
    public sealed class PPUnitAISkillAction : PPUnitAIActionBase
    {
        [Header("スキル")]
        [Label("対象スキル")]
        [SerializeField] private PPUnitAISkillFilter mFilter = new();
        [Label("複数ある場合の選び方")]
        [SerializeField] private PPUnitAISkillSelectRule mSelectRule = PPUnitAISkillSelectRule.HighestAIScore;

        [Header("対象")]
        [Label("対象の選び方")]
        [SerializeField] private PPUnitAITargetPolicy mTargetPolicy = PPUnitAITargetPolicy.ScopeDefault;

        protected override string DefaultActionName => $"スキル発動（{mFilter.ToDisplayString()}）";

        // 今発動できるスキルを絞り込んで 1 つ選び、対象を解決してコマンドを組み立てる
        // aContext : 評価 1 回分の入力
        // return : 組み立てられたスキル発動。撃てない場合は Failed
        public override PPUnitAINodeResult Build(PPUnitAIEvalContext aContext)
        {
            var (skill, definition) = PPUnitAISkillQuery.SelectCastable(
                aContext.Unit, mFilter, mSelectRule, aContext.Battle, aContext.Snapshot.Ledger);
            if (skill == null) return PPUnitAINodeResult.Failed;

            var target = PPUnitAITargeting.Resolve(mTargetPolicy, aContext);
            // 単体対象のスキルは対象が決まらなければ撃てない
            // 全体・自己完結のスキルは対象未指定のままスキル既定のリゾルバへ任せる
            if (target == null && PPUnitAITargeting.NeedsExplicitTarget(definition.TargetScope))
            {
                target = ResolveDefaultTarget(definition, aContext);
                if (target == null) return PPUnitAINodeResult.Failed;
            }

            var resolver = PPUnitAITargeting.BuildResolver(definition.TargetScope, target);
            var command = new PPSkillCommand(aContext.Unit, skill, resolver);
            return PPUnitAINodeResult.Execute(command, ActionName, target);
        }

        // 対象の選び方が「スコープ既定」なのに単体対象だった場合の落としどころを決める
        // 味方単体なら HP 割合が最も低い味方、敵単体なら HP 割合が最も低い敵を狙う
        // aDefinition : 発動するスキルの定義
        // aContext : 評価 1 回分の入力
        // return : 決まった対象。候補が居なければ null
        private static PPBattleUnit ResolveDefaultTarget(PPSkillDefinition aDefinition, PPUnitAIEvalContext aContext)
            => aDefinition.TargetScope == TargetScope.SingleAlly
                ? aContext.Snapshot.LowestHpRatioAlly ?? aContext.Unit
                : aContext.Snapshot.LowestHpRatioEnemy;
    }
}
