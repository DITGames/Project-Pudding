/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPDurationConditionDefinition.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief IDurationCondition 1つ分に対応するデータ定義（[SerializeReference] 対応の通常クラス）の抽象基底
 * =====================================*/

using System;
using CommandBattleCore;

namespace PPCore
{
    // StatusEffect の持続条件（IDurationCondition）をインスペクタ上で選ぶためのデータ定義
    // 「何ターンで切れるのか」「永続なのか」を、ターン数の数値ではなく条件の種類として選択させる
    // PPStatusEffectBehaviourDefinition と同じ形（[PPTypeMenuName] + ツリー選択 + SerializeReference）
    [Serializable]
    public abstract class PPDurationConditionDefinition
    {
        // この定義から持続条件のランタイムインスタンスを生成する
        // 付与のたびに呼ばれるため、必ず新しいインスタンスを返すこと
        // （TurnDurationCondition は残りターン数という状態を持つため使い回してはいけない）
        // return : 生成された持続条件
        public abstract IDurationCondition CreateDurationCondition();

        // フィールドラベルに表示する、この持続条件の内容を要約した文字列を組み立てる
        public abstract string BuildString();
    }
}
