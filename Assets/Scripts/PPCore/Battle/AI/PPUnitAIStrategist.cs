/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitAIStrategist.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニット単位で判断ツリーを評価するAI
 * =====================================*/

using System.Collections.Generic;
using CustomConsole;

namespace PPCore
{
    // 各ユニットが自分の判断ツリー（PPUnitBehaviourTree）を評価して次の行動を決める AI
    // ユニットの行動が終わるたびに 1 体ずつ思考し、結果を「選択中のスキル」と「狙う相手」として予約する
    public class PPUnitAIStrategist : IPPPartyCommandStrategist
    {
        /* 内部型 */

        // ユニット 1 体分の待機コミット
        // 「どの枝で待つと決めたか（道順）」と「あと何回の行動のあいだ維持するか」を持つ
        protected sealed class PPUnitAICommit
        {
            // 待ちを宣言した行動へ至る道順。優先度リストの子の添字を根から並べたもの
            public List<int> Path = new();
            // 残りの維持する行動回数
            public int RemainingActions;
        }

        /* 定数 */

        // 連携ノードが採用済みの枝へ付けるキーの目印。PPUnitAISequenceNode と揃える
        private const string SequenceKeyMarker = "#seq:";

        /* 状態 */

        // ユニットごとの待機コミット
        private readonly Dictionary<PPBattleUnit, PPUnitAICommit> mCommits = new();
        // ユニットごとの記憶。間隔を空ける判断や一度きりの判定に使う
        // 書き込みはこのクラスへ集約し、ノード側は読むだけにする
        private readonly Dictionary<PPBattleUnit, PPUnitAIMemory> mMemories = new();
        // ユニットごとのバトル中の見聞き。反撃・狙いの継続などの判断材料になる
        private readonly Dictionary<PPBattleUnit, PPUnitAIBlackboard> mBlackboards = new();
        // 連携ノードで採用済みになった枝。行動のたびに 1 つずつ消化するため、思考をまたいで持ち越す
        private readonly Dictionary<PPBattleUnit, HashSet<string>> mAdopted = new();
        // バトル進行の管理役。イベントを購読して見聞きを記録するために持つ
        private BattleManager mBattleManager;
        // この思考ルーチンが担当する陣営。他陣営の出来事を自分の記録に混ぜないための絞り込みに使う
        private BattleSide mSide;
        // 使えるツリーが無い旨の警告を出したユニット。毎思考で出し続けないための抑止
        private readonly HashSet<PPBattleUnit> mWarnedNoTreeUnits = new();

        /* 生成 */

        // ユニット 1 体分の判断記録を、判断前の状態で初期化する
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

        /* 初期化・接続 */

        // バトル進行の管理役と担当する陣営を受け取る
        // 二重購読を避けるため、既に繋がっている場合は先に外してから繋ぎ直す
        public void BindBattle(BattleManager aManager, BattleSide aSide)
        {
            Unbind();
            ResetForBattle();

            mBattleManager = aManager;
            mSide = aSide;
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

        /* 操作 */

        // 担当する陣営の全ユニットについて、次の行動を決めて予約する
        public void ThinkAll(BattleContext aContext)
        {
            if (aContext == null) return;

            // 思考の途中で編成が変わらないよう、先に対象を写してから回す
            var members = new List<BattleUnit>(aContext.GetParty(mSide).ActiveMembers);
            foreach (var member in members)
            {
                if (member is PPBattleUnit unit) Think(unit, aContext);
            }
        }

        // ユニット 1 体の次の行動を決めて予約する。そのユニットの行動が終わるたびに呼ぶ
        public void Think(PPBattleUnit aUnit, BattleContext aContext)
        {
            if (aUnit == null || aContext == null || !aUnit.IsAlive) return;
            if (aContext.GetParty(aUnit.Side) is not PPBattleParty party) return;

            var snap = PPPartyAIContext.Capture(party, aContext);
            if (snap.AliveEnemies.Count == 0) return;

            // 条件クラスが受け取るのはこのスナップショットだけなので、見聞きを引く口をここへ通す
            snap.AttachBlackboardResolver(ResolveBlackboard);

            // 時間の経過は自分の行動回数で数えるため、思考のたびに進める
            ResolveBlackboard(aUnit).BeginThink(aContext.Clock.Now);
            ConsumeCommit(aUnit);

            var tree = ResolveTree(aUnit, out var treeSource);
            if (tree == null) WarnNoTree(aUnit);

            var entry = CreateEntry(aUnit);
            entry.ActionCount = ResolveBlackboard(aUnit).ActionCount;
            entry.Tree = tree;
            entry.TreeSource = treeSource;

            var result = DecideUnitAction(aUnit, tree, snap, aContext, entry);
            ApplyReservation(aUnit, result);
            ReportThink(aUnit, aContext, entry);
        }

        // バトル開始時の初期化。コミット・記憶・見聞きを捨てる
        public void ResetForBattle()
        {
            mCommits.Clear();
            mMemories.Clear();
            mBlackboards.Clear();
            mAdopted.Clear();
            mWarnedNoTreeUnits.Clear();
        }

        // 決めた行動を、ユニットの「選択中のスキル」と「狙う相手」として予約する
        // スキルなら選択し、それ以外（通常攻撃・待機・決まらなかった）なら選択を外して通常攻撃を続けさせる
        protected virtual void ApplyReservation(PPBattleUnit aUnit, PPUnitAINodeResult aResult)
        {
            var target = aResult.IsDecided ? aResult.Target : null;
            bool isEnemyTarget = target != null && target.Side != aUnit.Side;

            if (aResult.IsDecided && aResult.Command is SkillCommand skillCommand)
            {
                // 味方が対象のスキルは、選んだ味方をスキルの対象として覚える
                aUnit.SelectSkill(skillCommand.Skill, isEnemyTarget ? null : target);
            }
            else
            {
                aUnit.ClearSkillSelection();
            }

            // 敵を狙う行動なら狙う相手として指定し、そうでなければ指定なしの規則（正面の敵）へ戻す
            if (isEnemyTarget) aUnit.DesignateTarget(target);
            else aUnit.ClearDesignatedTarget();
        }

        // 評価結果に応じて待機コミットを張り直す
        // 維持する行動回数を持つ行動なら道順ごと記録し、それ以外ならコミットを解除する
        protected virtual void UpdateCommit(PPBattleUnit aUnit, PPUnitAIEvalContext aEvalContext,
            PPUnitAINodeResult aResult, PPUnitAIThinkEntry aEntry)
        {
            if (aResult.IsDecided && aResult.CommitActions > 0)
            {
                var commit = new PPUnitAICommit { RemainingActions = aResult.CommitActions };
                commit.Path.AddRange(aEvalContext.Path);
                mCommits[aUnit] = commit;
                aEntry.CommitRemainingActions = commit.RemainingActions;
                return;
            }

            if (aResult.IsDecided)
            {
                mCommits.Remove(aUnit);
                aEntry.CommitRemainingActions = 0;
            }
        }

        // 行動が確定したら、連携ノードで採用された枝を覚える。次の行動では、連携ノードがその枝を飛ばす
        // 既に覚えている枝がもう一度採用された場合は、連携が一巡したとみなしてそのノードの記録を作り直す
        protected virtual void CommitAdopted(PPBattleUnit aUnit, PPUnitAINodeResult aResult,
            PPUnitAIEvalContext aEvalContext)
        {
            if (!aResult.IsDecided) return;

            var adopted = ResolveAdopted(aUnit);
            foreach (var key in aEvalContext.VisitedNodeIds)
            {
                int markerIndex = key.IndexOf(SequenceKeyMarker, System.StringComparison.Ordinal);
                if (markerIndex < 0) continue;

                if (adopted.Contains(key))
                {
                    string prefix = key.Substring(0, markerIndex + SequenceKeyMarker.Length);
                    adopted.RemoveWhere(k => k.StartsWith(prefix, System.StringComparison.Ordinal));
                }
                adopted.Add(key);
            }
        }

        // 行動が確定したら、その内容を見聞きへ記録する
        // ノードは状態を変えない約束のため、狙いの固定もここで張る
        protected virtual void CommitBlackboard(PPBattleUnit aUnit, PPUnitAINodeResult aResult)
        {
            if (!aResult.IsDecided) return;

            var blackboard = ResolveBlackboard(aUnit);
            var skill = (aResult.Command as SkillCommand)?.Skill?.SourceDefinition as PPSkillDefinition;
            blackboard.RecordAction(skill, aResult.Target);

            // 固定する行動回数を持つ行動だけが狙いを張り直す
            // 0 の行動で毎回上書きしてしまうと、固定した狙いが 1 手で流れてしまう
            if (aResult.FocusActions > 0 && aResult.Target != null)
            {
                blackboard.SetFocus(aResult.Target, aResult.FocusActions);
            }
        }

        // 行動が確定したら、その経路上のノードへ記憶（間隔・一度きり・ラッチ）を書き込む
        // ノードはバトルの状態を変えないため、書き込みはここへ集約する
        protected virtual void CommitMemory(PPBattleUnit aUnit, PPUnitAINodeResult aResult,
            PPUnitAIEvalContext aEvalContext)
        {
            if (!aResult.IsDecided) return;

            var memory = ResolveMemory(aUnit);
            int actionCount = ResolveBlackboard(aUnit).ActionCount;
            foreach (var nodeId in aEvalContext.VisitedNodeIds)
            {
                memory.MarkFired(nodeId, actionCount);
            }
        }

        // 評価で通過したノードの経路を思考記録へ写す
        // 行動が確定した場合は、経路の末尾がその行動を組み立てたノードになる
        protected virtual void RecordVisitedPath(PPUnitAIEvalContext aEvalContext, PPUnitAINodeResult aResult,
            PPUnitAIThinkEntry aEntry)
        {
            aEntry.VisitedNodeIds.Clear();
            aEntry.VisitedNodeIds.AddRange(aEvalContext.VisitedNodeIds);

            aEntry.DecidedNodeId = aResult.IsDecided && aEntry.VisitedNodeIds.Count > 0
                ? aEntry.VisitedNodeIds[^1]
                : null;
        }

        // ユニットの待機コミットを 1 回の行動ぶん消化する。思考のたびに呼ぶ
        protected void ConsumeCommit(PPBattleUnit aUnit)
        {
            if (mCommits.TryGetValue(aUnit, out var commit) && commit.RemainingActions > 0)
            {
                commit.RemainingActions--;
            }
        }

        // 使えるツリーが無いユニットについて、ユニットごとに一度だけ警告する
        // aUnit : 対象ユニット
        protected void WarnNoTree(PPBattleUnit aUnit)
        {
            if (!mWarnedNoTreeUnits.Add(aUnit)) return;

            CustomConsoleLog.Warning("AI",
                $"{aUnit.DisplayName}: 個別・ロール既定・全体既定のどのツリーも無いため通常攻撃を続けます。" +
                "ユニットのビヘイビアツリーか、PPRuntimeSettings の「ユニットAIの既定」を設定してください。");
        }

        // 思考記録をログとデバッグハブへ流す
        protected virtual void ReportThink(PPBattleUnit aUnit, BattleContext aContext, PPUnitAIThinkEntry aEntry)
        {
            string commit = aEntry.CommitRemainingActions > 0 ? $" 維持あと{aEntry.CommitRemainingActions}回" : "";
            string treeName = aEntry.Tree != null ? aEntry.Tree.name : "-";
            CustomConsoleLog.Verbose("AI",
                $"{aEntry.UnitName}: {aEntry.Decision}({aEntry.RejectReason}) {aEntry.ActionName} -> {aEntry.TargetName}{commit} " +
                $"[スキル{aEntry.SkillGauge:0.#}/{aEntry.SkillGaugeMax:0.#} コイン{aEntry.CoinGauge:0.#}/{aEntry.CoinGaugeMax:0.#}] " +
                $"ツリー={treeName}({PPUnitAITreeSourceUtility.ToDisplayString(aEntry.TreeSource)})");

            if (!PPUnitAIDebugHub.HasListener) return;

            var report = new PPUnitAIThinkReport
            {
                Side = aUnit.Side,
                Timestamp = aContext.Clock.Now,
            };
            report.Units.Add(aEntry);
            PPUnitAIDebugHub.Report(report);
        }

        /* 評価・判定 */

        // ユニット 1 体分の判断ツリーを評価して行動を決める。決まらなければ不成立の結果を返す
        protected virtual PPUnitAINodeResult DecideUnitAction(PPBattleUnit aUnit, PPUnitBehaviourTree aTree,
            PPPartyAIContext aSnap, BattleContext aContext, PPUnitAIThinkEntry aEntry)
        {
            if (aTree == null)
            {
                aEntry.RejectReason = PPUnitAIRejectReason.NoTree;
                return PPUnitAINodeResult.Failed;
            }

            var result = EvaluateTree(aUnit, aTree, aSnap, aContext, aEntry);
            if (!result.IsDecided)
            {
                // どの枝も成立しなかった。ツリーの取りこぼしなので、末尾に無条件の行動を置けば埋められる
                aEntry.RejectReason = PPUnitAIRejectReason.NoMatchedNode;
                return result;
            }

            aEntry.ActionName = result.ActionName ?? "-";
            aEntry.TargetName = result.Target?.DisplayName ?? "-";

            // コマンドを持たない結果は「待機」で確定したことを表す。スキルを選ばず通常攻撃を続ける
            if (result.Command == null)
            {
                aEntry.Decision = PPUnitAIDecision.Wait;
                aEntry.RejectReason = PPUnitAIRejectReason.DecidedToWait;
                return result;
            }

            aEntry.Decision = result.Command is PPSkillCommand ? PPUnitAIDecision.Skill : PPUnitAIDecision.NormalAttack;
            aEntry.RejectReason = PPUnitAIRejectReason.None;
            return result;
        }

        // 判断ツリーを評価し、待機コミットの適用と更新まで行う
        // コミット中は前回の道順で評価範囲を絞り、その枝が崩れていた場合は制約を外して評価し直す
        protected virtual PPUnitAINodeResult EvaluateTree(PPBattleUnit aUnit, PPUnitBehaviourTree aTree,
            PPPartyAIContext aSnap, BattleContext aContext, PPUnitAIThinkEntry aEntry)
        {
            aSnap.ResetConditionedUnits();

            var evalContext = new PPUnitAIEvalContext(aUnit, aSnap, aContext, aTree)
            {
                Memory = ResolveMemory(aUnit),
                AdoptedKeys = ResolveAdopted(aUnit),
                ActionCount = ResolveBlackboard(aUnit).ActionCount,
            };
            var commit = ResolveCommit(aUnit);
            if (commit != null)
            {
                evalContext.CommitPath = commit.Path;
                aEntry.CommitRemainingActions = commit.RemainingActions;
            }

            var result = aTree.Evaluate(evalContext);

            // コミットした枝が崩れていたら、制約を外して普通に選び直す
            if (!result.IsDecided && commit != null)
            {
                evalContext.CommitPath = null;
                evalContext.ResetPath();
                // 1 回目の評価で積まれた候補は、通らなかった枝のものなので捨ててから引き直す
                aSnap.ResetConditionedUnits();
                result = aTree.Evaluate(evalContext);
                aEntry.CommitRemainingActions = 0;
            }

            UpdateCommit(aUnit, evalContext, result, aEntry);
            RecordVisitedPath(evalContext, result, aEntry);
            CommitMemory(aUnit, result, evalContext);
            CommitBlackboard(aUnit, result);
            CommitAdopted(aUnit, result, evalContext);
            return result;
        }

        /* 問い合わせ */

        // ユニットに対応する見聞きを取得する。初回は生成して覚える
        // 条件クラスから読むため公開している
        public PPUnitAIBlackboard ResolveBlackboard(PPBattleUnit aUnit)
        {
            if (!mBlackboards.TryGetValue(aUnit, out var blackboard))
            {
                blackboard = new PPUnitAIBlackboard();
                mBlackboards[aUnit] = blackboard;
            }
            return blackboard;
        }

        // ユニットに対応する採用済みの枝を取得する。初回は生成して覚える
        protected HashSet<string> ResolveAdopted(PPBattleUnit aUnit)
        {
            if (!mAdopted.TryGetValue(aUnit, out var adopted))
            {
                adopted = new HashSet<string>();
                mAdopted[aUnit] = adopted;
            }
            return adopted;
        }

        // ユニットに対応する記憶を取得する。初回は生成して覚える
        protected PPUnitAIMemory ResolveMemory(PPBattleUnit aUnit)
        {
            if (!mMemories.TryGetValue(aUnit, out var memory))
            {
                memory = new PPUnitAIMemory();
                mMemories[aUnit] = memory;
            }
            return memory;
        }

        // ユニットに張られている有効な待機コミットを取得する
        // 残りの行動回数が尽きたものは解除して null を返す
        protected PPUnitAICommit ResolveCommit(PPBattleUnit aUnit)
        {
            if (!mCommits.TryGetValue(aUnit, out var commit)) return null;
            if (commit.RemainingActions > 0) return commit;

            mCommits.Remove(aUnit);
            return null;
        }

        // ユニットが使う判断ツリーを 個別 → ロール既定 → 全体既定 の順で引く
        // 個別ツリーで行動が決まらなくても既定ツリーへは差し戻さない
        protected virtual PPUnitBehaviourTree ResolveTree(PPBattleUnit aUnit, out PPUnitAITreeSource aSource)
            => PPUnitAIDefaultsDefinition.ResolveTree(aUnit.SourceDefinition as PPUnitDefinition,
                PPRuntimeSettings.Current.UnitAIDefaults, out aSource);

        /* イベントハンドラ */

        // ダメージ結果を自陣営のユニットの見聞きへ記録する
        // 発生元が取れないダメージ（反射など）は加害者の記録に使わない
        protected virtual void HandleDamageResolved(DamageInfo aInfo)
        {
            if (aInfo.IsMiss || aInfo.IsNullified) return;
            if (aInfo.Target is not PPBattleUnit target || target.Side != mSide) return;

            ResolveBlackboard(target).RecordDamaged(aInfo.Source as PPBattleUnit, aInfo.Amount);
        }

        // 味方が倒された事実を、生き残っている自陣営のユニットへ記録する
        // 撃破通知には撃破者の情報が無いため「誰に倒されたか」は残せない
        protected virtual void HandleUnitDefeated(BattleUnit aUnit)
        {
            if (aUnit is not PPBattleUnit defeated || defeated.Side != mSide) return;

            foreach (var member in mBattleManager.Context.GetParty(mSide).ActiveMembers)
            {
                if (member is PPBattleUnit pp && pp != defeated) ResolveBlackboard(pp).RecordAllyDefeated();
            }
        }
    }
}
