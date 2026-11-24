/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageSelectView.cs
 * @author hqrse
 * @date 2026/11/22
 * @brief はじめからで、進むステージを選ぶ画面
 * =====================================*/

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // 左にステージの表示名を並べ、右に選んでいるステージの表示名・サムネイル・説明を出す
    // まだ選べないステージも並べて中身を見せるが、決定はできない。キャンセルでは選ばずに閉じる
    public class PPStageSelectView : PPStageListMenuBase
    {
        /* 定数 */

        private const string LogTag = "Title";

        /* インスペクタ設定 */

        [Header("項目")]
        [Label("行のプレハブ")]
        [SerializeField] private PPStageListItem mItemPrefab;
        [Label("ステージ一覧")]
        [SerializeField] private PPMapListDefinition mStageList;

        [Header("選んでいるステージ")]
        [Label("表示名")]
        [SerializeField] private TMP_Text mNameText;
        // サムネイルが未設定のステージでは隠す
        [Label("サムネイル")]
        [SerializeField] private Image mThumbnail;
        [Label("説明")]
        [SerializeField] private TMP_Text mDescriptionText;
        [Label("まだ選べないときの説明")]
        [SerializeField] private string mLockedDescription = "ひとつ前のステージをクリアすると選べるようになります";
        [Label("まだ選べないときのサムネイルの色")]
        [SerializeField] private Color mLockedThumbnailColor = new(0.25f, 0.25f, 0.3f, 1f);

        /* 状態 */

        private readonly List<PPMapListEntry> mStages = new();
        private Action<PPMapDefinition> mOnPicked;
        private Action mOnCanceled;

        /* 操作 */

        // ステージを並べて開く。ステージが 1 つもなければ開かずに false
        public bool Open(Action<PPMapDefinition> aOnPicked, Action aOnCanceled)
        {
            mStages.Clear();
            if (mStageList != null) mStages.AddRange(mStageList.GetValidStages());
            if (mStages.Count == 0)
            {
                CustomConsoleLog.Error(LogTag, "ステージ一覧が未設定か、マップ定義が登録されていません。PPStageSelectView の「ステージ一覧」と、その「ステージ」を設定してください");
                return false;
            }
            mOnPicked = aOnPicked;
            mOnCanceled = aOnCanceled;

            OpenPanel();
            var items = PrepareItems(0, mItemPrefab, mStages.Count);
            for (int i = 0; i < items.Count; i++)
            {
                items[i].SetText(mStages[i].Map.DisplayName);
                items[i].SetInteractable(IsUnlocked(i));
            }
            Focus(0, 0);
            return true;
        }

        // 番号でステージを選ぶ。決定の入力と同じ処理で、検証から直接呼ぶこともできる。まだ選べないステージなら false
        public bool Pick(int aIndex)
        {
            if (!IsOpen || aIndex < 0 || aIndex >= mStages.Count || !IsUnlocked(aIndex)) return false;

            var onPicked = mOnPicked;
            ClearCallbacks();
            Close();
            onPicked?.Invoke(mStages[aIndex].Map);
            return true;
        }

        protected override void OnSubmit(int aGroup, int aIndex) => Pick(aIndex);

        protected override void OnCancel()
        {
            var onCanceled = mOnCanceled;
            ClearCallbacks();
            Close();
            onCanceled?.Invoke();
        }

        protected override void OnFocusChanged() => RefreshDetail();

        private void ClearCallbacks()
        {
            mOnPicked = null;
            mOnCanceled = null;
        }

        /* 問い合わせ */

        public bool IsUnlocked(int aIndex) => aIndex >= 0 && aIndex < mStages.Count && PPMapListDefinition.IsUnlocked(mStages, aIndex);

        /* 表示 */

        // 選んでいるステージの表示名・サムネイル・説明を出す。まだ選べないステージは説明を差し替え、サムネイルを暗くする
        private void RefreshDetail()
        {
            if (FocusIndex >= mStages.Count) return;
            var map = mStages[FocusIndex].Map;
            bool isUnlocked = IsUnlocked(FocusIndex);

            if (mNameText != null) mNameText.text = map.DisplayName;
            if (mDescriptionText != null) mDescriptionText.text = isUnlocked ? map.Description : mLockedDescription;
            if (mThumbnail != null)
            {
                mThumbnail.sprite = map.Thumbnail;
                mThumbnail.enabled = map.Thumbnail != null;
                mThumbnail.color = isUnlocked ? Color.white : mLockedThumbnailColor;
            }
        }
    }
}
