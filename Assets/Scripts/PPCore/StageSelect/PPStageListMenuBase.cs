/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageListMenuBase.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ステージセレクトの、行を並べて選ぶメニューの基底
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using AttributeUtility;

namespace PPCore
{
    // 列ごとに行を並べ、上下左右の移動・決定・キャンセルで選ぶメニューの基底
    // 入力は新 Input System の UI の操作で受け、キーボードとゲームパッドの両方で動く
    public abstract class PPStageListMenuBase : MonoBehaviour
    {
        /* 定数 */

        // スティックをどこまで倒したら 1 回の移動とみなすか(0〜1)
        private const float NavigateThreshold = 0.5f;

        /* インスペクタ設定 */

        [Header("入力")]
        [Label("移動の入力")]
        [SerializeField] private InputActionReference mNavigateAction;
        [Label("決定の入力")]
        [SerializeField] private InputActionReference mSubmitAction;
        [Label("キャンセルの入力")]
        [SerializeField] private InputActionReference mCancelAction;

        [Header("表示")]
        // 開いている間だけ表示する部分
        [Label("画面")]
        [SerializeField] private GameObject mPanel;
        [Label("行のプレハブ")]
        [SerializeField] private PPStageListItem mItemPrefab;
        // 列ごとに行を並べる親。左の列から順に置く
        [Label("列", true)]
        [SerializeField] private RectTransform[] mColumns = new RectTransform[0];

        /* 状態 */

        // 列ごとの行。表示しない行も作り直さずに残す
        private readonly List<List<PPStageListItem>> mItems = new();
        // 列ごとの、表示している行の数
        private readonly List<int> mRowCounts = new();
        private int mOpenedFrame = -1;
        private Vector2Int mLastNavigate;

        /* プロパティ */

        public bool IsOpen { get; private set; }
        // 最後に閉じたフレーム。閉じたキーでほかの操作まで動かないよう、呼び出し側がそのフレームの入力を捨てる
        public int ClosedFrame { get; private set; } = -1;
        public int FocusColumn { get; private set; }
        public int FocusRow { get; private set; }

        /* Unity イベント */

        protected virtual void Awake()
        {
            if (mPanel != null) mPanel.SetActive(false);
        }

        // 開いたフレームは、開いたキーで決定まで動かないよう入力を受け付けない
        protected virtual void Update()
        {
            if (!IsOpen || Time.frameCount == mOpenedFrame) return;

            var navigate = ReadNavigate();
            if (navigate != mLastNavigate && navigate != Vector2Int.zero) Move(navigate);
            mLastNavigate = navigate;

            if (WasPressed(mSubmitAction)) OnSubmit(FocusColumn, FocusRow);
            else if (WasPressed(mCancelAction)) OnCancel();
        }

        /* 操作 */

        public virtual void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            ClosedFrame = Time.frameCount;
            if (mPanel != null) mPanel.SetActive(false);
        }

        // 決定したとき(列, 行)
        protected abstract void OnSubmit(int aColumn, int aRow);

        protected virtual void OnCancel() => Close();

        // 移動先の列へ入れるか。入れない列へは左右に動かない
        protected virtual bool CanFocusColumn(int aColumn) => true;

        protected void OpenPanel()
        {
            IsOpen = true;
            mOpenedFrame = Time.frameCount;
            mLastNavigate = ReadNavigate();
            foreach (var action in new[] { mNavigateAction, mSubmitAction, mCancelAction })
            {
                if (action != null) action.action.Enable();
            }
            if (mPanel != null) mPanel.SetActive(true);
        }

        // 列の行を文字の一覧で作り直す。足りない行はプレハブから作り、余った行は隠す
        protected void SetColumn(int aColumn, IReadOnlyList<string> aTexts)
        {
            while (mItems.Count <= aColumn)
            {
                mItems.Add(new List<PPStageListItem>());
                mRowCounts.Add(0);
            }

            var items = mItems[aColumn];
            while (items.Count < aTexts.Count) items.Add(Instantiate(mItemPrefab, mColumns[aColumn]));

            for (int i = 0; i < items.Count; i++)
            {
                bool isUsed = i < aTexts.Count;
                items[i].gameObject.SetActive(isUsed);
                if (isUsed) items[i].SetText(aTexts[i]);
            }
            mRowCounts[aColumn] = aTexts.Count;
            Focus(FocusColumn, FocusRow);
        }

        // 選択を移す。行は列の行数に収める
        protected void Focus(int aColumn, int aRow)
        {
            if (mRowCounts.Count == 0) return;
            FocusColumn = Mathf.Clamp(aColumn, 0, mRowCounts.Count - 1);
            FocusRow = Mathf.Clamp(aRow, 0, Mathf.Max(0, mRowCounts[FocusColumn] - 1));

            for (int c = 0; c < mItems.Count; c++)
            {
                for (int r = 0; r < mItems[c].Count; r++) mItems[c][r].SetFocused(c == FocusColumn && r == FocusRow);
            }
        }

        protected void SetMarked(int aColumn, int aRow, bool aIsMarked)
        {
            if (aColumn < mItems.Count && aRow < mItems[aColumn].Count) mItems[aColumn][aRow].SetMarked(aIsMarked);
        }

        // 上下は列の中で、左右は入れる列の間で動かす。端では止まる
        private void Move(Vector2Int aDirection)
        {
            if (aDirection.y != 0)
            {
                Focus(FocusColumn, FocusRow - aDirection.y);
                return;
            }

            int column = FocusColumn + aDirection.x;
            if (column >= 0 && column < mRowCounts.Count && CanFocusColumn(column)) Focus(column, FocusRow);
        }

        /* 問い合わせ */

        // 指定した列と行の文字を返す。検証や表示の確認に使う
        public string GetItemText(int aColumn, int aRow)
        {
            if (aColumn >= mItems.Count || aRow >= mRowCounts[aColumn]) return null;
            return mItems[aColumn][aRow].Text;
        }

        // 縦と横のうち大きく倒した方へ 1 マス分の向きを返す。上が +y
        private Vector2Int ReadNavigate()
        {
            if (mNavigateAction == null) return Vector2Int.zero;

            var value = mNavigateAction.action.ReadValue<Vector2>();
            if (value.magnitude < NavigateThreshold) return Vector2Int.zero;
            return Mathf.Abs(value.x) > Mathf.Abs(value.y)
                ? new Vector2Int(value.x > 0f ? 1 : -1, 0)
                : new Vector2Int(0, value.y > 0f ? 1 : -1);
        }

        /* ヘルパー */

        // ユニットの名前・レベル・属性を 1 行の文にする
        protected static string FormatUnit(PPUnitDefinition aUnit, int aLevel)
            => $"{aUnit.DisplayName}  Lv{aLevel}  {ToAttributeName(aUnit.TypeAttribute)}";

        private static string ToAttributeName(PPTypeAttribute aAttribute) => aAttribute switch
        {
            PPTypeAttribute.Fire => PPTypeAttributeDefinition.TypeFire,
            PPTypeAttribute.Water => PPTypeAttributeDefinition.TypeWater,
            PPTypeAttribute.Earth => PPTypeAttributeDefinition.TypeEarth,
            PPTypeAttribute.Shine => PPTypeAttributeDefinition.TypeShine,
            PPTypeAttribute.Dark => PPTypeAttributeDefinition.TypeDark,
            _ => PPTypeAttributeDefinition.TypeNormal,
        };

        private static bool WasPressed(InputActionReference aAction) => aAction != null && aAction.action.WasPressedThisFrame();
    }
}
