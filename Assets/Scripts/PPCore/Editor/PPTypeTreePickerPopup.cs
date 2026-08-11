/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTypeTreePickerPopup.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 型をツリー表示で選ぶポップアップ(汎用)
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace PPCore
{
    // [SerializeReference] な型を選ばせるためのポップアップ。検索欄付きのツリービューを表示する
    // 選ばれた型をそのままコールバックへ渡すだけで、インスタンスの生成は呼び出し側に任せる
    // スキルエフェクトのように選択結果をラップしてから使いたいケースがあるため、
    // 生成方法をここに固定しない
    public sealed class PPTypeTreePickerPopup : PopupWindowContent
    {
        // 型が選ばれたときに呼ぶコールバック
        private readonly Action<Type> mOnSelected;
        // 選択候補を表示するツリービュー
        private readonly PPTypeTreePickerTreeView mTreeView;
        // ツリーの展開状態などを保持する状態オブジェクト
        private readonly TreeViewState<int> mTreeViewState = new();
        // ツリーの絞り込み用検索欄
        private readonly SearchField mSearchField = new();

        // aCandidateTypes : ツリーに並べる候補の型
        // aEmptyMessage : 候補が 1 つも無い場合に表示する文言
        // aOnSelected : 型が選ばれたときに呼ぶコールバック
        private PPTypeTreePickerPopup(IReadOnlyList<Type> aCandidateTypes, string aEmptyMessage, Action<Type> aOnSelected)
        {
            mOnSelected = aOnSelected;
            mTreeView = new PPTypeTreePickerTreeView(mTreeViewState, aCandidateTypes, aEmptyMessage, OnTypePicked);
            mTreeView.ExpandAll();
        }

        // ポップアップを表示する
        // aActivatorRect : ポップアップを出す基準の矩形
        // aCandidateTypes : ツリーに並べる候補の型
        // aEmptyMessage : 候補が 1 つも無い場合に表示する文言
        // aOnSelected : 型が選ばれたときに呼ぶコールバック
        public static void Show(Rect aActivatorRect, IReadOnlyList<Type> aCandidateTypes,
            string aEmptyMessage, Action<Type> aOnSelected)
            => PopupWindow.Show(aActivatorRect, new PPTypeTreePickerPopup(aCandidateTypes, aEmptyMessage, aOnSelected));

        // 基底型の具象派生型をそのまま候補にしてポップアップを表示する
        // 選ばれた型のインスタンスを生成してコールバックへ渡す、最も一般的な使い方の入口
        // aActivatorRect : ポップアップを出す基準の矩形
        // aEmptyMessage : 候補が 1 つも無い場合に表示する文言
        // aOnCreated : 生成されたインスタンスを受け取るコールバック
        public static void ShowDerived<T>(Rect aActivatorRect, string aEmptyMessage, Action<T> aOnCreated)
            where T : class
            => Show(aActivatorRect, PPTypeTreePickerTreeView.CollectDerived<T>(), aEmptyMessage,
                type => aOnCreated?.Invoke((T)Activator.CreateInstance(type)));

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

        // 型が選ばれたときの処理。コールバックへ渡してポップアップを閉じる
        // aType : 選ばれた型
        private void OnTypePicked(Type aType)
        {
            mOnSelected?.Invoke(aType);
            editorWindow.Close();
        }
    }
}
