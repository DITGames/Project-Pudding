/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPWaveGraphView.cs
 * @author hqrse
 * @date 2026/11/25
 * @brief バトル定義のウェーブをノード図で表示し、ノードと線の編集をバトル定義へ書き込む
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace PPCore
{
    // ウェーブのノード図。編集は SerializedObject 経由で書き、Undo と変更済みの印を通常のアセット編集と同じにする
    // 編集できない間(Play 中)は、ノード図の変更をすべて捨てる
    public class PPWaveGraphView : GraphView
    {
        /* 定数 */

        private const float PositionEpsilon = 0.01f;
        private static readonly Vector2 PasteOffset = new(30f, 30f);

        /* 状態 */

        private readonly SerializedObject mSerializedObject;
        private readonly PPBattleDefinition mDefinition;
        private readonly Dictionary<string, PPWaveNodeView> mNodeViews = new();
        private bool mIsEditable = true;
        private bool mIsRebuilding;
        // 最後に知らせた、選んでいるノードの識別子。1 つだけ選んでいなければ null
        private string mNotifiedNodeId;
        // Space のメニューの位置に使う、最後のポインタの位置(パネルの座標)
        private Vector2 mLastMousePosition;
        // 1 回の入力の間の書き込みを 1 回の Undo にまとめるための、開いている Undo のグループ。無ければ -1
        private int mUndoGroup = -1;

        /* プロパティ */

        public bool IsEditable => mIsEditable;

        /* イベント */

        // 1 つだけ選んでいるノードが変わったとき(ノードの識別子。1 つだけ選んでいなければ null)
        public event Action<string> OnNodeSelected;
        // ノード図での編集をバトル定義へ書き込んだとき(ノードを足したか消したか)
        public event Action<bool> OnEdited;
        // データからノード図を組み直したとき
        public event Action OnRebuilt;

        /* 生成 */

        public PPWaveGraphView(SerializedObject aSerializedObject, PPBattleDefinition aDefinition)
        {
            mSerializedObject = aSerializedObject;
            mDefinition = aDefinition;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            graphViewChanged = HandleGraphViewChanged;
            serializeGraphElements = SerializeElements;
            canPasteSerializedData = aData => mIsEditable && PPWaveClipboard.CanPaste(aData);
            unserializeAndPaste = (aOperationName, aData) => Paste(aData);
            nodeCreationRequest = _ => ShowAddNodeMenu(mLastMousePosition);
            RegisterCallback<MouseMoveEvent>(e => mLastMousePosition = e.mousePosition);
            RegisterCallback<GeometryChangedEvent>(HandleFirstGeometryChanged);

            Rebuild();
        }

        /* 操作 */

        // 編集できるかを切り替える。編集できない間はノードを動かせず、線もつなげない
        public void SetEditable(bool aIsEditable)
        {
            mIsEditable = aIsEditable;
            foreach (var view in mNodeViews.Values) ApplyCapabilities(view);
            foreach (var edge in edges.ToList()) ApplyCapabilities(edge);
        }

        // バトル定義の今の内容からノード図を組み直す。選んでいたノードと表示の位置は保つ
        public void Rebuild()
        {
            var selectedIds = new List<string>();
            foreach (var selectable in selection)
            {
                if (selectable is PPWaveNodeView selectedView) selectedIds.Add(selectedView.NodeId);
            }

            mIsRebuilding = true;
            ClearSelection();
            foreach (var element in graphElements.ToList()) RemoveElement(element);
            mNodeViews.Clear();

            foreach (var node in CollectDisplayedNodes())
            {
                var view = new PPWaveNodeView(node);
                ApplyCapabilities(view);
                mNodeViews[node.Id] = view;
                AddElement(view);
            }
            foreach (var link in mDefinition.Wave.Links)
            {
                if (!TryGetPorts(link, out var output, out var input)) continue;
                var edge = new PPWaveEdge { output = output, input = input };
                output.Connect(edge);
                input.Connect(edge);
                ApplyCapabilities(edge);
                AddElement(edge);
            }
            RefreshAllNodes();

            foreach (string id in selectedIds)
            {
                if (mNodeViews.TryGetValue(id, out var view)) AddToSelection(view);
            }
            mIsRebuilding = false;
            NotifySelection();
            OnRebuilt?.Invoke();
        }

        // 各ノードの見出しと要約を、今の値に合わせる
        public void RefreshAllNodes()
        {
            var properties = PPWaveSerializedAccess.CollectNodeProperties(mSerializedObject);
            foreach (var node in mDefinition.Wave.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Id) || !mNodeViews.TryGetValue(node.Id, out var view)) continue;
                view.Refresh(node, properties.GetValueOrDefault(node.Id), mDefinition.Wave);
            }
        }

        // 点検の誤りを、ノードの赤い枠と線の赤い色にする
        public void SetProblems(IReadOnlyList<PPWaveProblem> aProblems)
        {
            var messagesByNode = new Dictionary<string, List<string>>();
            var problemLinks = new List<string>();
            foreach (var problem in aProblems)
            {
                if (problem.LinkIndex >= 0 && problem.LinkIndex < mDefinition.Wave.Links.Count)
                {
                    var link = mDefinition.Wave.Links[problem.LinkIndex];
                    if (link != null) problemLinks.Add(GetLinkKey(link.FromId, link.FromPort, link.ToId));
                    // 線として描ける誤りは線の色で知らせ、描けない誤りはノードの枠で知らせる
                    if (link != null && TryGetPorts(link, out _, out _)) continue;
                }
                if (string.IsNullOrEmpty(problem.NodeId)) continue;
                if (!messagesByNode.TryGetValue(problem.NodeId, out var messages)) messagesByNode[problem.NodeId] = messages = new List<string>();
                messages.Add(problem.Message);
            }
            foreach (var pair in mNodeViews) pair.Value.SetProblems(messagesByNode.GetValueOrDefault(pair.Key));
            foreach (var edge in edges.ToList())
            {
                if (edge is PPWaveEdge waveEdge) waveEdge.HasProblem = TryGetEdgeKey(edge, out string key) && problemLinks.Contains(key);
            }
        }

        // 識別子のノードを選ぶ。aIsFramed なら画面の中央へ寄せる
        public void SelectNode(string aId, bool aIsFramed)
        {
            if (string.IsNullOrEmpty(aId) || !mNodeViews.TryGetValue(aId, out var view)) return;
            ClearSelection();
            AddToSelection(view);
            if (aIsFramed) FrameSelection();
        }

        // ノードを足し、足したノードの識別子を返す。編集できないか種類が違えば null
        public string AddNode(Type aType, Vector2 aPosition)
        {
            if (!mIsEditable || aType == null || aType.IsAbstract || !typeof(PPWaveNode).IsAssignableFrom(aType)) return null;
            EndUndoGroup();
            mSerializedObject.Update();
            var node = (PPWaveNode)Activator.CreateInstance(aType);
            node.Position = aPosition;
            PPWaveSerializedAccess.AddNode(mSerializedObject, node);
            Commit("ノードを追加");

            AddNodeView(node.Id);
            OnEdited?.Invoke(true);
            return node.Id;
        }

        // 識別子のノードを、つながった線ごと消す。編集できなければ false
        public bool RemoveNodes(IEnumerable<string> aIds)
        {
            if (!mIsEditable) return false;
            var views = new List<GraphElement>();
            foreach (string id in aIds)
            {
                if (mNodeViews.TryGetValue(id, out var view)) views.Add(view);
            }
            if (views.Count == 0) return false;
            EndUndoGroup();
            DeleteElements(views);
            return true;
        }

        // 出口から行き先へ線をつなぐ。編集できないか、つなげない組み合わせなら false
        public bool Connect(string aFromId, PPWaveNodePort aPort, string aToId)
        {
            if (!mIsEditable || !mNodeViews.TryGetValue(aFromId ?? string.Empty, out var from) || !mNodeViews.TryGetValue(aToId ?? string.Empty, out var to)) return false;
            var output = from.GetOutputPort(aPort);
            if (output == null || to.InputPort == null) return false;

            EndUndoGroup();
            var edge = new PPWaveEdge { output = output, input = to.InputPort };
            var created = HandleGraphViewChanged(new GraphViewChange { edgesToCreate = new List<Edge> { edge } }).edgesToCreate;
            if (created == null || !created.Contains(edge)) return false;
            output.Connect(edge);
            to.InputPort.Connect(edge);
            AddElement(edge);
            return true;
        }

        // 出口から行き先への線を外す。編集できないか、その線が無ければ false
        public bool Disconnect(string aFromId, PPWaveNodePort aPort, string aToId)
        {
            if (!mIsEditable) return false;
            string key = GetLinkKey(aFromId, aPort, aToId);
            foreach (var edge in edges.ToList())
            {
                if (!TryGetEdgeKey(edge, out string edgeKey) || edgeKey != key) continue;
                EndUndoGroup();
                DeleteElements(new GraphElement[] { edge });
                return true;
            }
            return false;
        }

        // ノードを指定した位置へ動かす。編集できなければ false
        public bool MoveNode(string aId, Vector2 aPosition)
        {
            if (!mIsEditable || !mNodeViews.TryGetValue(aId ?? string.Empty, out var view)) return false;
            EndUndoGroup();
            view.SetPosition(new Rect(aPosition, view.GetPosition().size));
            HandleGraphViewChanged(new GraphViewChange { movedElements = new List<GraphElement> { view } });
            return true;
        }

        // 識別子のノードを複製し、複製したノードの識別子を返す。選んだノードどうしの線も写す
        public List<string> DuplicateNodes(IEnumerable<string> aIds)
        {
            if (!mIsEditable) return new List<string>();
            EndUndoGroup();
            return Paste(PPWaveClipboard.Serialize(mDefinition.Wave, aIds));
        }

        // 中身を変えたノードの見出しと要約を直す
        public void RefreshNode(string aId)
        {
            var node = mDefinition.Wave.FindNode(aId);
            if (node == null || !mNodeViews.TryGetValue(aId, out var view)) return;
            view.Refresh(node, PPWaveSerializedAccess.FindNodeProperty(mSerializedObject, aId), mDefinition.Wave);
        }

        /* 問い合わせ */

        // 識別子のノードの見た目。実行中の情報を重ねるときにも使う
        public bool TryGetNodeView(string aId, out PPWaveNodeView aView) => mNodeViews.TryGetValue(aId ?? string.Empty, out aView);

        // ノード図が、バトル定義の今のノード(種類と位置)と線に合っているか
        public bool IsInSync()
        {
            var nodes = CollectDisplayedNodes();
            if (nodes.Count != mNodeViews.Count) return false;
            foreach (var node in nodes)
            {
                if (!mNodeViews.TryGetValue(node.Id, out var view)) return false;
                if (Vector2.Distance(view.Position, node.Position) > PositionEpsilon) return false;
                if ((view.InputPort != null) != (node is not PPWaveTriggerNode { IgnoresInput: true })) return false;
            }

            var expected = new List<string>();
            foreach (var link in mDefinition.Wave.Links)
            {
                if (TryGetPorts(link, out _, out _)) expected.Add(GetLinkKey(link.FromId, link.FromPort, link.ToId));
            }
            var actual = new List<string>();
            foreach (var edge in edges.ToList())
            {
                if (TryGetEdgeKey(edge, out string key)) actual.Add(key);
            }
            if (expected.Count != actual.Count) return false;
            expected.Sort(StringComparer.Ordinal);
            actual.Sort(StringComparer.Ordinal);
            for (int i = 0; i < expected.Count; i++)
            {
                if (expected[i] != actual[i]) return false;
            }
            return true;
        }

        // 出口と入口がつなげる相手。向きが逆の入口・出口だけで、編集できない間は無い
        public override List<Port> GetCompatiblePorts(Port aStartPort, NodeAdapter aNodeAdapter)
        {
            var result = new List<Port>();
            if (!mIsEditable) return result;
            ports.ForEach(port =>
            {
                if (port != aStartPort && port.direction != aStartPort.direction) result.Add(port);
            });
            return result;
        }

        /* 表示 */

        // 背景の右クリックに「ノードを追加」と「全体を表示」を出す。編集できない間は「全体を表示」だけ
        public override void BuildContextualMenu(ContextualMenuPopulateEvent aEvent)
        {
            if (aEvent.target == this)
            {
                if (mIsEditable)
                {
                    Vector2 position = contentViewContainer.WorldToLocal(aEvent.mousePosition);
                    foreach (var (type, path) in CollectNodeTypes())
                    {
                        aEvent.menu.AppendAction($"ノードを追加/{path}", _ => AddNode(type, position));
                    }
                    aEvent.menu.AppendSeparator();
                }
                aEvent.menu.AppendAction("全体を表示", _ => FrameAll());
            }
            if (mIsEditable) base.BuildContextualMenu(aEvent);
        }

        // ノードを足すメニューを、ポインタの位置に出す(Space キー)
        private void ShowAddNodeMenu(Vector2 aPanelPosition)
        {
            if (!mIsEditable) return;
            Vector2 position = contentViewContainer.WorldToLocal(aPanelPosition);
            var menu = new GenericMenu();
            foreach (var (type, path) in CollectNodeTypes())
            {
                menu.AddItem(new GUIContent(path), false, () => AddNode(type, position));
            }
            menu.ShowAsContext();
        }

        /* 選択 */

        public override void AddToSelection(ISelectable aSelectable)
        {
            base.AddToSelection(aSelectable);
            NotifySelection();
        }

        public override void RemoveFromSelection(ISelectable aSelectable)
        {
            base.RemoveFromSelection(aSelectable);
            NotifySelection();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            NotifySelection();
        }

        // 1 つだけ選んでいるノードが変わっていたら知らせる。組み直している間は、終わってから 1 度だけ知らせる
        private void NotifySelection()
        {
            if (mIsRebuilding) return;
            string id = selection.Count == 1 && selection[0] is PPWaveNodeView view ? view.NodeId : null;
            if (id == mNotifiedNodeId) return;
            mNotifiedNodeId = id;
            OnNodeSelected?.Invoke(id);
        }

        /* イベントハンドラ */

        // ノード図の変更(削除・線の作成・移動)をバトル定義へ書き込み、1 回の Undo にまとめる。編集できない間は変更を捨てる
        private GraphViewChange HandleGraphViewChanged(GraphViewChange aChange)
        {
            if (!mIsEditable)
            {
                bool hasChange = aChange.elementsToRemove?.Count > 0 || aChange.edgesToCreate?.Count > 0 || aChange.movedElements?.Count > 0;
                aChange.elementsToRemove?.Clear();
                aChange.edgesToCreate?.Clear();
                aChange.movedElements?.Clear();
                // 線の端のドラッグなどで見た目だけが変わっていることがあるため、データの通りに描き直す
                if (hasChange) schedule.Execute(() => Rebuild());
                return aChange;
            }

            mSerializedObject.Update();
            bool isNodeChanged = false;
            string undoName = null;

            if (aChange.elementsToRemove != null && aChange.elementsToRemove.Count > 0)
            {
                var removedIds = new HashSet<string>();
                foreach (var element in aChange.elementsToRemove)
                {
                    if (element is PPWaveNodeView view) removedIds.Add(view.NodeId);
                }
                // 消すノードにつながった線も、ノード図から一緒に外す
                var removing = new HashSet<GraphElement>(aChange.elementsToRemove);
                foreach (string id in removedIds)
                {
                    foreach (var edge in CollectConnectedEdges(mNodeViews[id]))
                    {
                        if (removing.Add(edge)) aChange.elementsToRemove.Add(edge);
                    }
                }
                foreach (var element in aChange.elementsToRemove)
                {
                    if (element is not Edge edge || !TryGetEdgeEnds(edge, out string fromId, out var port, out string toId)) continue;
                    if (removedIds.Contains(fromId) || removedIds.Contains(toId)) continue;
                    PPWaveSerializedAccess.RemoveLink(mSerializedObject, fromId, port, toId);
                    undoName ??= "線を外す";
                }
                foreach (string id in removedIds)
                {
                    PPWaveSerializedAccess.RemoveNode(mSerializedObject, id);
                    mNodeViews.Remove(id);
                }
                if (removedIds.Count > 0)
                {
                    isNodeChanged = true;
                    undoName = "ノードを削除";
                }
            }

            if (aChange.edgesToCreate != null)
            {
                for (int i = aChange.edgesToCreate.Count - 1; i >= 0; i--)
                {
                    var edge = aChange.edgesToCreate[i];
                    // 同じ線が既にあるか、つなげない組み合わせなら作らない
                    if (!TryGetEdgeEnds(edge, out string fromId, out var port, out string toId) || PPWaveSerializedAccess.FindLinkIndex(mSerializedObject, fromId, port, toId) >= 0)
                    {
                        aChange.edgesToCreate.RemoveAt(i);
                        continue;
                    }
                    PPWaveSerializedAccess.AddLink(mSerializedObject, fromId, port, toId);
                    ApplyCapabilities(edge);
                    undoName ??= "線をつなぐ";
                }
            }

            if (aChange.movedElements != null)
            {
                foreach (var element in aChange.movedElements)
                {
                    if (element is not PPWaveNodeView view) continue;
                    PPWaveSerializedAccess.SetNodePosition(mSerializedObject, view.NodeId, view.Position);
                    undoName ??= "ノードを移動";
                }
            }

            if (undoName != null)
            {
                Commit(undoName);
                OnEdited?.Invoke(isNodeChanged);
            }
            return aChange;
        }

        // 最初に大きさが決まったときに、全体が収まるように表示を合わせる
        private void HandleFirstGeometryChanged(GeometryChangedEvent aEvent)
        {
            UnregisterCallback<GeometryChangedEvent>(HandleFirstGeometryChanged);
            schedule.Execute(() => FrameAll());
        }

        /* コピー */

        // 選んだノードの中身と、選んだノードどうしの線を文字列にする
        private string SerializeElements(IEnumerable<GraphElement> aElements)
        {
            var ids = new List<string>();
            foreach (var element in aElements)
            {
                if (element is PPWaveNodeView view) ids.Add(view.NodeId);
            }
            return PPWaveClipboard.Serialize(mDefinition.Wave, ids);
        }

        // コピーしたノードを新しい識別子で少しずらして足し、線と全滅トリガーの対象を足したノードへ付け替える。足したノードを選ぶ
        private List<string> Paste(string aData)
        {
            if (!mIsEditable || !PPWaveClipboard.CanPaste(aData)) return new List<string>();
            mSerializedObject.Update();
            var idMap = new Dictionary<string, string>();
            var pastedIds = PPWaveClipboard.WriteNodes(mSerializedObject, aData, PasteOffset, idMap);
            if (pastedIds.Count == 0) return pastedIds;
            Commit("ノードを複製");

            // 足したノードの中身を読み直してから付け替える。同じ Undo にまとまる
            mSerializedObject.Update();
            PPWaveClipboard.WriteLinksAndTargets(mSerializedObject, aData, idMap);
            Commit("ノードを複製");

            Rebuild();
            ClearSelection();
            foreach (string id in pastedIds)
            {
                if (mNodeViews.TryGetValue(id, out var view)) AddToSelection(view);
            }
            OnEdited?.Invoke(true);
            return pastedIds;
        }

        /* ヘルパー */

        // ノード図に出すノード。種類の無いものと、識別子が空か前のノードと重なっているものは出さない(点検の一覧で知らせる)
        private List<PPWaveNode> CollectDisplayedNodes()
        {
            var result = new List<PPWaveNode>();
            var ids = new HashSet<string>();
            foreach (var node in mDefinition.Wave.Nodes)
            {
                if (node != null && !string.IsNullOrEmpty(node.Id) && ids.Add(node.Id)) result.Add(node);
            }
            return result;
        }

        // ノードを足せる種類と、メニューに出す名前([PPTypeMenuName] の道筋)。名前の順に並べる
        private static List<(Type, string)> CollectNodeTypes()
        {
            var result = new List<(Type, string)>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<PPWaveNode>())
            {
                if (type.IsAbstract) continue;
                result.Add((type, type.GetCustomAttribute<PPTypeMenuNameAttribute>()?.Path ?? ObjectNames.NicifyVariableName(type.Name)));
            }
            result.Sort((a, b) => string.CompareOrdinal(a.Item2, b.Item2));
            return result;
        }

        // 足したノードの見た目をノード図に足す
        private void AddNodeView(string aId)
        {
            var node = mDefinition.Wave.FindNode(aId);
            if (node == null || mNodeViews.ContainsKey(aId)) return;
            var view = new PPWaveNodeView(node);
            ApplyCapabilities(view);
            mNodeViews[aId] = view;
            AddElement(view);
            RefreshNode(aId);
        }

        // 編集できるかに合わせて、ノードを動かす・消す・写す能力を付け外しする
        private void ApplyCapabilities(PPWaveNodeView aView)
        {
            const Capabilities editing = Capabilities.Movable | Capabilities.Deletable | Capabilities.Copiable;
            if (mIsEditable) aView.capabilities |= editing;
            else aView.capabilities &= ~editing;
        }

        // 編集できない間は、線を選べず、端をつかめないようにする
        private void ApplyCapabilities(Edge aEdge)
        {
            const Capabilities editing = Capabilities.Selectable | Capabilities.Deletable;
            if (mIsEditable) aEdge.capabilities |= editing;
            else aEdge.capabilities &= ~editing;
            aEdge.pickingMode = mIsEditable ? PickingMode.Position : PickingMode.Ignore;
        }

        // 線の両端のポート。ノード図に出ていないノードか、出口・入口の無い線なら false
        private bool TryGetPorts(PPWaveLink aLink, out Port aOutput, out Port aInput)
        {
            aOutput = null;
            aInput = null;
            if (aLink == null || !mNodeViews.TryGetValue(aLink.FromId ?? string.Empty, out var from) || !mNodeViews.TryGetValue(aLink.ToId ?? string.Empty, out var to)) return false;
            aOutput = from.GetOutputPort(aLink.FromPort);
            aInput = to.InputPort;
            return aOutput != null && aInput != null;
        }

        private static bool TryGetEdgeEnds(Edge aEdge, out string aFromId, out PPWaveNodePort aPort, out string aToId)
        {
            aFromId = (aEdge.output?.node as PPWaveNodeView)?.NodeId;
            aToId = (aEdge.input?.node as PPWaveNodeView)?.NodeId;
            aPort = aEdge.output?.userData is PPWaveNodePort port ? port : default;
            return aFromId != null && aToId != null && aEdge.output.userData is PPWaveNodePort;
        }

        private static bool TryGetEdgeKey(Edge aEdge, out string aKey)
        {
            bool isValid = TryGetEdgeEnds(aEdge, out string fromId, out var port, out string toId);
            aKey = isValid ? GetLinkKey(fromId, port, toId) : null;
            return isValid;
        }

        private static string GetLinkKey(string aFromId, PPWaveNodePort aPort, string aToId) => $"{aFromId}|{(int)aPort}|{aToId}";

        private static List<Edge> CollectConnectedEdges(PPWaveNodeView aView)
        {
            var result = new List<Edge>();
            if (aView.InputPort != null) result.AddRange(aView.InputPort.connections);
            foreach (var output in aView.outputContainer.Query<Port>().ToList()) result.AddRange(output.connections);
            return result;
        }

        // 書き込んだ変更をバトル定義へ反映する。同じ入力の間の書き込み(線のつなぎ替えの、古い線の削除と新しい線の作成など)は 1 回の Undo にまとめる
        private void Commit(string aUndoName)
        {
            if (mUndoGroup < 0)
            {
                Undo.IncrementCurrentGroup();
                mUndoGroup = Undo.GetCurrentGroup();
                // 次の更新で閉じる。それまでの書き込みは同じ Undo にまとまる
                schedule.Execute(() => EndUndoGroup());
            }
            mSerializedObject.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(mUndoGroup);
            Undo.SetCurrentGroupName(aUndoName);
        }

        // 開いている Undo のグループを閉じる。public の操作は、それぞれを 1 回の Undo にするため最初に呼ぶ
        private void EndUndoGroup() => mUndoGroup = -1;
    }
}
