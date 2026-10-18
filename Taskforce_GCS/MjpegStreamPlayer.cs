using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Taskforce_GCS.Services;

public class MjpegStreamPlayer : IDisposable
{
    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _cts;
    private Task? _streamTask;

    public bool IsStreaming { get; private set; }
    public string StreamUrl { get; set; } = "http://192.168.4.1:81/stream";

    public double CurrentFps { get; private set; }
    public long TotalFrames { get; private set; }
    public double BytesPerSecond { get; private set; }
    public long LatencyMs { get; private set; }

    public event Action<BitmapSource, double, long>? FrameReceived;
    public event Action<string>? StatusChanged;
    public event Action<Exception>? StreamError;

    public MjpegStreamPlayer()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(3),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            EnableMultipleHttp2Connections = false
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan // Keep open for streaming
        };
    }

    public void Start(string? url = null)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            StreamUrl = url.Trim();
        }

        Stop();

        _cts = new CancellationTokenSource();
        _streamTask = Task.Run(() => StreamLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _streamTask?.Wait(500);
        }
        catch { }
        _streamTask = null;
        _cts?.Dispose();
        _cts = null;
        IsStreaming = false;
        StatusChanged?.Invoke("DESCONECTADO");
    }

    private async Task StreamLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                StatusChanged?.Invoke($"CONECTANDO A {StreamUrl}...");

                using var request = new HttpRequestMessage(HttpMethod.Get, StreamUrl);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!response.IsSuccessStatusCode)
                {
                    // Si puerto 80 da 404, probar puerto 81 (estándar de ESP32-CAM)
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound && !StreamUrl.Contains(":81"))
                    {
                        var uri = new Uri(StreamUrl);
                        StreamUrl = $"{uri.Scheme}://{uri.Host}:81/stream";
                        StatusChanged?.Invoke($"Puerto 80 devolvió 404. Probando puerto 81: {StreamUrl}...");
                        continue;
                    }

                    StatusChanged?.Invoke($"ERROR HTTP: {(int)response.StatusCode} {response.ReasonPhrase}");
                    await Task.Delay(2000, ct);
                    continue;
                }

                IsStreaming = true;
                StatusChanged?.Invoke("STREAM EN DIRECTO (OK)");

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                await ReadMjpegFramesAsync(stream, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                IsStreaming = false;
                StatusChanged?.Invoke($"REINTENTANDO TRAS ERROR: {ex.Message}");
                StreamError?.Invoke(ex);
                await Task.Delay(100, ct);
            }
        }

        IsStreaming = false;
        StatusChanged?.Invoke("DESCONECTADO");
    }

    private async Task ReadMjpegFramesAsync(Stream stream, CancellationToken ct)
    {
        byte[] buffer = new byte[65536];
        using var frameBuffer = new MemoryStream();

        bool inFrame = false;
        byte prevByte = 0;

        int fpsFrameCount = 0;
        long fpsBytesCount = 0;
        var fpsStopwatch = Stopwatch.StartNew();
        var frameLatencyWatch = Stopwatch.StartNew();

        while (!ct.IsCancellationRequested)
        {
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
            if (bytesRead == 0)
            {
                throw new IOException("Stream de vídeo cerrado por la ESP32-CAM.");
            }

            fpsBytesCount += bytesRead;

            for (int i = 0; i < bytesRead; i++)
            {
                byte currentByte = buffer[i];

                if (!inFrame)
                {
                    // Detectar SOI (Start of Image): 0xFF, 0xD8
                    if (prevByte == 0xFF && currentByte == 0xD8)
                    {
                        inFrame = true;
                        frameLatencyWatch.Restart();
                        frameBuffer.SetLength(0);
                        frameBuffer.WriteByte(0xFF);
                        frameBuffer.WriteByte(0xD8);
                    }
                }
                else
                {
                    frameBuffer.WriteByte(currentByte);

                    // Detectar EOI (End of Image): 0xFF, 0xD9
                    if (prevByte == 0xFF && currentByte == 0xD9)
                    {
                        inFrame = false;
                        LatencyMs = frameLatencyWatch.ElapsedMilliseconds;

                        // Decodificar imagen JPEG
                        byte[] jpegBytes = frameBuffer.ToArray();
                        if (jpegBytes.Length > 100)
                        {
                            try
                            {
                                var bitmap = CreateBitmapSource(jpegBytes);
                                TotalFrames++;
                                fpsFrameCount++;

                                if (fpsStopwatch.ElapsedMilliseconds >= 1000)
                                {
                                    CurrentFps = (double)fpsFrameCount / (fpsStopwatch.ElapsedMilliseconds / 1000.0);
                                    BytesPerSecond = (double)fpsBytesCount / (fpsStopwatch.ElapsedMilliseconds / 1000.0);
                                    fpsFrameCount = 0;
                                    fpsBytesCount = 0;
                                    fpsStopwatch.Restart();
                                }

                                FrameReceived?.Invoke(bitmap, CurrentFps, TotalFrames);
                            }
                            catch
                            {
                                // Descartar frames corruptos por ruido Wi-Fi sin detener la aplicación
                            }
                        }

                        frameBuffer.SetLength(0);
                    }
                }

                prevByte = currentByte;
            }
        }
    }

    private static BitmapSource CreateBitmapSource(byte[] jpegData)
    {
        using var ms = new MemoryStream(jpegData);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze(); // Permite cruzar de hilo secundario a la UI sin bloqueos
        return bitmap;
    }

    public void Dispose()
    {
        Stop();
        _httpClient.Dispose();
    }
}
