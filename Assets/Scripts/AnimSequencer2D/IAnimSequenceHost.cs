/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IAnimSequenceHost.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief 評価結果の適用先。ランタイム(Image)とエディタプレビュー(IMGUI)を差し替えるためのインターフェース
 * =====================================*/

using UnityEngine;

namespace AnimSequencer2D
{
    public interface IAnimSequenceHost
    {
        // 評価済みの状態を対象へ適用する
        // aTrackId : 対象トラックID / aState : 適用する状態
        void ApplyTrackState(string aTrackId, in AnimSequenceTrackState aState);

        // Materialパラメータの適用先を解決する。ApplyTrackState呼び出し後に使うこと。Material切り替え・
        // インスタンス化(aState.InstantiateMaterial)を考慮した「現在このトラックに実際に適用されているMaterial」を返す
        // aTrackId : 対象トラックID / 戻り値 : 適用中のMaterial(未設定ならnull)
        Material ResolveActiveMaterial(string aTrackId);
    }
}
