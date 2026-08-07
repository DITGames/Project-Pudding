/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillEffectPickerTreeView.cs
 * @author hqrse
 * @date 2026/08/07
 * @brief SkillEffect / StatusEffect の型をカテゴリ別に表示する折りたたみツリー
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace PPCore
{
    // PPSkillEffectDefinition 派生と PPEffectDefinition 派生をまとめて 1 本のツリーに表示する
    // 階層は PPTypeMenuNameAttribute の Path から組み立てる（PPConditionTreeView と同じ考え方）
    // アセットを生成しない方式のため、PPConditionTreeView と異なり既存アセットの列挙は行わない
    internal sealed class PPSkillEffectPickerTreeView : TreeView<int>
    {
        // 型が選ばれたときのコールバック
        private readonly Action<Type> mOnPickType;
        // ノード ID から型への対応
        private readonly Dictionary<int, Type> mIdToType = new();
        // フォルダノードに割り当てる次の ID。葉と衝突しないよう負方向へ進める
        private int mNextFolderId = -2;

        // aState : ツリーの展開状態を保持する状態オブジェクト
        // aOnPickType : 型が選ばれたときのコールバック
        public PPSkillEffectPickerTreeView(TreeViewState<int> aState, Action<Type> aOnPickType) : base(aState)
        {
            mOnPickType = aOnPickType;
            showAlternatingRowBackgrounds = true;
            Reload();
        }

        // ツリーを構築する
        // PPSkillEffectDefinition 派生と PPEffectDefinition 派生を走査し、
        // PPTypeMenuNameAttribute.Path に沿ってフォルダノードを掘りながら葉を追加する
        // return : 構築されたルートノード
        protected override TreeViewItem<int> BuildRoot()
        {
            var root = new TreeViewItem<int>(-1, -1, "root");
            var folders = new Dictionary<string, TreeViewItem<int>>();
            mIdToType.Clear();
            int nextLeafId = 0;

            foreach (var type in CollectCandidateTypes())
            {
                var menu = type.GetCustomAttribute<PPTypeMenuNameAttribute>();
                if (menu == null) continue; // 属性の無い型（内部用ラッパー等）はツリーに出さない

                string[] segments = menu.Path.Split('/');

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
                root.AddChild(new TreeViewItem<int>(0, 0, "(エフェクトが見つかりません)"));

            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        // PPSkillEffectDefinition 派生と PPEffectDefinition 派生をまとめて列挙する
        // 2 つの異なる型階層を 1 本のツリーに統合するための橋渡し
        private static IEnumerable<Type> CollectCandidateTypes()
        {
            foreach (var t in TypeCache.GetTypesDerivedFrom<PPSkillEffectDefinition>())
                if (!t.IsAbstract) yield return t;
            foreach (var t in TypeCache.GetTypesDerivedFrom<PPEffectDefinition>())
                if (!t.IsAbstract) yield return t;
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
