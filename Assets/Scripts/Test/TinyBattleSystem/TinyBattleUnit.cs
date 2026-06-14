/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file TinyBattleUnit.cs
 * @author hqrse
 * @date 2026/06/09
 * @brief 簡易バトルユニット定義
 * =====================================*/
using UnityEngine;

public class TinyBattleUnit : MonoBehaviour
{
    [SerializeField] private TinyUnitStats mUnitStats;

    public int mMaxHp;
    public int mCurrentHp;
    public int mAtk;
    public int mDef;

    public void InitializeUnitStats()
    {
        if (mUnitStats != null)
        {
            mMaxHp = mUnitStats.mBaseHp;
            mCurrentHp = mMaxHp;
            mAtk = mUnitStats.mBaseAtk;
            mDef = mUnitStats.mBaseDef;
        }
    }

    public void ApplyDamage(int aDamage)
    {
        mCurrentHp = Mathf.Max(0, mCurrentHp - aDamage);
        Debug.Log($"{mUnitStats.mUniqueName}: {mCurrentHp}/{mMaxHp}");
    }
    
    public bool IsDead => mCurrentHp <= 0;
}
