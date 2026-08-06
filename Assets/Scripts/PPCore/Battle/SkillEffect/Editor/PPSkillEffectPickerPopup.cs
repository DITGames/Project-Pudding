/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillEffectPickerPopup.cs
 * @author hqrse
 * @date 2026/08/07
 * @brief SkillEffect / StatusEffect の型を選ぶツリーポップアップ
 * =====================================*/

using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace PPCore
{
    // SkillEffect・StatusEffect の型を選ぶためのポップアップ。検索欄付きのツリービューを表示する
    // PPEffectDefinition 派生（毒・パラメータ変動など）が選ばれた場合は、
    // PPStatusApplySkillEffectDefinition でラップしてから返す（StatusEffect付与型 SkillEffect ＋ 中身を 1 回の選択でまとめて生成する）
    public sealed class PPSkillEffectPickerPopup : PopupWindowContent
    {
        // 型が選ばれたときに呼ぶコールバック
        private readonly Action<PPSkillEffectDefinition> mOnSelected;
        // 選択候補を表示するツリービュー
        private readonly PPSkillEffectPickerTreeView mTreeView;
        // ツリーの展開状態などを保持する状態オブジェクト
        private readonly TreeViewState<int> mTreeViewState = new();
        // ツリーの絞り込み用検索欄
        private readonly SearchField mSearchField = new();

        // aOnSelected : 型が選ばれたときに呼ぶコールバック
        private PPSkillEffectPickerPopup(Action<PPSkillEffectDefinition> aOnSelected)
        {
            mOnSelected = aOnSelected;
            mTreeView = new PPSkillEffectPickerTreeView(mTreeViewState, OnTypePicked);
            mTreeView.ExpandAll();
        }

        // ポップアップを表示する
        // aActivatorRect : ポップアップを出す基準の矩形
        // aOnSelected : 型が選ばれたときに呼ぶコールバック
        public static void Show(Rect aActivatorRect, Action<PPSkillEffectDefinition> aOnSelected)
            => PopupWindow.Show(aActivatorRect, new PPSkillEffectPickerPopup(aOnSelected));

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

        // 選ばれた型からインスタンスを組み立てる。PPEffectDefinition 派生ならラップしてから返す
        // aType : 選ばれた型
        private void OnTypePicked(Type aType)
        {
            PPSkillEffectDefinition instance = typeof(PPEffectDefinition).IsAssignableFrom(aType)
                ? new PPStatusApplySkillEffectDefinition((PPEffectDefinition)Activator.CreateInstance(aType))
                : (PPSkillEffectDefinition)Activator.CreateInstance(aType);

            mOnSelected?.Invoke(instance);
            editorWindow.Close();
        }
    }
}
