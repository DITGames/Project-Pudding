/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageUnitView.cs
 * @author hqrse
 * @date 2026/11/17
 * @brief バトル画面の道の上に立つユニット 1 体分の表示
 * =====================================*/

using AttributeUtility;
using UnityEngine;
using UnityEngine.Rendering;

namespace PPCore
{
    // バトル画面の道の上に立つユニット 1 体分の表示。板状の絵と、攻撃や被弾の簡単な動きを持つ
    // 重なったときの前後は、見た目の定義のレイヤー優先度と出た順で決める
    public class PPStageUnitView : MonoBehaviour
    {
        /* 定数 */

        // 同じレイヤー優先度の中で出た順を表す枠の数。これを超えると順が一巡し、古いものより奥に出ることがある
        private const int SpawnOrderSlots = 1000;

        /* インスペクタ設定 */

        [Header("絵")]
        [Label("絵")]
        [SerializeField] private SpriteRenderer mSpriteRenderer;
        [Label("絵の高さ")]
        [Min(0.1f)]
        [SerializeField] private float mSpriteHeight = 4f;

        [Header("動き")]
        [Label("待機の揺れ幅")]
        [SerializeField] private float mIdleBobHeight = 0.08f;
        [Label("待機の揺れの速さ")]
        [SerializeField] private float mIdleBobSpeed = 2.5f;
        [Label("攻撃で踏み込む距離")]
        [SerializeField] private float mAttackLunge = 0.8f;
        [Label("攻撃の時間(秒)")]
        [Min(0.01f)]
        [SerializeField] private float mAttackDuration = 0.35f;
        [Label("被弾の揺れ幅")]
        [SerializeField] private float mDamageShake = 0.15f;
        [Label("被弾の時間(秒)")]
        [Min(0.01f)]
        [SerializeField] private float mDamageDuration = 0.3f;
        [Label("被弾の色")]
        [SerializeField] private Color mDamageColor = new(1f, 0.4f, 0.4f, 1f);
        [Label("回復の色")]
        [SerializeField] private Color mHealColor = new(0.5f, 1f, 0.6f, 1f);
        [Label("撃破の時間(秒)")]
        [Min(0.01f)]
        [SerializeField] private float mDefeatDuration = 0.6f;

        /* 状態 */

        private BattleUnit mBattleUnit;
        // ユニット全体の描画の前後をまとめて決める。絵が複数になっても前後が崩れないようにする
        private SortingGroup mSortingGroup;
        // 向いている向き。右向きなら 1、左向きなら -1
        private float mFacing = 1f;
        // 絵の足元を立ち位置に合わせるための絵の位置
        private Vector3 mSpriteBasePosition;
        // 待機の揺れをユニットごとにずらすための位相
        private float mIdlePhase;
        // 各動きの経過時間。負なら再生していない
        private float mAttackElapsed = -1f;
        private float mDamageElapsed = -1f;
        private float mHealElapsed = -1f;
        private float mDefeatElapsed = -1f;

        /* プロパティ */

        public BattleUnit BattleUnit => mBattleUnit;

        /* static 関数 */

        // 描画順の値を求める。優先度が大きいほど、同じ優先度なら出た順が後ほど手前になる
        public static int GetSortingOrder(int aPriority, int aSpawnIndex)
        {
            int priority = Mathf.Clamp(aPriority, PPUnitVisualDefinition.MinSortingPriority, PPUnitVisualDefinition.MaxSortingPriority);
            int slot = aSpawnIndex % SpawnOrderSlots;
            if (slot < 0) slot += SpawnOrderSlots;
            return priority * SpawnOrderSlots + slot;
        }

        /* Unity イベント */

        // 各動きを進めて、絵の位置と色へ反映する。timeScale に従うので、メニューを開いている間は止まる
        private void Update()
        {
            if (mBattleUnit == null) return;
            float dt = Time.deltaTime;
            var offset = Vector3.zero;
            var tint = Color.white;
            float alpha = 1f;

            if (mDefeatElapsed < 0f)
            {
                offset.y += Mathf.Sin(Time.time * mIdleBobSpeed + mIdlePhase) * mIdleBobHeight;
            }
            if (Advance(ref mAttackElapsed, mAttackDuration, dt, out float attackT))
            {
                offset.x += mFacing * mAttackLunge * Mathf.Sin(attackT * Mathf.PI);
            }
            if (Advance(ref mDamageElapsed, mDamageDuration, dt, out float damageT))
            {
                offset.x += Mathf.Sin(damageT * Mathf.PI * 8f) * mDamageShake * (1f - damageT);
                tint = Color.Lerp(mDamageColor, Color.white, damageT);
            }
            if (Advance(ref mHealElapsed, mDamageDuration, dt, out float healT))
            {
                tint = Color.Lerp(mHealColor, Color.white, healT);
            }
            if (mDefeatElapsed >= 0f)
            {
                // 撃破後は消えたまま残すため、経過時間は上限で止める
                mDefeatElapsed = Mathf.Min(mDefeatElapsed + dt, mDefeatDuration);
                float t = mDefeatElapsed / mDefeatDuration;
                offset.y -= t * mSpriteHeight * 0.3f;
                alpha = 1f - t;
            }

            mSpriteRenderer.transform.localPosition = mSpriteBasePosition + offset;
            var color = Color.white * tint;
            color.a = alpha;
            mSpriteRenderer.color = color;
        }

        /* 初期化・接続 */

        // 表示するユニットと絵を設定する。味方は右向き、敵は左向きにし、足元を立ち位置に合わせる
        // aSpawnIndex は表示を出した通し番号で、同じレイヤー優先度どうしの前後に使う
        public void Initialize(BattleUnit aUnit, PPUnitVisualDefinition aVisual, BattleSide aSide, int aSpawnIndex)
        {
            mBattleUnit = aUnit;
            if (!TryGetComponent(out mSortingGroup)) mSortingGroup = gameObject.AddComponent<SortingGroup>();
            mSortingGroup.sortingOrder = GetSortingOrder(aVisual != null ? aVisual.SortingPriority : 0, aSpawnIndex);
            mFacing = aSide == BattleSide.Ally ? 1f : -1f;
            mIdlePhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            var sprite = aVisual != null ? (aVisual.UnitPortrait ? aVisual.UnitPortrait : aVisual.UnitIcon) : null;
            if (sprite == null)
            {
                CustomConsole.CustomConsoleLog.Warning("Battle", $"{aUnit.UnitId} の絵が見つからないため、絵なしで表示します。ユニットの見た目定義に立ち絵かアイコンを設定してください", this);
            }
            mSpriteRenderer.sprite = sprite;
            // 元の絵は左向きのため、右を向く味方だけ反転する
            mSpriteRenderer.flipX = aSide == BattleSide.Ally;

            if (sprite != null)
            {
                // 画像の余白を除いた、絵が描かれている範囲の高さで大きさを決める
                GetVisibleRange(sprite, out float minY, out float maxY);
                float scale = mSpriteHeight / Mathf.Max(0.0001f, maxY - minY);
                mSpriteRenderer.transform.localScale = Vector3.one * scale;
                // 絵の下端が立ち位置に来るよう持ち上げる
                mSpriteBasePosition = new Vector3(0f, -minY * scale, 0f);
            }
            if (aVisual != null) mSpriteBasePosition += aVisual.SpawnOffset;
            mSpriteRenderer.transform.localPosition = mSpriteBasePosition;
        }

        /* 操作 */

        // 攻撃の動き(相手の方へ踏み込む)を再生する
        public void CommandExecuted(BattleCommandBase aCommand) => mAttackElapsed = 0f;

        // 状態異常アイコンを追加する。未実装
        public void AddStatusIcon(StatusEffect aEffect)
        {
        }

        // 状態異常アイコンを除去する。未実装
        public void RemoveStatusIcon(StatusEffect aEffect)
        {
        }

        /* 表示 */

        // 被弾の動きを再生する。撃破時は撃破の動きを優先するため、生存時のみ再生する
        public void PlayDamage(float aDmg)
        {
            if (mBattleUnit.IsAlive) mDamageElapsed = 0f;
        }

        // 回復の色を一瞬乗せる
        public void PlayHeal(float aAmt) => mHealElapsed = 0f;

        // 撃破の動き(沈みながら消える)を再生する
        public void PlayDefeat() => mDefeatElapsed = 0f;

        /* ヘルパー */

        // 絵の頂点から、透明な余白を除いた上下の範囲を求める。頂点が無ければ画像全体の範囲を返す
        private static void GetVisibleRange(Sprite aSprite, out float aMinY, out float aMaxY)
        {
            var vertices = aSprite.vertices;
            if (vertices.Length == 0)
            {
                aMinY = aSprite.bounds.min.y;
                aMaxY = aSprite.bounds.max.y;
                return;
            }
            aMinY = float.MaxValue;
            aMaxY = float.MinValue;
            foreach (var v in vertices)
            {
                aMinY = Mathf.Min(aMinY, v.y);
                aMaxY = Mathf.Max(aMaxY, v.y);
            }
        }

        // 経過時間を進め、再生中なら 0〜1 の進み具合を返す。終わったら止める
        private static bool Advance(ref float aElapsed, float aDuration, float aDeltaTime, out float aT)
        {
            aT = 0f;
            if (aElapsed < 0f) return false;
            aElapsed += aDeltaTime;
            if (aElapsed >= aDuration)
            {
                aElapsed = -1f;
                return false;
            }
            aT = aElapsed / aDuration;
            return true;
        }
    }
}
