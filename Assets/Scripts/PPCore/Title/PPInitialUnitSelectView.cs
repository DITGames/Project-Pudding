/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPInitialUnitSelectView.cs
 * @author hqrse
 * @date 2026/11/22
 * @brief はじめからで、最初のパーティのユニットを 1 体選ぶ画面
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // 初期パーティ定義の候補を札で横に並べ、決定した 1 体を返す。キャンセルでは選ばずに閉じる
    public class PPInitialUnitSelectView : PPStageListMenuBase
    {
        /* 定数 */

        private const string LogTag = "Title";

        /* インスペクタ設定 */

        [Header("項目")]
        [Label("候補の札のプレハブ")]
        [SerializeField] private PPStageUnitCard mCardPrefab;
        [Label("初期パーティ定義")]
        [SerializeField] private PPInitialPartyDefinition mDefinition;

        /* 状態 */

        private readonly List<PPInitialPartyMember> mCandidates = new();
        private Action<PPInitialPartyMember> mOnPicked;
        private Action mOnCanceled;

        /* プロパティ */

        public PPInitialPartyDefinition Definition => mDefinition;

        /* 操作 */

        // 候補を並べて開く。候補が 1 体もなければ開かずに false
        public bool Open(Action<PPInitialPartyMember> aOnPicked, Action aOnCanceled)
        {
            mCandidates.Clear();
            if (mDefinition != null) mCandidates.AddRange(mDefinition.GetValidCandidates());
            if (mCandidates.Count == 0)
            {
                CustomConsoleLog.Error(LogTag, "初期パーティ定義が未設定か、候補にユニットがありません。PPInitialUnitSelectView の「初期パーティ定義」と、その「候補」を設定してください");
                return false;
            }
            mOnPicked = aOnPicked;
            mOnCanceled = aOnCanceled;

            OpenPanel();
            var cards = PrepareItems(0, mCardPrefab, mCandidates.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                var member = mCandidates[i];
                var unit = member.Unit;
                string sub = $"<color={PPStageUnitDisplay.AttributeColorCode(unit.TypeAttribute)}>{PPStageUnitDisplay.AttributeName(unit.TypeAttribute)}属性</color>";
                cards[i].SetUnit(PPStageUnitDisplay.ResolveIcon(UnitVisualCatalog, unit), PPStageUnitDisplay.NameAndLevel(unit, member.Level), sub);
            }
            Focus(0, 0);
            return true;
        }

        // 候補の番号で選ぶ。決定の入力と同じ処理で、検証から直接呼ぶこともできる
        public void Pick(int aIndex)
        {
            if (!IsOpen || aIndex < 0 || aIndex >= mCandidates.Count) return;

            var onPicked = mOnPicked;
            ClearCallbacks();
            Close();
            onPicked?.Invoke(mCandidates[aIndex]);
        }

        protected override void OnSubmit(int aGroup, int aIndex) => Pick(aIndex);

        protected override void OnCancel()
        {
            var onCanceled = mOnCanceled;
            ClearCallbacks();
            Close();
            onCanceled?.Invoke();
        }

        protected override PPUnitDefinition GetFocusedUnit(out int aLevel)
        {
            var member = FocusIndex < mCandidates.Count ? mCandidates[FocusIndex] : null;
            aLevel = member?.Level ?? 0;
            return member?.Unit;
        }

        private void ClearCallbacks()
        {
            mOnPicked = null;
            mOnCanceled = null;
        }
    }
}
