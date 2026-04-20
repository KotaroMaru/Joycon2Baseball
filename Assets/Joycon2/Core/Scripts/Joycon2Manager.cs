using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using AOT;

public class Joycon2Manager : MonoBehaviour
{
    public static Joycon2Manager Instance { get; private set; }

    [Header("Joycon L")]
    public bool leftConnected = false;
    public string pressedButtonsL;
    public JoyconData leftJoycon;
    [Header("Joycon L - Readable")]
    public Vector2 leftStickValue;
    public Vector3 leftAccelValue;
    public Vector3 leftGyroValue;
    public Vector2 leftMouseValue;

    [Header("Joycon R")]
    public bool rightConnected = false;
    public string pressedButtonsR;
    public JoyconData rightJoycon;
    [Header("Joycon R - Readable")]
    public Vector2 rightStickValue;
    public Vector3 rightAccelValue;
    public Vector3 rightGyroValue;
    public Vector2 rightMouseValue;

    [Header("Internal - Do not edit")]
    private Vector3 gyroDeltaL;
    private Vector3 gyroDeltaR;

    [Header("Calibration")]
    public float stickDivisor = 1250f; 
    public float stickDeadzone = 0.05f;
    public float gyroMultiplier = 1.0f; 
    public float mouseSensitivity = 1.0f;
 // 回転が弱い場合にインスペクターで調整

    [Header("Status")]
    public bool isScanning = false;
    public int packetCount = 0;

    private double lastPacketTimeL = -1;
    private double lastPacketTimeR = -1;
    private const float UnityToJoyconGyro = 360.0f / 4800.0f; // 48000から4800に修正 (10倍強める)

    // プロパティも維持（コードからのアクセス用）
    public Vector3 LeftAccel => leftAccelValue;
    public Vector3 LeftGyro => leftGyroValue;
    public Vector2 LeftStick => leftStickValue;

    public Vector3 RightAccel => rightAccelValue;
    public Vector3 RightGyro => rightGyroValue;
    public Vector2 RightStick => rightStickValue;

    // キャリブレーションオフセット（センサー軸とUnity軸のズレを補正）
    private Quaternion calibrationOffsetR = Quaternion.identity;

    // 横持ちなど任意のポーズでキャリブレーションする
    // batWorldRotation: キャリブレーション時のバット（BatController）のワールド回転
    public void CalibrateRight(Quaternion batWorldRotation)
    {
        if (rightAccelValue.sqrMagnitude < 0.001f)
        {
            Debug.LogWarning("[Joycon2] CalibrateRight: 加速度データが無効です。Joy-Conが接続されているか確認してください。");
            return;
        }

        // キャリブレーションポーズ時、センサーは重力のみを測定しているはず
        Vector3 measuredGravity = rightAccelValue.normalized;
        // Unity座標系でのそのポーズにおける重力方向（ローカル空間）
        Vector3 expectedGravity = (Quaternion.Inverse(batWorldRotation) * Vector3.up).normalized;
        // センサー軸をUnity軸に合わせるオフセットを算出
        calibrationOffsetR = Quaternion.FromToRotation(measuredGravity, expectedGravity);
        Debug.Log($"[Joycon2] キャリブレーション完了。オフセット: {calibrationOffsetR.eulerAngles}");
    }

    // 重力を除去した線形加速度を取得する
    public Vector3 GetLinearAcceleration(JoyconDeviceID id, Quaternion currentRotation)
    {
        Vector3 rawAccel = (id == JoyconDeviceID.Left) ? leftAccelValue : rightAccelValue;

        // キャリブレーションオフセットでセンサー軸をUnity軸に補正
        Quaternion offset = (id == JoyconDeviceID.Right) ? calibrationOffsetR : Quaternion.identity;
        Vector3 correctedAccel = offset * rawAccel;

        // 補正済み加速度から重力成分を除去
        Vector3 gravityInLocalSpace = Quaternion.Inverse(currentRotation) * Vector3.up;
        Vector3 linearAccel = correctedAccel - gravityInLocalSpace;

        return linearAccel;
    }

    public event Action<JoyconData> OnDataReceived;

    private static Joycon2Manager s_instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        s_instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Joycon2Native.UnitySetLogCallback(OnNativeLog);
        Joycon2Native.UnitySetDataCallback(OnNativeDataReceived);
        StartScan();
    }

    private void OnDestroy()
    {
        StopScan();
        Joycon2Native.UnitySetDataCallback(null);
        Joycon2Native.UnitySetLogCallback(null);
    }

    public void StartScan()
    {
        isScanning = true;
        Joycon2Native.UnityStartScan();
    }

    public void StopScan()
    {
        isScanning = false;
        Joycon2Native.UnityStopScan();
    }

    // 蓄積された回転量を取得してリセットする
    public Vector3 ConsumeGyroDelta(JoyconDeviceID id)
    {
        if (id == JoyconDeviceID.Left)
        {
            Vector3 delta = gyroDeltaL;
            gyroDeltaL = Vector3.zero;
            return delta;
        }
        else
        {
            Vector3 delta = gyroDeltaR;
            gyroDeltaR = Vector3.zero;
            return delta;
        }
    }

    [MonoPInvokeCallback(typeof(Joycon2Native.JoyconLogCallback))]
    private static void OnNativeLog(string message)
    {
        Debug.Log(message);
    }

    [MonoPInvokeCallback(typeof(Joycon2Native.JoyconDataCallback))]
    private static void OnNativeDataReceived(JoyconData data)
    {
        if (s_instance != null)
        {
            s_instance.packetCount++;
            double currentTime = (DateTime.UtcNow.Ticks / 10000000.0); // 秒単位の現在時刻

            if (data.deviceID == 0)
            {
                s_instance.leftJoycon = data;
                s_instance.leftConnected = true;
                s_instance.pressedButtonsL = ((JoyconButtons)data.buttons).ToString();
                
                float sx = data.stickX / s_instance.stickDivisor;
                float sy = data.stickY / s_instance.stickDivisor;
                if (Mathf.Abs(sx) < s_instance.stickDeadzone) sx = 0;
                if (Mathf.Abs(sy) < s_instance.stickDeadzone) sy = 0;
                s_instance.leftStickValue = new Vector2(Mathf.Clamp(sx, -1f, 1f), Mathf.Clamp(sy, -1f, 1f));

                s_instance.leftAccelValue = new Vector3(data.accelX, data.accelY, data.accelZ) / 4096.0f;
                s_instance.leftGyroValue = new Vector3(data.gyroX, data.gyroY, data.gyroZ) * (360.0f / 48000.0f); // 表示用はリファレンス通り
                s_instance.leftMouseValue = new Vector2(data.mouseX, data.mouseY) * s_instance.mouseSensitivity;
                
                // --- 精密積分 ---
                if (s_instance.lastPacketTimeL > 0) {
                    float dt = (float)(currentTime - s_instance.lastPacketTimeL);
                    Vector3 gyroRate = new Vector3(data.gyroX, data.gyroY, data.gyroZ) * UnityToJoyconGyro;
                    s_instance.gyroDeltaL += gyroRate * dt * s_instance.gyroMultiplier;
                }
                s_instance.lastPacketTimeL = currentTime;
            }
            else
            {
                s_instance.rightJoycon = data;
                s_instance.rightConnected = true;
                s_instance.pressedButtonsR = ((JoyconButtons)data.buttons).ToString();

                float sx = data.stickX / s_instance.stickDivisor;
                float sy = data.stickY / s_instance.stickDivisor;
                if (Mathf.Abs(sx) < s_instance.stickDeadzone) sx = 0;
                if (Mathf.Abs(sy) < s_instance.stickDeadzone) sy = 0;
                s_instance.rightStickValue = new Vector2(Mathf.Clamp(sx, -1f, 1f), Mathf.Clamp(sy, -1f, 1f));

                s_instance.rightAccelValue = new Vector3(data.accelX, data.accelY, data.accelZ) / 4096.0f;
                s_instance.rightGyroValue = new Vector3(data.gyroX, data.gyroY, data.gyroZ) * (360.0f / 48000.0f);
                s_instance.rightMouseValue = new Vector2(data.mouseX, data.mouseY) * s_instance.mouseSensitivity;

                // --- 精密積分 ---
                if (s_instance.lastPacketTimeR > 0) {
                    float dt = (float)(currentTime - s_instance.lastPacketTimeR);
                    Vector3 gyroRate = new Vector3(data.gyroX, data.gyroY, data.gyroZ) * UnityToJoyconGyro;
                    s_instance.gyroDeltaR += gyroRate * dt * s_instance.gyroMultiplier;
                }
                s_instance.lastPacketTimeR = currentTime;
            }
            s_instance.OnDataReceived?.Invoke(data);
        }
    }

    // 特定のボタンが押されているかチェックするヘルパーメソッド
    public bool GetButton(JoyconDeviceID device, JoyconButtons button)
    {
        uint currentButtons = (device == JoyconDeviceID.Left) ? leftJoycon.buttons : rightJoycon.buttons;
        return (currentButtons & (uint)button) != 0;
    }
}
