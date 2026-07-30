/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitVisualDefinition.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニットビジュアル定義
 * =====================================*/
using CommandBattleCore;
using UnityEngine;

/// <summary>
/// ユニットの見た目に関する定義（ScriptableObject）。
/// <para>
/// 性能定義（<see cref="PPCore.PPUnitDefinition"/>）とは別アセットに分けてあり、
/// 両者はユニット ID で対応付ける。バトルロジックが見た目のアセットを直接参照せずに済む。
/// 解決は <see cref="PPCore.PPUnitVisualCatalog"/> が行う。
/// </para>
/// </summary>
/// <remarks>
/// このクラスだけ namespace の外に置かれており、PPCore の他の型と同じ扱いになっていない。
/// </remarks>
[CreateAssetMenu(fileName = "PPUnitVisualDefinition", menuName = "Project-Pudding/Definition/PPUnitVisualDefinition")]
public class PPUnitVisualDefinition : ScriptableObject
{
    /// <summary>対応するユニット ID。カタログでの解決キー。</summary>
    [Label("ユニットID")]
    public string UnitId;
    /// <summary>UI に出すアイコン。</summary>
    [Label("アイコン")]
    public Sprite UnitIcon;
    /// <summary>立ち絵。</summary>
    [Label("立ち絵")]
    public Sprite UnitPortrait;
    /// <summary>戦場に配置する見た目のプレハブ。</summary>
    [Label("プレハブ")]
    public GameObject ViewPrefab;
    /// <summary>アニメーターコントローラー。</summary>
    [Label("アニメーター")]
    public RuntimeAnimatorController Animator;
    /// <summary>配置位置の補正。</summary>
    [Label("オフセット")]
    public Vector3 SpawnOffset;
}
