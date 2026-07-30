/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitVisualDefinition.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニットビジュアル定義
 * =====================================*/
using CommandBattleCore;
using UnityEngine;

// ユニットの見た目に関する定義（ScriptableObject）
// 性能定義（PPCore.PPUnitDefinition）とは別アセットに分けてあり、
// 両者はユニット ID で対応付ける。バトルロジックが見た目のアセットを直接参照せずに済む
// 解決は PPCore.PPUnitVisualCatalog が行う
// このクラスだけ namespace の外に置かれており、PPCore の他の型と同じ扱いになっていない
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
