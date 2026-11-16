/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPActionScheduler.cs
 * @author hqrse
 * @date 2026/11/16
 * @brief コインゲージが溜まったユニットを行動させる進行役
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;

namespace PPCore
{
    // コインゲージが行動コスト分溜まったユニットを行動させる進行役
    // 1 ユニットが同時に持てる行動は 1 つまでで、演出が終わるまで次の行動を積まない
    public class PPActionScheduler
    {
        /* 状態 */

        // 行動を積む先のバトルマネージャ。未接続なら null
        protected BattleManager mManager;
        // 行動の演出に掛かる時間（秒）。この間、そのユニットは次の行動を積まない
        protected float mPresentationSeconds;
        // 状態異常とクールダウンを 1 回分進める間隔（秒）
        protected float mStatusTickInterval;
        // 行動を積んでから演出が終わるまでのユニット
        private readonly HashSet<PPBattleUnit> mActingUnits = new();
        // 今回の評価で行動できるユニット。評価のたびに使い回す
        private readonly List<PPPendingAction> mReadyActions = new();
        // 評価の途中か。行動の結果として評価が入れ子に呼ばれるのを防ぐ
        private bool mIsEvaluating;

        /* 初期化・接続 */

        // バトルへ接続し、コインが配られるたびに行動できるユニットを調べるようにする
        public void Bind(BattleManager aManager, float aPresentationSeconds)
        {
            Unbind();

            mManager = aManager;
            mPresentationSeconds = Mathf.Max(0f, aPresentationSeconds);
            if (mManager?.Context == null) return;

            if (mManager.Context.AllyParty is PPBattleParty ally) ally.OnCoinsDistributed += Evaluate;
            if (mManager.Context.EnemyParty is PPBattleParty enemy) enemy.OnCoinsDistributed += Evaluate;
        }

        // バトルとの接続を切る。バトル終了時や破棄時に呼ぶ
        public void Unbind()
        {
            if (mManager?.Context != null)
            {
                if (mManager.Context.AllyParty is PPBattleParty ally) ally.OnCoinsDistributed -= Evaluate;
                if (mManager.Context.EnemyParty is PPBattleParty enemy) enemy.OnCoinsDistributed -= Evaluate;
            }

            mManager = null;
            mActingUnits.Clear();
        }

        /* 操作 */

        // 状態異常とクールダウンを一定間隔で進める。秒数で管理する形へ置き換えるまでの暫定の仕組み
        public void StartStatusTick(float aInterval)
        {
            if (mManager?.Context == null) return;

            mStatusTickInterval = Mathf.Max(0.1f, aInterval);
            mManager.Context.Timers.Schedule(mStatusTickInterval, HandleStatusTick);
        }

        // 行動できるユニットを調べ、素早さの順に 1 体ずつ行動させる。コインゲージが変化したあとに呼ぶ
        public void Evaluate()
        {
            if (mManager?.Context == null || mIsEvaluating) return;
            if (mManager.StateMachine.Current == BattleState.BattleEnd) return;

            mIsEvaluating = true;
            try
            {
                var context = mManager.Context;
                mReadyActions.Clear();
                CollectReady(context.AllyParty);
                CollectReady(context.EnemyParty);
                if (mReadyActions.Count == 0) return;

                // 同時に溜まったユニットの順序はルール側へ委譲する（既定は素早さ + 乱数）
                var ordered = context.Rules is PPBattleRules rules
                    ? rules.ActionOrderResolver.ResolveOrder(mReadyActions, context)
                    : new List<PPPendingAction>(mReadyActions);

                foreach (var action in ordered)
                {
                    if (mManager.StateMachine.Current == BattleState.BattleEnd) break;
                    Act(action.Unit, context);
                }
            }
            finally
            {
                mIsEvaluating = false;
            }
        }

        // ユニット 1 体を行動させる。行動コストを支払ってコマンドを実行し、演出の終了を予約する
        protected virtual void Act(PPBattleUnit aUnit, BattleContext aContext)
        {
            // 先に行動したユニットの攻撃で倒れている場合がある
            if (!aUnit.IsAlive) return;

            // 対象は実行の直前に決める。先に行動したユニットが対象を倒している場合があるため
            var command = SelectCommand(aUnit, aContext);
            if (command == null) return;
            if (!PPGaugeUtility.TryPay(aUnit.ExtraParameters.CoinGauge, aUnit.ActionCost)) return;

            mActingUnits.Add(aUnit);
            mManager.EnqueueCommand(command);
            mManager.ExecuteAllCommands();
            aContext.Timers.Schedule(mPresentationSeconds, () => FinishPresentation(aUnit));
        }

        // 演出が終わったユニットを行動できる状態へ戻し、持ち越し分でまだ行動できるかを調べ直す
        protected virtual void FinishPresentation(PPBattleUnit aUnit)
        {
            mActingUnits.Remove(aUnit);
            Evaluate();
        }

        // パーティの中から、行動できるユニットを集める
        private void CollectReady(BattleParty aParty)
        {
            foreach (var member in aParty.ActiveMembers)
            {
                if (member is not PPBattleUnit unit || !IsReady(unit)) continue;

                mReadyActions.Add(new PPPendingAction(unit, null, PPSkillActionPriority.Normal));
            }
        }

        /* 評価・判定 */

        // ユニットが今行動できるか。生存していて演出中でなく、コインゲージが行動コスト分溜まっていれば true
        public virtual bool IsReady(PPBattleUnit aUnit)
        {
            if (aUnit == null || !aUnit.IsAlive || mActingUnits.Contains(aUnit)) return false;

            // 行動コストが 0 以下だと際限なく行動してしまうため、行動できないものとして扱う
            float cost = aUnit.ActionCost;
            return cost > 0f && PPGaugeUtility.CanPay(aUnit.ExtraParameters.CoinGauge, cost);
        }

        // ユニットが行う行動を決める。対象が居なければ null を返す
        protected virtual BattleCommandBase SelectCommand(PPBattleUnit aUnit, BattleContext aContext)
        {
            var target = ResolveDefaultTarget(aUnit, aContext);
            return target == null ? null : new PPAttackCommand(aUnit, new SingleEnemyResolver(target));
        }

        // 指定が無いときの対象を決める。正面の敵が生きていれば正面、倒れていれば残りの敵からランダムに選ぶ
        protected virtual PPBattleUnit ResolveDefaultTarget(PPBattleUnit aUnit, BattleContext aContext)
        {
            var enemies = aContext.GetOpponentParty(aUnit.Side).GetAliveActiveMembers();
            if (enemies.Count == 0) return null;

            var front = ResolveFrontPosition(aUnit.Position);
            foreach (var enemy in enemies)
            {
                if (enemy is PPBattleUnit ppEnemy && ppEnemy.Position == front) return ppEnemy;
            }

            // 自分の対象選びなので、自分の乱数列から引く
            return enemies[aUnit.ResolveRandom(aContext).NextInt(enemies.Count)] as PPBattleUnit;
        }

        /* イベントハンドラ */

        // 状態異常とクールダウンを 1 回分進め、決着していなければ次の回を予約する
        private void HandleStatusTick()
        {
            if (mManager?.Context == null) return;

            mManager.AdvanceTick();
            if (mManager.StateMachine.Current == BattleState.BattleEnd) return;

            mManager.Context.Timers.Schedule(mStatusTickInterval, HandleStatusTick);
        }

        /* ヘルパー */

        // 正面にあたる相手側の配置を返す。両陣営が向かい合うため、右と左は入れ替わる
        private static PPPartyPosition ResolveFrontPosition(PPPartyPosition aPosition) => aPosition switch
        {
            PPPartyPosition.Right => PPPartyPosition.Left,
            PPPartyPosition.Left => PPPartyPosition.Right,
            _ => PPPartyPosition.Center,
        };
    }
}
