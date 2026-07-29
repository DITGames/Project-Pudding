/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file SlotListComponent.cs
 * @author hqrse
 * @date 2026/07/04
 * @brief 
 * =====================================*/
using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    [Serializable]
    public class SlotInfo
    {
        [Label("スロット")] 
        [SerializeField] public Transform mTransform;
        [Label("アタッチオブジェクトリスト", true)]
        [SerializeField] public List<GameObject> mAttachedObjects;

        public void AddAttachedObject(GameObject aObject)
        {
            mAttachedObjects.Add(aObject);
        }

        public void RemoveAttachedObject(GameObject aObject)
        {
            mAttachedObjects.Remove(aObject);
        }
    }
    
    public class SlotListComponent : MonoBehaviour
    {
        [Label("スロットリスト", true)]
        [SerializeField] private List<SlotInfo> mSlots = new();
        
        [Header("デフォルト設定")]
        [Label("オフセット")]
        [SerializeField] private Vector3 mOffset;
        [Label("スケール")]
        [SerializeField] private Vector3 mScale = new Vector3(1,1,1);
        [Label("回転")]
        [SerializeField] private Quaternion mRotation;

        public bool HasSlot(int aIndex) => mSlots.Count > aIndex;
        
        public SlotInfo GetSlot(int aIndex)
        {
            return mSlots.Count > aIndex ? mSlots[aIndex] : null;
        }

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

        public SlotInfo AddSlot()
        {
            return AddSlot(mOffset, mScale, mRotation);
        }
    }
}