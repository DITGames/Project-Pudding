/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAINode.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニットAIの判断ツリーを構成するノードの基底
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // ノード 1 つ分の評価結果
    // 「行動が確定したか」と「確定した内容」を 1 つにまとめたもの
    // 待機も行動の一種として確定扱いにするため、確定フラグとコマンドの有無は別で持つ
    public readonly struct PPUnitAINodeResult
    {
        // この枝で行動が確定したか。false なら親は次の枝へ進む
        public bool IsDecided { get; }
        // 実行するコマンド。待機が確定した場合は null
        public BattleCommandBase Command { get; }
        // 記録・ログ用の行動名
        public string ActionName { get; }
        // 確定した行動の対象。対象なしなら null
        public PPBattleUnit Target { get; }
        // この判断を維持するティック数。0 ならその思考限りで、次のティックは自由に選び直す
        public int CommitTicks { get; }

        private PPUnitAINodeResult(bool aIsDecided, BattleCommandBase aCommand, string aActionName,
            PPBattleUnit aTarget, int aCommitTicks)
        {
            IsDecided = aIsDecided;
            Command = aCommand;
            ActionName = aActionName;
            Target = aTarget;
            CommitTicks = aCommitTicks;
        }

        // 行動が決まらなかったことを表す結果。親は次の枝を評価する
        public static readonly PPUnitAINodeResult Failed = new(false, null, null, null, 0);

        // 待機で確定したことを表す結果を作る
        // aActionName : 記録用の行動名
        public static PPUnitAINodeResult Wait(string aActionName) => new(true, null, aActionName, null, 0);

        // コマンド実行で確定したことを表す結果を作る
        // aCommand : 実行するコマンド
        // aActionName : 記録用の行動名
        // aTarget : 行動の対象
        public static PPUnitAINodeResult Execute(BattleCommandBase aCommand, string aActionName, PPBattleUnit aTarget)
            => new(true, aCommand, aActionName, aTarget, 0);

        // 同じ内容のまま、維持するティック数だけを差し替えた結果を作る
        // 行動そのものはアクションが決め、どれだけ維持するかはノードが決めるため分けている
        // aCommitTicks : 維持するティック数
        public PPUnitAINodeResult WithCommit(int aCommitTicks)
            => new(IsDecided, Command, ActionName, Target, aCommitTicks);
    }

    // ツリー評価 1 回分の入力をまとめたもの
    // ノードと条件・アクションが共通で参照する材料を 1 つの箱に入れ、
    // ノードの評価シグネチャを増やさずに済ませる
    // 評価中に辿った枝の位置（Path）もここへ積み、待機コミットの記録と復元に使う
    public sealed class PPUnitAIEvalContext
    {
        // 思考対象のユニット
        public PPBattleUnit Unit { get; }
        // パーティ状況のスナップショット
        public PPPartyAIContext Snapshot { get; }
        // バトルコンテキスト。乱数・ルール・発動可否の検証に使う
        public BattleContext Battle { get; }
        // 評価中の判断ツリー。子ノードを ID から引くのに使う
        public PPUnitAIProfileDefinition Profile { get; }
        // 拡張ルール。差し込まれていない場合は null
        public PPBattleRules Rules => Battle.Rules as PPBattleRules;

        // 評価中に辿っている枝の位置。優先度リストが子の添字を積んでいく
        // 行動が確定した時点の中身が、そのままその行動へ至る道順になる
        public List<int> Path { get; } = new();

        // 前回の待機コミットで記録した道順。コミット中でなければ null
        // 優先度リストはこれを見て「待ちを宣言した枝より下は見ない」を実現する
        public IReadOnlyList<int> CommitPath { get; set; }

        // aUnit : 思考対象のユニット
        // aSnapshot : パーティ状況のスナップショット
        // aBattle : バトルコンテキスト
        // aProfile : 評価中の判断ツリー
        public PPUnitAIEvalContext(PPBattleUnit aUnit, PPPartyAIContext aSnapshot, BattleContext aBattle,
            PPUnitAIProfileDefinition aProfile)
        {
            Unit = aUnit;
            Snapshot = aSnapshot;
            Battle = aBattle;
            Profile = aProfile;
        }

        // ID からノードを引く
        // aNodeId : 引くノードの ID
        // return : 該当ノード。未接続（ID が空）や見つからない場合は null
        public PPUnitAINode ResolveNode(string aNodeId) => Profile != null ? Profile.FindNode(aNodeId) : null;

        // 評価状態を初期化する。コミットを解除して評価をやり直す際に使う
        public void ResetPath() => Path.Clear();

        // 指定の深さが、まだコミットした道順の上にいるかを判定する
        // 道順から外れた枝（＝割り込みや、より優先度の高い枝）では制約を掛けない
        // aDepth : 判定する深さ。優先度リストが自分の位置として渡す
        // return : その深さでコミットの制約を掛けるべきなら true
        public bool IsOnCommitPath(int aDepth)
        {
            if (CommitPath == null || aDepth >= CommitPath.Count) return false;

            for (int i = 0; i < aDepth; i++)
            {
                if (Path[i] != CommitPath[i]) return false;
            }
            return true;
        }

        // コミットした道順が、その深さで何番目の子へ進んでいたかを返す
        // aDepth : 対象の深さ
        // return : 子の添字
        public int CommitChildIndex(int aDepth) => CommitPath[aDepth];
    }

    // ユニット AI の判断ツリーを構成するノードの基底クラス
    // ツリーは「上から順に評価し、最初に成立した枝の行動をそのまま実行する」だけの単純な構造
    // 手順を跨いで状態を持たないため、毎ティック同じ木を評価すれば状況の変化がそのまま判断に反映される
    // 例外は待機コミットで、これだけは「決めた判断を数ティック維持する」ためにストラテジスト側が状態を持つ
    //
    // ノードはプロファイル側のフラットなリストに並び、親子関係はノード ID の参照で表す
    // 入れ子で持たないのは、ノードエディタ上で「作ってから繋ぐ」「一旦切り離す」を成立させるため
    //
    // PPSkillEffectDefinition と同じく ScriptableObject ではなく [SerializeReference] 対応の通常クラスとする
    // 派生クラスを追加するときは PPTypeMenuName を必ず付けること（型選択ピッカーとノードエディタがこれに依存する）
    [Serializable]
    public abstract class PPUnitAINode
    {
        [Header("表示")]
        [Label("ノード名")]
        [SerializeField] protected string mNodeName = "";

        // 待機コミット中でも評価するか
        // 通常はコミット中に優先度の低い枝を見ないが、これを立てた枝だけは常に評価される
        // 「溜めている最中でも瀕死の味方が出たら回復へ割り込む」といった例外を作るためのもの
        [Label("待機中でも割り込む")]
        [SerializeField] protected bool mIsInterrupt = false;

        // ノードを一意に指す ID。親子の接続はこの値で表す
        // ノードエディタが自動採番するため、手で編集しない
        [HideInInspector]
        [SerializeField] protected string mNodeId = "";

        // ノードエディタ上での配置。評価には影響しない
        [HideInInspector]
        [SerializeField] protected Vector2 mGraphPosition;

        // ノードエディタとインスペクタに出す表示名。未入力なら型ごとの既定名を使う
        public string NodeName => string.IsNullOrEmpty(mNodeName) ? DefaultNodeName : mNodeName;

        public bool IsInterrupt => mIsInterrupt;
        public string NodeId => mNodeId;
        public Vector2 GraphPosition => mGraphPosition;

        // 型ごとの既定表示名。派生側で上書きする
        protected virtual string DefaultNodeName => GetType().Name;

        // ID が未採番なら採番する。既に持っている場合は何もしない
        // 生成直後とアセット読み込み時にプロファイル側から呼ばれる
        public void EnsureNodeId()
        {
            if (string.IsNullOrEmpty(mNodeId))
            {
                mNodeId = Guid.NewGuid().ToString("N");
            }
        }

        // ノードエディタ上の配置を設定する
        // aPosition : 設定する座標
        public void SetGraphPosition(Vector2 aPosition) => mGraphPosition = aPosition;

        // このノードを評価する
        // 派生クラスで実装する。バトルの状態を変えず、判断だけを行うこと
        // aContext : 評価 1 回分の入力
        // return : 行動が確定した場合はその内容、確定しなければ PPUnitAINodeResult.Failed
        public abstract PPUnitAINodeResult Evaluate(PPUnitAIEvalContext aContext);

        // 接続している子ノードの ID を、接続口ごとに列挙する
        // ノードエディタが接続線を引くために使う。葉ノードは空を返す
        // return : 接続口の名前と、繋がっている子ノード ID の並び
        public virtual IReadOnlyList<PPUnitAINodePort> Ports => Array.Empty<PPUnitAINodePort>();

        // 指定の接続口へ子ノードを繋ぐ
        // ノードエディタからの操作専用。派生側で接続口ごとの保持先へ振り分ける
        // aPortIndex : 接続口の番号
        // aChildId : 繋ぐ子ノードの ID
        public virtual void ConnectChild(int aPortIndex, string aChildId)
        {
        }

        // 指定の子ノードとの接続を外す
        // ノードエディタからの操作専用
        // aPortIndex : 接続口の番号
        // aChildId : 外す子ノードの ID
        public virtual void DisconnectChild(int aPortIndex, string aChildId)
        {
        }

        // 接続している子ノードを、指定された ID の並び順へ揃える
        // 優先度リストのように並び順が意味を持つノードで、エディタ上の配置順を反映するために使う
        // aPortIndex : 接続口の番号
        // aOrderedChildIds : 並べ替え後の子ノード ID
        public virtual void ReorderChildren(int aPortIndex, IReadOnlyList<string> aOrderedChildIds)
        {
        }
    }

    // ノード 1 つが持つ接続口 1 つ分の情報
    // 「この口は何という名前で、今どの子が繋がっているか」をエディタへ伝える
    public readonly struct PPUnitAINodePort
    {
        // 接続口の表示名
        public string Name { get; }
        // 繋がっている子ノードの ID。並び順がそのまま優先度になる
        public IReadOnlyList<string> ChildIds { get; }
        // 複数の子を繋げる口か。false なら 1 つだけ
        public bool IsMultiple { get; }

        // aName : 接続口の表示名
        // aChildIds : 繋がっている子ノードの ID
        // aIsMultiple : 複数の子を繋げる口か
        public PPUnitAINodePort(string aName, IReadOnlyList<string> aChildIds, bool aIsMultiple)
        {
            Name = aName;
            ChildIds = aChildIds;
            IsMultiple = aIsMultiple;
        }
    }
}
