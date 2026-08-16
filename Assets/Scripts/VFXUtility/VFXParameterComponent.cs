/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXParameterComponent.cs
 * @author hqrse
 * @date 2026/08/17
 * @brief 複数のVFX Graphを登録し、再生とパラメータ設定を外部から制御する汎用コンポーネント
 * =====================================*/

using System;
using System.Collections;
using System.Collections.Generic;
using CommandBattleCore;
using CustomConsole;
using UnityEngine;
using UnityEngine.VFX;

namespace VFXUtility
{
    public class VFXParameterComponent : MonoBehaviour
    {
        private const string LogTag = "VFXUtility";

        [Label("VFXプロファイル")]
        [SerializeField] private VFXProfileDefinition mProfile;

        [Label("プールを使用する")]
        [SerializeField] private bool mUsePooling;

        private readonly Dictionary<string, VisualEffect> mVisualEffectCache = new();
        private readonly Dictionary<string, Coroutine> mWatchCoroutines = new();

        // VFXの再生が完了した際に通知する(引数は再生完了したVFX ID)
        public event Action<string> OnVFXCompleted;

        // 指定IDのVFXを再生する(未生成ならプールまたはAddComponentで生成)。呼び出すたびに必ずPlay()で最初から再生し直す
        // aVfxId: 対象VFXのID / aOnCompleted: 再生完了時に呼ばれるコールバック(省略可)
        public void ActivateVFX(string aVfxId, Action aOnCompleted = null)
        {
            if (mProfile == null)
            {
                CustomConsoleLog.Warning(LogTag, "VFXプロファイルが未設定です", this);
                return;
            }

            VFXEntry entry = mProfile.FindEntry(aVfxId);
            if (entry == null)
            {
                CustomConsoleLog.Warning(LogTag, $"VFXエントリが見つかりません: {aVfxId}", this);
                return;
            }

            if (mWatchCoroutines.TryGetValue(aVfxId, out Coroutine running))
            {
                StopCoroutine(running);
                mWatchCoroutines.Remove(aVfxId);
            }

            if (!mVisualEffectCache.TryGetValue(aVfxId, out VisualEffect visualEffect) || visualEffect == null)
            {
                if (mUsePooling)
                {
                    visualEffect = VFXPoolManager.Instance.Rent(entry.VisualEffectAsset, transform.position, transform.rotation);
                }
                else
                {
                    visualEffect = gameObject.AddComponent<VisualEffect>();
                    visualEffect.visualEffectAsset = entry.VisualEffectAsset;
                }

                mVisualEffectCache[aVfxId] = visualEffect;
            }

            visualEffect.Play();
            mWatchCoroutines[aVfxId] = StartCoroutine(WatchCompletion(aVfxId, visualEffect, aOnCompleted));
        }

        // 指定IDのVFXを停止する(プール使用時はプールへ返却する)
        // aVfxId: 対象VFXのID
        public void StopVFX(string aVfxId)
        {
            if (mWatchCoroutines.TryGetValue(aVfxId, out Coroutine running))
            {
                StopCoroutine(running);
                mWatchCoroutines.Remove(aVfxId);
            }

            if (!mVisualEffectCache.TryGetValue(aVfxId, out VisualEffect visualEffect))
            {
                return;
            }

            mVisualEffectCache.Remove(aVfxId);

            if (mUsePooling)
            {
                VFXPoolManager.Instance.Return(visualEffect);
            }
            else
            {
                visualEffect.Stop();
            }
        }

        // 指定IDのVFXに対し、指定した1パラメータだけを設定する(再生の制御は行わない)
        // aVfxId: 対象VFXのID / aParamName: 設定するパラメータ名 / aOverride: 上書き値。nullの場合はエントリの既定値を使う
        public void ApplyParameter(string aVfxId, string aParamName, object aOverride = null)
        {
            if (mProfile == null)
            {
                CustomConsoleLog.Warning(LogTag, "VFXプロファイルが未設定です", this);
                return;
            }

            if (!mVisualEffectCache.TryGetValue(aVfxId, out VisualEffect visualEffect))
            {
                CustomConsoleLog.Warning(LogTag, $"ActivateVFX未実行のVFXにパラメータを設定しようとしました: {aVfxId}", this);
                return;
            }

            VFXParameterEntry paramEntry = mProfile.FindParameter(aVfxId, aParamName);
            if (paramEntry == null)
            {
                CustomConsoleLog.Warning(LogTag, $"パラメータエントリが見つかりません: {aVfxId}/{aParamName}", this);
                return;
            }

            if (!TryResolveValue(paramEntry, aOverride, out object value))
            {
                return;
            }

            ApplyToVisualEffect(visualEffect, aParamName, paramEntry.ParamType, value);
        }

        // aSequenceのステップを順番に再生する(各ステップはVfxId + 直前ステップからの遅延 + 再生後に適用するパラメータ名の組)
        // aSequence: 再生するシーケンス定義 / aOnCompleted: 全ステップ再生後に呼ばれるコールバック(省略可)
        public Coroutine PlaySequence(VFXSequenceDefinition aSequence, Action aOnCompleted = null)
        {
            if (aSequence == null)
            {
                CustomConsoleLog.Warning(LogTag, "VFXシーケンスが未設定です", this);
                return null;
            }

            return StartCoroutine(RunSequence(aSequence, aOnCompleted));
        }

        private IEnumerator RunSequence(VFXSequenceDefinition aSequence, Action aOnCompleted)
        {
            foreach (VFXSequenceStep step in aSequence.Steps)
            {
                if (step.DelaySeconds > 0f)
                {
                    yield return new WaitForSeconds(step.DelaySeconds);
                }

                ActivateVFX(step.VfxId);
                foreach (string paramName in step.ParamNamesToApply)
                {
                    ApplyParameter(step.VfxId, paramName);
                }
            }

            aOnCompleted?.Invoke();
        }

        // VFXの生存状況を監視し、完了したらコールバックとイベントを発火する(プール使用時は完了後にプールへ返却する)
        // aliveParticleCountに基づく判定のため、パーティクルを生成しないVFXやスポーンに遅延があるVFXでは即座に完了扱いになる場合がある
        private IEnumerator WatchCompletion(string aVfxId, VisualEffect aVisualEffect, Action aOnCompleted)
        {
            yield return null;

            while (aVisualEffect != null && aVisualEffect.aliveParticleCount > 0)
            {
                yield return null;
            }

            mWatchCoroutines.Remove(aVfxId);
            aOnCompleted?.Invoke();
            OnVFXCompleted?.Invoke(aVfxId);

            if (mUsePooling && aVisualEffect != null
                && mVisualEffectCache.TryGetValue(aVfxId, out VisualEffect cached) && cached == aVisualEffect)
            {
                mVisualEffectCache.Remove(aVfxId);
                VFXPoolManager.Instance.Return(aVisualEffect);
            }
        }

        // 設定値を決定する。aOverrideがnullならエントリの既定値、非nullなら型一致を確認した上でaOverrideを採用する
        // aEntry: 対象パラメータエントリ / aOverride: 上書き値 / aValue: 決定した設定値の出力先
        // 戻り値: 値を決定できたか(型不一致の場合はfalse)
        private bool TryResolveValue(VFXParameterEntry aEntry, object aOverride, out object aValue)
        {
            if (aOverride == null)
            {
                aValue = aEntry.ParamType switch
                {
                    VFXParameterType.Float => aEntry.FloatValue,
                    VFXParameterType.Int => aEntry.IntValue,
                    VFXParameterType.Bool => aEntry.BoolValue,
                    VFXParameterType.Vector2 => aEntry.Vector2Value,
                    VFXParameterType.Vector3 => aEntry.Vector3Value,
                    VFXParameterType.Vector4 => aEntry.Vector4Value,
                    VFXParameterType.Color => aEntry.ColorValue,
                    VFXParameterType.Event => null,
                    _ => null,
                };
                return true;
            }

            bool typeMatches = aEntry.ParamType switch
            {
                VFXParameterType.Float => aOverride is float,
                VFXParameterType.Int => aOverride is int,
                VFXParameterType.Bool => aOverride is bool,
                VFXParameterType.Vector2 => aOverride is Vector2,
                VFXParameterType.Vector3 => aOverride is Vector3,
                VFXParameterType.Vector4 => aOverride is Vector4,
                VFXParameterType.Color => aOverride is Color,
                VFXParameterType.Event => false,
                _ => false,
            };

            if (!typeMatches)
            {
                CustomConsoleLog.Warning(LogTag, $"aOverrideの型が宣言型と一致しません: {aEntry.VfxId}/{aEntry.ParamName}", this);
                aValue = null;
                return false;
            }

            aValue = aOverride;
            return true;
        }

        // 型に応じたVisualEffect.Set*を呼び出す。ColorのみVector4(r,g,b,a)に変換してSetVector4を呼ぶ
        // aVisualEffect: 設定対象 / aParamName: パラメータ名 / aType: パラメータ型 / aValue: 設定値
        private void ApplyToVisualEffect(VisualEffect aVisualEffect, string aParamName, VFXParameterType aType, object aValue)
        {
            switch (aType)
            {
                case VFXParameterType.Float:
                    aVisualEffect.SetFloat(aParamName, (float)aValue);
                    break;
                case VFXParameterType.Int:
                    aVisualEffect.SetInt(aParamName, (int)aValue);
                    break;
                case VFXParameterType.Bool:
                    aVisualEffect.SetBool(aParamName, (bool)aValue);
                    break;
                case VFXParameterType.Vector2:
                    aVisualEffect.SetVector2(aParamName, (Vector2)aValue);
                    break;
                case VFXParameterType.Vector3:
                    aVisualEffect.SetVector3(aParamName, (Vector3)aValue);
                    break;
                case VFXParameterType.Vector4:
                    aVisualEffect.SetVector4(aParamName, (Vector4)aValue);
                    break;
                case VFXParameterType.Color:
                    Color color = (Color)aValue;
                    aVisualEffect.SetVector4(aParamName, new Vector4(color.r, color.g, color.b, color.a));
                    break;
                case VFXParameterType.Event:
                    aVisualEffect.SendEvent(aParamName);
                    break;
            }
        }
    }
}
