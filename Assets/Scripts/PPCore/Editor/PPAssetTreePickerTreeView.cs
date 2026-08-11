/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPAssetTreePickerTreeView.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief アセットをカテゴリ別に表示する折りたたみツリー(汎用)
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace PPCore
{
    // ツリーに並べるアセット 1 件分の情報
    // フォルダ階層と葉の表示名を分けて持つのは、葉の表示名に "/" が混ざっても
    // 意図しない階層に分解されないようにするため（説明文をそのまま葉に出す用途がある）
    internal readonly struct PPAssetPickerEntry
    {
        // 選択されたときに返すアセット
        public UnityEngine.Object Asset { get; }
        // "/" 区切りのフォルダ階層。空なら root 直下の葉になる
        public string FolderPath { get; }
        // 葉に表示する文字列
        public string Label { get; }

        // aAsset : 選択されたときに返すアセット
        // aFolderPath : "/" 区切りのフォルダ階層。階層不要なら空文字
        // aLabel : 葉に表示する文字列
        public PPAssetPickerEntry(UnityEngine.Object aAsset, string aFolderPath, string aLabel)
        {
            Asset = aAsset;
            FolderPath = aFolderPath;
            Label = aLabel;
        }
    }

    // プロジェクト内のアセットをツリーに表示する汎用ビュー
    // 型を並べる PPTypeTreePickerTreeView に対して、こちらは「アセット」を並べる
    // スキルタグ・戦術のように、素のオブジェクトフィールドだと
    // 全アセットが平坦に並んで探しづらいものの選択に使う
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

        // aState : ツリーの展開状態を保持する状態オブジェクト
        // aEntries : ツリーに並べる候補
        // aEmptyMessage : 候補が 1 つも無い場合に表示する文言
        // aOnPickAsset : アセットが選ばれたときのコールバック
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
        // aEntryBuilder : アセットからフォルダ階層と表示名を決める関数
        // return : 整列済みの候補リスト
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
        // return : 構築されたルートノード
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
        // aName : アイコン名
        // return : 取得したテクスチャ。見つからなければ null
        private static Texture2D LoadIcon(string aName)
        {
            var content = EditorGUIUtility.IconContent(aName);
            return content != null ? content.image as Texture2D : null;
        }
    }
}
