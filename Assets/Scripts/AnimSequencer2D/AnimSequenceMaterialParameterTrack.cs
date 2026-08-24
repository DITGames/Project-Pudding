/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequenceMaterialParameterTrack.cs
 * @author hqrse
 * @date 2026/08/23
 * @brief Materialのシェーダが公開する1プロパティ分のアニメーション定義(プロパティ名+型+専用キーフレーム列)
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace AnimSequencer2D
{
    [Serializable]
    public class AnimSequenceMaterialParameterTrack
    {
        [Label("プロパティ名")]
        [SerializeField] private string mPropertyName;

        [Label("型")]
        [SerializeField] private MaterialParameterType mType;

        [Label("Floatキーフレーム", true)]
        [SerializeField] private List<AnimSequenceFloatKeyframe> mFloatKeyframes = new();

        [Label("Colorキーフレーム", true)]
        [SerializeField] private List<AnimSequenceColorKeyframe> mColorKeyframes = new();

        [Label("Vector4キーフレーム", true)]
        [SerializeField] private List<AnimSequenceVector4Keyframe> mVector4Keyframes = new();

        public string PropertyName => mPropertyName;
        public MaterialParameterType Type => mType;
        public List<AnimSequenceFloatKeyframe> FloatKeyframes => mFloatKeyframes;
        public List<AnimSequenceColorKeyframe> ColorKeyframes => mColorKeyframes;
        public List<AnimSequenceVector4Keyframe> Vector4Keyframes => mVector4Keyframes;

        // 評価は時刻昇順を前提とするため、使用中の型のキーフレームリストを時刻昇順に整列する
        public void SortKeyframes()
        {
            mFloatKeyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
            mColorKeyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
            mVector4Keyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
        }
    }
}
