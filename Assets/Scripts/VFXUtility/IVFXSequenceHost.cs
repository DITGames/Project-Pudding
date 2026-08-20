/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IVFXSequenceHost.cs
 * @author hqrse
 * @date 2026/08/18
 * @brief VFXSequenceGraphExecutorが実際のVFX再生・停止・パラメータ適用を委譲する先のインターフェース
 * ランタイム(VFXSequencePlayer)とエディタ埋め込みプレビューの双方が実装し、
 * 実行エンジン側のDelay計算・並列分岐・イベント発火・Stop処理・完了判定ロジックを共有できるようにする
 * =====================================*/

using UnityEngine;
using UnityEngine.VFX;

namespace VFXUtility
{
    public interface IVFXSequenceHost
    {
        // 指定アセットのVFXを再生する
        // aAsset : 再生するVFXアセット
        // aPositionOffset : 基準Transform(VFXSequencePlayer)を基準としたローカル位置オフセット
        // aRotationOffset : 基準Transformを基準としたローカル回転オフセット(オイラー角)
        // aScaleOffset : 均一スケール倍率(既定1)
        // 戻り値 : 以降のStopVFX/ApplyParameter/IsAliveで使う不透明なハンドル
        object PlayVFX(VisualEffectAsset aAsset, Vector3 aPositionOffset, Vector3 aRotationOffset, float aScaleOffset);

        // 指定ハンドルのVFXを停止する
        // aVfxHandle : PlayVFXが返したハンドル
        void StopVFX(object aVfxHandle);

        // 指定ハンドルのVFXへパラメータを1件適用する
        // aVfxHandle : PlayVFXが返したハンドル / aParamName : パラメータ名 / aParamType : パラメータ型 / aValue : 設定値
        void ApplyParameter(object aVfxHandle, string aParamName, VFXParameterType aParamType, object aValue);

        // 指定ハンドルのVFXがまだ再生中かを返す
        // aVfxHandle : PlayVFXが返したハンドル
        bool IsAlive(object aVfxHandle);
    }
}
