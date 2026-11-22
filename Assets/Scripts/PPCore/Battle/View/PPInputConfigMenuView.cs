/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPInputConfigMenuView.cs
 * @author hqrse
 * @date 2026/11/18
 * @brief バトル中やタイトルで開く設定画面(ゲーム・サウンド・キー設定のタブと表)
 * =====================================*/

using System.Collections;
using System.Collections.Generic;
using AttributeUtility;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PPCore
{
    // 上のタブで「ゲーム」「サウンド」「キー設定」を切り替え、その中身を表の行で並べる設定画面
    // キー設定では UI 操作 / ゲーム操作 と キーボード / コントローラー を選び、選んだ組み合わせの操作の 2 枠を並べる
    // 開いている間はバトルの時計(バトル中だけ)と物理を止める
    public class PPInputConfigMenuView : MonoBehaviour
    {
        /* 内部型 */

        // ボタンの役割
        private enum CellKind
        {
            Tab,
            // キー設定の UI 操作 / ゲーム操作
            Category,
            // キー設定の キーボード / コントローラー
            Device,
            Rebind,
            // 左右で値を変える(AI モード・音量)
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

        // 並べたボタン 1 つ分の役割
        private class Cell
        {
            public PPSettingsButton Button;
            public CellKind Kind;
            public Tab Tab;
            public PPRebindCategory Category;
            public bool IsGamepad;
            public ValueKind Value;
            // 割り当てのボタンの操作と枠
            public InputAction Action;
            public int SlotIndex;
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
        [Label("ボタンのプレハブ")]
        [SerializeField] private PPSettingsButton mButtonPrefab;
        [Label("表の行のプレハブ")]
        [SerializeField] private PPSettingsRow mRowPrefab;
        [Label("タブを並べる場所")]
        [SerializeField] private RectTransform mTabContainer;
        // キー設定のときだけ表示する、分類と機器の切り替え
        [Label("キー設定の切り替え")]
        [SerializeField] private GameObject mKeySwitchArea;
        [Label("分類を並べる場所")]
        [SerializeField] private RectTransform mCategoryContainer;
        [Label("機器を並べる場所")]
        [SerializeField] private RectTransform mDeviceContainer;
        // 選んでいる組み合わせ(例: ゲーム操作 / コントローラー)を出す。未設定なら出さない
        [Label("切り替えのまとめ")]
        [SerializeField] private TMP_Text mSwitchSummary;
        // キー設定のときだけ表示する、表の見出し(操作 / 割り当て 1 / 割り当て 2)
        [Label("表の見出し")]
        [SerializeField] private GameObject mTableHeader;
        [Label("表の行を並べる場所")]
        [SerializeField] private RectTransform mRowContainer;
        [Label("下のボタンを並べる場所")]
        [SerializeField] private RectTransform mFooterContainer;

        [Header("大きさ")]
        [Label("タブの大きさ")]
        [SerializeField] private Vector2 mTabSize = new(165f, 58f);
        [Label("切り替えの大きさ")]
        [SerializeField] private Vector2 mSwitchSize = new(262f, 58f);
        [Label("割り当ての枠の大きさ")]
        [SerializeField] private Vector2 mCellSize = new(230f, 54f);
        [Label("値の切り替えの大きさ")]
        [SerializeField] private Vector2 mValueSize = new(500f, 54f);
        [Label("下のボタンの大きさ")]
        [SerializeField] private Vector2 mFooterSize = new(230f, 52f);

        /* 状態 */

        // ボタンの行。先頭がタブ、最後が「既定に戻す」「閉じる」、その間がタブの中身
        private readonly List<Cell[]> mRows = new();
        // ボタンの行ごとの表の行。表の行でなければ null
        private readonly List<PPSettingsRow> mRowViews = new();
        private Cell[] mTabCells;
        private Cell[] mSwitchCells;
        private Cell[] mFooterCells;
        // 今のタブで並べた表の行。タブや切り替えを変えるときに消す
        private readonly List<PPSettingsRow> mContentRows = new();
        private Tab mTab = Tab.Game;
        private PPRebindCategory mCategory = PPRebindCategory.UI;
        private bool mIsGamepad;
        private BattleManager mManager;
        private bool mIsOpen;
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

        // 保存されている割り当てを読み込み、タブ・切り替え・下のボタンを並べておく
        private void Awake()
        {
            if (mActions != null) PPInputBindingStore.Load(mActions);
            BuildFrame();
            mPanel.SetActive(false);
        }

        // 破棄時に開いていれば閉じ、止めていた時計を戻す
        private void OnDestroy()
        {
            if (mIsOpen) Close();
        }

        /* 初期化・接続 */

        // タブ・キー設定の切り替え・下のボタンを並べる。中身の表は開くときに並べる
        private void BuildFrame()
        {
            if (mButtonPrefab == null) return;
            mTabCells = new Cell[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
            {
                mTabCells[i] = CreateCell(mTabContainer, mTabSize, CellKind.Tab);
                mTabCells[i].Tab = (Tab)i;
            }

            mSwitchCells = new[]
            {
                CreateCell(mCategoryContainer, mSwitchSize, CellKind.Category),
                CreateCell(mCategoryContainer, mSwitchSize, CellKind.Category),
                CreateCell(mDeviceContainer, mSwitchSize, CellKind.Device),
                CreateCell(mDeviceContainer, mSwitchSize, CellKind.Device),
            };
            mSwitchCells[0].Category = PPRebindCategory.UI;
            mSwitchCells[1].Category = PPRebindCategory.Game;
            mSwitchCells[2].IsGamepad = false;
            mSwitchCells[3].IsGamepad = true;
            mSwitchCells[0].Button.SetText(PPInputBindingStore.GetCategoryName(PPRebindCategory.UI));
            mSwitchCells[1].Button.SetText(PPInputBindingStore.GetCategoryName(PPRebindCategory.Game));
            mSwitchCells[2].Button.SetText("キーボード");
            mSwitchCells[3].Button.SetText("コントローラー");

            mFooterCells = new[]
            {
                CreateCell(mFooterContainer, mFooterSize, CellKind.Reset),
                CreateCell(mFooterContainer, mFooterSize, CellKind.Close),
            };
            mFooterCells[0].Button.SetText(ResetText);
            mFooterCells[1].Button.SetText(CloseText);
        }

        // 今のタブの中身を並べ直す。前に並べていた表の行は消す
        private void BuildContent()
        {
            foreach (var row in mContentRows) Destroy(row.gameObject);
            mContentRows.Clear();
            mRows.Clear();
            mRowViews.Clear();
            AddRow(mTabCells, null);

            bool isKey = mTab == Tab.Key;
            if (mKeySwitchArea != null) mKeySwitchArea.SetActive(isKey);
            if (mTableHeader != null) mTableHeader.SetActive(isKey);

            switch (mTab)
            {
                case Tab.Game:
                    AddValueRow("オートのAIモード", ValueKind.AIStyle);
                    break;
                case Tab.Sound:
                    AddValueRow("全体の音量", ValueKind.MasterVolume);
                    AddValueRow("BGMの音量", ValueKind.BgmVolume);
                    AddValueRow("SEの音量", ValueKind.SeVolume);
                    break;
                case Tab.Key:
                    AddRow(mSwitchCells, null);
                    AddRebindRows();
                    break;
            }
            AddRow(mFooterCells, null);
        }

        // 今の分類の操作ごとに、選んだ機器の 2 枠を並べる
        private void AddRebindRows()
        {
            if (mActions == null) return;
            foreach (var rebindable in PPInputBindingStore.RebindableActions)
            {
                if (rebindable.Category != mCategory) continue;
                var row = CreateRow(rebindable.DisplayName);
                var action = PPInputBindingStore.FindAction(mActions, rebindable.ActionName);
                var cells = new Cell[PPInputBindingStore.SlotCount];
                for (int slot = 0; slot < cells.Length; slot++)
                {
                    cells[slot] = CreateCell(row.CellContainer, mCellSize, CellKind.Rebind);
                    cells[slot].Action = action;
                    cells[slot].IsGamepad = mIsGamepad;
                    cells[slot].SlotIndex = slot;
                }
                AddRow(cells, row);
            }
        }

        private void AddValueRow(string aName, ValueKind aValue)
        {
            var row = CreateRow(aName);
            var cell = CreateCell(row.CellContainer, mValueSize, CellKind.Value);
            cell.Value = aValue;
            AddRow(new[] { cell }, row);
        }

        private void AddRow(Cell[] aCells, PPSettingsRow aRow)
        {
            mRows.Add(aCells);
            mRowViews.Add(aRow);
        }

        private PPSettingsRow CreateRow(string aName)
        {
            var row = Instantiate(mRowPrefab, mRowContainer);
            row.SetText(aName);
            mContentRows.Add(row);
            return row;
        }

        private Cell CreateCell(RectTransform aParent, Vector2 aSize, CellKind aKind)
        {
            var button = Instantiate(mButtonPrefab, aParent);
            ((RectTransform)button.transform).sizeDelta = aSize;
            button.SetFocused(false);
            button.SetSelected(false);
            return new Cell { Button = button, Kind = aKind };
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
            BuildContent();
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

        // カーソルを動かす。行は上下で輪になり、行をまたぐときは列を行のボタンの数に収める
        // タブの行では左右でタブを、値のボタンでは左右で値を変える
        private void Move(int aColumnDelta, int aRowDelta)
        {
            if (aRowDelta != 0)
            {
                mFocusRow = (mFocusRow + aRowDelta + mRows.Count) % mRows.Count;
                // タブの行へ戻ったときは、開いているタブにカーソルを置く
                mFocusColumn = mFocusRow == 0 ? (int)mTab : Mathf.Min(mFocusColumn, mRows[mFocusRow].Length - 1);
            }
            if (aColumnDelta != 0)
            {
                var focused = GetFocused();
                if (focused.Kind == CellKind.Tab)
                {
                    SelectTab((Tab)((((int)mTab + aColumnDelta) % TabNames.Length + TabNames.Length) % TabNames.Length));
                    return;
                }
                if (focused.Kind == CellKind.Value)
                {
                    ChangeValue(focused.Value, aColumnDelta);
                    return;
                }
                int columns = mRows[mFocusRow].Length;
                mFocusColumn = (mFocusColumn + aColumnDelta + columns) % columns;
            }
            RefreshFocus();
        }

        // タブを切り替え、その中身を並べ直す。カーソルはタブの行に残す
        private void SelectTab(Tab aTab)
        {
            mTab = aTab;
            BuildContent();
            mFocusRow = 0;
            mFocusColumn = (int)mTab;
            RefreshAll();
        }

        // キー設定の分類か機器を、カーソルのあるボタンのものにして、割り当ての行を並べ直す。カーソルはそのボタンに残す
        private void SelectKeySwitch(Cell aCell)
        {
            if (aCell.Kind == CellKind.Category) mCategory = aCell.Category;
            else mIsGamepad = aCell.IsGamepad;
            int column = mFocusColumn;
            BuildContent();
            mFocusRow = 1;
            mFocusColumn = column;
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
        private IEnumerator RebindRoutine(Cell aCell)
        {
            int bindingIndex = PPInputBindingStore.GetBindingIndex(aCell.Action, aCell.IsGamepad, aCell.SlotIndex);
            if (bindingIndex < 0) yield break;
            aCell.Button.SetText(WaitingText);
            aCell.Button.SetSelected(true);
            // 待っている間に決定やキャンセルが重ならないよう、先に入力待ちの扱いにしておく
            mIsRebindPending = true;
            // 押したままのキーは入力待ちで拾われなくなるため、決定のキーを離してから始める
            yield return new WaitUntil(() => !mSubmitAction.action.IsPressed());
            yield return new WaitForSecondsRealtime(0.05f);

            mIsRebindPending = false;
            mRebindOperation = PPInputBindingStore.StartRebind(mActions, aCell.Action, bindingIndex, aCell.IsGamepad, mRebindTimeout, () =>
            {
                mRebindOperation = null;
                mRebindEndFrame = Time.frameCount;
                RefreshAll();
            });
        }

        /* 表示 */

        // すべてのボタンの文字と塗りを今の設定に合わせ、カーソルを付け直す
        private void RefreshAll()
        {
            foreach (var cells in mRows)
            {
                foreach (var cell in cells) RefreshCell(cell);
            }
            if (mSwitchSummary != null)
            {
                mSwitchSummary.text = $"{PPInputBindingStore.GetCategoryName(mCategory)} / {(mIsGamepad ? "コントローラー" : "キーボード")}";
            }
            RefreshFocus();
        }

        private void RefreshCell(Cell aCell)
        {
            switch (aCell.Kind)
            {
                case CellKind.Tab:
                    aCell.Button.SetText(TabNames[(int)aCell.Tab]);
                    aCell.Button.SetSelected(aCell.Tab == mTab);
                    break;
                case CellKind.Category:
                    aCell.Button.SetSelected(aCell.Category == mCategory);
                    break;
                case CellKind.Device:
                    aCell.Button.SetSelected(aCell.IsGamepad == mIsGamepad);
                    break;
                case CellKind.Rebind:
                    int index = PPInputBindingStore.GetBindingIndex(aCell.Action, aCell.IsGamepad, aCell.SlotIndex);
                    string path = index >= 0 ? aCell.Action.bindings[index].effectivePath : string.Empty;
                    aCell.Button.SetText(PPInputBindingStore.GetDisplayName(path), string.IsNullOrEmpty(path));
                    aCell.Button.SetSelected(false);
                    break;
                case CellKind.Value:
                    aCell.Button.SetText($"‹  {DescribeValue(aCell.Value)}  ›");
                    break;
            }
        }

        // カーソルのあるボタンを枠で囲み、そのボタンのある表の行を目立たせる
        private void RefreshFocus()
        {
            for (int row = 0; row < mRows.Count; row++)
            {
                for (int column = 0; column < mRows[row].Length; column++)
                {
                    mRows[row][column].Button.SetFocused(row == mFocusRow && column == mFocusColumn);
                }
                if (mRowViews[row] != null) mRowViews[row].SetFocused(row == mFocusRow);
            }
        }

        private string DescribeValue(ValueKind aValue) => aValue switch
        {
            ValueKind.AIStyle => PPCoinDropStyleUtility.GetName(PPGameSettings.GetAIStyle(GetDefaultAIStyle())),
            _ => $"{PPGameSettings.GetVolume(ToVolumeKind(aValue))} / {PPGameSettings.VolumeMax}",
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

        // ボタンの役割に応じて、切り替え・値の変更・入力待ち・既定に戻す・閉じる を行う
        private void HandleSubmit(InputAction.CallbackContext aContext)
        {
            if (IsRebinding || mRebindEndFrame == Time.frameCount) return;
            var focused = GetFocused();
            switch (focused.Kind)
            {
                case CellKind.Category:
                case CellKind.Device:
                    SelectKeySwitch(focused);
                    break;
                case CellKind.Value:
                    ChangeValue(focused.Value, 1);
                    break;
                case CellKind.Rebind:
                    StartCoroutine(RebindRoutine(focused));
                    break;
                case CellKind.Reset:
                    ResetCurrentTab();
                    break;
                case CellKind.Close:
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

        private Cell GetFocused() => mRows[mFocusRow][mFocusColumn];

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
    }
}
