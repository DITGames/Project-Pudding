/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyTacticsStrategist.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 戦術リストをもとにパーティの行動計画を立てるAI
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // 戦術（PPBattleTacticsDefinition）をもとにパーティ単位の行動計画を立てる AI
    //
    // ユニットごとに行動候補を出して並べるのではなく、まず「今どの戦術を取るか」を決め、
    // その戦術のステップに合う行動だけを集めて実行する。
    // 行動を選んだ結果として戦術が決まるのではなく、戦術を決めてから中身を埋めるのが要点。
    //
    // 思考 1 回の流れは次の通り
    //   1. ティックが進んでいれば全戦術のクールタイムを消化する
    //   2. パーティ状況をスナップショットし、リソース推移をサンプリングする
    //   3. 全戦術を更新して、実行できるものを列挙する（UpdateTacticsForThink）
    //   4. 優先度と進行状況からメイン戦術を決める（SelectMainTactics）
    //   5. メイン戦術のステップを、リソースと行動回数が許す範囲で積む（BuildPlan）
    //
    // 戦術の進行位置はランタイム戦術（PPRuntimeTactics）が思考をまたいで保持するため、
    // 1 回の思考で終わらない複数手順の戦術も途中から続行できる。
    //
    // 挙動のチューニングはコードではなく戦術アセットと AI プロファイルで行う。
    // 乱数は必ず aContext.Rules.RandomProvider を経由すること（UnityEngine.Random は使わない）
    public class PPPartyTacticsStrategist : IPPPartyCommandStrategist
    {
        // 戦術リストと思考設定をまとめた設定アセット
        protected readonly PPPartyAIProfileDefinition mProfile;
        // リソース増加トレンドの記録。「待てば撃てるか」の判断に使う
        protected readonly PPIncomTrendTracker mTrend = new();
        // プロファイルの戦術リストから生成したランタイム戦術。並び順がそのまま優先度になる
        protected readonly List<PPRuntimeTactics> mTactics = new();

        // 現在のメイン戦術。切り替わったときに前のものの進行をリセットする
        private PPRuntimeTactics mMainTactics;
        // クールタイム消化の基準にする、前回思考時のターン数
        private int mLastTurnCount = -1;
        // 戦術が 1 つも無い旨の警告を出したか。毎思考で出し続けないための抑止
        private bool mIsWarnedEmpty;

        // 生成済みのランタイム戦術。デバッグ表示から参照する
        public IReadOnlyList<PPRuntimeTactics> Tactics => mTactics;
        // 直近の思考で選ばれたメイン戦術。デバッグ表示から参照する
        // mMainTactics は「進行中の戦術」を指すため完走した時点で null に戻る
        // 記録用にはそれとは別に、その思考で何を選んだかをここへ残す
        public PPRuntimeTactics LastSelectedTactics { get; private set; }
        // 直近の思考でメイン戦術が選ばれた理由。デバッグ表示用
        public string LastMainSelectReason { get; private set; } = "候補なし";

        // aProfile : AI プロファイル。null の場合は戦術を 1 つも持たず、常に待機する
        public PPPartyTacticsStrategist(PPPartyAIProfileDefinition aProfile)
        {
            mProfile = aProfile;
            if (aProfile == null) return;

            // 初期化時にランタイム戦術を作る。優先度はリストの並び順（上ほど高い）
            int priority = 0;
            foreach (var definition in aProfile.Tactics)
            {
                if (definition == null) continue;

                mTactics.Add(new PPRuntimeTactics(definition, priority));
                priority++;
            }
        }

        // このティックでパーティが取る行動計画を組み立てる
        // aSelf : 思考主体のパーティ。PPBattleParty でなければ待機を返す
        // aContext : バトルコンテキスト
        // return : 採用された行動の割り当て。何も採用できなければ PPPartyPlan.Wait
        public PPPartyPlan PlanActions(BattleParty aSelf, BattleContext aContext)
        {
            if (aSelf is not PPBattleParty party)
                return PPPartyPlan.Wait;

            if (mTactics.Count == 0)
            {
                if (!mIsWarnedEmpty)
                {
                    mIsWarnedEmpty = true;
                    CustomConsoleLog.Warning("AI", "戦術が 1 つも設定されていないため、このパーティは常に待機します。");
                }
                return PPPartyPlan.Wait;
            }

            // パーティ情報収集
            var snap = PPPartyAIContext.Capture(party, aContext);
            if (snap.AliveMembers.Count == 0 || snap.AliveEnemies.Count == 0)
                return PPPartyPlan.Wait;

            // リソース推移のサンプリングはティックが進んだときだけ行う
            // 思考のたびに取ると増加量が「1 思考あたり」の値になり、
            // 待ち判定で使うティック数の見積もりが思考回数の分だけずれる
            if (SyncTick(aContext))
            {
                mTrend.Sample(snap.Current(PPTypeAttribute.Normal), mProfile.TrendSampleCount);
            }

            // 思考のたびに全戦術を評価し直す。対象も進行位置もここで解決し直される
            var executables = UpdateTacticsForThink(snap, party);
            var main = SelectMainTactics(executables);

            // メイン戦術が入れ替わったら、前のものの進行は破棄する
            // 破棄しても達成済み判定があるため、再選出時は適切な位置から再開される
            if (!ReferenceEquals(mMainTactics, main))
            {
                mMainTactics?.ResetProgress();
                mMainTactics = main;
            }
            LastSelectedTactics = main;

            CustomConsoleLog.Log("AI", main != null
                ? $"MainTactics: {main.Definition.TacticsName} ({LastMainSelectReason}) step={main.StepIndex}/{main.Definition.Steps.Count}"
                : "MainTactics: なし（待機）");

            var plan = main == null ? PPPartyPlan.Wait : BuildPlan(main, snap, party, aContext);
            ReportThink(party, aContext, plan);
            return plan;
        }

        // ティックの進行を検出してクールタイムを消化する
        // 思考はティックより細かい周期で回るため、経過ティック数で数えないと
        // クールタイムが思考回数分だけ速く溶けてしまう
        // aContext : ターン数を引くバトルコンテキスト
        // return : 前回の思考からティックが進んでいた場合 true
        protected bool SyncTick(BattleContext aContext)
        {
            if (mLastTurnCount == aContext.TurnCount) return false;

            // 初回はティック差を求められないため、基準を合わせるだけにする
            if (mLastTurnCount < 0)
            {
                mLastTurnCount = aContext.TurnCount;
                return false;
            }

            // ターン数が巻き戻っていたら別のバトルが始まったとみなして初期化する
            if (aContext.TurnCount < mLastTurnCount)
            {
                ResetForBattle();
                mLastTurnCount = aContext.TurnCount;
                return false;
            }

            for (int i = mLastTurnCount; i < aContext.TurnCount; i++)
            {
                foreach (var tactics in mTactics)
                {
                    tactics.TickCooldown();
                }
            }
            mLastTurnCount = aContext.TurnCount;
            return true;
        }

        // 全ランタイム戦術を今の盤面で評価し直し、実行できるものを列挙する
        // 落ちた戦術には理由を記録しておき、デバッグウィンドウから追えるようにする
        // aSnap : パーティ状況スナップショット
        // aParty : 思考主体のパーティ
        // return : この思考で実行できる戦術。優先度順ではない
        protected List<PPRuntimeTactics> UpdateTacticsForThink(PPPartyAIContext aSnap, PPBattleParty aParty)
        {
            // 評価用の帳簿。ここでは何も確保せず、実残量に対して判定するだけ
            var ledger = new PPTacticActionLedger();
            var budget = new PPResourceBudget(aParty.ResourcePool);
            var executables = new List<PPRuntimeTactics>();

            foreach (var tactics in mTactics)
            {
                if (tactics.IsDoneOnce)
                {
                    tactics.SetThinkResult(null, PPTacticRejectReason.DoneOnce, 0f, false);
                    continue;
                }
                if (tactics.RemainingCooldown > 0)
                {
                    tactics.SetThinkResult(null, PPTacticRejectReason.Cooldown, 0f, false);
                    continue;
                }
                if (!tactics.Definition.EvaluateConditions(aSnap))
                {
                    tactics.SetThinkResult(null, PPTacticRejectReason.ConditionFailed, 0f, false);
                    continue;
                }

                // ステップを持たない戦術は「何もしない」＝溜め。条件さえ通れば常に実行できる
                if (tactics.Definition.ValidStepCount == 0)
                {
                    tactics.SetThinkResult(null, PPTacticRejectReason.None, 0f, true);
                    executables.Add(tactics);
                    continue;
                }

                // 達成済みステップを飛ばして開始位置を決め直す
                // 進行中の戦術も毎回やり直すため、バフが切れていればそのステップへ戻る
                int startIndex = ResolveStartStepIndex(tactics, aSnap);
                if (startIndex >= tactics.Definition.Steps.Count)
                {
                    // 全ステップが達成済み。実行するものが無いので完走として畳む
                    tactics.Complete();
                    tactics.SetThinkResult(null, PPTacticRejectReason.NoSteps, 0f, false);
                    continue;
                }
                tactics.SetStepIndex(startIndex);

                var resolution = ResolveCurrentStep(tactics, aSnap, ledger, out var reason);
                if (resolution == null)
                {
                    tactics.SetThinkResult(null, reason, 0f, false);
                    continue;
                }

                // 実行可能予測。今払えないなら「あと何ティックで払えるか」を見積もって判断する
                bool isAffordable = budget.CanAfford(resolution.Cost);
                float waitTicks = 0f;
                if (!isAffordable)
                {
                    waitTicks = EstimateWaitTicks(resolution.Cost, budget);
                    if (float.IsPositiveInfinity(waitTicks))
                    {
                        tactics.SetThinkResult(null, PPTacticRejectReason.NoIncome, waitTicks, false);
                        continue;
                    }
                    if (waitTicks > tactics.Definition.AllowedWaitTicks)
                    {
                        tactics.SetThinkResult(null, PPTacticRejectReason.TooFarToWait, waitTicks, false);
                        continue;
                    }
                }

                tactics.SetThinkResult(resolution, PPTacticRejectReason.None, waitTicks, isAffordable);
                executables.Add(tactics);
            }

            return executables;
        }

        // 戦術をどのステップから始めるかを決める
        // 「常に先頭から実行」が有効なら達成済み判定を行わず先頭に戻す
        // aTactics : 対象のランタイム戦術
        // aSnap : パーティ状況スナップショット
        // return : 開始するステップの位置。全て達成済みならステップ数と同じ値
        protected static int ResolveStartStepIndex(PPRuntimeTactics aTactics, PPPartyAIContext aSnap)
        {
            var steps = aTactics.Definition.Steps;

            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] == null) continue;
                if (aTactics.Definition.IsAlwaysRestart) return i;
                if (!steps[i].IsCompleted(aSnap, aTactics)) return i;
            }
            return steps.Count;
        }

        // 現在ステップを解決する。null 要素は飛ばしながら進める
        // aTactics : 対象のランタイム戦術
        // aSnap : パーティ状況スナップショット
        // aLedger : 行動回数の仮押さえ帳
        // aReason : 解決できなかった場合の理由
        // return : 解決結果。解決できない場合は null
        protected static PPTacticStepResolution ResolveCurrentStep(PPRuntimeTactics aTactics, PPPartyAIContext aSnap,
            PPTacticActionLedger aLedger, out PPTacticRejectReason aReason)
        {
            aReason = PPTacticRejectReason.NoSteps;

            while (aTactics.HasRemainingStep)
            {
                var step = aTactics.Definition.Steps[aTactics.StepIndex];
                if (step == null)
                {
                    aTactics.SetStepIndex(aTactics.StepIndex + 1);
                    continue;
                }
                return step.Resolve(aSnap, aTactics, aLedger, out aReason);
            }
            return null;
        }

        // コストの不足分をリソース推移で割り、撃てるまでのティック数を見積もる
        // 増加が見込めない場合は無限大を返し、呼び出し側で「待っても撃てない」と判断させる
        // aCost : 判定するコスト
        // aBudget : 判定に使うリソース予算
        // return : 撃てるまでの見積もりティック数
        protected float EstimateWaitTicks(PPResourceCost aCost, PPResourceBudget aBudget)
        {
            if (aCost == null || aCost.IsFree) return 0f;

            float shortfall = 0f;
            for (int i = 0; i < PPTypeAttributeDefinition.TypeCount; i++)
            {
                shortfall += Mathf.Max(0f, aCost.Get(i) - aBudget.Remaining((PPTypeAttribute)i));
            }
            if (shortfall <= 0f) return 0f;

            float gainPerTick = mTrend.AverageRecentGainPerTick;
            return gainPerTick > 0f ? shortfall / gainPerTick : float.PositiveInfinity;
        }

        // 実行できる戦術からメイン戦術を決める
        // 基本は優先度が最も高いものを選ぶが、進行中の戦術がまだ実行できる場合は、
        // それより高い優先度の候補が出てこない限り継続する
        // 途中まで進めた手順が同格の戦術に横取りされて振り出しに戻るのを防ぐため
        // aExecutables : この思考で実行できる戦術
        // return : メイン戦術。候補が無ければ null
        protected PPRuntimeTactics SelectMainTactics(List<PPRuntimeTactics> aExecutables)
        {
            if (aExecutables.Count == 0)
            {
                LastMainSelectReason = "候補なし";
                return null;
            }

            PPRuntimeTactics best = null;
            PPRuntimeTactics inProgress = null;
            foreach (var tactics in aExecutables)
            {
                if (best == null || tactics.Priority < best.Priority)
                {
                    best = tactics;
                }
                if (tactics.IsInProgress && (inProgress == null || tactics.Priority < inProgress.Priority))
                {
                    inProgress = tactics;
                }
            }

            if (inProgress != null && best.Priority >= inProgress.Priority)
            {
                LastMainSelectReason = "進行継続";
                return inProgress;
            }

            LastMainSelectReason = inProgress != null ? "割り込み" : "新規選出";
            return best;
        }

        // メイン戦術のステップを、リソースと行動回数が許す範囲で積んで計画にする
        // 払えないステップに到達した時点で打ち切り、残りは次の思考へ持ち越す
        // aMain : メイン戦術
        // aSnap : パーティ状況スナップショット
        // aParty : 思考主体のパーティ
        // aContext : バトルコンテキスト
        // return : 採用された行動の割り当て。何も積めなければ PPPartyPlan.Wait
        protected PPPartyPlan BuildPlan(PPRuntimeTactics aMain, PPPartyAIContext aSnap, PPBattleParty aParty,
            BattleContext aContext)
        {
            // ステップを持たない戦術＝溜め。何も積まずに畳んでクールタイムへ入れる
            if (aMain.Definition.ValidStepCount == 0)
            {
                aMain.Complete();
                mMainTactics = null;
                return PPPartyPlan.Wait;
            }

            // 実行可能予測で「待つ」と判断された戦術は、この思考では何もしない
            if (!aMain.IsAffordableNow)
            {
                return PPPartyPlan.Wait;
            }

            var budget = new PPResourceBudget(aParty.ResourcePool);
            var ledger = new PPTacticActionLedger();
            var picks = new List<PPPartyActionAssignment>();
            var resolution = aMain.CurrentResolution;
            int order = 0;

            while (resolution != null)
            {
                if (!budget.CanAfford(resolution.Cost)) break;
                if (!ledger.CanAct(resolution.Actor, resolution.RequiredActionCount)) break;

                budget.TrySpend(resolution.Cost);
                ledger.Reserve(resolution.Actor, resolution.RequiredActionCount);

                // 実行順はステップの並び順をそのまま使う
                picks.Add(new PPPartyActionAssignment(resolution.Actor, resolution.BuildCommand(aContext), order));
                order++;

                CustomConsoleLog.Verbose("AI",
                    $"{aMain.Definition.TacticsName} step{aMain.StepIndex}: {resolution.Actor.DisplayName} -> {resolution.DisplayName} -> {resolution.Target?.DisplayName ?? "-"}");

                aMain.AdvanceStep(resolution);
                resolution = ResolveCurrentStep(aMain, aSnap, ledger, out _);
            }

            // 最終ステップまで消化できたら完走。クールタイムはここから数え始める
            if (!aMain.HasRemainingStep)
            {
                aMain.Complete();
                mMainTactics = null;
            }

            return picks.Count == 0 ? PPPartyPlan.Wait : new PPPartyPlan(picks);
        }

        // バトル開始時の初期化。全戦術の進行・クールタイム・消化状況を戻す
        public void ResetForBattle()
        {
            foreach (var tactics in mTactics)
            {
                tactics.ResetForBattle();
            }
            mMainTactics = null;
            LastSelectedTactics = null;
            mLastTurnCount = -1;
        }

        // 思考記録をデバッグハブへ流す。購読者が居なければ何もしない
        // aParty : 思考主体のパーティ
        // aContext : バトルコンテキスト
        // aPlan : 組み上がった行動計画
        protected void ReportThink(PPBattleParty aParty, BattleContext aContext, PPPartyPlan aPlan)
        {
            if (!PPTacticsDebugHub.HasListener) return;

            // BattleParty は陣営を公開していないため、コンテキストとの参照比較で判定する
            var report = new PPTacticsThinkReport
            {
                Side = ReferenceEquals(aParty, aContext.EnemyParty) ? BattleSide.Enemy : BattleSide.Ally,
                TurnCount = aContext.TurnCount,
                Timestamp = Time.time,
                MainTacticsName = LastSelectedTactics != null ? LastSelectedTactics.Definition.TacticsName : "(待機)",
                MainSelectReason = LastMainSelectReason,
                AverageGainPerTick = mTrend.AverageRecentGainPerTick,
                AdoptedCount = aPlan?.Assignments.Count ?? 0,
            };

            foreach (var tactics in mTactics)
            {
                var resolution = tactics.CurrentResolution;
                report.Tactics.Add(new PPTacticsThinkEntry
                {
                    TacticsName = tactics.Definition.TacticsName,
                    Priority = tactics.Priority,
                    IsExecutable = tactics.LastRejectReason == PPTacticRejectReason.None,
                    RejectReason = tactics.LastRejectReason,
                    StepIndex = tactics.StepIndex,
                    StepCount = tactics.Definition.Steps.Count,
                    ActorName = resolution?.Actor?.DisplayName ?? "-",
                    TargetName = resolution?.Target?.DisplayName ?? "-",
                    ActionName = resolution?.DisplayName ?? "-",
                    RemainingCooldown = tactics.RemainingCooldown,
                    EstimatedWaitTicks = tactics.EstimatedWaitTicks,
                    IsAffordableNow = tactics.IsAffordableNow,
                });
            }

            PPTacticsDebugHub.Report(report);
        }
    }
}
