/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleFieldState.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief バトル画面の道の上にいるユニットの位置・移動・攻撃の間合い・ノックバック
 * =====================================*/

using System;
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
        // 生成元の定義。射程・攻撃間隔などを引く。定義が無いユニットは null
        public PPUnitDefinition Definition { get; }
        // 味方拠点(左端 0)から敵拠点(右端 = 道の長さ)へ向かう距離
        public float Position { get; set; }
        // 道を 1 秒に進む距離
        public float MoveSpeed { get; set; }
        // 進む向き。味方は右(+1)、敵は左(-1)
        public float Direction => Side == BattleSide.Ally ? 1f : -1f;
        // 射程内の敵の前で止まっているか
        public bool IsEngaged { get; set; }
        // 次の攻撃までの残り秒数
        public float AttackTimer { get; set; }
        // これから押し戻される残りの距離。0 より大きい間は動かず攻撃もしない
        public float KnockbackRemaining { get; set; }
        // HP の区切りのうち、すでに切った数。ノックバックの回数から 1 を引いた数まで増える
        public int PassedHpThresholds { get; set; }

        /* 生成 */

        public PPFieldUnit(BattleUnit aUnit, BattleSide aSide, float aPosition, float aMoveSpeed)
        {
            Unit = aUnit;
            Side = aSide;
            Definition = aUnit.SourceDefinition as PPUnitDefinition;
            Position = aPosition;
            MoveSpeed = aMoveSpeed;
        }
    }

    // 対象を外から決めて渡すリゾルバ。道の上の間合いで選んだ相手を、通常攻撃の対象にする
    public class PPFieldTargetResolver : ITargetResolver
    {
        /* 状態 */

        private readonly List<BattleUnit> mTargets = new();

        /* 操作 */

        public void SetTargets(List<BattleUnit> aTargets)
        {
            mTargets.Clear();
            mTargets.AddRange(aTargets);
        }

        // 後段のフィルタが書き換えてもよいよう、写しを返す
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext) => new(mTargets);
    }

    // バトル画面の 1 本道の状態。生まれたユニットを相手の拠点へ進め、射程内の敵の前で止めて攻撃の時機を知らせる
    // 攻撃そのもの(ダメージ計算)は BattleManager が行う
    public class PPBattleFieldState
    {
        /* static 変数 */

        // 撃破されて押し戻しも終わったユニットを外す条件。毎フレームの割り当てを避けるため使い回す
        private static readonly Predicate<PPFieldUnit> sIsFinished = u => !u.Unit.IsAlive && u.KnockbackRemaining <= 0f;

        /* 状態 */

        private readonly List<PPFieldUnit> mUnits = new();
        private readonly Dictionary<BattleUnit, PPFieldUnit> mByUnit = new();
        private readonly List<BattleUnit> mTargetBuffer = new();

        /* プロパティ */

        // 道の長さ。味方拠点(0)から敵拠点まで
        public float Length { get; }
        // 相手の拠点の中心から、この距離の手前で止まる
        public float BaseFrontDistance { get; }
        // ノックバックで押し戻す距離と、押し戻される速さ(1 秒に進む距離)
        public float KnockbackDistance { get; }
        public float KnockbackSpeed { get; }
        public IReadOnlyList<PPFieldUnit> Units => mUnits;

        /* 生成 */

        public PPBattleFieldState(float aLength, float aBaseFrontDistance = 4f, float aKnockbackDistance = 3f, float aKnockbackSpeed = 12f)
        {
            Length = Mathf.Max(0f, aLength);
            BaseFrontDistance = Mathf.Clamp(aBaseFrontDistance, 0f, Length * 0.5f);
            KnockbackDistance = Mathf.Max(0f, aKnockbackDistance);
            KnockbackSpeed = Mathf.Max(0.01f, aKnockbackSpeed);
        }

        /* 操作 */

        // 道の上にユニットを加える。位置は道の範囲へ収める
        public PPFieldUnit Add(BattleUnit aUnit, BattleSide aSide, float aPosition, float aMoveSpeed)
        {
            var fieldUnit = new PPFieldUnit(aUnit, aSide, Mathf.Clamp(aPosition, 0f, Length), aMoveSpeed);
            mUnits.Add(fieldUnit);
            mByUnit[aUnit] = fieldUnit;
            return fieldUnit;
        }

        // aDeltaTime 秒分進める。押し戻され中のユニットは後ろへ下がり、射程内に敵がいれば止まって溜め・攻撃間隔を数え、
        // 攻撃の時機が来たら aAttack(攻撃するユニット, 当てる相手)を呼ぶ。敵がいなければ相手の拠点へ進み、拠点の手前で止まる
        public virtual void Advance(float aDeltaTime, Action<PPFieldUnit, List<BattleUnit>> aAttack)
        {
            // 攻撃で撃破や押し戻しが起きても並びは変わらないため、数え回しで回す
            for (int i = 0; i < mUnits.Count; i++)
            {
                var fieldUnit = mUnits[i];
                if (fieldUnit.KnockbackRemaining > 0f)
                {
                    float step = Mathf.Min(fieldUnit.KnockbackRemaining, KnockbackSpeed * aDeltaTime);
                    fieldUnit.KnockbackRemaining -= step;
                    fieldUnit.Position = Mathf.Clamp(fieldUnit.Position - fieldUnit.Direction * step, 0f, Length);
                    continue;
                }
                if (!fieldUnit.Unit.IsAlive) continue;

                var nearest = FindNearestEnemyInRange(fieldUnit, out float nearestDistance);
                if (nearest == null)
                {
                    fieldUnit.IsEngaged = false;
                    // 止まる線の先(拠点の中)で生まれたユニットは、線まで進んでから止まる
                    float remaining = Mathf.Max(0f, (GetStopLine(fieldUnit.Side) - fieldUnit.Position) * fieldUnit.Direction);
                    fieldUnit.Position += fieldUnit.Direction * Mathf.Min(remaining, fieldUnit.MoveSpeed * aDeltaTime);
                    continue;
                }

                if (!fieldUnit.IsEngaged)
                {
                    fieldUnit.IsEngaged = true;
                    fieldUnit.AttackTimer = fieldUnit.Definition != null ? fieldUnit.Definition.AttackWindup : 0f;
                }
                fieldUnit.AttackTimer -= aDeltaTime;
                if (fieldUnit.AttackTimer > 0f) continue;

                CollectTargets(fieldUnit, nearest, nearestDistance);
                float interval = fieldUnit.Definition != null ? fieldUnit.Definition.AttackInterval : 1f;
                // 1 フレームで何回も当てないよう、遅れは次の 1 回分までにする
                fieldUnit.AttackTimer = Mathf.Max(fieldUnit.AttackTimer + interval, 0f);
                aAttack?.Invoke(fieldUnit, mTargetBuffer);
            }

            RemoveFinished();
        }

        // ダメージを受けたユニットが HP の区切りを切っていれば押し戻す。区切りをまとめて切っても押し戻しは 1 回
        public void NotifyDamaged(BattleUnit aUnit)
        {
            if (!mByUnit.TryGetValue(aUnit, out var fieldUnit) || !aUnit.IsAlive) return;

            int count = fieldUnit.Definition != null ? fieldUnit.Definition.KnockbackCount : 1;
            float hpRate = aUnit.Parameters.Hp.Ratio;
            bool passed = false;
            // 区切りは HP の (回数 - k) / 回数。最後の 1 回は撃破時に回す
            while (fieldUnit.PassedHpThresholds < count - 1
                && hpRate <= (float)(count - 1 - fieldUnit.PassedHpThresholds) / count)
            {
                fieldUnit.PassedHpThresholds++;
                passed = true;
            }
            if (passed) Knockback(aUnit);
        }

        // 撃破されたユニットを押し戻す。押し戻しが終わったら道から外す
        public void NotifyDefeated(BattleUnit aUnit) => Knockback(aUnit);

        // ユニットを後ろへ押し戻す。押し戻されている間は動かず攻撃もしない。押し戻しの後は溜めからやり直す
        public void Knockback(BattleUnit aUnit)
        {
            if (!mByUnit.TryGetValue(aUnit, out var fieldUnit)) return;
            fieldUnit.KnockbackRemaining = KnockbackDistance;
            fieldUnit.IsEngaged = false;
        }

        /* 問い合わせ */

        public PPFieldUnit Find(BattleUnit aUnit) => aUnit != null ? mByUnit.GetValueOrDefault(aUnit) : null;

        // その陣営のユニットが進んで止まる位置。相手の拠点の手前
        public float GetStopLine(BattleSide aSide) => aSide == BattleSide.Ally ? Length - BaseFrontDistance : BaseFrontDistance;

        // 進む向きの前方で、射程内にいる最も近い生きた敵を返す。いなければ null
        private PPFieldUnit FindNearestEnemyInRange(PPFieldUnit aSelf, out float aDistance)
        {
            float range = aSelf.Definition != null ? aSelf.Definition.AttackRange : 1f;
            PPFieldUnit nearest = null;
            aDistance = float.MaxValue;
            foreach (var other in mUnits)
            {
                if (other.Side == aSelf.Side || !other.Unit.IsAlive) continue;
                float distance = (other.Position - aSelf.Position) * aSelf.Direction;
                if (distance < 0f || distance > range || distance >= aDistance) continue;
                nearest = other;
                aDistance = distance;
            }
            return nearest;
        }

        // 当てる相手を集める。単体なら最も近い敵だけ、範囲なら最も近い敵から奥へ範囲の広さまでにいる敵すべて
        private void CollectTargets(PPFieldUnit aSelf, PPFieldUnit aNearest, float aNearestDistance)
        {
            mTargetBuffer.Clear();
            if (aSelf.Definition == null || !aSelf.Definition.IsAreaAttack)
            {
                mTargetBuffer.Add(aNearest.Unit);
                return;
            }

            float far = aNearestDistance + aSelf.Definition.AreaWidth;
            foreach (var other in mUnits)
            {
                if (other.Side == aSelf.Side || !other.Unit.IsAlive) continue;
                float distance = (other.Position - aSelf.Position) * aSelf.Direction;
                if (distance >= aNearestDistance && distance <= far) mTargetBuffer.Add(other.Unit);
            }
        }

        /* ヘルパー */

        private void RemoveFinished()
        {
            for (int i = mUnits.Count - 1; i >= 0; i--)
            {
                if (!sIsFinished(mUnits[i])) continue;
                mByUnit.Remove(mUnits[i].Unit);
                mUnits.RemoveAt(i);
            }
        }
    }
}
