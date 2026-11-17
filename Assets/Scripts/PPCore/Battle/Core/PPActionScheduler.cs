/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPActionScheduler.cs
 * @author hqrse
 * @date 2026/11/16
 * @brief コインゲージが溜まったユニットを行動させる進行役
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PPCore
{
    // コインゲージが行動コスト分溜まったユニットを行動させる進行役
    // 1 ユニットが同時に持てる行動は 1 つまでで、演出が終わるまで次の行動を積まない
    public class PPActionScheduler
    {
        /* 状態 */

        // ユニットの行動（演出）が終わったとき。AI はここで次の行動を決めて予約する
        public event Action<PPBattleUnit> OnActionFinished;
        // 行動を積む直前に、そのユニットのコマンド選択を開くかを尋ねる関数。開いた場合は true を返す
        // 開いた側は、選択が閉じたときに ResumeAfterSelection を呼ぶこと。未設定なら尋ねずに行動する
        public Func<PPBattleUnit, bool> SelectionRequester { get; set; }
        // コマンド選択が閉じるのを待っているユニット。待っていなければ null
        private PPBattleUnit mSelectingUnit;
        // 行動を積む先のバトルマネージャ。未接続なら null
        protected BattleManager mManager;
        // 行動の演出に掛かる時間（秒）。この間、そのユニットは次の行動を積まない
        protected float mPresentationSeconds;
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
            mManager.OnUnitDefeated += HandleUnitDefeated;
        }

        // バトルとの接続を切る。バトル終了時や破棄時に呼ぶ
        public void Unbind()
        {
            if (mManager?.Context != null)
            {
                if (mManager.Context.AllyParty is PPBattleParty ally) ally.OnCoinsDistributed -= Evaluate;
                if (mManager.Context.EnemyParty is PPBattleParty enemy) enemy.OnCoinsDistributed -= Evaluate;
            }
            if (mManager != null) mManager.OnUnitDefeated -= HandleUnitDefeated;

            mManager = null;
            mSelectingUnit = null;
            mActingUnits.Clear();
        }

        /* 操作 */

        // 行動できるユニットを調べ、素早さの順に 1 体ずつ行動させる。コインゲージが変化したあとに呼ぶ
        public void Evaluate()
        {
            // コマンド選択が開いている間は、選択が閉じるまで誰も行動させない
            if (mManager?.Context == null || mIsEvaluating || mSelectingUnit != null) return;
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
                    // コマンド選択を開いた場合は、残りのユニットも選択が閉じてから調べ直す
                    if (TryRequestSelection(action.Unit)) break;
                    Act(action.Unit, context);
                }
            }
            finally
            {
                mIsEvaluating = false;
            }
        }

        // コマンド選択が閉じたあとに呼ぶ。待っていたユニットを行動させ、残りのユニットを調べ直す
        public void ResumeAfterSelection()
        {
            var unit = mSelectingUnit;
            if (unit == null) return;

            mSelectingUnit = null;
            if (mManager?.Context == null || mManager.StateMachine.Current == BattleState.BattleEnd) return;

            // 選び直しを続けて尋ねないよう、このユニットは尋ねずに行動させる
            mIsEvaluating = true;
            try
            {
                if (IsReady(unit)) Act(unit, mManager.Context);
            }
            finally
            {
                mIsEvaluating = false;
            }

            Evaluate();
        }

        // プレイヤーが確定したコマンドを、そのユニットのスキルの選択へ反映する
        // スキルなら選択中のスキルにし、通常攻撃なら選択を取り消す。実際の行動はコインゲージが溜まったときに積まれる
        public virtual void ApplyPlayerCommand(BattleUnit aUnit, BattleCommandBase aCommand, BattleUnit aTarget)
        {
            if (aUnit is not PPBattleUnit unit) return;

            if (aCommand is SkillCommand skillCommand)
            {
                unit.SelectSkill(skillCommand.Skill, aTarget);
            }
            else if (aCommand is AttackCommand)
            {
                unit.ClearSkillSelection();
            }
        }

        // 行動を積む直前のコマンド選択を開く。開いた場合は、閉じるまでそのユニットを待たせる
        private bool TryRequestSelection(PPBattleUnit aUnit)
        {
            if (SelectionRequester == null || !IsReady(aUnit)) return false;
            if (!SelectionRequester(aUnit)) return false;

            mSelectingUnit = aUnit;
            return true;
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
            // 次に積む行動へ反映されるよう、調べ直す前に知らせる
            if (aUnit.IsAlive) OnActionFinished?.Invoke(aUnit);
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

        // ユニットが行う行動を決める。選択中のスキルを積めるならそのスキル、そうでなければ通常攻撃
        // 対象が居なければ null を返す
        protected virtual BattleCommandBase SelectCommand(PPBattleUnit aUnit, BattleContext aContext)
        {
            var skillCommand = BuildSelectedSkillCommand(aUnit, aContext);
            if (skillCommand != null) return skillCommand;

            var target = ResolveEnemyTarget(aUnit, aContext);
            return target == null ? null : new PPAttackCommand(aUnit, new SingleEnemyResolver(target));
        }

        // 選択中のスキルのコマンドを作る。未選択・積めない・対象が居ない場合は null を返す
        protected virtual BattleCommandBase BuildSelectedSkillCommand(PPBattleUnit aUnit, BattleContext aContext)
        {
            var skill = aUnit.SelectedSkill;
            if (skill == null || !CanQueueSkill(aUnit, skill, aContext)) return null;

            switch (skill.DefaultTargetResolver)
            {
                case SingleEnemyResolver:
                    var enemy = ResolveEnemyTarget(aUnit, aContext);
                    return enemy == null ? null : new PPSkillCommand(aUnit, skill, new SingleEnemyResolver(enemy));
                case SingleAllyResolver:
                    return new PPSkillCommand(aUnit, skill, new SingleAllyResolver(aUnit.SelectedSkillTarget));
                default:
                    return new PPSkillCommand(aUnit, skill);
            }
        }

        // 選択中のスキルを今積めるか。発動可能（スキルゲージが足りていてクールタイム中でない）なら true
        // スキル封じは「積んだうえで失敗する」扱いのため、他の条件を満たしていれば積める側に含める
        protected virtual bool CanQueueSkill(PPBattleUnit aUnit, BattleSkill aSkill, BattleContext aContext)
        {
            var validation = aContext.Rules.CastValidator.Validate(aUnit, aSkill, aContext);
            if (validation.CanCast) return true;
            if (validation.Reason != CastFailReason.Silenced) return false;

            float cost = (aSkill.SourceDefinition as PPSkillDefinition)?.SkillGaugeCost ?? 0f;
            return aSkill.IsReady && PPGaugeUtility.CanPay(aUnit.ExtraParameters.SkillGauge, cost);
        }

        // 狙う敵を決める。指定した相手が生きていればその相手、指定が無ければ指定なしの規則で選ぶ
        // 通常攻撃と敵単体が対象のスキルの両方に使う
        protected virtual PPBattleUnit ResolveEnemyTarget(PPBattleUnit aUnit, BattleContext aContext)
        {
            if (aUnit.DesignatedTarget is PPBattleUnit { IsAlive: true } designated) return designated;

            return ResolveDefaultTarget(aUnit, aContext);
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

        // ユニットが倒れたとき、そのユニットを狙う相手に指定していた指定を解除する
        private void HandleUnitDefeated(BattleUnit aDefeated)
        {
            if (mManager?.Context == null || aDefeated == null) return;

            foreach (var member in mManager.Context.GetOpponentParty(aDefeated.Side).ActiveMembers)
            {
                if (member is PPBattleUnit unit && unit.DesignatedTarget == aDefeated) unit.ClearDesignatedTarget();
            }
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
