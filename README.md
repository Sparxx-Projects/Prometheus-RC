# 🏎️ Prometheus RC
**Creador:** Ernesto Torregrosa Palacio  
*Estudiante del Grado en Ingeniería Mecánica — UPV Campus d'Alcoi (EPSA) | Exalumno CEU Jesús-María*
## 📖 Descripción del Proyecto
Este repositorio contiene el código fuente oficial para el sistema de control del rover radiocontrol **Prometheus (Versión MK-IV / ONU)** y su ecosistema táctico **Ignition (Taskforce)**. El sistema utiliza un microcontrolador **ESP32** para conectarse de forma nativa a un mando inalámbrico de **Xbox Series X/S o Xbox One** mediante Bluetooth, ofreciendo un control de altísima precisión tanto en operaciones directas sobre el terreno como a través de la estación en tierra.
👉 **[Ver modelo 3D interactivo y piezas en Onshape](https://cad.onshape.com/documents/e0e2690dbaa761d20fdeb4b2/w/a619ad74b4e23a78b4f9217a/e/e99fb4568552d454a36312e6)**
El código base está dividido en módulos independientes dependiendo de la misión a realizar:
* 🏁 **PROMETHEUS RACE (Versión MK-IV):** Modo de competición y respuesta rápida. Elimina cualquier filtro de suavizado para ofrecer una respuesta 1:1 ultrarrápida entre los gatillos del mando y la potencia de los motores. Diseñado para maximizar la agilidad y velocidad en pista.
* 📡 **PROMETHEUS SENSORS (Versión MK-IV ONU):** Modo de exploración y salvamento. Integra lectura del entorno mediante sensores (Magnético y sensor de color I2C TCS34725). Incluye rutinas de telemetría por Bluetooth Serial para monitorizar datos del coche en tiempo real.
* 🦾 **PROMETHEUS ARM (Versión MK-IV):** Modo de manipulación. Diseñado para controlar cargas pesadas (Brazo robótico, Garra y Compuerta). Aísla las señales PWM de tracción de las de los servos para garantizar la máxima estabilidad eléctrica durante tareas de fuerza bruta, e incluye macros automáticas (como secuencias de descarga).
* 🖥️ **[Taskforce GCS - Versión Ignition](Taskforce_GCS/):** Estación de control en tierra avanzada desarrollada en **C# / .NET 8 (WPF)**. Diseñada para operaciones remotas tácticas de la Taskforce: telemetría en tiempo real, HUD estilo videojuego sobre transmisión de vídeo MJPEG y control directo mediante mando de Xbox Series.
## ⚙️ Componentes de Hardware Utilizados
* **Microcontrolador:** ESP32
* **Motores de tracción:** 2x JGA16 de acero
* **Driver de motores:** ZK-5AD
* **Servo de dirección:** MG90S (Piñonería metálica)
* **Batería de tracción:** LiPo 7.4V
* **Alimentación lógica:** Shield de baterías 18650
* **Periférico de control:** Mando inalámbrico Xbox Series X/S / Xbox One (Bluetooth)
* **Sensores (Versión SENSORS):** Adafruit TCS34725 (Color I2C) y sensor magnético A3144 (Efecto Hall)
* **Estación de tierra (GCS):** PC Windows con .NET 8 Runtime
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
