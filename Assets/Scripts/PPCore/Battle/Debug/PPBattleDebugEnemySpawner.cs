/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleDebugEnemySpawner.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief 戦闘の確認用に、敵拠点側から敵を一定間隔で出す
 * =====================================*/

using AttributeUtility;
using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // 戦闘の確認用。バトルが始まったら、敵拠点の近くから敵を一定間隔で道へ出す。本番の敵はウェーブ定義から出す
    public class PPBattleDebugEnemySpawner : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Label("バトルのランナー")]
        [SerializeField] private PPBattleRunner mRunner;
        // この中から順に選んで出す
        [Label("出す敵", true)]
        [SerializeField] private PPUnitDefinition[] mUnits = new PPUnitDefinition[0];
        [Label("敵のレベル")]
        [Min(1)]
        [SerializeField] private int mLevel = 1;
        // バトル専用の時計の秒で数えるため、メニューを開いている間は止まる
        [Label("出す間隔(秒)")]
        [Min(0.1f)]
        [SerializeField] private float mInterval = 3f;
        // 道の上で生きている敵がこの数に達している間は出さない
        [Label("同時に出す上限")]
        [Min(1)]
        [SerializeField] private int mMaxAlive = 10;
        // 敵拠点(道の右端)からの距離。味方が止まる線より拠点側に収める
        [Label("出す位置")]
        [Min(0f)]
        [SerializeField] private float mSpawnDistance = 2f;

        /* 状態 */

        private float mNextTime;
        private int mSpawnedCount;
        private bool mIsWarned;

        /* Unity イベント */

        private void Update()
        {
            var manager = mRunner != null ? mRunner.BattleManager : null;
            var field = manager?.Context?.Field;
            if (field == null) return;
            if (mUnits.Length == 0)
            {
                if (!mIsWarned) CustomConsoleLog.Warning("Battle", "出す敵が未設定のため、確認用の敵を出しません。PPBattleDebugEnemySpawner の「出す敵」を割り当ててください", this);
                mIsWarned = true;
                return;
            }

            float now = manager.Context.Clock.Now;
            if (now < mNextTime || CountAliveEnemies(field) >= mMaxAlive) return;

            mNextTime = now + mInterval;
            var definition = mUnits[mSpawnedCount % mUnits.Length];
            mSpawnedCount++;
            if (definition == null) return;
            // 拠点の前で止まっている味方より左に出ないようにする
            float position = Mathf.Max(field.Length - mSpawnDistance, field.GetStopLine(BattleSide.Ally));
            manager.SpawnFieldUnit(definition.CreateRuntimeUnit(mLevel), BattleSide.Enemy, position);
        }

        /* ヘルパー */

        private static int CountAliveEnemies(PPBattleFieldState aField)
        {
            int count = 0;
            foreach (var fieldUnit in aField.Units)
            {
                if (fieldUnit.Side == BattleSide.Enemy && fieldUnit.Unit.IsAlive) count++;
            }
            return count;
        }
    }
}
