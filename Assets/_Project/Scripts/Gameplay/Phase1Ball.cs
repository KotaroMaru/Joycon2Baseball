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

        private IBallGameController controller;
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

        // 変化球用の連続力（投球中のみ適用）
        private Vector3 continuousForce;

        public bool CanBeHit => !wasHit && !crossedPlate;

        public void Initialize(IBallGameController gameController)
        {
            controller = gameController;
            ballBody = GetComponent<Rigidbody>();
        }

        public void SetInitialVelocity(Vector3 velocity)
        {
            ballBody = GetComponent<Rigidbody>();
            ballBody.linearVelocity = velocity;
        }

        /// <summary>
        /// 変化球の軌道を作るための連続力を設定する。
        /// ヒット前の間だけ FixedUpdate で毎フレーム AddForce される。
        /// </summary>
        public void SetContinuousForce(Vector3 force)
        {
            continuousForce = force;
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

        private void FixedUpdate()
        {
            // 変化球力はヒット前・クロスプレート前のみ適用
            if (!wasHit && !crossedPlate && continuousForce != Vector3.zero)
            {
                ballBody.AddForce(continuousForce, ForceMode.Force);
            }
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
                    controller?.NotifyPitchFinishedWithoutHit(passedThroughStrikeZone);
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
                // StrikeZoneTrigger 名前判定は補助的に残すが、Update内での IsInsideStrikeZone 判定が優先される
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
