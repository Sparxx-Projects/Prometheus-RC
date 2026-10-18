# 🛡️ Política de Seguridad — Prometheus RC & Taskforce
## Versiones Soportadas
| Rama / Versión | Módulo | Estado |
| :--- | :--- | :--- |
| **Ignition (Taskforce)** | Taskforce GCS (.NET 8 WPF) + GCS Client UDP | :white_check_mark: Soportado |
| **Classic (ONU)** | Firmware ESP32 Bluetooth (Sensors, Arm, Full RC, Race) | :white_check_mark: Soportado |
| **Prototipos previos** | Versiones MK-I, MK-II y MK-III | :x: Obsoleto |
---
## ⚠️ Protocolos de Seguridad Física y Operativa
### 1. Failsafe en Modo Taskforce (Wi-Fi UDP)
* El cliente `PROMETHEUS_GCS_CLIENT.ino` cuenta con temporizador de seguridad: si no recibe paquetes UDP de control desde `Taskforce_GCS.exe` durante más de **300 ms**, los motores de tracción se detienen automáticamente para evitar colisiones por pérdida de señal.
### 2. Failsafe en Modo Classic / ONU (Bluetooth Directo)
* El firmware corta inmediatamente la señal de tracción PWM si se pierde el emparejamiento con el mando de Xbox Series.
### 3. Aislamiento Eléctrico (Protección contra Brownouts)
* Es obligatorio mantener físicamente separada la línea de alimentación de potencia (batería LiPo para motores) de la alimentación lógica (shield 18650 para el ESP32). Unir ambas en la misma batería sin regulador causará caídas de tensión bruscas que reinician el microcontrolador en plena marcha.
---
## 📩 Notificar un fallo de seguridad
Para reportar vulnerabilidades en los protocolos de desconexión de emergencia o fallos de control:  
**Ernesto Torregrosa Palacio** (`etpalicante@outlook.com`).
