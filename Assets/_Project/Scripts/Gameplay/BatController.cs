using UnityEngine;
using JoyconBaseball.Phase1.Core;

namespace JoyconBaseball.Phase1.Gameplay
{
    public sealed class BatController : MonoBehaviour
    {
        private const float JoyconSwingReleaseRatio = 0.45f;

        private Phase1GameController controller;
        private Joycon2Bridge joyconBridge;

        private float joyconPeakSwingAcceleration;
        private bool swinging;
        private bool useJoyconSwingInput;

        private float joyconSwingThreshold = 1.35f;

        public void Initialize(Phase1GameController gameController, Transform pivot)
        {
            controller = gameController;
            joyconBridge ??= new Joycon2Bridge();

            var batBody = GetComponent<Rigidbody>();
            if (batBody == null)
            {
                batBody = gameObject.AddComponent<Rigidbody>();
            }

            batBody.isKinematic = true;
            batBody.useGravity = false;

            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = false;
            }
        }

        public void ConfigureInput(bool enableJoyconGyroInput, float swingThreshold)
        {
            useJoyconSwingInput = enableJoyconGyroInput;
            joyconSwingThreshold = Mathf.Max(0.1f, swingThreshold);
        }

        private void Update()
        {
            if (ShouldUseJoyconSwingInput())
            {
                HandleJoyconSwing();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            var ball = collision.gameObject.GetComponent<Phase1Ball>();
            if (ball == null || !ball.CanBeHit)
            {
                return;
            }

            var contactPower = ShouldUseJoyconSwingInput()
                ? Mathf.Clamp01(Mathf.InverseLerp(joyconSwingThreshold, joyconSwingThreshold * 3.25f, joyconPeakSwingAcceleration))
                : 0.65f;

            var batEulerX = NormalizeAngle(transform.eulerAngles.x);
            var batEulerY = NormalizeAngle(transform.eulerAngles.y);
            var verticalInput = Mathf.Clamp(batEulerX, -25f, 25f) / -25f;
            var horizontalInput = Mathf.Clamp(batEulerY, -30f, 30f) / 30f;

            var hitVelocity = controller.BuildHitVelocity(contactPower, verticalInput, horizontalInput);
            controller.NotifyBallHit(hitVelocity);
        }

        private bool ShouldUseJoyconSwingInput()
        {
            return useJoyconSwingInput &&
                   joyconBridge != null &&
                   joyconBridge.IsAvailable &&
                   joyconBridge.RightConnected;
        }

        private void HandleJoyconSwing()
        {
            var linearAcceleration = joyconBridge.GetRightLinearAcceleration(transform.rotation);
            var accelMagnitude = linearAcceleration.magnitude;

            if (!swinging && accelMagnitude >= joyconSwingThreshold)
            {
                swinging = true;
                joyconPeakSwingAcceleration = accelMagnitude;
            }
            else if (swinging)
            {
                joyconPeakSwingAcceleration = Mathf.Max(joyconPeakSwingAcceleration, accelMagnitude);
                if (accelMagnitude <= joyconSwingThreshold * JoyconSwingReleaseRatio)
                {
                    swinging = false;
                }
            }
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
