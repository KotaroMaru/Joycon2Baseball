using UnityEngine;

namespace JoyconBaseball.Phase1.Gameplay
{
    public sealed class PitchingMachine : MonoBehaviour
    {
        /// <summary>
        /// PitchData に基づいてボールを投げる。
        /// strikeZoneCollider でゾーン座標をワールド座標に変換する。
        /// </summary>
        public Phase1Ball ThrowBall(PitchData pitch, GameObject ballPrefab, BoxCollider strikeZoneCollider)
        {
            Vector3? target = null;
            if (strikeZoneCollider != null)
            {
                target = ZoneToWorld(pitch.FinalZone, strikeZoneCollider);
            }
            return ThrowStraightBall(pitch.speedKmh, ballPrefab, target);
        }

        /// <summary>3x3 グリッド座標をストライクゾーン内のワールド座標に変換する。</summary>
        private static Vector3 ZoneToWorld(Vector2Int zone, BoxCollider sz)
        {
            var center    = sz.transform.TransformPoint(sz.center);
            var halfSizeX = sz.size.x * 0.5f;
            var halfSizeY = sz.size.y * 0.5f;

            // zone (0,0)=左下 (2,2)=右上 → -1〜+1 に正規化してから half-size 倍
            var nx = (zone.x - 1) / 1f;  // -1, 0, +1
            var ny = (zone.y - 1) / 1f;

            var right = sz.transform.right;
            var up    = sz.transform.up;

            return center + right * (nx * halfSizeX) + up * (ny * halfSizeY);
        }

        public Phase1Ball ThrowStraightBall(float speedKmh, GameObject ballPrefab, Vector3? targetPosition = null)
        {
            GameObject ballObject;
            if (ballPrefab != null)
            {
                ballObject = Instantiate(ballPrefab, transform.position, Quaternion.identity);
                ballObject.name = ballPrefab.name;
            }
            else
            {
                ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ballObject.name = "Baseball";
                ballObject.transform.position = transform.position;
                ballObject.transform.localScale = Vector3.one * 0.12f;
                ballObject.GetComponent<Renderer>().material.color = Color.white;
            }

            var collider = ballObject.GetComponent<Collider>();
            if (collider == null)
            {
                collider = ballObject.AddComponent<SphereCollider>();
            }

            var rigidbody = ballObject.GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                rigidbody = ballObject.AddComponent<Rigidbody>();
            }

            rigidbody.useGravity = false;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            var ball = ballObject.GetComponent<Phase1Ball>();
            if (ball == null)
            {
                ball = ballObject.AddComponent<Phase1Ball>();
            }

            var direction = targetPosition.HasValue
                ? (targetPosition.Value - transform.position).normalized
                : Vector3.back;
            ball.SetInitialVelocity(direction * (speedKmh / 3.6f));
            return ball;
        }
    }
}
