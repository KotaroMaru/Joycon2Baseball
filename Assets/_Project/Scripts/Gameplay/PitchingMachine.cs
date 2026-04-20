using UnityEngine;

namespace JoyconBaseball.Phase1.Gameplay
{
    public sealed class PitchingMachine : MonoBehaviour
    {
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
