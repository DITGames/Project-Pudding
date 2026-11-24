/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageMenuView.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ステージマップのメニュー
 * =====================================*/

using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // マップで開くメニュー。編成は専用の入力(Tab / パッドの North)で開くため、ここには載せない
    public class PPStageMenuView : PPStageListMenuBase
    {
        /* 定数 */

        private static readonly string[] Texts = { "閉じる" };

        /* インスペクタ設定 */

        [Header("項目")]
        [Label("行のプレハブ")]
        [SerializeField] private PPStageListItem mItemPrefab;

        /* 操作 */

        public void Open()
        {
            OpenPanel();
            var items = PrepareItems(0, mItemPrefab, Texts.Length);
            for (int i = 0; i < items.Count; i++) items[i].SetText(Texts[i]);
            Focus(0, 0);
        }

        protected override void OnSubmit(int aGroup, int aIndex) => Close();
    }
}
