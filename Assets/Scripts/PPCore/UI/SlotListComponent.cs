/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file SlotListComponent.cs
 * @author hqrse
 * @date 2026/07/04
 * @brief オブジェクトの取り付け位置(スロット)を管理する汎用コンポーネント
 * =====================================*/
using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// スロット 1 つ分の情報。取り付け位置となる Transform と、
    /// そこに取り付けられたオブジェクトの一覧を持つ。
    /// </summary>
    [Serializable]
    public class SlotInfo
    {
        /// <summary>取り付け位置となる Transform。</summary>
        [Label("スロット")]
        [SerializeField] public Transform mTransform;
        /// <summary>このスロットに取り付けられているオブジェクト。</summary>
        [Label("アタッチオブジェクトリスト", true)]
        [SerializeField] public List<GameObject> mAttachedObjects;

        /// <summary>取り付けオブジェクトを登録する。</summary>
        /// <param name="aObject">登録するオブジェクト。</param>
        public void AddAttachedObject(GameObject aObject)
        {
            mAttachedObjects.Add(aObject);
        }

        /// <summary>取り付けオブジェクトの登録を外す。</summary>
        /// <param name="aObject">外すオブジェクト。</param>
        public void RemoveAttachedObject(GameObject aObject)
        {
            mAttachedObjects.Remove(aObject);
        }
    }

    /// <summary>
    /// 複数のスロット（オブジェクトの取り付け位置）をまとめて管理するコンポーネント。
    /// エフェクトやアイコンを決まった位置へ動的に配置する用途を想定している。
    /// </summary>
    /// <remarks>
    /// 現状どこからも参照されていない。
    /// また <see cref="AddSlot(Vector3, Vector3, Quaternion)"/> は、
    /// 素の GameObject が持つ Transform を RectTransform へキャストしており、
    /// 実行すると InvalidCastException になる。使う際は先に修正が必要。
    /// </remarks>
    public class SlotListComponent : MonoBehaviour
    {
        /// <summary>管理しているスロット。</summary>
        [Label("スロットリスト", true)]
        [SerializeField] private List<SlotInfo> mSlots = new();

        /// <summary>スロット新規作成時の既定オフセット。</summary>
        [Header("デフォルト設定")]
        [Label("オフセット")]
        [SerializeField] private Vector3 mOffset;
        /// <summary>スロット新規作成時の既定スケール。</summary>
        [Label("スケール")]
        [SerializeField] private Vector3 mScale = new Vector3(1,1,1);
        /// <summary>スロット新規作成時の既定回転。</summary>
        [Label("回転")]
        [SerializeField] private Quaternion mRotation;

        /// <summary>指定インデックスのスロットが存在するか。</summary>
        /// <param name="aIndex">スロットのインデックス。</param>
        public bool HasSlot(int aIndex) => mSlots.Count > aIndex;

        /// <summary>
        /// 指定インデックスのスロットを取得する。
        /// </summary>
        /// <param name="aIndex">スロットのインデックス。</param>
        /// <returns>該当スロット。範囲外なら null。</returns>
        public SlotInfo GetSlot(int aIndex)
        {
            return mSlots.Count > aIndex ? mSlots[aIndex] : null;
        }

        /// <summary>
        /// 指定した配置でスロットを新規作成し、自身の子として追加する。
        /// </summary>
        /// <param name="aOffset">ローカル位置。</param>
        /// <param name="aScale">ローカルスケール。</param>
        /// <param name="aRotation">ローカル回転。</param>
        /// <returns>作成されたスロット。</returns>
        public SlotInfo AddSlot(Vector3 aOffset, Vector3 aScale, Quaternion aRotation)
        {
            var count = mSlots.Count + 1;
            var slotRts = new GameObject("Slot" + count);
            slotRts.transform.SetParent(transform);
            var rect = (RectTransform)slotRts.transform;
            rect.localPosition = aOffset;
            rect.localScale = aScale;
            rect.localRotation = aRotation;
            var slot = new SlotInfo();
            slot.mTransform = slotRts.transform;
            mSlots.Add(slot);
            return slot;
        }

        /// <summary>
        /// インスペクタで設定した既定の配置でスロットを新規作成する。
        /// </summary>
        /// <returns>作成されたスロット。</returns>
        public SlotInfo AddSlot()
        {
            return AddSlot(mOffset, mScale, mRotation);
        }
    }
}
