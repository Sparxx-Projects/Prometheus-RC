# Taskforce GCS (Ground Control Station)

Estación de control en tierra desarrollada en **C# / .NET 8 (WPF)** para la telemetría, control táctico y supervisión visual en tiempo real de la plataforma **Prometheus**.

---

## 🚀 Características Principales

* **Control Remoto Directo:** Integración nativa con mando de **Xbox Series** (vía XInput / NativeXInput) para dirección de precisión y tracción gradual.
* **HUD Táctico y Video en Vivo:** Reproductor MJPEG de baja latencia con retículas y superposición de telemetría.
* **Telemetría Bidireccional:** Comunicación serie / UDP de alta frecuencia para monitorización de estados (`PrometheusCommService`).
* **Failsafe y Seguridad:** Modos de parada de emergencia y control de enlace.

---

## 🛠️ Requisitos de Compilación

* Windows 10/11
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* Visual Studio 2022 o Visual Studio Code con C# Dev Kit

### Compilación y Ejecución
```bash
dotnet restore
dotnet build
dotnet run --project Taskforce_GCS.csproj
```
