/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageMenuView.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ステージセレクトのメニュー
 * =====================================*/

using System;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // マップで開くメニュー。編成画面を開くか、閉じるかを選ぶ
    public class PPStageMenuView : PPStageListMenuBase
    {
        /* 定数 */

        private const int FormationRow = 0;
        private static readonly string[] Texts = { "編成", "閉じる" };

        /* インスペクタ設定 */

        [Header("項目")]
        [Label("行のプレハブ")]
        [SerializeField] private PPStageListItem mItemPrefab;

        /* 状態 */

        private Action mOnFormation;

        /* 操作 */

        // メニューを開く。「編成」を選ぶとメニューを閉じてから aOnFormation を呼ぶ
        public void Open(Action aOnFormation)
        {
            mOnFormation = aOnFormation;
            OpenPanel();
            var items = PrepareItems(0, mItemPrefab, Texts.Length);
            for (int i = 0; i < items.Count; i++) items[i].SetText(Texts[i]);
            Focus(0, 0);
        }

        protected override void OnSubmit(int aGroup, int aIndex)
        {
            var onFormation = mOnFormation;
            Close();
            if (aIndex == FormationRow) onFormation?.Invoke();
        }
    }
}
