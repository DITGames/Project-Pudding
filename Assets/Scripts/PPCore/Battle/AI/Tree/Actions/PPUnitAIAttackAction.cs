/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAIAttackAction.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief 通常攻撃を実行する行動
 * =====================================*/

using System;
using AttributeUtility;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // コインゲージを消費して通常攻撃を行う行動
    // 通常攻撃は消費量・威力ともに固定で強弱の段階を持たないため、設定は対象の選び方だけ
    // コインゲージが足りない、または対象が居ない場合は組み立てに失敗し、次の候補へ処理が渡る
    [Serializable]
    [PPTypeMenuName("通常攻撃")]
    public sealed class PPUnitAIAttackAction : PPUnitAIActionBase
    {
        [Header("対象")]
        [Label("対象の選び方")]
        [SerializeField] private PPUnitAITargetPolicy mTargetPolicy = PPUnitAITargetPolicy.LowestHpRatioEnemy;
        // 指定した選び方で対象が見つからなかった場合に、HP 割合が最も低い敵へ切り替えるか
        // 「倒せる敵」「弱点を突ける敵」のように、該当者が居ないことが普通にある方針で使う
        [Label("見つからなければ他の敵を狙う")]
        [SerializeField] private bool mIsFallbackAnyEnemy = true;

        protected override string DefaultActionName => "通常攻撃";

        // 狙う相手と、外れたときの振る舞いを 1 行で示す
        public override string Summary
            => $"対象 : {PPUnitAITargeting.ToDisplayString(mTargetPolicy)}"
               + (mIsFallbackAnyEnemy ? "（居なければ他の敵）" : "");

        // コインゲージの残量と対象を確認し、足りていれば通常攻撃コマンドを組み立てる
        // aContext : 評価 1 回分の入力
        // return : 組み立てられた通常攻撃。撃てない場合は Failed
        public override PPUnitAINodeResult Build(PPUnitAIEvalContext aContext)
        {
            var unit = aContext.Unit;

            // 生成時点のコストで支払えるかを先に見る。実際の消費はコマンド実行時に行われる
            // 台帳がある場合は、同じティックで既に積んだ行動が使う予定の分も差し引いて判定する
            float attackCost = unit.ExtraParameters.Get(PPParameterSet.ParameterIdAttackCost)?.CurrentValue ?? 0f;
            var ledger = aContext.Snapshot.Ledger;
            bool canPay = ledger != null
                ? ledger.CanReserveCoin(unit, attackCost)
                : PPGaugeUtility.CanPay(unit.ExtraParameters.CoinGauge, attackCost);
            if (!canPay) return PPUnitAINodeResult.Failed;

            var target = PPUnitAITargeting.Resolve(mTargetPolicy, aContext);
            if (target == null && mIsFallbackAnyEnemy)
            {
                target = aContext.Snapshot.LowestHpRatioEnemy;
            }
            if (target == null)
                return PPUnitAINodeResult.Failed;

            var command = new PPAttackCommand(unit, new SingleEnemyResolver(target));
            return PPUnitAINodeResult.Execute(command, ActionName, target);
        }
    }
}
