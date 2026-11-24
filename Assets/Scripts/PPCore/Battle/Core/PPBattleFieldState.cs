/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleFieldState.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief バトル画面の道の上にいるユニットと、その位置・移動
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;

namespace PPCore
{
    // 道の上にいるユニット 1 体分。位置は味方拠点(左端)からの距離で持つ
    public class PPFieldUnit
    {
        /* プロパティ */

        public BattleUnit Unit { get; }
        public BattleSide Side { get; }
        // 味方拠点(左端 0)から敵拠点(右端 = 道の長さ)へ向かう距離
        public float Position { get; set; }
        // 道を 1 秒に進む距離
        public float MoveSpeed { get; set; }
        // 進む向き。味方は右(+1)、敵は左(-1)
        public float Direction => Side == BattleSide.Ally ? 1f : -1f;

        /* 生成 */

        public PPFieldUnit(BattleUnit aUnit, BattleSide aSide, float aPosition, float aMoveSpeed)
        {
            Unit = aUnit;
            Side = aSide;
            Position = aPosition;
            MoveSpeed = aMoveSpeed;
        }
    }

    // バトル画面の 1 本道の状態。生まれたユニットを持ち、時間に合わせて相手の拠点へ向けて進める
    public class PPBattleFieldState
    {
        /* 状態 */

        private readonly List<PPFieldUnit> mUnits = new();

        /* プロパティ */

        // 道の長さ。味方拠点(0)から敵拠点まで
        public float Length { get; }
        public IReadOnlyList<PPFieldUnit> Units => mUnits;

        /* 生成 */

        public PPBattleFieldState(float aLength) => Length = Mathf.Max(0f, aLength);

        /* 操作 */

        // 道の上にユニットを加える。位置は道の範囲へ収める
        public PPFieldUnit Add(BattleUnit aUnit, BattleSide aSide, float aPosition, float aMoveSpeed)
        {
            var fieldUnit = new PPFieldUnit(aUnit, aSide, Mathf.Clamp(aPosition, 0f, Length), aMoveSpeed);
            mUnits.Add(fieldUnit);
            return fieldUnit;
        }

        // 生きているユニットを、相手の拠点へ向けて aDeltaTime 秒分進める。拠点に着いたらそこで止まる
        public virtual void Advance(float aDeltaTime)
        {
            foreach (var fieldUnit in mUnits)
            {
                if (!fieldUnit.Unit.IsAlive) continue;
                float next = fieldUnit.Position + fieldUnit.Direction * fieldUnit.MoveSpeed * aDeltaTime;
                fieldUnit.Position = Mathf.Clamp(next, 0f, Length);
            }
        }
    }
}
