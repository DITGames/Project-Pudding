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
        // 確定した対象へ狙いを固定するティック数。0 なら固定しない
        public int FocusTicks { get; }

        private PPUnitAINodeResult(bool aIsDecided, BattleCommandBase aCommand, string aActionName,
            PPBattleUnit aTarget, int aCommitTicks, int aFocusTicks)
        {
            IsDecided = aIsDecided;
            Command = aCommand;
            ActionName = aActionName;
            Target = aTarget;
            CommitTicks = aCommitTicks;
            FocusTicks = aFocusTicks;
        }

        // 行動が決まらなかったことを表す結果。親は次の枝を評価する
        public static readonly PPUnitAINodeResult Failed = new(false, null, null, null, 0, 0);

        // 待機で確定したことを表す結果を作る
        // aActionName : 記録用の行動名
        public static PPUnitAINodeResult Wait(string aActionName) => new(true, null, aActionName, null, 0, 0);

        // コマンド実行で確定したことを表す結果を作る
        // aCommand : 実行するコマンド
        // aActionName : 記録用の行動名
        // aTarget : 行動の対象
        public static PPUnitAINodeResult Execute(BattleCommandBase aCommand, string aActionName, PPBattleUnit aTarget)
            => new(true, aCommand, aActionName, aTarget, 0, 0);

        // 同じ内容のまま、維持するティック数だけを差し替えた結果を作る
        // 行動そのものはアクションが決め、どれだけ維持するかはノードが決めるため分けている
        // aCommitTicks : 維持するティック数
        public PPUnitAINodeResult WithCommit(int aCommitTicks)
            => new(IsDecided, Command, ActionName, Target, aCommitTicks, FocusTicks);

        // 同じ内容のまま、狙いを固定するティック数だけを差し替えた結果を作る
        // 維持ティック数と同じく、対象を決めるのはアクション、どれだけ固定するかはノードの役割
        // aFocusTicks : 狙いを固定するティック数
        public PPUnitAINodeResult WithFocus(int aFocusTicks)
            => new(IsDecided, Command, ActionName, Target, CommitTicks, aFocusTicks);
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
        // サブツリー参照で評価先が切り替わるため、入れ子の最上段を「今のツリー」として扱う
        public PPUnitAIProfileDefinition Profile => mProfileStack[^1];
        // 思考主体の記憶。ノードは読むだけで、書き込みはストラテジストが行う
        public PPUnitAIMemory Memory { get; set; }
        // 思考主体のバトル中の見聞き。こちらもノードは読むだけ
        // 条件クラスはスナップショットしか受け取らないため、実体はそちらへ差し込まれている
        public PPUnitAIBlackboard Blackboard => Snapshot.GetBlackboard(Unit);
        // 実行待ちの行動の供給元。差し込まれていなければ null
        public IPPPendingActionSource PendingSource => Snapshot.PendingSource;

        // この思考で既に採用された枝のキー。連携ノードが「もう採った子」を飛ばすために読む
        // 寿命は 1 回の思考分で、仮押さえ台帳と同じくストラテジストが管理する
        public HashSet<string> AdoptedKeys { get; set; }
        // 経過の判定に使う現在のターン数
        public int TurnCount => Battle.TurnCount;
        // 拡張ルール。差し込まれていない場合は null
        public PPBattleRules Rules => Battle.Rules as PPBattleRules;

        // 評価中に辿っている枝の位置。優先度リストが子の添字を積んでいく
        // 行動が確定した時点の中身が、そのままその行動へ至る道順になる
        public List<int> Path { get; } = new();

        // 評価中に通過したノードの ID
        // 道順（Path）に積まれるのは優先度リストと抽選が選んだ子の添字だけで、
        // 条件分岐とターゲット検索は何も積まないため、道順からは通過したノード列を再構成できない
        // デバッグ表示で経路を辿れるようにするため、ノード ID の列を別に持つ
        public List<string> VisitedNodeIds { get; } = new();

        // 評価中の判断ツリーの入れ子。根が [0]、サブツリー参照で潜るたびに積まれる
        // 循環参照の検出も、この並びに同じツリーが既に居るかで判定する
        private readonly List<PPUnitAIProfileDefinition> mProfileStack = new();

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
            mProfileStack.Add(aProfile);
        }

        // サブツリー参照で参照先のツリーへ潜る
        // 既に入れ子へ積まれているツリーは循環しているため、潜らずに false を返す
        // 自己参照も相互参照も同じ判定で捕まえられる
        // aProfile : 潜る先のツリー
        // return : 潜れたら true。null または循環していれば false
        public bool TryPushProfile(PPUnitAIProfileDefinition aProfile)
        {
            if (aProfile == null || mProfileStack.Contains(aProfile)) return false;

            mProfileStack.Add(aProfile);
            return true;
        }

        // 潜った参照先から元のツリーへ戻る
        // 参照先の評価が不成立で終わった場合も必ず戻す必要があるため、呼び出し側は try / finally で通す
        public void PopProfile()
        {
            if (mProfileStack.Count > 1) mProfileStack.RemoveAt(mProfileStack.Count - 1);
        }

        // ID からノードを引く
        //
        // 評価から外されたノードは未接続と同じ扱いにするため null を返す
        // 判定をこの 1 箇所へ寄せることで、ノード種別ごとの個別対応が要らなくなる
        // 優先度リストは null を読み飛ばすだけで子の添字は進むため、優先度が繰り上がることもない
        // なお PPUnitAIProfileDefinition.FindNode 側には入れない
        // そちらはエディタのノード名解決や到達判定からも呼ぶため、外したノードも引けている必要がある
        //
        // aNodeId : 引くノードの ID
        // return : 該当ノード。未接続（ID が空）・見つからない・評価から外されている場合は null
        public PPUnitAINode ResolveNode(string aNodeId)
        {
            var node = Profile != null ? Profile.FindNode(aNodeId) : null;
            return node is { IsMuted: false } ? node : null;
        }

        // 評価状態を初期化する。コミットを解除して評価をやり直す際に使う
        // やり直しでは 1 回目の通過記録も捨てる。通らなかった経路が残ると経路表示が嘘になるため
        public void ResetPath()
        {
            Path.Clear();
            VisitedNodeIds.Clear();
        }

        // 通過したノードとして記録する
        // aNodeId : 通過したノードの ID
        public void PushVisited(string aNodeId)
        {
            if (!string.IsNullOrEmpty(aNodeId)) VisitedNodeIds.Add(aNodeId);
        }

        // その枝がこの思考で既に採用済みかを返す
        // aKey : 枝を表すキー
        // return : 採用済みなら true
        public bool IsAdopted(string aKey)
            => AdoptedKeys != null && !string.IsNullOrEmpty(aKey) && AdoptedKeys.Contains(aKey);

        // 通過記録を指定した長さまで切り詰める
        // 不成立で引き返した枝の記録を残さないため、道順を巻き戻すのと同じ位置で呼ぶ
        // aCount : 残す長さ
        public void TrimVisited(int aCount)
        {
            if (VisitedNodeIds.Count > aCount)
            {
                VisitedNodeIds.RemoveRange(aCount, VisitedNodeIds.Count - aCount);
            }
        }

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

        // 評価から外すか
        // ON にすると、このノードを指す接続がすべて未接続と同じ扱いになる
        // ツリーを崩さずに一部の枝だけ止めて挙動を切り分けたいときに使う
        [Label("評価から外す")]
        [SerializeField] protected bool mIsMuted = false;

        // このノードで行動が確定してから、次に成立させるまで空けるティック数
        // 0 なら間を空けない。待機コミット（判断を維持するティック数）とは別の軸で、両方を同時に設定できる
        [Label("間を空けるティック数")]
        [SerializeField] protected int mCooldownTicks = 0;

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
        public bool IsMuted => mIsMuted;
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

        // ID を採番し直す
        // 複製・貼り付けでノードを複写した際に、元のノードと ID が衝突しないよう振り直すために使う
        // 呼んだあとは、このノードを指していた接続を RemapChildIds で張り替える必要がある
        public void ReassignNodeId() => mNodeId = Guid.NewGuid().ToString("N");

        // 接続先の子ノード ID を、対応表に従って置き換える
        //
        // 複製・貼り付けで複写したノード同士の接続を、新しい ID へ張り直すために使う
        // 対応表に載っていない接続先は複写の範囲外だったものなので、未接続へ落とす
        // （複写した側から元のノードへ線が残ると、増やしたつもりのない枝が生える）
        //
        // 接続を持つノードで上書きする
        // ConnectChild / DisconnectChild で置き換えないのは、抽選ノードの重みのように
        // 接続へ紐づく設定が張り替えで失われてしまうため
        //
        // aMap : 置き換え前の ID から置き換え後の ID への対応表
        public virtual void RemapChildIds(IReadOnlyDictionary<string, string> aMap)
        {
        }

        // 対応表に従って接続先 1 つ分を置き換える
        // aChildId : 置き換える接続先の ID
        // aMap : 対応表
        // return : 置き換え後の ID。範囲外だった場合は空文字
        protected static string RemapChildId(string aChildId, IReadOnlyDictionary<string, string> aMap)
        {
            if (string.IsNullOrEmpty(aChildId)) return "";

            return aMap.TryGetValue(aChildId, out string mapped) ? mapped : "";
        }

        // 対応表に従って接続先のリストを置き換える
        // 範囲外だった接続はリストから取り除く
        // aChildIds : 置き換える接続先のリスト
        // aMap : 対応表
        protected static void RemapChildIds(List<string> aChildIds, IReadOnlyDictionary<string, string> aMap)
        {
            for (int i = aChildIds.Count - 1; i >= 0; i--)
            {
                string mapped = RemapChildId(aChildIds[i], aMap);
                if (string.IsNullOrEmpty(mapped)) aChildIds.RemoveAt(i);
                else aChildIds[i] = mapped;
            }
        }

        // このノードを評価する
        //
        // 全ノードに共通する判定（クールダウン中か、経路の記録）をここで済ませ、
        // 種別ごとの判断は EvaluateCore が担う
        // 共通処理を 1 箇所へ寄せることで、ノード種別を増やしても掛け忘れが起きない
        //
        // aContext : 評価 1 回分の入力
        // return : 行動が確定した場合はその内容、確定しなければ PPUnitAINodeResult.Failed
        public PPUnitAINodeResult Evaluate(PPUnitAIEvalContext aContext)
        {
            if (IsOnCooldown(aContext)) return PPUnitAINodeResult.Failed;

            aContext.PushVisited(mNodeId);
            return EvaluateCore(aContext);
        }

        // クールダウン中かを判定する
        // 前回このノードで行動が確定してから、指定ティック数が経過するまで不成立を返す
        // 経過はターン数の差分で数えるため、1 ティックに複数回思考しても余分に消化しない
        // aContext : 評価 1 回分の入力
        // return : クールダウン中なら true
        protected bool IsOnCooldown(PPUnitAIEvalContext aContext)
        {
            if (mCooldownTicks <= 0 || aContext.Memory == null) return false;
            if (!aContext.Memory.TryGetFiredTurn(mNodeId, out int firedTurn)) return false;

            return aContext.TurnCount - firedTurn < mCooldownTicks;
        }

        // 種別ごとの評価本体
        // 派生クラスで実装する。バトルの状態を変えず、判断だけを行うこと
        // aContext : 評価 1 回分の入力
        // return : 行動が確定した場合はその内容、確定しなければ PPUnitAINodeResult.Failed
        protected abstract PPUnitAINodeResult EvaluateCore(PPUnitAIEvalContext aContext);

        // グラフ上のノードへ出す設定内容の要約
        // タイトルだけでは「どんな条件で何を撃つのか」が読めないため、中身を 1〜2 行で示す
        // 派生側で上書きする。空文字を返した場合は要約行を出さない
        public virtual string Summary => "";

        // このノードが持つ条件の説明文を組み立て直す
        // 条件を持つノードで上書きする。グラフ上のサマリ表示を設定内容へ追従させるために使う
        public virtual void RefreshConditionDescriptions()
        {
        }

        // 条件リストの説明文を「／」で連ねて要約にする
        // 説明文はそれぞれの条件が設定内容から組み立てたもので、空のものは飛ばす
        // aUnitConditions : ユニット条件
        // aPartyConditions : パーティ条件
        // return : 連ねた要約。条件が 1 つも無ければ「条件なし」
        protected static string BuildConditionSummary(IReadOnlyList<PPUnitConditionValidator> aUnitConditions,
            IReadOnlyList<PPPartyConditionValidator> aPartyConditions = null)
        {
            var parts = new List<string>();
            if (aUnitConditions != null)
            {
                foreach (var condition in aUnitConditions)
                {
                    if (!string.IsNullOrEmpty(condition?.Description)) parts.Add(condition.Description);
                }
            }

            if (aPartyConditions != null)
            {
                foreach (var condition in aPartyConditions)
                {
                    if (!string.IsNullOrEmpty(condition?.Description)) parts.Add(condition.Description);
                }
            }
            return parts.Count == 0 ? "条件なし" : string.Join(" ／ ", parts);
        }

        // 条件リストの説明文をまとめて組み立て直す
        // 条件を持つノードが RefreshConditionDescriptions から呼ぶ
        // aConditions : 対象の条件リスト
        protected static void RefreshDescriptions(IReadOnlyList<PPUnitConditionValidator> aConditions)
        {
            if (aConditions == null) return;

            foreach (var condition in aConditions)
            {
                condition?.RefreshDescription();
            }
        }

        // 条件リストの説明文をまとめて組み立て直す（パーティ条件版）
        // aConditions : 対象の条件リスト
        protected static void RefreshDescriptions(IReadOnlyList<PPPartyConditionValidator> aConditions)
        {
            if (aConditions == null) return;

            foreach (var condition in aConditions)
            {
                condition?.RefreshDescription();
            }
        }

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
