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
                }
            }
        }

        public bool IsAvailable =>
            managerType != null &&
            instanceProperty != null &&
            rightConnectedField != null &&
            consumeGyroDeltaMethod != null &&
            getLinearAccelerationMethod != null &&
            joyconDeviceIdType != null &&
            rightDeviceIdValue != null;

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

        private object GetInstance()
        {
            return instanceProperty?.GetValue(null);
        }
    }
}
