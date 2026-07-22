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
    public sealed class PPConditionPickerPopup : PopupWindowContent
    {
        private readonly Action<Type> mOnSelected;
        private readonly PPConditionTreeView mTreeView;
        private readonly TreeViewState<int> mTreeViewState = new();
        private readonly SearchField mSearchField = new();

        private PPConditionPickerPopup(Action<Type> aOnSelected)
        {
            mOnSelected = aOnSelected;
            mTreeView = new PPConditionTreeView(mTreeViewState, OnTypePicked);
            mTreeView.ExpandAll();
        }

        public static void Show(Rect aActivatorRect, Action<Type> aOnSelected)
            => PopupWindow.Show(aActivatorRect, new PPConditionPickerPopup(aOnSelected));

        public override Vector2 GetWindowSize() => new Vector2(320f, 360f);

        public override void OnGUI(Rect aRect)
        {
            const float searchHeight = 20f;
            var searchRect = new Rect(aRect.x + 4f, aRect.y + 4f, aRect.width - 8f, searchHeight);
            var treeRect   = new Rect(aRect.x, aRect.y + searchHeight + 6f, aRect.width, aRect.height - searchHeight - 6f);

            mTreeView.searchString = mSearchField.OnGUI(searchRect, mTreeView.searchString);
            mTreeView.OnGUI(treeRect);
        }

        private void OnTypePicked(Type aType)
        {
            mOnSelected?.Invoke(aType);
            editorWindow.Close();
        }
    }
}