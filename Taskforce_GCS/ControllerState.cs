namespace Taskforce_GCS.Models;

public struct ControllerState
{
    public bool IsConnected { get; set; }

    // Normalized sticks (-1.0f to 1.0f)
    public float LeftStickX { get; set; }
    public float LeftStickY { get; set; }
    public float RightStickX { get; set; }
    public float RightStickY { get; set; }

    // Triggers (0.0f to 1.0f)
    public float LeftTrigger { get; set; }   // Brake / Reverse
    public float RightTrigger { get; set; }  // Forward Throttle

    // Buttons
    public bool ButtonA { get; set; }
    public bool ButtonB { get; set; }
    public bool ButtonX { get; set; }
    public bool ButtonY { get; set; }
    public bool LeftShoulder { get; set; }   // LB (Sport Mode)
    public bool RightShoulder { get; set; }  // RB (Tank Mode)
    public bool Start { get; set; }          // Emergency Stop / Menu
    public bool Back { get; set; }
    public bool LeftThumbClick { get; set; }
    public bool RightThumbClick { get; set; }

    // D-Pad
    public bool DPadUp { get; set; }
    public bool DPadDown { get; set; }
    public bool DPadLeft { get; set; }
    public bool DPadRight { get; set; }

    // Computed control metrics for Prometheus
    public int CalculatedSteeringAngle { get; set; } // 0..180 (Center: 95)
    public int CalculatedSteeringUS { get; set; }    // 1000..2000 us (Center: 1500)
    public int CalculatedThrottlePWM { get; set; }   // 0..255
    public bool IsForward { get; set; }
    public bool IsReverse { get; set; }
    public bool IsIdle => !IsForward && !IsReverse;

    public bool SportModeActive { get; set; }
    public bool TankModeActive { get; set; }
}
