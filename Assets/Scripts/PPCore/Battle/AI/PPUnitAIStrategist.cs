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
        // ユニットごとの記憶。クールダウンや一度きりの判定に使う
        // 書き込みはこのクラスへ集約し、ノード側は読むだけにする
        private readonly Dictionary<PPBattleUnit, PPUnitAIMemory> mMemories = new();
        // ユニットごとのバトル中の見聞き。反撃・狙いの継続などの判断材料になる
        private readonly Dictionary<PPBattleUnit, PPUnitAIBlackboard> mBlackboards = new();
        // この思考で採用済みになった枝。連携ノードが「もう採った子」を飛ばすために読む
        // 寿命は仮押さえ台帳と同じ 1 思考分で、思考のたびに捨てる
        private readonly Dictionary<PPBattleUnit, HashSet<string>> mAdopted = new();

        // バトル進行の管理役。イベントを購読して見聞きを記録するために持つ
        private BattleManager mBattleManager;
        // この思考ルーチンが担当する陣営。他陣営の出来事を自分の記録に混ぜないための絞り込みに使う
        private BattleSide mSide;
        // 実行待ちの行動の供給元。差し込まれていなければ「敵の次の行動」を見る条件は常に不成立になる
        private IPPPendingActionSource mPendingSource;

        // コミット消化の基準にする、前回思考時のターン数
        private int mLastTurnCount = -1;
        // AI プロファイルを持つユニットが 1 体も居ない旨の警告を出したか。毎思考で出し続けないための抑止
        private bool mIsWarnedEmpty;

        // バトル進行の管理役と、実行待ちの行動の供給元を受け取る
        // 二重購読を避けるため、既に繋がっている場合は先に外してから繋ぎ直す
        // aManager : バトル進行の管理役
        // aSide : この思考ルーチンが担当する陣営
        // aPendingSource : 実行待ちの行動の供給元
        public void BindBattle(BattleManager aManager, BattleSide aSide, IPPPendingActionSource aPendingSource)
        {
            Unbind();

            mBattleManager = aManager;
            mSide = aSide;
            mPendingSource = aPendingSource;
            if (mBattleManager == null) return;

            mBattleManager.OnDamageResolved += HandleDamageResolved;
            mBattleManager.OnUnitDefeated += HandleUnitDefeated;
        }

        // 購読を外す。バトルが終わったあともイベントで記録が更新され続けるのを防ぐ
        public void Unbind()
        {
            if (mBattleManager == null) return;

            mBattleManager.OnDamageResolved -= HandleDamageResolved;
            mBattleManager.OnUnitDefeated -= HandleUnitDefeated;
            mBattleManager = null;
        }

        // ユニットに対応する見聞きを取得する。初回は生成して覚える
        // 条件クラスから読むため公開している
        // aUnit : 対象ユニット
        // return : そのユニットの見聞き
        public PPUnitAIBlackboard ResolveBlackboard(PPBattleUnit aUnit)
        {
            if (!mBlackboards.TryGetValue(aUnit, out var blackboard))
            {
                blackboard = new PPUnitAIBlackboard();
                mBlackboards[aUnit] = blackboard;
            }
            return blackboard;
        }

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
            // 条件クラスが受け取るのはこのスナップショットだけなので、見聞きと実行待ちの口をここへ通す
            snap.AttachBlackboardResolver(ResolveBlackboard);
            snap.AttachPendingSource(mPendingSource);

            // コミットの消化はティックが進んだときだけ行う
            // 思考のたびに減らすと、維持ティック数が思考回数の分だけ短くなってしまう
            SyncTick(aContext);

            mThinkEntries.Clear();
            mLedger.Clear();
            mAdopted.Clear();
            var picks = new List<PPPartyActionAssignment>();
            int order = 0;
            bool hasProfile = false;

            // ユニットごとに独立して判断する。順序はパーティの編成順で固定される
            foreach (var unit in snap.AliveMembers)
            {
                var profile = ResolveProfile(unit);
                if (profile != null) hasProfile = true;

                // 行動回数の上限まで積む。積むたびに台帳へ仮押さえするため、
                // 2 手目以降は 1 手目で使う予定のゲージを差し引いた状態で判断される
                // 思考記録は行動ごとに分ける。1 件に上書きすると 2 手目の判断が追えなくなるため
                int actionIndex = 0;
                while (mLedger.HasActionLeft(unit))
                {
                    var entry = CreateEntry(unit);
                    entry.ActionIndex = actionIndex;
                    entry.Profile = profile;

                    var command = DecideUnitAction(unit, profile, snap, aContext, entry);
                    mThinkEntries.Add(entry);
                    if (command == null) break;

                    ReserveCost(unit, command);
                    picks.Add(new PPPartyActionAssignment(unit, command, order));
                    order++;
                    actionIndex++;
                }
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

            var evalContext = new PPUnitAIEvalContext(aUnit, aSnap, aContext, aProfile)
            {
                Memory = ResolveMemory(aUnit),
                AdoptedKeys = ResolveAdopted(aUnit),
            };
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
            RecordVisitedPath(evalContext, result, aEntry);
            CommitMemory(aUnit, result, evalContext, aContext.TurnCount);
            CommitBlackboard(aUnit, result, aContext.TurnCount);
            CommitAdopted(aUnit, result, evalContext);
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

        // ユニットに対応する採用済みの枝を取得する。初回は生成して覚える
        // aUnit : 対象ユニット
        // return : そのユニットが この思考で採用済みにした枝のキー
        protected HashSet<string> ResolveAdopted(PPBattleUnit aUnit)
        {
            if (!mAdopted.TryGetValue(aUnit, out var adopted))
            {
                adopted = new HashSet<string>();
                mAdopted[aUnit] = adopted;
            }
            return adopted;
        }

        // 行動が確定したら、その経路上で採用された枝を覚える
        // 連携ノードが次の手で同じ枝を採らないようにするためのもの
        // 通過記録には連携ノードが積んだ採用キーも含まれており、確定した経路の分だけがここへ残る
        // aUnit : 判断対象のユニット
        // aResult : ツリーが確定させた行動
        // aEvalContext : 評価に使ったコンテキスト。確定時の通過経路を持つ
        protected virtual void CommitAdopted(PPBattleUnit aUnit, PPUnitAINodeResult aResult,
            PPUnitAIEvalContext aEvalContext)
        {
            if (!aResult.IsDecided) return;

            var adopted = ResolveAdopted(aUnit);
            foreach (var nodeId in aEvalContext.VisitedNodeIds)
            {
                adopted.Add(nodeId);
            }
        }

        // 行動が確定したら、その内容を見聞きへ記録する
        // ノードは状態を変えない約束のため、狙いの固定もここで張る
        // aUnit : 判断対象のユニット
        // aResult : ツリーが確定させた行動
        // aTurnCount : 確定した時点のターン数
        protected virtual void CommitBlackboard(PPBattleUnit aUnit, PPUnitAINodeResult aResult, int aTurnCount)
        {
            if (!aResult.IsDecided) return;

            var blackboard = ResolveBlackboard(aUnit);
            var skill = (aResult.Command as SkillCommand)?.Skill?.SourceDefinition as PPSkillDefinition;
            blackboard.RecordAction(skill, aResult.Target);

            // 固定するティック数を持つ行動だけが狙いを張り直す
            // 0 の行動で毎回上書きしてしまうと、固定した狙いが 1 手で流れてしまう
            if (aResult.FocusTicks > 0 && aResult.Target != null)
            {
                blackboard.SetFocus(aResult.Target, aTurnCount, aResult.FocusTicks);
            }
        }

        // ダメージ結果を自陣営のユニットの見聞きへ記録する
        // 発生元が取れないダメージ（反射など）は加害者の記録に使わない
        // aInfo : 解決済みのダメージ情報
        protected virtual void HandleDamageResolved(DamageInfo aInfo)
        {
            if (aInfo.IsMiss || aInfo.IsNullified) return;
            if (aInfo.Target is not PPBattleUnit target || target.Side != mSide) return;

            ResolveBlackboard(target).RecordDamaged(aInfo.Source as PPBattleUnit, aInfo.Amount, CurrentTurnCount);
        }

        // 味方が倒された事実を、生き残っている自陣営のユニットへ記録する
        // 撃破通知には撃破者の情報が無いため「誰に倒されたか」は残せない
        // aUnit : 倒されたユニット
        protected virtual void HandleUnitDefeated(BattleUnit aUnit)
        {
            if (aUnit is not PPBattleUnit defeated || defeated.Side != mSide) return;

            int turnCount = CurrentTurnCount;
            foreach (var member in mBattleManager.Context.GetParty(mSide).ActiveMembers)
            {
                if (member is PPBattleUnit pp && pp != defeated) ResolveBlackboard(pp).RecordAllyDefeated(turnCount);
            }
        }

        // 現在のターン数。バトルへ繋がっていなければ 0
        private int CurrentTurnCount => mBattleManager?.Context?.TurnCount ?? 0;

        // ユニットに対応する記憶を取得する。初回は生成して覚える
        // aUnit : 対象ユニット
        // return : そのユニットの記憶
        protected PPUnitAIMemory ResolveMemory(PPBattleUnit aUnit)
        {
            if (!mMemories.TryGetValue(aUnit, out var memory))
            {
                memory = new PPUnitAIMemory();
                mMemories[aUnit] = memory;
            }
            return memory;
        }

        // 行動が確定したら、その経路上のノードへ記憶を書き込む
        // ノードは「バトルの状態を変えない」約束を守るため、書き込みはここへ集約する
        // クールダウン・一度きり・ラッチはいずれも「確定経路上のノード」に対して立つため、書き込み口は 1 つで足りる
        // aUnit : 判断対象のユニット
        // aResult : ツリーが確定させた行動
        // aEvalContext : 評価に使ったコンテキスト。確定時の通過経路を持つ
        // aTurnCount : 確定した時点のターン数
        protected virtual void CommitMemory(PPBattleUnit aUnit, PPUnitAINodeResult aResult,
            PPUnitAIEvalContext aEvalContext, int aTurnCount)
        {
            if (!aResult.IsDecided) return;

            var memory = ResolveMemory(aUnit);
            foreach (var nodeId in aEvalContext.VisitedNodeIds)
            {
                memory.MarkFired(nodeId, aTurnCount);
            }
        }

        // 評価で通過したノードの経路を思考記録へ写す
        // 行動が確定した場合は、経路の末尾がその行動を組み立てたノードになる
        // aEvalContext : 評価に使ったコンテキスト
        // aResult : ツリーが確定させた行動
        // aEntry : 判断結果の記録先
        protected virtual void RecordVisitedPath(PPUnitAIEvalContext aEvalContext, PPUnitAINodeResult aResult,
            PPUnitAIThinkEntry aEntry)
        {
            aEntry.VisitedNodeIds.Clear();
            aEntry.VisitedNodeIds.AddRange(aEvalContext.VisitedNodeIds);

            aEntry.DecidedNodeId = aResult.IsDecided && aEntry.VisitedNodeIds.Count > 0
                ? aEntry.VisitedNodeIds[^1]
                : null;
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
            mMemories.Clear();
            mBlackboards.Clear();
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
