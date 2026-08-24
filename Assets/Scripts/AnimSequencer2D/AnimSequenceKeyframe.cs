/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequenceKeyframe.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief 2Dアニメーションの各プロパティが持つキーフレーム型の定義
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace AnimSequencer2D
{
    // 全キーフレーム共通の基底。非ポリモーフィックなSerializable継承のため、派生側でもフラットにシリアライズされる
    [Serializable]
    public abstract class AnimSequenceKeyframeBase
    {
        [Label("時刻(秒)")]
        [SerializeField] private float mTime;

        // タイムライン上で選択・ドラッグ対象を追跡するための内部ID。並べ替え後も選択を保てるようにする
        [SerializeField, HideInInspector] private string mKeyframeId = Guid.NewGuid().ToString("N");

        public float Time => mTime;
        public string KeyframeId => mKeyframeId;
    }

    [Serializable]
    public class AnimSequenceVector2Keyframe : AnimSequenceKeyframeBase
    {
        [Label("値")]
        [SerializeField] private Vector2 mValue;

        public Vector2 Value => mValue;
    }

    [Serializable]
    public class AnimSequenceVector3Keyframe : AnimSequenceKeyframeBase
    {
        [Label("値")]
        [SerializeField] private Vector3 mValue;

        public Vector3 Value => mValue;
    }

    [Serializable]
    public class AnimSequenceColorKeyframe : AnimSequenceKeyframeBase
    {
        [Label("色")]
        [SerializeField] private Color mValue = Color.white;

        public Color Value => mValue;
    }

    // 画像切り替えは補間しないため、他のキーフレームと同じ基底からそのまま派生させる
    [Serializable]
    public class AnimSequenceSpriteKeyframe : AnimSequenceKeyframeBase
    {
        [Label("スプライト")]
        [SerializeField] private Sprite mSprite;

        public Sprite Sprite => mSprite;
    }

    // Material切り替えも画像切り替えと同様に補間しない
    [Serializable]
    public class AnimSequenceMaterialKeyframe : AnimSequenceKeyframeBase
    {
        [Label("Material")]
        [SerializeField] private Material mMaterial;

        public Material Material => mMaterial;
    }

    [Serializable]
    public class AnimSequenceFloatKeyframe : AnimSequenceKeyframeBase
    {
        [Label("値")]
        [SerializeField] private float mValue;

        public float Value => mValue;
    }

    [Serializable]
    public class AnimSequenceVector4Keyframe : AnimSequenceKeyframeBase
    {
        [Label("値")]
        [SerializeField] private Vector4 mValue;

        public Vector4 Value => mValue;
    }
}
