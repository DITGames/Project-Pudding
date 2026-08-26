/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAIStrategist.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニット単位で判断ツリーを評価するAI
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // ユニット単位で判断ツリーを評価する AI
    //
    // パーティ共有のリソースを誰に割り当てるかを調停するのではなく、
    // ゲージを専有する各ユニットが自分の判断ツリー（PPUnitAIProfileDefinition）を評価して行動を決める
    // このクラス自体は判断を持たず、次の 4 つだけを担当する
    //   1. 生存ユニットを順に回して、それぞれのツリーを評価する
    //   2. 待機コミット（決めた判断を数ティック維持する）の記録と消化
    //   3. 確定した行動を 1 ティック分の計画へ積む
    //   4. 判断の経過をログとデバッグウィンドウへ流す
    //
    // 判断そのものはツリーが持つため、挙動のチューニングはコードではなくプロファイルアセットで行う
    public class PPUnitAIStrategist : IPPPartyCommandStrategist
    {
        // ユニット 1 体分の待機コミット
        // 「どの枝で待つと決めたか（道順）」と「あと何ティック維持するか」を持つ
        protected sealed class PPUnitAICommit
        {
            // 待ちを宣言した行動へ至る道順。優先度リストの子の添字を根から並べたもの
            public List<int> Path = new();
            // 残りの維持ティック数
            public int RemainingTicks;
        }

        // この思考で組み立てた判断記録。思考のたびに作り直す
        private readonly List<PPUnitAIThinkEntry> mThinkEntries = new();
        // ユニットごとの待機コミット
        private readonly Dictionary<PPBattleUnit, PPUnitAICommit> mCommits = new();
        // この思考で積んだ行動の仮押さえ台帳。2 手目以降の判断で 1 手目の消費予定を差し引くために使う
        private readonly PPUnitActionLedger mLedger = new();

        // コミット消化の基準にする、前回思考時のターン数
        private int mLastTurnCount = -1;
        // AI プロファイルを持つユニットが 1 体も居ない旨の警告を出したか。毎思考で出し続けないための抑止
        private bool mIsWarnedEmpty;

        // このティックでパーティが取る行動計画を組み立てる
        // aSelf : 思考主体のパーティ。PPBattleParty でなければ待機を返す
        // aContext : バトルコンテキスト
        // return : 採用された行動の割り当て。何も採用できなければ PPPartyPlan.Wait
        public PPPartyPlan PlanActions(BattleParty aSelf, BattleContext aContext)
        {
            if (aSelf is not PPBattleParty party)
                return PPPartyPlan.Wait;

            var snap = PPPartyAIContext.Capture(party, aContext);
            if (snap.AliveMembers.Count == 0 || snap.AliveEnemies.Count == 0)
                return PPPartyPlan.Wait;

            snap.AttachLedger(mLedger);

            // コミットの消化はティックが進んだときだけ行う
            // 思考のたびに減らすと、維持ティック数が思考回数の分だけ短くなってしまう
            SyncTick(aContext);

            mThinkEntries.Clear();
            mLedger.Clear();
            var picks = new List<PPPartyActionAssignment>();
            int order = 0;
            bool hasProfile = false;

            // ユニットごとに独立して判断する。順序はパーティの編成順で固定される
            foreach (var unit in snap.AliveMembers)
            {
                var profile = ResolveProfile(unit);
                if (profile != null) hasProfile = true;

                var entry = CreateEntry(unit);
                // 行動回数の上限まで積む。積むたびに台帳へ仮押さえするため、
                // 2 手目以降は 1 手目で使う予定のゲージを差し引いた状態で判断される
                while (mLedger.HasActionLeft(unit))
                {
                    var command = DecideUnitAction(unit, profile, snap, aContext, entry);
                    if (command == null) break;

                    ReserveCost(unit, command);
                    picks.Add(new PPPartyActionAssignment(unit, command, order));
                    order++;
                }
                mThinkEntries.Add(entry);
            }

            WarnIfNoProfile(hasProfile);

            var plan = picks.Count == 0 ? PPPartyPlan.Wait : new PPPartyPlan(picks);
            ReportThink(party, aContext, plan);
            return plan;
        }

        // ユニット 1 体分の判断ツリーを評価して行動を決める
        // aUnit : 判断対象のユニット
        // aProfile : そのユニットの判断ツリー。null なら思考せず待機する
        // aSnap : パーティ状況スナップショット
        // aContext : バトルコンテキスト
        // aEntry : 判断結果の記録先
        // return : 採用されたコマンド。何もしない場合は null
        protected virtual BattleCommandBase DecideUnitAction(PPBattleUnit aUnit, PPUnitAIProfileDefinition aProfile,
            PPPartyAIContext aSnap, BattleContext aContext, PPUnitAIThinkEntry aEntry)
        {
            if (aProfile == null)
            {
                aEntry.RejectReason = PPUnitAIRejectReason.NoProfile;
                return null;
            }
            // 行動回数を使い切っているユニットはコマンドを積んでも空振りするため、ここで外す
            if (!aUnit.Actions.CanAction)
            {
                aEntry.RejectReason = PPUnitAIRejectReason.NoActionBudget;
                return null;
            }

            var result = EvaluateTree(aUnit, aProfile, aSnap, aContext, aEntry);
            if (!result.IsDecided)
            {
                // どの枝も成立しなかった。ツリーの取りこぼしなので、末尾に無条件の行動を置けば埋められる
                aEntry.RejectReason = PPUnitAIRejectReason.NoMatchedNode;
                return null;
            }

            aEntry.ActionName = result.ActionName ?? "-";
            aEntry.TargetName = result.Target?.DisplayName ?? "-";

            // コマンドを持たない結果は「待機」で確定したことを表す
            if (result.Command == null)
            {
                aEntry.Decision = PPUnitAIDecision.Wait;
                aEntry.RejectReason = PPUnitAIRejectReason.DecidedToWait;
                return null;
            }

            aEntry.Decision = result.Command is PPSkillCommand ? PPUnitAIDecision.Skill : PPUnitAIDecision.NormalAttack;
            aEntry.RejectReason = PPUnitAIRejectReason.None;
            return result.Command;
        }

        // 判断ツリーを評価し、待機コミットの適用と更新まで行う
        // コミット中は前回の道順で評価範囲を絞るが、その枝が崩れていた場合は制約を外して評価し直す
        // 待ち条件が成立しなくなったまま何もできない、という状態を避けるため
        // aUnit : 判断対象のユニット
        // aProfile : そのユニットの判断ツリー
        // aSnap : パーティ状況スナップショット
        // aContext : バトルコンテキスト
        // aEntry : 判断結果の記録先
        // return : ツリーが確定させた行動
        protected virtual PPUnitAINodeResult EvaluateTree(PPBattleUnit aUnit, PPUnitAIProfileDefinition aProfile,
            PPPartyAIContext aSnap, BattleContext aContext, PPUnitAIThinkEntry aEntry)
        {
            // スナップショットはパーティ内の全ユニットで共有されるため、
            // 前のユニットが積んだ対象候補を持ち越さないようここで空にする
            aSnap.ResetConditionedUnits();

            var evalContext = new PPUnitAIEvalContext(aUnit, aSnap, aContext, aProfile);
            var commit = ResolveCommit(aUnit);
            if (commit != null)
            {
                evalContext.CommitPath = commit.Path;
                aEntry.CommitRemainingTicks = commit.RemainingTicks;
            }

            var result = aProfile.Evaluate(evalContext);

            // コミットした枝が崩れていたら、制約を外して普通に選び直す
            if (!result.IsDecided && commit != null)
            {
                evalContext.CommitPath = null;
                evalContext.ResetPath();
                // 1 回目の評価で積まれた候補は、通らなかった枝のものなので捨ててから引き直す
                aSnap.ResetConditionedUnits();
                result = aProfile.Evaluate(evalContext);
                aEntry.CommitRemainingTicks = 0;
            }

            UpdateCommit(aUnit, evalContext, result, aEntry);
            return result;
        }

        // 評価結果に応じて待機コミットを張り直す
        // 維持ティック数を持つ行動を選んだ場合はその道順ごと記録し、
        // それ以外を選んだ場合はコミットを解除して次のティックから自由に選ばせる
        // aUnit : 判断対象のユニット
        // aEvalContext : 評価に使ったコンテキスト。確定時の道順を持つ
        // aResult : ツリーが確定させた行動
        // aEntry : 判断結果の記録先
        protected virtual void UpdateCommit(PPBattleUnit aUnit, PPUnitAIEvalContext aEvalContext,
            PPUnitAINodeResult aResult, PPUnitAIThinkEntry aEntry)
        {
            if (aResult.IsDecided && aResult.CommitTicks > 0)
            {
                var commit = new PPUnitAICommit { RemainingTicks = aResult.CommitTicks };
                commit.Path.AddRange(aEvalContext.Path);
                mCommits[aUnit] = commit;
                aEntry.CommitRemainingTicks = commit.RemainingTicks;
                return;
            }

            if (aResult.IsDecided)
            {
                mCommits.Remove(aUnit);
                aEntry.CommitRemainingTicks = 0;
            }
        }

        // ユニットに張られている有効な待機コミットを取得する
        // 残りティック数が尽きたものは解除して null を返す
        // aUnit : 対象ユニット
        // return : 有効なコミット。無ければ null
        protected PPUnitAICommit ResolveCommit(PPBattleUnit aUnit)
        {
            if (!mCommits.TryGetValue(aUnit, out var commit)) return null;
            if (commit.RemainingTicks > 0) return commit;

            mCommits.Remove(aUnit);
            return null;
        }

        // ティックの進行を検出して、全ユニットのコミットを 1 ティック分消化する
        // 思考はティックより細かい周期で回るため、経過ティック数で数えないと
        // 維持ティック数が思考回数の分だけ速く溶けてしまう
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

            int elapsed = aContext.TurnCount - mLastTurnCount;
            foreach (var commit in mCommits.Values)
            {
                commit.RemainingTicks = Mathf.Max(0, commit.RemainingTicks - elapsed);
            }
            mLastTurnCount = aContext.TurnCount;
            return true;
        }

        // バトル開始時の初期化。コミットとティック基準を戻す
        public void ResetForBattle()
        {
            mCommits.Clear();
            mLastTurnCount = -1;
        }

        // 積んだ行動が使う予定のゲージを台帳へ仮押さえする
        // コマンドの種類で消費先が変わるため、ここで振り分ける
        // aUnit : 行動するユニット
        // aCommand : 積んだコマンド
        protected virtual void ReserveCost(PPBattleUnit aUnit, BattleCommandBase aCommand)
        {
            switch (aCommand)
            {
                case PPSkillCommand skillCommand:
                    float cost = (skillCommand.Skill.SourceDefinition as PPSkillDefinition)?.SkillGaugeCost ?? 0f;
                    mLedger.ReserveSkill(aUnit, cost);
                    break;

                case IPPBattleCommand attackCommand:
                    mLedger.ReserveCoin(aUnit, attackCommand.AttackCost);
                    break;

                default:
                    // 消費を伴わない行動でも、行動回数だけは数えないと無限に積めてしまう
                    mLedger.ReserveCoin(aUnit, 0f);
                    break;
            }
        }

        // ユニット定義から判断ツリーを引く
        // aUnit : 対象ユニット
        // return : そのユニットの判断ツリー。未設定なら null
        protected static PPUnitAIProfileDefinition ResolveProfile(PPBattleUnit aUnit)
            => (aUnit.SourceDefinition as PPUnitDefinition)?.AIProfile;

        // ユニット 1 体分の判断記録を、判断前の状態で初期化する
        // aUnit : 対象ユニット
        // return : 初期化された記録
        protected virtual PPUnitAIThinkEntry CreateEntry(PPBattleUnit aUnit)
            => new PPUnitAIThinkEntry
            {
                UnitName = aUnit.DisplayName,
                Decision = PPUnitAIDecision.Wait,
                RejectReason = PPUnitAIRejectReason.None,
                ActionName = "-",
                TargetName = "-",
                SkillGauge = aUnit.ExtraParameters.SkillGauge.Current,
                SkillGaugeMax = aUnit.ExtraParameters.SkillGauge.Max.CurrentValue,
                CoinGauge = aUnit.ExtraParameters.CoinGauge.Current,
                CoinGaugeMax = aUnit.ExtraParameters.CoinGauge.Max.CurrentValue,
            };

        // AI プロファイルを持つユニットが 1 体も居ない場合に一度だけ警告する
        // aHasProfile : プロファイルを持つユニットが 1 体でも居たか
        protected void WarnIfNoProfile(bool aHasProfile)
        {
            if (aHasProfile || mIsWarnedEmpty) return;

            mIsWarnedEmpty = true;
            CustomConsoleLog.Warning("AI", "AIプロファイルを持つユニットが 1 体も居ないため、このパーティは常に待機します。");
        }

        // 思考記録をログとデバッグハブへ流す
        // aParty : 思考主体のパーティ
        // aContext : バトルコンテキスト
        // aPlan : 組み上がった行動計画
        protected virtual void ReportThink(PPBattleParty aParty, BattleContext aContext, PPPartyPlan aPlan)
        {
            foreach (var entry in mThinkEntries)
            {
                string commit = entry.CommitRemainingTicks > 0 ? $" 維持あと{entry.CommitRemainingTicks}T" : "";
                CustomConsoleLog.Verbose("AI",
                    $"{entry.UnitName}: {entry.Decision}({entry.RejectReason}) {entry.ActionName} -> {entry.TargetName}{commit} " +
                    $"[スキル{entry.SkillGauge:0.#}/{entry.SkillGaugeMax:0.#} コイン{entry.CoinGauge:0.#}/{entry.CoinGaugeMax:0.#}]");
            }

            if (!PPUnitAIDebugHub.HasListener) return;

            // BattleParty は陣営を公開していないため、コンテキストとの参照比較で判定する
            var report = new PPUnitAIThinkReport
            {
                Side = ReferenceEquals(aParty, aContext.EnemyParty) ? BattleSide.Enemy : BattleSide.Ally,
                TurnCount = aContext.TurnCount,
                Timestamp = Time.time,
                AdoptedCount = aPlan?.Assignments.Count ?? 0,
            };
            report.Units.AddRange(mThinkEntries);
            PPUnitAIDebugHub.Report(report);
        }
    }
}
