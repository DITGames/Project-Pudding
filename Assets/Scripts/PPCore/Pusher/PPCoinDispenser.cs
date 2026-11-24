/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPCoinDispenser.cs
 * @author hqrse
 * @date 2026/11/17
 * @brief プッシャーへのコイン投入（プレイヤーのカーソル）
 * =====================================*/

using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using AttributeUtility;

namespace PPCore
{
    // プッシャーへコインを投入する役。所持コインを消費して投入位置の線上から落とし、所持コインは一定時間ごとに回復する
    // 投入位置は左端 0〜右端 1 の割合で指定し、プレイヤーが左右の入力でカーソルを動かして投入の入力で落とす
    public class PPCoinDispenser : MonoBehaviour
    {
        /* 定数 */

        // 開始時に重ねて置くときの、1 段あたりの高さ
        private const float LayerHeight = 0.4f;

        /* インスペクタ設定 */

        [Header("コイン")]
        [Label("所持コインの初期値")]
        [Min(0)]
        [SerializeField] private int mStock = 10;

        [Header("所持コインの回復")]
        [Label("回復の間隔(秒)")]
        [Min(0.05f)]
        [SerializeField] private float mRecoverInterval = 1f;
        [Label("1回の回復量")]
        [Min(0)]
        [SerializeField] private int mRecoverAmount = 1;
        // 回復で増やせる上限。所持コインがこの枚数に達したら回復を止める
        [Label("所持コインの上限")]
        [Min(0)]
        [SerializeField] private int mStockMax = 10;

        [Header("投入")]
        // 投入位置の線の左端(割合 0)
        [Label("投入位置(左)")]
        [SerializeField] private Transform mLeftPoint;
        // 投入位置の線の右端(割合 1)
        [Label("投入位置(右)")]
        [SerializeField] private Transform mRightPoint;
        // キー設定で変更できる
        [Label("カーソルを左へ動かす入力")]
        [SerializeField] private InputActionReference mDropLeftAction;
        [Label("カーソルを右へ動かす入力")]
        [SerializeField] private InputActionReference mDropRightAction;
        [Label("投入する入力")]
        [SerializeField] private InputActionReference mDropAction;
        // 投入位置から左右へずらす幅。毎回この範囲でばらつかせる
        [Label("投入位置のばらつき")]
        [Min(0f)]
        [SerializeField] private float mSpread = 5f;

        [Header("カーソル")]
        // 押し続けたときに 1 秒で動く割合(左端から右端までが 1)
        [Label("カーソルの速さ")]
        [Min(0.05f)]
        [SerializeField] private float mCursorSpeed = 0.8f;
        [Label("カーソルの初期位置(割合)")]
        [Range(0f, 1f)]
        [SerializeField] private float mCursorStart = 0.5f;

        [Header("開始時の配置")]
        [Label("並べておく枚数")]
        [Min(0)]
        [SerializeField] private int mInitialCount = 200;
        // 開始時にコインを並べる範囲。この Transform の位置と大きさ（スケール）を箱として使う
        [Label("並べる範囲")]
        [SerializeField] private Transform mInitialArea;
        // 並べるときのコイン同士の間隔。コインの直径より少し大きくして、重なって弾け飛ばないようにする
        [Label("並べる間隔")]
        [Min(0.1f)]
        [SerializeField] private float mInitialSpacing = 1.7f;

        [Header("表示")]
        // 所持コインの枚数を出すラベル。未設定なら表示しない
        [Label("所持コインのラベル")]
        [SerializeField] private TMP_Text mStockLabel;

        /* 状態 */

        // コインを出す置き場。読み込み時に PPPusherField から渡される
        private PPCoinPool mPool;
        // 投入と回復を始めたか。Begin が呼ばれるまでは何もしない
        private bool mIsRunning;
        // 次の回復までの残り秒数
        private float mRecoverTimer;
        // 最後にメニューで止まっていたフレーム
        private int mPausedFrame = -1;

        /* プロパティ */

        // 現在の所持コイン
        public int Stock => mStock;
        // 回復の間隔・量・上限。実行中に変えられる
        public float RecoverInterval
        {
            get => mRecoverInterval;
            set => mRecoverInterval = Mathf.Max(0.05f, value);
        }
        public int RecoverAmount
        {
            get => mRecoverAmount;
            set => mRecoverAmount = Mathf.Max(0, value);
        }
        public int StockMax
        {
            get => mStockMax;
            set => mStockMax = Mathf.Max(0, value);
        }
        // カーソルの位置。左端 0〜右端 1
        public float CursorRatio { get; private set; }
        // 投入と回復を始めたか
        public bool IsRunning => mIsRunning;

        /* イベント */

        // 所持コインが変わったとき(変化後の枚数)
        public event Action<int> OnStockChanged;

        /* Unity イベント */

        private void Awake()
        {
            CursorRatio = mCursorStart;
        }

        private void Start()
        {
            foreach (var action in new[] { mDropLeftAction, mDropRightAction, mDropAction })
            {
                if (action != null) action.action.Enable();
            }
            RefreshLabel();
        }

        // 開始後、所持コインを回復し、プレイヤーのカーソルでコインを投入する
        // timeScale に従う時間で数えるため、メニューを開いている間は回復も投入も止まる
        private void Update()
        {
            if (!mIsRunning) return;

            UpdateRecover();
            // メニューを開いている間は timeScale が 0 になる。メニューの左右キーでカーソルや投入が動かないよう止める
            if (Time.timeScale <= 0f)
            {
                mPausedFrame = Time.frameCount;
                return;
            }

            UpdatePlayer();
        }

        /* 初期化・接続 */

        // コインを出す置き場を設定する
        public void Bind(PPCoinPool aPool) => mPool = aPool;

        // 投入と回復を始める
        public void Begin()
        {
            mIsRunning = true;
            mRecoverTimer = mRecoverInterval;
        }

        /* 操作 */

        // 投入位置の線上の割合(左端 0〜右端 1)からコインを 1 枚落とす。所持コインが無い、または開始前なら何もせず false を返す
        public bool TryDropAt(float aRatio)
        {
            if (!mIsRunning || mStock <= 0 || mLeftPoint == null || mRightPoint == null || mPool == null) return false;

            var position = GetDropPoint(aRatio) + mLeftPoint.right * UnityEngine.Random.Range(-mSpread, mSpread);
            // ほぼ水平にして、積み重なりやすくする
            var rotation = Quaternion.Euler(
                UnityEngine.Random.Range(-10f, 10f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(-10f, 10f));
            if (mPool.Spawn(position, rotation) == null) return false;

            ChangeStock(-1);
            return true;
        }

        // 投入位置の線上の割合(左端 0〜右端 1)にあたる位置。ばらつきを加える前の位置で、投入位置の表示にも使う
        public Vector3 GetDropPoint(float aRatio)
        {
            if (mLeftPoint == null || mRightPoint == null) return transform.position;
            return Vector3.Lerp(mLeftPoint.position, mRightPoint.position, Mathf.Clamp01(aRatio));
        }

        // 開始時のコインを、並べる範囲の中へ格子状に置く。1 段に収まらない分は上の段へ重ねる
        // 重ねて生成すると物理で弾け飛んで落ちてしまうため、間隔を空けて水平に置く
        public void SpawnInitialCoins()
        {
            if (mInitialArea == null || mPool == null || mInitialCount <= 0) return;

            var size = mInitialArea.lossyScale;
            int columns = Mathf.Max(1, Mathf.FloorToInt(size.x / mInitialSpacing));
            int rows = Mathf.Max(1, Mathf.FloorToInt(size.z / mInitialSpacing));
            var origin = new Vector3(-(columns - 1) * 0.5f, 0f, -(rows - 1) * 0.5f) * mInitialSpacing;

            for (int i = 0; i < mInitialCount; i++)
            {
                int layer = i / (columns * rows);
                int index = i % (columns * rows);
                var local = origin + new Vector3(index % columns, 0f, index / columns) * mInitialSpacing;
                local.y = -size.y * 0.5f + LayerHeight * (layer + 0.5f);
                // 段ごとに半分ずらして、下の段の隙間へ載るようにする
                if (layer % 2 == 1) local.x += mInitialSpacing * 0.5f;

                var rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                mPool.Spawn(mInitialArea.position + mInitialArea.rotation * local, rotation);
            }
        }

        // 左右の入力を押している間カーソルを動かし、投入の入力で落とす
        private void UpdatePlayer()
        {
            float move = 0f;
            if (IsPressed(mDropLeftAction)) move -= 1f;
            if (IsPressed(mDropRightAction)) move += 1f;
            if (move != 0f) CursorRatio = Mathf.Clamp01(CursorRatio + move * mCursorSpeed * Time.deltaTime);
            // 投入はメニューの決定と同じボタンのため、メニューを閉じた決定でコインを落とさないよう、止まっていた直後のフレームは見送る
            bool isJustResumed = Time.frameCount - mPausedFrame <= 1;
            if (!isJustResumed && mDropAction != null && mDropAction.action.WasPressedThisFrame()) TryDropAt(CursorRatio);
        }

        // 間隔ごとに所持コインを回復する。上限に達している間は回復を止め、減ってから間隔分待って回復する
        private void UpdateRecover()
        {
            if (mStock >= mStockMax)
            {
                mRecoverTimer = mRecoverInterval;
                return;
            }

            mRecoverTimer -= Time.deltaTime;
            if (mRecoverTimer > 0f) return;

            mRecoverTimer += mRecoverInterval;
            ChangeStock(Mathf.Min(mRecoverAmount, mStockMax - mStock));
        }

        // 所持コインを増減し、表示と購読側へ反映する
        private void ChangeStock(int aDelta)
        {
            if (aDelta == 0) return;

            mStock = Mathf.Max(0, mStock + aDelta);
            RefreshLabel();
            OnStockChanged?.Invoke(mStock);
        }

        /* 表示 */

        // 所持コインの表示を更新する
        private void RefreshLabel()
        {
            if (mStockLabel != null) mStockLabel.text = mStock.ToString();
        }

        /* ヘルパー */

        private static bool IsPressed(InputActionReference aAction) => aAction != null && aAction.action.IsPressed();
    }
}
