using JoyconBaseball.Phase1.Core;
using UnityEngine;

namespace JoyconBaseball.Phase1.Gameplay
{
    public sealed class Phase1Ball : MonoBehaviour
    {
        private const float MaxLifetime = 20f;
        private const float StopVelocityThreshold = 0.4f;  // これ以下になったら停止とみなす (m/s)
        private const float MaxRollTime = 3.5f;               // 着地後この秒数でタイムアウト判定
        private const float LandingDrag = 3f;               // 着地後のリニアドラッグ（大きいほど早く止まる）
        private const float LandingAngularDrag = 3f;        // 着地後のアンギュラードラッグ

        private Phase1GameController controller;
        private Rigidbody ballBody;
        private bool crossedPlate;
        private bool wasHit;
        private bool hasLanded;
        private bool landingResolved;
        private bool hasFieldResult;
        private HitResult fieldResult;
        private bool passedThroughStrikeZone;
        private float lifetime;
        private float timeSinceLanding;

        public bool CanBeHit => !wasHit && !crossedPlate;

        public void Initialize(Phase1GameController gameController)
        {
            controller = gameController;
            ballBody = GetComponent<Rigidbody>();
        }

        public void SetInitialVelocity(Vector3 velocity)
        {
            ballBody = GetComponent<Rigidbody>();
            ballBody.linearVelocity = velocity;
        }

        public void ApplyHit(Vector3 hitVelocity)
        {
            if (wasHit)
            {
                return;
            }

            wasHit = true;
            ballBody.useGravity = true;
            ballBody.linearVelocity = hitVelocity;
        }

        private void Update()
        {
            lifetime += Time.deltaTime;

            if (hasLanded && !landingResolved)
            {
                timeSinceLanding += Time.deltaTime;
                var speed = ballBody.linearVelocity.magnitude;
                var stopped = speed < StopVelocityThreshold;
                var timedOut = timeSinceLanding >= MaxRollTime;
                if (stopped || timedOut)
                {
                    landingResolved = true;
                    controller.NotifyBallLanded(hasFieldResult ? fieldResult : Phase1HitJudge.Judge(transform.position, controller.BatterPosition));
                    Destroy(gameObject, 0.05f);
                }
            }

            if (lifetime > MaxLifetime)
            {
                if (!wasHit && !crossedPlate)
                {
                    crossedPlate = true;
                    controller?.NotifyPitchFinishedWithoutHit(false);
                }
                Destroy(gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!wasHit || landingResolved || hasLanded)
            {
                return;
            }

            if (!collision.gameObject.CompareTag("Ground") && !collision.gameObject.name.Contains("Ground"))
            {
                return;
            }

            // 着地フラグを立て、Dragで転がりをすぐ止める
            // 摩擦はボールのColliderにアサインしたPhysicsMaterialで設定
            hasLanded = true;
            ballBody.linearDamping = LandingDrag;
            ballBody.angularDamping = LandingAngularDrag;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!wasHit && !crossedPlate)
            {
                if (other.gameObject.name == "StrikeZoneTrigger")
                {
                    passedThroughStrikeZone = true;
                }
                else if (other.gameObject.name == "Catcher")
                {
                    crossedPlate = true;
                    controller.NotifyPitchFinishedWithoutHit(passedThroughStrikeZone);
                    Destroy(gameObject, 0.3f);
                }
                return;
            }

            if (wasHit)
            {
                var zone = other.GetComponent<FieldResultZone>();
                if (zone == null)
                {
                    return;
                }

                if (zone.ResolveImmediately && !landingResolved)
                {
                    // ファール壁・ホームラン壁：通過した瞬間に即判定
                    landingResolved = true;
                    controller.NotifyBallLanded(zone.HitResult);
                    Destroy(gameObject, 0.3f);
                    return;
                }

                hasFieldResult = true;
                fieldResult = zone.HitResult;
            }
        }
    }
}
