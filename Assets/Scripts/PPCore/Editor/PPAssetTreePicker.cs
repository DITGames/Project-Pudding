/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPAssetTreePicker.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief アセットをツリーから選ぶピッカー（ポップアップとツリービュー）
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace PPCore
{
    // アセットを選ぶためのポップアップ。検索欄付きのツリービューを表示する
    // 「名前 : 説明」のように中身が分かる表示で選ばせたい場合に使う
    public sealed class PPAssetTreePickerPopup : PopupWindowContent
    {
        // アセットが選ばれたときに呼ぶコールバック
        private readonly Action<UnityEngine.Object> mOnSelected;
        // 選択候補を表示するツリービュー
        private readonly PPAssetTreePickerTreeView mTreeView;
        // ツリーの展開状態などを保持する状態オブジェクト
        private readonly TreeViewState<int> mTreeViewState = new();
        // ツリーの絞り込み用検索欄
        private readonly SearchField mSearchField = new();
        // ポップアップの表示サイズ
        private readonly Vector2 mWindowSize;

        private PPAssetTreePickerPopup(IReadOnlyList<PPAssetPickerEntry> aEntries, string aEmptyMessage,
            Vector2 aWindowSize, Action<UnityEngine.Object> aOnSelected)
        {
            mOnSelected = aOnSelected;
            mWindowSize = aWindowSize;
            mTreeView = new PPAssetTreePickerTreeView(mTreeViewState, aEntries, aEmptyMessage, OnAssetPicked);
            mTreeView.ExpandAll();
        }

        // プロジェクト内の指定型アセットを候補にしてポップアップを表示する
        public static void ShowAssets<T>(Rect aActivatorRect, Func<T, (string FolderPath, string Label)> aEntryBuilder,
            string aEmptyMessage, Action<T> aOnSelected, Vector2 aWindowSize = default)
            where T : UnityEngine.Object
        {
            var entries = PPAssetTreePickerTreeView.Collect(aEntryBuilder);
            var size = aWindowSize == default ? new Vector2(320f, 360f) : aWindowSize;
            PopupWindow.Show(aActivatorRect,
                new PPAssetTreePickerPopup(entries, aEmptyMessage, size, picked => aOnSelected?.Invoke(picked as T)));
        }

        // ポップアップのサイズ
        public override Vector2 GetWindowSize() => mWindowSize;

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

        // アセットが選ばれたときの処理。コールバックへ渡してポップアップを閉じる
        // aAsset : 選ばれたアセット
        private void OnAssetPicked(UnityEngine.Object aAsset)
        {
            mOnSelected?.Invoke(aAsset);
            editorWindow.Close();
        }
    }

    // ツリーに並べるアセット 1 件分の情報
    // 葉の表示名に "/" が混ざっても階層に分解されないよう、フォルダ階層と表示名を分けて持つ
    internal readonly struct PPAssetPickerEntry
    {
        // 選択されたときに返すアセット
        public UnityEngine.Object Asset { get; }
        // "/" 区切りのフォルダ階層。空なら root 直下の葉になる
        public string FolderPath { get; }
        // 葉に表示する文字列
        public string Label { get; }

        public PPAssetPickerEntry(UnityEngine.Object aAsset, string aFolderPath, string aLabel)
        {
            Asset = aAsset;
            FolderPath = aFolderPath;
            Label = aLabel;
        }
    }

    // プロジェクト内のアセットをツリーに表示する汎用ビュー
    internal sealed class PPAssetTreePickerTreeView : TreeView<int>
    {
        // ツリーに並べる候補
        private readonly IReadOnlyList<PPAssetPickerEntry> mEntries;
        // 候補が 1 つも無い場合に表示する文言
        private readonly string mEmptyMessage;
        // アセットが選ばれたときのコールバック
        private readonly Action<UnityEngine.Object> mOnPickAsset;
        // ノード ID からアセットへの対応
        private readonly Dictionary<int, UnityEngine.Object> mIdToAsset = new();
        // フォルダノードに割り当てる次の ID。葉と衝突しないよう負方向へ進める
        private int mNextFolderId = -2;

        public PPAssetTreePickerTreeView(TreeViewState<int> aState, IReadOnlyList<PPAssetPickerEntry> aEntries,
            string aEmptyMessage, Action<UnityEngine.Object> aOnPickAsset) : base(aState)
        {
            mEntries = aEntries;
            mEmptyMessage = aEmptyMessage;
            mOnPickAsset = aOnPickAsset;
            showAlternatingRowBackgrounds = true;
            Reload();
        }

        // プロジェクト内の指定型アセットを集めて候補リストを作る
        // 並びが安定しないとツリーの見た目が開くたびに変わるため、フォルダ＋表示名で整列させる
        public static List<PPAssetPickerEntry> Collect<T>(Func<T, (string FolderPath, string Label)> aEntryBuilder)
            where T : UnityEngine.Object
        {
            var list = new List<PPAssetPickerEntry>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null) continue;

                var (folderPath, label) = aEntryBuilder(asset);
                list.Add(new PPAssetPickerEntry(asset, folderPath, label));
            }

            list.Sort((a, b) =>
            {
                int folder = string.CompareOrdinal(a.FolderPath, b.FolderPath);
                return folder != 0 ? folder : string.CompareOrdinal(a.Label, b.Label);
            });
            return list;
        }

        // ツリーを構築する
        // フォルダ階層を掘りながら葉を追加する。葉の表示名は分解せずそのまま使う
        protected override TreeViewItem<int> BuildRoot()
        {
            var root = new TreeViewItem<int>(-1, -1, "root");
            var folders = new Dictionary<string, TreeViewItem<int>>();
            mIdToAsset.Clear();
            int nextLeafId = 0;

            foreach (var entry in mEntries)
            {
                TreeViewItem<int> parent = root;
                if (!string.IsNullOrEmpty(entry.FolderPath))
                {
                    string accum = "";
                    foreach (var segment in entry.FolderPath.Split('/'))
                    {
                        if (string.IsNullOrEmpty(segment)) continue;

                        accum = string.IsNullOrEmpty(accum) ? segment : $"{accum}/{segment}";
                        if (!folders.TryGetValue(accum, out var folder))
                        {
                            folder = new TreeViewItem<int>(mNextFolderId--, 0, segment)
                            {
                                icon = LoadIcon("Folder Icon")
                            };
                            parent.AddChild(folder);
                            folders[accum] = folder;
                        }
                        parent = folder;
                    }
                }

                int leafId = nextLeafId++;
                parent.AddChild(new TreeViewItem<int>(leafId, 0, entry.Label));
                mIdToAsset[leafId] = entry.Asset;
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
            if (mIdToAsset.TryGetValue(aId, out var asset))
                mOnPickAsset?.Invoke(asset);
        }

        // エディタ組み込みアイコンを名前で取得する
        private static Texture2D LoadIcon(string aName)
        {
            var content = EditorGUIUtility.IconContent(aName);
            return content != null ? content.image as Texture2D : null;
        }
    }
}
