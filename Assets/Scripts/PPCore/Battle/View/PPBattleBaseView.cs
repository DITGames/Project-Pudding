/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleBaseView.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief バトル画面の拠点の真上に出す HP バー
 * =====================================*/

using AttributeUtility;
using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // 拠点 1 つ分の HP バー。道の端(拠点の位置)の真上に置き、拠点がダメージを受けるたびに縮める
    public class PPBattleBaseView : MonoBehaviour
    {
        /* 定数 */

        // ユニットの描画順(優先度 × 1000 + 出た順)より手前に出す
        private const int SortingOrder = 32000;

        /* static 変数 */

        private static Sprite sWhiteSprite;

        /* インスペクタ設定 */

        [Label("バトルの道")]
        [SerializeField] private PPBattleField mField;
        [Label("陣営")]
        [SerializeField] private BattleSide mSide = BattleSide.Ally;
        // 道の地面からバーの中心までの高さ
        [Label("バーの高さ")]
        [Min(0f)]
        [SerializeField] private float mHeight = 6f;
        [Label("バーの幅")]
        [Min(0.1f)]
        [SerializeField] private float mWidth = 5f;
        [Label("バーの太さ")]
        [Min(0.01f)]
        [SerializeField] private float mThickness = 0.5f;
        [Label("背景の色")]
        [SerializeField] private Color mBackgroundColor = new(0f, 0f, 0f, 0.6f);
        [Label("味方の色")]
        [SerializeField] private Color mAllyColor = new(0.3f, 0.8f, 1f);
        [Label("敵の色")]
        [SerializeField] private Color mEnemyColor = new(1f, 0.4f, 0.35f);

        /* 状態 */

        private Transform mFill;
        private PPFieldBase mBase;

        /* 初期化・接続 */

        // 道に置かれた自陣営の拠点を見つけ、HP の割合が変わるたびにバーを縮める
        public void Bind(BattleManager aManager)
        {
            mBase = aManager?.Context?.Field?.GetBase(mSide);
            if (mBase == null || mField == null)
            {
                CustomConsoleLog.Warning("Battle", $"{mSide} の拠点{(mField == null ? "の道が未設定" : "が道に無い")}ため、HP バーを出しません", this);
                return;
            }

            BuildBar();
            SetRatio(mBase.Unit.Parameters.Hp.Ratio);
            aManager.OnBaseHpRatioChanged += (fieldBase, _, after) =>
            {
                if (fieldBase == mBase) SetRatio(after);
            };
        }

        /* 表示 */

        // 拠点の真上に、背景と中身の 2 本のバーを作る
        private void BuildBar()
        {
            float x = mSide == BattleSide.Ally ? mField.LeftX : mField.RightX;
            transform.position = new Vector3(x, mField.GroundY + mHeight, mField.transform.position.z);

            CreateBar("Background", mBackgroundColor, SortingOrder);
            mFill = CreateBar("Fill", mSide == BattleSide.Ally ? mAllyColor : mEnemyColor, SortingOrder + 1).transform;
        }

        // 中身のバーを左端を基準に縮める
        private void SetRatio(float aRatio)
        {
            if (mFill == null) return;
            var scale = mFill.localScale;
            scale.x = mWidth * Mathf.Clamp01(aRatio);
            mFill.localScale = scale;
        }

        /* ヘルパー */

        // 左端を基準にした横長のバーを作る。絵は 1 単位四方の白い四角を色と拡大率で使い回す
        private SpriteRenderer CreateBar(string aName, Color aColor, int aOrder)
        {
            var go = new GameObject(aName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(-mWidth * 0.5f, 0f, 0f);
            go.transform.localScale = new Vector3(mWidth, mThickness, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = GetWhiteSprite();
            renderer.color = aColor;
            renderer.sortingOrder = aOrder;
            return renderer;
        }

        private static Sprite GetWhiteSprite()
        {
            if (sWhiteSprite != null) return sWhiteSprite;
            var texture = Texture2D.whiteTexture;
            // 左端の中央を基準にして、横へ縮めても左端が動かないようにする
            sWhiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0f, 0.5f), texture.width);
            return sWhiteSprite;
        }
    }
}
