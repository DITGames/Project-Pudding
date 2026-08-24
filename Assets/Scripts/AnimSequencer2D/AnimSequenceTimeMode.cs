/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequenceTimeMode.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief アニメーションの進行に使うデルタタイムの種別を表す列挙型
 * =====================================*/

namespace AnimSequencer2D
{
    public enum AnimSequenceTimeMode
    {
        // Time.deltaTimeを基本の再生速度として使う(timeScaleの影響を受ける)
        Scaled,
        // Time.unscaledDeltaTimeを使う(常にtimeScaleの影響を受けない)
        Unscaled,
    }
}
