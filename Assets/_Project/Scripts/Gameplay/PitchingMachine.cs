using UnityEngine;

namespace JoyconBaseball.Phase1.Gameplay
{
    public sealed class PitchingMachine : MonoBehaviour
    {
        [Header("Curve Parameters")]
        [Tooltip("カーブの横方向への力 (N)。大きいほど横に曲がる")]
        public float curveLateralForce = 8f;

        [Tooltip("カーブ特有の追加下向き力 (N)。大きいほど鋭く落ちる")]
        public float curveDropForce = 5f;

        [Tooltip("カーブ時に狙いを曲がり方向と逆にずらす補正量 (0〜1, ゾーン幅に対する割合)")]
        public float curveAimBias = 0.25f;

        [Header("Fork Parameters")]
        [Tooltip("フォークの追加下向き力 (N)。大きいほど急激に落ちる")]
        public float forkDropForce = 14f;

        [Tooltip("フォーク時に狙いを上方向にずらす補正量 (m)。落下前提の狙い補正")]
        public float forkAimHeightBias = 0.35f;

        // ストライクゾーン幅の概算（ZoneToWorld の補正計算に使用）
        private const float StrikeZoneHalfWidth = 0.5f;

        /// <summary>
        /// PitchData に基づいてボールをピッチャーマウンドから投げる。
        /// </summary>
        public Phase1Ball ThrowBall(PitchData pitch, GameObject ballPrefab, BoxCollider strikeZoneCollider)
        {
            Vector3? target = strikeZoneCollider != null ? CalcAimTarget(pitch, strikeZoneCollider) : (Vector3?)null;
            var ball = SpawnBallObject(transform.position, ballPrefab);
            var direction = target.HasValue ? (target.Value - transform.position).normalized : Vector3.back;
            ball.SetInitialVelocity(direction * (pitch.speedKmh / 3.6f));
            ApplyPitchForce(ball, pitch, direction);
            return ball;
        }

        /// <summary>
        /// 任意のスポーン位置からボールを投げる（ピッチャーアームの手先など）。
        /// </summary>
        public Phase1Ball ThrowBallFrom(Vector3 spawnPosition, PitchData pitch, GameObject ballPrefab, BoxCollider strikeZoneCollider)
        {
            Vector3? target = strikeZoneCollider != null ? CalcAimTarget(pitch, strikeZoneCollider) : (Vector3?)null;
            var direction = target.HasValue ? (target.Value - spawnPosition).normalized : Vector3.back;
            var ball = SpawnBallObject(spawnPosition, ballPrefab);
            ball.SetInitialVelocity(direction * (pitch.speedKmh / 3.6f));
            ApplyPitchForce(ball, pitch, direction);
            return ball;
        }

        public Phase1Ball ThrowStraightBall(float speedKmh, GameObject ballPrefab, Vector3? targetPosition = null)
        {
            var ball = SpawnBallObject(transform.position, ballPrefab);
            var direction = targetPosition.HasValue ? (targetPosition.Value - transform.position).normalized : Vector3.back;
            ball.SetInitialVelocity(direction * (speedKmh / 3.6f));
            return ball;
        }

        /// <summary>
        /// 球種に応じた「狙い位置」を計算する。
        ///
        /// ストレート  : FinalZone（現状通り）
        /// カーブ      : targetZone を基準に、曲がり方向と逆にずらした位置
        ///              （物理力で曲がるので、発射時点では逆側を狙う）
        ///              ずらし量は curveAmount でスケール
        /// フォーク    : targetZone を基準に、Y を forkAimHeightBias だけ高くした位置
        ///              （重力・下向き力で落ちるので、高めから入る）
        /// </summary>
        private Vector3 CalcAimTarget(PitchData pitch, BoxCollider strikeZoneCollider)
        {
            switch (pitch.pitchType)
            {
                case PitchType.Curve:
                {
                    var baseTarget = ZoneToWorld(pitch.targetZone, strikeZoneCollider);
                    var halfW = strikeZoneCollider.size.x * 0.5f;
                    // curveDir=+1 で右曲がり → 左を狙って補正、curveAmount でスケール
                    baseTarget += strikeZoneCollider.transform.right
                                  * (-pitch.curveDir * curveAimBias * halfW * pitch.curveAmount);
                    return baseTarget;
                }
                case PitchType.Fork:
                {
                    var baseTarget = ZoneToWorld(pitch.targetZone, strikeZoneCollider);
                    baseTarget += Vector3.up * forkAimHeightBias;
                    return baseTarget;
                }
                case PitchType.CurveFork:
                {
                    var baseTarget = ZoneToWorld(pitch.targetZone, strikeZoneCollider);
                    var halfW = strikeZoneCollider.size.x * 0.5f;
                    baseTarget += strikeZoneCollider.transform.right
                                  * (-pitch.curveDir * curveAimBias * halfW * pitch.curveAmount);
                    baseTarget += Vector3.up * forkAimHeightBias;
                    return baseTarget;
                }
                default:
                    return ZoneToWorld(pitch.FinalZone, strikeZoneCollider);
            }
        }

        /// <summary>
        /// 球種に応じた連続力を Phase1Ball に設定する。
        ///
        /// カーブ: 横(Magnus・飛行方向に対して垂直) + 下(ドロップ)。横力は curveAmount でスケール。
        /// フォーク: 下(急落下)
        /// ストレート: なし
        /// </summary>
        private void ApplyPitchForce(Phase1Ball ball, PitchData pitch, Vector3 flyDirection)
        {
            // 飛行方向に対して水平垂直な「右」方向（フィールド向きに依存しない）
            var lateral = Vector3.Cross(flyDirection, Vector3.up).normalized;

            Vector3 force = Vector3.zero;

            switch (pitch.pitchType)
            {
                case PitchType.Curve:
                    // curveDir=+1 → lateral 方向（右）へ曲がる、curveAmount でスケール
                    force = lateral * (pitch.curveDir * curveLateralForce * pitch.curveAmount)
                          + Vector3.down * (curveDropForce * Mathf.Max(pitch.curveAmount, 0.3f));
                    break;

                case PitchType.Fork:
                    force = Vector3.down * forkDropForce;
                    break;

                case PitchType.CurveFork:
                    force = lateral * (pitch.curveDir * curveLateralForce * pitch.curveAmount)
                          + Vector3.down * (curveDropForce * Mathf.Max(pitch.curveAmount, 0.3f) + forkDropForce * 0.5f);
                    break;
            }

            if (force != Vector3.zero)
                ball.SetContinuousForce(force);
        }

        /// <summary>3x3 グリッド座標をストライクゾーン内のワールド座標に変換する。</summary>
        public static Vector3 ZoneToWorld(Vector2Int zone, BoxCollider sz)
        {
            var center = sz.transform.TransformPoint(sz.center);
            // lossyScale を掛けてワールド空間での実際のサイズを使う
            var halfSizeX = sz.size.x * sz.transform.lossyScale.x * 0.5f;
            var halfSizeY = sz.size.y * sz.transform.lossyScale.y * 0.5f;

            // 境界線(1.0)ではなく、各マスの中心(0.66)を狙うように調整
            var nx = (zone.x - 1) * 0.66f;
            var ny = (zone.y - 1) * 0.66f;
            return center + sz.transform.right * (nx * halfSizeX) + sz.transform.up * (ny * halfSizeY);
        }

        private Phase1Ball SpawnBallObject(Vector3 position, GameObject ballPrefab)
        {
            GameObject ballObject;
            if (ballPrefab != null)
            {
                ballObject = Instantiate(ballPrefab, position, Quaternion.identity);
                ballObject.name = ballPrefab.name;
            }
            else
            {
                ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ballObject.name = "Baseball";
                ballObject.transform.position = position;
                ballObject.transform.localScale = Vector3.one * 0.12f;
                ballObject.GetComponent<Renderer>().material.color = Color.white;
            }

            if (ballObject.GetComponent<Collider>() == null)
                ballObject.AddComponent<SphereCollider>();

            var rb = ballObject.GetComponent<Rigidbody>() ?? ballObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            return ballObject.GetComponent<Phase1Ball>() ?? ballObject.AddComponent<Phase1Ball>();
        }
    }
}
