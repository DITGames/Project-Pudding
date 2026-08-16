/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXParameterComponent.cs
 * @author hqrse
 * @date 2026/08/17
 * @brief 複数のVFX Graphを登録し、再生とパラメータ設定を外部から制御する汎用コンポーネント
 * =====================================*/

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

        [Label("登録VFX一覧", true)]
        [SerializeField] private List<VFXEntry> mVfxEntries = new();

        [Label("パラメータ一覧", true)]
        [SerializeField] private List<VFXParameterEntry> mParameters = new();

        private readonly Dictionary<string, VisualEffect> mVisualEffectCache = new();

        // 指定IDのVFXを再生する(未生成ならAddComponentで生成)。呼び出すたびに必ずPlay()で最初から再生し直す
        // aVfxId: 対象VFXのID
        public void ActivateVFX(string aVfxId)
        {
            VFXEntry entry = mVfxEntries.Find(e => e.VfxId == aVfxId);
            if (entry == null)
            {
                CustomConsoleLog.Warning(LogTag, $"VFXエントリが見つかりません: {aVfxId}", this);
                return;
            }

            if (!mVisualEffectCache.TryGetValue(aVfxId, out VisualEffect visualEffect))
            {
                visualEffect = gameObject.AddComponent<VisualEffect>();
                visualEffect.visualEffectAsset = entry.VisualEffectAsset;
                mVisualEffectCache[aVfxId] = visualEffect;
            }

            visualEffect.Play();
        }

        // 指定IDのVFXに対し、指定した1パラメータだけを設定する(再生の制御は行わない)
        // aVfxId: 対象VFXのID / aParamName: 設定するパラメータ名 / aOverride: 上書き値。nullの場合はエントリの既定値を使う
        public void ApplyParameter(string aVfxId, string aParamName, object aOverride = null)
        {
            if (!mVisualEffectCache.TryGetValue(aVfxId, out VisualEffect visualEffect))
            {
                CustomConsoleLog.Warning(LogTag, $"ActivateVFX未実行のVFXにパラメータを設定しようとしました: {aVfxId}", this);
                return;
            }

            VFXParameterEntry paramEntry = mParameters.Find(p => p.VfxId == aVfxId && p.ParamName == aParamName);
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
