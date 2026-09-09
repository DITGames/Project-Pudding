/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitGrowthTemplate.cs
 * @author hqrse
 * @date 2026/09/09
 * @brief 成長曲線のテンプレートと端点・時間軸変換
 * =====================================*/

using System;
using UnityEngine;

namespace PPCore
{
    public static class PPUnitGrowthTemplate
    {
        public static readonly string[] Names = { "線形", "早熟", "晩成", "S字" };

        // 正規化したテンプレートを倍率曲線へ変換する
        public static AnimationCurve Create(int aKind, int aLevel, float aRatio)
        {
            if (aLevel == 1) return AnimationCurve.Constant(1, 1, 1);
            var keys = new Keyframe[9];
            for (var i = 0; i < keys.Length; i++)
            {
                var t = i / 8f;
                var value = aKind switch { 1 => 2 * t - t * t, 2 => t * t, 3 => t * t * (3 - 2 * t), _ => t };
                var slope = aKind switch { 1 => 2 - 2 * t, 2 => 2 * t, 3 => 6 * t * (1 - t), _ => 1 };
                var tangent = slope * (aRatio - 1) / (aLevel - 1);
                keys[i] = new Keyframe(1 + t * (aLevel - 1), 1 + value * (aRatio - 1), tangent, tangent);
            }
            return new AnimationCurve(keys);
        }

        // 接線とウェイトを含む曲線をアフィン変換する
        public static AnimationCurve Rescale(AnimationCurve aCurve, int aOldLevel, int aNewLevel, float aOldEnd, float aNewEnd)
        {
            if (aNewLevel == 1) return new AnimationCurve(new Keyframe(1, 1));
            if (aOldLevel <= 1)
                return Create(0, aNewLevel, aNewEnd);
            var keys = aCurve.keys;
            var timeScale = (aNewLevel - 1f) / (aOldLevel - 1f);
            var flatEndpoints = Mathf.Abs(aOldEnd - 1) < 0.000001f;
            var valueScale = flatEndpoints ? 1 : (aNewEnd - 1) / (aOldEnd - 1);
            for (var i = 0; i < keys.Length; i++)
            {
                keys[i].time = 1 + (keys[i].time - 1) * timeScale;
                keys[i].value = 1 + (keys[i].value - 1) * valueScale;
                keys[i].inTangent = valueScale == 0 ? 0 : keys[i].inTangent * valueScale / timeScale;
                keys[i].outTangent = valueScale == 0 ? 0 : keys[i].outTangent * valueScale / timeScale;
                // 両端同値でも途中の山谷は消さず、必要な終端差分だけを加える
                if (flatEndpoints)
                {
                    keys[i].value += (aNewEnd - 1) * (keys[i].time - 1) / (aNewLevel - 1);
                    keys[i].inTangent += (aNewEnd - 1) / (aNewLevel - 1);
                    keys[i].outTangent += (aNewEnd - 1) / (aNewLevel - 1);
                }
            }
            return Anchor(new AnimationCurve(keys) { preWrapMode = aCurve.preWrapMode, postWrapMode = aCurve.postWrapMode }, aNewLevel);
        }

        public static AnimationCurve Anchor(AnimationCurve aCurve, int aLevel)
        {
            if (aCurve == null || aCurve.length == 0) throw new ArgumentException("曲線にキーが必要です。");
            if (aLevel == 1) return new AnimationCurve(new Keyframe(1, 1));
            if (aCurve.length < 2) throw new ArgumentException("曲線に始点と終点が必要です。");
            var keys = aCurve.keys;
            keys[0].time = 1;
            keys[0].value = 1;
            keys[keys.Length - 1].time = aLevel;
            return new AnimationCurve(keys) { preWrapMode = aCurve.preWrapMode, postWrapMode = aCurve.postWrapMode };
        }
    }
}
