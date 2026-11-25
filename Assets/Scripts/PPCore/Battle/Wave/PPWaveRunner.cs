/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPWaveRunner.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief ウェーブの進行(ノードのつながりに沿った敵の出現)と、マスの深さによるレベルの割り振り
 * =====================================*/

using System;
using System.Collections.Generic;
using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // マスの深さで決まるレベルを、出てくる敵の種類へ 1 レベルずつ無作為に割り振る
    public static class PPWaveLevelAllocator
    {
        /* 操作 */

        // aPoints レベルを aUnits へ割り振る。1 種類に偏ることも、種類ごとに散ることもある
        public static Dictionary<PPUnitDefinition, int> Allocate(IReadOnlyList<PPUnitDefinition> aUnits, int aPoints, IRandomProvider aRandom)
        {
            var bonus = new Dictionary<PPUnitDefinition, int>();
            foreach (var unit in aUnits) bonus[unit] = 0;
            if (aUnits.Count == 0) return bonus;

            for (int i = 0; i < aPoints; i++)
            {
                var unit = aUnits[aRandom.NextInt(aUnits.Count)];
                bonus[unit]++;
            }
            return bonus;
        }
    }

    // バトル 1 回分のウェーブの進行。トリガーを起点に、線でつながったノードを順に動かして敵拠点の近くへ敵を出す
    // バトル専用の時計で進めるため、メニューを開いている間は止まる
    public class PPWaveRunner : IPPWaveGraphContext
    {
        /* 内部型 */

        // ノード 1 つ分の実行中の状態
        private class NodeState
        {
            public readonly PPWaveNode Node;
            // 出口ごとの行き先。添字は PPWaveNodePort
            public readonly List<NodeState>[] Outgoing;
            // 入口に線があるか(入口を受け付けないトリガーでは常に false)
            public bool HasIncoming;
            // トリガーでなければ null
            public readonly PPWaveTriggerState Trigger;
            public int ActivationCount;
            public int ActivatedFrame = -1;
            // ウェーブノードの回の数と、最新の回と、出している最中の回の数
            public int RunCount;
            public SpawnRun LatestRun;
            public int RunningCount;
            // 入口から待ち始めたばかりで、まだ判定していないか(トリガーだけ)
            public bool IsArmCheckPending;
            // 次のフレームへ回し続けたフレーム数。上限の警告に使う
            public int DeferredStreak;
            public int LastDeferredFrame = -1;
            public bool IsDeferWarned;

            public NodeState(PPWaveNode aNode)
            {
                Node = aNode;
                Outgoing = new List<NodeState>[PortCount];
                for (int i = 0; i < PortCount; i++) Outgoing[i] = new List<NodeState>();
                if (aNode is PPWaveTriggerNode) Trigger = new PPWaveTriggerState();
            }
        }

        // ウェーブノードの 1 回分。同じノードがまた動かされたら、別の回として作る
        private class SpawnRun
        {
            public readonly NodeState Owner;
            public readonly int Number;
            public readonly SpawnEntry[] Spawns;
            public readonly List<BattleUnit> SpawnedUnits = new();
            public bool IsFinishedNotified;
            public bool IsWipedNotified;

            public SpawnRun(NodeState aOwner, int aNumber, PPWaveSpawnNode aNode, float aStartTime)
            {
                Owner = aOwner;
                Number = aNumber;
                Spawns = new SpawnEntry[aNode.Spawns.Count];
                for (int i = 0; i < Spawns.Length; i++) Spawns[i] = new SpawnEntry(aNode.Spawns[i], aStartTime);
            }

            public bool IsFinished
            {
                get
                {
                    foreach (var spawn in Spawns)
                    {
                        if (!spawn.IsFinished) return false;
                    }
                    return true;
                }
            }

            // 出し終えていて、出した敵が 1 体以上いて、すべて倒されているか
            public bool IsWiped
            {
                get
                {
                    if (!IsFinished || SpawnedUnits.Count == 0) return false;
                    foreach (var unit in SpawnedUnits)
                    {
                        if (unit.IsAlive) return false;
                    }
                    return true;
                }
            }
        }

        // 1 種類の敵を出す進み具合
        private class SpawnEntry
        {
            public readonly PPWaveSpawn Spawn;
            public float NextTime;
            public int SpawnedCount;

            public SpawnEntry(PPWaveSpawn aSpawn, float aStartTime)
            {
                Spawn = aSpawn;
                NextTime = aStartTime + (aSpawn?.FirstDelay ?? 0f);
            }

            public bool IsFinished => Spawn?.Unit == null || (!Spawn.IsInfinite && SpawnedCount >= Spawn.Count);
        }

        // 動かす予定 1 件。どのノードのどの出口から来たか(バトル開始の起点なら Source が null)
        private readonly struct Activation
        {
            public readonly NodeState Target;
            public readonly NodeState Source;
            public readonly PPWaveNodePort Port;

            public Activation(NodeState aTarget, NodeState aSource, PPWaveNodePort aPort)
            {
                Target = aTarget;
                Source = aSource;
                Port = aPort;
            }

            public bool IsSame(in Activation aOther) => Target == aOther.Target && Source == aOther.Source && Port == aOther.Port;
        }

        /* 定数 */

        private const string LogTag = "Battle";
        private static readonly int PortCount = Enum.GetValues(typeof(PPWaveNodePort)).Length;
        // 1 フレームに動かすノードの総数の上限。超えた分は次のフレームへ回す
        private const int MaxActivationsPerFrame = 256;
        // 同じノードを続けてこのフレーム数だけ次へ回したら、待ちのない輪として警告する
        private const int DeferWarnFrames = 10;

        /* 状態 */

        private readonly PPWaveDefinition mWave;
        private readonly BattleManager mManager;
        private readonly float mSpawnPosition;
        private readonly Dictionary<PPUnitDefinition, int> mLevelBonus;
        private readonly Dictionary<string, NodeState> mStatesById = new();
        private readonly Dictionary<PPWaveNode, NodeState> mStatesByNode = new();
        // トリガーのノードの状態。定義の並び順
        private readonly List<NodeState> mTriggers = new();
        // 出している最中か、まだ全滅し得る回
        private readonly List<SpawnRun> mRuns = new();
        private readonly Queue<Activation> mQueue = new();
        // 1 フレームの上限を超えて、次のフレームへ回した分。同じノード・同じきっかけの分は 1 件にまとめる
        private readonly List<Activation> mDeferred = new();
        // 入口から待ち始めて、まだ判定していないトリガーがあるか
        private bool mHasArmCheckPending;
        private int mFrame;
        private int mActivationsThisFrame;
        private int mAllyBaseDamageCount;
        private int mEnemyBaseDamageCount;

        /* プロパティ */

        public PPWaveDefinition Definition => mWave;
        // マスの深さで割り振られた、ユニットの種類ごとの追加のレベル
        public IReadOnlyDictionary<PPUnitDefinition, int> LevelBonus => mLevelBonus;
        public float Now => mManager.Context?.Clock.Now ?? 0f;

        /* イベント */

        // ノードが入口から動かされて動いたとき(トリガーが待ち始めた・ウェーブが回を始めた・条件を判定した)(動いたノード)
        // 入口を受け付けないトリガーと、起動済みの「1 度だけ」のトリガーは、動かないため知らせない
        public event Action<PPWaveNode> OnNodeActivated;
        // ノードの出口から先へ進んだとき(ノード, 出口)
        public event Action<PPWaveNode, PPWaveNodePort> OnNodeOutput;
        // トリガーが起動したとき(起動したトリガー)
        public event Action<PPWaveTriggerNode> OnTriggerFired;

        /* 生成 */

        // aSpawnPosition は敵を出す道の位置(味方拠点からの距離)。aLevelPoints は深さで割り振るレベル
        public PPWaveRunner(PPWaveDefinition aWave, BattleManager aManager, float aSpawnPosition, int aLevelPoints)
        {
            mWave = aWave;
            mManager = aManager;
            mSpawnPosition = aSpawnPosition;
            mLevelBonus = PPWaveLevelAllocator.Allocate(aWave.CollectUnits(), aLevelPoints, aManager.Context.Rules.RandomProvider);
            BuildStates();

            // 入口に線の無いトリガーは、バトル開始の時刻から待つ
            foreach (var state in mTriggers)
            {
                if (!state.HasIncoming) Arm(state, false);
            }

            aManager.OnBaseDamaged += (fieldBase, _) =>
            {
                if (fieldBase.Side == BattleSide.Ally) mAllyBaseDamageCount++;
                else mEnemyBaseDamageCount++;
            };
        }

        // ノードの状態と、出口ごとの行き先の表を作る。毎フレーム線を探さないよう、ここで引いておく
        private void BuildStates()
        {
            foreach (var node in mWave.Nodes)
            {
                if (node == null) continue;
                // 識別子が空のノードは、未選択の線とつながってしまうため使わない
                if (string.IsNullOrEmpty(node.Id))
                {
                    CustomConsoleLog.Warning(LogTag, $"ウェーブ: ノード「{node.DisplayName}」の識別子が空のため、使いません。ノードエディタで消して足し直してください");
                    continue;
                }
                if (mStatesByNode.ContainsKey(node) || mStatesById.ContainsKey(node.Id))
                {
                    CustomConsoleLog.Warning(LogTag, $"ウェーブ: ノード「{node.DisplayName}」の識別子が他と重なっているため、使いません。バトル定義のインスペクタで識別子を振り直してください");
                    continue;
                }
                var state = new NodeState(node);
                mStatesById[node.Id] = state;
                mStatesByNode[node] = state;
                if (state.Trigger != null) mTriggers.Add(state);
            }

            foreach (var link in mWave.Links)
            {
                if (link == null) continue;
                if (!mStatesById.TryGetValue(link.FromId ?? string.Empty, out var from) || !mStatesById.TryGetValue(link.ToId ?? string.Empty, out var to))
                {
                    CustomConsoleLog.Warning(LogTag, "ウェーブ: 出口か行き先のノードが見つからない線があるため、その線を使いません。バトル定義の「線」を直してください");
                    continue;
                }
                if (!from.Node.HasOutput(link.FromPort)) continue;
                from.Outgoing[(int)link.FromPort].Add(to);
                if (to.Node is not PPWaveTriggerNode { IgnoresInput: true }) to.HasIncoming = true;
            }
        }

        /* 操作 */

        // 待っているトリガーを判定し、線に沿ってノードを動かし、始まっている回から時刻の来た敵を出す。決着後は何もしない
        public void Update()
        {
            if (mManager.Context == null || mManager.StateMachine.Current == BattleState.BattleEnd) return;
            mFrame++;
            mActivationsThisFrame = 0;
            float now = Now;

            foreach (var activation in mDeferred) mQueue.Enqueue(activation);
            mDeferred.Clear();

            foreach (var state in mTriggers)
            {
                if (state.Trigger.IsWaiting && IsTriggerSatisfied(state)) Fire(state, now);
            }
            DrainAll(now);
            UpdateRuns(now);
            DrainAll(now);
        }

        // 動かす予定を処理し、その間に入口から待ち始めたトリガーを定義の並び順に判定する。起動して増えた予定も続けて処理する
        // 同じきっかけで待ち始めた同じグループのトリガーは、全部が待ち始めてから判定するため、線の並び順に関係なく 1 つだけが起動する
        private void DrainAll(float aNow)
        {
            Drain(aNow);
            while (mHasArmCheckPending)
            {
                mHasArmCheckPending = false;
                foreach (var state in mTriggers)
                {
                    if (!state.IsArmCheckPending) continue;
                    state.IsArmCheckPending = false;
                    if (state.Trigger.IsWaiting && IsTriggerSatisfied(state)) Fire(state, aNow);
                }
                Drain(aNow);
            }
        }

        // 動かす予定を順に処理する。同じノードは 1 フレームに 1 回までにし、超えた分は次のフレームへ回す
        private void Drain(float aNow)
        {
            while (mQueue.Count > 0)
            {
                var activation = mQueue.Dequeue();
                var target = activation.Target;
                if (target.ActivatedFrame == mFrame || mActivationsThisFrame >= MaxActivationsPerFrame)
                {
                    Defer(activation);
                    continue;
                }
                Activate(activation, aNow);
            }
        }

        // 動かす予定を次のフレームへ回す。同じノードを回し続けていたら、待ちのない輪として 1 度だけ警告する
        private void Defer(Activation aActivation)
        {
            var target = aActivation.Target;
            // 待ちのない輪で同じ予定が毎フレーム増え続けないよう、同じノード・同じきっかけの予定は 1 件にまとめる
            if (!ContainsDeferred(aActivation)) mDeferred.Add(aActivation);
            if (target.LastDeferredFrame == mFrame) return;
            target.DeferredStreak = target.LastDeferredFrame == mFrame - 1 ? target.DeferredStreak + 1 : 1;
            target.LastDeferredFrame = mFrame;
            if (target.IsDeferWarned || target.DeferredStreak < DeferWarnFrames) return;
            target.IsDeferWarned = true;
            CustomConsoleLog.Warning(LogTag, $"ウェーブ: ノード「{target.Node.DisplayName}」が 1 フレームに 1 回ずつ動き続けています。待ちのない輪になっていないか、バトル定義の線を確かめてください");
        }

        private bool ContainsDeferred(in Activation aActivation)
        {
            foreach (var deferred in mDeferred)
            {
                if (deferred.IsSame(aActivation)) return true;
            }
            return false;
        }

        // ノードを入口から動かす。トリガーは待ち始め(判定は DrainAll がまとめて行う)、ウェーブは回を始め、条件はその場で判定して先へ進む
        private void Activate(Activation aActivation, float aNow)
        {
            var state = aActivation.Target;
            state.ActivatedFrame = mFrame;
            mActivationsThisFrame++;
            if (state.Node is PPWaveTriggerNode ignored && (ignored.IgnoresInput || (ignored.IsOnce && state.Trigger.FireCount > 0))) return;
            state.ActivationCount++;
            OnNodeActivated?.Invoke(state.Node);

            switch (state.Node)
            {
                case PPWaveTriggerNode:
                    Arm(state, false);
                    state.IsArmCheckPending = true;
                    mHasArmCheckPending = true;
                    break;
                case PPWaveSpawnNode spawnNode:
                    StartRun(state, spawnNode, aActivation, aNow);
                    break;
                case PPWaveConditionNode condition:
                    bool result = condition.Evaluate(this);
                    CustomConsoleLog.Verbose(LogTag, $"ウェーブ: 条件「{condition.DisplayName}」は{(result ? "成り立つ" : "成り立たない")}(時刻 {aNow:F1})");
                    Emit(state, result ? PPWaveNodePort.True : PPWaveNodePort.False);
                    break;
            }
        }

        // トリガーを待ち始めさせ、待ち始めた時刻と拠点の被ダメージ回数を覚える。aIsRearm は起動したその場で待ち直すとき
        private void Arm(NodeState aState, bool aIsRearm)
        {
            var trigger = aState.Trigger;
            trigger.IsWaiting = true;
            trigger.ArmedTime = Now;
            trigger.AllyDamageCountAtArm = mAllyBaseDamageCount;
            trigger.EnemyDamageCountAtArm = mEnemyBaseDamageCount;
            trigger.IsRearmed = aIsRearm;
        }

        // トリガーを起動する。同じグループの他のトリガーは待つのをやめ、何度でも・入口に線なしならその場で待ち直す
        private void Fire(NodeState aState, float aNow)
        {
            var trigger = (PPWaveTriggerNode)aState.Node;
            aState.Trigger.FireCount++;
            aState.Trigger.IsWaiting = false;
            trigger.OnFired(aState.Trigger, this);
            CustomConsoleLog.Verbose(LogTag, $"ウェーブ: トリガー「{trigger.DisplayName}」が起動した(時刻 {aNow:F1} {aState.Trigger.FireCount} 回目)");

            if (!string.IsNullOrEmpty(trigger.RaceGroup))
            {
                foreach (var other in mTriggers)
                {
                    if (other != aState && ((PPWaveTriggerNode)other.Node).RaceGroup == trigger.RaceGroup) other.Trigger.IsWaiting = false;
                }
            }
            if (!trigger.IsOnce && !aState.HasIncoming) Arm(aState, true);

            OnTriggerFired?.Invoke(trigger);
            Emit(aState, PPWaveNodePort.Fired);
        }

        // ウェーブノードの新しい回を始め、「出し始めたとき」の先へ進む
        private void StartRun(NodeState aState, PPWaveSpawnNode aNode, Activation aActivation, float aNow)
        {
            aState.RunCount++;
            aState.RunningCount++;
            var run = new SpawnRun(aState, aState.RunCount, aNode, aNow);
            aState.LatestRun = run;
            mRuns.Add(run);
            string cause = aActivation.Source != null ? $"{aActivation.Source.Node.DisplayName}.{PPWaveDefinition.GetPortName(aActivation.Port)}" : "なし";
            CustomConsoleLog.Log(LogTag, $"ウェーブ: 「{aNode.DisplayName}」を出し始める(時刻 {aNow:F1} {run.Number} 回目 きっかけ={cause})");
            Emit(aState, PPWaveNodePort.Started);
        }

        // 始まっている回から時刻の来た敵を出し、出し終えた・全滅した回の出口から先へ進む
        // 1 フレームの遅れでまとめて出さないよう、出すのは 1 種類につき 1 フレームに 1 体まで
        private void UpdateRuns(float aNow)
        {
            int kept = 0;
            for (int i = 0; i < mRuns.Count; i++)
            {
                var run = mRuns[i];
                foreach (var spawn in run.Spawns)
                {
                    if (spawn.IsFinished || aNow < spawn.NextTime) continue;

                    var definition = spawn.Spawn.Unit;
                    int level = spawn.Spawn.Level + mLevelBonus.GetValueOrDefault(definition);
                    var unit = definition.CreateRuntimeUnit(level);
                    spawn.SpawnedCount++;
                    spawn.NextTime = Mathf.Max(spawn.NextTime + spawn.Spawn.Interval, aNow);
                    if (mManager.SpawnFieldUnit(unit, BattleSide.Enemy, mSpawnPosition) != null) run.SpawnedUnits.Add(unit);
                }

                if (!run.IsFinishedNotified && run.IsFinished)
                {
                    run.IsFinishedNotified = true;
                    run.Owner.RunningCount--;
                    Emit(run.Owner, PPWaveNodePort.Finished);
                }
                if (!run.IsWipedNotified && run.IsWiped)
                {
                    run.IsWipedNotified = true;
                    Emit(run.Owner, PPWaveNodePort.Wiped);
                }
                // 全滅を知らせた回と、1 体も出さずに出し終えた回は、もう見る必要がない
                if (run.IsWipedNotified || (run.IsFinishedNotified && run.SpawnedUnits.Count == 0)) continue;
                mRuns[kept++] = run;
            }
            mRuns.RemoveRange(kept, mRuns.Count - kept);
        }

        // ノードの出口から先へ進み、その出口につながったノードを動かす予定に入れる
        private void Emit(NodeState aState, PPWaveNodePort aPort)
        {
            OnNodeOutput?.Invoke(aState.Node, aPort);
            foreach (var target in aState.Outgoing[(int)aPort]) mQueue.Enqueue(new Activation(target, aState, aPort));
        }

        // 待っているトリガーの状態を今のバトルの様子で更新してから、起動する条件を満たしたかを判定する
        private bool IsTriggerSatisfied(NodeState aState)
        {
            var trigger = (PPWaveTriggerNode)aState.Node;
            trigger.Observe(aState.Trigger, this);
            return trigger.IsSatisfied(aState.Trigger, this);
        }

        /* 問い合わせ */

        // そのトリガーが今待っているか
        public bool IsWaiting(PPWaveTriggerNode aNode) => aNode != null && mStatesByNode.TryGetValue(aNode, out var state) && state.Trigger.IsWaiting;

        // そのトリガーが起動した回数
        public int GetFireCount(PPWaveTriggerNode aNode) => aNode != null && mStatesByNode.TryGetValue(aNode, out var state) ? state.Trigger.FireCount : 0;

        // そのノードが入口から動かされて動いた回数
        public int GetActivationCount(PPWaveNode aNode) => aNode != null && mStatesByNode.TryGetValue(aNode, out var state) ? state.ActivationCount : 0;

        // そのウェーブノードが出している最中の回の数
        public int GetRunningCount(PPWaveSpawnNode aNode) => aNode != null && mStatesByNode.TryGetValue(aNode, out var state) ? state.RunningCount : 0;

        public int GetBaseDamageCount(BattleSide aSide) => aSide == BattleSide.Ally ? mAllyBaseDamageCount : mEnemyBaseDamageCount;

        public float GetBaseHpRatio(BattleSide aSide)
        {
            var fieldBase = mManager.Context?.Field?.GetBase(aSide);
            return fieldBase != null ? fieldBase.Unit.Parameters.Hp.Ratio : 1f;
        }

        public bool IsLatestRunWiped(string aNodeId, out int aRunNumber)
        {
            aRunNumber = 0;
            if (string.IsNullOrEmpty(aNodeId) || !mStatesById.TryGetValue(aNodeId, out var state) || state.LatestRun == null) return false;
            aRunNumber = state.LatestRun.Number;
            return state.LatestRun.IsWiped;
        }
    }
}
