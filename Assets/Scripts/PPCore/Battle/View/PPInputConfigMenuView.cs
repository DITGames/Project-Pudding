/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPInputConfigMenuView.cs
 * @author hqrse
 * @date 2026/11/18
 * @brief バトル中に開くキー設定画面
 * =====================================*/

using System.Collections;
using System.Collections.Generic;
using AttributeUtility;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PPCore
{
    // 変更できる操作ごとに、キーボード 2 枠・ゲームパッド 2 枠のコインを並べるキー設定画面
    // 開いている間はバトルの時計と物理を止める。動きは timeScale に関係なく unscaled の時間で進める
    public class PPInputConfigMenuView : MonoBehaviour
    {
        /* 内部型 */

        // 並べたコイン 1 枚分の役割と動きの状態
        private class SlotEntry
        {
            public PPInputConfigSlot Slot;
            // 割り当ての枠なら操作。下の「既定に戻す」「閉じる」なら null
            public InputAction Action;
            public bool IsGamepad;
            public int SlotIndex;
            public float DropOffset;
            public float Delay;
            public float Scale = 1f;
            public float Lift;
        }

        /* 定数 */

        private const string ResetText = "既定に戻す";
        private const string CloseText = "閉じる";
        // 割り当ての入力待ちの間に出す文字
        private const string WaitingText = "入力待ち";
        // 列の数(キーボード 2 枠・ゲームパッド 2 枠)
        private const int ColumnCount = PPInputBindingStore.SlotCount * 2;

        /* インスペクタ設定 */

        [Header("入力")]
        // 割り当てを変更・保存する入力アクションの一式
        [Label("入力アクション")]
        [SerializeField] private InputActionAsset mActions;
        // 上下左右の移動。キー設定では変更できない UI の移動を使う
        [Label("移動の入力")]
        [SerializeField] private InputActionReference mNavigateAction;
        [Label("決定の入力")]
        [SerializeField] private InputActionReference mSubmitAction;
        [Label("キャンセルの入力")]
        [SerializeField] private InputActionReference mCancelAction;
        // 何も押さずにこの秒数が経つと、割り当ての変更を取り消す
        [Label("入力待ちの上限(秒)")]
        [Min(1f)]
        [SerializeField] private float mRebindTimeout = 5f;

        [Header("表示")]
        // 開いている間だけ表示する部分
        [Label("画面")]
        [SerializeField] private GameObject mPanel;
        [Label("コインのプレハブ")]
        [SerializeField] private PPInputConfigSlot mSlotPrefab;
        // 行の名前に使う文字のひな形。複製して使う
        [Label("行の名前のひな形")]
        [SerializeField] private TMP_Text mRowLabelTemplate;
        [Label("並べる場所")]
        [SerializeField] private RectTransform mGrid;
        [Label("行の名前の位置(横)")]
        [SerializeField] private float mLabelX = -230f;
        // 各列のコインの横の位置。左からキーボード 1・2、ゲームパッド 1・2
        [Label("列の位置(横)", true)]
        [SerializeField] private float[] mColumnX = { -40f, 60f, 170f, 270f };
        [Label("最初の行の位置(縦)")]
        [SerializeField] private float mFirstRowY = 110f;
        [Label("行の間隔")]
        [SerializeField] private float mRowSpacing = 56f;
        // 一番下の「既定に戻す」「閉じる」の横の位置
        [Label("下のボタンの位置(横)", true)]
        [SerializeField] private float[] mFooterX = { -90f, 90f };
        [Label("下のボタンの位置(縦)")]
        [SerializeField] private float mFooterY = -230f;
        // 文字が長いため、割り当ての枠より大きく表示する
        [Label("下のボタンの大きさ")]
        [Min(0.1f)]
        [SerializeField] private float mFooterScale = 1.4f;

        [Header("動き")]
        [Label("落ち始める高さ")]
        [SerializeField] private float mDropHeight = 400f;
        [Label("落ちる時間(秒)")]
        [Min(0.01f)]
        [SerializeField] private float mDropDuration = 0.4f;
        [Label("1列ごとの遅れ(秒)")]
        [Min(0f)]
        [SerializeField] private float mDropStagger = 0.04f;
        [Label("選択中の拡大率")]
        [Min(1f)]
        [SerializeField] private float mFocusScale = 1.15f;
        [Label("選択中の浮き上がり")]
        [SerializeField] private float mFocusLift = 6f;
        // 大きいほど早く目標の大きさ・高さに追いつく
        [Label("追従の速さ")]
        [Min(0.1f)]
        [SerializeField] private float mFocusFollowSpeed = 16f;

        /* 状態 */

        // 割り当ての枠。[行][列] の並び
        private readonly List<SlotEntry[]> mRows = new();
        // 一番下の「既定に戻す」「閉じる」
        private readonly List<SlotEntry> mFooter = new();
        private BattleManager mManager;
        private bool mIsOpen;
        // 開いてからの経過時間。落ちてくる動きに使う
        private float mOpenElapsed;
        private int mFocusRow;
        private int mFocusColumn;
        // 直前の移動入力の向き。倒し続けても 1 回だけ動かすために持つ
        private Vector2Int mLastNavigate;
        // 入力待ちの最中の操作。待っていなければ null
        private InputActionRebindingExtensions.RebindingOperation mRebindOperation;
        // 決定から入力待ちが始まるまでの間か
        private bool mIsRebindPending;
        // 入力待ちが終わったフレーム。終わらせたキーで画面まで閉じないよう、そのフレームの開閉を受け付けない
        private int mRebindEndFrame = -1;

        /* プロパティ */

        public bool IsOpen => mIsOpen;
        public bool IsRebinding => mRebindOperation != null || mIsRebindPending;

        /* Unity イベント */

        // 保存されている割り当てを読み込み、コインを並べておく
        private void Awake()
        {
            if (mActions != null) PPInputBindingStore.Load(mActions);
            BuildGrid();
            mPanel.SetActive(false);
        }

        // 選択中の拡大・浮き上がりと、開いたときに落ちてくる動きを反映する
        private void Update()
        {
            if (!mIsOpen) return;
            mOpenElapsed += Time.unscaledDeltaTime;
            float follow = 1f - Mathf.Exp(-mFocusFollowSpeed * Time.unscaledDeltaTime);
            for (int row = 0; row <= mRows.Count; row++)
            {
                int columns = row < mRows.Count ? ColumnCount : mFooter.Count;
                for (int column = 0; column < columns; column++)
                {
                    var entry = GetEntry(row, column);
                    bool isFocused = row == mFocusRow && column == mFocusColumn;
                    float t = Mathf.Clamp01((mOpenElapsed - entry.Delay) / mDropDuration);
                    entry.DropOffset = mDropHeight * (1f - EaseOutBounce(t));
                    entry.Scale = Mathf.Lerp(entry.Scale, isFocused ? mFocusScale : 1f, follow);
                    entry.Lift = Mathf.Lerp(entry.Lift, isFocused ? mFocusLift : 0f, follow);
                    // 入力待ちの枠は、待っていることが分かるよう脈打たせる
                    float pulse = isFocused && IsRebinding ? 1f + Mathf.Sin(Time.unscaledTime * 10f) * 0.06f : 1f;
                    entry.Slot.Body.anchoredPosition = new Vector2(0f, entry.DropOffset + entry.Lift);
                    entry.Slot.Body.localScale = Vector3.one * (entry.Scale * pulse);
                }
            }
        }

        // 破棄時に開いていれば閉じ、止めていた時計を戻す
        private void OnDestroy()
        {
            if (mIsOpen) Close();
        }

        /* 初期化・接続 */

        // 変更できる操作ごとに行の名前と 4 枠のコインを並べ、一番下に「既定に戻す」「閉じる」を置く
        private void BuildGrid()
        {
            if (mActions == null) return;
            for (int row = 0; row < PPInputBindingStore.RebindableActions.Length; row++)
            {
                var rebindable = PPInputBindingStore.RebindableActions[row];
                var action = PPInputBindingStore.FindAction(mActions, rebindable.ActionName);
                float y = mFirstRowY - row * mRowSpacing;

                var label = Instantiate(mRowLabelTemplate, mGrid);
                label.gameObject.SetActive(true);
                label.text = rebindable.DisplayName;
                label.rectTransform.anchoredPosition = new Vector2(mLabelX, y);

                var entries = new SlotEntry[ColumnCount];
                for (int column = 0; column < ColumnCount; column++)
                {
                    entries[column] = CreateEntry(new Vector2(mColumnX[column], y), row + column);
                    entries[column].Action = action;
                    entries[column].IsGamepad = column >= PPInputBindingStore.SlotCount;
                    entries[column].SlotIndex = column % PPInputBindingStore.SlotCount;
                }
                mRows.Add(entries);
            }

            int footerRow = mRows.Count;
            mFooter.Add(CreateEntry(new Vector2(mFooterX[0], mFooterY), footerRow));
            mFooter.Add(CreateEntry(new Vector2(mFooterX[1], mFooterY), footerRow + 1));
            mFooter[0].Slot.SetText(ResetText);
            mFooter[1].Slot.SetText(CloseText);
            foreach (var entry in mFooter) entry.Slot.transform.localScale = Vector3.one * mFooterScale;
        }

        // コインを 1 枚置く。落ちてくる順番は左上から斜めに並ぶ
        private SlotEntry CreateEntry(Vector2 aPosition, int aOrder)
        {
            var slot = Instantiate(mSlotPrefab, mGrid);
            ((RectTransform)slot.transform).anchoredPosition = aPosition;
            slot.SetFocused(false);
            return new SlotEntry { Slot = slot, Delay = aOrder * mDropStagger };
        }

        /* 操作 */

        // 開いていれば閉じ、閉じていれば開く。入力待ちの最中と、入力待ちが終わったフレームは何もしない
        public void Toggle(BattleManager aManager)
        {
            if (IsRebinding || mRebindEndFrame == Time.frameCount) return;
            if (mIsOpen) Close();
            else Open(aManager);
        }

        // 画面を開き、バトルの時計と物理を止めて、入力を受け付ける
        public void Open(BattleManager aManager)
        {
            if (mIsOpen) return;
            mIsOpen = true;
            mManager = aManager;
            mManager?.Context?.Clock.Pause();
            Time.timeScale = 0f;

            mPanel.SetActive(true);
            mOpenElapsed = 0f;
            mFocusRow = 0;
            mFocusColumn = 0;
            RefreshAll();

            mNavigateAction.action.performed += HandleNavigate;
            mNavigateAction.action.canceled += HandleNavigate;
            mSubmitAction.action.performed += HandleSubmit;
            mCancelAction.action.performed += HandleCancel;
            mNavigateAction.action.Enable();
            mSubmitAction.action.Enable();
            mCancelAction.action.Enable();
        }

        // 画面を閉じ、止めていた時計と物理を戻す
        public void Close()
        {
            if (!mIsOpen) return;
            mIsOpen = false;
            StopAllCoroutines();
            mIsRebindPending = false;
            mRebindOperation?.Cancel();

            mNavigateAction.action.performed -= HandleNavigate;
            mNavigateAction.action.canceled -= HandleNavigate;
            mSubmitAction.action.performed -= HandleSubmit;
            mCancelAction.action.performed -= HandleCancel;

            mPanel.SetActive(false);
            mManager?.Context?.Clock.Resume();
            Time.timeScale = 1f;
        }

        // カーソルを動かす。行をまたぐときは、下のボタンの列と割り当ての列を近い位置どうしで対応させる
        private void Move(int aColumnDelta, int aRowDelta)
        {
            int rowCount = mRows.Count + 1;
            if (aRowDelta != 0)
            {
                int nextRow = (mFocusRow + aRowDelta + rowCount) % rowCount;
                if (nextRow == mRows.Count) mFocusColumn = mFocusColumn < PPInputBindingStore.SlotCount ? 0 : 1;
                else if (mFocusRow == mRows.Count) mFocusColumn = mFocusColumn == 0 ? 0 : PPInputBindingStore.SlotCount;
                mFocusRow = nextRow;
            }
            if (aColumnDelta != 0)
            {
                int columns = mFocusRow < mRows.Count ? ColumnCount : mFooter.Count;
                mFocusColumn = (mFocusColumn + aColumnDelta + columns) % columns;
            }
            RefreshFocus();
        }

        // 選んでいる枠の入力待ちを始める。決定に使ったキーを拾わないよう、離されてから始める
        private IEnumerator RebindRoutine(SlotEntry aEntry)
        {
            int bindingIndex = PPInputBindingStore.GetBindingIndex(aEntry.Action, aEntry.IsGamepad, aEntry.SlotIndex);
            if (bindingIndex < 0) yield break;
            aEntry.Slot.SetText(WaitingText);
            // 待っている間に決定やキャンセルが重ならないよう、先に入力待ちの扱いにしておく
            mIsRebindPending = true;
            // 押したままのキーは入力待ちで拾われなくなるため、決定のキーを離してから始める
            yield return new WaitUntil(() => !mSubmitAction.action.IsPressed());
            yield return new WaitForSecondsRealtime(0.05f);

            mIsRebindPending = false;
            mRebindOperation = PPInputBindingStore.StartRebind(mActions, aEntry.Action, bindingIndex, aEntry.IsGamepad, mRebindTimeout, () =>
            {
                mRebindOperation = null;
                mRebindEndFrame = Time.frameCount;
                RefreshAll();
            });
        }

        /* 表示 */

        // すべての枠の文字を今の割り当てに合わせ、選択中の光を付け直す
        private void RefreshAll()
        {
            foreach (var entries in mRows)
            {
                foreach (var entry in entries)
                {
                    int index = PPInputBindingStore.GetBindingIndex(entry.Action, entry.IsGamepad, entry.SlotIndex);
                    string path = index >= 0 ? entry.Action.bindings[index].effectivePath : string.Empty;
                    entry.Slot.SetText(PPInputBindingStore.GetDisplayName(path), string.IsNullOrEmpty(path));
                }
            }
            RefreshFocus();
        }

        // 選択中の光を今のカーソル位置に合わせる
        private void RefreshFocus()
        {
            for (int row = 0; row <= mRows.Count; row++)
            {
                int columns = row < mRows.Count ? ColumnCount : mFooter.Count;
                for (int column = 0; column < columns; column++)
                {
                    GetEntry(row, column).Slot.SetFocused(row == mFocusRow && column == mFocusColumn);
                }
            }
        }

        /* イベントハンドラ */

        // 上下左右の入力でカーソルを動かす。倒した向きが変わったときだけ 1 つ動かす
        private void HandleNavigate(InputAction.CallbackContext aContext)
        {
            var value = aContext.canceled ? Vector2.zero : aContext.ReadValue<Vector2>();
            var direction = Vector2Int.zero;
            if (Mathf.Abs(value.x) > 0.5f && Mathf.Abs(value.x) >= Mathf.Abs(value.y)) direction.x = value.x > 0f ? 1 : -1;
            else if (Mathf.Abs(value.y) > 0.5f) direction.y = value.y > 0f ? -1 : 1;
            if (direction != Vector2Int.zero && direction != mLastNavigate && !IsRebinding) Move(direction.x, direction.y);
            mLastNavigate = direction;
        }

        // 割り当ての枠なら入力待ちを始め、下のボタンなら既定に戻すか閉じる
        private void HandleSubmit(InputAction.CallbackContext aContext)
        {
            if (IsRebinding || mRebindEndFrame == Time.frameCount) return;
            if (mFocusRow < mRows.Count)
            {
                StartCoroutine(RebindRoutine(mRows[mFocusRow][mFocusColumn]));
                return;
            }
            if (mFocusColumn == 0)
            {
                PPInputBindingStore.ResetToDefault(mActions);
                RefreshAll();
            }
            else
            {
                Close();
            }
        }

        // キャンセルで画面を閉じる。入力待ちの最中は何もしない
        private void HandleCancel(InputAction.CallbackContext aContext)
        {
            if (IsRebinding || mRebindEndFrame == Time.frameCount) return;
            Close();
        }

        /* ヘルパー */

        private SlotEntry GetEntry(int aRow, int aColumn) => aRow < mRows.Count ? mRows[aRow][aColumn] : mFooter[aColumn];

        // 床で跳ねるような減衰
        private static float EaseOutBounce(float aT)
        {
            const float n = 7.5625f;
            const float d = 2.75f;
            if (aT < 1f / d) return n * aT * aT;
            if (aT < 2f / d) return n * (aT -= 1.5f / d) * aT + 0.75f;
            if (aT < 2.5f / d) return n * (aT -= 2.25f / d) * aT + 0.9375f;
            return n * (aT -= 2.625f / d) * aT + 0.984375f;
        }
    }
}
