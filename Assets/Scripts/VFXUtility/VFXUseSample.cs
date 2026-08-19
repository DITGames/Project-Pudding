/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXUseSample.cs
 * @author hqrse
 * @date 2026/08/18
 * @brief VFXSequencePlayerの利用例。一定間隔でSpawnCountを増やしながらシーケンスを再生し直すサンプル
 * =====================================*/

using System;
using System.Threading;
using AttributeUtility;
using CustomConsole;
using UnityEngine;

namespace VFXUtility
{
    public class VFXUseSample : MonoBehaviour
    {
        [Label("シーケンス再生コンポーネント")]
        [SerializeField] private VFXSequencePlayer mVfxSequencePlayer;

        [Label("使用するオーバーライドセット")]
        [SerializeField] private VFXSequenceOverrideSet mVfxSequenceOverrideSet;

        private CancellationTokenSource mCancellationTokenSource;

        private async Awaitable Start()
        {
            if (mVfxSequencePlayer == null)
            {
                mVfxSequencePlayer = GetComponent<VFXSequencePlayer>();
            }

            if (mVfxSequencePlayer == null)
            {
                return;
            }

            if (mVfxSequenceOverrideSet != null)
            {
                mVfxSequencePlayer.ApplyOverrideSet(mVfxSequenceOverrideSet);
            }

            // ゴールノード未到達のまま破棄された場合に待機し続けないよう、破棄時に打ち切れるトークンを渡す
            mCancellationTokenSource = new CancellationTokenSource();

            try
            {
                await mVfxSequencePlayer.PlayAsync(mCancellationTokenSource.Token);
                CustomConsoleLog.Log("VFXUtility", "VFXUseSample: シーケンス再生が完了しました");
            }
            catch (OperationCanceledException)
            {
                // OnDestroy等による打ち切り
            }
        }

        private void OnDestroy()
        {
            mCancellationTokenSource?.Cancel();
            mCancellationTokenSource?.Dispose();
        }
    }
}
