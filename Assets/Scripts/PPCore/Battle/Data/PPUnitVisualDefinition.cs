/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitVisualDefinition.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief PPユニットビジュアル定義
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

[CreateAssetMenu(fileName = "PPUnitVisualDefinition", menuName = "Project-Pudding/Definition/PPUnitVisualDefinition")]
public class PPUnitVisualDefinition : ScriptableObject
{
    [Label("ユニットID")]
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