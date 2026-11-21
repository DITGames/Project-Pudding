/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageRecruitView.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ユニット獲得のマスで、候補から 1 体を選ぶ画面
 * =====================================*/

using System;
using System.Collections.Generic;

namespace PPCore
{
    // 候補のユニットを並べ、決定した 1 体を返す。必ず 1 体選ぶため、キャンセルでは閉じない
    public class PPStageRecruitView : PPStageListMenuBase
    {
        /* 状態 */

        private readonly List<PPUnitDefinition> mCandidates = new();
        private Action<PPUnitDefinition> mOnPicked;

        /* 操作 */

        public void Open(IReadOnlyList<PPUnitDefinition> aCandidates, int aLevel, Action<PPUnitDefinition> aOnPicked)
        {
            mCandidates.Clear();
            mCandidates.AddRange(aCandidates);
            mOnPicked = aOnPicked;

            var texts = new List<string>(mCandidates.Count);
            foreach (var unit in mCandidates) texts.Add(FormatUnit(unit, aLevel));
            OpenPanel();
            SetColumn(0, texts);
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

        protected override void OnSubmit(int aColumn, int aRow) => Pick(aRow);

        protected override void OnCancel() { }
    }
}
