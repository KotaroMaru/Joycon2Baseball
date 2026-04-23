using UnityEngine;

public class Joycon2ControllerModel : MonoBehaviour
{
    public JoyconDeviceID deviceID;
    public float gyroSensitivity = 0.01f;

    [Tooltip("キャリブレーション（Home/Captureボタン）時にリセットするローカル回転（Euler角）")]
    public Vector3 calibrationEulerAngles = new Vector3(90f, 0f, -90f);

    private void Start()
    {
        // 起動時もキャリブレーションポーズから開始する
        transform.localRotation = Quaternion.Euler(calibrationEulerAngles);
    }

    private void Update()
    {
        if (Joycon2Manager.Instance == null) return;

        bool connected = (deviceID == JoyconDeviceID.Left) ?
            Joycon2Manager.Instance.leftConnected :
            Joycon2Manager.Instance.rightConnected;

        if (!connected) return;

        // 蓄積された回転変位を取得 (ConsumeGyroDelta内でリセットされる)
        Vector3 gyroDelta = Joycon2Manager.Instance.ConsumeGyroDelta(deviceID);

        Vector3 accel = (deviceID == JoyconDeviceID.Left) ?
            Joycon2Manager.Instance.LeftAccel :
            Joycon2Manager.Instance.RightAccel;

        uint buttons = (deviceID == JoyconDeviceID.Left) ?
            Joycon2Manager.Instance.leftJoycon.buttons :
            Joycon2Manager.Instance.rightJoycon.buttons;

        // Apply accumulated rotation (Axis: X=Pitch, Y=Yaw, Z=Roll)
        float rx = gyroDelta.x;
        float ry = gyroDelta.y;
        float rz = gyroDelta.z;

        transform.Rotate(new Vector3(-rx, -ry, rz), Space.Self);

        // Tilt correction using gravity (accel)
        if (accel.sqrMagnitude > 0.8f && accel.sqrMagnitude < 1.2f) {
            // Very simple tilt correction: placeholder
            Vector3 gravity = -accel.normalized;
            // transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(transform.forward, gravity), 0.01f);
        }

        // Reset rotation with Home (0x00100000) or Capture (0x00200000)
        // バットを横向きに構えた状態でボタンを押してキャリブレーション
        if ((buttons & 0x00100000) != 0 || (buttons & 0x00200000) != 0) {
            ResetToCalibrationPose();
        }
    }

    /// <summary>
    /// オブジェクトをキャリブレーションポーズ（デフォルト位置）に戻す。
    /// Home ボタン押下時と同じ挙動。ゲーム側のキャリブレーション処理からも呼び出せる。
    /// </summary>
    public void ResetToCalibrationPose()
    {
        transform.localRotation = Quaternion.Euler(calibrationEulerAngles);
    }
}
