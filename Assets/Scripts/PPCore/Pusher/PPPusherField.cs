/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPusherField.cs
 * @author hqrse
 * @date 2026/11/19
 * @brief プッシャー一式の準備と開始
 * =====================================*/

using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // プッシャー一式（投入・押し出し板・ストッパー）の準備と開始をまとめる窓口。PPPusherLoader から呼ばれる
    public class PPPusherField : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Label("コインの投入")]
        [SerializeField] private PPCoinDispenser mDispenser;
        [Label("押し出し板")]
        [SerializeField] private PPPusherPlate mPlate;
        // 開始時に並べたコインが落下口から落ちないよう塞ぐ当たり判定。開始の合図で無効にする。未設定なら塞がない
        [Label("ストッパー")]
        [SerializeField] private GameObject mStopper;

        /* 操作 */

        // 落下口を塞ぎ、共有の置き場から開始時のコインを並べる
        public void Prepare(PPCoinPool aPool)
        {
            if (mStopper != null) mStopper.SetActive(true);
            if (mDispenser == null) return;

            mDispenser.Bind(aPool);
            mDispenser.SpawnInitialCoins();
        }

        // 落下口を開け、押し出し板の動きとコインの投入・回復を始める
        public void Begin()
        {
            if (mStopper != null) mStopper.SetActive(false);
            if (mPlate != null) mPlate.Begin();
            if (mDispenser != null) mDispenser.Begin();
        }
    }
}
