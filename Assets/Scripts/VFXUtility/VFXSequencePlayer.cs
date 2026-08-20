/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequencePlayer.cs
 * @author hqrse
 * @date 2026/08/18
 * @brief VFXSequenceDefinitionのノードグラフをランタイムで再生するコンポーネント
 * =====================================*/

using System;
using System.Threading;
using AttributeUtility;
using CustomConsole;
using UnityEngine;
using UnityEngine.VFX;

namespace VFXUtility
{
    public class VFXSequencePlayer : MonoBehaviour, IVFXSequenceHost
    {
        [Label("シーケンス定義")]
        [SerializeField] private VFXSequenceDefinition mSequenceDefinition;

        [Label("プールを使用する")]
        [SerializeField] private bool mUsePooling;

        private VFXSequenceGraphExecutor mExecutor;

        // 他コンポーネントのAwake()からの参照順序に依存しないよう遅延生成する
        private VFXSequenceGraphExecutor Executor => mExecutor ??= new VFXSequenceGraphExecutor(mSequenceDefinition, this);

        // ゴールノードに到達してセッションが完了した際に発火する(引数はそのセッションのハンドル)
        public event Action<int> OnSequenceCompleted
        {
            add => Executor.OnSequenceCompleted += value;
            remove => Executor.OnSequenceCompleted -= value;
        }

        // Delay経過後にノードが発火した際に発火する(引数はそのノードの通知イベント名)
        public event Action<string> OnNodeStarted
        {
            add => Executor.OnNodeStarted += value;
            remove => Executor.OnNodeStarted -= value;
        }

        private void Update()
        {
            Executor.Tick(Time.deltaTime);

            // 再生中セッションが無くなったらUpdateを止める(Play/PlayEvent側で再開する)
            if (!Executor.HasActiveSessions)
            {
                enabled = false;
            }
        }

        // 新規セッションを開始し、全ての自動開始ルートノードから並列にフローを開始する
        // 戻り値 : Stop(ハンドル)で個別停止するためのハンドル
        public int Play()
        {
            enabled = true;
            return Executor.Play();
        }

        // 指定セッションのみを個別に停止する
        // aHandle : Play()が返したハンドル
        public void Stop(int aHandle) => Executor.Stop(aHandle);

        // 該当イベント名のイベントノードを新規セッションとして開始する
        // aEventName : 発火するイベント名
        public void PlayEvent(string aEventName)
        {
            enabled = true;
            Executor.PlayEvent(aEventName);
        }

        // 公開名を指定してパラメータ値を上書きする。以降そのパラメータが適用される際に反映される
        // aExposedName : 対象パラメータの公開名 / aValue : 上書き値
        public void SetOverride(string aExposedName, object aValue)
            => Executor.SetOverride(aExposedName, aValue);

        // オーバーライドセットの有効なエントリを一括で適用する
        // aOverrideSet : 適用するオーバーライドセット
        public void ApplyOverrideSet(VFXSequenceOverrideSet aOverrideSet)
            => Executor.ApplyOverrideSet(aOverrideSet);

        // 再生を開始し、ゴールノードへ到達するまで待機する
        // ゴールノードに到達しない構成では戻らないため、必要に応じてaTokenで打ち切る
        // aToken : 待機を打ち切るためのトークン
        public async Awaitable PlayAsync(CancellationToken aToken = default)
        {
            var completionSource = new AwaitableCompletionSource();
            int sessionHandle = 0;
            bool handleResolved = false;
            bool completed = false;

            // Play()の内部で即座にゴールへ到達する構成もあるため、購読はPlay()より先に行う
            // (Play()中に完了しうるのは今から開始するセッションだけなので、ハンドル未確定なら自分の完了とみなしてよい)
            void HandleCompleted(int aCompletedHandle)
            {
                if (!handleResolved || aCompletedHandle == sessionHandle)
                {
                    completed = true;
                    completionSource.TrySetResult();
                }
            }

            // ゴール未到達のままセッションが尽きた場合、この待機は戻らない。事故を検知できるよう警告を出す
            void HandleDiscarded(int aDiscardedHandle)
            {
                if (handleResolved && aDiscardedHandle == sessionHandle)
                {
                    CustomConsoleLog.Warning("VFXUtility",
                        $"セッション#{aDiscardedHandle}がゴールノードへ到達しないまま終了したため、PlayAsyncは完了しません。到達可能なゴールノードをグラフに配置するか、CancellationTokenで打ち切ってください");
                }
            }

            Executor.OnSequenceCompleted += HandleCompleted;
            Executor.OnSessionDiscarded += HandleDiscarded;
            try
            {
                sessionHandle = Play();
                handleResolved = true;

                // ルートノードが無い等でPlay()の時点でセッションが尽きた場合はイベントを取りこぼすため、ここで検知する
                if (!completed && !Executor.IsSessionActive(sessionHandle))
                {
                    HandleDiscarded(sessionHandle);
                }

                using (aToken.Register(() => completionSource.TrySetCanceled()))
                {
                    await completionSource.Awaitable;
                }
            }
            finally
            {
                Executor.OnSequenceCompleted -= HandleCompleted;
                Executor.OnSessionDiscarded -= HandleDiscarded;
            }
        }

        // IVFXSequenceHost実装: プールを使用する場合はVFXPoolManager経由でレンタルする
        object IVFXSequenceHost.PlayVFX(VisualEffectAsset aAsset, Vector3 aPositionOffset, Vector3 aRotationOffset, float aScaleOffset)
        {
            VisualEffect visualEffect = mUsePooling
                ? RentPooledVisualEffect(aAsset, aPositionOffset, aRotationOffset, aScaleOffset)
                : CreateVisualEffect(aAsset, aPositionOffset, aRotationOffset, aScaleOffset);

            visualEffect.Play();
            return visualEffect;
        }

        void IVFXSequenceHost.StopVFX(object aVfxHandle)
        {
            if (aVfxHandle is not VisualEffect visualEffect || visualEffect == null)
            {
                return;
            }

            if (mUsePooling)
            {
                VFXPoolManager.Instance.Return(visualEffect);
            }
            else
            {
                visualEffect.Stop();
                Destroy(visualEffect.gameObject);
            }
        }

        // 型に応じたVisualEffect.Set*を呼び出す。ColorのみVector4(r,g,b,a)に変換してSetVector4を呼ぶ
        void IVFXSequenceHost.ApplyParameter(object aVfxHandle, string aParamName, VFXParameterType aParamType, object aValue)
        {
            if (aVfxHandle is not VisualEffect visualEffect || visualEffect == null)
            {
                return;
            }

            switch (aParamType)
            {
                case VFXParameterType.Float:
                    visualEffect.SetFloat(aParamName, (float)aValue);
                    break;
                case VFXParameterType.Int:
                    visualEffect.SetInt(aParamName, (int)aValue);
                    break;
                case VFXParameterType.Bool:
                    visualEffect.SetBool(aParamName, (bool)aValue);
                    break;
                case VFXParameterType.Vector2:
                    visualEffect.SetVector2(aParamName, (Vector2)aValue);
                    break;
                case VFXParameterType.Vector3:
                    visualEffect.SetVector3(aParamName, (Vector3)aValue);
                    break;
                case VFXParameterType.Vector4:
                    visualEffect.SetVector4(aParamName, (Vector4)aValue);
                    break;
                case VFXParameterType.Color:
                    Color color = (Color)aValue;
                    visualEffect.SetVector4(aParamName, new Vector4(color.r, color.g, color.b, color.a));
                    break;
                case VFXParameterType.Event:
                    visualEffect.SendEvent(aParamName);
                    break;
            }
        }

        bool IVFXSequenceHost.IsAlive(object aVfxHandle)
        {
            return aVfxHandle is VisualEffect visualEffect && visualEffect != null && visualEffect.aliveParticleCount > 0;
        }

        // 非プール時: Player配下にローカルオフセットのまま配置する(Player本体の移動にオフセット込みで追従する)
        private VisualEffect CreateVisualEffect(VisualEffectAsset aAsset, Vector3 aPositionOffset, Vector3 aRotationOffset, float aScaleOffset)
        {
            var go = new GameObject($"VFX_{aAsset.name}");
            go.transform.SetParent(transform, false);
            go.transform.SetLocalPositionAndRotation(aPositionOffset, Quaternion.Euler(aRotationOffset));
            go.transform.localScale = Vector3.one * aScaleOffset;
            var visualEffect = go.AddComponent<VisualEffect>();
            visualEffect.visualEffectAsset = aAsset;
            return visualEffect;
        }

        // プール時: 既存通りワールド座標へ変換して一度だけ配置する(以降Playerには追従しない、という既存挙動は維持)。
        // Rent自体はスケールを扱わないため、Rent後にlocalScaleを個別設定する
        // (VFXPoolManagerのルートは常時デフォルトスケールのため、localScaleの値がそのままワールドスケール相当になる)
        private VisualEffect RentPooledVisualEffect(VisualEffectAsset aAsset, Vector3 aPositionOffset, Vector3 aRotationOffset, float aScaleOffset)
        {
            Vector3 worldPosition = transform.TransformPoint(aPositionOffset);
            Quaternion worldRotation = transform.rotation * Quaternion.Euler(aRotationOffset);
            VisualEffect visualEffect = VFXPoolManager.Instance.Rent(aAsset, worldPosition, worldRotation);
            visualEffect.transform.localScale = Vector3.one * aScaleOffset;
            return visualEffect;
        }
    }
}
