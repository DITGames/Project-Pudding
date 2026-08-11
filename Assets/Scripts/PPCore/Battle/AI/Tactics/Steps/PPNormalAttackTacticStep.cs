/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPNormalAttackTacticStep.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 戦術ステップ : 通常攻撃
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 戦術ステップ: 通常攻撃を行う
    // 通常攻撃はスキル定義を持たないため、コストはユニットの通常攻撃コスト、
    // スコープは敵単体固定になる（対象選択方針で誰を殴るかだけ決める）
    [Serializable]
    [PPTypeMenuName("行動/通常攻撃")]
    public sealed class PPNormalAttackTacticStep : PPTacticStepBase
    {
        // このステップを今の盤面へ当てはめる
        // aSnap : パーティ状況スナップショット
        // aRuntime : 進行状況を保持するランタイム戦術
        // aLedger : 行動回数の仮押さえ帳
        // aReason : 解決できなかった場合の理由
        // return : 解決結果。解決できない場合は null
        public override PPTacticStepResolution Resolve(PPPartyAIContext aSnap, PPRuntimeTactics aRuntime,
            PPTacticActionLedger aLedger, out PPTacticRejectReason aReason)
        {
            var actor = SelectActor(aSnap, aLedger);
            if (actor == null)
            {
                aReason = PPTacticRejectReason.NoActor;
                return null;
            }

            // 通常攻撃は必ず相手が要るため、スコープ既定を指定されていても敵を 1 体決める
            var target = ResolveTarget(aSnap, aRuntime, actor) ?? aSnap.LowestHpRatioEnemy;
            if (target == null)
            {
                aReason = PPTacticRejectReason.NoTarget;
                return null;
            }

            float attackCost = actor.ExtraParameters.Get(PPParameterSet.ParameterIdAttackCost)?.CurrentValue ?? 0f;

            // ラムダに載せるためローカルへ退避する
            var user = actor;
            var victim = target;

            aReason = PPTacticRejectReason.None;
            return new PPTacticStepResolution
            {
                Actor = actor,
                Target = target,
                Skill = null,
                Cost = PPResourceCost.BaseCost(attackCost),
                RequiredActionCount = RequiredActionCount,
                BuildCommand = _ => new PPAttackCommand(user, new SingleEnemyResolver(victim)),
            };
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
            => mDescription = $"通常攻撃を {GetTargetPolicyString(TargetPolicy)} へ";
    }
}
