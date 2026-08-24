/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PreviewAnimSequenceTimeProvider.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief エディタにはTime.deltaTimeが無いため、ウィンドウ側が算出したエディタ経過時間をそのまま返す時間供給元
 * =====================================*/

namespace AnimSequencer2D.Editor
{
    internal class PreviewAnimSequenceTimeProvider : IAnimSequenceTimeProvider
    {
        private float mDeltaTime;

        // aDeltaTime : EditorApplication.timeSinceStartupの差分
        public void SetEditorDeltaTime(float aDeltaTime) => mDeltaTime = aDeltaTime;

        // プレビューにはtimeScaleの概念が無いため、時間種別によらず同じ値を返す
        public float GetDeltaTime(AnimSequenceTimeMode aTimeMode, bool aPlayWhilePaused) => mDeltaTime;
    }
}
