/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequenceRuntimeTimeProvider.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief UnityEngine.Timeを用いたランタイム用のデルタタイム供給元
 * =====================================*/

using UnityEngine;

namespace AnimSequencer2D
{
    public class AnimSequenceRuntimeTimeProvider : IAnimSequenceTimeProvider
    {
        public float GetDeltaTime(AnimSequenceTimeMode aTimeMode, bool aPlayWhilePaused)
        {
            // Unscaledは常にtimeScaleの影響を受けないため、aPlayWhilePausedの値によらず非スケール時間で進む
            if (aTimeMode == AnimSequenceTimeMode.Unscaled)
            {
                return Time.unscaledDeltaTime;
            }

            // Scaled + ポーズ中も進行する場合は、timeScaleが0の間だけ非スケール時間へフォールバックする
            // (スロー演出には追従しつつ、ポーズだけでは止まらない、という使い分けができる)
            if (aPlayWhilePaused && Mathf.Approximately(Time.timeScale, 0f))
            {
                return Time.unscaledDeltaTime;
            }

            return Time.deltaTime;
        }
    }
}
