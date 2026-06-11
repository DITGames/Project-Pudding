/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file TinyUnitStats.cs
 * @author hqrse
 * @date 2026/06/09
 * @brief 簡易用バトルユニット定義
 * =====================================*/
using UnityEngine;

[CreateAssetMenu(fileName = "TinyUnitStats", menuName = "Scriptable Objects/TinyUnitStats")]
public class TinyUnitStats : ScriptableObject
{
    public string mUniqueName;
    public int mBaseHp;
    public int mBaseAtk;
    public int mBaseDef;
}
