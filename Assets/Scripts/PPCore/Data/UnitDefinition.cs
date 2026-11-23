/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file UnitDefinition.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief ユニットのマスターデータ
 * =====================================*/

using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // ユニットのマスターデータ（ScriptableObject）。基礎ステータスの定義を持つ
    // CreateRuntimeUnit でランタイムの BattleUnit を生成する
    [CreateAssetMenu(menuName = "Project-Pudding/Definition/UnitDefinition", fileName = "NewUnit")]
    public class UnitDefinition : ScriptableObject
    {
        /* インスペクタ設定 */

        [Header("ユニット")]
        [Label("ユニットID")]
        [SerializeField] protected string mUnitId;
        [Label("表示名")]
        [SerializeField] protected string mDisplayName;

        [Header("詳細")]
        [Label("ステータス")]
        [SerializeField] protected StatBlock mBaseStatBlock;

        /* プロパティ */

        public string UnitId => mUnitId;
        public string DisplayName => mDisplayName;
        public StatBlock BaseStatBlock => mBaseStatBlock;

        /* 生成 */

        // この定義からランタイムのユニットインスタンスを生成する
        public virtual BattleUnit CreateRuntimeUnit()
        {
            return new BattleUnit(mUnitId, mDisplayName, CreateParameterSet())
            {
                SourceDefinition = this,
            };
        }

        // 基礎ステータスからランタイムのパラメータ一式を組み立てる
        // 追加パラメータを持たせる場合は派生側でオーバーライドする
        protected virtual ParameterSet CreateParameterSet() =>
            new(mBaseStatBlock.MaxHP, mBaseStatBlock.Attack, mBaseStatBlock.Defense, mBaseStatBlock.Speed);
    }
}
