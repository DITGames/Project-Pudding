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

        public bool HasSlot(int aIndex) => mSlots.Count > aIndex;
        
        public SlotInfo GetSlot(int aIndex)
        {
            return mSlots.Count > aIndex ? mSlots[aIndex] : null;
        }
    }
}