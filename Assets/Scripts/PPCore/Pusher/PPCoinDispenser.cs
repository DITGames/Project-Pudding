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
        // プレイヤーが左右の矢印キーで投入する
        [InspectorName("プレイヤー操作")]
        Player = 0,
        // 一定間隔で、左右どちらかへ自動で投入する。敵のプッシャーに使う
        [InspectorName("自動")]
        Auto = 1,
    }

    // プッシャーへコインを投入する役。所持コインを消費して左右どちらかから落とし、落下したコインは所持コインへ戻す
    // 味方と敵で仕組みは同じで、違うのは投入のきっかけ（キー入力か一定間隔か）だけ
    // 開始時には、台の上へコインを並べておく
    public class PPCoinDispenser : MonoBehaviour
    {
        /* 定数 */

        // 開始時に重ねて置くときの、1 段あたりの高さ
        private const float LayerHeight = 0.4f;
        // コインの最高速度
        private const float MaxCoinSpeed = 12f;

        /* インスペクタ設定 */

        [Header("コイン")]
        [Label("コインのプレハブ")]
        [SerializeField] private GameObject mCoinPrefab;
        [Label("所持コインの初期値")]
        [Min(0)]
        [SerializeField] private int mStock = 30;
        // 落ちたコインを所持コインへ戻すか。戻すと、落とした分だけまた投入できる
        [Label("落ちたコインを所持コインへ戻す")]
        [SerializeField] private bool mIsRefundDropped = true;
        [Label("落下検出")]
        [SerializeField] private PPPusherDropZone mDropZone;

        [Header("投入")]
        [Label("投入の方法")]
        [SerializeField] private PPCoinDispenserMode mMode = PPCoinDispenserMode.Player;
        [Label("投入位置(左)")]
        [SerializeField] private Transform mLeftPoint;
        [Label("投入位置(右)")]
        [SerializeField] private Transform mRightPoint;
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
        [SerializeField] private int mInitialCount = 60;
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

        // 自動投入の、次の投入までの残り秒数
        private float mAutoTimer;

        /* プロパティ */

        // 現在の所持コイン
        public int Stock => mStock;

        /* イベント */

        // 所持コインが変わったとき(変化後の枚数)
        public event Action<int> OnStockChanged;

        /* Unity イベント */

        // 落下の通知を購読し、台の上へコインを並べる
        private void Start()
        {
            if (mDropZone != null) mDropZone.OnCoinGained += HandleCoinDropped;
            SpawnInitialCoins();
            RefreshLabel();
            mAutoTimer = mAutoInterval;
        }

        // 投入の方法に応じて、キー入力または経過時間でコインを投入する
        private void Update()
        {
            if (mMode == PPCoinDispenserMode.Player)
            {
                if (Keyboard.current == null) return;
                if (Keyboard.current.leftArrowKey.wasPressedThisFrame) TryDrop(mLeftPoint);
                if (Keyboard.current.rightArrowKey.wasPressedThisFrame) TryDrop(mRightPoint);
                return;
            }

            // timeScale に従う時間で数えるため、コマンド入力中は投入も止まる
            mAutoTimer -= Time.deltaTime;
            if (mAutoTimer > 0f) return;

            mAutoTimer = mAutoInterval;
            TryDrop(UnityEngine.Random.value < 0.5f ? mLeftPoint : mRightPoint);
        }

        // 破棄時に購読を外す
        private void OnDestroy()
        {
            if (mDropZone != null) mDropZone.OnCoinGained -= HandleCoinDropped;
        }

        /* 操作 */

        // 指定した投入位置からコインを 1 枚落とす。所持コインが無ければ何もせず false を返す
        public bool TryDrop(Transform aPoint)
        {
            if (mStock <= 0 || aPoint == null || mCoinPrefab == null) return false;

            var position = aPoint.position + aPoint.right * UnityEngine.Random.Range(-mSpread, mSpread);
            SpawnCoin(position);
            ChangeStock(-1);
            return true;
        }

        // 開始時のコインを、並べる範囲の中へ格子状に置く。1 段に収まらない分は上の段へ重ねる
        // 重ねて生成すると物理で弾け飛んで落ちてしまうため、間隔を空けて水平に置く
        private void SpawnInitialCoins()
        {
            if (mInitialArea == null || mCoinPrefab == null || mInitialCount <= 0) return;

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
                SpawnCoin(mInitialArea.position + mInitialArea.rotation * local, rotation);
            }
        }

        // 投入するコインを 1 枚生成する。ほぼ水平にして、積み重なりやすくする
        private void SpawnCoin(Vector3 aPosition)
        {
            var rotation = Quaternion.Euler(
                UnityEngine.Random.Range(-10f, 10f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(-10f, 10f));
            SpawnCoin(aPosition, rotation);
        }

        // コインを 1 枚、指定した向きで生成する
        // 挟まれて弾かれたときに台の外まで飛び出さないよう、最高速度を抑えておく
        private void SpawnCoin(Vector3 aPosition, Quaternion aRotation)
        {
            var coin = Instantiate(mCoinPrefab, aPosition, aRotation, transform);
            if (coin.TryGetComponent<Rigidbody>(out var body)) body.maxLinearVelocity = MaxCoinSpeed;
        }

        // 所持コインを増減し、表示と購読側へ反映する
        private void ChangeStock(int aDelta)
        {
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

        /* イベントハンドラ */

        // コインが落ちたとき、設定に応じて所持コインへ戻す
        private void HandleCoinDropped(PPTypeAttribute aAttribute, int aCount)
        {
            if (mIsRefundDropped) ChangeStock(aCount);
        }
    }
}
