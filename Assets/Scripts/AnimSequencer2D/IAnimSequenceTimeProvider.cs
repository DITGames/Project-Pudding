/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IAnimSequenceTimeProvider.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief 進行に使うデルタタイムの供給元。エディタプレビューではTimeが使えないため注入で差し替える
 * =====================================*/

namespace AnimSequencer2D
{
    public interface IAnimSequenceTimeProvider
    {
        // aTimeMode : 再生中アニメーションの時間種別 / aPlayWhilePaused : ポーズ中も進行するか
        float GetDeltaTime(AnimSequenceTimeMode aTimeMode, bool aPlayWhilePaused);
    }
}
