/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPCoinContent.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief コインに入っているユニットと、その見た目・大きさ・重さ
 * =====================================*/

using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // コインに入っているユニット。表と裏にユニットの絵を出し、ユニットの重さに合わせて大きさと質量を変える
    // 中身を決めるのはバトル側の橋渡し(PPCoinUnitBridge)で、このコンポーネントは持つだけ
    public class PPCoinContent : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Label("表の絵")]
        [SerializeField] private SpriteRenderer mFrontFace;
        [Label("裏の絵")]
        [SerializeField] private SpriteRenderer mBackFace;
        // 大きさが 1 倍のときの絵の大きさ(長い辺)。コインの面に収まるようにする
        [Label("絵の大きさ")]
        [Min(0.01f)]
        [SerializeField] private float mFaceSize = 1.1f;

        /* 状態 */

        private Rigidbody mBody;
        // 中身が無いときの大きさと質量。中身を外したときにこれへ戻す
        private Vector3 mBaseScale;
        private float mBaseMass;
        private bool mIsInitialized;

        /* プロパティ */

        // 入っているユニット。null なら中身の無いコイン
        public PPUnitDefinition Unit { get; private set; }
        public int Level { get; private set; }
        public bool HasUnit => Unit != null;

        /* Unity イベント */

        private void Awake() => Initialize();

        /* 操作 */

        // ユニットを入れ、絵と大きさ(基準の何倍か)・質量を当てる
        public void Set(PPUnitDefinition aUnit, int aLevel, Sprite aIcon, float aScale, float aMass)
        {
            Initialize();
            Unit = aUnit;
            Level = aLevel;
            transform.localScale = mBaseScale * aScale;
            if (mBody != null) mBody.mass = aMass;
            SetFace(mFrontFace, aIcon);
            SetFace(mBackFace, aIcon);
        }

        // 中身を外し、絵を消して大きさと質量を元に戻す
        public void Clear()
        {
            Initialize();
            Unit = null;
            Level = 0;
            transform.localScale = mBaseScale;
            if (mBody != null) mBody.mass = mBaseMass;
            SetFace(mFrontFace, null);
            SetFace(mBackFace, null);
        }

        /* ヘルパー */

        // 置き場が非表示のまま作るため Awake より先に呼ばれることがある。最初の 1 回だけ元の値を覚える
        private void Initialize()
        {
            if (mIsInitialized) return;
            mIsInitialized = true;
            mBody = GetComponent<Rigidbody>();
            mBaseScale = transform.localScale;
            mBaseMass = mBody != null ? mBody.mass : 1f;
        }

        // 絵を差し替え、長い辺が絵の大きさになるよう拡大率を合わせる。絵が無ければ隠す
        private void SetFace(SpriteRenderer aFace, Sprite aIcon)
        {
            if (aFace == null) return;
            aFace.sprite = aIcon;
            aFace.enabled = aIcon != null;
            if (aIcon == null) return;
            var size = aIcon.bounds.size;
            float longest = Mathf.Max(0.0001f, Mathf.Max(size.x, size.y));
            aFace.transform.localScale = Vector3.one * (mFaceSize / longest);
        }
    }
}
