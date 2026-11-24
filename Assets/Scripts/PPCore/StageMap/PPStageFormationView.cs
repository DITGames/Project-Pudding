/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageFormationView.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ステージマップのパーティ編成画面
 * =====================================*/

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // 上に 3 つの配置、下に所持ユニットの札を並べ、配置へのユニットの出し入れ・入れ替えを行う
    // 配置とユニットのどちらを先に決定してもよく、もう片方を決定するとその配置へ反映する。ユニットコストの合計が上限を超える編成はできない
    public class PPStageFormationView : PPStageListMenuBase
    {
        /* 定数 */

        private const int SlotGroup = 0;
        private const int UnitGroup = 1;
        private const string EmptyText = "（空き）";
        private const string RemoveText = "外す";
        private const string RemoveSubText = "枠を空にする";
        private const string BenchText = "控え";
        private const string OverCostText = "コストの合計が上限を超えるため、編成できません";
        // 上の枠の数
        private const int SlotCount = PPStageProgress.MaxPartySize;

        /* インスペクタ設定 */

        [Header("項目")]
        [Label("配置の札のプレハブ")]
        [SerializeField] private PPStageUnitCard mSlotCardPrefab;
        [Label("所持ユニットの札のプレハブ")]
        [SerializeField] private PPStageUnitCard mUnitCardPrefab;

        // 選んでいるユニットの要約。未設定の項目は出さない
        [Header("選択中のユニット")]
        [Label("アイコン")]
        [SerializeField] private Image mSummaryIcon;
        [Label("名前")]
        [SerializeField] private TMP_Text mSummaryName;
        // 属性
        [Label("情報")]
        [SerializeField] private TMP_Text mSummaryInfo;
        [Label("ステータス")]
        [SerializeField] private TMP_Text mSummaryStats;

        /* 状態 */

        private PPStageProgress mProgress;
        private Action mOnClosed;
        // 先に決定して押さえている札のまとまりと番号。押さえていなければ番号は -1
        private int mPickGroup;
        private int mPickIndex = -1;

        /* 操作 */

        // 編成画面を開く。閉じたら aOnClosed を呼ぶ
        public void Open(PPStageProgress aProgress, Action aOnClosed)
        {
            mProgress = aProgress;
            mOnClosed = aOnClosed;
            mPickIndex = -1;
            OpenPanel();
            Rebuild();
            Focus(SlotGroup, 0);
        }

        public override void Close()
        {
            if (!IsOpen) return;
            base.Close();
            var onClosed = mOnClosed;
            mOnClosed = null;
            onClosed?.Invoke();
        }

        // 1 つ目の決定で札を押さえ、もう片方のまとまりの札を決定したら配置へ反映する
        // 同じまとまりの札を決定したときは、押さえる札を替える(同じ札なら押さえるのをやめる)
        protected override void OnSubmit(int aGroup, int aIndex)
        {
            if (mPickIndex < 0)
            {
                Pick(aGroup, aIndex);
                Focus(aGroup == SlotGroup ? UnitGroup : SlotGroup, 0);
                return;
            }
            if (aGroup == mPickGroup)
            {
                bool isSame = aIndex == mPickIndex;
                EndPicking();
                if (!isSame) Pick(aGroup, aIndex);
                return;
            }

            int slot = aGroup == SlotGroup ? aIndex : mPickIndex;
            int unit = aGroup == UnitGroup ? aIndex : mPickIndex;
            // 所持ユニットの札の先頭は「外す」
            var picked = unit == 0 ? null : mProgress.OwnedUnits[unit - 1];
            if (!mProgress.CanSetMember(slot, picked))
            {
                EndPicking();
                if (mSummaryInfo != null) mSummaryInfo.text = $"{OverCostText}({CostText()})";
                return;
            }
            mProgress.SetMember(slot, picked);
            EndPicking();
            Rebuild();
            Focus(SlotGroup, slot);
        }

        // 押さえている札があれば押さえるのをやめ、無ければ画面を閉じる
        protected override void OnCancel()
        {
            if (mPickIndex < 0)
            {
                Close();
                return;
            }
            EndPicking();
        }

        protected override void OnFocusChanged() => ShowSummary(GetFocusedUnit(out int level), level);

        protected override PPUnitDefinition GetFocusedUnit(out int aLevel)
        {
            var owned = FocusedOwnedUnit();
            aLevel = owned?.Level ?? 0;
            return owned?.Unit;
        }

        private void Pick(int aGroup, int aIndex)
        {
            mPickGroup = aGroup;
            mPickIndex = aIndex;
            SetMarked(aGroup, aIndex, true);
        }

        private void EndPicking()
        {
            if (mPickIndex >= 0) SetMarked(mPickGroup, mPickIndex, false);
            mPickIndex = -1;
        }

        /* 問い合わせ */

        // 選んでいる札のユニット。空きの配置や「外す」なら null
        private PPStageOwnedUnit FocusedOwnedUnit()
        {
            if (mProgress == null) return null;
            if (FocusGroup == SlotGroup) return mProgress.GetMember(FocusIndex);
            return FocusIndex > 0 && FocusIndex - 1 < mProgress.OwnedUnits.Count ? mProgress.OwnedUnits[FocusIndex - 1] : null;
        }

        /* 表示 */

        // 配置と所持ユニットの札を、今の編成で書き直す
        private void Rebuild()
        {
            var slots = PrepareItems(SlotGroup, mSlotCardPrefab, SlotCount);
            for (int i = 0; i < SlotCount; i++)
            {
                var owned = mProgress.GetMember(i);
                // 見出しの無い札でも何番目の枠か分かるよう、下の行にも番号を出す
                slots[i].SetHeader(ToSlotName(i));
                if (owned == null) slots[i].SetUnit(null, EmptyText, ToSlotName(i));
                else slots[i].SetUnit(Icon(owned.Unit), PPStageUnitDisplay.NameAndLevel(owned.Unit, owned.Level), $"{ToSlotName(i)}   {PPStageUnitDisplay.ColoredAttribute(owned.Unit)}   コスト{owned.Unit.UnitCost}");
            }

            var cards = PrepareItems(UnitGroup, mUnitCardPrefab, mProgress.OwnedUnits.Count + 1);
            cards[0].SetUnit(null, RemoveText, RemoveSubText);
            for (int i = 0; i < mProgress.OwnedUnits.Count; i++)
            {
                var owned = mProgress.OwnedUnits[i];
                var slot = mProgress.FindSlot(owned);
                string place = slot.HasValue ? ToSlotName(slot.Value) : BenchText;
                cards[i + 1].SetUnit(Icon(owned.Unit), PPStageUnitDisplay.NameAndLevel(owned.Unit, owned.Level),
                    $"{PPStageUnitDisplay.ColoredAttribute(owned.Unit)}   {place}   コスト{owned.Unit.UnitCost}");
            }
        }

        // 選んでいるユニットの要約を出す。ユニットが無ければ空にする
        private void ShowSummary(PPUnitDefinition aUnit, int aLevel)
        {
            var icon = aUnit != null ? Icon(aUnit) : null;
            if (mSummaryIcon != null)
            {
                mSummaryIcon.sprite = icon;
                mSummaryIcon.enabled = icon != null;
            }
            if (mSummaryName != null) mSummaryName.text = aUnit != null ? PPStageUnitDisplay.NameAndLevel(aUnit, aLevel) : string.Empty;
            if (mSummaryInfo != null) mSummaryInfo.text = aUnit != null ? $"{PPStageUnitDisplay.ColoredAttribute(aUnit)}属性   コスト{aUnit.UnitCost}   ({CostText()})" : CostText();
            if (mSummaryStats != null) mSummaryStats.text = aUnit != null ? PPStageUnitDisplay.StatsLine(aUnit, aLevel) : string.Empty;
        }

        private Sprite Icon(PPUnitDefinition aUnit) => PPStageUnitDisplay.ResolveIcon(UnitVisualCatalog, aUnit);

        // 編成のコストの合計と上限
        private string CostText() => mProgress != null ? $"編成コスト {mProgress.GetPartyCost()}/{PPStageProgress.MaxPartyCost}" : string.Empty;

        // 枠の番号を 1 から数えて出す
        private static string ToSlotName(int aSlot) => $"{aSlot + 1}番";
    }
}
