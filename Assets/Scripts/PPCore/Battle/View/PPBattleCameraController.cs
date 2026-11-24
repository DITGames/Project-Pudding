/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleCameraController.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief バトル画面のカメラの横スクロールとズーム
 * =====================================*/

using AttributeUtility;
using CustomConsole;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PPCore
{
    // バトル画面のカメラ。道を真横から遠近なしで映し、横スクロールとズームを道の範囲の中で受け付ける
    [RequireComponent(typeof(Camera))]
    public class PPBattleCameraController : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Header("映す範囲")]
        [Label("バトルの道")]
        [SerializeField] private PPBattleField mField;
        // 画面の下端から地面までの高さ。画面の高さを 1 とした割合で、ズームしても地面をこの高さに保つ
        [Label("地面の高さ(画面の割合)")]
        [Range(0f, 1f)]
        [SerializeField] private float mGroundViewportY = 0.15f;
        // 開始時に映す位置。道の左端 0〜右端 1 の割合で、画面の中央をここに合わせる
        [Label("開始位置(道の割合)")]
        [Range(0f, 1f)]
        [SerializeField] private float mStartRatio = 0f;

        // 大きさは画面に映す縦の幅の半分(ワールドの単位)。小さいほど寄る
        [Header("ズーム")]
        [Label("最も寄ったときの大きさ")]
        [Min(0.5f)]
        [SerializeField] private float mMinSize = 3f;
        // 道全体が画面に収まる大きさより引くことはない
        [Label("最も引いたときの大きさ")]
        [Min(0.5f)]
        [SerializeField] private float mMaxSize = 12f;
        [Label("開始時の大きさ")]
        [Min(0.5f)]
        [SerializeField] private float mStartSize = 6f;
        // ホイール 1 回で大きさを変える割合
        [Label("ホイールのズーム量")]
        [Range(0.01f, 0.5f)]
        [SerializeField] private float mWheelZoomStep = 0.1f;
        // スティックを倒し切ったまま 1 秒で、大きさがこの倍率だけ変わる
        [Label("スティックのズームの速さ")]
        [Min(1.01f)]
        [SerializeField] private float mStickZoomRate = 2.5f;

        [Header("スクロール")]
        // スティックを倒し切ったときに 1 秒で動く量。画面の横幅を 1 とした割合
        [Label("スティックのスクロールの速さ")]
        [Min(0.01f)]
        [SerializeField] private float mStickScrollSpeed = 0.8f;

        // 割り当ては固定で、キー設定では変更できない
        [Header("入力")]
        [Label("ドラッグでスクロールする入力")]
        [SerializeField] private InputActionReference mDragAction;
        [Label("ポインタの位置")]
        [SerializeField] private InputActionReference mPointerPositionAction;
        [Label("ポインタの移動量")]
        [SerializeField] private InputActionReference mPointerDeltaAction;
        [Label("ホイールでズームする入力")]
        [SerializeField] private InputActionReference mWheelZoomAction;
        // 左右でスクロール、上下でズームする
        [Label("スティックの入力")]
        [SerializeField] private InputActionReference mStickAction;

        /* 状態 */

        private Camera mCamera;
        // 画面の中で押し始めたドラッグの途中か。画面の外で押し始めたドラッグではスクロールしない
        private bool mIsDragging;

        /* プロパティ */

        public Camera Camera => mCamera;
        // 今映している横の範囲を、余白を含めたスクロールできる範囲に対する割合(左端, 右端)で返す。どちらも 0〜1
        public Vector2 VisibleRange
        {
            get
            {
                if (mField == null || mCamera == null) return new Vector2(0f, 1f);
                GetScrollBounds(out float minX, out float maxX);
                float halfWidth = mCamera.orthographicSize * mCamera.aspect;
                float x = transform.position.x;
                return new Vector2(Mathf.InverseLerp(minX, maxX, x - halfWidth), Mathf.InverseLerp(minX, maxX, x + halfWidth));
            }
        }

        /* Unity イベント */

        private void Awake()
        {
            mCamera = GetComponent<Camera>();
            mCamera.orthographic = true;
        }

        private void OnEnable()
        {
            EnableAction(mDragAction);
            EnableAction(mPointerPositionAction);
            EnableAction(mPointerDeltaAction);
            EnableAction(mWheelZoomAction);
            EnableAction(mStickAction);
        }

        // 開始位置と大きさを当てる
        private void Start()
        {
            if (mField == null)
            {
                CustomConsoleLog.Warning("Battle", "バトルの道が未設定のため、バトル画面のスクロールとズームを止めます。PPBattleCameraController の「バトルの道」を割り当ててください", this);
                enabled = false;
                return;
            }
            mCamera.orthographicSize = mStartSize;
            var position = transform.position;
            position.x = mField.GetPoint(mStartRatio).x;
            transform.position = position;
            ApplyLimits();
        }

        // 入力を反映してから、範囲の制限をかける。メニューで時間を止めている間は操作を受け付けない
        private void LateUpdate()
        {
            if (Time.timeScale > 0f)
            {
                UpdateDrag();
                UpdateWheel();
                UpdateStick();
            }
            else
            {
                mIsDragging = false;
            }
            ApplyLimits();
        }

        /* 操作 */

        // 右クリックのドラッグで、つかんだ場所がポインタに付いてくるように横へ動かす
        private void UpdateDrag()
        {
            if (mDragAction == null) return;
            var drag = mDragAction.action;
            if (drag.WasPressedThisFrame()) mIsDragging = IsPointerInView();
            if (!drag.IsPressed()) mIsDragging = false;
            if (!mIsDragging || mPointerDeltaAction == null) return;

            float worldPerPixel = mCamera.orthographicSize * 2f / Mathf.Max(1, mCamera.pixelHeight);
            ScrollBy(-mPointerDeltaAction.action.ReadValue<Vector2>().x * worldPerPixel);
        }

        // ホイールで、ポインタの下の位置を動かさずにズームする。画面の外のホイールは受け付けない
        private void UpdateWheel()
        {
            if (mWheelZoomAction == null) return;
            float wheel = mWheelZoomAction.action.ReadValue<float>();
            if (Mathf.Approximately(wheel, 0f) || !IsPointerInView()) return;

            // 1 回の回転量は機器や OS で違うため、向きだけを使う
            float scale = wheel > 0f ? 1f - mWheelZoomStep : 1f / (1f - mWheelZoomStep);
            float pivotX = mCamera.ScreenToWorldPoint(ReadPointer()).x;
            ZoomBy(scale, pivotX);
        }

        // 右スティックの左右でスクロールし、上下でズームする。上に倒すと寄る
        private void UpdateStick()
        {
            if (mStickAction == null) return;
            var stick = mStickAction.action.ReadValue<Vector2>();
            float dt = Time.unscaledDeltaTime;
            if (!Mathf.Approximately(stick.x, 0f))
            {
                float viewWidth = mCamera.orthographicSize * 2f * mCamera.aspect;
                ScrollBy(stick.x * mStickScrollSpeed * viewWidth * dt);
            }
            if (!Mathf.Approximately(stick.y, 0f))
            {
                ZoomBy(Mathf.Pow(mStickZoomRate, -stick.y * dt), transform.position.x);
            }
        }

        private void ScrollBy(float aDeltaX)
        {
            var position = transform.position;
            position.x += aDeltaX;
            transform.position = position;
        }

        // 大きさを aScale 倍にする。aPivotX の位置が画面上で動かないよう、カメラも横へずらす
        private void ZoomBy(float aScale, float aPivotX)
        {
            float before = mCamera.orthographicSize;
            float after = Mathf.Clamp(before * aScale, mMinSize, GetMaxSize());
            var position = transform.position;
            position.x = aPivotX + (position.x - aPivotX) * (after / before);
            transform.position = position;
            mCamera.orthographicSize = after;
        }

        // 大きさと横の位置を範囲の中へ収め、地面が画面の決まった高さに来るよう縦の位置を合わせる
        private void ApplyLimits()
        {
            if (mField == null) return;
            float size = Mathf.Clamp(mCamera.orthographicSize, mMinSize, GetMaxSize());
            mCamera.orthographicSize = size;

            GetScrollBounds(out float minX, out float maxX);
            float halfWidth = size * mCamera.aspect;
            var position = transform.position;
            // 道全体が画面に収まるときは中央に置く
            position.x = halfWidth * 2f >= maxX - minX
                ? (minX + maxX) * 0.5f
                : Mathf.Clamp(position.x, minX + halfWidth, maxX - halfWidth);
            position.y = mField.GroundY + size * (1f - mGroundViewportY * 2f);
            transform.position = position;
        }

        /* 問い合わせ */

        // 余白を含めたスクロールできる範囲(ワールド座標の x)
        private void GetScrollBounds(out float aMinX, out float aMaxX)
        {
            aMinX = mField.LeftX - mField.Margin;
            aMaxX = mField.RightX + mField.Margin;
        }

        // 引ける上限の大きさ。スクロールできる範囲の横幅を超えて映さない
        private float GetMaxSize()
        {
            GetScrollBounds(out float minX, out float maxX);
            float fitSize = (maxX - minX) * 0.5f / Mathf.Max(0.01f, mCamera.aspect);
            return Mathf.Max(mMinSize, Mathf.Min(mMaxSize, fitSize));
        }

        private bool IsPointerInView() => mPointerPositionAction != null && mCamera.pixelRect.Contains(ReadPointer());

        private Vector2 ReadPointer() => mPointerPositionAction != null ? mPointerPositionAction.action.ReadValue<Vector2>() : Vector2.zero;

        /* ヘルパー */

        private static void EnableAction(InputActionReference aAction)
        {
            if (aAction != null) aAction.action.Enable();
        }
    }
}
