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
    internal sealed class PPConditionTreeView : TreeView<int>
    {
        private readonly Action<Type> mOnPickType;
        private readonly Action<PPPartyConditionValidator> mOnPickAsset;
        private readonly Dictionary<int, Type> mIdToType = new();
        private readonly Dictionary<int, PPPartyConditionValidator> mIdToAsset = new();
        private int mNextFolderId = -2;

        public PPConditionTreeView(TreeViewState<int> aState, Action<Type> aOnPickType, Action<PPPartyConditionValidator> aOnPickAsset)
            : base(aState)
        {
            mOnPickType = aOnPickType;
            mOnPickAsset = aOnPickAsset;
            showAlternatingRowBackgrounds = true;
            Reload();
        }

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
                
                var menu = type.GetCustomAttribute<PPConditionMenuAttribute>();
                string path = menu != null ? menu.Path : $"未分類/{type.Name}";
                string[] segments = path.Split('/');
                
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
            
            if (!root.hasChildren)
                root.AddChild(new TreeViewItem<int>(0, 0, "(条件クラスが見つかりません)"));

            SetupDepthsFromParentsAndChildren(root);
            return root;
        }
        
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

        private static Texture2D LoadIcon(string aName)
        {
            var content = EditorGUIUtility.IconContent(aName);
            return content != null ? content.image as Texture2D : null;
        }
    }
}