/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPCoinPool.cs
 * @author hqrse
 * @date 2026/11/19
 * @brief プッシャーのコインの使い回し
 * =====================================*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 味方と敵のプッシャーで共有するコインの置き場。読み込み時にまとめて作り、表示を切り替えて使い回す
    // コインはこのオブジェクトの子として作る。落下検出は親をたどってここへ戻す
    public class PPCoinPool : MonoBehaviour
    {
        /* 定数 */

        // コインの最高速度。挟まれて弾かれたときに台の外まで飛び出さないよう抑える
        private const float MaxCoinSpeed = 12f;

        /* インスペクタ設定 */

        [Label("コインのプレハブ")]
        [SerializeField] private GameObject mCoinPrefab;
        // 生成したコインの当たり判定に付ける物理素材。未設定ならプレハブの素材のまま使う
        [Label("コインの物理素材")]
        [SerializeField] private PhysicsMaterial mCoinMaterial;
        // 読み込み時に作っておく枚数。足りなくなったら実行中に追加で作る
        [Label("最初に作る枚数")]
        [Min(0)]
        [SerializeField] private int mInitialCapacity = 2000;
        // 読み込み時に 1 フレームで作る枚数。多いほど早く終わるが、その間の画面の更新が遅れる
        [Label("1フレームに作る枚数")]
        [Min(1)]
        [SerializeField] private int mCreatePerFrame = 100;

        /* 状態 */

        // 使われていないコイン
        private readonly Stack<GameObject> mIdle = new();

        /* プロパティ */

        // これまでに作ったコインの枚数
        public int CreatedCount { get; private set; }
        // 台の上に出ているコインの枚数
        public int ActiveCount => CreatedCount - mIdle.Count;

        /* 操作 */

        // 最初に作る枚数に達するまで、フレームを分けてコインを作る。進み具合(0〜1)を毎フレーム通知する
        public IEnumerator Prewarm(Action<float> aOnProgress = null)
        {
            if (mCoinPrefab == null)
            {
                Debug.LogError($"{name}: コインのプレハブが未設定のため、コインを作れません。インスペクタで設定してください", this);
                yield break;
            }

            while (CreatedCount < mInitialCapacity)
            {
                int count = Mathf.Min(mCreatePerFrame, mInitialCapacity - CreatedCount);
                for (int i = 0; i < count; i++) mIdle.Push(CreateCoin());
                aOnProgress?.Invoke((float)CreatedCount / mInitialCapacity);
                yield return null;
            }
            aOnProgress?.Invoke(1f);
        }

        // 指定した位置と向きでコインを 1 枚出す。使えるコインが無ければ新しく作る
        public GameObject Spawn(Vector3 aPosition, Quaternion aRotation)
        {
            if (mCoinPrefab == null) return null;

            var coin = mIdle.Count > 0 ? mIdle.Pop() : CreateCoin();
            coin.transform.SetPositionAndRotation(aPosition, aRotation);
            coin.SetActive(true);
            if (coin.TryGetComponent<Rigidbody>(out var body))
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            return coin;
        }

        // 落ちたコインを非表示にして置き場へ戻す。すでに戻っているものは何もしない
        public void Release(GameObject aCoin)
        {
            if (aCoin == null || !aCoin.activeSelf) return;

            aCoin.SetActive(false);
            mIdle.Push(aCoin);
        }

        // 非表示のコインを 1 枚作る。物理の設定はここで一度だけ行う
        // 先読みの補間は止まったコインの微小な揺れを大きく見せるため、前後の位置で補間する設定に上書きする
        private GameObject CreateCoin()
        {
            var coin = Instantiate(mCoinPrefab, transform);
            coin.SetActive(false);
            CreatedCount++;

            if (mCoinMaterial != null)
            {
                foreach (var collider in coin.GetComponentsInChildren<Collider>(true)) collider.sharedMaterial = mCoinMaterial;
            }
            if (coin.TryGetComponent<Rigidbody>(out var body))
            {
                body.maxLinearVelocity = MaxCoinSpeed;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }
            return coin;
        }
    }
}
