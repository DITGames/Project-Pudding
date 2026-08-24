/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequenceTrack.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief 動かす対象(AnimSequenceObject)を、1つのアニメーションキー内でどう時間変化させるかの情報。
 * チャンネルごとにキーフレームリストを持つ。基準(デフォルト)の見た目自体はAnimSequenceObject側が持つ
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace AnimSequencer2D
{
    [Serializable]
    public class AnimSequenceTrack
    {
        // 参照先のAnimSequenceObject.ObjectId。同じオブジェクトを複数のアニメーションキーが独立して
        // 時間変化させられるよう、基準値(Sprite/Position/Scale/Rotation/Color/Material)は持たずIDのみ持つ
        [Label("トラックID")]
        [SerializeField] private string mTrackId = "Main";

        // 既定はInherit(参照先オブジェクトのデフォルト表示状態にそのまま従う)
        [Label("表示")]
        [SerializeField] private AnimSequenceVisibilityOverride mVisibilityOverride = AnimSequenceVisibilityOverride.Inherit;

        [Label("位置(相対)", true)]
        [SerializeField] private List<AnimSequenceVector2Keyframe> mPositionKeyframes = new();

        [Label("スケール(倍率)", true)]
        [SerializeField] private List<AnimSequenceVector2Keyframe> mScaleKeyframes = new();

        [Label("回転(相対度, X/Y/Z)", true)]
        [SerializeField] private List<AnimSequenceVector3Keyframe> mRotationKeyframes = new();

        [Label("色(絶対)", true)]
        [SerializeField] private List<AnimSequenceColorKeyframe> mColorKeyframes = new();

        [Label("画像切り替え", true)]
        [SerializeField] private List<AnimSequenceSpriteKeyframe> mSpriteKeyframes = new();

        [Label("Material切り替え", true)]
        [SerializeField] private List<AnimSequenceMaterialKeyframe> mMaterialKeyframes = new();

        [Label("Materialパラメータ", true)]
        [SerializeField] private List<AnimSequenceMaterialParameterTrack> mMaterialParameterTracks = new();

        public string TrackId => mTrackId;
        public AnimSequenceVisibilityOverride VisibilityOverride => mVisibilityOverride;
        public List<AnimSequenceVector2Keyframe> PositionKeyframes => mPositionKeyframes;
        public List<AnimSequenceVector2Keyframe> ScaleKeyframes => mScaleKeyframes;
        public List<AnimSequenceVector3Keyframe> RotationKeyframes => mRotationKeyframes;
        public List<AnimSequenceColorKeyframe> ColorKeyframes => mColorKeyframes;
        public List<AnimSequenceSpriteKeyframe> SpriteKeyframes => mSpriteKeyframes;
        public List<AnimSequenceMaterialKeyframe> MaterialKeyframes => mMaterialKeyframes;
        public List<AnimSequenceMaterialParameterTrack> MaterialParameterTracks => mMaterialParameterTracks;

        // このトラックの表示上書き設定と、参照先オブジェクトのデフォルト表示状態を合成して最終的な表示可否を求める
        // aObject : 参照先オブジェクト(基準のデフォルト表示状態を持つ。nullの場合はtrue扱い)
        public bool ResolveVisible(AnimSequenceObject aObject) => mVisibilityOverride switch
        {
            AnimSequenceVisibilityOverride.ForceShow => true,
            AnimSequenceVisibilityOverride.ForceHide => false,
            _ => aObject?.DefaultVisible ?? true,
        };

        // 評価は時刻昇順を前提とするため、各チャンネルのキーフレームを時刻昇順に整列する
        public void SortKeyframes()
        {
            mPositionKeyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
            mScaleKeyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
            mRotationKeyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
            mColorKeyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
            mSpriteKeyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
            mMaterialKeyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
            foreach (AnimSequenceMaterialParameterTrack paramTrack in mMaterialParameterTracks)
            {
                paramTrack.SortKeyframes();
            }
        }
    }
}
