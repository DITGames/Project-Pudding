/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitVisualDefinition.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニットビジュアル定義
 * =====================================*/

using AttributeUtility;
using UnityEngine;

// ユニットの見た目に関する定義（ScriptableObject）。性能定義とはユニット ID で対応付ける
// 解決は PPCore.PPUnitVisualCatalog が行う
[CreateAssetMenu(fileName = "PPUnitVisualDefinition", menuName = "Project-Pudding/Definition/PPUnitVisualDefinition")]
public class PPUnitVisualDefinition : ScriptableObject
{
    // 対応するユニット ID。カタログでの解決キー
    [Label("ユニットID")]
    public string UnitId;
    [Label("アイコン")]
    public Sprite UnitIcon;
    // 立ち絵
    [Label("立ち絵")]
    public Sprite UnitPortrait;
    // 戦場に配置する見た目のプレハブ
    [Label("プレハブ")]
    public GameObject ViewPrefab;
    [Label("アニメーター")]
    public RuntimeAnimatorController Animator;
    // 配置位置の補正
    [Label("オフセット")]
    public Vector3 SpawnOffset;
}
