/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAIMemory.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief ユニット1体分のAIの記憶
 * =====================================*/

using System.Collections.Generic;

namespace PPCore
{
    // ユニット 1 体分の AI の記憶
    //
    // 判断ツリーはステートレスで、毎ティック根から評価し直す
    // 「一度だけ」「しばらく間を空ける」といった時間をまたぐ判断はツリー側では表現できないため、
    // 行動が確定したノードとその時点のターン数をここへ記録し、次の評価で参照する
    //
    // 書き込むのは PPUnitAIStrategist だけで、ノードは読むだけにする
    // ノードの評価がバトルの状態を変えない、という設計上の約束を守るため
    //
    // 経過は BattleContext.TurnCount の差分で数える
    // 残ティック数を減らしていく方式にしないのは、1 ティックに複数回思考する設定では二重に消化されるため
    // 記憶はバトルをまたがない（ストラテジストがバトル開始時に作り直される）
    public sealed class PPUnitAIMemory
    {
        // ノードごとに、最後に行動が確定したときのターン数
        // クールダウン・一度きり・ラッチはいずれもこの 1 つの記録から判定できる
        private readonly Dictionary<string, int> mFiredTurns = new();

        // ノードで行動が確定したことを記録する
        // aNodeId : 対象ノードの ID
        // aTurnCount : 確定した時点のターン数
        public void MarkFired(string aNodeId, int aTurnCount)
        {
            if (string.IsNullOrEmpty(aNodeId)) return;

            mFiredTurns[aNodeId] = aTurnCount;
        }

        // そのノードで一度でも行動が確定したことがあるか
        // aNodeId : 対象ノードの ID
        // return : 確定したことがあれば true
        public bool HasFired(string aNodeId)
            => !string.IsNullOrEmpty(aNodeId) && mFiredTurns.ContainsKey(aNodeId);

        // そのノードで最後に確定したターン数を引く
        // aNodeId : 対象ノードの ID
        // aOutTurnCount : 最後に確定したターン数
        // return : 記録があれば true
        public bool TryGetFiredTurn(string aNodeId, out int aOutTurnCount)
        {
            aOutTurnCount = 0;
            return !string.IsNullOrEmpty(aNodeId) && mFiredTurns.TryGetValue(aNodeId, out aOutTurnCount);
        }

        // 記憶を全て捨てる。バトルをまたいで持ち越さないために呼ぶ
        public void Clear() => mFiredTurns.Clear();
    }
}
