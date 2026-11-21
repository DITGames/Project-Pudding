/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageMenuView.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ステージセレクトのメニュー
 * =====================================*/

using System;

namespace PPCore
{
    // マップで開くメニュー。編成画面を開くか、閉じるかを選ぶ
    public class PPStageMenuView : PPStageListMenuBase
    {
        /* 定数 */

        private const int FormationRow = 0;
        private static readonly string[] Texts = { "編成", "閉じる" };

        /* 状態 */

        private Action mOnFormation;

        /* 操作 */

        // メニューを開く。「編成」を選ぶとメニューを閉じてから aOnFormation を呼ぶ
        public void Open(Action aOnFormation)
        {
            mOnFormation = aOnFormation;
            OpenPanel();
            SetColumn(0, Texts);
            Focus(0, 0);
        }

        protected override void OnSubmit(int aColumn, int aRow)
        {
            var onFormation = mOnFormation;
            Close();
            if (aRow == FormationRow) onFormation?.Invoke();
        }
    }
}
