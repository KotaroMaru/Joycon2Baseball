using UnityEngine;

namespace JoyconBaseball.Phase1.Gameplay
{
    public enum HitResult
    {
        Out,
        Foul,
        Single,
        Double,
        HomeRun,
        Triple   // シリアライズ互換のため末尾に追加（HomeRun=4を変えない）
    }

    public static class Phase1HitJudge
    {
        private const float OutThreshold    = 0f;
        private const float SingleThreshold = 28f;
        private const float DoubleThreshold = 38f;
        private const float TripleThreshold = 50f;
        // HomeRun はFieldResultZoneの壁トリガーで即判定するが、
        // フォールバックとしてTripleThreshold以上をHomeRunとする

        /// <summary>
        /// 打球の着地座標とバッター座標からヒット結果を判定する。
        /// FieldResultZoneが設定されていない場合のフォールバック用。
        /// </summary>
        public static HitResult Judge(Vector3 landingPosition, Vector3 batterPosition)
        {
            var dx = landingPosition.x - batterPosition.x;
            var dz = landingPosition.z - batterPosition.z;
            var distance = Mathf.Sqrt(dx * dx + dz * dz);

            if (distance >= TripleThreshold) return HitResult.HomeRun;
            if (distance >= DoubleThreshold) return HitResult.Triple;
            if (distance >= SingleThreshold) return HitResult.Double;
            if (distance >= OutThreshold)    return HitResult.Single;
            return HitResult.Out;
        }
    }
}
