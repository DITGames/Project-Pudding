/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleDummyUnitSpawner.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief 描画順の確認用に、ダミーのユニットを道の上へ並べる
 * =====================================*/

using AttributeUtility;
using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // 描画順の確認用。開始時にダミーのユニットを道の上へ重ねて並べる。バトルには加わらず、行動もしない
    public class PPBattleDummyUnitSpawner : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Label("ユニット表示の対応付け")]
        [SerializeField] private PPStageUnitViewBinder mBinder;
        [Label("バトルの道")]
        [SerializeField] private PPBattleField mField;
        // この中から順に選んで並べる
        [Label("並べるユニット", true)]
        [SerializeField] private PPUnitDefinition[] mUnits = new PPUnitDefinition[0];
        [Label("味方の数")]
        [Min(0)]
        [SerializeField] private int mAllyCount = 20;
        [Label("敵の数")]
        [Min(0)]
        [SerializeField] private int mEnemyCount = 20;
        // 味方は道の左端から、敵は右端から、道の長さに対するこの割合の範囲にばらして並べる
        [Label("並べる範囲(道の割合)")]
        [Range(0f, 1f)]
        [SerializeField] private float mSpreadRatio = 0.35f;
        // 同じ値なら毎回同じ並びになる
        [Label("乱数の種")]
        [SerializeField] private int mSeed = 1;

        /* Unity イベント */

        private void Start()
        {
            if (mBinder == null || mField == null || mUnits.Length == 0)
            {
                CustomConsoleLog.Warning("Battle", "ダミーのユニットを並べる設定が足りないため並べません。対応付け・道・並べるユニットを割り当ててください", this);
                return;
            }
            var random = new System.Random(mSeed);
            Spawn(random, mAllyCount, BattleSide.Ally, 0f, mSpreadRatio);
            Spawn(random, mEnemyCount, BattleSide.Enemy, 1f - mSpreadRatio, 1f);
        }

        /* 操作 */

        // 道の aFrom〜aTo の割合の範囲へ、ユニット定義を順に選んで aCount 体並べる
        private void Spawn(System.Random aRandom, int aCount, BattleSide aSide, float aFrom, float aTo)
        {
            for (int i = 0; i < aCount; i++)
            {
                var definition = mUnits[i % mUnits.Length];
                if (definition == null) continue;
                var unit = definition.CreateRuntimeUnit(1);
                float ratio = Mathf.Lerp(aFrom, aTo, (float)aRandom.NextDouble());
                mBinder.SpawnView(unit, aSide, mField.GetPoint(ratio));
            }
        }
    }
}
