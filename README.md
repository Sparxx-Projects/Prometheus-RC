# 🏎️ Prometheus RC & Taskforce Ecosystem
**Creador:** Ernesto Torregrosa Palacio  
*Estudiante del Grado en Ingeniería Mecánica — UPV Campus d'Alcoi (EPSA) | Exalumno CEU Jesús-María*
## 📖 Descripción del Proyecto
Este repositorio contiene el ecosistema oficial para el control del UGV radiocontrol **Prometheus**, diseñado tanto para misiones de salvamento sobre el terreno como para operaciones tácticas a distancia. El proyecto se articula en **dos modalidades principales de operación**:
1. 🌐 **Modalidad Taskforce / Ignition (Control en Tierra por Wi-Fi UDP):** Control remoto mediante estación en PC (**Taskforce GCS**), transmisión de vídeo en directo con HUD y enlace de comandos de bajísima latencia (< 2 ms).
2. 🦾 **Modalidad Classic / ONU (Operación Autónoma Directa por Bluetooth):** Control directo sobre el vehículo enlazando un mando inalámbrico de **Xbox Series X/S o Xbox One** nativamente al microcontrolador ESP32 sin necesidad de ordenador.
   
👉 **[Ver modelo 3D interactivo y piezas en Onshape](https://cad.onshape.com/documents/e0e2690dbaa761d20fdeb4b2/w/a619ad74b4e23a78b4f9217a/e/e99fb4568552d454a36312e6)**

---
## 🗂️ Módulos de Software y Firmwares
### 1. Rama Taskforce **[Ignition GCS](Prometheus_Taskforce_Suite_v1.0.zip)**
* 🖥️ **`Taskforce GCS`:** Estación de control en tierra desarrollada en **C# / .NET 8 (WPF)** con HUD táctico, telemetría bidireccional y soporte de mando de Xbox.
* 📡 **`PROMETHEUS_GCS_CLIENT.ino`:** Firmware para el ESP32 del vehículo receptor de paquetes de control Wi-Fi UDP y emisor de telemetría hacia el PC.
* 📷 **`PROMETHEUS_CAM_SERVER.ino`:** Firmware para la cámara AI-Thinker ESP32-CAM para streaming de vídeo MJPEG continuo al HUD de la estación.
### 2. Rama Classic **[Salvamento ONU & Competición](Prometheus_MK-IV_ONU_Suite.zip)**
* 📡 **`PROMETHEUS_SENSORS.ino` (Versión ONU):** Modo de exploración y salvamento. Lectura en tiempo real de sensor de color I2C TCS34725 y sensor magnético de efecto Hall para detección de balizas.
* 🦾 **`PROMETHEUS_ARM.ino`:** Modo manipulación para accionamiento seguro de brazo robótico, garra y compuerta de descarga.
* 🎮 **`PROMETHEUS_FULL_RC.ino`:** Modo radiocontrol completo con mapeo suave de aceleración y dirección.
* 🏁 **`PROMETHEUS_RACE.ino`:** Modo competición con respuesta 1:1 ultrarrápida sin filtros.
---
## ⚙️ Componentes de Hardware Utilizados
* **Microcontrolador principal:** ESP32
* **Cámara de a bordo (Rama Taskforce):** ESP32-CAM AI-Thinker
* **Motores de tracción:** 2x JGA16 de 6V
* **Driver de potencia:** ZK-5AD
* **Servo de dirección:** MG90S
* **Alimentación de tracción:** Batería LiPo 7.4V (2S)
* **Alimentación lógica:** Shield de baterías 18650 (5V regulados independientes)
* **Periférico de control:** Mando inalámbrico original Xbox Series X/S o Xbox One
* **Sensores (Versión ONU):** Sensor RGB Adafruit TCS34725 y sensor magnético A3144
---
## ⚖️ Licencia y Condiciones de Uso (CC BY-NC-SA 4.0)
Este proyecto está registrado y protegido bajo la licencia **Creative Commons Atribución - No Comercial - Compartir Igual 4.0 Internacional**.
Al descargar, visualizar o utilizar este repositorio, aceptas las siguientes condiciones:
### ✅ QUÉ PUEDES HACER:
* **Inspirarte y Aprender:** Eres libre de estudiar el código para entender cómo funciona la conexión ESP32-Xbox, el mapeo de control PWM y la estación GCS.
* **Modificar y Adaptar:** Puedes copiar el código, usar los motores que tú quieras, cambiar los pines, añadir nuevos sensores o mejorarlo para adaptarlo a tus propios proyectos de radiocontrol o salvamento.
* **Compartir:** Puedes distribuir tus versiones mejoradas libremente.
### ❌ QUÉ NO PUEDES HACER:
* 💰 **Uso Comercial:** Está **estrictamente prohibido** utilizar este código, o cualquier parte de él, para ganar dinero, vender proyectos, o darle un uso empresarial sin permiso previo del autor.
* 🕵️‍♂️ **Plagiar (Falta de Atribución):** Si presentas este código (o una versión modificada basada en él) en un entorno académico, escolar, certamen o feria de ciencias, **debes citar obligatoriamente a "Ernesto Torregrosa Palacio"** como el creador original del software base y la plataforma Prometheus. Apropiarse del código sin dar crédito constituye una violación de la licencia y plagio académico.
* 🔒 **Cerrar el código (Compartir Igual):** Si modificas este código y publicas tu versión, estás obligado a hacerlo bajo esta misma licencia para que la comunidad pueda seguir aprendiendo de forma abierta.
