/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AnimSequencePlayer.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief AnimSequenceDefinitionの内容を元に対象UImageの見た目を実際に更新するプレイヤー
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;
using UnityEngine.UI;

namespace AnimSequencer2D
{
    public class AnimSequencePlayer : MonoBehaviour, IAnimSequenceHost
    {
        [Label("シーケンス定義")]
        [SerializeField] private AnimSequenceDefinition mSequenceDefinition;

        private AnimSequencePlayback mPlayback;
        // トラックIDごとにランタイム自動生成したImageのキャッシュ。同一トラックIDへの複数回の要求で多重生成しないためのもの
        private readonly Dictionary<string, Image> mGeneratedImages = new();
        // トラックIDごとに、直近ApplyTrackStateで実際に適用したMaterial(インスタンス化していればそのコピー)
        private readonly Dictionary<string, Material> mActiveMaterials = new();
        // トラックIDごとに、直近インスタンス化の元にしたMaterial(切り替え検出用。インスタンス化していない場合は未登録)
        private readonly Dictionary<string, Material> mInstantiatedFrom = new();

        // 他コンポーネントのAwake()からの参照順序に依存しないよう遅延生成する
        private AnimSequencePlayback Playback => mPlayback ??=
            new AnimSequencePlayback(mSequenceDefinition, this, new AnimSequenceRuntimeTimeProvider());

        // 再生開始時に発火する(アニメーションキー, タグ)
        public event Action<string, string> OnSequenceStarted { add => Playback.OnSequenceStarted += value; remove => Playback.OnSequenceStarted -= value; }
        // 再生終了時に発火する(アニメーションキー, タグ)
        public event Action<string, string> OnSequenceCompleted { add => Playback.OnSequenceCompleted += value; remove => Playback.OnSequenceCompleted -= value; }
        // タイムライン上のイベントキーに到達した際に発火する(イベントキー)
        public event Action<string> OnEventTriggered { add => Playback.OnEventTriggered += value; remove => Playback.OnEventTriggered -= value; }

        public bool IsPlaying => Playback.IsPlaying;

        // 指定キーのアニメーション再生を開始する。再生中の場合は打ち切って切り替える
        // aKey : 再生するアニメーションキー
        public void PlaySequence(string aKey)
        {
            enabled = true;
            Playback.PlaySequence(aKey);
        }

        public void Stop() => Playback.Stop();

        private void Update()
        {
            Playback.Tick();

            // 再生が終わったらUpdateを止める(PlaySequence側で再開する)
            if (!Playback.IsPlaying)
            {
                enabled = false;
            }
        }

        void IAnimSequenceHost.ApplyTrackState(string aTrackId, in AnimSequenceTrackState aState)
        {
            Image image = ResolveOrCreateImage(aTrackId);

            if (image.enabled != aState.IsVisible)
            {
                image.enabled = aState.IsVisible;
            }
            if (!aState.IsVisible)
            {
                return; // 非表示中はTransform/Material更新を省略する(再表示時はApplyTrackStateが改めて最新値を渡す)
            }

            RectTransform rectTransform = image.rectTransform;

            // キーフレームが無いチャンネルは毎フレーム同じ値が来る(最後のキーフレーム到達後もそのまま)ため、
            // 値が変化していない場合はRectTransformへの再代入自体を避けてUI再構築のダーティフラグを立てないようにする
            if (rectTransform.anchoredPosition != aState.AnchoredPosition)
            {
                rectTransform.anchoredPosition = aState.AnchoredPosition;
            }

            // Zスケールは2D用途では扱わないため元の値を維持する
            var scale = new Vector3(aState.Scale.x, aState.Scale.y, rectTransform.localScale.z);
            if (rectTransform.localScale != scale)
            {
                rectTransform.localScale = scale;
            }

            Quaternion rotation = Quaternion.Euler(aState.Rotation);
            if (rectTransform.localRotation != rotation)
            {
                rectTransform.localRotation = rotation;
            }

            image.color = aState.Color; // Graphic.colorは内部で同値チェック済みのためそのまま代入してよい

            // スプライト差し替えはCanvasの再構築を伴うため、変化した時だけ代入する
            if (image.sprite != aState.Sprite)
            {
                image.sprite = aState.Sprite;
            }

            ApplyMaterialSwitch(aTrackId, image, aState);
        }

        // Material切り替えを適用する。インスタンス化が有効なトラックは、切り替え元が変わった時だけ
        // new Material(...)でコピーを作り直す(毎フレーム同じMaterialが指定される通常フレームでは再生成しない)
        private void ApplyMaterialSwitch(string aTrackId, Image aImage, in AnimSequenceTrackState aState)
        {
            if (aState.Material == null)
            {
                return;
            }

            if (aState.InstantiateMaterial)
            {
                if (!mInstantiatedFrom.TryGetValue(aTrackId, out Material source) || source != aState.Material)
                {
                    DestroyPreviousInstanceIfAny(aTrackId);
                    var instance = new Material(aState.Material);
                    aImage.material = instance;
                    mInstantiatedFrom[aTrackId] = aState.Material;
                    mActiveMaterials[aTrackId] = instance;
                }
            }
            else if (aImage.material != aState.Material)
            {
                DestroyPreviousInstanceIfAny(aTrackId);
                aImage.material = aState.Material;
                mInstantiatedFrom.Remove(aTrackId);
                mActiveMaterials[aTrackId] = aState.Material;
            }
        }

        // mActiveMaterials[aTrackId]が直近インスタンス化したコピーであれば破棄する(共有アセットそのものの場合は何もしない)。
        // 破棄しないとランタイム生成したMaterialのネイティブリソース解放が遅延するため、切り替え・インスタンス化解除の直前に呼ぶ
        private void DestroyPreviousInstanceIfAny(string aTrackId)
        {
            if (mInstantiatedFrom.ContainsKey(aTrackId) && mActiveMaterials.TryGetValue(aTrackId, out Material previous) && previous != null)
            {
                Destroy(previous);
            }
        }

        Material IAnimSequenceHost.ResolveActiveMaterial(string aTrackId) => mActiveMaterials.GetValueOrDefault(aTrackId);

        // トラックIDに対応するImageを解決する。未生成であれば自身の子としてランタイム生成し、以後使い回す
        // aTrackId : 解決するトラックID
        private Image ResolveOrCreateImage(string aTrackId)
        {
            if (mGeneratedImages.TryGetValue(aTrackId, out Image image) && image != null)
            {
                return image;
            }

            var go = new GameObject($"Track_{aTrackId}", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            image = go.AddComponent<Image>();
            // AddComponent<Image>()はUnity既定のUIスプライトを自動で設定するため、
            // 「キーフレームが無ければ何も表示しない」というエディタプレビューと同じ挙動に合わせて明示的にクリアする
            image.sprite = null;

            mGeneratedImages[aTrackId] = image;
            return image;
        }
    }
}
