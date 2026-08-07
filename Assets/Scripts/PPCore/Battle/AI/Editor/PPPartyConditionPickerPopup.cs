/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyConditionPickerPopup.cs
 * @author hqrse
 * @date 2026/08/07
 * @brief 条件クラスをツリー表示で選ぶポップアップ
 * =====================================*/

using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace PPCore
{
    // AI のシチュエーション条件を選ぶためのポップアップ。検索欄付きのツリービューを表示する
    // PPSkillEffectPickerPopup と同じ形で、選ばれた型のインスタンスをその場で生成して返す
    public sealed class PPPartyConditionPickerPopup : PopupWindowContent
    {
        // 型が選ばれたときに呼ぶコールバック
        private readonly Action<PPPartyConditionValidator> mOnSelected;
        // 選択候補を表示するツリービュー
        private readonly PPPartyConditionPickerTreeView mTreeView;
        // ツリーの展開状態などを保持する状態オブジェクト
        private readonly TreeViewState<int> mTreeViewState = new();
        // ツリーの絞り込み用検索欄
        private readonly SearchField mSearchField = new();

        // aOnSelected : 型が選ばれたときに呼ぶコールバック
        private PPPartyConditionPickerPopup(Action<PPPartyConditionValidator> aOnSelected)
        {
            mOnSelected = aOnSelected;
            mTreeView = new PPPartyConditionPickerTreeView(mTreeViewState, OnTypePicked);
            mTreeView.ExpandAll();
        }

        // ポップアップを表示する
        // aActivatorRect : ポップアップを出す基準の矩形
        // aOnSelected : 型が選ばれたときに呼ぶコールバック
        public static void Show(Rect aActivatorRect, Action<PPPartyConditionValidator> aOnSelected)
            => PopupWindow.Show(aActivatorRect, new PPPartyConditionPickerPopup(aOnSelected));

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

        // 型が選ばれたときの処理。インスタンスを生成して採用し、ポップアップを閉じる
        // aType : 選ばれた条件クラスの型
        private void OnTypePicked(Type aType)
        {
            var instance = (PPPartyConditionValidator)Activator.CreateInstance(aType);
            mOnSelected?.Invoke(instance);
            editorWindow.Close();
        }
    }
}
