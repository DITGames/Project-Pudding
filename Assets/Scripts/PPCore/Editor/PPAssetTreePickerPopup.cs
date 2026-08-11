/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPAssetTreePickerPopup.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief アセットをツリー表示で選ぶポップアップ(汎用)
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace PPCore
{
    // アセットを選ぶためのポップアップ。検索欄付きのツリービューを表示する
    // 標準のオブジェクトピッカーはアセット名しか出せないため、
    // 「戦術名 : 説明」のように中身が分かる表示で選ばせたい場合にこちらを使う
    // 型を選ぶ PPTypeTreePickerPopup と操作感を揃えてある
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

        // aEntries : ツリーに並べる候補
        // aEmptyMessage : 候補が 1 つも無い場合に表示する文言
        // aWindowSize : ポップアップの表示サイズ
        // aOnSelected : アセットが選ばれたときに呼ぶコールバック
        private PPAssetTreePickerPopup(IReadOnlyList<PPAssetPickerEntry> aEntries, string aEmptyMessage,
            Vector2 aWindowSize, Action<UnityEngine.Object> aOnSelected)
        {
            mOnSelected = aOnSelected;
            mWindowSize = aWindowSize;
            mTreeView = new PPAssetTreePickerTreeView(mTreeViewState, aEntries, aEmptyMessage, OnAssetPicked);
            mTreeView.ExpandAll();
        }

        // プロジェクト内の指定型アセットを候補にしてポップアップを表示する
        // aActivatorRect : ポップアップを出す基準の矩形
        // aEntryBuilder : アセットからフォルダ階層と表示名を決める関数
        // aEmptyMessage : 候補が 1 つも無い場合に表示する文言
        // aOnSelected : アセットが選ばれたときに呼ぶコールバック
        // aWindowSize : ポップアップの表示サイズ。説明文まで出す場合は幅を広めに取る
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
}
