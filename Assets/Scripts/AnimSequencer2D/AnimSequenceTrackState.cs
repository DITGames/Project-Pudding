/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequenceTrackState.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief 1トラック分の評価結果(適用可能な絶対値)
 * =====================================*/

using UnityEngine;

namespace AnimSequencer2D
{
    public struct AnimSequenceTrackState
    {
        public Vector2 AnchoredPosition;
        public Vector2 Scale;
        public Vector3 Rotation;
        public Color Color;
        public Sprite Sprite;
        public Material Material;
        // trueの場合、Material適用時にトラック専用のインスタンスへコピーしてから使う(元アセット・他要素への影響を避ける)
        public bool InstantiateMaterial;
        // このフレームでオブジェクトを表示すべきか(AnimSequenceObject.DefaultVisible/AnimSequenceTrack.VisibilityOverrideの解決結果)
        public bool IsVisible;
    }
}
