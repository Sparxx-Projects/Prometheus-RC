using System;
using System.Threading;
using System.Threading.Tasks;
using Taskforce_GCS.Models;

namespace Taskforce_GCS.Services;

public class XboxControllerManager : IDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _pollTask;

    public bool IsConnected { get; private set; }
    public ControllerState CurrentState { get; private set; }

    // Toggle states matching PROMETHEUS_RACE.ino
    public bool SportMode { get; set; } = false;
    public bool TankMode { get; set; } = false;

    // Previous button states for edge detection (press once to toggle)
    private bool _prevLB;
    private bool _prevRB;
    private bool _prevStart;
    private bool _prevB;
    private bool _prevA;

    public event Action<ControllerState>? StateUpdated;
    public event Action<bool>? ConnectionChanged;
    public event Action? EmergencyStopTriggered;
    public event Action? HornTriggered;

    private int _activeUserIndex = -1;

    public XboxControllerManager(int userIndex = -1)
    {
        _activeUserIndex = userIndex;
    }

    public void Start()
    {
        if (_pollTask != null) return;

        _cts = new CancellationTokenSource();
        _pollTask = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _pollTask?.Wait(200);
        }
        catch { }
        _pollTask = null;
        _cts?.Dispose();
        _cts = null;
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16)); // ~60 Hz

        while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
        {
            bool connected = false;
            NativeXInput.XInputState rawState = default;

            // Si ya teníamos un slot activo, probarlo primero
            if (_activeUserIndex >= 0)
            {
                int res = NativeXInput.GetState(_activeUserIndex, out rawState);
                if (res == NativeXInput.ERROR_SUCCESS)
                {
                    connected = true;
                }
                else
                {
                    _activeUserIndex = -1; // Desconectado, volver a escanear slots
                }
            }

            // Si no está conectado, escanear del slot 0 al 3 (muy común en Windows 11 por Bluetooth)
            if (!connected)
            {
                for (int i = 0; i < 4; i++)
                {
                    int res = NativeXInput.GetState(i, out rawState);
                    if (res == NativeXInput.ERROR_SUCCESS)
                    {
                        _activeUserIndex = i;
                        connected = true;
                        break;
                    }
                }
            }

            if (connected != IsConnected)
            {
                IsConnected = connected;
                ConnectionChanged?.Invoke(connected);
            }

            if (connected)
            {
                ProcessGamepadInput(rawState.Gamepad);
            }
            else
            {
                var emptyState = new ControllerState { IsConnected = false };
                CurrentState = emptyState;
                StateUpdated?.Invoke(emptyState);
            }
        }
    }

    private void ProcessGamepadInput(NativeXInput.XInputGamepad pad)
    {
        var state = new ControllerState { IsConnected = true };

        // 1. STICK ANALÓGICO IZQUIERDO (DIRECCIÓN)
        const int JOY_DEADZONE = 8000;
        int rawLX = pad.sThumbLX;
        float normalizedSteer = 0.0f;
        int targetUS = 1500;
        int targetAngle = 95; // Centro en firmware: 95°

        if (Math.Abs(rawLX) > JOY_DEADZONE)
        {
            // Mapeo normalizado -1.0 .. +1.0
            normalizedSteer = (float)rawLX / 32767.0f;
            if (normalizedSteer > 1.0f) normalizedSteer = 1.0f;
            if (normalizedSteer < -1.0f) normalizedSteer = -1.0f;

            // Curva exponencial cuadrática igual que en PROMETHEUS_RACE.ino:
            float expo = normalizedSteer * Math.Abs(normalizedSteer);
            targetUS = 1500 + (int)(expo * 500.0f);
            targetUS = Math.Clamp(targetUS, 1000, 2000);

            // Ángulo para comando V{angle}: Centro 95°, Izq ~46°, Der ~146°
            targetAngle = 95 + (int)(expo * 50.0f);
            targetAngle = Math.Clamp(targetAngle, 46, 146);
        }

        state.LeftStickX = normalizedSteer;
        state.LeftStickY = Math.Abs(pad.sThumbLY) > JOY_DEADZONE ? (float)pad.sThumbLY / 32767.0f : 0f;
        state.RightStickX = Math.Abs(pad.sThumbRX) > JOY_DEADZONE ? (float)pad.sThumbRX / 32767.0f : 0f;
        state.RightStickY = Math.Abs(pad.sThumbRY) > JOY_DEADZONE ? (float)pad.sThumbRY / 32767.0f : 0f;

        // 2. GATILLOS ANALÓGICOS (VELOCIDAD RT = AVANCE, LT = MARCHA ATRÁS)
        byte rawRT = pad.bRightTrigger;
        byte rawLT = pad.bLeftTrigger;

        state.RightTrigger = rawRT / 255.0f;
        state.LeftTrigger = rawLT / 255.0f;

        // 3. BOTONES
        ushort buttons = pad.wButtons;
        state.ButtonA = (buttons & NativeXInput.XINPUT_GAMEPAD_A) != 0;
        state.ButtonB = (buttons & NativeXInput.XINPUT_GAMEPAD_B) != 0;
        state.ButtonX = (buttons & NativeXInput.XINPUT_GAMEPAD_X) != 0;
        state.ButtonY = (buttons & NativeXInput.XINPUT_GAMEPAD_Y) != 0;
        state.LeftShoulder = (buttons & NativeXInput.XINPUT_GAMEPAD_LEFT_SHOULDER) != 0;
        state.RightShoulder = (buttons & NativeXInput.XINPUT_GAMEPAD_RIGHT_SHOULDER) != 0;
        state.Start = (buttons & NativeXInput.XINPUT_GAMEPAD_START) != 0;
        state.Back = (buttons & NativeXInput.XINPUT_GAMEPAD_BACK) != 0;
        state.LeftThumbClick = (buttons & NativeXInput.XINPUT_GAMEPAD_LEFT_THUMB) != 0;
        state.RightThumbClick = (buttons & NativeXInput.XINPUT_GAMEPAD_RIGHT_THUMB) != 0;

        state.DPadUp = (buttons & NativeXInput.XINPUT_GAMEPAD_DPAD_UP) != 0;
        state.DPadDown = (buttons & NativeXInput.XINPUT_GAMEPAD_DPAD_DOWN) != 0;
        state.DPadLeft = (buttons & NativeXInput.XINPUT_GAMEPAD_DPAD_LEFT) != 0;
        state.DPadRight = (buttons & NativeXInput.XINPUT_GAMEPAD_DPAD_RIGHT) != 0;

        // 4. TOGGLE MODO SPORT (LB)
        if (state.LeftShoulder && !_prevLB)
        {
            SportMode = !SportMode;
            TriggerHapticPulse(150, 0.4f, 0.4f);
        }
        _prevLB = state.LeftShoulder;

        // 5. TOGGLE MODO TANQUE (RB)
        if (state.RightShoulder && !_prevRB)
        {
            TankMode = !TankMode;
            TriggerHapticPulse(150, 0.5f, 0.2f);
        }
        _prevRB = state.RightShoulder;

        // 6. PARADA DE EMERGENCIA (B o Start)
        if ((state.ButtonB && !_prevB) || (state.Start && !_prevStart))
        {
            TriggerHapticPulse(300, 1.0f, 1.0f);
            EmergencyStopTriggered?.Invoke();
        }
        _prevB = state.ButtonB;
        _prevStart = state.Start;

        // 7. BOCINA / CLAXON (A)
        if (state.ButtonA && !_prevA)
        {
            HornTriggered?.Invoke();
        }
        _prevA = state.ButtonA;

        // 8. CÁLCULO DE PWM DE MOTORES (IDÉNTICO A PROMETHEUS_RACE.ino)
        const int MOTOR_MIN_PWM = 75;
        int topSpeed = SportMode ? 255 : 140;
        int calculatedPWM = 0;

        if (rawRT > 10)
        {
            state.IsForward = true;
            state.IsReverse = false;
            // Mapea 10..255 a MOTOR_MIN_PWM..topSpeed
            int clamped = Math.Clamp((int)rawRT, 10, 255);
            calculatedPWM = MOTOR_MIN_PWM + (int)((clamped - 10) / 245.0f * (topSpeed - MOTOR_MIN_PWM));
        }
        else if (rawLT > 10)
        {
            state.IsForward = false;
            state.IsReverse = true;
            int clamped = Math.Clamp((int)rawLT, 10, 255);
            calculatedPWM = MOTOR_MIN_PWM + (int)((clamped - 10) / 245.0f * (topSpeed - MOTOR_MIN_PWM));
        }
        else
        {
            state.IsForward = false;
            state.IsReverse = false;
            calculatedPWM = 0;
        }

        state.CalculatedThrottlePWM = calculatedPWM;
        state.CalculatedSteeringAngle = targetAngle;
        state.CalculatedSteeringUS = targetUS;
        state.SportModeActive = SportMode;
        state.TankModeActive = TankMode;

        CurrentState = state;
        StateUpdated?.Invoke(state);
    }

    public void TriggerHapticPulse(int durationMs, float leftMotor, float rightMotor)
    {
        if (_activeUserIndex < 0) return;
        int targetSlot = _activeUserIndex;

        Task.Run(async () =>
        {
            try
            {
                var vib = new NativeXInput.XInputVibration
                {
                    wLeftMotorSpeed = (ushort)(Math.Clamp(leftMotor, 0f, 1f) * 65535),
                    wRightMotorSpeed = (ushort)(Math.Clamp(rightMotor, 0f, 1f) * 65535)
                };
                NativeXInput.SetState(targetSlot, ref vib);

                await Task.Delay(durationMs);

                vib.wLeftMotorSpeed = 0;
                vib.wRightMotorSpeed = 0;
                NativeXInput.SetState(targetSlot, ref vib);
            }
            catch { }
        });
    }

    public void Dispose()
    {
        Stop();
    }
}
