/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPTypeTreePicker.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 型をツリーから選ぶピッカー（ポップアップ・ツリービュー・managed reference 用ユーティリティ）
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace PPCore
{
    // [SerializeReference] な型を選ばせるためのポップアップ。検索欄付きのツリービューを表示する
    // 選ばれた型をそのままコールバックへ渡すだけで、インスタンスの生成は呼び出し側に任せる
    // スキルエフェクトのように選択結果をラップしてから使いたいケースがあるため、
    // 生成方法をここに固定しない
    public sealed class PPTypeTreePickerPopup : PopupWindowContent
    {
        // 型が選ばれたときに呼ぶコールバック
        private readonly Action<Type> mOnSelected;
        // 選択候補を表示するツリービュー
        private readonly PPTypeTreePickerTreeView mTreeView;
        // ツリーの展開状態などを保持する状態オブジェクト
        private readonly TreeViewState<int> mTreeViewState = new();
        // ツリーの絞り込み用検索欄
        private readonly SearchField mSearchField = new();

        // aCandidateTypes : ツリーに並べる候補の型
        // aEmptyMessage : 候補が 1 つも無い場合に表示する文言
        // aOnSelected : 型が選ばれたときに呼ぶコールバック
        private PPTypeTreePickerPopup(IReadOnlyList<Type> aCandidateTypes, string aEmptyMessage, Action<Type> aOnSelected)
        {
            mOnSelected = aOnSelected;
            mTreeView = new PPTypeTreePickerTreeView(mTreeViewState, aCandidateTypes, aEmptyMessage, OnTypePicked);
            mTreeView.ExpandAll();
        }

        // ポップアップを表示する
        // aActivatorRect : ポップアップを出す基準の矩形
        // aCandidateTypes : ツリーに並べる候補の型
        // aEmptyMessage : 候補が 1 つも無い場合に表示する文言
        // aOnSelected : 型が選ばれたときに呼ぶコールバック
        public static void Show(Rect aActivatorRect, IReadOnlyList<Type> aCandidateTypes,
            string aEmptyMessage, Action<Type> aOnSelected)
            => PopupWindow.Show(aActivatorRect, new PPTypeTreePickerPopup(aCandidateTypes, aEmptyMessage, aOnSelected));

        // 基底型の具象派生型をそのまま候補にしてポップアップを表示する
        // 選ばれた型のインスタンスを生成してコールバックへ渡す、最も一般的な使い方の入口
        // aActivatorRect : ポップアップを出す基準の矩形
        // aEmptyMessage : 候補が 1 つも無い場合に表示する文言
        // aOnCreated : 生成されたインスタンスを受け取るコールバック
        public static void ShowDerived<T>(Rect aActivatorRect, string aEmptyMessage, Action<T> aOnCreated)
            where T : class
            => Show(aActivatorRect, PPTypeTreePickerTreeView.CollectDerived<T>(), aEmptyMessage,
                type => aOnCreated?.Invoke((T)Activator.CreateInstance(type)));

        // ポップアップのサイズ
        public override Vector2 GetWindowSize() => new Vector2(320f, 360f);

        // 上部に検索欄、その下にツリービューを配置して描画する
        // aRect : 描画領域
        public override void OnGUI(Rect aRect)
        {
            const float searchHeight = 20f;
            var searchRect = new Rect(aRect.x + 4f, aRect.y + 4f, aRect.width - 8f, searchHeight);
            var treeRect   = new Rect(aRect.x, aRect.y + searchHeight + 6f, aRect.width, aRect.height - searchHeight - 6f);

            mTreeView.searchString = mSearchField.OnGUI(searchRect, mTreeView.searchString);
            mTreeView.OnGUI(treeRect);
        }

        // 型が選ばれたときの処理。コールバックへ渡してポップアップを閉じる
        // aType : 選ばれた型
        private void OnTypePicked(Type aType)
        {
            mOnSelected?.Invoke(aType);
            editorWindow.Close();
        }
    }

    // 渡された型の一覧をツリーに表示する汎用ビュー
    // 階層は PPTypeMenuNameAttribute の Path から組み立てる
    // 条件クラス・スキルエフェクト・戦術ステップのように [SerializeReference] で
    // 型を選ばせる場面が増えたため、基底型ごとに同じ実装を持つのをやめてここへ寄せている
    // アセットを生成しない方式のため、既存アセットの列挙は行わない
    internal sealed class PPTypeTreePickerTreeView : TreeView<int>
    {
        // ツリーに並べる候補の型
        private readonly IReadOnlyList<Type> mCandidateTypes;
        // 候補が 1 つも無い場合に表示する文言
        private readonly string mEmptyMessage;
        // 型が選ばれたときのコールバック
        private readonly Action<Type> mOnPickType;
        // ノード ID から型への対応
        private readonly Dictionary<int, Type> mIdToType = new();
        // フォルダノードに割り当てる次の ID。葉と衝突しないよう負方向へ進める
        private int mNextFolderId = -2;

        // aState : ツリーの展開状態を保持する状態オブジェクト
        // aCandidateTypes : ツリーに並べる候補の型
        // aEmptyMessage : 候補が 1 つも無い場合に表示する文言
        // aOnPickType : 型が選ばれたときのコールバック
        public PPTypeTreePickerTreeView(TreeViewState<int> aState, IReadOnlyList<Type> aCandidateTypes,
            string aEmptyMessage, Action<Type> aOnPickType) : base(aState)
        {
            mCandidateTypes = aCandidateTypes;
            mEmptyMessage = aEmptyMessage;
            mOnPickType = aOnPickType;
            showAlternatingRowBackgrounds = true;
            Reload();
        }

        // 基底型の具象派生型を集める
        // aIsSkipUnattributed : true なら PPTypeMenuNameAttribute の無い型を候補から除外する
        //                       内部用ラッパーのようにツリーへ出したくない型がある場合に使う
        //                       false の場合は「未分類/型名」として並べ、属性の付け忘れに気付けるようにする
        // return : 候補として並べる型のリスト
        public static List<Type> CollectDerived<T>(bool aIsSkipUnattributed = false)
        {
            var list = new List<Type>();
            AppendDerived<T>(list, aIsSkipUnattributed);
            return list;
        }

        // 既存のリストへ基底型の具象派生型を追記する
        // 異なる型階層を 1 本のツリーへまとめたい場合に複数回呼ぶ
        // aList : 追記先のリスト
        // aIsSkipUnattributed : PPTypeMenuNameAttribute の無い型を除外するか
        public static void AppendDerived<T>(List<Type> aList, bool aIsSkipUnattributed = false)
        {
            foreach (var type in TypeCache.GetTypesDerivedFrom<T>())
            {
                if (type.IsAbstract) continue;
                if (aIsSkipUnattributed && type.GetCustomAttribute<PPTypeMenuNameAttribute>() == null) continue;
                aList.Add(type);
            }
        }

        // ツリーを構築する
        // 候補の型を走査し、PPTypeMenuNameAttribute.Path に沿ってフォルダノードを掘りながら葉を追加する
        // return : 構築されたルートノード
        protected override TreeViewItem<int> BuildRoot()
        {
            var root = new TreeViewItem<int>(-1, -1, "root");
            var folders = new Dictionary<string, TreeViewItem<int>>();
            mIdToType.Clear();
            int nextLeafId = 0;

            foreach (var type in mCandidateTypes)
            {
                var menu = type.GetCustomAttribute<PPTypeMenuNameAttribute>();
                string path = menu != null ? menu.Path : $"未分類/{type.Name}";
                string[] segments = path.Split('/');

                // 末尾の要素は葉になるため、その手前までをフォルダとして辿る
                TreeViewItem<int> parent = root;
                string accum = "";
                for (int i = 0; i < segments.Length - 1; i++)
                {
                    accum = i == 0 ? segments[i] : $"{accum}/{segments[i]}";
                    if (!folders.TryGetValue(accum, out var folder))
                    {
                        folder = new TreeViewItem<int>(mNextFolderId--, 0, segments[i])
                        {
                            icon = LoadIcon("Folder Icon")
                        };
                        parent.AddChild(folder);
                        folders[accum] = folder;
                    }
                    parent = folder;
                }

                int leafId = nextLeafId++;
                var leaf = new TreeViewItem<int>(leafId, 0, segments[^1]);
                parent.AddChild(leaf);
                mIdToType[leafId] = type;
            }

            // TreeView は子が 1 つも無いと例外になるため、ダミーを入れておく
            if (!root.hasChildren)
                root.AddChild(new TreeViewItem<int>(0, 0, mEmptyMessage));

            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        // ダブルクリックで選択を確定する
        // aId : ダブルクリックされたノードの ID
        protected override void DoubleClickedItem(int aId)
        {
            if (mIdToType.TryGetValue(aId, out var type))
                mOnPickType?.Invoke(type);
        }

        // エディタ組み込みアイコンを名前で取得する
        // aName : アイコン名
        // return : 取得したテクスチャ。見つからなければ null
        private static Texture2D LoadIcon(string aName)
        {
            var content = EditorGUIUtility.IconContent(aName);
            return content != null ? content.image as Texture2D : null;
        }
    }

    // [SerializeReference] フィールドの「型未選択なら選択ボタン、選択済みならフィールド展開」を共通化する
    // aBaseType の具象派生型を TypeCache から集め、GenericMenu で選ばせる
    // 選択時は Activator.CreateInstance でインスタンスを生成しその場で managedReferenceValue に設定する
    public static class PPManagedReferencePickerUtility
    {
        // aPosition : 描画領域
        // aProperty : 描画対象の SerializeReference プロパティ
        // aLabel : フィールドのラベル
        // aBaseType : 選択候補を絞り込む基底型
        public static void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel, Type aBaseType)
        {
            if (string.IsNullOrEmpty(aProperty.managedReferenceFullTypename))
            {
                var buttonRect = new Rect(aPosition.x, aPosition.y, aPosition.width, EditorGUIUtility.singleLineHeight);
                if (GUI.Button(buttonRect, $"+ {aLabel.text} を選択"))
                {
                    ShowTypeMenu(aProperty, aBaseType);
                }
                return;
            }

            DrawAssignedField(aPosition, aProperty, aLabel);
        }

        // aProperty : 高さを求める対象の SerializeReference プロパティ
        public static float GetPropertyHeight(SerializedProperty aProperty)
            => string.IsNullOrEmpty(aProperty.managedReferenceFullTypename)
                ? EditorGUIUtility.singleLineHeight
                : EditorGUI.GetPropertyHeight(aProperty, true);

        // 選択済みの SerializeReference プロパティを、型ごとの要約をラベルにして描画する
        // ツリーピッカー（PPTypeTreePickerPopup）経由で設定された場合の描画にも共通で使う
        // aPosition : 描画領域
        // aProperty : 描画対象の SerializeReference プロパティ（型選択済み）
        // aLabel : フィールドの既定ラベル
        public static void DrawAssignedField(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            var label = new GUIContent(aLabel);
            switch (aProperty.managedReferenceValue)
            {
                case PPSkillEffectDefinition skillEffect: label.text = skillEffect.BuildString(); break;
                case PPStatusEffectBehaviourDefinition behaviour: label.text = behaviour.BuildString(); break;
                case PPPartyConditionValidator condition when !string.IsNullOrEmpty(condition.Description):
                    label.text = condition.Description; break;
                case PPUnitConditionValidator unitCondition when !string.IsNullOrEmpty(unitCondition.Description):
                    label.text = unitCondition.Description; break;
                case PPUnitAINode node: label.text = node.NodeName; break;
                case PPUnitAIActionBase action: label.text = action.ActionName; break;
            }
            EditorGUI.PropertyField(aPosition, aProperty, label, true);
        }

        // aBaseType の具象派生型一覧をメニュー表示し、選ばれた型のインスタンスを aProperty に設定する
        // aProperty : 設定先の SerializeReference プロパティ
        // aBaseType : 選択候補を絞り込む基底型
        private static void ShowTypeMenu(SerializedProperty aProperty, Type aBaseType)
        {
            var menu = new GenericMenu();
            foreach (var type in TypeCache.GetTypesDerivedFrom(aBaseType))
            {
                if (type.IsAbstract) continue;

                var path = type.GetCustomAttribute<PPTypeMenuNameAttribute>()?.Path
                                   ?? ObjectNames.NicifyVariableName(type.Name);

                menu.AddItem(new GUIContent(path), false, () =>
                {
                    aProperty.managedReferenceValue = Activator.CreateInstance(type);
                    aProperty.serializedObject.ApplyModifiedProperties();
                });
            }
            menu.ShowAsContext();
        }
    }
}
