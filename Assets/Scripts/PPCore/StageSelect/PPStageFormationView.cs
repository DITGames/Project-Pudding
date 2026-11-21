/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageFormationView.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ステージセレクトのパーティ編成画面
 * =====================================*/

using System;
using System.Collections.Generic;

namespace PPCore
{
    // 左の列に 3 つの配置、右の列に所持ユニットを並べ、配置へのユニットの出し入れ・入れ替えを行う
    // 配置を決定してから右の列でユニットを選ぶ。ほかの配置にいるユニットを選ぶと、2 つの配置を入れ替える
    public class PPStageFormationView : PPStageListMenuBase
    {
        /* 定数 */

        private const int SlotColumn = 0;
        private const int UnitColumn = 1;
        private const string EmptyText = "（空き）";
        private const string RemoveText = "外す";
        private const string BenchText = "控え";
        // 左の列の並び
        private static readonly PPPartyPosition[] Positions =
            { PPPartyPosition.Center, PPPartyPosition.Right, PPPartyPosition.Left };

        /* 状態 */

        private PPStageProgress mProgress;
        private Action mOnClosed;
        // ユニットを選んでいる最中の配置の行。選んでいなければ -1
        private int mPickingRow = -1;

        /* 操作 */

        // 編成画面を開く。閉じたら aOnClosed を呼ぶ
        public void Open(PPStageProgress aProgress, Action aOnClosed)
        {
            mProgress = aProgress;
            mOnClosed = aOnClosed;
            mPickingRow = -1;
            OpenPanel();
            Rebuild();
            Focus(SlotColumn, 0);
        }

        public override void Close()
        {
            if (!IsOpen) return;
            base.Close();
            var onClosed = mOnClosed;
            mOnClosed = null;
            onClosed?.Invoke();
        }

        // 配置を決定すると右の列へ移り、右の列で決定するとその配置へ反映して左の列へ戻る
        protected override void OnSubmit(int aColumn, int aRow)
        {
            if (aColumn == SlotColumn)
            {
                mPickingRow = aRow;
                SetMarked(SlotColumn, aRow, true);
                Focus(UnitColumn, 0);
                return;
            }

            var position = Positions[mPickingRow];
            // 右の列の先頭は「外す」
            mProgress.SetMember(position, aRow == 0 ? null : mProgress.OwnedUnits[aRow - 1]);
            EndPicking();
            Rebuild();
        }

        // 右の列にいる間は選ぶのをやめて左の列へ戻り、左の列なら画面を閉じる
        protected override void OnCancel()
        {
            if (mPickingRow < 0)
            {
                Close();
                return;
            }
            EndPicking();
        }

        // 配置を選んでいる間だけ右の列へ入れる
        protected override bool CanFocusColumn(int aColumn) => aColumn == SlotColumn || mPickingRow >= 0;

        private void EndPicking()
        {
            int row = mPickingRow;
            SetMarked(SlotColumn, row, false);
            mPickingRow = -1;
            Focus(SlotColumn, row);
        }

        /* 表示 */

        // 配置と所持ユニットの一覧を、今の編成で書き直す
        private void Rebuild()
        {
            var slots = new List<string>(Positions.Length);
            foreach (var position in Positions)
            {
                var owned = mProgress.GetMember(position);
                slots.Add($"{ToPositionName(position)}  {(owned == null ? EmptyText : FormatUnit(owned.Unit, owned.Level))}");
            }
            SetColumn(SlotColumn, slots);

            var units = new List<string>(mProgress.OwnedUnits.Count + 1) { RemoveText };
            foreach (var owned in mProgress.OwnedUnits)
            {
                var position = mProgress.FindPosition(owned);
                units.Add($"{FormatUnit(owned.Unit, owned.Level)}  [{(position.HasValue ? ToPositionName(position.Value) : BenchText)}]");
            }
            SetColumn(UnitColumn, units);
        }

        private static string ToPositionName(PPPartyPosition aPosition) => aPosition switch
        {
            PPPartyPosition.Right => "右",
            PPPartyPosition.Left => "左",
            _ => "中央",
        };
    }
}
