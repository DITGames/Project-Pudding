/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPusherPlate.cs
 * @author hqrse
 * @date 2026/11/17
 * @brief プッシャーの押し出し板
 * =====================================*/

using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // プッシャーの押し出し板。開始の合図の後、奥の台の下から手前へ一定の周期で出入りし、床のコインを押し出す
    // 物理で動くコインを運べるよう、キネマティックな Rigidbody を FixedUpdate で動かす
    [RequireComponent(typeof(Rigidbody))]
    public class PPPusherPlate : MonoBehaviour
    {
        /* インスペクタ設定 */

        // 置いた位置から、最も引っ込んだ位置までの距離。板の先端が奥の台の面に揃う量を入れる
        [Label("引っ込んだ位置までの距離")]
        [SerializeField] private float mStartOffset = 6.4f;
        // 最も引っ込んだ位置から、最も出た位置までの距離
        [Label("出入りする距離")]
        [Min(0f)]
        [SerializeField] private float mStroke = 2.4f;
        // 1 往復に掛かる秒数
        [Label("1往復の秒数")]
        [Min(0.1f)]
        [SerializeField] private float mPeriod = 3f;

        /* 状態 */

        // 動かす対象の Rigidbody
        private Rigidbody mRigidbody;
        // 置いた時点の位置。出入りの基準にする
        private Vector3 mRestPosition;
        // 出入りの経過時間（秒）。timeScale で止まるよう FixedUpdate で積む
        private float mElapsed;
        // 動き始めたか。Begin が呼ばれるまでは引っ込んだ位置で止めておく
        private bool mIsRunning;

        /* Unity イベント */

        // 置いた位置を基準として覚え、最も引っ込んだ位置から始める
        private void Awake()
        {
            mRigidbody = GetComponent<Rigidbody>();
            mRigidbody.isKinematic = true;
            mRestPosition = transform.position;
            transform.position = ResolvePosition(0f);
        }

        // 開始後、周期に沿って板を動かす。コインを摩擦で運べるよう MovePosition を使う
        private void FixedUpdate()
        {
            if (!mIsRunning) return;

            mElapsed += Time.fixedDeltaTime;
            mRigidbody.MovePosition(ResolvePosition(mElapsed));
        }

        /* 操作 */

        // 引っ込んだ位置から出入りを始める
        public void Begin() => mIsRunning = true;

        /* ヘルパー */

        // 経過時間に対応する板の位置を返す。引っ込んだ位置から出て戻るまでを、滑らかな 1 往復にする
        private Vector3 ResolvePosition(float aElapsed)
        {
            float phase = 0.5f - 0.5f * Mathf.Cos(aElapsed / mPeriod * Mathf.PI * 2f);
            return mRestPosition + transform.forward * (mStartOffset + mStroke * phase);
        }
    }
}
