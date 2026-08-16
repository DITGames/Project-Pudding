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

            // 通常攻撃は必ず敵が要るため、スコープ既定を指定されていても敵を 1 体決める
            // 味方向けの対象選択方針を設定された場合も敵へ差し替える
            // SingleEnemyResolver は渡された対象の陣営を検証しないため、
            // ここで弾かないと設定ミスがそのまま味方への攻撃になる
            var target = ResolveEnemyTarget(aSnap, aRuntime, actor);
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

        // 攻撃対象を解決する。敵が返ってこない方針が設定されていた場合は敵へ差し替える
        // 対象選択方針は味方向けのものも選べるが、通常攻撃で味方を殴っても意味が無く、
        // リゾルバ側も陣営を検証しないため、ここで敵に寄せておく
        // aSnap : パーティ状況スナップショット
        // aRuntime : 進行状況を保持するランタイム戦術
        // aActor : 解決済みの実行者
        // return : 攻撃対象。生存する敵が居なければ null
        private PPBattleUnit ResolveEnemyTarget(PPPartyAIContext aSnap, PPRuntimeTactics aRuntime, PPBattleUnit aActor)
        {
            var target = ResolveTarget(aSnap, aRuntime, aActor);
            return aSnap.AliveEnemies.Contains(target) ? target : aSnap.LowestHpRatioEnemy;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
            => mDescription = $"通常攻撃を {GetTargetPolicyString(TargetPolicy)} へ";
    }
}
