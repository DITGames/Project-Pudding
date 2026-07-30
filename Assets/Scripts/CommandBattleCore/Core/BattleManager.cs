/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleManager.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトル進行のコアクラス
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CommandBattleCore
{
    /// <summary>
    /// バトル 1 戦闘分の進行を統括するコアクラス。
    /// <para>
    /// 担う責務は次の 4 つ。
    /// 1. <see cref="BattleContext"/> を受け取ってバトルを開始し、パーティ／ユニットのイベントを購読する
    /// 2. <see cref="ActionQueue"/> に積まれたコマンドを取り出して実行する（同期版 / 演出待ちの async 版）
    /// 3. <see cref="AdvanceTick"/> でターン経過を進め、勝敗判定を行う
    /// 4. 上記の過程で起きた出来事を C# event として外部（View・ブリッジ層）へ通知する
    /// </para>
    /// <para>
    /// 判定ロジックそのものは持たず、<see cref="IBattleResultChecker"/> / <see cref="ITurnOrderResolver"/> /
    /// <see cref="IBattlePresenter"/> などの差し替え可能なインターフェースへ委譲する。
    /// </para>
    /// </summary>
    public class BattleManager
    {
        /// <summary>1 バトル当たりの状態（両パーティ・ルール・ターン数・報酬）を持つコンテキスト。</summary>
        public BattleContext Context { get; protected set; }
        /// <summary>バトル進行ステート（BattleStart → CommandInput → ActionExecution → ResultCheck → BattleEnd）の管理。</summary>
        public BattleStateMachine StateMachine { get; } = new();
        /// <summary>実行待ちコマンドの FIFO キュー。リアクションは先頭へ割り込む。</summary>
        public ActionQueue ActionQueue { get; } = new ActionQueue();
        /// <summary>スキル演出プレゼンター。null なら演出待ちなしで即座に処理が進む。</summary>
        public IBattlePresenter Presenter { get; set; }
        /// <summary>進行中の演出スキップ用。各コマンド演出ごとに作り直す。</summary>
        public CancellationTokenSource PresentationCts { get; protected set; }
        /// <summary>勝敗判定クラス。差し替えることで引き分け条件などを追加できる。</summary>
        public IBattleResultChecker ResultChecker { get; set; } = new DefaultBattleResultChecker();
        /// <summary>バトルログの出力先。</summary>
        public IBattleLogger Logger { get; set; } = new DefaultBattleLogger();
        /// <summary>ターンごとの行動順並び替えクラス。既定は素早さ順。</summary>
        public ITurnOrderResolver TurnOrderResolver { get; set; } = new SpeedTurnOrderResolver();
        /// <summary>1 イベント当たりのリアクション上限。反撃の連鎖が無限に続くのを抑止する。</summary>
        public int MaxReactionPerEvent { get; set; } = 1;
        /// <summary>現在のコマンドが誘発したリアクション総数。<see cref="MaxReactionPerEvent"/> との比較に使う。</summary>
        protected int mReactionsThisCommand = 0;
        /// <summary>リアクション実行中は true。この間は新たなリアクションを発生させない。</summary>
        protected bool mIsSuppressReactions = false;

        /// <summary>ステート変更時(バトル状態)</summary>
        public event Action<BattleState> OnStateChanged;
        /// <summary>ターン開始時など(ターン番号)</summary>
        public event Action<int> OnTickStarted;
        /// <summary>ターン終了時など(ターン番号)</summary>
        public event Action<int> OnTickEnded;
        /// <summary>行動が状態異常により失敗したとき(行動ユニット, 実行コマンド)</summary>
        public event Action<BattleUnit, BattleCommandBase> OnActionBlocked;
        /// <summary>コマンド実行直前(行動ユニット, 実行コマンド)</summary>
        public event Action<BattleUnit, BattleCommandBase> OnPreCommand;
        /// <summary>コマンド実行直後(行動ユニット, 実行コマンド)</summary>
        public event Action<BattleUnit, BattleCommandBase> OnPostCommand;
        /// <summary>コマンド追加時(行動ユニット, 実行コマンド)</summary>
        public event Action<BattleUnit, BattleCommandBase> OnCommandQueued;
        /// <summary>コマンド実行時(行動ユニット, 実行コマンド)</summary>
        public event Action<BattleUnit, BattleCommandBase> OnCommandExecuted;
        /// <summary>ダメージ時(対象ユニット, 値)</summary>
        public event Action<BattleUnit, float> OnDamageTaken;
        /// <summary>攻撃結果決定時(攻撃情報)</summary>
        public event Action<DamageInfo> OnDamageResolved;
        /// <summary>回復時(対象ユニット, 値)</summary>
        public event Action<BattleUnit, float> OnHealed;
        /// <summary>ユニット撃破時(対象ユニット)</summary>
        public event Action<BattleUnit> OnUnitDefeated;
        /// <summary>ユニット入れ替え時(退避ユニット, 参戦ユニット)</summary>
        public event Action<BattleUnit, BattleUnit> OnUnitSwapped;
        /// <summary>ステータスエフェクト追加時(対象ユニット, エフェクト)</summary>
        public event Action<BattleUnit, StatusEffect> OnStatsEffectAdded;
        /// <summary>ステータスエフェクト除去時(対象ユニット, エフェクト)</summary>
        public event Action<BattleUnit, StatusEffect> OnStatsEffectRemoved;
        /// <summary>ステータスエフェクトスタック時(対象ユニット, エフェクト)</summary>
        public event Action<BattleUnit,  StatusEffect> OnStatusEffectStacked;
        /// <summary>バトル終了時(リザルト)</summary>
        public event Action<BattleResult> OnBattleEnded;

        /// <summary>バトルログのタイムスタンプ供給関数。既定は常に 0。Unity 側から Time.time 等を差し込む。</summary>
        public Func<float> TimeProvider { get; set; } = () => 0f;

        /// <summary>
        /// ステートマシンの遷移イベントを自身の <see cref="OnStateChanged"/> へ中継する。
        /// </summary>
        public BattleManager()
        {
            StateMachine.OnStateChanged += (_, next) => OnStateChanged?.Invoke(next);
        }

        /// <summary>
        /// コンテキストを受け取ってバトルを開始する。
        /// 両パーティのイベント購読、全スキルのクールダウン／使用回数リセット、入れ替え通知の接続までを行う。
        /// </summary>
        /// <param name="aContext">この戦闘で使用するコンテキスト。null は不可。</param>
        /// <exception cref="ArgumentNullException"><paramref name="aContext"/> が null の場合。</exception>
        public void StartBattle(BattleContext aContext)
        {
            Context = aContext ?? throw new ArgumentNullException(nameof(aContext));
            StateMachine.TransitionTo(BattleState.BattleStart);

            // 両パーティの全ユニットのイベントを購読する
            SubscribeParty(aContext.AllyParty);
            SubscribeParty(aContext.EnemyParty);

            // 控えも含む全ユニットのスキルを戦闘開始状態へ戻す（クールダウン・使用回数）
            foreach (var party in new[]{Context.AllyParty, Context.EnemyParty})
                foreach (var unit in party.ActiveMembers.Concat(party.ReserveMembers))
                    foreach (var skill in unit.Skills)
                        skill.ResetForBattle();

            aContext.AllyParty.OnSwapped += HandleSwap;
            aContext.EnemyParty.OnSwapped += HandleSwap;
        }

        /// <summary>
        /// パーティに属する全ユニット（アクティブ・控え両方）のイベントを購読する。
        /// </summary>
        /// <param name="aParty">購読対象のパーティ。</param>
        protected virtual void SubscribeParty(BattleParty aParty)
        {
            foreach (var unit in aParty.ActiveMembers) SubscribeUnit(unit);
            foreach (var unit in aParty.ReserveMembers) SubscribeUnit(unit);
        }

        /// <summary>
        /// ユニット単位のイベントを購読し、BattleManager のイベント通知・バトルログ出力・
        /// リアクション発火・撃破時の勝敗判定へ変換する中継を張る。
        /// </summary>
        /// <param name="aUnit">購読対象のユニット。</param>
        protected virtual void SubscribeUnit(BattleUnit aUnit)
        {
            aUnit.OnDamaged += (u, dmg) =>
            {
                OnDamageTaken?.Invoke(u, dmg);
                Log(BattleLogType.Damage, null, u, $"{u.DisplayName} took {dmg} damage.");
            };
            aUnit.OnHealed += (u, amt) =>
            {
                OnHealed?.Invoke(u, amt);
                DispatchReactions(new ReactionContext(
                    ReactionTrigger.OnHealed, u, u));
                Log(BattleLogType.Heal, null, u, $"{u.DisplayName} recovered {amt} HP.");
            };
            aUnit.OnDefeated += u =>
            {
                OnUnitDefeated?.Invoke(u);
                DispatchReactions(new ReactionContext(
                    ReactionTrigger.OnUnitDefeated, null, u));
                Log(BattleLogType.UnitDefeated, null, u, $"{u.DisplayName} was defeated.");
                // 撃破で全滅している可能性があるため、その場で勝敗を判定する
                CheckBattleResult();
            };
            aUnit.OnStatusEffectAdded += (u, e) =>
            {
                OnStatsEffectAdded?.Invoke(u, e);
                DispatchReactions(new ReactionContext(
                    ReactionTrigger.OnStatusAdded, null, u));
                Log(BattleLogType.StatusEffect, null, u, $"{u.DisplayName} gained {e.DisplayName}.");
            };
            aUnit.OnStatusEffectRemoved += (u, e) =>
            {
                OnStatsEffectRemoved?.Invoke(u, e);
                Log(BattleLogType.StatusEffect, null, u, $"{u.DisplayName} lost {e.DisplayName}.");
            };
            aUnit.OnStatusEffectStacked += (u, e) =>
            {
                OnStatusEffectStacked?.Invoke(u, e);
                Log(BattleLogType.StatusEffect, null, u, $"{u.DisplayName}'s {e.DisplayName} stacked ({e.CurrentStacks}).");
            };
            aUnit.OnDamageResolved += info =>
            {
                OnDamageResolved?.Invoke(info);
                DispatchReactions(new ReactionContext(
                    ReactionTrigger.OnDamaged, info.Source, info.Target, info));
            };
        }

        /// <summary>
        /// パーティのメンバー入れ替えを受けて通知とログ出力を行う。
        /// </summary>
        /// <param name="aOut">退場したユニット。</param>
        /// <param name="aIn">参戦したユニット。</param>
        protected virtual void HandleSwap(BattleUnit aOut, BattleUnit aIn)
        {
            OnUnitSwapped?.Invoke(aOut, aIn);
            Log(BattleLogType.Swap, aOut, aIn, $"{aOut.DisplayName} swapped out for {aIn.DisplayName}.");
        }

        /// <summary>
        /// コマンドをキュー末尾に積む。積むタイミングは採用先（プレイヤー入力 / AI）に委ねられる。
        /// </summary>
        /// <param name="aCommand">実行待ちにするコマンド。</param>
        public void EnqueueCommand(BattleCommandBase aCommand)
        {
            ActionQueue.Enqueue(aCommand);
            OnCommandQueued?.Invoke(aCommand.Source, aCommand);
            Log(BattleLogType.Action, aCommand.Source, null, $"{aCommand.Source.DisplayName} queued {aCommand.GetType().Name}.");
        }

        /// <summary>
        /// キュー先頭のコマンドを 1 件実行する（同期版・演出待ちなし）。
        /// 実行 → 行動回数消費 → 勝敗チェック → コマンド入力ステートへ復帰、までを 1 呼び出しで行う。
        /// </summary>
        /// <returns>コマンドを取り出して処理した場合 true。バトル終了済みまたはキューが空なら false。</returns>
        public bool ExecuteNextCommand()
        {
            if(StateMachine.Current == BattleState.BattleEnd) return false;
            if (!ActionQueue.TryDequeue(out var command)) return false;

            // 実行ステートに遷移
            StateMachine.TransitionTo(BattleState.ActionExecution);
            OnPreCommand?.Invoke(command.Source, command);

            // オブジェクトが生存 + 行動不可の制限がかかっていなければ実行する
            // 麻痺などの確率による行動制限がある場合は別で判別する必要がありそう
            if (command.Source.IsAlive && (command.Source.CurrentRestrictions & ActionRestriction.CannotAct) == 0)
            {
                if (command.Source.RollActionBlocked(Context))
                {
                    OnActionBlocked?.Invoke(command.Source, command);
                    Log(BattleLogType.ActionBlocked, command.Source, null,
                        $"{command.Source.DisplayName}'s action was blocked by a status effect.");
                }
                else
                {
                    command.Execute(Context);
                    OnCommandExecuted?.Invoke(command.Source, command);
                }
            }

            // 行動回数の消費
            command.Source.Actions.Consume();
            OnPostCommand?.Invoke(command.Source, command);

            // ステートマシンをリザルトチェックに移行
            // 演出を入れる場合演出終了をリッスンしてそのあとにチェックするほうがよさそう
            StateMachine.TransitionTo(BattleState.ResultCheck);
            CheckBattleResult();

            // 決着していなければ次のコマンド入力を受け付ける
            if (StateMachine.Current != BattleState.BattleEnd)
            {
                StateMachine.TransitionTo(BattleState.CommandInput);
            }

            return true;
        }

        /// <summary>
        /// キューが空になるかバトルが終了するまで連続実行する。ターン制向けのユーティリティ。
        /// 演出待ちがないので、演出を挟みたい場合は <see cref="ExecuteAllCommandAsync"/> を使う。
        /// </summary>
        public void ExecuteAllCommands()
        {
            while (StateMachine.Current != BattleState.BattleEnd && ExecuteNextCommand())
            {

            }
        }

        /// <summary>
        /// キュー先頭のコマンドを 1 件実行する（async 版）。
        /// 同期版との違いは <see cref="Presenter"/> による実行前後の演出待ちが入る点と、
        /// リアクションコマンド実行中はリアクション抑止フラグを立てる点。
        /// </summary>
        /// <param name="aCt">演出スキップ用のキャンセルトークン。</param>
        public async ValueTask ExecuteNextCommandAsync(CancellationToken aCt = default)
        {
            if (StateMachine.Current == BattleState.BattleEnd) return;
            if(!ActionQueue.TryDequeue(out var command)) return;

            // リアクション中は新たなリアクションを抑止し、通常コマンドならリアクション数を数え直す
            bool isReaction = command.IsReaction;
            if (isReaction)
            {
                mIsSuppressReactions = true;
            }
            else
            {
                mReactionsThisCommand = 0;
            }

            StateMachine.TransitionTo(BattleState.ActionExecution);

            // コマンド実行前イベントの通知
            OnPreCommand?.Invoke(command.Source, command);

            try
            {
                if (command.Source.IsAlive)
                {
                    if (command.Source.RollActionBlocked(Context))
                    {
                        OnActionBlocked?.Invoke(command.Source, command);
                        Log(BattleLogType.ActionBlocked, command.Source, null,
                            $"{command.Source.DisplayName}'s action was blocked by a status effect.");
                    }
                    else
                    {
                        // コマンド実行前演出
                        if (Presenter != null)
                        {
                            await SafePlaySkillPresentation(Presenter.PlayPreExecute(command, Context, aCt));
                        }

                        // 実際のコマンド実行
                        command.Execute(Context);
                        OnCommandExecuted?.Invoke(command.Source, command);

                        // コマンド実行後演出
                        if (Presenter != null)
                        {
                            await SafePlaySkillPresentation(Presenter.PlayPostExecute(command, Context, aCt));
                        }
                    }
                }
            }
            finally
            {
                // 例外や演出スキップで抜けても抑止フラグを残さない
                if(isReaction) mIsSuppressReactions = false;
            }

            // コマンド実行後イベントの通知
            command.Source.Actions.Consume();
            OnPostCommand?.Invoke(command.Source, command);

            StateMachine.TransitionTo(BattleState.ResultCheck);
            CheckBattleResult();
            if(StateMachine.Current != BattleState.BattleEnd)
                StateMachine.TransitionTo(BattleState.CommandInput);
        }

        /// <summary>
        /// キューが空になるかバトルが終了するまで、演出待ちを挟みながら連続実行する（async 版）。
        /// </summary>
        /// <param name="aCt">演出スキップ用のキャンセルトークン。</param>
        public async ValueTask ExecuteAllCommandAsync(CancellationToken aCt = default)
        {
            while (StateMachine.Current != BattleState.BattleEnd && ActionQueue.Count > 0)
            {
                await ExecuteNextCommandAsync(aCt);
            }
        }

        /// <summary>進行中のスキル演出をキャンセルしてスキップする。</summary>
        public void SkipCurrentPresentation() => PresentationCts?.Cancel();

        /// <summary>
        /// スキル演出を実行し、スキップによるキャンセル例外だけを飲み込む。
        /// 演出が中断されてもバトル進行自体は止めないためのラッパー。
        /// </summary>
        /// <param name="aPlay">待機対象の演出タスク。</param>
        protected static async ValueTask SafePlaySkillPresentation(ValueTask aPlay)
        {
            try
            {
                await aPlay;
            }
            catch (OperationCanceledException)
            {
                // スキップされた場合。演出は中断
            }
        }

        /// <summary>
        /// リアクショントリガーを発火し、条件を満たしたユニットの反撃コマンドをキュー先頭へ割り込ませる。
        /// <see cref="MaxReactionPerEvent"/> に達した時点で打ち切り、リアクション実行中は何もしない。
        /// </summary>
        /// <param name="aContext">発生したトリガーと関係ユニットを持つリアクションコンテキスト。</param>
        protected virtual void DispatchReactions(ReactionContext aContext)
        {
            // 反撃の実行中には新たな反撃は起こさないようにする
            if(mIsSuppressReactions) return;

            foreach (var unit in EnumerateAllAliveUnits())
            {
                if (mReactionsThisCommand >= MaxReactionPerEvent) break;
                foreach (var reaction in unit.Reactions)
                {
                    if (mReactionsThisCommand >= MaxReactionPerEvent) break;
                    // トリガー種別が一致し、反撃側が生存し、条件を満たすものだけを採用する
                    if (reaction.Trigger != aContext.Trigger) continue;
                    if (!unit.IsAlive) break;
                    if (!reaction.ShouldReact(unit, aContext, Context)) continue;

                    var cmd = reaction.BuildReaction(unit, aContext, Context);
                    if (cmd == null) continue;

                    // 通常コマンドより先に処理させるため先頭へ積む
                    cmd.IsReaction = true;
                    ActionQueue.EnqueueFront(cmd);
                    mReactionsThisCommand++;
                }
            }
        }

        /// <summary>
        /// 敵味方双方の、生存しているアクティブメンバーを順に列挙する。
        /// </summary>
        /// <returns>味方 → 敵の順に並んだ生存ユニット列。</returns>
        protected virtual IEnumerable<BattleUnit> EnumerateAllAliveUnits()
        {
            foreach (var unit in Context.AllyParty.GetAliveActiveMembers()) yield return unit;
            foreach (var unit in Context.EnemyParty.GetAliveActiveMembers()) yield return unit;
        }

        /// <summary>
        /// ターン経過処理。ターン制ならターンごと、ATB などなら一定秒ごとに呼び出す。
        /// ターン終了通知 → 両パーティの Tick（状態異常・行動回数・クールダウン更新）→ 勝敗判定 →
        /// ターン番号加算 → ターン開始通知、の順に進む。
        /// </summary>
        public void AdvanceTick()
        {
            OnTickEnded?.Invoke(Context.TurnCount);
            DispatchReactions(new ReactionContext(
                ReactionTrigger.OnTurnEnded, null, null, null, Context));

            Context.AllyParty.PartyTick(Context);
            Context.EnemyParty.PartyTick(Context);

            // 毒などの継続ダメージで決着する場合があるためここで判定する
            CheckBattleResult();
            if (StateMachine.Current == BattleState.BattleEnd) return;

            Context.TurnCount++;
            OnTickStarted?.Invoke(Context.TurnCount);
            DispatchReactions(new ReactionContext(
                ReactionTrigger.OnTurnStarted, null, null, null, Context));
        }

        /// <summary>
        /// 現在の行動順を取得する。並び替えロジックは <see cref="TurnOrderResolver"/> に委譲する。
        /// </summary>
        /// <returns>行動順に並んだユニットのリスト。</returns>
        public List<BattleUnit> GetTurnOrder() => TurnOrderResolver.ResolveOrder(Context);

        /// <summary>
        /// 勝敗をチェックし、決着していれば <see cref="EndBattle"/> を呼んでバトルを終了させる。
        /// </summary>
        /// <returns>判定結果。コンテキスト未設定または終了済みなら null。</returns>
        public BattleResult CheckBattleResult()
        {
            if(Context == null || StateMachine.Current == BattleState.BattleEnd) return null;

            var result = ResultChecker.CheckResult(Context);
            if (result.Type != BattleResultType.InProgress)
            {
                EndBattle(result);
            }

            return result;
        }

        /// <summary>
        /// バトルを終了させる。未実行コマンドを破棄し、終了ステートへ遷移して結果を通知する。
        /// </summary>
        /// <param name="aResult">確定した戦闘結果。</param>
        protected virtual void EndBattle(BattleResult aResult)
        {
            ActionQueue.Clear();
            StateMachine.TransitionTo(BattleState.BattleEnd);
            Log(aResult.Type == BattleResultType.Escaped ? BattleLogType.Escape : BattleLogType.Custom, null, null,
                $"Battle ended: {aResult.Type}");
            OnBattleEnded?.Invoke(aResult);
        }

        /// <summary>
        /// バトルログ出力のヘルパー。<see cref="TimeProvider"/> でタイムスタンプを付けて <see cref="Logger"/> へ流す。
        /// </summary>
        /// <param name="aType">ログ種別。</param>
        /// <param name="aUnit">行動主体のユニット。無ければ null。</param>
        /// <param name="aTarget">対象ユニット。無ければ null。</param>
        /// <param name="aDescription">ログ本文。</param>
        protected virtual void Log(BattleLogType aType, BattleUnit aUnit, BattleUnit aTarget, string aDescription)
        {
            Logger?.Log(new BattleLogEntry(aType, aUnit, aTarget, aDescription, TimeProvider()));
        }
    }
}
