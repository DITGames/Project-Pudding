/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageUnitView.cs
 * @author hqrse
 * @date 2026/11/17
 * @brief プッシャーのステージ上に立つユニット 1 体分の表示
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;
using UnityEngine.UI;

namespace PPCore
{
    // ステージ上に立つユニット 1 体分の表示。板状の絵と頭上・足元のステータス、攻撃や被弾の簡単な動きを持つ
    // 絵とステータスは常にカメラの方を向く
    public class PPStageUnitView : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Header("絵")]
        [Label("絵")]
        [SerializeField] private SpriteRenderer mSpriteRenderer;
        [Label("絵の高さ")]
        [Min(0.1f)]
        [SerializeField] private float mSpriteHeight = 4f;
        [Label("選ばれていないときの色")]
        [SerializeField] private Color mDimColor = new(0.35f, 0.35f, 0.35f, 1f);

        [Header("ステータス")]
        [Label("ステータスウィジェット")]
        [SerializeField] private PPUnitStatusWidget mStatusWidget;
        // ステータスのキャンバス。ユニットの直下に置く
        [Label("ステータスの表示")]
        [SerializeField] private Transform mStatusRoot;
        // 足元の表示が台に隠れないよう、見た目の位置を変えずにカメラ側へ寄せる距離(ワールド単位)
        [Label("ステータスを手前に出す距離")]
        [Min(0f)]
        [SerializeField] private float mStatusPull = 4f;
        // 絵より上に置く部分(選択中のスキル・HP)
        [Label("頭上の表示")]
        [SerializeField] private RectTransform mTopBlock;
        // 絵より下に置く部分(コインゲージ・スキルゲージ)
        [Label("足元の表示")]
        [SerializeField] private RectTransform mBottomBlock;
        [Label("狙う相手のアイコン")]
        [SerializeField] private Image mTargetIcon;
        // 絵とステータスの間を空ける幅(ワールド単位)
        [Label("ステータスの間隔")]
        [SerializeField] private float mStatusMargin = 0.2f;

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
        // ステータスウィジェットへ渡した表示ソース。破棄時に購読を解除するため保持する
        private PPBattleUnitStatusSource mStatusSource;
        // 狙う相手のアイコンを引く関数。バインダーが渡す
        private Func<BattleUnit, Sprite> mIconResolver;
        // 向いている向き。右向きなら 1、左向きなら -1
        private float mFacing = 1f;
        // 絵の足元を立ち位置に合わせるための絵の位置
        private Vector3 mSpriteBasePosition;
        private bool mIsDimmed;
        // 待機の揺れをユニットごとにずらすための位相
        private float mIdlePhase;
        // 各動きの経過時間。負なら再生していない
        private float mAttackElapsed = -1f;
        private float mDamageElapsed = -1f;
        private float mHealElapsed = -1f;
        private float mDefeatElapsed = -1f;
        // 手前へ寄せる前のステータスの縮尺
        private Vector3 mStatusBaseScale = Vector3.one;

        /* プロパティ */

        public BattleUnit BattleUnit => mBattleUnit;

        /* Unity イベント */

        // 絵とステータスをカメラへ向け、ステータスをカメラとの間へ寄せる
        // 寄せた分だけ縮めるので、画面上の位置と大きさはユニットの位置に置いたときと変わらない
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.rotation = cam.transform.rotation;

            var toCamera = cam.transform.position - transform.position;
            float distance = toCamera.magnitude;
            if (distance <= 0f) return;
            float pull = Mathf.Min(mStatusPull, distance * 0.5f);
            mStatusRoot.position = transform.position + toCamera / distance * pull;
            mStatusRoot.localScale = mStatusBaseScale * ((distance - pull) / distance);
        }

        // 各動きを進めて、絵の位置と色へ反映する。timeScale に従うので、コマンド入力中は止まる
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
            var baseColor = mIsDimmed ? mDimColor : Color.white;
            var color = baseColor * tint;
            color.a = alpha;
            mSpriteRenderer.color = color;
        }

        // 破棄時にステータス表示の購読を解除する
        private void OnDestroy()
        {
            if (mStatusWidget != null) mStatusWidget.Unbind();
            if (mStatusSource != null)
            {
                mStatusSource.Changed -= RefreshTargetIcon;
                mStatusSource.Dispose();
                mStatusSource = null;
            }
        }

        /* 初期化・接続 */

        // 表示するユニットと絵を設定する。味方は右向き、敵は左向きにし、足元を立ち位置に合わせる
        public void Initialize(BattleUnit aUnit, PPUnitVisualDefinition aVisual, BattleSide aSide,
            Func<BattleUnit, Sprite> aIconResolver)
        {
            mBattleUnit = aUnit;
            mIconResolver = aIconResolver;
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

            float spriteHeight = mSpriteHeight;
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

            PlaceStatus(aVisual != null ? aVisual.SpawnOffset.y : 0f, spriteHeight);

            if (mStatusSource != null)
            {
                mStatusSource.Changed -= RefreshTargetIcon;
                mStatusSource.Dispose();
            }
            mStatusSource = new PPBattleUnitStatusSource(aUnit);
            mStatusWidget.Bind(mStatusSource);
            mStatusSource.Changed += RefreshTargetIcon;
            RefreshTargetIcon();
        }

        // 頭上の表示を絵の上に、足元の表示を絵の下に置く。ステータスのキャンバスの縮尺でワールド単位から換算する
        private void PlaceStatus(float aFeetY, float aSpriteHeight)
        {
            mStatusBaseScale = mStatusRoot.localScale;
            float toCanvas = 1f / Mathf.Max(0.0001f, mStatusBaseScale.y);
            mTopBlock.anchoredPosition = new Vector2(0f, (aFeetY + aSpriteHeight + mStatusMargin) * toCanvas);
            mBottomBlock.anchoredPosition = new Vector2(0f, (aFeetY - mStatusMargin) * toCanvas);
        }

        /* 操作 */

        // 選ばれていない表示(暗い色)にするかを切り替える
        public void SetDimmed(bool aDimmed) => mIsDimmed = aDimmed;

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

        // 撃破の動き(沈みながら消える)を再生し、ステータスを隠す
        public void PlayDefeat()
        {
            mDefeatElapsed = 0f;
            mTopBlock.gameObject.SetActive(false);
            mBottomBlock.gameObject.SetActive(false);
        }

        // 狙う相手のアイコンを今の指定に合わせる。指定がなければ隠す
        private void RefreshTargetIcon()
        {
            if (mTargetIcon == null) return;
            var target = (mBattleUnit as PPBattleUnit)?.DesignatedTarget;
            var icon = target != null ? mIconResolver?.Invoke(target) : null;
            mTargetIcon.sprite = icon;
            mTargetIcon.gameObject.SetActive(icon != null);
        }

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
