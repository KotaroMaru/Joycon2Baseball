using UnityEngine;
using JoyconBaseball.Phase1.Core;

namespace JoyconBaseball.Phase1.Gameplay
{
    public sealed class BatController : MonoBehaviour
    {
        private const float SwingWindowSeconds = 0.6f;
        private const float BaselineSmoothTime = 1.0f;  // ベースライン追従の時定数（秒）

        private IBallGameController controller;
        private Joycon2Bridge joyconBridge;

        private float joyconPeakSwingAcceleration;
        private bool swinging;
        private float swingTimer;
        private float accelBaseline;
        private bool baselineInitialized;
        private bool useJoyconSwingInput;

        private float joyconSwingThreshold = 2.0f;  // ベースラインからの突出量の閾値

        public void Initialize(IBallGameController gameController, Transform pivot)
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

        private const float Restitution = 0.4f;
        private const float MaxSwingSpeed = 34f;

        private void OnCollisionEnter(Collision collision)
        {
            var ball = collision.gameObject.GetComponent<Phase1Ball>();
            if (ball == null || !ball.CanBeHit)
            {
                return;
            }

            if (controller == null)
            {
                Debug.LogError($"[Bat] controller が null！このBatController({gameObject.name})はInitialize未呼び出しです。");
                return;
            }

            // 1. 衝突法線 = バット面がボールを押し出す方向（打球方向の基底）
            var hitNormal = collision.contacts[0].normal;

            // 2. 入射ピッチ速度のうち、バット面に垂直な成分（跳ね返りに寄与）
            var ballRigidbody = collision.rigidbody;
            var incomingSpeed = ballRigidbody != null
                ? Mathf.Max(0f, Vector3.Dot(ballRigidbody.linearVelocity, -hitNormal))
                : 0f;

            // 3. JoyCon スイング速度（ピーク加速度 → m/s に換算）
            // joyconPeakSwingAcceleration はベースラインからの差分値
            // 閾値(2.0)〜閾値×5倍(10.0)の範囲でMaxSwingSpeedにマッピング
            var swingSpeed = ShouldUseJoyconSwingInput()
                ? Mathf.Lerp(0f, MaxSwingSpeed, Mathf.InverseLerp(joyconSwingThreshold, joyconSwingThreshold * 5f, joyconPeakSwingAcceleration))
                : 0f;

            // 4. 打球速度 = (スイング速度 + ピッチ反発) × 法線方向
            var hitSpeed = swingSpeed + incomingSpeed * Restitution;
            var hitVelocity = hitNormal * hitSpeed;

            controller.NotifyBallHit(hitVelocity);
            joyconPeakSwingAcceleration = 0f;
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
            // 生の加速度を使用（rotation精度に依存しない）
            var accelMagnitude = joyconBridge.RightAccel.magnitude;

            // ベースライン初期化
            if (!baselineInitialized)
            {
                accelBaseline = accelMagnitude;
                baselineInitialized = true;
            }

            // ゆっくりベースラインを追従（重力・ドリフトを吸収）
            if (!swinging)
            {
                accelBaseline = Mathf.Lerp(accelBaseline, accelMagnitude, Time.deltaTime / BaselineSmoothTime);
            }

            var accelDelta = accelMagnitude - accelBaseline;

            if (!swinging && accelDelta >= joyconSwingThreshold)
            {
                swinging = true;
                swingTimer = 0f;
                joyconPeakSwingAcceleration = accelDelta;
                controller?.NotifySwingStarted();
                Debug.Log($"[Swing] スイング開始: delta={accelDelta:F2}, baseline={accelBaseline:F2}, raw={accelMagnitude:F2}");
            }
            else if (swinging)
            {
                swingTimer += Time.deltaTime;
                joyconPeakSwingAcceleration = Mathf.Max(joyconPeakSwingAcceleration, accelDelta);

                if (swingTimer >= SwingWindowSeconds)
                {
                    swinging = false;
                    Debug.Log($"[Swing] スイング終了: peak={joyconPeakSwingAcceleration:F2}");
                    joyconPeakSwingAcceleration = 0f;
                }
            }
        }
    }
}
