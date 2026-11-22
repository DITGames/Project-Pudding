/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPCoinDispenser.cs
 * @author hqrse
 * @date 2026/11/17
 * @brief プッシャーへのコイン投入（プレイヤーのカーソル・投入 AI・床の区画の数え上げ）
 * =====================================*/

using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using AttributeUtility;

namespace PPCore
{
    // コインを投入する方法の初期値。バトルの進行役が UsePlayer / UseAI で切り替える
    public enum PPCoinDispenserMode
    {
        // プレイヤーが左右の入力でカーソルを動かし、投入の入力で落とす
        [InspectorName("プレイヤー操作")]
        Player = 0,
        // 投入 AI が列を選んで落とす。AI が渡されていなければ一定間隔で左右ランダムに落とす
        [InspectorName("自動")]
        Auto = 1,
    }

    // プッシャーへコインを投入する役。所持コインを消費して投入位置の線上から落とし、所持コインは一定時間ごとに回復する
    // 投入位置は左端 0〜右端 1 の割合で指定する。どこへいつ落とすかは、プレイヤーのカーソルか投入 AI が決める
    public class PPCoinDispenser : MonoBehaviour
    {
        /* 定数 */

        // 開始時に重ねて置くときの、1 段あたりの高さ
        private const float LayerHeight = 0.4f;
        // 投入位置のガイドを置く面を、投入位置から真下へ探す長さ
        private const float GuideRayLength = 50f;

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
        // 投入 AI が渡されていないときに、左右ランダムに落とす間隔(秒)
        [Label("自動投入の間隔(秒)")]
        [Min(0.05f)]
        [SerializeField] private float mAutoInterval = 1.5f;

        [Header("カーソル")]
        // 押し続けたときに 1 秒で動く割合(左端から右端までが 1)
        [Label("カーソルの速さ")]
        [Min(0.05f)]
        [SerializeField] private float mCursorSpeed = 0.8f;
        [Label("カーソルの初期位置(割合)")]
        [Range(0f, 1f)]
        [SerializeField] private float mCursorStart = 0.5f;
        // プレイヤー操作の間だけ表示し、カーソルの真下にある床やコインの上へ置く。未設定なら出さない
        [Label("投入位置のガイド")]
        [SerializeField] private Transform mCursorGuide;
        // 有効ならメインカメラの方へ向けて立て、無効なら面に沿って平らに置く(スプライトの表はローカルの -Z 側)
        [Label("ガイドをカメラへ向ける")]
        [SerializeField] private bool mIsCursorGuideFacingCamera = true;
        // 真下に何も見つからないとき、投入位置の線から上下へずらして置く量。負の値で下へ
        [Label("床が見つからないときの高さのずれ")]
        [SerializeField] private float mCursorGuideHeightOffset = -3.5f;
        // 床やコインの表面から浮かせる量。カメラへ向けるときは、ガイドの下端が表面に付くよう高さの半分ほどにする
        [Label("ガイドを浮かせる量")]
        [Min(0f)]
        [SerializeField] private float mCursorGuideLift = 0.05f;

        [Header("床の区画")]
        // 投入 AI が数える範囲。未設定なら開始時に並べる範囲を使う
        [Label("数える範囲")]
        [SerializeField] private Transform mSampleArea;
        [Label("列の数")]
        [Min(1)]
        [SerializeField] private int mSampleColumns = 3;
        [Label("行の数")]
        [Min(1)]
        [SerializeField] private int mSampleRows = 3;
        // 手前(落下口の側)の行から順に並べる。足りない行は最後の値を使う
        [Label("行の重み(手前から)", true)]
        [SerializeField] private float[] mRowWeights = { 3f, 2f, 1f };

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

        private readonly PPPusherFloorSampler mFloorSampler = new();
        // コインを出す置き場。読み込み時に PPPusherField から渡される
        private PPCoinPool mPool;
        // 投入と回復を始めたか。Begin が呼ばれるまでは何もしない
        private bool mIsRunning;
        // 投入 AI。プレイヤー操作中と、AI を渡されていない自動投入では null
        private PPCoinDropAI mAI;
        private bool mIsPlayerControlled;
        // AI を渡されていない自動投入の、次の投入までの残り秒数
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
        // カーソルの位置。左端 0〜右端 1
        public float CursorRatio { get; private set; }
        public bool IsPlayerControlled => mIsPlayerControlled;
        // 投入 AI。プレイヤー操作中などは null
        public PPCoinDropAI AI => mAI;
        public PPPusherFloorSampler FloorSampler => mFloorSampler;

        // 投入 AI が数える範囲
        private Transform SampleArea => mSampleArea != null ? mSampleArea : mInitialArea;

        /* イベント */

        // 所持コインが変わったとき(変化後の枚数)
        public event Action<int> OnStockChanged;

        /* Unity イベント */

        // 投入の方法の初期値に合わせて操作を決め、所持コインを表示する
        private void Awake()
        {
            CursorRatio = mCursorStart;
            mIsPlayerControlled = mMode == PPCoinDispenserMode.Player;
        }

        private void Start()
        {
            foreach (var action in new[] { mDropLeftAction, mDropRightAction, mDropAction })
            {
                if (action != null) action.action.Enable();
            }
            RefreshGuide();
            RefreshLabel();
        }

        // 開始後、所持コインを回復し、プレイヤーのカーソルか投入 AI でコインを投入する
        // timeScale に従う時間で数えるため、コマンド入力中は回復も投入も止まる
        private void Update()
        {
            if (!mIsRunning) return;

            UpdateRecover();
            // コマンド入力中は timeScale が 0 になる。選択の左右キーでカーソルや投入が動かないよう止める
            if (Time.timeScale <= 0f) return;

            if (mIsPlayerControlled) UpdatePlayer();
            else if (mAI != null) mAI.Tick(this, Time.deltaTime);
            else UpdateRandomAuto();
        }

        // 選択中は、数える範囲の区画と、最後に数えたコイン:アイテムの数を表示する
        private void OnDrawGizmosSelected()
        {
            var area = SampleArea;
            if (area == null) return;

            Gizmos.matrix = area.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            int columns = Mathf.Max(1, mSampleColumns);
            int rows = Mathf.Max(1, mSampleRows);
            for (int column = 0; column < columns; column++)
            {
                for (int row = 0; row < rows; row++)
                {
                    var center = new Vector3((column + 0.5f) / columns - 0.5f, 0f, (row + 0.5f) / rows - 0.5f);
                    Gizmos.DrawWireCube(center, new Vector3(1f / columns, 1f, 1f / rows));
#if UNITY_EDITOR
                    int cell = row * columns + column;
                    if (cell < mFloorSampler.Coins.Length)
                    {
                        UnityEditor.Handles.Label(area.TransformPoint(center), $"{mFloorSampler.Coins[cell]}:{mFloorSampler.Items[cell]}");
                    }
#endif
                }
            }
            Gizmos.matrix = Matrix4x4.identity;
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

        // プレイヤーのカーソル操作で投入する
        public void UsePlayer()
        {
            mIsPlayerControlled = true;
            mAI = null;
            RefreshGuide();
        }

        // 投入 AI で投入する。プロファイルが null なら、一定間隔で左右ランダムに投入する
        public void UseAI(PPCoinDropAIProfile aProfile, IRandomProvider aRandom, string aOwner)
        {
            mIsPlayerControlled = false;
            mAI = aProfile != null ? new PPCoinDropAI(aProfile, aRandom, aOwner) : null;
            RefreshGuide();
        }

        /* 操作 */

        // 投入位置の線上の割合(左端 0〜右端 1)からコインを 1 枚落とす。所持コインが無い、または開始前なら何もせず false を返す
        public bool TryDropAt(float aRatio)
        {
            if (!mIsRunning || mStock <= 0 || mLeftPoint == null || mRightPoint == null || mPool == null) return false;

            var basePosition = Vector3.Lerp(mLeftPoint.position, mRightPoint.position, Mathf.Clamp01(aRatio));
            var position = basePosition + mLeftPoint.right * UnityEngine.Random.Range(-mSpread, mSpread);
            // ほぼ水平にして、積み重なりやすくする
            var rotation = Quaternion.Euler(
                UnityEngine.Random.Range(-10f, 10f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(-10f, 10f));
            if (mPool.Spawn(position, rotation) == null) return false;

            ChangeStock(-1);
            return true;
        }

        // 床の区画を数え直して返す
        public PPPusherFloorSampler SampleFloor()
        {
            mFloorSampler.Sample(SampleArea, mSampleColumns, mSampleRows);
            return mFloorSampler;
        }

        // 区画の列の中の位置(列の左端 0〜右端 1)を、投入位置の割合へ直す
        public float ColumnToRatio(int aColumn, float aInColumn)
        {
            var area = SampleArea;
            if (area == null || mLeftPoint == null || mRightPoint == null) return 0.5f;

            int columns = Mathf.Max(1, mFloorSampler.Columns > 0 ? mFloorSampler.Columns : mSampleColumns);
            float localX = (Mathf.Clamp(aColumn, 0, columns - 1) + Mathf.Clamp01(aInColumn)) / columns - 0.5f;
            var point = area.TransformPoint(new Vector3(localX, 0f, 0f));
            var line = mRightPoint.position - mLeftPoint.position;
            return Mathf.Clamp01(Vector3.Dot(point - mLeftPoint.position, line) / line.sqrMagnitude);
        }

        // 手前から数えた行の重み。設定より奥の行は最後の値を使う
        public float GetRowWeight(int aRow)
        {
            if (mRowWeights == null || mRowWeights.Length == 0) return 1f;
            return mRowWeights[Mathf.Clamp(aRow, 0, mRowWeights.Length - 1)];
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
            // 押し出し板やコインが動くため、カーソルを動かしていなくても毎フレーム置き直す
            RefreshGuide();
            if (mDropAction != null && mDropAction.action.WasPressedThisFrame()) TryDropAt(CursorRatio);
        }

        // 投入 AI を渡されていないときは、一定間隔で左右どちらかの端へ落とす
        private void UpdateRandomAuto()
        {
            mAutoTimer -= Time.deltaTime;
            if (mAutoTimer > 0f) return;

            mAutoTimer = mAutoInterval;
            TryDropAt(UnityEngine.Random.value < 0.5f ? 0f : 1f);
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

        // ガイドはプレイヤー操作の間だけ出し、カーソルの真下で最初に当たった面の上へ置く
        private void RefreshGuide()
        {
            if (mCursorGuide == null) return;

            if (mCursorGuide.gameObject.activeSelf != mIsPlayerControlled) mCursorGuide.gameObject.SetActive(mIsPlayerControlled);
            if (!mIsPlayerControlled || mLeftPoint == null || mRightPoint == null) return;

            var dropPoint = Vector3.Lerp(mLeftPoint.position, mRightPoint.position, CursorRatio);
            bool isHit = Physics.Raycast(dropPoint, Vector3.down, out var hit, GuideRayLength, ~0, QueryTriggerInteraction.Ignore);
            var surface = isHit ? hit.point : dropPoint + Vector3.up * mCursorGuideHeightOffset;
            var normal = isHit ? hit.normal : Vector3.up;

            var camera = mIsCursorGuideFacingCamera ? Camera.main : null;
            if (camera != null)
            {
                mCursorGuide.SetPositionAndRotation(surface + Vector3.up * mCursorGuideLift, Quaternion.LookRotation(camera.transform.forward));
                return;
            }
            mCursorGuide.SetPositionAndRotation(surface + normal * mCursorGuideLift, Quaternion.LookRotation(-normal));
        }

        /* ヘルパー */

        private static bool IsPressed(InputActionReference aAction) => aAction != null && aAction.action.IsPressed();
    }
}
