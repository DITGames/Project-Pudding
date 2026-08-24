/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequenceVisibilityOverride.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief トラック側でのオブジェクト表示可否の上書き設定
 * =====================================*/

namespace AnimSequencer2D
{
    // Inheritはオブジェクト側のデフォルト表示状態(AnimSequenceObject.DefaultVisible)にそのまま従う
    public enum AnimSequenceVisibilityOverride
    {
        Inherit,
        ForceShow,
        ForceHide,
    }
}
