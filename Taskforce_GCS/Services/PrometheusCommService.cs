using System;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Taskforce_GCS.Models;

namespace Taskforce_GCS.Services;

public enum CommMode
{
    SerialBluetooth,
    WifiUdp
}

public class PrometheusCommService : IDisposable
{
    private SerialPort? _serialPort;
    private UdpClient? _udpClient;
    private UdpClient? _udpTelemetryListener;
    private CancellationTokenSource? _telemetryCts;

    private string _udpHost = "192.168.4.255"; // Broadcast en la subred de la camara
    private int _udpPort = 4210;
    private bool _carIpResolved = false;

    public CommMode CurrentMode { get; set; } = CommMode.WifiUdp;
    public bool IsConnected { get; private set; }

    private int _lastSentSteering = 95;
    private int _lastSentPWM = 0;
    private string _lastSentDirection = "S";
    private bool _lastTankMode = false;
    private DateTime _lastKeepAlive = DateTime.MinValue;

    public event Action<string>? CommandSent;
    public event Action<bool, string>? ConnectionStatusChanged;
    public event Action<string>? LogMessage;
    public event Action<int, int, string>? TelemetryReceived;

    public static string[] GetAvailablePorts()
    {
        try
        {
            return SerialPort.GetPortNames();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public bool ConnectSerial(string portName, int baudRate = 115200)
    {
        Disconnect();

        try
        {
            _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 2000,
                WriteTimeout = 2000,
                Handshake = Handshake.None,
                DtrEnable = false,
                RtsEnable = false
            };

            _serialPort.Open();
            CurrentMode = CommMode.SerialBluetooth;
            IsConnected = true;
            ConnectionStatusChanged?.Invoke(true, $"SERIAL [{portName}] @ {baudRate} bps");
            LogMessage?.Invoke($"[CONEXIÓN] Enlace Serie abierto en {portName}");
            return true;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ConnectionStatusChanged?.Invoke(false, $"Error al abrir {portName}: {ex.Message}");
            LogMessage?.Invoke($"[ERROR SERIE] {ex.Message}");
            return false;
        }
    }

    public bool ConnectUdp(string host = "192.168.4.255", int port = 4210)
    {
        Disconnect();

        try
        {
            _udpHost = host;
            _udpPort = port;
            _carIpResolved = false;
            _udpClient = new UdpClient();
            _udpClient.EnableBroadcast = true;

            // Iniciar receptor de telemetria en puerto 4211
            _telemetryCts = new CancellationTokenSource();
            _udpTelemetryListener = new UdpClient(4211);
            Task.Run(() => TelemetryListenLoopAsync(_telemetryCts.Token));

            CurrentMode = CommMode.WifiUdp;
            IsConnected = true;
            ConnectionStatusChanged?.Invoke(true, $"WI-FI UDP [{host}:{port}]");
            LogMessage?.Invoke($"[CONEXIÓN] Enlace Wi-Fi UDP activo hacia {host}:{port}");
            
            // Enviar ping inicial de parada para despertar el coche
            SendRaw("S");
            return true;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ConnectionStatusChanged?.Invoke(false, $"Error UDP: {ex.Message}");
            LogMessage?.Invoke($"[ERROR UDP] {ex.Message}");
            return false;
        }
    }

    private async Task TelemetryListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _udpTelemetryListener != null)
        {
            try
            {
                var result = await _udpTelemetryListener.ReceiveAsync(ct);
                string text = Encoding.ASCII.GetString(result.Buffer);

                // Protocolo: T,PWM,STEER,DIR
                if (text.StartsWith("T,"))
                {
                    // Fijar IP exacta del coche (STA-to-STA)
                    string senderIp = result.RemoteEndPoint.Address.ToString();
                    if (!_carIpResolved || _udpHost != senderIp)
                    {
                        _udpHost = senderIp;
                        _carIpResolved = true;
                        LogMessage?.Invoke($"[ENLACE] Coche localizado en IP: {_udpHost}");
                    }

                    string[] parts = text.Split(',');
                    if (parts.Length >= 4 &&
                        int.TryParse(parts[1], out int pwm) &&
                        int.TryParse(parts[2], out int steer))
                    {
                        TelemetryReceived?.Invoke(pwm, steer, parts[3]);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignorar paquetes corruptos de red
            }
        }
    }

    public void Disconnect()
    {
        try
        {
            _telemetryCts?.Cancel();
            _telemetryCts?.Dispose();
            _telemetryCts = null;

            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Write("X"); // Parada de seguridad antes de cerrar
                    _serialPort.Close();
                }
                _serialPort.Dispose();
                _serialPort = null;
            }

            if (_udpClient != null)
            {
                _udpClient.Close();
                _udpClient.Dispose();
                _udpClient = null;
            }

            if (_udpTelemetryListener != null)
            {
                _udpTelemetryListener.Close();
                _udpTelemetryListener.Dispose();
                _udpTelemetryListener = null;
            }
        }
        catch { }

        if (IsConnected)
        {
            IsConnected = false;
            _carIpResolved = false;
            ConnectionStatusChanged?.Invoke(false, "DESCONECTADO");
            LogMessage?.Invoke("[DESCONEXIÓN] Enlace cerrado");
        }
    }

    public void SendRaw(string cmd, bool showInLog = true)
    {
        if (!IsConnected) return;

        try
        {
            byte[] bytes = Encoding.ASCII.GetBytes(cmd);

            if (CurrentMode == CommMode.SerialBluetooth && _serialPort?.IsOpen == true)
            {
                _serialPort.Write(bytes, 0, bytes.Length);
            }
            else if (CurrentMode == CommMode.WifiUdp && _udpClient != null)
            {
                if (_carIpResolved)
                {
                    _udpClient.Send(bytes, bytes.Length, _udpHost, _udpPort);
                }
                else
                {
                    // Enviar a todas las estaciones posibles para evitar bloqueo de broadcast en SoftAP
                    _udpClient.Send(bytes, bytes.Length, "192.168.4.2", _udpPort);
                    _udpClient.Send(bytes, bytes.Length, "192.168.4.3", _udpPort);
                    _udpClient.Send(bytes, bytes.Length, "192.168.4.4", _udpPort);
                    _udpClient.Send(bytes, bytes.Length, "192.168.4.255", _udpPort);
                }
            }

            if (showInLog)
            {
                CommandSent?.Invoke(cmd);
            }
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"[ERROR ENVÍO] {ex.Message}");
        }
    }

    public void EmergencyStop()
    {
        _lastSentDirection = "X";
        _lastSentPWM = 0;
        SendRaw("X");
        LogMessage?.Invoke("[ALERTA] ¡PARADA DE EMERGENCIA ENVIADA (X)!");
    }

    public void TriggerHorn()
    {
        SendRaw("G");
        LogMessage?.Invoke("[ACCIÓN] Bocina / Pulso activado (G)");
    }

    public void ProcessControllerUpdate(ControllerState state)
    {
        if (!IsConnected) return;

        bool needSend = false;
        DateTime now = DateTime.UtcNow;

        // 1. DIRECCIÓN
        int steer = state.CalculatedSteeringAngle;
        if (Math.Abs(steer - _lastSentSteering) >= 2)
        {
            SendRaw($"V{steer}");
            _lastSentSteering = steer;
            needSend = true;
        }

        // 2. ACELERACIÓN / RETROCESO / PARADA
        int pwm = state.CalculatedThrottlePWM;
        string dir = "S";

        if (state.IsForward) dir = "F";
        else if (state.IsReverse) dir = "B";
        else dir = "S";

        if (dir != _lastSentDirection || Math.Abs(pwm - _lastSentPWM) >= 4)
        {
            if (dir == "F") SendRaw($"F{pwm}");
            else if (dir == "B") SendRaw($"B{pwm}");
            else SendRaw("S");

            _lastSentDirection = dir;
            _lastSentPWM = pwm;
            needSend = true;
            _lastKeepAlive = now;
        }

        // 3. MODO TANQUE (RB)
        if (state.TankModeActive != _lastTankMode)
        {
            _lastTankMode = state.TankModeActive;
            SendRaw(_lastTankMode ? "K1" : "K0");
            LogMessage?.Invoke($"[MODO] Giro Tanque Skid-Steer: {(_lastTankMode ? "ACTIVADO (K1)" : "DESACTIVADO (K0)")}");
        }

        // 3. Keep-alive cada 250ms SOLO si el coche está en movimiento (F o B)
        // para mantener vivo el watchdog sin saturar el terminal con 'S' cuando está parado.
        if (!needSend && (now - _lastKeepAlive).TotalMilliseconds > 250)
        {
            if (_lastSentDirection == "F")
            {
                SendRaw($"F{_lastSentPWM}", showInLog: false);
                _lastKeepAlive = now;
            }
            else if (_lastSentDirection == "B")
            {
                SendRaw($"B{_lastSentPWM}", showInLog: false);
                _lastKeepAlive = now;
            }
        }
    }

    public void Dispose()
    {
        Disconnect();
    }
}
