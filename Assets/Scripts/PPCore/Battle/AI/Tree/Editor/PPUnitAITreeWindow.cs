/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAITreeWindow.cs
 * @author hqrse
 * @date 2026/08/25
 * @brief 判断ツリーをノードグラフとして編集する専用ウィンドウ
 * =====================================*/

using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PPCore
{
    // PPUnitAIProfileDefinition をノードグラフとして編集するウィンドウ
    // 左にグラフ、右に選択中ノードのインスペクタを並べた構成
    // グラフ側はノードの配置と接続だけを扱い、条件や行動の中身は右側のインスペクタで編集する
    public sealed class PPUnitAITreeWindow : EditorWindow
    {
        // インスペクタ幅を保存する EditorPrefs キー(ウィンドウを閉じても復元される)
        private const string PrefKeyInspectorWidth = "PPCore.PPUnitAITreeWindow.InspectorWidth";
        // 最後に開いていたアセットを保存する EditorPrefs キー
        // ウィンドウを閉じて開き直しても同じツリーへ戻れるようにするためのもの
        private const string PrefKeyLastAsset = "PPCore.PPUnitAITreeWindow.LastAssetGuid";
        private const float DefaultInspectorWidth = 360f;

        // 編集対象。ドメインリロードをまたいで保持したいのでシリアライズ対象にする
        [SerializeField] private PPUnitAIProfileDefinition mTarget;
        private SerializedObject mSerializedObject;
        private PPUnitAITreeGraphView mGraphView;
        private VisualElement mInspectorContainer;
        private HelpBox mRootWarningBox;
        private float mCurrentInspectorWidth;

        [MenuItem("Window/Unit AI Tree")]
        public static void Open()
        {
            var window = GetWindow<PPUnitAITreeWindow>();
            window.titleContent = new GUIContent("Unit AI Tree");
            window.minSize = new Vector2(720, 420);
            window.Show();
        }

        // Project ウィンドウで PPUnitAIProfileDefinition をダブルクリックした際にこのウィンドウで開く
        // aInstanceId : 開かれたアセットのインスタンス ID
        // aLine : 行番号（テキストアセット用。ここでは使わない）
        // return : このウィンドウで処理した場合 true
        [OnOpenAsset]
        public static bool OnOpenAsset(int aInstanceId, int aLine)
        {
            if (EditorUtility.EntityIdToObject(aInstanceId) is not PPUnitAIProfileDefinition asset)
            {
                return false;
            }

            var window = GetWindow<PPUnitAITreeWindow>();
            window.titleContent = new GUIContent("Unit AI Tree");
            window.SetTarget(asset);
            window.Show();
            return true;
        }

        private void OnEnable()
        {
            mCurrentInspectorWidth = EditorPrefs.GetFloat(PrefKeyInspectorWidth, DefaultInspectorWidth);
            Selection.selectionChanged += HandleSelectionChanged;
            // 閉じて開き直した場合は、前回編集していたツリーへ戻す
            mTarget ??= LoadLastTarget();
            RebuildUI();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= HandleSelectionChanged;
            EditorPrefs.SetFloat(PrefKeyInspectorWidth, mCurrentInspectorWidth);
        }

        // 編集対象を差し替えてグラフを組み立て直す
        // aTarget : 編集する判断ツリー
        public void SetTarget(PPUnitAIProfileDefinition aTarget)
        {
            mTarget = aTarget;
            SaveLastTarget();
            RebuildUI();
        }

        // 最後に開いていたアセットを覚えておく
        private void SaveLastTarget()
        {
            string path = mTarget == null ? "" : AssetDatabase.GetAssetPath(mTarget);
            EditorPrefs.SetString(PrefKeyLastAsset, string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path));
        }

        // 最後に開いていたアセットを読み直す
        // return : 復元できたアセット。記録が無い・削除済みなら null
        private static PPUnitAIProfileDefinition LoadLastTarget()
        {
            string guid = EditorPrefs.GetString(PrefKeyLastAsset, "");
            if (string.IsNullOrEmpty(guid)) return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<PPUnitAIProfileDefinition>(path);
        }

        // Project ウィンドウで別のプロファイルを選んだら、そちらへ追従する
        private void HandleSelectionChanged()
        {
            if (Selection.activeObject is not PPUnitAIProfileDefinition profile) return;
            if (ReferenceEquals(profile, mTarget)) return;

            SetTarget(profile);
        }

        // ウィンドウの中身を組み立て直す
        private void RebuildUI()
        {
            rootVisualElement.Clear();

            if (mTarget == null)
            {
                rootVisualElement.Add(new HelpBox(
                    "編集する PPUnitAIProfileDefinition を Project ウィンドウで選択してください。", HelpBoxMessageType.Info));
                return;
            }

            // 手で追加されたノードなど、ID 未採番のものをここで埋めておく
            mTarget.EnsureNodeIds();
            mSerializedObject = new SerializedObject(mTarget);

            var header = new Label(mTarget.name) { style = { unityFontStyleAndWeight = FontStyle.Bold } };
            header.style.paddingLeft = 6f;
            header.style.paddingTop = 4f;
            rootVisualElement.Add(header);

            mRootWarningBox = new HelpBox(
                "ルートノードが設定されていません。ノードを右クリックして「このノードをルートにする」を選んでください。",
                HelpBoxMessageType.Warning);
            rootVisualElement.Add(mRootWarningBox);

            var split = new TwoPaneSplitView(1, mCurrentInspectorWidth, TwoPaneSplitViewOrientation.Horizontal);
            rootVisualElement.Add(split);

            mGraphView = new PPUnitAITreeGraphView(mSerializedObject, mTarget) { name = "UnitAITreeGraph" };
            mGraphView.style.flexGrow = 1f;
            mGraphView.OnNodeSelectionChanged += ShowNodeInspector;
            mGraphView.OnGraphStructureChanged += RefreshWarnings;
            split.Add(mGraphView);

            mInspectorContainer = new ScrollView { style = { flexGrow = 1f } };
            split.Add(mInspectorContainer);

            // ドラッグでの幅変更を追いかけて、閉じるときに保存できるようにする
            split.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (split.childCount > 1) mCurrentInspectorWidth = split[1].resolvedStyle.width;
            });

            ShowNodeInspector(null);
            RefreshWarnings();
            // 開いた直後にどこを見ているか分からなくならないよう、ルートを画面中央へ寄せる
            mGraphView.FrameRootNode();
        }

        // 選択中ノードのインスペクタを右側へ表示する
        // aNode : 選択されたノード。未選択なら null
        private void ShowNodeInspector(PPUnitAINode aNode)
        {
            mInspectorContainer.Clear();

            if (aNode == null)
            {
                mInspectorContainer.Add(new Label("ノードを選択すると、ここで条件と行動を編集できます。")
                {
                    style = { paddingLeft = 8f, paddingTop = 8f, whiteSpace = WhiteSpace.Normal },
                });
                return;
            }

            var property = FindNodeProperty(aNode);
            if (property == null) return;

            // 選ぶたびに畳まれていると毎回開き直す手間になるため、最初から展開しておく
            property.isExpanded = true;

            // IMGUI 側の PropertyDrawer（条件・行動の型ピッカー）をそのまま使いたいので IMGUIContainer で描く
            var container = new IMGUIContainer(() =>
            {
                if (mSerializedObject == null || mSerializedObject.targetObject == null) return;

                mSerializedObject.Update();
                EditorGUILayout.PropertyField(property, new GUIContent(aNode.NodeName), true);
                if (mSerializedObject.ApplyModifiedProperties())
                {
                    mTarget.InvalidateNodeMap();
                    // ノード名や割り込み指定を書き換えた場合に、グラフ側の表示を即座に追従させる
                    mGraphView.RefreshNodeView(aNode.NodeId);
                }

                DrawNodeTools(aNode);
            });
            container.style.paddingLeft = 6f;
            container.style.paddingRight = 6f;
            container.style.paddingTop = 6f;
            mInspectorContainer.Add(container);
        }

        // ノード種別ごとの補助操作を描く
        // 標準のフィールド編集では届かない、まとめて書き換える系の操作を置く
        // aNode : 表示中のノード
        private void DrawNodeTools(PPUnitAINode aNode)
        {
            if (aNode is not PPUnitAILotteryNode lottery) return;

            EditorGUILayout.Space();
            if (!GUILayout.Button("重みを正規化（0〜1）")) return;

            // 比率は変わらないため挙動は同じだが、数字がそのまま確率として読めるようになる
            Undo.RecordObject(mTarget, "抽選の重みを正規化");
            lottery.NormalizeWeights();
            EditorUtility.SetDirty(mTarget);
            mSerializedObject.Update();
        }

        // ノードに対応する SerializedProperty を引く
        // aNode : 対象ノード
        // return : 該当する配列要素のプロパティ。見つからなければ null
        private SerializedProperty FindNodeProperty(PPUnitAINode aNode)
        {
            var nodesProperty = mSerializedObject.FindProperty("mNodes");
            for (int i = 0; i < nodesProperty.arraySize; i++)
            {
                var element = nodesProperty.GetArrayElementAtIndex(i);
                if (element.managedReferenceValue is PPUnitAINode node && ReferenceEquals(node, aNode))
                {
                    return element;
                }
            }
            return null;
        }

        // ルート未設定などの警告表示を更新する
        private void RefreshWarnings()
        {
            if (mRootWarningBox == null || mTarget == null) return;

            bool hasRoot = mTarget.Root != null;
            mRootWarningBox.style.display = hasRoot ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
