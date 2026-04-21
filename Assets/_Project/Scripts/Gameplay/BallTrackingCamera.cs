using UnityEngine;

namespace JoyconBaseball.Phase1.Gameplay
{
    [System.Serializable]
    public struct CameraPreset
    {
        public Vector3 position;
        public Vector3 lookAtTarget;
    }

    public sealed class BallTrackingCamera : MonoBehaviour
    {
        private enum State { Idle, WaitingForExit, SwitchedToPreset }
        private enum PresetIndex { Center = 0, Left = 1, Right = 2 }

        // Inspector から調整可能なパラメータ
        [Header("Presets")]
        public CameraPreset centerPreset;
        public CameraPreset leftPreset;
        public CameraPreset rightPreset;

        [Header("Detection")]
        public Vector3 fieldForwardDirection = new Vector3(-0.707f, 0f, 0.707f);
        public float viewportExitMargin = 0.1f;
        public float directionAngleThreshold = 25f;
        public float waitingTimeOut = 5f;

        private Camera targetCamera;
        private State state = State.Idle;

        private Vector3 defaultPosition;
        private Quaternion defaultRotation;

        private Transform targetBall;
        private PresetIndex selectedPreset;
        private float waitingElapsed;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
        }

        public void Configure(Vector3 forwardDir, float margin, float angleThreshold)
        {
            fieldForwardDirection = forwardDir;
            viewportExitMargin = margin;
            directionAngleThreshold = angleThreshold;
        }

        public void StartTracking(Transform ball, Vector3 hitVelocity)
        {
            if (targetCamera == null || ball == null) return;

            // 真後ろへの打球はスキップ
            var hitDirXZ = new Vector3(hitVelocity.x, 0f, hitVelocity.z).normalized;
            if (Vector3.Dot(transform.forward, hitDirXZ) < -0.1f) return;

            defaultPosition = transform.position;
            defaultRotation = transform.rotation;
            targetBall = ball;
            selectedPreset = SelectPreset(hitVelocity);
            waitingElapsed = 0f;
            state = State.WaitingForExit;
        }

        public void StopTracking()
        {
            if (state == State.Idle) return;
            ReturnToDefault();
        }

        public void ForceReset()
        {
            targetBall = null;
            state = State.Idle;
            if (targetCamera == null) return;
            transform.SetPositionAndRotation(defaultPosition, defaultRotation);
        }

        private void LateUpdate()
        {
            if (targetCamera == null) return;

            switch (state)
            {
                case State.WaitingForExit:
                    UpdateWaiting();
                    break;
            }
        }

        private void UpdateWaiting()
        {
            // ボールが消えたら復帰
            if (targetBall == null)
            {
                ReturnToDefault();
                return;
            }

            waitingElapsed += Time.deltaTime;

            // タイムアウト（内野ゴロ等、視野内で止まる場合）
            if (waitingElapsed >= waitingTimeOut)
            {
                ReturnToDefault();
                return;
            }

            // ボールが視野外に出たら定点カメラに瞬間カット
            if (IsBallOutOfView())
            {
                SwitchToPreset(selectedPreset);
            }
        }

        private bool IsBallOutOfView()
        {
            var vp = targetCamera.WorldToViewportPoint(targetBall.position);

            // カメラ背後
            if (vp.z < 0f) return true;

            return vp.x < viewportExitMargin || vp.x > 1f - viewportExitMargin ||
                   vp.y < viewportExitMargin || vp.y > 1f - viewportExitMargin;
        }

        private void SwitchToPreset(PresetIndex preset)
        {
            var p = GetPreset(preset);
            transform.position = p.position;
            transform.rotation = Quaternion.LookRotation(p.lookAtTarget - p.position);
            state = State.SwitchedToPreset;
        }

        private void ReturnToDefault()
        {
            transform.SetPositionAndRotation(defaultPosition, defaultRotation);
            targetBall = null;
            state = State.Idle;
        }

        private PresetIndex SelectPreset(Vector3 hitVelocity)
        {
            var hitDirXZ = new Vector3(hitVelocity.x, 0f, hitVelocity.z);
            var angle = Vector3.SignedAngle(fieldForwardDirection, hitDirXZ, Vector3.up);

            if (angle > directionAngleThreshold)  return PresetIndex.Left;
            if (angle < -directionAngleThreshold) return PresetIndex.Right;
            return PresetIndex.Center;
        }

        private CameraPreset GetPreset(PresetIndex index)
        {
            return index switch
            {
                PresetIndex.Left  => leftPreset,
                PresetIndex.Right => rightPreset,
                _                 => centerPreset,
            };
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            DrawPresetGizmo(centerPreset, Color.green);
            DrawPresetGizmo(leftPreset, Color.blue);
            DrawPresetGizmo(rightPreset, Color.red);
        }

        private static void DrawPresetGizmo(CameraPreset preset, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawSphere(preset.position, 0.4f);
            Gizmos.DrawLine(preset.position, preset.lookAtTarget);
            Gizmos.DrawWireSphere(preset.lookAtTarget, 0.2f);
        }
#endif
    }
}
