/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPConditionTreeView.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief 条件クラスをカテゴリ別に表示する折りたたみツリー
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace PPCore
{
    // 条件クラスと既存の条件アセットをカテゴリ別に並べるツリービュー
    // 階層は PPConditionMenuAttribute の Path から組み立てる
    // 各条件クラスのノードの下に、その型で既に作られているアセットをぶら下げるため、
    // 「新規作成」と「既存の再利用」を同じツリーから選べる
    // ノード ID はフォルダを負値、葉（型とアセット）を 0 以上で採番して衝突を避けている
    internal sealed class PPConditionTreeView : TreeView<int>
    {
        // 条件の型が選ばれたときのコールバック
        private readonly Action<Type> mOnPickType;
        // 既存アセットが選ばれたときのコールバック
        private readonly Action<PPPartyConditionValidator> mOnPickAsset;
        // ノード ID から条件クラスの型への対応
        private readonly Dictionary<int, Type> mIdToType = new();
        // ノード ID から既存アセットへの対応
        private readonly Dictionary<int, PPPartyConditionValidator> mIdToAsset = new();
        // フォルダノードに割り当てる次の ID。葉と衝突しないよう負方向へ進める
        private int mNextFolderId = -2;

        // aState : ツリーの展開状態を保持する状態オブジェクト
        // aOnPickType : 条件の型が選ばれたときのコールバック
        // aOnPickAsset : 既存アセットが選ばれたときのコールバック
        public PPConditionTreeView(TreeViewState<int> aState, Action<Type> aOnPickType, Action<PPPartyConditionValidator> aOnPickAsset)
            : base(aState)
        {
            mOnPickType = aOnPickType;
            mOnPickAsset = aOnPickAsset;
            showAlternatingRowBackgrounds = true;
            Reload();
        }

        // ツリーを構築する
        // 条件クラスを走査し、属性のパスに沿ってフォルダノードを掘りながら葉を追加する
        // さらに各型について既存アセットを検索し、その型のノードの子として並べる
        // return : 構築されたルートノード
        protected override TreeViewItem<int> BuildRoot()
        {
            var root = new TreeViewItem<int>(-1, -1, "root");
            var folders = new Dictionary<string, TreeViewItem<int>>();
            mIdToType.Clear();
            mIdToAsset.Clear();
            int nextLeafId = 0;

            foreach (var type in TypeCache.GetTypesDerivedFrom<PPPartyConditionValidator>())
            {
                if(type.IsAbstract) continue;

                // 属性が無い条件クラスは「未分類」へ逃がす
                var menu = type.GetCustomAttribute<PPConditionMenuAttribute>();
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
                var leaf = new TreeViewItem<int>(leafId, 0, segments[^1])
                {
                    icon = LoadIcon("ScriptableObject Icon")
                };
                parent.AddChild(leaf);
                mIdToType[leafId] = type;

                // 既に作成済みのアセットがあれば子として一覧表示し、既存アセットの再利用を選べるようにする
                foreach (var guid in AssetDatabase.FindAssets($"t:{type.Name}"))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<PPPartyConditionValidator>(assetPath);
                    // FindAssets は派生型も拾うため、厳密に一致するものだけを採用する
                    if (asset == null || asset.GetType() != type) continue;

                    int assetId = nextLeafId++;
                    string label = !string.IsNullOrEmpty(asset.Description) ? asset.Description : asset.name;
                    var assetLeaf = new TreeViewItem<int>(assetId, 0, label)
                    {
                        icon = LoadIcon("ScriptableObject On Icon")
                    };
                    leaf.AddChild(assetLeaf);
                    mIdToAsset[assetId] = asset;
                }
            }

            // TreeView は子が 1 つも無いと例外になるため、ダミーを入れておく
            if (!root.hasChildren)
                root.AddChild(new TreeViewItem<int>(0, 0, "(条件クラスが見つかりません)"));

            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        // ダブルクリックで選択を確定する
        // アセットを先に判定するため、型ノードとアセットノードが混在していても取り違えない
        // aId : ダブルクリックされたノードの ID
        protected override void DoubleClickedItem(int aId)
        {
            if (mIdToAsset.TryGetValue(aId, out var asset))
            {
                mOnPickAsset?.Invoke(asset);
                return;
            }

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
