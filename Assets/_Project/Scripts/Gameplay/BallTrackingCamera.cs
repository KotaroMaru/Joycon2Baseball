using UnityEngine;

namespace JoyconBaseball.Phase1.Gameplay
{
    public sealed class BallTrackingCamera : MonoBehaviour
    {
        private enum State { Idle, Tracking, Returning }

        private const float DefaultMaxRotationSpeed = 100f;
        private const float DefaultFovExpansion = 12f;
        private const float DefaultReturnDuration = 0.6f;

        private float maxRotationSpeed = DefaultMaxRotationSpeed;
        private float fovExpansion = DefaultFovExpansion;
        private float returnDuration = DefaultReturnDuration;

        private Camera targetCamera;
        private State state = State.Idle;

        private Quaternion defaultRotation;
        private float defaultFov;
        private Transform targetBall;

        private float returnElapsed;

        public void Configure(float rotationSpeed, float fovExp, float returnDur)
        {
            maxRotationSpeed = rotationSpeed;
            fovExpansion = fovExp;
            returnDuration = returnDur;
        }

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
        }

        public void StartTracking(Transform ball)
        {
            if (targetCamera == null) return;

            defaultRotation = transform.rotation;
            defaultFov = targetCamera.fieldOfView;
            targetBall = ball;
            state = State.Tracking;
        }

        public void StopTracking()
        {
            if (state == State.Idle) return;
            BeginReturn();
        }

        public void ForceReset()
        {
            state = State.Idle;
            targetBall = null;
            if (targetCamera == null) return;
            transform.rotation = defaultRotation;
            targetCamera.fieldOfView = defaultFov;
        }

        private void LateUpdate()
        {
            if (targetCamera == null) return;

            switch (state)
            {
                case State.Tracking:
                    UpdateTracking();
                    break;
                case State.Returning:
                    UpdateReturning();
                    break;
            }
        }

        private void UpdateTracking()
        {
            // ボールが消えたら復帰開始
            if (targetBall == null)
            {
                BeginReturn();
                return;
            }

            var toBall = targetBall.position - transform.position;

            // ボールが真後ろに飛んだ場合は追従しない
            if (Vector3.Dot(transform.forward, toBall.normalized) < -0.1f)
            {
                BeginReturn();
                return;
            }

            var targetRotation = Quaternion.LookRotation(toBall);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                maxRotationSpeed * Time.deltaTime);

            targetCamera.fieldOfView = Mathf.Lerp(
                targetCamera.fieldOfView,
                defaultFov + fovExpansion,
                Time.deltaTime * 3f);
        }

        private void UpdateReturning()
        {
            returnElapsed += Time.deltaTime;
            var t = Mathf.Clamp01(returnElapsed / returnDuration);
            var smooth = Mathf.SmoothStep(0f, 1f, t);

            transform.rotation = Quaternion.Slerp(transform.rotation, defaultRotation, smooth);
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, defaultFov, smooth);

            if (t >= 1f)
            {
                transform.rotation = defaultRotation;
                targetCamera.fieldOfView = defaultFov;
                state = State.Idle;
                targetBall = null;
            }
        }

        private void BeginReturn()
        {
            state = State.Returning;
            returnElapsed = 0f;
            targetBall = null;
        }
    }
}
