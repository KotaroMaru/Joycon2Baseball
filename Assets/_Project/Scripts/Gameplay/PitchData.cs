using UnityEngine;

namespace JoyconBaseball.Phase1.Gameplay
{
    public enum PitchType { Straight, Curve, Fork, CurveFork }

    public struct PitchData
    {
        /// <summary>3x3 グリッド座標。(0,0)=左下、(1,1)=中央、(2,2)=右上</summary>
        public Vector2Int targetZone;

        public PitchType pitchType;

        /// <summary>カーブ方向。-1=左、0=なし、+1=右</summary>
        public int curveDir;

        /// <summary>振り下ろし速度から算出した球速 (km/h)</summary>
        public float speedKmh;

        public static PitchData Default => new PitchData
        {
            targetZone = new Vector2Int(1, 1),
            pitchType  = PitchType.Straight,
            curveDir   = 0,
            speedKmh   = 120f,
        };

        /// <summary>カーブ/フォークのオフセットを適用した最終到達ゾーン</summary>
        public Vector2Int FinalZone
        {
            get
            {
                var x = targetZone.x;
                var y = targetZone.y;

                switch (pitchType)
                {
                    case PitchType.Curve:
                        x += curveDir;
                        break;
                    case PitchType.Fork:
                        y -= 1;
                        break;
                    case PitchType.CurveFork:
                        x += curveDir;
                        y -= 1;
                        break;
                }

                // ストライクゾーン外（-1〜3）も許容（ボール球）
                return new Vector2Int(x, y);
            }
        }
    }
}
