/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPRoleValue.cs
 * @author hqrse
 * @date 2026/08/07
 * @brief ロールと値のペア。シチュエーション係数・ロール別重みの汎用データ構造
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace PPCore
{
    // ロールと値のペア 1 件
    // シチュエーション係数・ロール別重みなど、「ロールごとに 1 つの値を持たせたい」場面で共通して使う
    // ロールが増えても、この型を使う側は行を足すだけで対応でき、コード変更は不要になる
    [Serializable]
    public struct PPRoleValue
    {
        [Label("ロール")] public PPBattleSkillRole Role;
        [Label("値")] public float Value;
    }

    // PPRoleValue のリストからロール別の値を引くための拡張メソッド
    public static class PPRoleValueListExtensions
    {
        // 指定ロールに対応する値を探す。複数該当する場合は最初の 1 件を使う
        // aRole : 探すロール
        // aFallback : 該当が無い場合に返す値
        // return : 対応する値。無ければ aFallback
        public static float Resolve(this List<PPRoleValue> aList, PPBattleSkillRole aRole, float aFallback)
        {
            if (aList == null) return aFallback;
            foreach (var entry in aList)
            {
                if (entry.Role == aRole) return entry.Value;
            }
            return aFallback;
        }

        // リストに登録されている値の平均を返す。ロールが未割り当てのユニットの扱いに使う
        // aFallback : リストが空の場合に返す値
        // return : 登録値の平均。空なら aFallback
        public static float Average(this List<PPRoleValue> aList, float aFallback)
            => (aList != null && aList.Count > 0) ? aList.Average(e => e.Value) : aFallback;
    }
}
