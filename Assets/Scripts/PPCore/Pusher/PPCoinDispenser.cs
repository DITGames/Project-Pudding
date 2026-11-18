/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPCoinDispenser.cs
 * @author hqrse
 * @date 2026/11/17
 * @brief プッシャーへのコイン投入
 * =====================================*/

using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using AttributeUtility;

namespace PPCore
{
    // コインを投入する方法
    public enum PPCoinDispenserMode
    {
        // プレイヤーが入力で左右へ投入する(既定は左右の矢印キーとゲームパッドの L / R ボタン)
        [InspectorName("プレイヤー操作")]
        Player = 0,
        // 一定間隔で、左右どちらかへ自動で投入する。敵のプッシャーに使う
        [InspectorName("自動")]
        Auto = 1,
    }

    // プッシャーへコインを投入する役。所持コインを消費して左右どちらかから落とし、所持コインは一定時間ごとに回復する
    // 味方と敵で仕組みは同じで、違うのは投入のきっかけ（キー入力か一定間隔か）だけ。コインは共有の置き場から出す
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
        [Label("投入の方法")]
        [SerializeField] private PPCoinDispenserMode mMode = PPCoinDispenserMode.Player;
        [Label("投入位置(左)")]
        [SerializeField] private Transform mLeftPoint;
        [Label("投入位置(右)")]
        [SerializeField] private Transform mRightPoint;
        // プレイヤー操作のときに左へ投入する入力。キー設定で変更できる
        [Label("左へ投入する入力")]
        [SerializeField] private InputActionReference mDropLeftAction;
        [Label("右へ投入する入力")]
        [SerializeField] private InputActionReference mDropRightAction;
        // 投入位置から左右へずらす幅。毎回この範囲でばらつかせる
        [Label("投入位置のばらつき")]
        [Min(0f)]
        [SerializeField] private float mSpread = 5f;
        // 自動で投入するときの間隔（秒）
        [Label("自動投入の間隔(秒)")]
        [Min(0.05f)]
        [SerializeField] private float mAutoInterval = 1.5f;

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
        // 自動投入の、次の投入までの残り秒数
        private float mAutoTimer;
        // 次の回復までの残り秒数
        private float mRecoverTimer;

        /* プロパティ */

        // 現在の所持コイン
        public int Stock => mStock;
        // 回復の間隔・量・上限。パッシブスキルなどで実行中に変えられる
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

        /* イベント */

        // 所持コインが変わったとき(変化後の枚数)
        public event Action<int> OnStockChanged;

        /* Unity イベント */

        // 所持コインを表示する。プレイヤー操作なら投入の入力を有効にする
        private void Start()
        {
            if (mMode == PPCoinDispenserMode.Player)
            {
                if (mDropLeftAction != null) mDropLeftAction.action.Enable();
                if (mDropRightAction != null) mDropRightAction.action.Enable();
            }
            RefreshLabel();
        }

        // 開始後、所持コインを回復し、投入の方法に応じて入力または経過時間でコインを投入する
        private void Update()
        {
            if (!mIsRunning) return;

            UpdateRecover();

            if (mMode == PPCoinDispenserMode.Player)
            {
                // コマンド入力中は timeScale が 0 になる。選択の左右キーでコインを落とさないよう投入を止める
                if (Time.timeScale <= 0f) return;
                if (WasPressed(mDropLeftAction)) TryDrop(mLeftPoint);
                if (WasPressed(mDropRightAction)) TryDrop(mRightPoint);
                return;
            }

            // timeScale に従う時間で数えるため、コマンド入力中は投入も止まる
            mAutoTimer -= Time.deltaTime;
            if (mAutoTimer > 0f) return;

            mAutoTimer = mAutoInterval;
            TryDrop(UnityEngine.Random.value < 0.5f ? mLeftPoint : mRightPoint);
        }

        /* 初期化・接続 */

        // コインを出す置き場を設定する
        public void Bind(PPCoinPool aPool) => mPool = aPool;

        // 投入と回復を始める
        public void Begin()
        {
            mIsRunning = true;
            mAutoTimer = mAutoInterval;
            mRecoverTimer = mRecoverInterval;
        }

        /* 操作 */

        // 指定した投入位置からコインを 1 枚落とす。所持コインが無い、または開始前なら何もせず false を返す
        public bool TryDrop(Transform aPoint)
        {
            if (!mIsRunning || mStock <= 0 || aPoint == null || mPool == null) return false;

            var position = aPoint.position + aPoint.right * UnityEngine.Random.Range(-mSpread, mSpread);
            // ほぼ水平にして、積み重なりやすくする
            var rotation = Quaternion.Euler(
                UnityEngine.Random.Range(-10f, 10f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(-10f, 10f));
            if (mPool.Spawn(position, rotation) == null) return false;

            ChangeStock(-1);
            return true;
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

        // 間隔ごとに所持コインを回復する。上限に達している間は回復を止め、減ってから間隔分待って回復する
        // timeScale に従う時間で数えるため、コマンド入力中は回復も止まる
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

        private static bool WasPressed(InputActionReference aAction) => aAction != null && aAction.action.WasPressedThisFrame();
    }
}
