/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageRecruitView.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ユニット獲得のマスで、候補から 1 体を選ぶ画面
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 候補のユニットを札で横に並べ、決定した 1 体を返す。必ず 1 体選ぶため、キャンセルでは閉じない
    public class PPStageRecruitView : PPStageListMenuBase
    {
        /* インスペクタ設定 */

        [Header("項目")]
        [Label("候補の札のプレハブ")]
        [SerializeField] private PPStageUnitCard mCardPrefab;

        /* 状態 */

        private readonly List<PPUnitDefinition> mCandidates = new();
        private Action<PPUnitDefinition> mOnPicked;
        private int mLevel;

        /* 操作 */

        public void Open(IReadOnlyList<PPUnitDefinition> aCandidates, int aLevel, Action<PPUnitDefinition> aOnPicked)
        {
            mCandidates.Clear();
            mCandidates.AddRange(aCandidates);
            mOnPicked = aOnPicked;
            mLevel = aLevel;

            OpenPanel();
            var cards = PrepareItems(0, mCardPrefab, mCandidates.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                var unit = mCandidates[i];
                string sub = $"<color={PPStageUnitDisplay.AttributeColorCode(unit.TypeAttribute)}>{PPStageUnitDisplay.AttributeName(unit.TypeAttribute)}属性</color>\n{PPStageUnitDisplay.RoleName(unit)}";
                cards[i].SetUnit(PPStageUnitDisplay.ResolveIcon(UnitVisualCatalog, unit), PPStageUnitDisplay.NameAndLevel(unit, aLevel), sub);
            }
            Focus(0, 0);
        }

        // 候補の番号で選ぶ。決定の入力と同じ処理で、検証から直接呼ぶこともできる
        public void Pick(int aIndex)
        {
            if (!IsOpen || aIndex < 0 || aIndex >= mCandidates.Count) return;

            var onPicked = mOnPicked;
            mOnPicked = null;
            Close();
            onPicked?.Invoke(mCandidates[aIndex]);
        }

        protected override void OnSubmit(int aGroup, int aIndex) => Pick(aIndex);

        protected override void OnCancel() { }

        protected override PPUnitDefinition GetFocusedUnit(out int aLevel)
        {
            aLevel = mLevel;
            return FocusIndex < mCandidates.Count ? mCandidates[FocusIndex] : null;
        }
    }
}
