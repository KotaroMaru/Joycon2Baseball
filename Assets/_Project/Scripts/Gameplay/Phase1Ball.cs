using JoyconBaseball.Phase1.Core;
using UnityEngine;

namespace JoyconBaseball.Phase1.Gameplay
{
    public sealed class Phase1Ball : MonoBehaviour
    {
        private const float MaxLifetime = 20f;

        private Phase1GameController controller;
        private Rigidbody ballBody;
        private bool crossedPlate;
        private bool wasHit;
        private bool landingResolved;
        private bool hasFieldResult;
        private HitResult fieldResult;
        private bool passedThroughStrikeZone;
        private float lifetime;

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
            if (!wasHit || landingResolved)
            {
                return;
            }

            if (!collision.gameObject.CompareTag("Ground") && !collision.gameObject.name.Contains("Ground"))
            {
                return;
            }

            landingResolved = true;
            controller.NotifyBallLanded(hasFieldResult ? fieldResult : Phase1HitJudge.Judge(transform.position));
            Destroy(gameObject, 0.05f);
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

                hasFieldResult = true;
                fieldResult = zone.HitResult;
            }
        }
    }
}
