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
    // バトル1戦闘分の進行を統括するコアクラス
    // コンテキストを受けてバトルを開始し、コマンドキューの実行とターン経過を管理する
    // 判定ロジックそのものは持たず、差し替え可能なインターフェースへ委譲する
    public class BattleManager
    {
        // 1 バトル当たりの状態（両パーティ・ルール・ターン数・報酬）を持つコンテキスト
        public BattleContext Context { get; protected set; }
        // バトル進行ステート（BattleStart → CommandInput → ActionExecution → ResultCheck → BattleEnd）の管理
        public BattleStateMachine StateMachine { get; } = new();
        // 実行待ちコマンドの FIFO キュー。リアクションは先頭へ割り込む
        public ActionQueue ActionQueue { get; } = new ActionQueue();
        // スキル演出プレゼンター。null なら演出待ちなしで即座に処理が進む
        public IBattlePresenter Presenter { get; set; }
        // 進行中の演出スキップ用。各コマンド演出ごとに作り直す
        public CancellationTokenSource PresentationCts { get; protected set; }
        // 勝敗判定クラス。差し替えることで引き分け条件などを追加できる
        public IBattleResultChecker ResultChecker { get; set; } = new DefaultBattleResultChecker();
        // バトルログの出力先
        public IBattleLogger Logger { get; set; } = new DefaultBattleLogger();
        // ターンごとの行動順並び替えクラス。既定は素早さ順
        public ITurnOrderResolver TurnOrderResolver { get; set; } = new SpeedTurnOrderResolver();
        // 1 イベント当たりのリアクション上限。反撃の連鎖が無限に続くのを抑止する
        public int MaxReactionPerEvent { get; set; } = 1;
        // 現在のコマンドが誘発したリアクション総数。MaxReactionPerEvent との比較に使う
        protected int mReactionsThisCommand = 0;
        // リアクション実行中は true。この間は新たなリアクションを発生させない
        protected bool mIsSuppressReactions = false;

        // ステート変更時(バトル状態)
        public event Action<BattleState> OnStateChanged;
        // ターン開始時など(ターン番号)
        public event Action<int> OnTickStarted;
        // ターン終了時など(ターン番号)
        public event Action<int> OnTickEnded;
        // 行動が状態異常により失敗したとき(行動ユニット, 実行コマンド)
        public event Action<BattleUnit, BattleCommandBase> OnActionBlocked;
        // コマンド実行直前(行動ユニット, 実行コマンド)
        public event Action<BattleUnit, BattleCommandBase> OnPreCommand;
        // コマンド実行直後(行動ユニット, 実行コマンド)
        public event Action<BattleUnit, BattleCommandBase> OnPostCommand;
        // コマンド追加時(行動ユニット, 実行コマンド)
        public event Action<BattleUnit, BattleCommandBase> OnCommandQueued;
        // コマンド実行時(行動ユニット, 実行コマンド)
        public event Action<BattleUnit, BattleCommandBase> OnCommandExecuted;
        // ダメージ時(対象ユニット, 値)
        public event Action<BattleUnit, float> OnDamageTaken;
        // 攻撃結果決定時(攻撃情報)
        public event Action<DamageInfo> OnDamageResolved;
        // 回復時(対象ユニット, 値)
        public event Action<BattleUnit, float> OnHealed;
        // ユニット撃破時(対象ユニット)
        public event Action<BattleUnit> OnUnitDefeated;
        // ユニット入れ替え時(退避ユニット, 参戦ユニット)
        public event Action<BattleUnit, BattleUnit> OnUnitSwapped;
        // ステータスエフェクト追加時(対象ユニット, エフェクト)
        public event Action<BattleUnit, StatusEffect> OnStatsEffectAdded;
        // ステータスエフェクト除去時(対象ユニット, エフェクト)
        public event Action<BattleUnit, StatusEffect> OnStatsEffectRemoved;
        // ステータスエフェクトスタック時(対象ユニット, エフェクト)
        public event Action<BattleUnit,  StatusEffect> OnStatusEffectStacked;
        // バトル終了時(リザルト)
        public event Action<BattleResult> OnBattleEnded;

        // バトルログのタイムスタンプ供給関数。既定は常に 0。Unity 側から Time.time 等を差し込む
        public Func<float> TimeProvider { get; set; } = () => 0f;

        // ステートマシンの遷移イベントを自身の OnStateChanged へ中継する
        public BattleManager()
        {
            StateMachine.OnStateChanged += (_, next) => OnStateChanged?.Invoke(next);
        }

        // コンテキストを受け取ってバトルを開始する
        // 両パーティのイベント購読、全スキルのクールダウン／使用回数リセット、入れ替え通知の接続までを行う
        // aContext : この戦闘で使用するコンテキスト。null は不可
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

        // パーティに属する全ユニット（アクティブ・控え両方）のイベントを購読する
        // aParty : 購読対象のパーティ
        protected virtual void SubscribeParty(BattleParty aParty)
        {
            foreach (var unit in aParty.ActiveMembers) SubscribeUnit(unit);
            foreach (var unit in aParty.ReserveMembers) SubscribeUnit(unit);
        }

        // ユニット単位のイベントを購読し、BattleManager のイベント通知・バトルログ出力・
        // リアクション発火・撃破時の勝敗判定へ変換する中継を張る
        // aUnit : 購読対象のユニット
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

        // パーティのメンバー入れ替えを受けて通知とログ出力を行う
        // aOut : 退場したユニット
        // aIn : 参戦したユニット
        protected virtual void HandleSwap(BattleUnit aOut, BattleUnit aIn)
        {
            OnUnitSwapped?.Invoke(aOut, aIn);
            Log(BattleLogType.Swap, aOut, aIn, $"{aOut.DisplayName} swapped out for {aIn.DisplayName}.");
        }

        // コマンドをキュー末尾に積む。積むタイミングは採用先（プレイヤー入力 / AI）に委ねられる
        // aCommand : 実行待ちにするコマンド
        public void EnqueueCommand(BattleCommandBase aCommand)
        {
            ActionQueue.Enqueue(aCommand);
            OnCommandQueued?.Invoke(aCommand.Source, aCommand);
            Log(BattleLogType.Action, aCommand.Source, null, $"{aCommand.Source.DisplayName} queued {aCommand.GetType().Name}.");
        }

        // キュー先頭のコマンドを 1 件実行する（同期版・演出待ちなし）
        // 実行 → 行動回数消費 → 勝敗チェック → コマンド入力ステートへ復帰、までを 1 呼び出しで行う
        // return : コマンドを取り出して処理した場合 true。バトル終了済みまたはキューが空なら false
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

        // キューが空になるかバトルが終了するまで連続実行する。ターン制向けのユーティリティ
        // 演出待ちがないので、演出を挟みたい場合は ExecuteAllCommandAsync を使う
        public void ExecuteAllCommands()
        {
            while (StateMachine.Current != BattleState.BattleEnd && ExecuteNextCommand())
            {

            }
        }

        // キュー先頭のコマンドを 1 件実行する（async 版）
        // 同期版との違いは Presenter による実行前後の演出待ちが入る点と、
        // リアクションコマンド実行中はリアクション抑止フラグを立てる点
        // aCt : 演出スキップ用のキャンセルトークン
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

        // キューが空になるかバトルが終了するまで、演出待ちを挟みながら連続実行する（async 版）
        // aCt : 演出スキップ用のキャンセルトークン
        public async ValueTask ExecuteAllCommandAsync(CancellationToken aCt = default)
        {
            while (StateMachine.Current != BattleState.BattleEnd && ActionQueue.Count > 0)
            {
                await ExecuteNextCommandAsync(aCt);
            }
        }

        // 進行中のスキル演出をキャンセルしてスキップする
        public void SkipCurrentPresentation() => PresentationCts?.Cancel();

        // スキル演出を実行し、スキップによるキャンセル例外だけを飲み込む
        // 演出が中断されてもバトル進行自体は止めないためのラッパー
        // aPlay : 待機対象の演出タスク
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

        // リアクショントリガーを発火し、条件を満たしたユニットの反撃コマンドをキュー先頭へ割り込ませる
        // MaxReactionPerEvent に達した時点で打ち切り、リアクション実行中は何もしない
        // aContext : 発生したトリガーと関係ユニットを持つリアクションコンテキスト
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

        // 敵味方双方の、生存しているアクティブメンバーを順に列挙する
        // return : 味方 → 敵の順に並んだ生存ユニット列
        protected virtual IEnumerable<BattleUnit> EnumerateAllAliveUnits()
        {
            foreach (var unit in Context.AllyParty.GetAliveActiveMembers()) yield return unit;
            foreach (var unit in Context.EnemyParty.GetAliveActiveMembers()) yield return unit;
        }

        // ターン経過処理。ターン制ならターンごと、ATB などなら一定秒ごとに呼び出す
        // ターン終了通知 → 両パーティの Tick（状態異常・行動回数・クールダウン更新）→ 勝敗判定 →
        // ターン番号加算 → ターン開始通知、の順に進む
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

        // 現在の行動順を取得する。並び替えロジックは TurnOrderResolver に委譲する
        // return : 行動順に並んだユニットのリスト
        public List<BattleUnit> GetTurnOrder() => TurnOrderResolver.ResolveOrder(Context);

        // 勝敗をチェックし、決着していれば EndBattle を呼んでバトルを終了させる
        // return : 判定結果。コンテキスト未設定または終了済みなら null
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

        // バトルを終了させる。未実行コマンドを破棄し、終了ステートへ遷移して結果を通知する
        // aResult : 確定した戦闘結果
        protected virtual void EndBattle(BattleResult aResult)
        {
            ActionQueue.Clear();
            StateMachine.TransitionTo(BattleState.BattleEnd);
            Log(aResult.Type == BattleResultType.Escaped ? BattleLogType.Escape : BattleLogType.Custom, null, null,
                $"Battle ended: {aResult.Type}");
            OnBattleEnded?.Invoke(aResult);
        }

        // バトルログ出力のヘルパー。TimeProvider でタイムスタンプを付けて Logger へ流す
        // aType : ログ種別
        // aUnit : 行動主体のユニット。無ければ null
        // aTarget : 対象ユニット。無ければ null
        // aDescription : ログ本文
        protected virtual void Log(BattleLogType aType, BattleUnit aUnit, BattleUnit aTarget, string aDescription)
        {
            Logger?.Log(new BattleLogEntry(aType, aUnit, aTarget, aDescription, TimeProvider()));
        }
    }
}
