/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAITreeGraphView.cs
 * @author hqrse
 * @date 2026/08/25
 * @brief 判断ツリーをノードグラフとして表示・編集するGraphView
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace PPCore
{
    // 判断ツリー（PPUnitAIProfileDefinition）をノードグラフとして編集する GraphView
    //
    // ノードの追加・削除・接続・移動は全て SerializedObject を通してアセットへ反映する
    // Undo とダーティ管理を Unity 側に任せられるため、独自の履歴を持たずに済む
    //
    // 優先度リストの子の並び順は、グラフ上の縦位置（上にあるものほど優先）から決める
    // 接続線の順序は見た目に出ないため、位置で表現したほうが読み違えない
    internal sealed class PPUnitAITreeGraphView : GraphView
    {
        private readonly SerializedObject mSerializedObject;
        private readonly PPUnitAIProfileDefinition mProfile;
        private readonly Dictionary<string, PPUnitAITreeNodeView> mNodeViews = new();
        // 注記のビュー。ID から引いて削除・保存に使う
        private readonly Dictionary<string, PPUnitAITreeNoteElement> mNoteViews = new();

        // 貼り付けたノードを複写元からずらす量。真上に重なって見失うのを防ぐ
        private static readonly Vector2 PasteOffset = new(30f, 30f);

        // 自動整列で使う、深さ 1 段ぶんの横幅とノード 1 つぶんの縦幅
        private const float LayoutColumnWidth = 320f;
        private const float LayoutRowHeight = 150f;

        // 選択中ノードが変わった際に、対応するノードを渡して通知する(未選択時は null)
        public event Action<PPUnitAINode> OnNodeSelectionChanged;

        // グラフ構造が変わった際に通知する。ルート未設定などの警告表示を更新するのに使う
        public event Action OnGraphStructureChanged;

        // aSerializedObject : 編集対象のプロファイルの SerializedObject
        // aProfile : 表示対象の判断ツリー
        public PPUnitAITreeGraphView(SerializedObject aSerializedObject, PPUnitAIProfileDefinition aProfile)
        {
            mSerializedObject = aSerializedObject;
            mProfile = aProfile;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            SetupMiniMap();
            // Ctrl+F でノードを名前検索できるようにする。木が大きくなると目視で探せなくなるため
            RegisterCallback<KeyDownEvent>(OnKeyDown);

            graphViewChanged = OnGraphViewChanged;
            // Ctrl+C / Ctrl+V / Ctrl+D はこの 3 つの委譲を通って処理される
            serializeGraphElements = SerializeSelection;
            canPasteSerializedData = PPUnitAITreeClipboard.CanDeserialize;
            unserializeAndPaste = PasteSerializedData;

            Rebuild();
        }

        // 右クリックメニューにノード追加の項目を並べる
        // 追加できる型は PPTypeMenuName を持つ PPUnitAINode 派生から集める
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            Vector2 graphPosition = contentViewContainer.WorldToLocal(evt.mousePosition);

            foreach (var (type, displayName) in EnumerateNodeTypes())
            {
                evt.menu.AppendAction($"ノードを追加/{displayName}", _ => AddNode(type, graphPosition));
            }

            if (selection.OfType<PPUnitAITreeNodeView>().FirstOrDefault() is { } selected)
            {
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("このノードをルートにする", _ => SetRoot(selected.Node));
            }

            base.BuildContextualMenu(evt);
        }

        // 接続可能なポートを返す。入力と出力、かつ別ノード同士のみ繋げる
        public override List<Port> GetCompatiblePorts(Port aStartPort, NodeAdapter aNodeAdapter)
            => ports.ToList()
                .Where(p => p != aStartPort && p.node != aStartPort.node && p.direction != aStartPort.direction)
                .ToList();

        // 診断結果をグラフへ反映する
        // 問題が見つかったノードをグレー表示にし、それ以外は種別ごとの色へ戻す
        // aIssues : 反映する診断結果
        public void ApplyIssues(IReadOnlyList<PPUnitAITreeIssue> aIssues)
        {
            // 1 つのノードに複数の問題が出ることがあるため、ノードごとにまとめてから渡す
            // 文言はそのままノード上の警告アイコンのツールチップになる
            var messages = new Dictionary<string, List<string>>();
            foreach (var issue in aIssues)
            {
                if (string.IsNullOrEmpty(issue.NodeId)) continue;

                if (!messages.TryGetValue(issue.NodeId, out var list))
                {
                    list = new List<string>();
                    messages[issue.NodeId] = list;
                }
                list.Add(issue.Message);
            }

            foreach (var pair in mNodeViews)
            {
                pair.Value.SetIssues(messages.TryGetValue(pair.Key, out var found) ? found : null);
            }
        }

        // 思考記録 1 件分の通過経路を強調表示する
        // aVisitedNodeIds : 通過したノードの ID。null や空なら強調を解除する
        // aDecidedNodeId : 行動が確定したノードの ID
        public void ApplyHighlight(IReadOnlyList<string> aVisitedNodeIds, string aDecidedNodeId)
        {
            var visited = new HashSet<string>();
            if (aVisitedNodeIds != null)
            {
                foreach (var nodeId in aVisitedNodeIds)
                {
                    if (!string.IsNullOrEmpty(nodeId)) visited.Add(nodeId);
                }
            }

            foreach (var pair in mNodeViews)
            {
                var state = PPUnitAITreeHighlight.None;
                if (pair.Key == aDecidedNodeId) state = PPUnitAITreeHighlight.Decided;
                else if (visited.Contains(pair.Key)) state = PPUnitAITreeHighlight.Passed;

                pair.Value.SetHighlight(state);
                // 経路表示と濃淡は排他。切り替えたときに前の表示が残らないよう、こちらで濃淡を解除する
                pair.Value.SetHeat(-1f);
            }
        }

        // 通過回数の集計を濃淡として反映する
        // aCounts : ノード ID ごとの通過回数
        public void ApplyHeatmap(IReadOnlyDictionary<string, int> aCounts)
        {
            int max = 0;
            foreach (var count in aCounts.Values)
            {
                if (count > max) max = count;
            }

            foreach (var pair in mNodeViews)
            {
                // 一度も通っていないノードが最も薄くなる
                int count = aCounts.TryGetValue(pair.Key, out int value) ? value : 0;
                pair.Value.SetHeat(max > 0 ? (float)count / max : 0f);
                // 濃淡と経路表示は排他。前に出していた経路の強調を解除する
                pair.Value.SetHighlight(PPUnitAITreeHighlight.None);
            }
        }

        // 強調表示・濃淡を解除して、種別ごとの色へ戻す
        public void ClearHighlight()
        {
            foreach (var view in mNodeViews.Values)
            {
                view.SetHighlight(PPUnitAITreeHighlight.None);
                view.SetHeat(-1f);
            }
        }

        // 指定ノードの表示を現在の値へ更新する
        // インスペクタでノード名や割り込み指定を編集した直後に呼び、グラフ側の表示を追従させる
        // aNodeId : 対象ノードの ID
        public void RefreshNodeView(string aNodeId)
        {
            if (!mNodeViews.TryGetValue(aNodeId ?? "", out var view)) return;

            view.RefreshView(view.Node.NodeId == mProfile.RootNodeId);
        }

        // ルートノードが画面中央へ来るように表示位置を合わせる
        // アセットを開いた直後はレイアウトが未確定のため、1 フレーム後に実行する
        public void FrameRootNode()
        {
            schedule.Execute(() => FrameNodeImmediate(mProfile.RootNodeId)).ExecuteLater(50);
        }

        // 指定したノードが画面中央へ来るように表示位置を合わせ、選択状態にする
        // 検索から呼ぶため、待たずにその場で動かす
        // aNodeId : 寄せるノードの ID
        public void FrameNode(string aNodeId)
        {
            if (!mNodeViews.TryGetValue(aNodeId ?? "", out var view)) return;

            ClearSelection();
            AddToSelection(view);
            FrameNodeImmediate(aNodeId);
        }

        // 指定したノードを画面中央へ寄せる
        // aNodeId : 寄せるノードの ID
        private void FrameNodeImmediate(string aNodeId)
        {
            if (!mNodeViews.TryGetValue(aNodeId ?? "", out var view)) return;

            var rect = view.GetPosition();
            // 生成直後はノードの大きさが未確定なため、その場合は左上を基準にして寄せる
            Vector2 center = rect.size == Vector2.zero ? rect.position : rect.center;
            Vector3 offset = new Vector3(layout.width * 0.5f - center.x, layout.height * 0.5f - center.y, 0f);
            UpdateViewTransform(offset, Vector3.one);
        }

        // グラフ全体を俯瞰するミニマップを左上へ置く
        private void SetupMiniMap()
        {
            var miniMap = new MiniMap { anchored = true };
            miniMap.SetPosition(new Rect(10f, 30f, 200f, 140f));
            Add(miniMap);
        }

        // ショートカット操作を受け取る
        // aEvent : キー入力
        private void OnKeyDown(KeyDownEvent aEvent)
        {
            if (aEvent.keyCode != KeyCode.F || !aEvent.actionKey) return;

            ShowNodeSearchWindow();
            aEvent.StopPropagation();
        }

        // ノードを名前で検索する窓を開く
        // 選ぶとそのノードへスクロールし、選択状態にする
        private void ShowNodeSearchWindow()
        {
            var provider = ScriptableObject.CreateInstance<PPUnitAITreeNodeSearchProvider>();
            provider.Setup(mProfile, FrameNode);

            var context = new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current?.mousePosition ?? Vector2.zero));
            SearchWindow.Open(context, provider);
        }

        // 根から幅優先でノードを並べ直す
        //
        // 優先度リストと連携ノードの子は、現在の並び順を保ったまま縦位置へ写す
        // 縦位置が優先度そのものなので、整列で順序を書き換えてはならない
        // 根から辿れないノードは触らず、その場に残す
        public void AutoLayout()
        {
            var root = mProfile.Root;
            if (root == null) return;

            Undo.RecordObject(mProfile, "ノードの自動整列");

            var placed = new HashSet<string>();
            // 深さごとに次へ置く縦位置。同じ深さのノードが重ならないようにするためのもの
            var nextY = new Dictionary<int, float>();
            PlaceNode(root, 0, 0f, placed, nextY);

            foreach (var pair in mNodeViews)
            {
                var position = pair.Value.Node.GraphPosition;
                pair.Value.SetPosition(new Rect(position, pair.Value.GetPosition().size));
            }
            ApplyChanges();
        }

        // ノードを 1 つ配置し、子を続けて配置する
        // aNode : 配置するノード
        // aDepth : 根からの深さ
        // aPreferredY : 置きたい縦位置。既に埋まっていれば下へずらす
        // aPlaced : 配置済みのノード ID。循環しても止まるように使う
        // aNextY : 深さごとの次に空いている縦位置
        // return : 実際に置いた縦位置
        private float PlaceNode(PPUnitAINode aNode, int aDepth, float aPreferredY,
            HashSet<string> aPlaced, Dictionary<int, float> aNextY)
        {
            if (!aPlaced.Add(aNode.NodeId)) return aPreferredY;

            aNextY.TryGetValue(aDepth, out float reserved);
            float y = Mathf.Max(aPreferredY, reserved);
            aNode.SetGraphPosition(new Vector2(aDepth * LayoutColumnWidth, y));
            aNextY[aDepth] = y + LayoutRowHeight;

            float childY = y;
            foreach (var port in aNode.Ports)
            {
                // 並び順は触らない。上にあるものほど優先、という規則をそのまま縦位置へ写す
                foreach (var childId in port.ChildIds)
                {
                    var child = mProfile.FindNode(childId);
                    if (child == null) continue;

                    childY = PlaceNode(child, aDepth + 1, childY, aPlaced, aNextY) + LayoutRowHeight;
                }
            }
            return y;
        }

        // アセットの現在の内容からグラフを組み立て直す
        public void Rebuild()
        {
            foreach (var view in mNodeViews.Values)
            {
                RemoveElement(view);
            }
            mNodeViews.Clear();
            foreach (var note in mNoteViews.Values)
            {
                RemoveElement(note);
            }
            mNoteViews.Clear();
            foreach (var edge in edges.ToList())
            {
                RemoveElement(edge);
            }

            foreach (var node in mProfile.Nodes)
            {
                if (node == null) continue;

                CreateNodeView(node);
            }
            foreach (var note in mProfile.Notes)
            {
                CreateNoteView(note);
            }
            ConnectAllEdges();
            OnGraphStructureChanged?.Invoke();
        }

        // 注記を 1 枚追加する
        // 画面の中央あたりへ置き、そのまま書き込めるようにする
        public void AddNote()
        {
            Undo.RecordObject(mProfile, "注記の追加");

            Vector2 center = contentViewContainer.WorldToLocal(layout.center);
            var data = new PPUnitAINoteData(Guid.NewGuid().ToString("N"),
                new Rect(center.x, center.y, 240f, 160f));

            var notesProperty = mSerializedObject.FindProperty("mNotes");
            notesProperty.arraySize++;
            notesProperty.GetArrayElementAtIndex(notesProperty.arraySize - 1).boxedValue = data;
            mSerializedObject.ApplyModifiedProperties();
            ApplyChanges();

            CreateNoteView(mProfile.FindNote(data.NoteId));
        }

        // 注記 1 枚分のビューを作ってグラフへ載せる
        // 見出し・本文・位置・大きさのいずれが変わってもアセットへ書き戻す
        // aData : 表示する注記。null なら何もしない
        private void CreateNoteView(PPUnitAINoteData aData)
        {
            if (aData == null) return;

            var note = new PPUnitAITreeNoteElement(aData);

            note.OnChanged += () => SaveNote(note);
            // 移動と大きさの変更は位置の変化としてしか拾えないため、そちらも見る
            note.RegisterCallback<GeometryChangedEvent>(_ => SaveNote(note));

            mNoteViews[aData.NoteId] = note;
            AddElement(note);
        }

        // 注記の現在の内容をアセットへ書き戻す
        // aNote : 対象の注記ビュー
        private void SaveNote(PPUnitAITreeNoteElement aNote)
        {
            var data = mProfile.FindNote(aNote.NoteId);
            if (data == null) return;

            var rect = aNote.GetPosition();
            if (data.Title == aNote.NoteTitle && data.Color == aNote.NoteColor && data.Rect == rect) return;

            data.SetTitle(aNote.NoteTitle);
            data.SetColor(aNote.NoteColor);
            data.SetRect(rect);
            EditorUtility.SetDirty(mProfile);
        }

        // 注記を 1 枚削除する
        // aNoteId : 削除する注記の ID
        private void RemoveNote(string aNoteId)
        {
            var notesProperty = mSerializedObject.FindProperty("mNotes");
            for (int i = 0; i < notesProperty.arraySize; i++)
            {
                if (notesProperty.GetArrayElementAtIndex(i).boxedValue is not PPUnitAINoteData note) continue;
                if (note.NoteId != aNoteId) continue;

                notesProperty.DeleteArrayElementAtIndex(i);
                break;
            }
            mSerializedObject.ApplyModifiedProperties();
            mNoteViews.Remove(aNoteId);
        }

        // ノード 1 つ分のビューを作ってグラフへ載せる
        // aNode : 表示するノード
        private void CreateNodeView(PPUnitAINode aNode)
        {
            var view = new PPUnitAITreeNodeView(aNode);
            view.RefreshView(aNode.NodeId == mProfile.RootNodeId);
            mNodeViews[aNode.NodeId] = view;
            AddElement(view);
        }

        // 全ノードの接続口を辿って接続線を張る
        private void ConnectAllEdges()
        {
            foreach (var view in mNodeViews.Values)
            {
                var ports = view.Node.Ports;
                for (int i = 0; i < ports.Count && i < view.OutputPorts.Count; i++)
                {
                    foreach (var childId in ports[i].ChildIds)
                    {
                        if (!mNodeViews.TryGetValue(childId ?? "", out var childView)) continue;

                        var edge = view.OutputPorts[i].ConnectTo(childView.InputPort);
                        AddElement(edge);
                    }
                }
            }
        }

        // グラフ上の操作をアセットへ反映する
        // 接続・切断・削除・移動をまとめて受け取るため、Undo の記録もここで 1 回だけ行う
        // aChange : GraphView が通知する変更内容
        // return : 受け取った変更内容をそのまま返す
        private GraphViewChange OnGraphViewChanged(GraphViewChange aChange)
        {
            bool isStructureChanged = false;
            Undo.RecordObject(mProfile, "判断ツリーの編集");

            if (aChange.elementsToRemove != null)
            {
                foreach (var element in aChange.elementsToRemove)
                {
                    if (element is Edge edge) DisconnectEdge(edge);
                    else if (element is PPUnitAITreeNodeView nodeView) RemoveNode(nodeView.Node);
                    else if (element is PPUnitAITreeNoteElement note) RemoveNote(note.NoteId);
                    isStructureChanged = true;
                }
            }

            if (aChange.edgesToCreate != null)
            {
                foreach (var edge in aChange.edgesToCreate)
                {
                    ConnectEdge(edge);
                    isStructureChanged = true;
                }
            }

            if (aChange.movedElements != null)
            {
                foreach (var element in aChange.movedElements.OfType<PPUnitAITreeNodeView>())
                {
                    element.Node.SetGraphPosition(element.GetPosition().position);
                }
                // 縦位置が優先度になるため、移動しただけでも並び順を取り直す
                ReorderAllSelectors();
            }

            if (isStructureChanged)
            {
                ReorderAllSelectors();
            }

            ApplyChanges();
            if (isStructureChanged) OnGraphStructureChanged?.Invoke();
            return aChange;
        }

        // 接続線 1 本を実データへ反映する
        // aEdge : 張られた接続線
        private void ConnectEdge(Edge aEdge)
        {
            if (aEdge.output?.node is not PPUnitAITreeNodeView parent) return;
            if (aEdge.input?.node is not PPUnitAITreeNodeView child) return;

            int portIndex = parent.OutputPorts.ToList().IndexOf(aEdge.output);
            if (portIndex < 0) return;

            parent.Node.ConnectChild(portIndex, child.Node.NodeId);
        }

        // 接続線 1 本の切断を実データへ反映する
        // aEdge : 外された接続線
        private void DisconnectEdge(Edge aEdge)
        {
            if (aEdge.output?.node is not PPUnitAITreeNodeView parent) return;
            if (aEdge.input?.node is not PPUnitAITreeNodeView child) return;

            int portIndex = parent.OutputPorts.ToList().IndexOf(aEdge.output);
            if (portIndex < 0) return;

            parent.Node.DisconnectChild(portIndex, child.Node.NodeId);
        }

        // ポートから空白領域へドロップされた際に、ノード追加メニューを出す
        // 選ばれた型のノードをその場に作り、ドラッグ元のポートと繋ぐ
        // aSourcePort : ドラッグ元のポート
        // aPosition : ドロップ位置(ワールド座標)
        public void ShowNodeCreationMenuForDrop(Port aSourcePort, Vector2 aPosition)
        {
            bool isFromOutput = aSourcePort.direction == Direction.Output;
            Vector2 graphPosition = contentViewContainer.WorldToLocal(aPosition);

            var menu = new GenericMenu();
            foreach (var (type, displayName) in EnumerateNodeTypes())
            {
                menu.AddItem(new GUIContent(displayName), false,
                    () => AddNodeAndConnect(type, graphPosition, aSourcePort, isFromOutput));
            }
            menu.ShowAsContext();
        }

        // 新規ノードを生成し、ドラッグ元ポートと接続する
        // aNodeType : 追加するノードの型
        // aPosition : グラフ上の配置
        // aSourcePort : ドラッグ元のポート
        // aIsFromOutput : true ならドラッグ元が出力ポート(新規ノードが子になる)、false なら入力ポート(新規ノードが親になる)
        private void AddNodeAndConnect(Type aNodeType, Vector2 aPosition, Port aSourcePort, bool aIsFromOutput)
        {
            var newNode = AddNode(aNodeType, aPosition);
            if (newNode == null || !mNodeViews.TryGetValue(newNode.NodeId, out var newNodeView)) return;

            // 新規ノードが親になる場合、繋ぐ先は最初の接続口にする
            Port newNodePort = aIsFromOutput
                ? newNodeView.InputPort
                : newNodeView.OutputPorts.FirstOrDefault();
            if (newNodePort == null) return;

            Port outputPort = aIsFromOutput ? aSourcePort : newNodePort;
            Port inputPort = aIsFromOutput ? newNodePort : aSourcePort;

            var edge = outputPort.ConnectTo(inputPort);
            AddElement(edge);
            ConnectEdge(edge);
            ReorderAllSelectors();
            ApplyChanges();
            OnGraphStructureChanged?.Invoke();
        }

        // 選択中のノードを複写用の文字列にする
        // Ctrl+C と Ctrl+D の両方からここを通る
        // aElements : 選択されているグラフ要素。接続線も混ざるためノードだけを拾う
        // return : 複写に使う文字列
        private string SerializeSelection(IEnumerable<GraphElement> aElements)
        {
            var nodes = aElements.OfType<PPUnitAITreeNodeView>().Select(v => v.Node).ToList();
            return nodes.Count == 0 ? "" : PPUnitAITreeClipboard.Serialize(nodes);
        }

        // 複写した文字列からノードを貼り付ける
        //
        // 貼り付けたノードには新しい ID を振り、複写元同士で繋がっていた接続だけを張り直す
        // 複写の範囲外へ出ていた接続は未接続になる（貼り付けた側から元のノードへ線を伸ばさない）
        // サブツリー参照の参照先はアセットへの参照で ID 参照ではないため、そのまま保たれる
        //
        // aOperationName : 操作名。Undo の表示に使う
        // aData : SerializeSelection が書き出した文字列
        private void PasteSerializedData(string aOperationName, string aData)
        {
            var pasted = PPUnitAITreeClipboard.Deserialize(aData);
            if (pasted.Count == 0) return;

            Undo.RecordObject(mProfile, aOperationName);

            // 先に全ノードの ID を振り直し、複写元の ID との対応表を作る
            var idMap = new Dictionary<string, string>();
            foreach (var node in pasted)
            {
                string oldId = node.NodeId;
                node.ReassignNodeId();
                if (!string.IsNullOrEmpty(oldId)) idMap[oldId] = node.NodeId;
            }

            var nodesProperty = mSerializedObject.FindProperty("mNodes");
            foreach (var node in pasted)
            {
                node.RemapChildIds(idMap);
                // 貼り付け元と重ならないよう少しずらして置く
                node.SetGraphPosition(node.GraphPosition + PasteOffset);

                nodesProperty.arraySize++;
                nodesProperty.GetArrayElementAtIndex(nodesProperty.arraySize - 1).managedReferenceValue = node;
            }
            mSerializedObject.ApplyModifiedProperties();
            ApplyChanges();

            // 接続が張り替わっているため、線を引き直すには組み立て直すのが確実
            Rebuild();
            SelectNodes(pasted);
        }

        // 貼り付けたノードを選択状態にする
        // 続けて動かしたり消したりできるようにするためのもの
        // aNodes : 選択するノード
        private void SelectNodes(IReadOnlyList<PPUnitAINode> aNodes)
        {
            ClearSelection();
            foreach (var node in aNodes)
            {
                if (mNodeViews.TryGetValue(node.NodeId, out var view)) AddToSelection(view);
            }
        }

        // ノードを 1 つ追加する
        // aType : 追加するノードの型
        // aPosition : グラフ上の配置
        // return : 追加されたノード
        private PPUnitAINode AddNode(Type aType, Vector2 aPosition)
        {
            Undo.RecordObject(mProfile, "ノードの追加");

            var node = (PPUnitAINode)Activator.CreateInstance(aType);
            node.EnsureNodeId();
            node.SetGraphPosition(aPosition);

            var nodesProperty = mSerializedObject.FindProperty("mNodes");
            nodesProperty.arraySize++;
            nodesProperty.GetArrayElementAtIndex(nodesProperty.arraySize - 1).managedReferenceValue = node;
            mSerializedObject.ApplyModifiedProperties();
            mProfile.InvalidateNodeMap();

            // 最初の 1 つは自動的にルートにしておく。ルート未設定のまま気付かないのを防ぐ
            if (string.IsNullOrEmpty(mProfile.RootNodeId))
            {
                SetRootProperty(node.NodeId);
            }

            CreateNodeView(node);
            OnGraphStructureChanged?.Invoke();
            return node;
        }

        // ノードを 1 つ削除し、他のノードから張られていた接続も外す
        // aNode : 削除するノード
        private void RemoveNode(PPUnitAINode aNode)
        {
            foreach (var other in mProfile.Nodes)
            {
                if (other == null || ReferenceEquals(other, aNode)) continue;

                var ports = other.Ports;
                for (int i = 0; i < ports.Count; i++)
                {
                    other.DisconnectChild(i, aNode.NodeId);
                }
            }

            var nodesProperty = mSerializedObject.FindProperty("mNodes");
            for (int i = 0; i < nodesProperty.arraySize; i++)
            {
                if (nodesProperty.GetArrayElementAtIndex(i).managedReferenceValue is not PPUnitAINode node) continue;
                if (!ReferenceEquals(node, aNode)) continue;

                nodesProperty.DeleteArrayElementAtIndex(i);
                break;
            }
            mSerializedObject.ApplyModifiedProperties();
            mProfile.InvalidateNodeMap();

            mNodeViews.Remove(aNode.NodeId);
            if (mProfile.RootNodeId == aNode.NodeId)
            {
                SetRootProperty("");
            }
        }

        // 指定ノードをルートにする
        // aNode : ルートにするノード
        private void SetRoot(PPUnitAINode aNode)
        {
            Undo.RecordObject(mProfile, "ルートノードの変更");
            SetRootProperty(aNode.NodeId);

            foreach (var view in mNodeViews.Values)
            {
                view.RefreshView(view.Node.NodeId == mProfile.RootNodeId);
            }
            OnGraphStructureChanged?.Invoke();
        }

        // ルートノード ID を書き込む
        // aNodeId : 設定するノード ID
        private void SetRootProperty(string aNodeId)
        {
            mSerializedObject.FindProperty("mRootNodeId").stringValue = aNodeId;
            mSerializedObject.ApplyModifiedProperties();
        }

        // 全ての優先度リストについて、子の並び順をグラフ上の縦位置へ揃える
        // 接続線の順序は見た目に出ないため、上にあるものほど優先という規則で読めるようにする
        private void ReorderAllSelectors()
        {
            foreach (var view in mNodeViews.Values)
            {
                var ports = view.Node.Ports;
                for (int i = 0; i < ports.Count; i++)
                {
                    if (!ports[i].IsMultiple) continue;

                    var ordered = ports[i].ChildIds
                        .Where(id => mNodeViews.ContainsKey(id ?? ""))
                        .OrderBy(id => mNodeViews[id].GetPosition().position.y)
                        .ToList();
                    view.Node.ReorderChildren(i, ordered);
                }
            }
        }

        // 変更をアセットへ書き戻し、保存対象として記録する
        private void ApplyChanges()
        {
            mSerializedObject.ApplyModifiedProperties();
            mProfile.InvalidateNodeMap();
            EditorUtility.SetDirty(mProfile);
        }

        // 選択が変わった際に通知する。インスペクタ側の表示切り替えに使う
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

        // 現在の選択内容を通知する
        private void NotifySelection()
            => OnNodeSelectionChanged?.Invoke(selection.OfType<PPUnitAITreeNodeView>().FirstOrDefault()?.Node);

        // 追加できるノード型を、表示名付きで列挙する
        // PPTypeMenuName が付いていない型はメニューに出さない（付け忘れの検出も兼ねる）
        // return : ノード型と表示名の組
        private static IEnumerable<(Type Type, string DisplayName)> EnumerateNodeTypes()
        {
            foreach (var type in TypeCache.GetTypesDerivedFrom<PPUnitAINode>())
            {
                if (type.IsAbstract) continue;

                var attribute = type.GetCustomAttribute<PPTypeMenuNameAttribute>();
                if (attribute == null) continue;

                yield return (type, attribute.Path);
            }
        }
    }
}
