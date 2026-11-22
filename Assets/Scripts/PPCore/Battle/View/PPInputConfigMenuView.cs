/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPInputConfigMenuView.cs
 * @author hqrse
 * @date 2026/11/18
 * @brief バトル中やタイトルで開く設定画面(ゲーム・サウンド・キー設定のタブ)
 * =====================================*/

using System.Collections;
using System.Collections.Generic;
using AttributeUtility;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PPCore
{
    // 上のタブで「ゲーム」「サウンド」「キー設定」を切り替え、札を並べて設定を変える画面
    // キー設定では UI 操作 / ゲーム操作 と キーボード / コントローラー を切り替え、選んだ機器の 2 枠を並べる
    // 開いている間はバトルの時計(バトル中だけ)と物理を止める。動きは timeScale に関係なく unscaled の時間で進める
    public class PPInputConfigMenuView : MonoBehaviour
    {
        /* 内部型 */

        // 札の役割
        private enum SlotKind
        {
            Tab,
            // キー設定の UI 操作 / ゲーム操作 の切り替え
            CategorySwitch,
            // キー設定の キーボード / コントローラー の切り替え
            DeviceSwitch,
            Rebind,
            // 左右で値を変える札(AI モード・音量)
            Value,
            Reset,
            Close,
        }

        // 設定画面のタブ。並び順が画面の左からの順になる
        private enum Tab
        {
            Game = 0,
            Sound = 1,
            Key = 2,
        }

        // 左右で変える値の種類
        private enum ValueKind
        {
            AIStyle,
            MasterVolume,
            BgmVolume,
            SeVolume,
        }

        // 並べた札 1 枚分の役割と動きの状態
        private class SlotEntry
        {
            public PPInputConfigSlot Slot;
            public SlotKind Kind;
            // タブの札のタブ
            public Tab Tab;
            // 値の札が変える値
            public ValueKind Value;
            // 割り当ての札の操作と枠
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
        private static readonly string[] TabNames = { "ゲーム", "サウンド", "キー設定" };

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
        [Label("札のプレハブ")]
        [SerializeField] private PPInputConfigSlot mSlotPrefab;
        // 行の名前に使う文字のひな形。複製して使う
        [Label("行の名前のひな形")]
        [SerializeField] private TMP_Text mRowLabelTemplate;
        [Label("並べる場所")]
        [SerializeField] private RectTransform mGrid;
        [Label("行の名前の位置(横)")]
        [SerializeField] private float mLabelX = -230f;
        // キー設定で並べる 2 枠の横の位置
        [Label("割り当ての枠の位置(横)", true)]
        [SerializeField] private float[] mColumnX = { 20f, 170f };
        // ゲーム・サウンドで値を変える札の横の位置
        [Label("値の札の位置(横)")]
        [SerializeField] private float mValueX = 100f;
        // タブの下の、設定を並べ始める行の位置
        [Label("最初の行の位置(縦)")]
        [SerializeField] private float mFirstRowY = 100f;
        [Label("行の間隔")]
        [SerializeField] private float mRowSpacing = 44f;
        [Label("タブの位置(縦)")]
        [SerializeField] private float mTabY = 150f;
        // 左から ゲーム・サウンド・キー設定
        [Label("タブの位置(横)", true)]
        [SerializeField] private float[] mTabX = { -160f, 0f, 160f };
        // 一番下の「既定に戻す」「閉じる」の横の位置
        [Label("下のボタンの位置(横)", true)]
        [SerializeField] private float[] mFooterX = { -90f, 90f };
        [Label("下のボタンの位置(縦)")]
        [SerializeField] private float mFooterY = -230f;
        // 文字が長いため、割り当ての枠より大きく表示する。タブもこの大きさにする
        [Label("下のボタンの大きさ")]
        [Min(0.1f)]
        [SerializeField] private float mFooterScale = 1.4f;
        // キー設定の 分類 と 機器 の切り替えの札の横の位置
        [Label("切り替えの札の位置(横)", true)]
        [SerializeField] private float[] mSwitchX = { -110f, 110f };
        // 値の札とキー設定の切り替えの札の大きさ。行の間隔に収まるよう、下のボタンより小さくする
        [Label("値と切り替えの札の大きさ")]
        [Min(0.1f)]
        [SerializeField] private float mValueScale = 1.15f;

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

        // 札の行。先頭がタブ、最後が「既定に戻す」「閉じる」、その間がタブの中身
        private readonly List<SlotEntry[]> mRows = new();
        private SlotEntry[] mTabRow;
        private SlotEntry[] mFooterRow;
        // タブの中身の札と行の名前。タブや切り替えを変えるときに消す
        private readonly List<SlotEntry> mContentSlots = new();
        private readonly List<TMP_Text> mRowLabels = new();
        private Tab mTab = Tab.Game;
        private PPRebindCategory mCategory = PPRebindCategory.UI;
        private bool mIsGamepad;
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
        // 最後に閉じたフレーム。閉じたキーでほかの操作まで動かないよう、呼び出し側がそのフレームの入力を捨てる
        public int ClosedFrame { get; private set; } = -1;

        /* Unity イベント */

        // 保存されている割り当てを読み込み、タブと下のボタンを並べておく
        private void Awake()
        {
            if (mActions != null) PPInputBindingStore.Load(mActions);
            BuildFrame();
            mPanel.SetActive(false);
        }

        // 選択中の拡大・浮き上がりと、開いたときに落ちてくる動きを反映する
        private void Update()
        {
            if (!mIsOpen) return;
            mOpenElapsed += Time.unscaledDeltaTime;
            float follow = 1f - Mathf.Exp(-mFocusFollowSpeed * Time.unscaledDeltaTime);
            for (int row = 0; row < mRows.Count; row++)
            {
                for (int column = 0; column < mRows[row].Length; column++)
                {
                    var entry = mRows[row][column];
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

        // 上にタブ、一番下に「既定に戻す」「閉じる」を置く。中身はタブを開くときに並べる
        private void BuildFrame()
        {
            if (mSlotPrefab == null) return;
            mTabRow = new SlotEntry[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
            {
                mTabRow[i] = CreateEntry(new Vector2(mTabX[i], mTabY), i, SlotKind.Tab, mFooterScale);
                mTabRow[i].Tab = (Tab)i;
            }
            mFooterRow = new[]
            {
                CreateEntry(new Vector2(mFooterX[0], mFooterY), 8, SlotKind.Reset, mFooterScale),
                CreateEntry(new Vector2(mFooterX[1], mFooterY), 9, SlotKind.Close, mFooterScale),
            };
            mFooterRow[0].Slot.SetText(ResetText);
            mFooterRow[1].Slot.SetText(CloseText);
        }

        // 今のタブの中身を並べ直す。前に並べていた中身は消す
        // 落ちてくる動きは、開いてからの経過時間が aBaseDelay 秒になってから始める
        private void BuildContent(float aBaseDelay)
        {
            foreach (var entry in mContentSlots) Destroy(entry.Slot.gameObject);
            foreach (var label in mRowLabels) Destroy(label.gameObject);
            mContentSlots.Clear();
            mRowLabels.Clear();
            mRows.Clear();
            mRows.Add(mTabRow);

            switch (mTab)
            {
                case Tab.Game:
                    AddValueRow(0, "オートのAIモード", ValueKind.AIStyle, aBaseDelay);
                    break;
                case Tab.Sound:
                    AddValueRow(0, "全体の音量", ValueKind.MasterVolume, aBaseDelay);
                    AddValueRow(1, "BGMの音量", ValueKind.BgmVolume, aBaseDelay);
                    AddValueRow(2, "SEの音量", ValueKind.SeVolume, aBaseDelay);
                    break;
                case Tab.Key:
                    BuildKeyContent(aBaseDelay);
                    break;
            }
            mRows.Add(mFooterRow);
        }

        // 切り替えの札の行と、今の分類の操作ごとに選んだ機器の 2 枠を並べる
        private void BuildKeyContent(float aBaseDelay)
        {
            mRows.Add(new[]
            {
                AddContentEntry(new Vector2(mSwitchX[0], mFirstRowY), 0, SlotKind.CategorySwitch, mValueScale, aBaseDelay),
                AddContentEntry(new Vector2(mSwitchX[1], mFirstRowY), 1, SlotKind.DeviceSwitch, mValueScale, aBaseDelay),
            });
            if (mActions == null) return;

            int row = 1;
            foreach (var rebindable in PPInputBindingStore.RebindableActions)
            {
                if (rebindable.Category != mCategory) continue;
                float y = mFirstRowY - row * mRowSpacing;
                AddRowLabel(rebindable.DisplayName, y);

                var action = PPInputBindingStore.FindAction(mActions, rebindable.ActionName);
                var entries = new SlotEntry[PPInputBindingStore.SlotCount];
                for (int slot = 0; slot < entries.Length; slot++)
                {
                    entries[slot] = AddContentEntry(new Vector2(mColumnX[slot], y), row + slot, SlotKind.Rebind, 1f, aBaseDelay);
                    entries[slot].Action = action;
                    entries[slot].IsGamepad = mIsGamepad;
                    entries[slot].SlotIndex = slot;
                }
                mRows.Add(entries);
                row++;
            }
        }

        private void AddValueRow(int aRow, string aName, ValueKind aValue, float aBaseDelay)
        {
            float y = mFirstRowY - aRow * mRowSpacing;
            AddRowLabel(aName, y);
            var entry = AddContentEntry(new Vector2(mValueX, y), aRow, SlotKind.Value, mValueScale, aBaseDelay);
            entry.Value = aValue;
            mRows.Add(new[] { entry });
        }

        private void AddRowLabel(string aText, float aY)
        {
            var label = Instantiate(mRowLabelTemplate, mGrid);
            label.gameObject.SetActive(true);
            label.text = aText;
            label.rectTransform.anchoredPosition = new Vector2(mLabelX, aY);
            mRowLabels.Add(label);
        }

        private SlotEntry AddContentEntry(Vector2 aPosition, int aOrder, SlotKind aKind, float aScale, float aBaseDelay)
        {
            var entry = CreateEntry(aPosition, aOrder, aKind, aScale);
            entry.Delay += aBaseDelay;
            mContentSlots.Add(entry);
            return entry;
        }

        // 札を 1 枚置く。落ちてくる順番は左上から斜めに並ぶ
        private SlotEntry CreateEntry(Vector2 aPosition, int aOrder, SlotKind aKind, float aScale)
        {
            var slot = Instantiate(mSlotPrefab, mGrid);
            ((RectTransform)slot.transform).anchoredPosition = aPosition;
            slot.transform.localScale = Vector3.one * aScale;
            slot.SetFocused(false);
            return new SlotEntry { Slot = slot, Kind = aKind, Delay = aOrder * mDropStagger };
        }

        /* 操作 */

        // 開いていれば閉じ、閉じていれば開く。入力待ちの最中と、入力待ちが終わったフレームは何もしない
        public void Toggle(BattleManager aManager = null)
        {
            if (IsRebinding || mRebindEndFrame == Time.frameCount) return;
            if (mIsOpen) Close();
            else Open(aManager);
        }

        // 画面を開き、物理と、バトル中ならバトルの時計を止めて、入力を受け付ける。バトルの外では aManager を省く
        public void Open(BattleManager aManager = null)
        {
            if (mIsOpen) return;
            mIsOpen = true;
            mManager = aManager;
            mManager?.Context?.Clock.Pause();
            Time.timeScale = 0f;

            mPanel.SetActive(true);
            mOpenElapsed = 0f;
            BuildContent(0f);
            mFocusRow = 0;
            mFocusColumn = (int)mTab;
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
            ClosedFrame = Time.frameCount;
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

        // カーソルを動かす。行は上下で輪になり、行をまたぐときは列を行の札の数に収める
        // タブの行では左右でタブを、値の札では左右で値を変える
        private void Move(int aColumnDelta, int aRowDelta)
        {
            if (aRowDelta != 0)
            {
                mFocusRow = (mFocusRow + aRowDelta + mRows.Count) % mRows.Count;
                mFocusColumn = Mathf.Min(mFocusColumn, mRows[mFocusRow].Length - 1);
            }
            if (aColumnDelta != 0)
            {
                var focused = GetFocused();
                if (focused.Kind == SlotKind.Tab)
                {
                    SelectTab((Tab)((((int)mTab + aColumnDelta) % TabNames.Length + TabNames.Length) % TabNames.Length));
                    return;
                }
                if (focused.Kind == SlotKind.Value)
                {
                    ChangeValue(focused.Value, aColumnDelta);
                    return;
                }
                int columns = mRows[mFocusRow].Length;
                mFocusColumn = (mFocusColumn + aColumnDelta + columns) % columns;
            }
            RefreshFocus();
        }

        // タブを切り替え、その中身を並べ直す。選択はタブの行に残す
        private void SelectTab(Tab aTab)
        {
            mTab = aTab;
            BuildContent(mOpenElapsed);
            mFocusRow = 0;
            mFocusColumn = (int)mTab;
            RefreshAll();
        }

        // キー設定の 分類 か 機器 を切り替え、割り当ての行を並べ直す。選択は切り替えの札に残す
        private void ToggleKeySwitch(SlotKind aKind)
        {
            if (aKind == SlotKind.CategorySwitch) mCategory = mCategory == PPRebindCategory.UI ? PPRebindCategory.Game : PPRebindCategory.UI;
            else mIsGamepad = !mIsGamepad;
            BuildContent(mOpenElapsed);
            mFocusRow = 1;
            mFocusColumn = aKind == SlotKind.CategorySwitch ? 0 : 1;
            RefreshAll();
        }

        // 値を 1 段変える。AI モードは型を順に、音量は 1 ずつ変える
        private void ChangeValue(ValueKind aValue, int aDelta)
        {
            switch (aValue)
            {
                case ValueKind.AIStyle:
                    var style = PPGameSettings.GetAIStyle(GetDefaultAIStyle());
                    int count = System.Enum.GetValues(typeof(PPCoinDropStyle)).Length;
                    // バランス → コイン重視 → アイテム重視 の順に回す(左は逆回り)
                    for (int i = 0; i < (aDelta > 0 ? 1 : count - 1); i++) style = PPCoinDropStyleUtility.Next(style);
                    PPGameSettings.SetAIStyle(style);
                    break;
                default:
                    var kind = ToVolumeKind(aValue);
                    PPGameSettings.SetVolume(kind, PPGameSettings.GetVolume(kind) + aDelta);
                    break;
            }
            RefreshAll();
        }

        // 今のタブの設定を既定に戻す
        private void ResetCurrentTab()
        {
            switch (mTab)
            {
                case Tab.Game: PPGameSettings.ResetGame(); break;
                case Tab.Sound: PPGameSettings.ResetSound(); break;
                case Tab.Key: PPInputBindingStore.ResetToDefault(mActions); break;
            }
            RefreshAll();
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

        // すべての札の文字を今の設定に合わせ、選択中の光を付け直す
        private void RefreshAll()
        {
            foreach (var entries in mRows)
            {
                foreach (var entry in entries) RefreshText(entry);
            }
            RefreshFocus();
        }

        private void RefreshText(SlotEntry aEntry)
        {
            switch (aEntry.Kind)
            {
                case SlotKind.Tab:
                    // 開いているタブは普通の文字、ほかのタブは薄くする
                    aEntry.Slot.SetText(TabNames[(int)aEntry.Tab], aEntry.Tab != mTab);
                    break;
                case SlotKind.CategorySwitch:
                    aEntry.Slot.SetText($"◀ {PPInputBindingStore.GetCategoryName(mCategory)} ▶");
                    break;
                case SlotKind.DeviceSwitch:
                    aEntry.Slot.SetText(mIsGamepad ? "◀ コントローラー ▶" : "◀ キーボード ▶");
                    break;
                case SlotKind.Rebind:
                    int index = PPInputBindingStore.GetBindingIndex(aEntry.Action, aEntry.IsGamepad, aEntry.SlotIndex);
                    string path = index >= 0 ? aEntry.Action.bindings[index].effectivePath : string.Empty;
                    aEntry.Slot.SetText(PPInputBindingStore.GetDisplayName(path), string.IsNullOrEmpty(path));
                    break;
                case SlotKind.Value:
                    aEntry.Slot.SetText($"◀ {DescribeValue(aEntry.Value)} ▶");
                    break;
            }
        }

        // 選択中の光を今のカーソル位置に合わせる
        private void RefreshFocus()
        {
            for (int row = 0; row < mRows.Count; row++)
            {
                for (int column = 0; column < mRows[row].Length; column++)
                {
                    mRows[row][column].Slot.SetFocused(row == mFocusRow && column == mFocusColumn);
                }
            }
        }

        private string DescribeValue(ValueKind aValue) => aValue switch
        {
            ValueKind.AIStyle => PPCoinDropStyleUtility.GetName(PPGameSettings.GetAIStyle(GetDefaultAIStyle())),
            _ => PPGameSettings.GetVolume(ToVolumeKind(aValue)).ToString(),
        };

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

        // 札の役割に応じて、切り替え・値の変更・入力待ち・既定に戻す・閉じる を行う
        private void HandleSubmit(InputAction.CallbackContext aContext)
        {
            if (IsRebinding || mRebindEndFrame == Time.frameCount) return;
            var focused = GetFocused();
            switch (focused.Kind)
            {
                case SlotKind.CategorySwitch:
                case SlotKind.DeviceSwitch:
                    ToggleKeySwitch(focused.Kind);
                    break;
                case SlotKind.Value:
                    ChangeValue(focused.Value, 1);
                    break;
                case SlotKind.Rebind:
                    StartCoroutine(RebindRoutine(focused));
                    break;
                case SlotKind.Reset:
                    ResetCurrentTab();
                    break;
                case SlotKind.Close:
                    Close();
                    break;
            }
        }

        // キャンセルで画面を閉じる。入力待ちの最中は何もしない
        private void HandleCancel(InputAction.CallbackContext aContext)
        {
            if (IsRebinding || mRebindEndFrame == Time.frameCount) return;
            Close();
        }

        /* ヘルパー */

        private SlotEntry GetFocused() => mRows[mFocusRow][mFocusColumn];

        private static PPVolumeKind ToVolumeKind(ValueKind aValue) => aValue switch
        {
            ValueKind.BgmVolume => PPVolumeKind.Bgm,
            ValueKind.SeVolume => PPVolumeKind.Se,
            _ => PPVolumeKind.Master,
        };

        // 保存した AI モードが無いときの型。PPRuntimeSettings の既定に合わせる
        private static PPCoinDropStyle GetDefaultAIStyle()
        {
            var defaults = PPRuntimeSettings.Current.CoinDropAIDefaults;
            return defaults != null ? defaults.PlayerStyle : PPCoinDropStyle.Balance;
        }

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
