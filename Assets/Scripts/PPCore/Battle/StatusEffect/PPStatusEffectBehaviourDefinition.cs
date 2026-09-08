/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStatusEffectBehaviourDefinition.cs
 * @author hqrse
 * @date 2026/09/03
 * @brief StatusEffectBehaviour 1つ分に対応するデータ定義（[SerializeReference] 対応の通常クラス）の抽象基底
 * =====================================*/

using System;
using CommandBattleCore;

namespace PPCore
{
    // StatusEffectBehaviour 1つ分をインスペクタ上で組み立てるためのデータ定義
    // PPEffectDefinition はこの派生型をリストで持ち、状態異常の中身を「振る舞いの組み合わせ」として表現する
    // PPSkillEffectDefinition と同じ形（[PPTypeMenuName] + ツリー選択 + SerializeReference のリスト）
    [Serializable]
    public abstract class PPStatusEffectBehaviourDefinition
    {
        // この定義から StatusEffectBehaviour を組み立て、aEffect に積む
        // aEffect : 組み立て先のエフェクト
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        public abstract void ConfigureBehaviour(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext);

        // リストの要素ラベルに表示する、この振る舞いの内容を要約した文字列を組み立てる
        public abstract string BuildString();
    }
}
