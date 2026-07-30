/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPConditionPickerPopup.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief 条件ツリービューを表示するポップアップ
 * =====================================*/
using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// AI 条件を選ぶためのポップアップ。検索欄付きのツリービューを表示する。
    /// <para>
    /// ツリーには「条件の型」と「既存の条件アセット」の両方が並ぶ。
    /// 型を選べば新規アセットを自動生成して採用し、既存アセットを選べばそれを再利用する。
    /// アセットを手で作ってから参照させる手間を省くための作り。
    /// </para>
    /// </summary>
    public sealed class PPConditionPickerPopup : PopupWindowContent
    {
        /// <summary>条件が選ばれたときに呼ぶコールバック。</summary>
        private readonly Action<PPPartyConditionValidator> mOnSelected;
        /// <summary>選択候補を表示するツリービュー。</summary>
        private readonly PPConditionTreeView mTreeView;
        /// <summary>ツリーの展開状態などを保持する状態オブジェクト。</summary>
        private readonly TreeViewState<int> mTreeViewState = new();
        /// <summary>ツリーの絞り込み用検索欄。</summary>
        private readonly SearchField mSearchField = new();

        /// <param name="aOnSelected">条件が選ばれたときに呼ぶコールバック。</param>
        private PPConditionPickerPopup(Action<PPPartyConditionValidator> aOnSelected)
        {
            mOnSelected = aOnSelected;
            mTreeView = new PPConditionTreeView(mTreeViewState, OnTypePicked, OnAssetPicked);
            mTreeView.ExpandAll();
        }

        /// <summary>
        /// ポップアップを表示する。
        /// 型を選んだ場合は新規アセットを作成し、既存アセットを選んだ場合はそれをそのまま使う。
        /// </summary>
        /// <param name="aActivatorRect">ポップアップを出す基準の矩形。</param>
        /// <param name="aOnSelected">条件が選ばれたときに呼ぶコールバック。</param>
        public static void Show(Rect aActivatorRect, Action<PPPartyConditionValidator> aOnSelected)
            => PopupWindow.Show(aActivatorRect, new PPConditionPickerPopup(aOnSelected));

        /// <summary>ポップアップのサイズ。</summary>
        public override Vector2 GetWindowSize() => new Vector2(320f, 360f);

        /// <summary>上部に検索欄、その下にツリービューを配置して描画する。</summary>
        /// <param name="aRect">描画領域。</param>
        public override void OnGUI(Rect aRect)
        {
            const float searchHeight = 20f;
            var searchRect = new Rect(aRect.x + 4f, aRect.y + 4f, aRect.width - 8f, searchHeight);
            var treeRect   = new Rect(aRect.x, aRect.y + searchHeight + 6f, aRect.width, aRect.height - searchHeight - 6f);

            mTreeView.searchString = mSearchField.OnGUI(searchRect, mTreeView.searchString);
            mTreeView.OnGUI(treeRect);
        }

        /// <summary>
        /// 条件の型が選ばれたときの処理。新規アセットを生成して採用し、ポップアップを閉じる。
        /// 生成に失敗した場合は何も採用せずに閉じる。
        /// </summary>
        /// <param name="aType">選ばれた条件クラスの型。</param>
        private void OnTypePicked(Type aType)
        {
            var asset = PPConditionAssetFactory.CreateAndSave(aType);
            if (asset != null)
                mOnSelected?.Invoke(asset);
            editorWindow.Close();
        }

        /// <summary>
        /// 既存の条件アセットが選ばれたときの処理。そのまま採用してポップアップを閉じる。
        /// </summary>
        /// <param name="aAsset">選ばれた条件アセット。</param>
        private void OnAssetPicked(PPPartyConditionValidator aAsset)
        {
            mOnSelected?.Invoke(aAsset);
            editorWindow.Close();
        }
    }
}
