using System;
using System.Reflection;
using UnityEngine;

namespace JoyconBaseball.Phase1.Core
{
    public sealed class Joycon2Bridge
    {
        private readonly Type managerType;
        private readonly PropertyInfo instanceProperty;
        private readonly FieldInfo rightConnectedField;
        private readonly PropertyInfo rightAccelProperty;
        private readonly FieldInfo rightJoyconField;
        private readonly MethodInfo consumeGyroDeltaMethod;
        private readonly MethodInfo getLinearAccelerationMethod;
        private readonly MethodInfo calibrateRightMethod;
        private readonly Type joyconDeviceIdType;
        private readonly object rightDeviceIdValue;
        private readonly Type joyconButtonsType;
        private readonly FieldInfo joyconDataButtonsField;

        // Left JoyCon
        private readonly FieldInfo leftConnectedField;
        private readonly PropertyInfo leftAccelProperty;
        private readonly PropertyInfo leftGyroProperty;
        private readonly PropertyInfo leftStickProperty;
        private readonly FieldInfo leftJoyconField;
        private readonly object leftDeviceIdValue;

        public Joycon2Bridge()
        {
            managerType = Type.GetType("Joycon2Manager") ?? Type.GetType("Joycon2Manager, Assembly-CSharp");
            if (managerType == null)
            {
                return;
            }

            instanceProperty = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            rightConnectedField = managerType.GetField("rightConnected", BindingFlags.Public | BindingFlags.Instance);
            rightAccelProperty = managerType.GetProperty("RightAccel", BindingFlags.Public | BindingFlags.Instance);
            rightJoyconField = managerType.GetField("rightJoycon", BindingFlags.Public | BindingFlags.Instance);
            consumeGyroDeltaMethod = managerType.GetMethod("ConsumeGyroDelta", BindingFlags.Public | BindingFlags.Instance);
            getLinearAccelerationMethod = managerType.GetMethod("GetLinearAcceleration", BindingFlags.Public | BindingFlags.Instance);
            calibrateRightMethod = managerType.GetMethod("CalibrateRight", BindingFlags.Public | BindingFlags.Instance);

            if (rightJoyconField != null)
            {
                joyconDataButtonsField = rightJoyconField.FieldType.GetField("buttons", BindingFlags.Public | BindingFlags.Instance);
            }

            joyconButtonsType = Type.GetType("JoyconButtons") ?? Type.GetType("JoyconButtons, Assembly-CSharp");

            var consumeParameter = consumeGyroDeltaMethod?.GetParameters();
            if (consumeParameter != null && consumeParameter.Length == 1)
            {
                joyconDeviceIdType = consumeParameter[0].ParameterType;
                if (joyconDeviceIdType.IsEnum)
                {
                    rightDeviceIdValue = Enum.Parse(joyconDeviceIdType, "Right", false);
                    leftDeviceIdValue  = Enum.Parse(joyconDeviceIdType, "Left",  false);
                }
            }

            // Left JoyCon reflection
            leftConnectedField = managerType.GetField("leftConnected", BindingFlags.Public | BindingFlags.Instance);
            leftAccelProperty  = managerType.GetProperty("LeftAccel",  BindingFlags.Public | BindingFlags.Instance);
            leftGyroProperty   = managerType.GetProperty("LeftGyro",   BindingFlags.Public | BindingFlags.Instance);
            leftStickProperty  = managerType.GetProperty("LeftStick",  BindingFlags.Public | BindingFlags.Instance);
            leftJoyconField    = managerType.GetField("leftJoycon",    BindingFlags.Public | BindingFlags.Instance);
        }

        public bool IsAvailable =>
            managerType != null &&
            instanceProperty != null &&
            rightConnectedField != null &&
            consumeGyroDeltaMethod != null &&
            getLinearAccelerationMethod != null &&
            joyconDeviceIdType != null &&
            rightDeviceIdValue != null;

        /// <summary>Left JoyCon の読み取りに必要な Reflection が解決できているか。</summary>
        public bool IsLeftAvailable =>
            managerType != null &&
            instanceProperty != null &&
            leftConnectedField != null &&
            leftAccelProperty != null;

        /// <summary>Reflection の解決状況をコンソールに出力する（デバッグ用）。</summary>
        public void LogReflectionStatus()
        {
            Debug.Log("[Joycon2Bridge] ==== Reflection Status ====");
            Debug.Log($"  managerType          : {(managerType        != null ? managerType.FullName : "NULL")}");
            Debug.Log($"  instanceProperty     : {(instanceProperty   != null ? "OK" : "NULL")}");
            Debug.Log($"  rightConnectedField  : {(rightConnectedField != null ? "OK" : "NULL")}");
            Debug.Log($"  rightAccelProperty   : {(rightAccelProperty  != null ? "OK" : "NULL")}");
            Debug.Log($"  consumeGyroDelta     : {(consumeGyroDeltaMethod != null ? "OK" : "NULL")}");
            Debug.Log($"  getLinearAccel       : {(getLinearAccelerationMethod != null ? "OK" : "NULL")}");
            Debug.Log($"  joyconDeviceIdType   : {(joyconDeviceIdType  != null ? joyconDeviceIdType.Name : "NULL")}");
            Debug.Log($"  rightDeviceIdValue   : {(rightDeviceIdValue  != null ? rightDeviceIdValue.ToString() : "NULL")}");
            Debug.Log($"  leftConnectedField   : {(leftConnectedField  != null ? "OK" : "NULL")}");
            Debug.Log($"  leftAccelProperty    : {(leftAccelProperty   != null ? "OK" : "NULL")}");
            Debug.Log($"  leftStickProperty    : {(leftStickProperty   != null ? "OK" : "NULL")}");
            Debug.Log($"  leftJoyconField      : {(leftJoyconField     != null ? "OK" : "NULL")}");
            Debug.Log($"  leftDeviceIdValue    : {(leftDeviceIdValue   != null ? leftDeviceIdValue.ToString() : "NULL")}");
            Debug.Log($"  IsAvailable          : {IsAvailable}");
            Debug.Log($"  IsLeftAvailable      : {IsLeftAvailable}");
        }

        public bool RightConnected
        {
            get
            {
                var instance = GetInstance();
                return instance != null && rightConnectedField != null && (bool)rightConnectedField.GetValue(instance);
            }
        }

        public Vector3 RightAccel
        {
            get
            {
                var instance = GetInstance();
                if (instance == null || rightAccelProperty == null)
                {
                    return Vector3.zero;
                }

                return (Vector3)rightAccelProperty.GetValue(instance);
            }
        }

        public Vector3 ConsumeRightGyroDelta()
        {
            var instance = GetInstance();
            if (instance == null)
            {
                return Vector3.zero;
            }

            return (Vector3)consumeGyroDeltaMethod.Invoke(instance, new[] { rightDeviceIdValue });
        }

        public void CalibrateRight(Quaternion batWorldRotation)
        {
            var instance = GetInstance();
            if (instance == null || calibrateRightMethod == null)
            {
                return;
            }

            calibrateRightMethod.Invoke(instance, new object[] { batWorldRotation });
        }

        public Vector3 GetRightLinearAcceleration(Quaternion rotation)
        {
            var instance = GetInstance();
            if (instance == null)
            {
                return Vector3.zero;
            }

            return (Vector3)getLinearAccelerationMethod.Invoke(instance, new object[] { rightDeviceIdValue, rotation });
        }

        public uint GetRightButtonsMask()
        {
            var instance = GetInstance();
            if (instance == null || rightJoyconField == null || joyconDataButtonsField == null)
            {
                return 0;
            }

            var joyconData = rightJoyconField.GetValue(instance);
            if (joyconData == null)
            {
                return 0;
            }

            return (uint)joyconDataButtonsField.GetValue(joyconData);
        }

        public uint GetButtonMask(string buttonName)
        {
            if (joyconButtonsType == null)
            {
                return 0;
            }

            return Convert.ToUInt32(Enum.Parse(joyconButtonsType, buttonName, false));
        }

        // ── Left JoyCon ──────────────────────────────────────────

        public bool LeftConnected
        {
            get
            {
                var instance = GetInstance();
                return instance != null && leftConnectedField != null && (bool)leftConnectedField.GetValue(instance);
            }
        }

        public Vector3 LeftAccel
        {
            get
            {
                var instance = GetInstance();
                if (instance == null || leftAccelProperty == null) return Vector3.zero;
                return (Vector3)leftAccelProperty.GetValue(instance);
            }
        }

        /// <summary>Left JoyCon のリアルタイムジャイロ角速度（消費なし）。Y 軸 = ひねり方向。</summary>
        public Vector3 LeftGyro
        {
            get
            {
                var instance = GetInstance();
                if (instance == null || leftGyroProperty == null) return Vector3.zero;
                return (Vector3)leftGyroProperty.GetValue(instance);
            }
        }

        public Vector2 LeftStick
        {
            get
            {
                var instance = GetInstance();
                if (instance == null || leftStickProperty == null) return Vector2.zero;
                return (Vector2)leftStickProperty.GetValue(instance);
            }
        }

        public uint GetLeftButtonsMask()
        {
            var instance = GetInstance();
            if (instance == null || leftJoyconField == null || joyconDataButtonsField == null) return 0;
            var joyconData = leftJoyconField.GetValue(instance);
            if (joyconData == null) return 0;
            return (uint)joyconDataButtonsField.GetValue(joyconData);
        }

        public Vector3 ConsumeLeftGyroDelta()
        {
            var instance = GetInstance();
            if (instance == null || consumeGyroDeltaMethod == null || leftDeviceIdValue == null)
                return Vector3.zero;
            return (Vector3)consumeGyroDeltaMethod.Invoke(instance, new[] { leftDeviceIdValue });
        }

        private object GetInstance()
        {
            return instanceProperty?.GetValue(null);
        }
    }
}
