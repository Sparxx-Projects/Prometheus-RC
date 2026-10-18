using System;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Taskforce_GCS.Models;
using Taskforce_GCS.Services;

namespace Taskforce_GCS;

public partial class MainWindow : Window
{
    private readonly XboxControllerManager _controllerManager;
    private readonly MjpegStreamPlayer _streamPlayer;
    private readonly PrometheusCommService _commService;

    private SoundPlayer? _hackSoundPlayer;
    private bool _isHackingActive = false;
    private bool _isCameraRequested = false;

    private readonly SolidColorBrush _brushRed = new(Color.FromRgb(255, 51, 68));
    private readonly SolidColorBrush _brushGreen = new(Color.FromRgb(0, 255, 136));
    private readonly SolidColorBrush _brushCyan = new(Color.FromRgb(0, 240, 255));
    private readonly SolidColorBrush _brushOrange = new(Color.FromRgb(255, 170, 0));
    private readonly SolidColorBrush _brushDim = new(Color.FromRgb(139, 148, 158));
    private readonly SolidColorBrush _brushBtnActive = new(Color.FromRgb(45, 60, 85));
    private readonly SolidColorBrush _brushBtnInactive = new(Color.FromRgb(29, 36, 51));

    public MainWindow()
    {
        InitializeComponent();

        _controllerManager = new XboxControllerManager(-1);
        _streamPlayer = new MjpegStreamPlayer();
        _commService = new PrometheusCommService();

        // 1. Eventos del Mando Xbox
        _controllerManager.ConnectionChanged += OnControllerConnectionChanged;
        _controllerManager.StateUpdated += OnControllerStateUpdated;
        _controllerManager.EmergencyStopTriggered += OnEmergencyStopTriggered;
        _controllerManager.HornTriggered += OnHornTriggered;

        // 2. Eventos del Stream de Vídeo
        _streamPlayer.FrameReceived += OnVideoFrameReceived;
        _streamPlayer.StatusChanged += OnVideoStatusChanged;
        _streamPlayer.StreamError += OnVideoStreamError;

        // 3. Eventos del Enlace al Coche
        _commService.ConnectionStatusChanged += OnRobotConnectionStatusChanged;
        _commService.CommandSent += OnRobotCommandSent;
        _commService.LogMessage += AppendLog;
        _commService.TelemetryReceived += OnTelemetryReceived;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        KeyDown += MainWindow_KeyDown;
        KeyUp += MainWindow_KeyUp;

        InitializeAudio();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _controllerManager.Start();
        AppendLog("[INICIO] Ground Control Station v2.0 NieR 9S inicializada con éxito.");
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _controllerManager.Dispose();
        _streamPlayer.Dispose();
        _commService.Dispose();
    }

    #region Conexión Wi-Fi UDP y Control del Coche

    private void BtnConnectUdp_Click(object sender, RoutedEventArgs e)
    {
        if (_commService.IsConnected && _commService.CurrentMode == CommMode.WifiUdp)
        {
            _commService.Disconnect();
            BtnConnectUdp.Content = "⚡ ENLACE WI-FI UDP";
            BtnConnectUdp.Background = new SolidColorBrush(Color.FromRgb(10, 58, 64));
        }
        else
        {
            bool ok = _commService.ConnectUdp("192.168.4.255", 4210);
            if (ok)
            {
                BtnConnectUdp.Content = "⚡ DESCONECTAR UDP";
                BtnConnectUdp.Background = new SolidColorBrush(Color.FromRgb(18, 90, 50));
            }
        }
    }

    private void OnTelemetryReceived(int pwm, int steer, string dir)
    {
        Dispatcher.Invoke(() =>
        {
            DotRobot.Fill = _brushGreen;
            TxtRobot.Text = "ENLACE COCHE: OK";
            TxtRobot.Foreground = _brushGreen;

            BarPWM.Value = pwm;
            int pct = (int)(pwm / 255.0 * 100);
            string dirLabel = dir == "F" ? "AVANCE" : dir == "B" ? "RETROCESO" : "PARADO";
            TxtTelemetryPWM.Text = $"{pwm} PWM ({pct}%) [{dirLabel}]";

            BarSteer.Value = steer;
            TxtTelemetrySteer.Text = $"{steer}°";
        });
    }

    private void OnRobotConnectionStatusChanged(bool connected, string message)
    {
        Dispatcher.Invoke(() =>
        {
            if (connected)
            {
                if (_commService.CurrentMode == CommMode.WifiUdp)
                {
                    DotRobot.Fill = _brushOrange;
                    TxtRobot.Text = "ENLACE: BUSCANDO COCHE...";
                    TxtRobot.Foreground = _brushOrange;
                }
                else
                {
                    DotRobot.Fill = _brushGreen;
                    TxtRobot.Text = "ENLACE COCHE: OK";
                    TxtRobot.Foreground = _brushGreen;
                }
            }
            else
            {
                DotRobot.Fill = _brushRed;
                TxtRobot.Text = "ENLACE COCHE: OFF";
                TxtRobot.Foreground = _brushDim;
            }

            TxtFooterStatus.Text = $"Estado Enlace: {message}";
        });
    }

    private void OnRobotCommandSent(string cmd)
    {
        // Actualizar terminal de comandos
        Dispatcher.Invoke(() =>
        {
            AppendLog($"TX >> {cmd}");
        });
    }

    #endregion

    #region Control de Vídeo ESP32-CAM y Protocolo 9S

    private void InitializeAudio()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            string? resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("hack_sound.wav", StringComparison.OrdinalIgnoreCase));
            if (resourceName != null)
            {
                var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    _hackSoundPlayer = new SoundPlayer(stream);
                    _hackSoundPlayer.LoadAsync();
                    return;
                }
            }

            string diskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "hack_sound.wav");
            if (File.Exists(diskPath))
            {
                _hackSoundPlayer = new SoundPlayer(diskPath);
                _hackSoundPlayer.LoadAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AUDIO] Error cargando sonido 9S: {ex.Message}");
        }
    }

    private void PlayHackSound()
    {
        try
        {
            _hackSoundPlayer?.Play();
        }
        catch { }
    }

    private void ResetHackingAnimation()
    {
        NierHackingOverlay.BeginAnimation(UIElement.OpacityProperty, null);
        HackingTerminalStage.BeginAnimation(UIElement.OpacityProperty, null);
        HackingLockStage.BeginAnimation(UIElement.OpacityProperty, null);
        HackingMatrixStage.BeginAnimation(UIElement.OpacityProperty, null);
        HackingWhiteFlash.BeginAnimation(UIElement.OpacityProperty, null);

        RingOuterRot.BeginAnimation(RotateTransform.AngleProperty, null);
        RingInnerRot.BeginAnimation(RotateTransform.AngleProperty, null);

        LockReticleScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        LockReticleScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        RadialBarsScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        RadialBarsScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        TopLineTrans.BeginAnimation(TranslateTransform.YProperty, null);
        BottomLineTrans.BeginAnimation(TranslateTransform.YProperty, null);
        LeftLineTrans.BeginAnimation(TranslateTransform.XProperty, null);
        RightLineTrans.BeginAnimation(TranslateTransform.XProperty, null);

        IntersectionTLTrans.BeginAnimation(TranslateTransform.XProperty, null);
        IntersectionTLTrans.BeginAnimation(TranslateTransform.YProperty, null);
        IntersectionTRTrans.BeginAnimation(TranslateTransform.XProperty, null);
        IntersectionTRTrans.BeginAnimation(TranslateTransform.YProperty, null);
        IntersectionBLTrans.BeginAnimation(TranslateTransform.XProperty, null);
        IntersectionBLTrans.BeginAnimation(TranslateTransform.YProperty, null);
        IntersectionBRTrans.BeginAnimation(TranslateTransform.XProperty, null);
        IntersectionBRTrans.BeginAnimation(TranslateTransform.YProperty, null);

        LeftNeedleTrans.BeginAnimation(TranslateTransform.XProperty, null);
        RightNeedleTrans.BeginAnimation(TranslateTransform.XProperty, null);
        TopNeedleTrans.BeginAnimation(TranslateTransform.YProperty, null);
        BottomNeedleTrans.BeginAnimation(TranslateTransform.YProperty, null);

        NierHackingOverlay.Visibility = Visibility.Collapsed;
        NierHackingOverlay.Opacity = 1.0;
        HackingTerminalStage.Visibility = Visibility.Collapsed;
        HackingTerminalStage.Opacity = 0.0;
        HackingLockStage.Visibility = Visibility.Collapsed;
        HackingLockStage.Opacity = 0.0;
        HackingMatrixStage.Opacity = 0.0;
        HackingWhiteFlash.Opacity = 0.0;
        HackingSuccessBanner.Visibility = Visibility.Collapsed;

        RingOuterRot.Angle = 0;
        RingInnerRot.Angle = 0;

        BarHackingProgress.Value = 0;
        TxtHackingPercent.Text = "0%";
        TxtCoreStatus.Text = "LOCKED";
        TxtCoreStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 222, 89));
        TxtCoreSub.Text = "SECURITY CORE";
        TxtDepStatus1.Text = "BYPASSING...";
        TxtDepStatus1.Foreground = new SolidColorBrush(Color.FromRgb(255, 222, 89));
        BadgeDep1.BorderBrush = new SolidColorBrush(Color.FromRgb(53, 69, 96));
        DotDep1.Fill = new SolidColorBrush(Color.FromRgb(255, 222, 89));

        TxtDepStatus2.Text = "PENDING";
        TxtDepStatus2.Foreground = new SolidColorBrush(Color.FromRgb(85, 112, 144));
        BadgeDep2.BorderBrush = new SolidColorBrush(Color.FromRgb(53, 69, 96));
        DotDep2.Fill = new SolidColorBrush(Color.FromRgb(85, 112, 144));

        TxtDepStatus3.Text = "PENDING";
        TxtDepStatus3.Foreground = new SolidColorBrush(Color.FromRgb(85, 112, 144));
        BadgeDep3.BorderBrush = new SolidColorBrush(Color.FromRgb(53, 69, 96));
        DotDep3.Fill = new SolidColorBrush(Color.FromRgb(85, 112, 144));

        TxtHackingLogLine.Text = "> Inicializando intrusión AetherTerminal...";

        LockReticleScale.ScaleX = 1.0;
        LockReticleScale.ScaleY = 1.0;
        RadialBarsScale.ScaleX = 0.85;
        RadialBarsScale.ScaleY = 0.85;

        TopLineTrans.Y = -35;
        BottomLineTrans.Y = 35;
        LeftLineTrans.X = -45;
        RightLineTrans.X = 45;

        IntersectionTLTrans.X = -45;
        IntersectionTLTrans.Y = -35;
        IntersectionTRTrans.X = 45;
        IntersectionTRTrans.Y = -35;
        IntersectionBLTrans.X = -45;
        IntersectionBLTrans.Y = 35;
        IntersectionBRTrans.X = 45;
        IntersectionBRTrans.Y = 35;

        LeftNeedleTrans.X = -40;
        RightNeedleTrans.X = 40;
        TopNeedleTrans.Y = -30;
        BottomNeedleTrans.Y = 30;
    }

    private void BtnConnectStream_Click(object sender, RoutedEventArgs e)
    {
        if (_isCameraRequested || _streamPlayer.IsStreaming)
        {
            // DESCONECTAR CÁMARA
            _isCameraRequested = false;
            _streamPlayer.Stop();

            ResetHackingAnimation();
            _isHackingActive = false;

            VideoPlayer.Source = null;
            NoSignalOverlay.Visibility = Visibility.Visible;

            BtnConnectStream.Content = "⚡ INICIAR CÁMARA [AETHERTERMINAL]";
            BtnConnectStream.Background = new SolidColorBrush(Color.FromRgb(44, 36, 23));
            BtnConnectStream.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 222, 89));
            BtnConnectStream.Foreground = new SolidColorBrush(Color.FromRgb(255, 222, 89));

            TxtVideo.Text = "CÁMARA: OFF";
            TxtVideo.Foreground = _brushDim;
            DotVideo.Fill = _brushRed;

            AppendLog("[VÍDEO] Cámara desconectada por el operador.");
        }
        else
        {
            // INICIAR CÁMARA CON PROTOCOLO AETHERTERMINAL
            string url = TxtStreamUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                url = "http://192.168.4.1:81/stream";
                TxtStreamUrl.Text = url;
            }

            _isCameraRequested = true;

            BtnConnectStream.Content = "⏹ DESCONECTAR CÁMARA";
            BtnConnectStream.Background = new SolidColorBrush(Color.FromRgb(85, 20, 28));
            BtnConnectStream.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 70, 85));
            BtnConnectStream.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));

            TxtVideo.Text = "CÁMARA: AETHERTERMINAL...";
            TxtVideo.Foreground = _brushOrange;
            AppendLog($"[VÍDEO] Iniciando protocolo de intrusión AetherTerminal a {url}...");

            // Disparar inmediatamente la secuencia de hackeo y animación
            Play9sHackingAnimation();

            // Iniciar recepción del flujo de vídeo en paralelo
            _streamPlayer.Start(url);
        }
    }

    private async void Play9sHackingAnimation()
    {
        if (_isHackingActive) return;
        _isHackingActive = true;

        ResetHackingAnimation();

        // =========================================================================
        // 1. ETAPA PREVIA: PANTALLA REINVENTADA DE INTRUSIÓN AETHERTERMINAL (HUD VITAL)
        // Las líneas blancas tácticas permanecen COLLAPSED para que NUNCA aparezcan encima.
        // =========================================================================
        NierHackingOverlay.Visibility = Visibility.Visible;
        NierHackingOverlay.Opacity = 1.0;
        HackingTerminalStage.Visibility = Visibility.Visible;
        HackingTerminalStage.Opacity = 1.0;
        HackingLockStage.Visibility = Visibility.Collapsed;
        HackingLockStage.Opacity = 0.0;

        // Iniciar rotación continua y suave de los anillos cibernéticos
        var ringOuterAnim = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(2.8)))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        var ringInnerAnim = new DoubleAnimation(0, -360, new Duration(TimeSpan.FromSeconds(1.9)))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        RingOuterRot.BeginAnimation(RotateTransform.AngleProperty, ringOuterAnim);
        RingInnerRot.BeginAnimation(RotateTransform.AngleProperty, ringInnerAnim);

        // Paso A: Cortafuegos
        TxtHackingLogLine.Text = "> AetherTerminal // Vulnerando cortafuegos de subred [ESP32-CAM]...";
        BarHackingProgress.Value = 18;
        TxtHackingPercent.Text = "18%";
        AppendLog("[AETHERTERMINAL // INTRUSIÓN] Escaneando subred y cortafuegos en 192.168.4.1...");
        await Task.Delay(260);

        TxtDepStatus1.Text = "OVERRIDDEN";
        TxtDepStatus1.Foreground = new SolidColorBrush(Color.FromRgb(56, 239, 125));
        BadgeDep1.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 239, 125));
        DotDep1.Fill = new SolidColorBrush(Color.FromRgb(56, 239, 125));
        BarHackingProgress.Value = 42;
        TxtHackingPercent.Text = "42%";

        // Paso B: Dependencias del sistema de red
        TxtDepStatus2.Text = "INJECTING...";
        TxtDepStatus2.Foreground = new SolidColorBrush(Color.FromRgb(255, 222, 89));
        DotDep2.Fill = new SolidColorBrush(Color.FromRgb(255, 222, 89));
        TxtHackingLogLine.Text = "> AetherTerminal // Inyectando protocolos y dependencias de enlace...";
        AppendLog("[AETHERTERMINAL // HACKEO] Inyectando dependencias y resolviendo sockets... [OK]");
        await Task.Delay(260);

        TxtDepStatus2.Text = "ACTIVE";
        TxtDepStatus2.Foreground = new SolidColorBrush(Color.FromRgb(56, 239, 125));
        BadgeDep2.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 239, 125));
        DotDep2.Fill = new SolidColorBrush(Color.FromRgb(56, 239, 125));
        BarHackingProgress.Value = 78;
        TxtHackingPercent.Text = "78%";

        // Paso C: Driver óptico OV2640
        TxtDepStatus3.Text = "ATTACHING...";
        TxtDepStatus3.Foreground = new SolidColorBrush(Color.FromRgb(255, 222, 89));
        DotDep3.Fill = new SolidColorBrush(Color.FromRgb(255, 222, 89));
        TxtHackingLogLine.Text = "> AetherTerminal // Vinculando driver de sensor de vídeo OV2640...";
        await Task.Delay(260);

        TxtDepStatus3.Text = "READY";
        TxtDepStatus3.Foreground = new SolidColorBrush(Color.FromRgb(56, 239, 125));
        BadgeDep3.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 239, 125));
        DotDep3.Fill = new SolidColorBrush(Color.FromRgb(56, 239, 125));
        BarHackingProgress.Value = 100;
        TxtHackingPercent.Text = "100%";
        AppendLog("[AETHERTERMINAL // DRIVER] Sensor óptico OV2640 sincronizado.");

        // Paso D: Desbloqueo del núcleo táctico
        TxtCoreStatus.Text = "UNLOCKED";
        TxtCoreStatus.Foreground = new SolidColorBrush(Color.FromRgb(56, 239, 125));
        TxtCoreSub.Text = "ACCESS GRANTED";
        TxtHackingLogLine.Text = "> NÚCLEO DESBLOQUEADO // ENLACE AETHER CONCEDIDO";
        await Task.Delay(300);

        // Desvanecer rápidamente la pantalla de dependencias
        var termFade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(120));
        HackingTerminalStage.BeginAnimation(UIElement.OpacityProperty, termFade);
        await Task.Delay(120);
        HackingTerminalStage.Visibility = Visibility.Collapsed;

        // =========================================================================
        // 2. DISPARO DEL EFECTO 9S Y AUDIO ORIGINAL
        // =========================================================================
        PlayHackSound();

        // 3. ACTIVAR ESCENA TÁCTICA BLANCA (SÓLO AHORA ES VISIBLE)
        HackingLockStage.Visibility = Visibility.Visible;
        HackingLockStage.Opacity = 1.0;
        HackingMatrixStage.Opacity = 0.0;
        HackingWhiteFlash.Opacity = 0.0;
        HackingSuccessBanner.Visibility = Visibility.Visible;

        // =========================================================================
        // 4. ANIMACIONES DIRECTAS ULTRA-FLUIDAS (SIN PAUSAS, MOVIMIENTO CONTINUO)
        // NieR: Automata Ver1.1a Authentic Fluid Keyframes
        // =========================================================================
        
        // A. Escala Retículo Global (Expansión fluida continua t=0 -> t=740ms sin congelamiento)
        var scaleXAnim = new DoubleAnimationUsingKeyFrames();
        var scaleYAnim = new DoubleAnimationUsingKeyFrames();

        scaleXAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.15, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)))
        {
            KeySpline = new KeySpline(0.1, 0.9, 0.2, 1.0)
        });
        scaleXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(4.6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(740)))
        {
            KeySpline = new KeySpline(0.65, 0.0, 0.95, 0.6)
        });

        scaleYAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.15, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)))
        {
            KeySpline = new KeySpline(0.1, 0.9, 0.2, 1.0)
        });
        scaleYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(4.6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(740)))
        {
            KeySpline = new KeySpline(0.65, 0.0, 0.95, 0.6)
        });

        LockReticleScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
        LockReticleScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);

        // B. Líneas Horizontales Deslizándose Continuamente (desde t=0ms en movimiento fluido)
        var topYAnim = new DoubleAnimationUsingKeyFrames();
        topYAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(-35, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        topYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-85, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320)))
        {
            KeySpline = new KeySpline(0.2, 0.8, 0.2, 1.0)
        });
        topYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-260, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(740)))
        {
            KeySpline = new KeySpline(0.65, 0.0, 0.95, 0.6)
        });
        TopLineTrans.BeginAnimation(TranslateTransform.YProperty, topYAnim);

        var bottomYAnim = new DoubleAnimationUsingKeyFrames();
        bottomYAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(35, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        bottomYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(85, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320)))
        {
            KeySpline = new KeySpline(0.2, 0.8, 0.2, 1.0)
        });
        bottomYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(260, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(740)))
        {
            KeySpline = new KeySpline(0.65, 0.0, 0.95, 0.6)
        });
        BottomLineTrans.BeginAnimation(TranslateTransform.YProperty, bottomYAnim);

        // C. Líneas Verticales Deslizándose Continuamente
        var leftXAnim = new DoubleAnimationUsingKeyFrames();
        leftXAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(-45, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        leftXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-110, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320)))
        {
            KeySpline = new KeySpline(0.2, 0.8, 0.2, 1.0)
        });
        leftXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-320, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(740)))
        {
            KeySpline = new KeySpline(0.65, 0.0, 0.95, 0.6)
        });
        LeftLineTrans.BeginAnimation(TranslateTransform.XProperty, leftXAnim);

        var rightXAnim = new DoubleAnimationUsingKeyFrames();
        rightXAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(45, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        rightXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(110, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320)))
        {
            KeySpline = new KeySpline(0.2, 0.8, 0.2, 1.0)
        });
        rightXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(320, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(740)))
        {
            KeySpline = new KeySpline(0.65, 0.0, 0.95, 0.6)
        });
        RightLineTrans.BeginAnimation(TranslateTransform.XProperty, rightXAnim);

        // D. 4 Intersecciones de Cruz (+) sincronizadas perfectamente
        IntersectionTLTrans.BeginAnimation(TranslateTransform.XProperty, leftXAnim);
        IntersectionTLTrans.BeginAnimation(TranslateTransform.YProperty, topYAnim);

        IntersectionTRTrans.BeginAnimation(TranslateTransform.XProperty, rightXAnim);
        IntersectionTRTrans.BeginAnimation(TranslateTransform.YProperty, topYAnim);

        IntersectionBLTrans.BeginAnimation(TranslateTransform.XProperty, leftXAnim);
        IntersectionBLTrans.BeginAnimation(TranslateTransform.YProperty, bottomYAnim);

        IntersectionBRTrans.BeginAnimation(TranslateTransform.XProperty, rightXAnim);
        IntersectionBRTrans.BeginAnimation(TranslateTransform.YProperty, bottomYAnim);

        // E. Expansión Radial de los 8 Corchetes (movimiento continuo)
        var radXAnim = new DoubleAnimationUsingKeyFrames();
        radXAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.85, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        radXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320)))
        {
            KeySpline = new KeySpline(0.2, 0.8, 0.2, 1.0)
        });
        radXAnim.KeyFrames.Add(new SplineDoubleKeyFrame(3.2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(740)))
        {
            KeySpline = new KeySpline(0.65, 0.0, 0.95, 0.6)
        });

        var radYAnim = new DoubleAnimationUsingKeyFrames();
        radYAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.85, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        radYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320)))
        {
            KeySpline = new KeySpline(0.2, 0.8, 0.2, 1.0)
        });
        radYAnim.KeyFrames.Add(new SplineDoubleKeyFrame(3.2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(740)))
        {
            KeySpline = new KeySpline(0.65, 0.0, 0.95, 0.6)
        });

        RadialBarsScale.BeginAnimation(ScaleTransform.ScaleXProperty, radXAnim);
        RadialBarsScale.BeginAnimation(ScaleTransform.ScaleYProperty, radYAnim);

        // F. Agujas Laterales Convergiendo al Centro suavemente
        var needleLX = new DoubleAnimationUsingKeyFrames();
        needleLX.KeyFrames.Add(new DiscreteDoubleKeyFrame(-40, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        needleLX.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380)))
        {
            KeySpline = new KeySpline(0.0, 0.0, 0.2, 1.0)
        });
        LeftNeedleTrans.BeginAnimation(TranslateTransform.XProperty, needleLX);

        var needleRX = new DoubleAnimationUsingKeyFrames();
        needleRX.KeyFrames.Add(new DiscreteDoubleKeyFrame(40, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        needleRX.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380)))
        {
            KeySpline = new KeySpline(0.0, 0.0, 0.2, 1.0)
        });
        RightNeedleTrans.BeginAnimation(TranslateTransform.XProperty, needleRX);

        var needleTY = new DoubleAnimationUsingKeyFrames();
        needleTY.KeyFrames.Add(new DiscreteDoubleKeyFrame(-30, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        needleTY.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380)))
        {
            KeySpline = new KeySpline(0.0, 0.0, 0.2, 1.0)
        });
        TopNeedleTrans.BeginAnimation(TranslateTransform.YProperty, needleTY);

        var needleBY = new DoubleAnimationUsingKeyFrames();
        needleBY.KeyFrames.Add(new DiscreteDoubleKeyFrame(30, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        needleBY.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380)))
        {
            KeySpline = new KeySpline(0.0, 0.0, 0.2, 1.0)
        });
        BottomNeedleTrans.BeginAnimation(TranslateTransform.YProperty, needleBY);

        // G. Destello Blanco Cegador en el clímax
        var flashAnim = new DoubleAnimationUsingKeyFrames();
        flashAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(660))));
        flashAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(740))));
        flashAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(860))));
        HackingWhiteFlash.BeginAnimation(UIElement.OpacityProperty, flashAnim);

        // H. Conmutación a la Matriz Dorada (Parrilla Limpia sin saturación de texto)
        var lockFade = new DoubleAnimationUsingKeyFrames();
        lockFade.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(735))));
        lockFade.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(740))));
        HackingLockStage.BeginAnimation(UIElement.OpacityProperty, lockFade);

        var matrixFade = new DoubleAnimationUsingKeyFrames();
        matrixFade.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(735))));
        matrixFade.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(740))));
        HackingMatrixStage.BeginAnimation(UIElement.OpacityProperty, matrixFade);

        AppendLog("[AETHERTERMINAL] ENLACE DE RED COMPLETADO: CÁMARA OV2640 VINCULADA.");

        // I. Disolución Final Hacia la Cámara
        var overlayFade = new DoubleAnimationUsingKeyFrames();
        overlayFade.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1050))));
        overlayFade.KeyFrames.Add(new SplineDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1250)))
        {
            KeySpline = new KeySpline(0.0, 0.0, 0.2, 1.0)
        });

        overlayFade.Completed += (s, e) =>
        {
            ResetHackingAnimation();
            _isHackingActive = false;
        };

        NierHackingOverlay.BeginAnimation(UIElement.OpacityProperty, overlayFade);
    }



    private BitmapSource? _pendingVideoFrame;
    private readonly object _videoLock = new();
    private bool _videoRenderQueued = false;

    private void OnVideoFrameReceived(BitmapSource frame, double fps, long totalFrames)
    {
        lock (_videoLock)
        {
            _pendingVideoFrame = frame;
            if (_videoRenderQueued) return;
            _videoRenderQueued = true;
        }

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
        {
            BitmapSource? frameToRender;
            lock (_videoLock)
            {
                frameToRender = _pendingVideoFrame;
                _videoRenderQueued = false;
            }

            if (frameToRender != null)
            {
                // 1. Mostrar de inmediato la imagen decodificada en el reproductor
                VideoPlayer.Source = frameToRender;

                if (NoSignalOverlay.Visibility != Visibility.Collapsed)
                {
                    NoSignalOverlay.Visibility = Visibility.Collapsed;
                }

                if (DotVideo.Fill != _brushGreen)
                {
                    DotVideo.Fill = _brushGreen;
                    TxtVideo.Text = "CÁMARA: ACTIVA";
                    TxtVideo.Foreground = _brushGreen;
                }


                HudFpsText.Text = $"FPS: {fps:F1}";
                HudLatencyText.Text = $"LATENCIA: {_streamPlayer.LatencyMs} ms";
                TxtDiagFps.Text = $"{fps:F1} FPS";
                TxtDiagLatency.Text = $"{_streamPlayer.LatencyMs} ms";
                TxtDiagTotalFrames.Text = $"{totalFrames:N0}";
            }
        });
    }


    private void OnVideoStatusChanged(string status)
    {
        Dispatcher.Invoke(() =>
        {
            bool active = _streamPlayer.IsStreaming;
            DotVideo.Fill = active ? _brushGreen : _brushRed;
            TxtVideo.Text = active ? "CÁMARA: ACTIVA" : "CÁMARA: OFF";
            TxtVideo.Foreground = active ? _brushGreen : _brushDim;
            if (!active)
            {
                BtnConnectStream.Content = "⚡ INICIAR CÁMARA [9S]";
            }
            AppendLog($"[VÍDEO] {status}");
        });
    }

    private void OnVideoStreamError(Exception ex)
    {
        Dispatcher.Invoke(() =>
        {
            AppendLog($"[ERROR VÍDEO] {ex.Message}");
        });
    }

    #endregion

    #region Lectura y Visualización del Mando Xbox (60 Hz)

    private void OnControllerConnectionChanged(bool connected)
    {
        Dispatcher.Invoke(() =>
        {
            DotMando.Fill = connected ? _brushGreen : _brushRed;
            TxtMando.Text = connected ? "MANDO XBOX: OK" : "MANDO XBOX: OFF";
            TxtMando.Foreground = connected ? _brushGreen : _brushDim;
            AppendLog(connected ? "[MANDO] Xbox Controller Detectado y Conectado." : "[MANDO] Mando de Xbox Desconectado.");
        });
    }

    private void OnControllerStateUpdated(ControllerState state)
    {
        // Enviar al servicio de transmisión hacia el coche
        _commService.ProcessControllerUpdate(state);

        // Actualizar interfaz HUD y Telemetría
        Dispatcher.Invoke(() =>
        {
            // 1. Stick Dirección
            TxtJoyX.Text = $"{state.LeftStickX:+0.00;-0.00;0.00}";
            BarSteer.Value = state.CalculatedSteeringAngle;
            TxtTelemetrySteer.Text = $"{state.CalculatedSteeringAngle}° ({state.CalculatedSteeringUS} µs)";
            HudSteeringText.Text = $"SERVO: {state.CalculatedSteeringAngle}° ({(state.LeftStickX >= 0.05f ? "DERECHA" : state.LeftStickX <= -0.05f ? "IZQUIERDA" : "CENTRO")})";

            // Transform cursor HUD (-85 a +85 px)
            SteerTransform.X = state.LeftStickX * 85.0;

            // 2. Triggers (Gatillos)
            TxtTrigRT.Text = $"{(int)(state.RightTrigger * 100)}%";
            TxtTrigLT.Text = $"{(int)(state.LeftTrigger * 100)}%";
            HudThrottleBar.Value = state.RightTrigger * 255;
            HudBrakeBar.Value = state.LeftTrigger * 255;

            // 3. Potencia y Telemetría Motor (Solo si el coche no envía telemetría directa)
            if (!_commService.IsConnected)
            {
                BarPWM.Value = state.CalculatedThrottlePWM;
                int pct = (int)(state.CalculatedThrottlePWM / 255.0 * 100);
                string directionLabel = state.IsForward ? "AVANCE" : state.IsReverse ? "RETROCESO" : "PARADO";
                TxtTelemetryPWM.Text = $"{state.CalculatedThrottlePWM} PWM ({pct}%) [{directionLabel}]";
                BarSteer.Value = state.CalculatedSteeringAngle;
                TxtTelemetrySteer.Text = $"{state.CalculatedSteeringAngle}° ({state.CalculatedSteeringUS} µs)";
            }

            // 4. Modos Sport y Tanque
            if (state.SportModeActive)
            {
                TxtSportState.Text = "SPORT [255]";
                TxtSportState.Foreground = _brushOrange;
                BadgeSportState.Background = new SolidColorBrush(Color.FromRgb(40, 30, 10));
                HudSportBanner.Visibility = Visibility.Visible;
            }
            else
            {
                TxtSportState.Text = "NORMAL [140]";
                TxtSportState.Foreground = _brushDim;
                BadgeSportState.Background = new SolidColorBrush(Color.FromRgb(32, 41, 56));
                HudSportBanner.Visibility = Visibility.Collapsed;
            }

            if (state.TankModeActive)
            {
                TxtTankState.Text = "TANQUE: ON";
                TxtTankState.Foreground = _brushCyan;
                BadgeTankState.Background = new SolidColorBrush(Color.FromRgb(10, 35, 45));
                HudTankBanner.Visibility = Visibility.Visible;
            }
            else
            {
                TxtTankState.Text = "TANQUE: OFF";
                TxtTankState.Foreground = _brushDim;
                BadgeTankState.Background = new SolidColorBrush(Color.FromRgb(32, 41, 56));
                HudTankBanner.Visibility = Visibility.Collapsed;
            }

            // 5. Botones Rápidos HUD
            BtnIndicatorA.Background = state.ButtonA ? _brushBtnActive : _brushBtnInactive;
            BtnIndicatorB.Background = state.ButtonB ? _brushRed : _brushBtnInactive;
            BtnIndicatorLB.Background = state.LeftShoulder ? _brushBtnActive : _brushBtnInactive;
            BtnIndicatorRB.Background = state.RightShoulder ? _brushBtnActive : _brushBtnInactive;
        });
    }

    private void OnEmergencyStopTriggered()
    {
        _commService.EmergencyStop();
    }

    private void OnHornTriggered()
    {
        _commService.TriggerHorn();
    }

    #endregion

    #region Acciones UI

    private void BtnToggleSport_Click(object sender, RoutedEventArgs e)
    {
        _controllerManager.SportMode = !_controllerManager.SportMode;
        AppendLog($"[MODO] Sport Mode cambiado a: {(_controllerManager.SportMode ? "ACTIVADO" : "DESACTIVADO")}");
    }

    private void BtnToggleTank_Click(object sender, RoutedEventArgs e)
    {
        _controllerManager.TankMode = !_controllerManager.TankMode;
        AppendLog($"[MODO] Modo Tanque Skid-Steer cambiado a: {(_controllerManager.TankMode ? "ACTIVADO" : "DESACTIVADO")}");
    }

    private void BtnEmergencyStop_Click(object sender, RoutedEventArgs e)
    {
        _controllerManager.TriggerHapticPulse(300, 1.0f, 1.0f);
        _commService.EmergencyStop();
    }

    private void BtnHorn_Click(object sender, RoutedEventArgs e)
    {
        _commService.TriggerHorn();
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        TxtConsoleLog.Clear();
    }

    private void AppendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            TxtConsoleLog.AppendText($"[{timestamp}] {message}\n");
            ScrollLog.ScrollToEnd();
        });
    }

    private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.IsRepeat) return;

        switch (e.Key)
        {
            case System.Windows.Input.Key.W:
            case System.Windows.Input.Key.Up:
                _commService.SendRaw("F140");
                AppendLog("[TECLADO] Avance (W)");
                break;
            case System.Windows.Input.Key.S:
            case System.Windows.Input.Key.Down:
                _commService.SendRaw("B140");
                AppendLog("[TECLADO] Retroceso (S)");
                break;
            case System.Windows.Input.Key.A:
            case System.Windows.Input.Key.Left:
                _commService.SendRaw("V46");
                AppendLog("[TECLADO] Giro Izquierda (A)");
                break;
            case System.Windows.Input.Key.D:
            case System.Windows.Input.Key.Right:
                _commService.SendRaw("V146");
                AppendLog("[TECLADO] Giro Derecha (D)");
                break;
            case System.Windows.Input.Key.Space:
                _commService.SendRaw("S");
                AppendLog("[TECLADO] Parada (Espacio)");
                break;
            case System.Windows.Input.Key.G:
                _commService.TriggerHorn();
                break;
            case System.Windows.Input.Key.H:
                Play9sHackingAnimation();
                break;
            case System.Windows.Input.Key.Escape:
                _commService.EmergencyStop();
                break;
        }
    }

    private void MainWindow_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case System.Windows.Input.Key.W:
            case System.Windows.Input.Key.Up:
            case System.Windows.Input.Key.S:
            case System.Windows.Input.Key.Down:
                _commService.SendRaw("S");
                break;
            case System.Windows.Input.Key.A:
            case System.Windows.Input.Key.Left:
            case System.Windows.Input.Key.D:
            case System.Windows.Input.Key.Right:
                _commService.SendRaw("V95"); // Recentrar servo
                break;
        }
    }

    #endregion
}