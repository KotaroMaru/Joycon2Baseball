using System;
using System.Runtime.InteropServices;

public enum JoyconDeviceID {
    Left = 0,
    Right = 1
}

[Flags]
public enum JoyconButtons : uint {
    A = 0x00000800,
    B = 0x00000400,
    X = 0x00000200,
    Y = 0x00000100,
    UP = 0x02000000,
    DOWN = 0x01000000,
    LEFT = 0x08000000,
    RIGHT = 0x04000000,
    L = 0x40000000,
    R = 0x00004000,
    ZL = 0x80000000,
    ZR = 0x00008000,
    SL_L = 0x20000000,
    SR_L = 0x10000000,
    SL_R = 0x00002000,
    SR_R = 0x00001000,
    START = 0x00020000,
    SELECT = 0x00010000,
    HOME = 0x00100000,
    CAPTURE = 0x00200000,
    LS = 0x00080000,
    RS = 0x00040000
}

[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct JoyconData {
    public int deviceID;
    public uint buttons;
    public short stickX, stickY;
    public short accelX, accelY, accelZ;
    public short gyroX, gyroY, gyroZ;
    public short mouseX, mouseY;
    public byte trigger;
}

public static class Joycon2Native {
    private const string PluginName = "Joycon2macOS";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void JoyconDataCallback(JoyconData data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void JoyconLogCallback(string message);

    [DllImport(PluginName)]
    public static extern void UnityStartScan();

    [DllImport(PluginName)]
    public static extern void UnityStopScan();

    [DllImport(PluginName)]
    public static extern void UnitySetDataCallback(JoyconDataCallback callback);

    [DllImport(PluginName)]
    public static extern void UnitySetLogCallback(JoyconLogCallback callback);
}
