/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleField.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief バトル画面の 1 本道
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // バトル画面の 1 本道。この Transform の位置を道の中央の足元とし、左端に味方拠点、右端に敵拠点を置く
    // 道の長さはバトルの始まりにバトル定義から変わることがあり、そのときは地面と拠点の目印も合わせて動かす
    public class PPBattleField : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Label("道の長さ")]
        [Min(1f)]
        [SerializeField] private float mLength = 60f;
        // 道の両端より外側にも、この幅だけスクロールできるようにする。拠点の絵が端で切れないようにするため
        [Label("左右の余白")]
        [Min(0f)]
        [SerializeField] private float mMargin = 8f;
        // ユニットは相手の拠点の中心からこの距離の手前で止まる。拠点の絵とユニットが重ならないようにするため
        [Label("拠点の手前で止まる距離")]
        [Min(0f)]
        [SerializeField] private float mBaseFrontDistance = 4f;
        // ウェーブの敵を出す位置。敵拠点の中心(道の右端)からの距離
        [Label("敵が出る位置")]
        [Min(0f)]
        [SerializeField] private float mEnemySpawnDistance = 2f;
        // HP の区切りでも攻撃でも、押し戻す距離と速さはここで決めた共通の値を使う
        [Header("ノックバック")]
        [Label("押し戻す距離")]
        [Min(0f)]
        [SerializeField] private float mKnockbackDistance = 3f;
        // 1 秒に押し戻す距離
        [Label("押し戻す速さ")]
        [Min(0.01f)]
        [SerializeField] private float mKnockbackSpeed = 12f;

        // 道の長さが変わったときに合わせる見た目。未設定のものは動かさない
        [Header("道の見た目")]
        // 横幅を「道の長さ + 今の余り」に合わせる
        [Label("地面")]
        [SerializeField] private Transform mGround;
        // 道の両端へ置き直す
        [Label("味方拠点の目印")]
        [SerializeField] private Transform mAllyBaseMarker;
        [Label("敵拠点の目印")]
        [SerializeField] private Transform mEnemyBaseMarker;

        /* 状態 */

        // 地面の横幅のうち、道の長さを超える分。シーンに置いた時点の値を覚えておく
        private float mGroundExtraWidth;

        /* プロパティ */

        public float Length => mLength;
        // 道の左端(味方拠点)のワールド座標の x
        public float LeftX => transform.position.x - mLength * 0.5f;
        // 道の右端(敵拠点)のワールド座標の x
        public float RightX => transform.position.x + mLength * 0.5f;
        // ユニットが立つ地面の高さ(ワールド座標の y)
        public float GroundY => transform.position.y;
        public float Margin => mMargin;
        public float BaseFrontDistance => mBaseFrontDistance;
        public float EnemySpawnDistance => mEnemySpawnDistance;
        public float KnockbackDistance => mKnockbackDistance;
        public float KnockbackSpeed => mKnockbackSpeed;

        /* イベント */

        // 道の長さが変わったとき。カメラが開始位置を当て直す
        public event Action OnLengthChanged;

        /* Unity イベント */

        private void Awake()
        {
            if (mGround != null) mGroundExtraWidth = mGround.localScale.x - mLength;
        }

#if UNITY_EDITOR
        // 道と、余白を含めたスクロールできる範囲をシーンビューに描く
        private void OnDrawGizmos()
        {
            float z = transform.position.z;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector3(LeftX, GroundY, z), new Vector3(RightX, GroundY, z));
            Gizmos.DrawLine(new Vector3(LeftX, GroundY, z), new Vector3(LeftX, GroundY + 3f, z));
            Gizmos.DrawLine(new Vector3(RightX, GroundY, z), new Vector3(RightX, GroundY + 3f, z));
            // ユニットが止まる線
            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(LeftX + mBaseFrontDistance, GroundY, z), new Vector3(LeftX + mBaseFrontDistance, GroundY + 2f, z));
            Gizmos.DrawLine(new Vector3(RightX - mBaseFrontDistance, GroundY, z), new Vector3(RightX - mBaseFrontDistance, GroundY + 2f, z));
            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            Gizmos.DrawLine(new Vector3(LeftX - mMargin, GroundY, z), new Vector3(RightX + mMargin, GroundY, z));
        }
#endif

        /* 操作 */

        // 道の長さを変え、地面の幅と拠点の目印の位置を合わせる。道の状態を作る前に呼ぶ
        public void SetLength(float aLength)
        {
            mLength = Mathf.Max(1f, aLength);
            if (mGround != null)
            {
                var scale = mGround.localScale;
                scale.x = mLength + mGroundExtraWidth;
                mGround.localScale = scale;
            }
            PlaceMarker(mAllyBaseMarker, -mLength * 0.5f);
            PlaceMarker(mEnemyBaseMarker, mLength * 0.5f);
            OnLengthChanged?.Invoke();
        }

        /* 問い合わせ */

        // 道の上の位置を返す。左端 0〜右端 1 の割合で指定する
        public Vector3 GetPoint(float aRatio) => new(Mathf.LerpUnclamped(LeftX, RightX, aRatio), GroundY, transform.position.z);

        // 道の上の位置を返す。左端からの距離で指定する
        public Vector3 GetPointAtDistance(float aDistance) => new(LeftX + aDistance, GroundY, transform.position.z);

        /* ヘルパー */

        private static void PlaceMarker(Transform aMarker, float aLocalX)
        {
            if (aMarker == null) return;
            var position = aMarker.localPosition;
            position.x = aLocalX;
            aMarker.localPosition = position;
        }
    }
}
