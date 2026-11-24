/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPusherLoader.cs
 * @author hqrse
 * @date 2026/11/19
 * @brief プッシャーの読み込みとロード画面
 * =====================================*/

using System.Collections;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // ロード画面を出している間に、コインの置き場を作って開始時のコインを並べ、終わったらプッシャーを動かす
    // 読み込みの開始と終了の合図は PPBattleRunner が出す
    public class PPPusherLoader : MonoBehaviour
    {
        /* 定数 */

        // 進み具合のうち、置き場のコインを作る段階が占める割合(0〜1)
        private const float PrewarmRatio = 0.9f;

        /* インスペクタ設定 */

        [Label("コインの置き場")]
        [SerializeField] private PPCoinPool mPool;
        // 準備するプッシャー一式。無効になっているものは飛ばす
        [Label("プッシャー", true)]
        [SerializeField] private PPPusherField[] mFields = new PPPusherField[0];
        // 開始時のコインを並べてから、落ち着くまで待つ秒数
        [Label("並べた後に待つ秒数")]
        [Min(0f)]
        [SerializeField] private float mSettleSeconds = 1f;

        // 読み込み中だけ表示する画面。未設定なら出さない
        [Label("ロード画面")]
        [SerializeField] private PPLoadingScreenView mLoadingView;

        /* 状態 */

        // 読み込みが終わったか
        public bool IsLoaded { get; private set; }

        /* 操作 */

        // ロード画面を出し、置き場のコインを作ってから、各プッシャーへ開始時のコインを並べて落ち着くまで待つ
        public IEnumerator Load()
        {
            IsLoaded = false;
            if (mLoadingView != null) mLoadingView.Show();

            if (mPool != null) yield return mPool.Prewarm(aRatio => SetProgress(aRatio * PrewarmRatio));

            foreach (var field in mFields)
            {
                if (field == null || !field.isActiveAndEnabled) continue;
                field.Prepare(mPool);
            }

            // 物理で落ち着かせる間も進み具合を動かし、止まって見えないようにする
            for (float elapsed = 0f; elapsed < mSettleSeconds; elapsed += Time.deltaTime)
            {
                SetProgress(PrewarmRatio + (1f - PrewarmRatio) * (elapsed / mSettleSeconds));
                yield return null;
            }
            SetProgress(1f);
            IsLoaded = true;
        }

        // ロード画面を消し、各プッシャーの落下口を開けて動かし始める
        public void Begin()
        {
            if (mLoadingView != null) mLoadingView.Hide();
            foreach (var field in mFields)
            {
                if (field == null || !field.isActiveAndEnabled) continue;
                field.Begin();
            }
        }

        /* 表示 */

        // 進み具合(0〜1)をロード画面のゲージへ出す
        private void SetProgress(float aRatio)
        {
            if (mLoadingView != null) mLoadingView.SetProgress(aRatio);
        }
    }
}
