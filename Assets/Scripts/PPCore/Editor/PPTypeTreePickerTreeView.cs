/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTypeTreePickerTreeView.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 型をカテゴリ別に表示する折りたたみツリー(汎用)
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace PPCore
{
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
}
