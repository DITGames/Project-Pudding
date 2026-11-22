/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageListMenuBase.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ステージマップの、札や行を並べて選ぶ画面の基底
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using AttributeUtility;

namespace PPCore
{
    // 選択中と、操作の途中で押さえている状態を表示できる、並べる要素
    public interface IPPStageFocusable
    {
        void SetFocused(bool aIsFocused);
        void SetMarked(bool aIsMarked);
    }

    // 要素を並べるまとまり。上から順に置き、まとまりの中は列数で折り返す格子として動く
    [Serializable]
    public sealed class PPStageListGroup
    {
        [Label("並べる場所")] public RectTransform Container;
        [Label("列数")] [Min(1)] public int Columns = 1;
    }

    // 要素をまとまりごとに並べ、上下左右の移動・決定・キャンセル・詳細で選ぶ画面の基底
    // 入力は新 Input System で受け、キーボードとゲームパッドの両方で動く。詳細はユニットを選んでいるときだけ開く
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
        // 未設定なら詳細は開けない
        [Label("詳細の入力")]
        [SerializeField] private InputActionReference mDetailAction;

        [Header("表示")]
        // 開いている間だけ表示する部分
        [Label("画面")]
        [SerializeField] private GameObject mPanel;
        // 上のまとまりから順に置く
        [Label("まとまり", true)]
        [SerializeField] private PPStageListGroup[] mGroups = new PPStageListGroup[0];
        // 未設定なら詳細は開けない
        [Label("ユニット詳細")]
        [SerializeField] private PPStageUnitDetailView mDetailView;
        [Label("ユニットの見た目カタログ")]
        [SerializeField] private PPUnitVisualCatalog mUnitVisualCatalog;

        /* 状態 */

        // まとまりごとの要素。表示しない要素も作り直さずに残す
        private readonly List<List<Component>> mItems = new();
        // まとまりごとの、表示している要素の数
        private readonly List<int> mCounts = new();
        private int mOpenedFrame = -1;
        private Vector2Int mLastNavigate;

        /* プロパティ */

        public bool IsOpen { get; private set; }
        // 最後に閉じたフレーム。閉じたキーでほかの操作まで動かないよう、呼び出し側がそのフレームの入力を捨てる
        public int ClosedFrame { get; private set; } = -1;
        public int FocusGroup { get; private set; }
        public int FocusIndex { get; private set; }
        public PPStageUnitDetailView DetailView => mDetailView;

        protected PPUnitVisualCatalog UnitVisualCatalog => mUnitVisualCatalog;

        /* Unity イベント */

        protected virtual void Awake()
        {
            if (mPanel != null) mPanel.SetActive(false);
        }

        // 開いたフレームと、詳細を開いている間・閉じたフレームは、開閉に使ったキーで動かないよう入力を受け付けない
        protected virtual void Update()
        {
            if (!IsOpen || Time.frameCount == mOpenedFrame) return;
            if (mDetailView != null && (mDetailView.IsOpen || mDetailView.ClosedFrame == Time.frameCount))
            {
                mLastNavigate = ReadNavigate();
                return;
            }

            var navigate = ReadNavigate();
            if (navigate != mLastNavigate && navigate != Vector2Int.zero) Move(navigate);
            mLastNavigate = navigate;

            if (WasPressed(mSubmitAction)) OnSubmit(FocusGroup, FocusIndex);
            else if (WasPressed(mCancelAction)) OnCancel();
            else if (WasPressed(mDetailAction)) OpenDetail();
        }

        /* 操作 */

        public virtual void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            ClosedFrame = Time.frameCount;
            if (mPanel != null) mPanel.SetActive(false);
        }

        // 選んでいるユニットの詳細を開く。ユニットを選んでいなければ何もしない
        public bool OpenDetail()
        {
            var owned = GetFocusedUnit(out int level);
            if (mDetailView == null || owned == null) return false;

            mDetailView.Open(owned, level);
            return true;
        }

        // 決定したとき(まとまり, まとまりの中の番号)
        protected abstract void OnSubmit(int aGroup, int aIndex);

        protected virtual void OnCancel() => Close();

        // 選択が移ったとき。選んでいるユニットの要約を出し直すのに使う
        protected virtual void OnFocusChanged() { }

        // 移動先のまとまりへ入れるか。入れないまとまりへは上下に動かない
        protected virtual bool CanFocusGroup(int aGroup) => true;

        // 詳細を開くユニットとそのレベル。ユニットを選んでいなければ null
        protected virtual PPUnitDefinition GetFocusedUnit(out int aLevel)
        {
            aLevel = 0;
            return null;
        }

        protected void OpenPanel()
        {
            IsOpen = true;
            mOpenedFrame = Time.frameCount;
            mLastNavigate = ReadNavigate();
            foreach (var action in new[] { mNavigateAction, mSubmitAction, mCancelAction, mDetailAction })
            {
                if (action != null) action.action.Enable();
            }
            if (mPanel != null) mPanel.SetActive(true);
        }

        // まとまりの要素を aCount 個にそろえて返す。足りない要素はプレハブから作り、余った要素は隠す
        protected List<T> PrepareItems<T>(int aGroup, T aPrefab, int aCount) where T : Component, IPPStageFocusable
        {
            while (mItems.Count <= aGroup)
            {
                mItems.Add(new List<Component>());
                mCounts.Add(0);
            }

            var items = mItems[aGroup];
            while (items.Count < aCount) items.Add(Instantiate(aPrefab, mGroups[aGroup].Container));

            var result = new List<T>(aCount);
            for (int i = 0; i < items.Count; i++)
            {
                items[i].gameObject.SetActive(i < aCount);
                if (i < aCount) result.Add((T)items[i]);
            }
            mCounts[aGroup] = aCount;
            return result;
        }

        // 選択を移す。番号はまとまりの要素の数に収める
        protected void Focus(int aGroup, int aIndex)
        {
            if (mCounts.Count == 0) return;
            FocusGroup = Mathf.Clamp(aGroup, 0, mCounts.Count - 1);
            FocusIndex = Mathf.Clamp(aIndex, 0, Mathf.Max(0, mCounts[FocusGroup] - 1));

            for (int g = 0; g < mItems.Count; g++)
            {
                for (int i = 0; i < mItems[g].Count; i++)
                {
                    ((IPPStageFocusable)mItems[g][i]).SetFocused(g == FocusGroup && i == FocusIndex);
                }
            }
            OnFocusChanged();
        }

        protected void SetMarked(int aGroup, int aIndex, bool aIsMarked)
        {
            if (aGroup < mItems.Count && aIndex < mItems[aGroup].Count) ((IPPStageFocusable)mItems[aGroup][aIndex]).SetMarked(aIsMarked);
        }

        // 左右はまとまりの行の中で、上下は行をまたいで動かす。まとまりの上下の端からは隣のまとまりへ移る
        private void Move(Vector2Int aDirection)
        {
            int columns = Mathf.Max(1, mGroups[FocusGroup].Columns);
            int column = FocusIndex % columns;

            if (aDirection.x != 0)
            {
                int next = FocusIndex + aDirection.x;
                bool isSameRow = next >= 0 && next < mCounts[FocusGroup] && next / columns == FocusIndex / columns;
                if (isSameRow) Focus(FocusGroup, next);
                return;
            }

            // 上が +y。画面の下へ動くと番号が増える
            int target = FocusIndex - aDirection.y * columns;
            if (target >= 0 && target < mCounts[FocusGroup])
            {
                Focus(FocusGroup, target);
                return;
            }

            int group = FocusGroup - aDirection.y;
            if (group < 0 || group >= mCounts.Count || mCounts[group] == 0 || !CanFocusGroup(group)) return;

            int groupColumns = Mathf.Max(1, mGroups[group].Columns);
            int lastRowStart = (mCounts[group] - 1) / groupColumns * groupColumns;
            int index = aDirection.y < 0 ? column : lastRowStart + column;
            Focus(group, Mathf.Min(index, mCounts[group] - 1));
        }

        /* 問い合わせ */

        // 指定した要素の文字を返す。検証や表示の確認に使う
        public string GetItemText(int aGroup, int aIndex)
        {
            if (aGroup >= mItems.Count || aIndex >= mCounts[aGroup]) return null;
            return mItems[aGroup][aIndex] switch
            {
                PPStageUnitCard card => card.Text,
                PPStageListItem item => item.Text,
                _ => null,
            };
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

        private static bool WasPressed(InputActionReference aAction) => aAction != null && aAction.action.WasPressedThisFrame();
    }
}
