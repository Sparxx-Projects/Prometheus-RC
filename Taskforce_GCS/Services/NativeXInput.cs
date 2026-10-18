using System;
using System.Runtime.InteropServices;

namespace Taskforce_GCS.Services;

internal static class NativeXInput
{
    public const int ERROR_SUCCESS = 0;
    public const int ERROR_DEVICE_NOT_CONNECTED = 1167;

    public const ushort XINPUT_GAMEPAD_DPAD_UP = 0x0001;
    public const ushort XINPUT_GAMEPAD_DPAD_DOWN = 0x0002;
    public const ushort XINPUT_GAMEPAD_DPAD_LEFT = 0x0004;
    public const ushort XINPUT_GAMEPAD_DPAD_RIGHT = 0x0008;
    public const ushort XINPUT_GAMEPAD_START = 0x0010;
    public const ushort XINPUT_GAMEPAD_BACK = 0x0020;
    public const ushort XINPUT_GAMEPAD_LEFT_THUMB = 0x0040;
    public const ushort XINPUT_GAMEPAD_RIGHT_THUMB = 0x0080;
    public const ushort XINPUT_GAMEPAD_LEFT_SHOULDER = 0x0100;
    public const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200;
    public const ushort XINPUT_GAMEPAD_A = 0x1000;
    public const ushort XINPUT_GAMEPAD_B = 0x2000;
    public const ushort XINPUT_GAMEPAD_X = 0x4000;
    public const ushort XINPUT_GAMEPAD_Y = 0x8000;

    public const short XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE = 7849;
    public const short XINPUT_GAMEPAD_RIGHT_THUMB_DEADZONE = 8689;
    public const byte XINPUT_GAMEPAD_TRIGGER_THRESHOLD = 30;

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputGamepad
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputState
    {
        public uint dwPacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputVibration
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState_1_4(int dwUserIndex, out XInputState pState);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
    private static extern int XInputSetState_1_4(int dwUserIndex, ref XInputVibration pVibration);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState_9_1(int dwUserIndex, out XInputState pState);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputSetState")]
    private static extern int XInputSetState_9_1(int dwUserIndex, ref XInputVibration pVibration);

    private static bool _useFallback = false;

    public static int GetState(int userIndex, out XInputState state)
    {
        if (!_useFallback)
        {
            try
            {
                return XInputGetState_1_4(userIndex, out state);
            }
            catch (DllNotFoundException)
            {
                _useFallback = true;
            }
        }

        try
        {
            return XInputGetState_9_1(userIndex, out state);
        }
        catch
        {
            state = default;
            return ERROR_DEVICE_NOT_CONNECTED;
        }
    }

    public static int SetState(int userIndex, ref XInputVibration vibration)
    {
        if (!_useFallback)
        {
            try
            {
                return XInputSetState_1_4(userIndex, ref vibration);
            }
            catch (DllNotFoundException)
            {
                _useFallback = true;
            }
        }

        try
        {
            return XInputSetState_9_1(userIndex, ref vibration);
        }
        catch
        {
            return ERROR_DEVICE_NOT_CONNECTED;
        }
    }
}
