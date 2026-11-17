/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPCoinMenuView.cs
 * @author hqrse
 * @date 2026/11/17
 * @brief コインを横に並べたコマンドメニュー（項目の定義・メニュー本体）
 * =====================================*/

using System;
using System.Collections;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PPCore
{
    // コイン型メニューに並べる 1 項目分の内容
    public class PPCoinMenuEntry
    {
        /* 状態 */

        public readonly Sprite Icon;
        public readonly string Label;
        // コストなどの補足。null か空なら表示しない
        public readonly string SubLabel;
        public readonly bool IsInteractable;

        /* 生成 */

        public PPCoinMenuEntry(Sprite aIcon, string aLabel, string aSubLabel = null, bool aIsInteractable = true)
        {
            Icon = aIcon;
            Label = aLabel;
            SubLabel = aSubLabel;
            IsInteractable = aIsInteractable;
        }
    }

    // 画面の決まった位置にコインを横一列に並べるメニュー。左右で選び、決定・キャンセルは呼び出し側が渡す
    // 入力中は timeScale が 0 になるため、動きはすべて unscaled の時間で進める
    public class PPCoinMenuView : MonoBehaviour
    {
        /* 内部型 */

        // 開くときのコインの落とし方
        private enum DropPattern
        {
            LeftToRight,
            RightToLeft,
            CenterOut,
            Scatter,
        }

        // 1 枚分の動きの状態。Update でまとめて見た目へ反映する
        private class ItemMotion
        {
            public PPCoinMenuItem Item;
            public Vector2 BasePosition;
            public float DropOffset;
            public float Lift;
            public float Scale = 1f;
            public float ExtraOffset;
            public float ShakeOffset;
            public float Spin;
            public float Alpha = 1f;
        }

        /* インスペクタ設定 */

        [Header("並び")]
        [Label("コインのプレハブ")]
        [SerializeField] private PPCoinMenuItem mItemPrefab;
        [Label("並べる場所")]
        [SerializeField] private RectTransform mItemRoot;
        [Label("コインの間隔")]
        [SerializeField] private float mSpacing = 200f;

        [Header("記号")]
        [Label("スキル")]
        [SerializeField] private Sprite mSkillIcon;
        [Label("ターゲット")]
        [SerializeField] private Sprite mTargetIcon;
        [Label("詳細")]
        [SerializeField] private Sprite mDetailIcon;
        [Label("戻る")]
        [SerializeField] private Sprite mBackIcon;
        [Label("解除")]
        [SerializeField] private Sprite mClearIcon;

        [Header("落ちてくる動き")]
        [Label("落ち始める高さ")]
        [SerializeField] private float mDropHeight = 700f;
        [Label("落ちる時間(秒)")]
        [Min(0.01f)]
        [SerializeField] private float mDropDuration = 0.45f;
        [Label("1枚ごとの遅れ(秒)")]
        [Min(0f)]
        [SerializeField] private float mDropStagger = 0.06f;

        [Header("選択中の動き")]
        [Label("拡大率")]
        [Min(1f)]
        [SerializeField] private float mFocusScale = 1.15f;
        [Label("浮き上がる高さ")]
        [SerializeField] private float mFocusLift = 24f;
        // 大きいほど早く目標の大きさ・高さに追いつく
        [Label("追従の速さ")]
        [Min(0.1f)]
        [SerializeField] private float mFocusFollowSpeed = 16f;

        [Header("決定の動き")]
        [Label("決定の時間(秒)")]
        [Min(0.01f)]
        [SerializeField] private float mDecideDuration = 0.4f;
        [Label("跳ね上がる高さ")]
        [SerializeField] private float mDecideLift = 160f;
        [Label("回転数")]
        [SerializeField] private float mDecideSpinTurns = 1.5f;
        [Label("押し出す距離")]
        [SerializeField] private float mPushDistance = 320f;

        /* 状態 */

        private readonly List<ItemMotion> mMotions = new();
        private int mFocusIndex;
        // 決定の動きの最中か。この間は入力を受け付けない
        private bool mIsDeciding;
        // Show / Hide のたびに進める番号。決定の通知中に開き直されたかを見分ける
        private int mVersion;

        /* プロパティ */

        public bool IsShown => gameObject.activeSelf && mMotions.Count > 0;
        public bool IsDeciding => mIsDeciding;
        public int FocusIndex => mFocusIndex;
        public Sprite SkillIcon => mSkillIcon;
        public Sprite TargetIcon => mTargetIcon;
        public Sprite DetailIcon => mDetailIcon;
        public Sprite BackIcon => mBackIcon;
        public Sprite ClearIcon => mClearIcon;

        /* イベント */

        // カーソルが別の項目へ移ったとき(項目の番号)
        public event Action<int> OnFocusChanged;
        // 決定の動きが終わったとき(項目の番号)
        public event Action<int> OnDecided;

        /* Unity イベント */

        // 各コインの動きの状態を見た目へ反映する。選択中の拡大と浮き上がりはここで追従させる
        private void Update()
        {
            float follow = 1f - Mathf.Exp(-mFocusFollowSpeed * Time.unscaledDeltaTime);
            for (int i = 0; i < mMotions.Count; i++)
            {
                var motion = mMotions[i];
                bool isFocused = i == mFocusIndex && !mIsDeciding;
                motion.Scale = Mathf.Lerp(motion.Scale, isFocused ? mFocusScale : 1f, follow);
                motion.Lift = Mathf.Lerp(motion.Lift, isFocused ? mFocusLift : 0f, follow);
                Apply(motion);
            }
        }

        /* 操作 */

        // 項目を並べて表示し、上から落とす。前の項目は破棄する
        public void Show(IReadOnlyList<PPCoinMenuEntry> aEntries, int aFocusIndex)
        {
            ClearItems();
            mVersion++;
            gameObject.SetActive(true);
            // UI 標準の決定入力と二重に反応しないよう、UI の選択を外しておく
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            for (int i = 0; i < aEntries.Count; i++)
            {
                var item = Instantiate(mItemPrefab, mItemRoot);
                item.Setup(aEntries[i]);
                var motion = new ItemMotion
                {
                    Item = item,
                    BasePosition = new Vector2((i - (aEntries.Count - 1) * 0.5f) * mSpacing, 0f),
                    DropOffset = mDropHeight,
                };
                ((RectTransform)item.transform).anchoredPosition = motion.BasePosition;
                mMotions.Add(motion);
                Apply(motion);
            }

            mFocusIndex = Mathf.Clamp(aFocusIndex, 0, Mathf.Max(0, mMotions.Count - 1));
            RefreshFocus();
            StartCoroutine(DropRoutine((DropPattern)UnityEngine.Random.Range(0, 4)));
        }

        // メニューを閉じ、並べたコインを破棄する
        public void Hide()
        {
            ClearItems();
            mVersion++;
            gameObject.SetActive(false);
        }

        // カーソルを左右へ動かす。端まで行くと反対側へ回り込む
        public void Move(int aDirection)
        {
            if (!IsShown || mIsDeciding || aDirection == 0) return;
            mFocusIndex = (mFocusIndex + (aDirection > 0 ? 1 : -1) + mMotions.Count) % mMotions.Count;
            RefreshFocus();
            OnFocusChanged?.Invoke(mFocusIndex);
        }

        // カーソルの項目を決定する。選べない項目なら揺らすだけで通知しない
        public void Submit()
        {
            if (!IsShown || mIsDeciding) return;
            var motion = mMotions[mFocusIndex];
            if (!motion.Item.IsInteractable)
            {
                StartCoroutine(ShakeRoutine(motion));
                return;
            }
            StartCoroutine(DecideRoutine(mFocusIndex));
        }

        // 選択中の光を今のカーソル位置に合わせる
        private void RefreshFocus()
        {
            for (int i = 0; i < mMotions.Count; i++) mMotions[i].Item.SetFocused(i == mFocusIndex);
        }

        // 動きを止めて、並べたコインをすべて破棄する
        private void ClearItems()
        {
            StopAllCoroutines();
            mIsDeciding = false;
            foreach (var motion in mMotions)
            {
                if (motion.Item != null) Destroy(motion.Item.gameObject);
            }
            mMotions.Clear();
        }

        /* 描画 */

        // 1 枚分の動きの状態を、位置・大きさ・回転・透明度へ反映する
        private static void Apply(ItemMotion aMotion)
        {
            var body = aMotion.Item.Body;
            body.anchoredPosition = new Vector2(aMotion.ShakeOffset, aMotion.DropOffset + aMotion.Lift + aMotion.ExtraOffset);
            body.localScale = Vector3.one * aMotion.Scale;
            body.localRotation = Quaternion.Euler(0f, aMotion.Spin, 0f);
            aMotion.Item.CanvasGroup.alpha = aMotion.Alpha;
        }

        // 落とし方に応じて各コインの落ち始めを遅らせ、跳ねながら所定の位置へ落とす
        private IEnumerator DropRoutine(DropPattern aPattern)
        {
            int count = mMotions.Count;
            var delays = new float[count];
            var spins = new float[count];
            for (int i = 0; i < count; i++)
            {
                float order = aPattern switch
                {
                    DropPattern.LeftToRight => i,
                    DropPattern.RightToLeft => count - 1 - i,
                    DropPattern.CenterOut => Mathf.Abs(i - (count - 1) * 0.5f),
                    _ => UnityEngine.Random.Range(0f, count - 1),
                };
                delays[i] = order * mDropStagger;
                // ばらばらに落とすときだけ、コインを回しながら落とす
                spins[i] = aPattern == DropPattern.Scatter ? 360f * UnityEngine.Random.Range(1, 3) : 0f;
            }

            float elapsed = 0f;
            float total = mDropDuration + (count > 0 ? Mathf.Max(delays) : 0f);
            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;
                for (int i = 0; i < count; i++)
                {
                    float t = Mathf.Clamp01((elapsed - delays[i]) / mDropDuration);
                    mMotions[i].DropOffset = mDropHeight * (1f - EaseOutBounce(t));
                    mMotions[i].Spin = spins[i] * (1f - t);
                }
                yield return null;
            }

            foreach (var motion in mMotions)
            {
                motion.DropOffset = 0f;
                motion.Spin = 0f;
            }
        }

        // 選んだコインを回しながら跳ね上げて消し、残りを下へ押し出してから決定を通知する
        private IEnumerator DecideRoutine(int aIndex)
        {
            mIsDeciding = true;
            float elapsed = 0f;
            while (elapsed < mDecideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / mDecideDuration);
                for (int i = 0; i < mMotions.Count; i++)
                {
                    var motion = mMotions[i];
                    if (i == aIndex)
                    {
                        motion.ExtraOffset = mDecideLift * EaseOutCubic(t);
                        motion.Spin = 360f * mDecideSpinTurns * t;
                        // 跳ね上がりが見えるよう、消え始めを少し遅らせる
                        motion.Alpha = 1f - Mathf.Clamp01((t - 0.3f) / 0.7f);
                    }
                    else
                    {
                        motion.ExtraOffset = -mPushDistance * t * t;
                        motion.Alpha = 1f - t;
                    }
                }
                yield return null;
            }

            int version = mVersion;
            OnDecided?.Invoke(aIndex);
            // 通知先が開き直しも閉じもしなかったときは、元の並びに戻して操作を続けられるようにする
            if (version != mVersion) yield break;
            mIsDeciding = false;
            foreach (var motion in mMotions)
            {
                motion.ExtraOffset = 0f;
                motion.Spin = 0f;
                motion.Alpha = 1f;
            }
        }

        // 選べない項目を決定しようとしたことを、左右に揺らして知らせる
        private IEnumerator ShakeRoutine(ItemMotion aMotion)
        {
            const float duration = 0.25f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                aMotion.ShakeOffset = Mathf.Sin(t * Mathf.PI * 6f) * 12f * (1f - t);
                yield return null;
            }
            aMotion.ShakeOffset = 0f;
        }

        /* ヘルパー */

        private static float EaseOutCubic(float aT) => 1f - Mathf.Pow(1f - aT, 3f);

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
