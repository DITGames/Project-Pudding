/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequenceEndBehavior.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief アニメーションが末尾(Duration)へ到達した際の挙動を表す列挙型
 * =====================================*/

namespace AnimSequencer2D
{
    public enum AnimSequenceEndBehavior
    {
        // 末尾で停止する
        Stop,
        // 先頭に戻って継続再生する(周回境界では開始/終了イベントは発火しない)
        Loop,
        // 別のアニメーションキーへ継続再生する
        Transition,
    }
}
