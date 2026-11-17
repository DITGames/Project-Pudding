/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPChargeItemSpawner.cs
 * @author hqrse
 * @date 2026/11/17
 * @brief チャージ用アイテムの出現
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // プッシャーへチャージ用アイテムを一定間隔で出現させる役
    // 中身（誰に入るか）は IPPChargeItemTicketSource が決める。口が繋がるまでは出現させない
    public class PPChargeItemSpawner : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Label("アイテムのプレハブ")]
        [SerializeField] private PPPusherChargeItem mItemPrefab;
        [Label("出現の間隔(秒)")]
        [Min(0.5f)]
        [SerializeField] private float mInterval = 12f;
        // 台の上に同時に置いておける数。これ以上は、落ちて減るまで出現させない
        [Label("同時に置ける数")]
        [Min(1)]
        [SerializeField] private int mMaxCount = 3;
        // アイテムを出現させる範囲。この Transform の位置と大きさ（スケール）を箱として使う
        [Label("出現させる範囲")]
        [SerializeField] private Transform mSpawnArea;

        /* 状態 */

        // 出現させるアイテムの中身を決める口。バトル側のブリッジが差し込む
        public IPPChargeItemTicketSource TicketSource { get; set; }
        // 台の上に残っているアイテム。落ちて消えたものは数えるときに取り除く
        private readonly List<PPPusherChargeItem> mItems = new();
        // 次の出現までの残り秒数
        private float mTimer;

        /* Unity イベント */

        // 最初の出現までの時間を設定する
        private void Start() => mTimer = mInterval;

        // 間隔ごとにアイテムを出現させる。timeScale に従う時間で数えるため、コマンド入力中は止まる
        private void Update()
        {
            if (TicketSource == null) return;

            mTimer -= Time.deltaTime;
            if (mTimer > 0f) return;

            mTimer = mInterval;
            TrySpawn();
        }

        /* 操作 */

        // アイテムを 1 個出現させる。置ける数に達している、または中身を決められない場合は何もしない
        public bool TrySpawn()
        {
            if (mItemPrefab == null || mSpawnArea == null || TicketSource == null) return false;

            mItems.RemoveAll(item => item == null);
            if (mItems.Count >= mMaxCount) return false;

            var ticket = TicketSource.IssueTicket();
            if (ticket == null) return false;

            var half = mSpawnArea.lossyScale * 0.5f;
            var local = new Vector3(
                Random.Range(-half.x, half.x),
                Random.Range(-half.y, half.y),
                Random.Range(-half.z, half.z));
            var rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var item = Instantiate(mItemPrefab, mSpawnArea.position + mSpawnArea.rotation * local, rotation, transform);
            item.Setup(ticket);
            mItems.Add(item);
            return true;
        }
    }
}
