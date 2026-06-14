/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file TinyBattleManager.cs
 * @author hqrse
 * @date 2026/06/09
 * @brief 簡易バトルマネージャー
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;

public class TinyBattleManager : MonoBehaviour
{
    [SerializeField] private List<TinyBattleUnit> mAllies;
    [SerializeField] private List<TinyBattleUnit> mEnemies;

    public event Action<bool> OnAllUnitsDefeated;
    
    void Start()
    {
        foreach (TinyBattleUnit battleUnit in mAllies)
        {
            battleUnit.InitializeUnitStats();
        }

        foreach (TinyBattleUnit battleUnit in mEnemies)
        {
            battleUnit.InitializeUnitStats();
        }
    }

    public void AttackToTarget(bool aTargetIsPlayer, int aTargetIdx, int aDamage)
    {
        if (!aTargetIsPlayer)
        {
            if (mEnemies.Count > aTargetIdx)
            {
                mEnemies[aTargetIdx].ApplyDamage(aDamage);
                if (mEnemies[aTargetIdx].IsDead)
                {
                    mEnemies.RemoveAt(aTargetIdx);
                }
            }
        }
        else
        {
            if (mAllies.Count > aTargetIdx)
            {
                mAllies[aTargetIdx].ApplyDamage(aDamage);
                if(mAllies[aTargetIdx].IsDead)
                {
                    mAllies.RemoveAt(aTargetIdx);
                }
            }
        }

        if (IsAllUnitsDefeated(aTargetIsPlayer))
        {
            OnAllUnitsDefeated?.Invoke(aTargetIsPlayer);
        }
    }

    public void AttackToFirst(bool aTargetIsPlayer, int aDamage)
    {
        if (!aTargetIsPlayer)
        {
            if(mEnemies.Count > 0)
            {
                mEnemies[0].ApplyDamage(aDamage);
                if (mEnemies[0].IsDead)
                {
                    mEnemies.RemoveAt(0);
                }
            }
        }
        else
        {
            if (mAllies.Count > 0)
            {
                mAllies[0].ApplyDamage(aDamage);
                if (mAllies[0].IsDead)
                {
                    mAllies.RemoveAt(0);
                }
            }
        }
        
        if (IsAllUnitsDefeated(aTargetIsPlayer))
        {
            OnAllUnitsDefeated?.Invoke(aTargetIsPlayer);
        }
    }

    private bool IsAllUnitsDefeated(bool aTargetIsPlayer)
    {
        if (!aTargetIsPlayer)
        {
            return mEnemies.Count <= 0;
        }
        else
        {
            return mAllies.Count <= 0;
        }
    }
}