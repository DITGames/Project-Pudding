/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitVisualDefinition.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニットのビジュアル定義
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

[CreateAssetMenu(fileName = "PPUnitVisualDefinition", menuName = "Project-Pudding/PPUnitVisualDefinition")]
public class PPUnitVisualDefinition : ScriptableObject
{
    [Label("ユニットId")]
    public string UnitId;
    [Label("アイコン")]
    public Sprite UnitIcon;
    [Label("立ち絵")]
    public Sprite UnitPortrait;
    [Label("プレハブ")] 
    public GameObject ViewPrefab;
    [Label("アニメーター")]
    public RuntimeAnimatorController Animator;
    [Label("オフセット")]
    public Vector3 SpawnOffset;
}