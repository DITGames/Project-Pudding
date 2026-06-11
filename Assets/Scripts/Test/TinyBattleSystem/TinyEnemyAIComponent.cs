using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TinyEnemyAIComponent : MonoBehaviour
{
    [SerializeField] private TinyBattleManager mBattleManager;
    public List<TinyBattleUnit> mUnits;

    public float mAttackInterval = 3.0f;

    private Coroutine mAttackRoutine;

    public void Start()
    {
        if (mBattleManager)
        {
            mBattleManager.OnAllUnitsDefeated += HandleAllUnitDefeated;
        }
        
        mAttackRoutine = StartCoroutine(StartAttack());
    }

    public void OnDestroy()
    {
        if (mBattleManager)
        {
            mBattleManager.OnAllUnitsDefeated -= HandleAllUnitDefeated;
        }
    }

    IEnumerator StartAttack()
    {
        while (true)
        {
            yield return new WaitForSeconds(mAttackInterval);
        
            if(!mBattleManager) yield break;

            foreach (var unit in mUnits)
            {
                if (unit != null && !unit.IsDead)
                {
                    mBattleManager.AttackToFirst(true, unit.mAtk);
                }
            }   
        }
    }

    void HandleAllUnitDefeated(bool aIsPlayerUnits)
    {
        if (mAttackRoutine != null)
        {
            StopCoroutine(mAttackRoutine);
            mAttackRoutine = null;
        }

        if (aIsPlayerUnits)
        {
            Debug.Log("Enemy Win!!");
        }
        else
        {
            Debug.Log("Player Win!!");
        }
    }
}
