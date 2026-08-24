/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequencePlayback.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief AnimSequenceDefinitionのアニメーションキーを評価・進行するエンジン
 * =====================================*/

using System;
using System.Collections.Generic;
using CustomConsole;
using UnityEngine;

namespace AnimSequencer2D
{
    // Unityのコルーチンに依存しないプレーンC#クラスとし、Tick()で外部から駆動する。
    // ランタイム(AnimSequencePlayer)とエディタ埋め込みプレビューの双方から同じロジックを使い回すための共通実装
    public class AnimSequencePlayback
    {
        private const string LogTag = "AnimSequencer2D";

        // 1回のTick内で末尾到達処理(ループ周回・遷移)を繰り返す上限。長さ0のアニメーション同士が
        // 相互に遷移するような構成で無限ループに陥るのを防ぐ
        private const int MaxEndHandlePerTick = 1000;

        private readonly AnimSequenceDefinition mDefinition;
        private readonly IAnimSequenceHost mHost;
        private readonly IAnimSequenceTimeProvider mTimeProvider;
        // トラックIDごとの基準状態。再生開始時にホストから取得し、相対値の基準として使う
        private readonly Dictionary<string, AnimSequenceTrackState> mBaseStates = new();
        // (トラックID, プロパティ名)ごとのMaterialパラメータ基準値。再生開始時に基準Materialから読み取ってキャッシュする。
        // タプルキーは値型のため、ApplyState側での読み取り(GetValueOrDefault)は毎フレームのGCアロケーションを伴わない
        private readonly Dictionary<(string TrackId, string PropertyName), float> mBaseFloatParams = new();
        private readonly Dictionary<(string TrackId, string PropertyName), Color> mBaseColorParams = new();
        private readonly Dictionary<(string TrackId, string PropertyName), Vector4> mBaseVectorParams = new();
        // エントリ開始時に1回だけ構築する、オブジェクトID→そのエントリでの参照トラックの対応表(毎フレームの走査を避けるため)
        private readonly Dictionary<string, AnimSequenceTrack> mCurrentTracksByObjectId = new();
        // エントリ開始時に解決した、オブジェクトIDごとの表示可否(AnimSequenceObject.DefaultVisible/AnimSequenceTrack.VisibilityOverrideの合成結果)
        private readonly Dictionary<string, bool> mVisibilityByObjectId = new();
        // 直近にホストへ適用した状態(オブジェクトID単位)。次のエントリ開始時に基準値として引き継ぐ。
        // 未再生(このPlaybackで一度も再生していない)オブジェクトは未登録で、その場合はオブジェクトの基準値を使う
        private readonly Dictionary<string, AnimSequenceTrackState> mLastAppliedStates = new();

        private AnimSequenceEntry mCurrentEntry;
        private float mTime;
        // 区間先端ちょうどのイベントも発火するか。再生開始直後とループ周回直後だけtrueにして、t=0のイベントを取りこぼさない
        private bool mFireEventsAtRangeStart;

        // 再生開始時に発火する(アニメーションキー, タグ)
        public event Action<string, string> OnSequenceStarted;
        // 再生終了時に発火する(アニメーションキー, タグ)。Stop()・末尾到達での停止・遷移直前のいずれでも発火する
        public event Action<string, string> OnSequenceCompleted;
        // タイムライン上のイベントキーに到達した際に発火する(イベントキー)
        public event Action<string> OnEventTriggered;

        public bool IsPlaying => mCurrentEntry != null;
        public string CurrentKey => mCurrentEntry?.Key;
        public float CurrentTime => mTime;

        // trueの場合、末尾到達時にEndBehaviorの設定を無視して常にLoopとして扱う(Transitionで別のアニメーションキーへ
        // 切り替わらず、現在のエントリを無限ループし続ける)。エディタプレビューの「SimulateRuntime OFF」モード
        // (選択中のアニメーションだけを確認したい場合)向けの上書き設定。ランタイム側は既定のfalseのまま使う想定
        public bool ForceLoopCurrentEntry { get; set; }

        // aDefinition : 再生対象の定義 / aHost : 評価結果の適用先 / aTimeProvider : デルタタイムの供給元
        public AnimSequencePlayback(AnimSequenceDefinition aDefinition, IAnimSequenceHost aHost, IAnimSequenceTimeProvider aTimeProvider)
        {
            mDefinition = aDefinition;
            mHost = aHost;
            mTimeProvider = aTimeProvider;
        }

        // 指定キーの再生を開始する。再生中の場合は打ち切って切り替える(同一キーでも最初からやり直す)
        // aKey : 再生するアニメーションキー
        public void PlaySequence(string aKey)
        {
            AnimSequenceEntry entry = mDefinition != null ? mDefinition.FindEntry(aKey) : null;

            // 見つからない場合は現在の再生状態を一切変更しない。先に検索してから割り込み処理へ進める
            if (entry == null)
            {
                CustomConsoleLog.Warning(LogTag, $"アニメーションキー「{aKey}」が定義に存在しないため再生を開始しません");
                return;
            }

            if (IsPlaying)
            {
                CompleteCurrent();
            }

            BeginEntry(entry);
            // 長さ0のアニメーションを同フレームで完了させ、t=0のイベントも取りこぼさないためここで0秒進める
            AdvanceInternal(0f);
        }

        public void Stop()
        {
            if (!IsPlaying)
            {
                return;
            }
            CompleteCurrent();
        }

        // 外部(Update / EditorApplication.update)から毎フレーム呼ぶ
        public void Tick()
        {
            if (!IsPlaying)
            {
                return;
            }
            AdvanceInternal(mTimeProvider.GetDeltaTime(mCurrentEntry.TimeMode, mCurrentEntry.PlayWhilePaused));
        }

        // 再生中のアニメーションの時刻を直接指定する(エディタのスクラブ/コマ送り操作用)。
        // イベント発火・EndBehavior処理(Loop/Transition)は行わず、見た目の適用のみ行う
        // aTime : 指定する時刻(秒)。[0, Duration]にクランプする
        public void SetTime(float aTime)
        {
            if (!IsPlaying)
            {
                return;
            }
            mTime = Mathf.Clamp(aTime, 0f, mCurrentEntry.Duration);
            ApplyState(mTime);
        }

        // 再生時刻を進める。末尾到達をwhileループで処理することで、長さ0のアニメーションの即時完了・
        // 大きなデルタタイムでの複数周回・遷移連鎖を再帰なしで一様に扱う
        // aDeltaTime : 進める秒数
        private void AdvanceInternal(float aDeltaTime)
        {
            float remaining = Mathf.Max(0f, aDeltaTime);
            int endHandleCount = 0;

            while (IsPlaying)
            {
                float previousTime = mTime;
                float duration = mCurrentEntry.Duration;

                // 長さ0のアニメーションは進行させず、その場で末尾に到達したものとして扱う
                if (duration > 0f)
                {
                    float nextTime = previousTime + remaining;

                    // 末尾に届かない通常ケース
                    if (nextTime < duration)
                    {
                        mTime = nextTime;
                        FireEventsInRange(previousTime, nextTime, mFireEventsAtRangeStart);
                        mFireEventsAtRangeStart = false;
                        ApplyState(nextTime);
                        return;
                    }

                    remaining = nextTime - duration;
                }
                else
                {
                    remaining = 0f;
                }

                // 末尾に到達した。末尾までのイベントを発火し、末尾の見た目を適用してから終了時の挙動を処理する
                FireEventsInRange(previousTime, duration, mFireEventsAtRangeStart);
                mFireEventsAtRangeStart = false;
                mTime = duration;
                ApplyState(duration);

                HandleEnd();

                endHandleCount++;
                if (endHandleCount >= MaxEndHandlePerTick)
                {
                    CustomConsoleLog.Warning(LogTag,
                        $"1フレーム内での終了処理が{MaxEndHandlePerTick}回に達したため再生を停止します。長さ0のアニメーション同士が循環して遷移していないか確認してください");
                    Stop();
                    return;
                }
            }
        }

        // 末尾到達時の挙動を処理する。継続する場合はmCurrentEntryが継続対象へ更新される
        private void HandleEnd()
        {
            AnimSequenceEndBehavior effectiveBehavior = ForceLoopCurrentEntry ? AnimSequenceEndBehavior.Loop : mCurrentEntry.EndBehavior;
            switch (effectiveBehavior)
            {
                case AnimSequenceEndBehavior.Loop:
                    // ループ境界では開始/終了イベントを発火しない。イベントキーは周回のたびに発火させたいので先端を含める設定に戻す
                    mTime = 0f;
                    mFireEventsAtRangeStart = true;
                    break;

                case AnimSequenceEndBehavior.Transition:
                    AnimSequenceEntry next = mDefinition.FindEntry(mCurrentEntry.TransitionTargetKey);
                    if (next == null)
                    {
                        CustomConsoleLog.Warning(LogTag,
                            $"アニメーション「{mCurrentEntry.Key}」の遷移先キー「{mCurrentEntry.TransitionTargetKey}」が存在しないため、その場で再生を終了します");
                        CompleteCurrent();
                        break;
                    }
                    // 遷移はPlaySequenceを呼んだのと同等に扱う。基準値は遷移直前の最終状態を引き継ぐ(BeginEntry参照)
                    CompleteCurrent();
                    BeginEntry(next);
                    break;

                default: // Stop
                    CompleteCurrent();
                    break;
            }
        }

        // 再生を開始する。基準値の記録・初期状態の適用・開始イベント発火まで行う(進行はしない)
        private void BeginEntry(AnimSequenceEntry aEntry)
        {
            mCurrentEntry = aEntry;
            mTime = 0f;
            mFireEventsAtRangeStart = true;

            // 相対値の基準は「直前の再生の最終状態」。前のアニメーションが終わった位置・姿勢から続けて動かせるようにするため。
            // このPlaybackで一度も再生していないオブジェクト(初回再生時)は、参照先AnimSequenceObjectの基準値
            // (初期配置画面で設定した値)を使う。ループ中はBeginEntryを通らないため周回で基準値がずれることはない
            // IReadOnlyList<T>をforeachすると列挙子がボクシングされGCアロケーションが発生するため、indexループで走査する
            mBaseStates.Clear();
            mBaseFloatParams.Clear();
            mBaseColorParams.Clear();
            mBaseVectorParams.Clear();
            mCurrentTracksByObjectId.Clear();
            mVisibilityByObjectId.Clear();

            // このエントリのトラックをオブジェクトIDでマップ化する(1エントリにつき1オブジェクトにつき1トラックが前提)
            IReadOnlyList<AnimSequenceTrack> tracks = aEntry.Tracks;
            for (int i = 0; i < tracks.Count; i++)
            {
                AnimSequenceTrack track = tracks[i];
                mCurrentTracksByObjectId[track.TrackId] = track;
                if (mDefinition.FindObject(track.TrackId) == null)
                {
                    CustomConsoleLog.Warning(LogTag, $"トラック「{track.TrackId}」が参照するオブジェクトが見つからないため、このトラックの見た目更新をスキップします");
                }
            }

            // トラックの有無に関わらず、定義済みの全オブジェクトを対象に基準状態・表示可否を解決する
            // (オブジェクトは配置画面で常時表示され、トラックはそれをどう時間変化・表示上書きするかを表すため)
            IReadOnlyList<AnimSequenceObject> objects = mDefinition.Objects;
            for (int i = 0; i < objects.Count; i++)
            {
                AnimSequenceObject obj = objects[i];
                AnimSequenceTrackState baseState = obj.ToBaseState();

                // 直前の再生の最終状態があればそれを基準値として引き継ぐ(初回再生時は未登録のためオブジェクトの基準値のまま)。
                // 引き継ぐのはアニメーションで変化する値のみで、インスタンス化フラグ・表示可否はオブジェクト/トラックの
                // 設定からエントリごとに解決し直す(これらは時間変化しない設定値であり、引き継ぐと設定変更が反映されなくなるため)
                if (mLastAppliedStates.TryGetValue(obj.ObjectId, out AnimSequenceTrackState lastState))
                {
                    baseState.AnchoredPosition = lastState.AnchoredPosition;
                    baseState.Scale = lastState.Scale;
                    baseState.Rotation = lastState.Rotation;
                    baseState.Color = lastState.Color;
                    baseState.Sprite = lastState.Sprite;
                    baseState.Material = lastState.Material;
                }
                mBaseStates[obj.ObjectId] = baseState;

                bool hasTrack = mCurrentTracksByObjectId.TryGetValue(obj.ObjectId, out AnimSequenceTrack track);
                mVisibilityByObjectId[obj.ObjectId] = hasTrack ? track.ResolveVisible(obj) : obj.DefaultVisible;

                if (hasTrack)
                {
                    CacheBaseMaterialParams(track, baseState.Material);
                }
            }

            ApplyState(0f);
            OnSequenceStarted?.Invoke(aEntry.Key, aEntry.Tag);
        }

        // aBaseMaterialから、aTrackの各Materialパラメータトラックの基準値(切り替え前・キーフレーム到達前の値)を
        // 読み取ってキャッシュする。基準Materialが無い、またはそのプロパティを持たない場合はキャッシュせず、
        // Evaluate側のGetValueOrDefaultの既定値(0/白/zero)に委ねる
        // aTrack : 対象トラック / aBaseMaterial : このエントリ開始時点の基準Material(前の再生から引き継いだものを含む)
        private void CacheBaseMaterialParams(AnimSequenceTrack aTrack, Material aBaseMaterial)
        {
            Material baseMaterial = aBaseMaterial;
            List<AnimSequenceMaterialParameterTrack> paramTracks = aTrack.MaterialParameterTracks;
            for (int i = 0; i < paramTracks.Count; i++)
            {
                AnimSequenceMaterialParameterTrack paramTrack = paramTracks[i];
                if (baseMaterial == null || !baseMaterial.HasProperty(paramTrack.PropertyName))
                {
                    continue;
                }

                var key = (aTrack.TrackId, paramTrack.PropertyName);
                switch (paramTrack.Type)
                {
                    case MaterialParameterType.Float:
                        mBaseFloatParams[key] = baseMaterial.GetFloat(paramTrack.PropertyName);
                        break;
                    case MaterialParameterType.Color:
                        mBaseColorParams[key] = baseMaterial.GetColor(paramTrack.PropertyName);
                        break;
                    case MaterialParameterType.Vector4:
                        mBaseVectorParams[key] = baseMaterial.GetVector(paramTrack.PropertyName);
                        break;
                }
            }
        }

        private void CompleteCurrent()
        {
            string key = mCurrentEntry.Key;
            string tag = mCurrentEntry.Tag; // mCurrentEntryをnullにする前に退避する
            mCurrentEntry = null;
            OnSequenceCompleted?.Invoke(key, tag);
        }

        // aFromより後 aTo以下 のイベントを発火する。aIncludeFromがtrueならaFromちょうどのイベントも含める
        // (イベントキーはOnValidateで時刻昇順に整列済みのため、この走査順がそのまま発火順になる)
        // 毎フレーム呼ばれるためIReadOnlyList<T>のforeachによるボクシングを避け、indexループで走査する
        private void FireEventsInRange(float aFrom, float aTo, bool aIncludeFrom)
        {
            IReadOnlyList<AnimSequenceEventKey> eventKeys = mCurrentEntry.EventKeys;
            for (int i = 0; i < eventKeys.Count; i++)
            {
                AnimSequenceEventKey eventKey = eventKeys[i];
                bool isAfterFrom = aIncludeFrom ? eventKey.Time >= aFrom : eventKey.Time > aFrom;
                if (isAfterFrom && eventKey.Time <= aTo)
                {
                    OnEventTriggered?.Invoke(eventKey.EventKey);
                }
            }
        }

        // 指定時刻における各トラックの状態を評価し、ホストへ適用する
        // 相対値の合成規則: 位置・回転は基準値へ加算、スケールは基準値へ乗算、色は絶対値、画像は離散切替
        // 毎フレーム呼ばれるためIReadOnlyList<T>のforeachによるボクシングを避け、indexループで走査する
        // aTime : 評価時刻
        private void ApplyState(float aTime)
        {
            IReadOnlyList<AnimSequenceObject> objects = mDefinition.Objects;
            for (int i = 0; i < objects.Count; i++)
            {
                AnimSequenceObject obj = objects[i];

                // BeginEntryで定義済みの全オブジェクト分を登録済みのため通常到達しないが、念のため防御する
                if (!mBaseStates.TryGetValue(obj.ObjectId, out AnimSequenceTrackState baseState))
                {
                    continue;
                }
                bool isVisible = mVisibilityByObjectId.GetValueOrDefault(obj.ObjectId, true);

                if (mCurrentTracksByObjectId.TryGetValue(obj.ObjectId, out AnimSequenceTrack track))
                {
                    var state = new AnimSequenceTrackState
                    {
                        AnchoredPosition = baseState.AnchoredPosition + EvaluateVector2(track.PositionKeyframes, aTime, Vector2.zero),
                        Scale = Vector2.Scale(baseState.Scale, EvaluateVector2(track.ScaleKeyframes, aTime, Vector2.one)),
                        Rotation = baseState.Rotation + EvaluateVector3(track.RotationKeyframes, aTime, Vector3.zero),
                        Color = EvaluateColor(track.ColorKeyframes, aTime, baseState.Color),
                        Sprite = EvaluateSprite(track.SpriteKeyframes, aTime, baseState.Sprite),
                        // baseState.Material/InstantiateMaterialは参照先AnimSequenceObjectの基準値(BeginEntryで設定済み)
                        Material = EvaluateMaterial(track.MaterialKeyframes, aTime, baseState.Material),
                        InstantiateMaterial = baseState.InstantiateMaterial,
                        IsVisible = isVisible,
                    };

                    mLastAppliedStates[obj.ObjectId] = state; // 次のエントリ開始時に基準値として引き継ぐ
                    mHost.ApplyTrackState(obj.ObjectId, state);
                    if (isVisible)
                    {
                        // 非表示中はMaterialパラメータの書き込みも省略する(共有Material本体を書き換えると、
                        // 同じMaterialを使う他の表示中オブジェクトへ意図せず影響するため。Transform/Material本体の
                        // 更新省略はAnimSequencePlayer.ApplyTrackState側で行っており、ここで揃える)
                        ApplyMaterialParams(track, aTime);
                    }
                }
                else
                {
                    // トラックを持たないオブジェクトは基準状態のまま(時間変化なし)。表示可否だけ解決結果を反映する
                    AnimSequenceTrackState state = baseState;
                    state.IsVisible = isVisible;
                    mLastAppliedStates[obj.ObjectId] = state; // 次のエントリ開始時に基準値として引き継ぐ
                    mHost.ApplyTrackState(obj.ObjectId, state);
                }
            }
        }

        // Material切り替え適用後、実際に適用されているMaterial(インスタンス化していればそのコピー)へ
        // 各パラメータトラックの評価値を直接書き込む。パラメータは可変個・可変型のためAnimSequenceTrackStateを経由させず
        // (毎フレームのコレクション確保を避けるため)、ホストから取得したMaterialへ直接SetFloat/SetColor/SetVectorする
        private void ApplyMaterialParams(AnimSequenceTrack aTrack, float aTime)
        {
            List<AnimSequenceMaterialParameterTrack> paramTracks = aTrack.MaterialParameterTracks;
            if (paramTracks.Count == 0)
            {
                return;
            }

            Material activeMaterial = mHost.ResolveActiveMaterial(aTrack.TrackId);
            if (activeMaterial == null)
            {
                return;
            }

            for (int i = 0; i < paramTracks.Count; i++)
            {
                AnimSequenceMaterialParameterTrack paramTrack = paramTracks[i];
                if (!activeMaterial.HasProperty(paramTrack.PropertyName))
                {
                    continue; // 切り替え後のMaterialに同名プロパティが無い場合はスキップする(SPEC.mdのエッジケース)
                }

                var key = (aTrack.TrackId, paramTrack.PropertyName);
                switch (paramTrack.Type)
                {
                    case MaterialParameterType.Float:
                        float baseFloat = mBaseFloatParams.GetValueOrDefault(key);
                        activeMaterial.SetFloat(paramTrack.PropertyName, EvaluateFloat(paramTrack.FloatKeyframes, aTime, baseFloat));
                        break;
                    case MaterialParameterType.Color:
                        Color baseColor = mBaseColorParams.GetValueOrDefault(key, Color.white);
                        activeMaterial.SetColor(paramTrack.PropertyName, EvaluateColor(paramTrack.ColorKeyframes, aTime, baseColor));
                        break;
                    case MaterialParameterType.Vector4:
                        Vector4 baseVector = mBaseVectorParams.GetValueOrDefault(key);
                        activeMaterial.SetVector(paramTrack.PropertyName, EvaluateVector4(paramTrack.Vector4Keyframes, aTime, baseVector));
                        break;
                }
            }
        }

        // 時刻からVector2を評価する(常に線形補間)。先頭に「t=0 / 値=aIdentity」の暗黙キーフレームがあるものとして扱う
        // aKeyframes : 時刻昇順に整列済みのキーフレーム / aTime : 評価時刻 / aIdentity : 暗黙の基準値
        private static Vector2 EvaluateVector2(List<AnimSequenceVector2Keyframe> aKeyframes, float aTime, Vector2 aIdentity)
        {
            if (aKeyframes == null || aKeyframes.Count == 0)
            {
                return aIdentity;
            }

            // 最初のキーフレームより前は、暗黙の基準値から最初のキーフレームへ向けて補間する
            AnimSequenceVector2Keyframe first = aKeyframes[0];
            if (aTime <= first.Time)
            {
                if (first.Time <= 0f)
                {
                    return first.Value;
                }
                return Vector2.LerpUnclamped(aIdentity, first.Value, aTime / first.Time);
            }

            // 最後のキーフレーム以降は最後の値を保持する
            AnimSequenceVector2Keyframe last = aKeyframes[^1];
            if (aTime >= last.Time)
            {
                return last.Value;
            }

            // 該当区間を探し、直前のキーフレームから今回のキーフレームへ線形補間する
            for (int i = 1; i < aKeyframes.Count; i++)
            {
                AnimSequenceVector2Keyframe next = aKeyframes[i];
                if (aTime > next.Time)
                {
                    continue;
                }

                AnimSequenceVector2Keyframe prev = aKeyframes[i - 1];
                float span = next.Time - prev.Time;
                float progress = span <= 0f ? 1f : (aTime - prev.Time) / span;
                return Vector2.LerpUnclamped(prev.Value, next.Value, progress);
            }

            return last.Value;
        }

        // 時刻からVector3を評価する(常に線形補間)。構造はEvaluateVector2と同じ(回転X/Y/Zに使う)
        // aKeyframes : 時刻昇順に整列済みのキーフレーム / aTime : 評価時刻 / aIdentity : 暗黙の基準値
        private static Vector3 EvaluateVector3(List<AnimSequenceVector3Keyframe> aKeyframes, float aTime, Vector3 aIdentity)
        {
            if (aKeyframes == null || aKeyframes.Count == 0)
            {
                return aIdentity;
            }

            AnimSequenceVector3Keyframe first = aKeyframes[0];
            if (aTime <= first.Time)
            {
                if (first.Time <= 0f)
                {
                    return first.Value;
                }
                return Vector3.LerpUnclamped(aIdentity, first.Value, aTime / first.Time);
            }

            AnimSequenceVector3Keyframe last = aKeyframes[^1];
            if (aTime >= last.Time)
            {
                return last.Value;
            }

            for (int i = 1; i < aKeyframes.Count; i++)
            {
                AnimSequenceVector3Keyframe next = aKeyframes[i];
                if (aTime > next.Time)
                {
                    continue;
                }

                AnimSequenceVector3Keyframe prev = aKeyframes[i - 1];
                float span = next.Time - prev.Time;
                float progress = span <= 0f ? 1f : (aTime - prev.Time) / span;
                return Vector3.LerpUnclamped(prev.Value, next.Value, progress);
            }

            return last.Value;
        }

        // 時刻からColorを評価する(常に線形補間)。色のみ絶対値のため、暗黙の基準値には「再生開始時点の実際の色」を使う
        // aKeyframes : 時刻昇順に整列済みのキーフレーム / aTime : 評価時刻 / aBaseColor : 再生開始時点の実際の色
        private static Color EvaluateColor(List<AnimSequenceColorKeyframe> aKeyframes, float aTime, Color aBaseColor)
        {
            if (aKeyframes == null || aKeyframes.Count == 0)
            {
                return aBaseColor;
            }

            AnimSequenceColorKeyframe first = aKeyframes[0];
            if (aTime <= first.Time)
            {
                if (first.Time <= 0f)
                {
                    return first.Value;
                }
                return Color.LerpUnclamped(aBaseColor, first.Value, aTime / first.Time);
            }

            AnimSequenceColorKeyframe last = aKeyframes[^1];
            if (aTime >= last.Time)
            {
                return last.Value;
            }

            for (int i = 1; i < aKeyframes.Count; i++)
            {
                AnimSequenceColorKeyframe next = aKeyframes[i];
                if (aTime > next.Time)
                {
                    continue;
                }

                AnimSequenceColorKeyframe prev = aKeyframes[i - 1];
                float span = next.Time - prev.Time;
                float progress = span <= 0f ? 1f : (aTime - prev.Time) / span;
                return Color.LerpUnclamped(prev.Value, next.Value, progress);
            }

            return last.Value;
        }

        // 画像切り替えは補間せず、その時刻までに到達した最後のキーフレームのスプライトを採用する
        // 最初のキーフレームより前は基準スプライト(再生開始時点の画像)のまま
        private static Sprite EvaluateSprite(List<AnimSequenceSpriteKeyframe> aKeyframes, float aTime, Sprite aBaseSprite)
        {
            Sprite result = aBaseSprite;
            for (int i = 0; i < aKeyframes.Count; i++)
            {
                if (aKeyframes[i].Time > aTime)
                {
                    break;
                }
                result = aKeyframes[i].Sprite;
            }
            return result;
        }

        // Material切り替えは補間せず、その時刻までに到達した最後のキーフレームのMaterialを採用する(EvaluateSpriteと同型)
        // 最初のキーフレームより前は基準Material(トラックに設定した基準Material)のまま
        private static Material EvaluateMaterial(List<AnimSequenceMaterialKeyframe> aKeyframes, float aTime, Material aBaseMaterial)
        {
            Material result = aBaseMaterial;
            for (int i = 0; i < aKeyframes.Count; i++)
            {
                if (aKeyframes[i].Time > aTime)
                {
                    break;
                }
                result = aKeyframes[i].Material;
            }
            return result;
        }

        // 時刻からfloatを評価する(常に線形補間)。構造はEvaluateVector2と同じ(Materialのfloatパラメータに使う)
        // aKeyframes : 時刻昇順に整列済みのキーフレーム / aTime : 評価時刻 / aIdentity : 暗黙の基準値
        private static float EvaluateFloat(List<AnimSequenceFloatKeyframe> aKeyframes, float aTime, float aIdentity)
        {
            if (aKeyframes == null || aKeyframes.Count == 0)
            {
                return aIdentity;
            }

            AnimSequenceFloatKeyframe first = aKeyframes[0];
            if (aTime <= first.Time)
            {
                if (first.Time <= 0f)
                {
                    return first.Value;
                }
                return Mathf.LerpUnclamped(aIdentity, first.Value, aTime / first.Time);
            }

            AnimSequenceFloatKeyframe last = aKeyframes[^1];
            if (aTime >= last.Time)
            {
                return last.Value;
            }

            for (int i = 1; i < aKeyframes.Count; i++)
            {
                AnimSequenceFloatKeyframe next = aKeyframes[i];
                if (aTime > next.Time)
                {
                    continue;
                }

                AnimSequenceFloatKeyframe prev = aKeyframes[i - 1];
                float span = next.Time - prev.Time;
                float progress = span <= 0f ? 1f : (aTime - prev.Time) / span;
                return Mathf.LerpUnclamped(prev.Value, next.Value, progress);
            }

            return last.Value;
        }

        // 時刻からVector4を評価する(常に線形補間)。構造はEvaluateVector2と同じ(Materialのvectorパラメータに使う)
        // aKeyframes : 時刻昇順に整列済みのキーフレーム / aTime : 評価時刻 / aIdentity : 暗黙の基準値
        private static Vector4 EvaluateVector4(List<AnimSequenceVector4Keyframe> aKeyframes, float aTime, Vector4 aIdentity)
        {
            if (aKeyframes == null || aKeyframes.Count == 0)
            {
                return aIdentity;
            }

            AnimSequenceVector4Keyframe first = aKeyframes[0];
            if (aTime <= first.Time)
            {
                if (first.Time <= 0f)
                {
                    return first.Value;
                }
                return Vector4.LerpUnclamped(aIdentity, first.Value, aTime / first.Time);
            }

            AnimSequenceVector4Keyframe last = aKeyframes[^1];
            if (aTime >= last.Time)
            {
                return last.Value;
            }

            for (int i = 1; i < aKeyframes.Count; i++)
            {
                AnimSequenceVector4Keyframe next = aKeyframes[i];
                if (aTime > next.Time)
                {
                    continue;
                }

                AnimSequenceVector4Keyframe prev = aKeyframes[i - 1];
                float span = next.Time - prev.Time;
                float progress = span <= 0f ? 1f : (aTime - prev.Time) / span;
                return Vector4.LerpUnclamped(prev.Value, next.Value, progress);
            }

            return last.Value;
        }
    }
}
