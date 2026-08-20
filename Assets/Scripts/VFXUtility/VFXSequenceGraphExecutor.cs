/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceGraphExecutor.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief VFXSequenceDefinitionのノードグラフを走査・実行するエンジン
 * Unityのコルーチンに依存しないプレーンC#クラスとし、Tick(deltaTime)で外部から駆動する。
 * ランタイム(VFXSequencePlayer)とエディタ埋め込みプレビューの双方から同じロジックを使い回すための共通実装
 * =====================================*/

using System;
using System.Collections.Generic;
using CustomConsole;
using Unity.Profiling;
using UnityEngine;

namespace VFXUtility
{
    public class VFXSequenceGraphExecutor
    {
        // 循環接続でTick内の同一Tick連鎖発火が無限に続くのを防ぐための、1ノードあたりの発火回数上限
        private const int MaxFireCountPerNode = 1000;

        // 発火直後、この秒数が経過するまではIsAlive判定を行わない(VFX Graphが実際に粒子を生成し始めるまでのラグ対策)
        // 1Tick分のみの猶予だとフレームレートやスクリプト実行順序によっては足りず、生成直後のVFXが即座に「終了」扱いになってしまうため秒数ベースにしている
        private const float MinAliveCheckDelay = 0.2f;

        // IsAliveがfalseを返し続けた状態がこの秒数継続して初めて「終了」とみなす(ループ/バースト周期を持つVFXは、
        // 1周期の中で瞬間的にaliveParticleCountが0になる谷間があり得るため、単発のfalseで即座に終了扱いにしない)
        private const float NaturalCompletionGraceSeconds = 0.5f;

        // 生存判定の実行間隔(秒)。毎フレームaliveParticleCountを参照しないようにして負荷を下げる
        private const float AliveCheckInterval = 0.1f;

        private static readonly ProfilerMarker sTickMarker = new("VFXSequenceGraphExecutor.Tick");
        private static readonly ProfilerMarker sFireNodeMarker = new("VFXSequenceGraphExecutor.FireNode");
        private static readonly ProfilerMarker sAliveCheckMarker = new("VFXSequenceGraphExecutor.AliveCheck");

        // 発火予約1件分(まだ再生していない、これから発火するノード)
        private class ScheduledFire
        {
            public VFXSequenceNodeBase Node;
            public float FireAtTime;
            public string OriginRootId;
        }

        // 現在再生中のVFXインスタンス1件分
        private class AliveVfx
        {
            public object HostHandle;
            public VFXSequenceNodeBase Node;
            public string OriginRootId;
            // このVFXが発火した時点のSession.ElapsedTime。IsAlive判定の猶予期間の起点として使う
            public float FiredAtTime;
            // IsAliveが連続してfalseを返し始めた時点のSession.ElapsedTime。まだfalseが続いていなければnull
            public float? NotAliveSinceTime;
        }

        // Play()/PlayEvent()呼び出し1回分の再生セッション
        private class Session
        {
            public int Handle;
            public float ElapsedTime;
            public float LastAliveCheckTime;
            public readonly List<ScheduledFire> Pending = new();
            public readonly List<AliveVfx> Alive = new();
            public readonly Dictionary<string, int> FireCounts = new();
            // ループノードの周回カウント(Key: ループノードのNodeId、Value: 現在何周目か)。ループ継続ノードが参照・更新する
            public readonly Dictionary<string, int> LoopIterationCounts = new();
        }

        private readonly VFXSequenceDefinition mDefinition;
        private readonly IVFXSequenceHost mHost;
        private readonly Dictionary<int, Session> mSessions = new();
        // 公開名をキーとした上書き値。公開名が設定されたパラメータのみが対象になる
        private readonly Dictionary<string, object> mOverrides = new();
        // Tick()内でmSessions.Keysのスナップショットを取るための使い回しバッファ(毎フレームのGCアロケーション回避)
        private readonly List<int> mTickHandleScratch = new();
        private int mNextHandle = 1;

        // ゴールノードに到達してセッションが完了した際に発火する(引数はそのセッションのハンドル)
        public event Action<int> OnSequenceCompleted;

        // Delay経過後にノードが発火した際、そのノードの通知イベント名を渡して発火する(通知イベント名が空のノードでは発火しない)
        public event Action<string> OnNodeStarted;

        // ゴールノードに到達しないままやることが無くなり、セッションが破棄された際に発火する(引数はそのセッションのハンドル)
        // 完了ではないため通知用途には使わず、PlayAsyncが永久に待ち続ける事故を検知するために使う
        public event Action<int> OnSessionDiscarded;

        // 実行中のセッションが1件以上あるか。アイドル時にUpdateを止める判断に使う
        public bool HasActiveSessions => mSessions.Count > 0;

        // 指定ハンドルのセッションがまだ実行中か
        // aHandle : Play()が返したハンドル
        public bool IsSessionActive(int aHandle) => mSessions.ContainsKey(aHandle);

        // aDefinition : 実行対象のノードグラフ / aHost : 実際のVFX再生・停止を委譲する先
        public VFXSequenceGraphExecutor(VFXSequenceDefinition aDefinition, IVFXSequenceHost aHost)
        {
            mDefinition = aDefinition;
            mHost = aHost;
        }

        // 新規セッションを開始し、唯一のルートノードを予約する
        // 戻り値 : このセッションを識別するハンドル。以降Stop(ハンドル)で個別停止できる
        public int Play()
        {
            Session session = CreateSession();

            VFXSequenceRootNode rootNode = mDefinition?.GetPlayRootNodeOrNull();
            if (rootNode == null)
            {
                CustomConsoleLog.Warning("VFXUtility", "ルートノードが見つからないためPlay()は何も開始しません");
            }
            else
            {
                // ルートノード自身はt=0で発火する(既存のFireNode経路に乗せることで、通知イベント名・発火回数ガード等の
                // 共通処理を素通りさせず正しく適用させる)
                ScheduleNode(session, rootNode, 0f, null);
            }

            TickSession(session);
            return session.Handle;
        }

        // 指定セッションのみを個別に停止する(完了イベントは発火しない)。無効なハンドルは無視する
        // aHandle : Play()が返したハンドル
        public void Stop(int aHandle)
        {
            if (!mSessions.TryGetValue(aHandle, out Session session))
            {
                return;
            }

            StopSessionInternal(session);
            mSessions.Remove(aHandle);
        }

        // 外部からのイベント発火。該当イベント名のイベントノードを新規セッションとして開始する
        // aEventName : 発火するイベント名
        public void PlayEvent(string aEventName)
        {
            if (mDefinition == null)
            {
                return;
            }

            Session session = CreateSession();

            foreach (VFXSequenceEventNode eventNode in mDefinition.FindEventNodes(aEventName))
            {
                ScheduleNode(session, eventNode, 0f, null);
            }

            TickSession(session);
        }

        // 公開名を指定してパラメータ値を上書きする。以降そのパラメータが適用される際に反映される
        // aExposedName : 対象パラメータの公開名 / aValue : 上書き値
        public void SetOverride(string aExposedName, object aValue)
        {
            if (string.IsNullOrEmpty(aExposedName))
            {
                CustomConsoleLog.Warning("VFXUtility", "公開名が空のため上書きできません");
                return;
            }

            mOverrides[aExposedName] = aValue;
        }

        // オーバーライドセットの有効なエントリを一括で適用する
        // 対象グラフに存在しない公開名は適用せず警告を出す
        // aOverrideSet : 適用するオーバーライドセット
        public void ApplyOverrideSet(VFXSequenceOverrideSet aOverrideSet)
        {
            if (aOverrideSet == null)
            {
                return;
            }

            HashSet<string> knownExposedNames = mDefinition != null
                ? mDefinition.CollectExposedNames()
                : new HashSet<string>();

            foreach (VFXSequenceOverrideEntry entry in aOverrideSet.Entries)
            {
                if (!entry.Enabled || string.IsNullOrEmpty(entry.ExposedName))
                {
                    continue;
                }

                if (!knownExposedNames.Contains(entry.ExposedName))
                {
                    CustomConsoleLog.Warning("VFXUtility", $"オーバーライドセットの公開名'{entry.ExposedName}'は対象グラフに存在しないため適用しません");
                    continue;
                }

                mOverrides[entry.ExposedName] = entry.GetValue();
            }
        }

        // 全セッションを進行させる。毎フレーム(またはエディタ更新ごと)に外部から呼ぶ
        // aDeltaTime : 前回呼び出しからの経過秒数
        public void Tick(float aDeltaTime)
        {
            using (sTickMarker.Auto())
            {
                mTickHandleScratch.Clear();
                mTickHandleScratch.AddRange(mSessions.Keys);

                foreach (int handle in mTickHandleScratch)
                {
                    if (!mSessions.TryGetValue(handle, out Session session))
                    {
                        continue; // このTick中に他ノードの制御処理で既に停止済み
                    }

                    session.ElapsedTime += aDeltaTime;
                    TickSession(session);
                }
            }
        }

        // 予約時刻を迎えたノードを同一Tick内で連鎖的に発火させ、生存確認と枯渇判定を行う
        private void TickSession(Session session)
        {
            while (true)
            {
                ScheduledFire next = null;
                for (int i = 0; i < session.Pending.Count; i++)
                {
                    ScheduledFire candidate = session.Pending[i];
                    if (candidate.FireAtTime > session.ElapsedTime)
                    {
                        continue;
                    }
                    if (next == null || candidate.FireAtTime < next.FireAtTime)
                    {
                        next = candidate;
                    }
                }

                if (next == null)
                {
                    break;
                }

                session.Pending.Remove(next);
                FireNode(session, next);

                if (!mSessions.ContainsKey(session.Handle))
                {
                    return; // 発火した制御ノードの処理でこのセッション自体が破棄された(ゴール到達・StopAll等)
                }
            }

            UpdateAliveVfx(session);
            DiscardIfExhausted(session);
        }

        // 再生中VFXの生存判定を行い、自然終了したものを破棄して追跡から外す
        // 完了通知はゴールノードのみが行うため、ここでは通知しない
        private void UpdateAliveVfx(Session session)
        {
            if (session.Alive.Count == 0)
            {
                return;
            }

            // 毎フレームaliveParticleCountを触らないよう、一定間隔でのみ判定する
            if (session.ElapsedTime - session.LastAliveCheckTime < AliveCheckInterval)
            {
                return;
            }
            session.LastAliveCheckTime = session.ElapsedTime;

            using (sAliveCheckMarker.Auto())
            {
                for (int i = session.Alive.Count - 1; i >= 0; i--)
                {
                    AliveVfx alive = session.Alive[i];

                    // 発火直後はVFXがまだ粒子を生成していないことがあるため猶予を設ける
                    if (session.ElapsedTime - alive.FiredAtTime < MinAliveCheckDelay)
                    {
                        continue;
                    }

                    if (mHost.IsAlive(alive.HostHandle))
                    {
                        alive.NotAliveSinceTime = null;
                        continue;
                    }

                    alive.NotAliveSinceTime ??= session.ElapsedTime;
                    if (session.ElapsedTime - alive.NotAliveSinceTime.Value < NaturalCompletionGraceSeconds)
                    {
                        continue;
                    }

                    // 自然終了したVFXは破棄(プール使用時は返却)して追跡から外す
                    mHost.StopVFX(alive.HostHandle);
                    session.Alive.RemoveAt(i);
                }
            }
        }

        private void FireNode(Session session, ScheduledFire aFire)
        {
            using (sFireNodeMarker.Auto())
            {
                VFXSequenceNodeBase node = aFire.Node;

                int fireCount = session.FireCounts.TryGetValue(node.NodeId, out int count) ? count : 0;
                if (fireCount >= MaxFireCountPerNode)
                {
                    CustomConsoleLog.Warning("VFXUtility", $"ノード'{node.NodeId}'の発火回数が上限({MaxFireCountPerNode})を超えたため、これ以上の伝播を打ち切ります。循環接続の可能性があります");
                    return;
                }
                session.FireCounts[node.NodeId] = fireCount + 1;

                // Delay経過後の発火時点で通知する(通知イベント名が空のノードでは通知しない)
                if (!string.IsNullOrEmpty(node.NotifyEventName))
                {
                    OnNodeStarted?.Invoke(node.NotifyEventName);
                }

                switch (node)
                {
                    case VFXSequenceRootNode:
                        // 通常のScheduleNextNodesは使わない(全接続先へ同一のOriginRootIdを伝播してしまうため)。
                        // ルートノードの直接の接続先ごとに、接続先自身のNodeIdを新しいOriginRootIdとして個別に予約する
                        // (StopNodeノードがブランチ単位で停止する際のキーになる)
                        foreach (string nextNodeId in node.NextNodeIds)
                        {
                            VFXSequenceNodeBase branchHead = mDefinition?.FindNode(nextNodeId);
                            if (branchHead == null)
                            {
                                continue; // 削除済みノードへの参照は無視する
                            }
                            ScheduleNode(session, branchHead, session.ElapsedTime + branchHead.DelaySeconds, branchHead.NodeId);
                        }
                        break;
                    case VFXSequencePlayableNodeBase playableNode:
                        FirePlayableNode(session, playableNode, aFire.OriginRootId);
                        ScheduleNextNodes(session, node, aFire.OriginRootId);
                        break;
                    case VFXSequencePlayEventTriggerNode triggerNode:
                        FireTriggerNode(session, triggerNode);
                        ScheduleNextNodes(session, node, aFire.OriginRootId);
                        break;
                    case VFXSequenceStopNodeNode stopNodeNode:
                        CustomConsoleLog.Verbose("VFXUtility_Verify", $"StopNodeノード'{node.NodeId}'発火。対象ノードID='{stopNodeNode.TargetBranchNodeId}'");
                        // 先に後続ノードを予約してからStop処理を行う(Stop処理内の枯渇判定で、まだ後続を控えている自セッションが
                        // 誤って破棄されないようにするため)
                        ScheduleNextNodes(session, node, aFire.OriginRootId);
                        StopNodeByRootId(stopNodeNode.TargetBranchNodeId);
                        break;
                    case VFXSequenceStopVFXNode stopVfxNode:
                        CustomConsoleLog.Verbose("VFXUtility_Verify", $"StopVFXノード'{node.NodeId}'発火。対象ノードID='{stopVfxNode.TargetNodeId}'");
                        ScheduleNextNodes(session, node, aFire.OriginRootId);
                        StopVFXByNodeId(stopVfxNode.TargetNodeId);
                        break;
                    case VFXSequenceStopAllNode:
                        CustomConsoleLog.Verbose("VFXUtility_Verify", $"StopAllノード'{node.NodeId}'発火。全セッション({mSessions.Count}件)を停止します");
                        StopAll();
                        // 全セッションを停止した後も、このStopAllノード自身のフローだけは新セッションとして継続する
                        session = CreateSession();
                        ScheduleNextNodes(session, node, aFire.OriginRootId);
                        break;
                    case VFXSequenceLoopNode:
                        // 本体接続先(mNextNodeIds)は通常ノードと同じ要領で発火する。次周回への再発火はループ継続ノードが行う
                        ScheduleNextNodes(session, node, aFire.OriginRootId);
                        break;
                    case VFXSequenceRandomBranchNode randomBranch:
                        FireRandomBranch(session, randomBranch, aFire.OriginRootId);
                        break;
                    case VFXSequenceConditionalBranchNode conditionalBranch:
                        FireConditionalBranch(session, conditionalBranch, aFire.OriginRootId);
                        break;
                    case VFXSequenceSetParameterNode setParameterNode:
                        ScheduleNextNodes(session, node, aFire.OriginRootId);
                        ApplyParametersToNode(setParameterNode);
                        break;
                    case VFXSequenceLoopContinueNode continueNode:
                        FireLoopContinue(session, continueNode, aFire.OriginRootId);
                        break;
                    case VFXSequenceGoalNode:
                        CustomConsoleLog.Verbose("VFXUtility_Verify", $"ゴールノード'{node.NodeId}'に到達。セッション#{session.Handle}を完了します");
                        CompleteSession(session);
                        break; // ゴールは終端のため後続ノードを予約しない
                    default:
                        ScheduleNextNodes(session, node, aFire.OriginRootId);
                        break;
                }
            }
        }

        // 通常ノード・イベントノードの発火処理。VFXを再生しパラメータを適用する
        private void FirePlayableNode(Session session, VFXSequencePlayableNodeBase aNode, string aOriginRootId)
        {
            if (aNode.VisualEffectAsset == null)
            {
                CustomConsoleLog.Warning("VFXUtility", $"ノード'{aNode.NodeId}'にVFXアセットが未設定のため再生をスキップします");
                return;
            }

            object handle = mHost.PlayVFX(aNode.VisualEffectAsset, aNode.PositionOffset, aNode.RotationOffset, aNode.ScaleOffset);
            CustomConsoleLog.Verbose("VFXUtility_Verify", $"ノード'{aNode.NodeId}'({aNode.VisualEffectAsset.name})を再生開始。セッション#{session.Handle}, t={session.ElapsedTime:F2}");

            foreach (VFXSequenceNodeParameter param in aNode.Parameters)
            {
                mHost.ApplyParameter(handle, param.ParamName, param.ParamType, ResolveParameterValue(param));
            }

            session.Alive.Add(new AliveVfx
            {
                HostHandle = handle,
                Node = aNode,
                OriginRootId = aOriginRootId,
                FiredAtTime = session.ElapsedTime,
            });
        }

        // 公開名の上書きが登録されていればその値を、無ければノードの埋め込み値を返す
        // 公開名が空のパラメータは上書き対象にならない
        // aParam : 値を解決するパラメータ
        private object ResolveParameterValue(VFXSequenceNodeParameter aParam)
        {
            if (string.IsNullOrEmpty(aParam.ExposedName))
            {
                return aParam.GetValue();
            }

            if (!mOverrides.TryGetValue(aParam.ExposedName, out object overrideValue))
            {
                return aParam.GetValue();
            }

            if (!IsValueCompatible(aParam.ParamType, overrideValue))
            {
                CustomConsoleLog.Warning("VFXUtility", $"公開名'{aParam.ExposedName}'の上書き値の型がパラメータ型({aParam.ParamType})と一致しないため、埋め込み値を使用します");
                return aParam.GetValue();
            }

            return overrideValue;
        }

        // 上書き値がパラメータ型と適合するかを判定する
        // aParamType : パラメータ型 / aValue : 判定する値
        private static bool IsValueCompatible(VFXParameterType aParamType, object aValue)
        {
            return aParamType switch
            {
                VFXParameterType.Float => aValue is float,
                VFXParameterType.Int => aValue is int,
                VFXParameterType.Bool => aValue is bool,
                VFXParameterType.Vector2 => aValue is Vector2,
                VFXParameterType.Vector3 => aValue is Vector3,
                VFXParameterType.Vector4 => aValue is Vector4,
                VFXParameterType.Color => aValue is Color,
                VFXParameterType.Event => aValue == null,
                _ => false,
            };
        }

        // PlayEvent発火ノードの発火処理。同一グラフ内の一致するイベントノードを、同一セッション内の新たな開始点として予約する
        private void FireTriggerNode(Session session, VFXSequencePlayEventTriggerNode aNode)
        {
            if (mDefinition == null)
            {
                return;
            }

            foreach (VFXSequenceEventNode eventNode in mDefinition.FindEventNodes(aNode.EventName))
            {
                // イベント経由の開始点は、ルートノードから始まるブランチの系譜とは別枠として扱う(OriginRootIdなし)
                ScheduleNode(session, eventNode, session.ElapsedTime, null);
            }
        }

        // 接続先ごとの重みで抽選し、選ばれた1件のみを予約する(重み合計が0以下の場合は均等重みとして扱う)
        private void FireRandomBranch(Session session, VFXSequenceRandomBranchNode aNode, string aOriginRootId)
        {
            var candidates = new List<VFXSequenceNodeBase>();
            var weights = new List<float>();
            float totalWeight = 0f;

            foreach (VFXSequenceBranchWeight entry in aNode.Weights)
            {
                VFXSequenceNodeBase target = mDefinition?.FindNode(entry.TargetNodeId);
                if (target == null)
                {
                    continue; // 削除済みノードへの参照は無視する
                }
                float weight = Mathf.Max(0f, entry.Weight);
                candidates.Add(target);
                weights.Add(weight);
                totalWeight += weight;
            }

            if (candidates.Count == 0)
            {
                return;
            }

            int selectedIndex;
            if (totalWeight > 0f)
            {
                float roll = UnityEngine.Random.Range(0f, totalWeight);
                float cursor = 0f;
                selectedIndex = candidates.Count - 1;
                for (int i = 0; i < weights.Count; i++)
                {
                    cursor += weights[i];
                    if (roll < cursor)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }
            else
            {
                selectedIndex = UnityEngine.Random.Range(0, candidates.Count); // 全重み0 → 均等抽選にフォールバック
            }

            VFXSequenceNodeBase selected = candidates[selectedIndex];
            ScheduleNode(session, selected, session.ElapsedTime + selected.DelaySeconds, aOriginRootId);
        }

        // 条件用公開名の現在値(未設定時は既定値)を判定し、該当する側の接続先を全て予約する
        private void FireConditionalBranch(Session session, VFXSequenceConditionalBranchNode aNode, string aOriginRootId)
        {
            bool value = aNode.DefaultValue;
            if (!string.IsNullOrEmpty(aNode.ConditionExposedName) &&
                mOverrides.TryGetValue(aNode.ConditionExposedName, out object overrideValue) &&
                overrideValue is bool boolValue)
            {
                value = boolValue;
            }

            foreach (VFXSequenceBranchCondition branch in aNode.Branches)
            {
                if (branch.FireOnTrue != value)
                {
                    continue;
                }
                VFXSequenceNodeBase target = mDefinition?.FindNode(branch.TargetNodeId);
                if (target == null)
                {
                    continue; // 削除済みノードへの参照は無視する
                }
                ScheduleNode(session, target, session.ElapsedTime + target.DelaySeconds, aOriginRootId);
            }
        }

        // 対象ループノードの周回カウントを見て、次周回への再発火(規定回数未満または無限)、または
        // 完了後(自身の接続先)への進行のどちらかを行う
        private void FireLoopContinue(Session session, VFXSequenceLoopContinueNode aNode, string aOriginRootId)
        {
            VFXSequenceNodeBase targetNode = mDefinition?.FindNode(aNode.TargetLoopNodeId);
            if (targetNode is not VFXSequenceLoopNode loopNode)
            {
                CustomConsoleLog.Warning("VFXUtility", $"ループ継続ノード'{aNode.NodeId}'の対象ループノードが見つからないため、完了後へ進みます");
                ScheduleNextNodes(session, aNode, aOriginRootId);
                return;
            }

            int iteration = session.LoopIterationCounts.TryGetValue(loopNode.NodeId, out int count) ? count : 1;
            bool shouldContinue = loopNode.LoopCount <= 0 || iteration < loopNode.LoopCount;

            if (shouldContinue)
            {
                session.LoopIterationCounts[loopNode.NodeId] = iteration + 1;
                ScheduleNextNodes(session, loopNode, aOriginRootId); // 本体を再度予約(次周回)
            }
            else
            {
                session.LoopIterationCounts.Remove(loopNode.NodeId);
                ScheduleNextNodes(session, aNode, aOriginRootId); // 完了後へ
            }
        }

        private void ScheduleNextNodes(Session session, VFXSequenceNodeBase aNode, string aOriginRootId)
        {
            foreach (string nextNodeId in aNode.NextNodeIds)
            {
                VFXSequenceNodeBase next = mDefinition?.FindNode(nextNodeId);
                if (next == null)
                {
                    continue; // 削除済みノードへの参照等は無視する
                }

                ScheduleNode(session, next, session.ElapsedTime + next.DelaySeconds, aOriginRootId);
            }
        }

        private void ScheduleNode(Session session, VFXSequenceNodeBase aNode, float aFireAtTime, string aOriginRootId)
        {
            session.Pending.Add(new ScheduledFire { Node = aNode, FireAtTime = aFireAtTime, OriginRootId = aOriginRootId });
        }

        // 新規セッションを作成してmSessionsへ登録する
        private Session CreateSession()
        {
            var session = new Session { Handle = mNextHandle++ };
            mSessions[session.Handle] = session;
            return session;
        }

        // ゴールノード到達時の処理。自セッションのみを停止し、完了通知を発火する
        private void CompleteSession(Session session)
        {
            StopSessionInternal(session);
            if (mSessions.Remove(session.Handle))
            {
                OnSequenceCompleted?.Invoke(session.Handle);
            }
        }

        // やることが無くなったセッションを破棄する。完了通知は行わない(通知はゴール到達時のみ)
        private void DiscardIfExhausted(Session session)
        {
            if (session.Pending.Count > 0 || session.Alive.Count > 0)
            {
                return;
            }

            if (mSessions.Remove(session.Handle))
            {
                OnSessionDiscarded?.Invoke(session.Handle);
            }
        }

        // 指定ブランチ(ルートノードの直接の接続先)から始まる全てのフローを、全セッションを横断して停止する
        private void StopNodeByRootId(string aTargetBranchNodeId)
        {
            if (string.IsNullOrEmpty(aTargetBranchNodeId))
            {
                CustomConsoleLog.Warning("VFXUtility_Verify", "StopNodeノードの対象ノードIDが未設定のため何も停止しません");
                return;
            }

            int stoppedCount = 0;
            foreach (Session session in mSessions.Values)
            {
                session.Pending.RemoveAll(p => p.OriginRootId == aTargetBranchNodeId);

                for (int i = session.Alive.Count - 1; i >= 0; i--)
                {
                    AliveVfx alive = session.Alive[i];
                    if (alive.OriginRootId != aTargetBranchNodeId)
                    {
                        continue;
                    }
                    mHost.StopVFX(alive.HostHandle);
                    session.Alive.RemoveAt(i);
                    stoppedCount++;
                }
            }

            if (stoppedCount == 0)
            {
                CustomConsoleLog.Warning("VFXUtility_Verify", $"StopNode: 対象ノードID'{aTargetBranchNodeId}'に一致する再生中フローが見つかりませんでした(既に終了している、対象ノードの発火がまだ、または指定間違いの可能性があります)");
            }
            else
            {
                CustomConsoleLog.Verbose("VFXUtility_Verify", $"StopNode: 対象ノードID'{aTargetBranchNodeId}'に一致する{stoppedCount}件のVFXを停止しました");
            }

            DiscardExhaustedSessions();
        }

        // 指定ノードが再生中のVFXインスタンスを、全セッションを横断して停止する
        private void StopVFXByNodeId(string aTargetNodeId)
        {
            if (string.IsNullOrEmpty(aTargetNodeId))
            {
                CustomConsoleLog.Warning("VFXUtility_Verify", "StopVFXノードの対象ノードIDが未設定のため何も停止しません");
                return;
            }

            // 診断用: このStopVFX実行時点で全セッションにまたがって再生中の全VFXの内訳を出す(対象ノードIDの重複・取り違え等の切り分け用)
            LogAliveVfxSnapshot(aTargetNodeId);

            int stoppedCount = 0;
            foreach (Session session in mSessions.Values)
            {
                for (int i = session.Alive.Count - 1; i >= 0; i--)
                {
                    AliveVfx alive = session.Alive[i];
                    if (alive.Node.NodeId != aTargetNodeId)
                    {
                        continue;
                    }
                    mHost.StopVFX(alive.HostHandle);
                    session.Alive.RemoveAt(i);
                    stoppedCount++;
                }
            }

            if (stoppedCount == 0)
            {
                CustomConsoleLog.Warning("VFXUtility_Verify", $"StopVFX: 対象ノード'{aTargetNodeId}'は現在再生中のVFXがありませんでした(既に終了している、またはまだ発火していない可能性があります)");
            }
            else
            {
                CustomConsoleLog.Verbose("VFXUtility_Verify", $"StopVFX: 対象ノード'{aTargetNodeId}'のVFXを{stoppedCount}件停止しました");
            }

            DiscardExhaustedSessions();
        }

        // 診断用: 現在再生中の全VFXのノードID・VFXアセット名を一覧ログ出力する
        // aTargetNodeId : 比較対象として強調表示する対象ノードID(StopVFXの対象)
        private void LogAliveVfxSnapshot(string aTargetNodeId)
        {
            var lines = new System.Text.StringBuilder();
            int total = 0;
            foreach (Session session in mSessions.Values)
            {
                foreach (AliveVfx alive in session.Alive)
                {
                    total++;
                    bool isMatch = alive.Node.NodeId == aTargetNodeId;
                    string assetName = alive.Node is VFXSequencePlayableNodeBase playable && playable.VisualEffectAsset != null
                        ? playable.VisualEffectAsset.name
                        : "?";
                    lines.Append($"\n  - セッション#{session.Handle}: ノードID='{alive.Node.NodeId}'({assetName}) {(isMatch ? "★対象一致" : "")}");
                }
            }

            CustomConsoleLog.Verbose("VFXUtility_Verify", $"StopVFX実行直前の再生中VFX一覧(計{total}件、対象ノードID='{aTargetNodeId}'){lines}");
        }

        // 指定ノードが再生中のVFXインスタンスへ、新規再生を行わずパラメータのみを適用する(全セッションを横断する)
        private void ApplyParametersToNode(VFXSequenceSetParameterNode aNode)
        {
            if (string.IsNullOrEmpty(aNode.TargetNodeId))
            {
                CustomConsoleLog.Warning("VFXUtility_Verify", "SetParameterノードの対象ノードIDが未設定のため何も適用しません");
                return;
            }

            int appliedCount = 0;
            foreach (Session session in mSessions.Values)
            {
                foreach (AliveVfx alive in session.Alive)
                {
                    if (alive.Node.NodeId != aNode.TargetNodeId)
                    {
                        continue;
                    }

                    foreach (VFXSequenceNodeParameter param in aNode.Parameters)
                    {
                        mHost.ApplyParameter(alive.HostHandle, param.ParamName, param.ParamType, ResolveParameterValue(param));
                    }
                    appliedCount++;
                }
            }

            if (appliedCount == 0)
            {
                CustomConsoleLog.Warning("VFXUtility_Verify", $"SetParameter: 対象ノード'{aNode.TargetNodeId}'は現在再生中のVFXがありませんでした(パラメータは適用されません)");
            }
            else
            {
                CustomConsoleLog.Verbose("VFXUtility_Verify", $"SetParameter: 対象ノード'{aNode.TargetNodeId}'の{appliedCount}件のVFXにパラメータを適用しました");
            }
        }

        // 実行中の全セッションを一括停止する(完了イベントは発火しない)
        private void StopAll()
        {
            int sessionCount = mSessions.Count;
            int stoppedCount = 0;
            foreach (Session session in mSessions.Values)
            {
                stoppedCount += session.Alive.Count;
                StopSessionInternal(session);
            }
            mSessions.Clear();

            CustomConsoleLog.Verbose("VFXUtility_Verify", $"StopAll: {sessionCount}セッション、計{stoppedCount}件のVFXを停止しました");
        }

        private void StopSessionInternal(Session session)
        {
            foreach (AliveVfx alive in session.Alive)
            {
                mHost.StopVFX(alive.HostHandle);
            }
            session.Alive.Clear();
            session.Pending.Clear();
        }

        // 全セッションを走査し、やることが無くなったものを破棄する(完了通知はしない)
        private void DiscardExhaustedSessions()
        {
            foreach (Session session in new List<Session>(mSessions.Values))
            {
                DiscardIfExhausted(session);
            }
        }
    }
}
