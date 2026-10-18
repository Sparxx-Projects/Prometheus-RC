# 🛠️ Prometheus MK-IV & Taskforce: Filosofía de Diseño y Arquitectura Hardware

**Autor:** Ernesto Torregrosa Palacio  
*Estudiante del Grado en Ingeniería Mecánica — UPV Campus d'Alcoi (EPSA) | Exalumno CEU Jesús-María*

Este documento detalla las decisiones técnicas de ingeniería detrás de la construcción física, cinemática y electrónica del rover **Prometheus MK-IV (Versión ONU / Salvamento)** y su evolución hacia el ecosistema táctico **Taskforce (Ignition)**.

---

## 1. Arquitectura Electrónica: El Sistema de Doble Alimentación Aislada

Una de las decisiones más críticas en el diseño de Prometheus ha sido la implementación de un sistema de **doble alimentación**. Aislar el circuito de tracción pesada es fundamental para evitar caídas de tensión bruscas (*brownouts*) que reiniciarían el microcontrolador y cortarían el enlace inalámbrico (Bluetooth o Wi-Fi UDP) en plena operación.

### La Solución Implementada:

* **Batería A (Lógica, Sensórica y Servomotores):** 
  * **Especificaciones:** Pack de celdas de litio **18650** con una capacidad total de **6600 mAh**. Su voltaje base (~3.7V nominal) atraviesa un módulo elevador regulado (*Boost/Step-up*) que garantiza una entrega fija, continua y limpia de **5V y hasta 3A**.
  * **Función:** Alimenta de forma centralizada al microcontrolador **ESP32 NodeMCU-32S**, a la sensórica I2C/digital y a los servomotores.
  * **Desglose de Consumos:** 
    * *Microcontrolador (ESP32):* Demanda un flujo constante de entre **150 mA y 240 mA** debido al procesamiento dual-core (240 MHz) y la transmisión inalámbrica ininterrumpida (Bluetooth BLE o Wi-Fi UDP).
    * *Cámara ESP32-CAM (en versión Taskforce):* Consumo medio de **180 mA a 260 mA** durante la codificación y transmisión activa de streaming MJPEG.
    * *Sensórica (TCS34725 + A3144):* Consumo residual inferior a **15 mA** con el LED de iluminación del sensor de color encendido.
    * *Servomotores (MG90S piñonería metálica):* Consumo altamente dinámico. En movimiento suave, el servo demanda entre **500 mA y 800 mA**, pero ante esfuerzos máximos o bloqueo mecánico (*Stall*) puede exigir picos de **1.5A a 2.5A**. La versión de carreras/salvamento alimenta 1 servo de dirección Ackermann, mientras que la versión con brazo robótico y compuerta gestiona hasta 4 actuadores simultáneos.
  * **Autonomía Calculada y Verificada:** Gracias a la gran reserva de 6600 mAh y al aislamiento de la línea, el sistema proporciona unas sólidas **3 horas de autonomía operativa ininterrumpida**, garantizando que la lógica jamás sufra reinicios por estrés mecánico.

* **Batería B (Tracción de Fuerza Bruta):** 
  * **Especificaciones:** Batería **LiPo 2S de 7.4V / 7.5V y 2200 mAh** (tasa de descarga de alta corriente).
  * **Función:** Dedicada **en exclusiva** a la etapa de potencia: el driver dual **ZK-5AD** y los motores de tracción de las ruedas traseras.
  * **Desglose de Consumos:** 
    * *Driver ZK-5AD (MOSFET Dual):* Pérdida interna por conmutación y polarización mínima de entre **20 mA y 35 mA**.
    * *Motorreductores JGA16-130 (Acero templado):* A velocidad de crucero constante consumen entre **250 mA y 450 mA** por canal. Sin embargo, en aceleraciones violentas desde parado o superación de pendientes, cada motor genera picos instantáneos de **más de 1.2A a 1.6A** para vencer la inercia del chasis.
  * **Autonomía en Escenario Real:** Intercalando aceleraciones, frenadas regenerativas por PWM y maniobras de exploración, la batería LiPo de 2200 mAh está calibrada para ofrecer **2 horas continuas de conducción táctica e intensa a velocidades de hasta 15 km/h**.

> **Nota Técnica de Seguridad:** Las líneas de masa (**GND**) de ambas baterías están obligatoriamente unificadas en un único punto común del chasis. Esto es indispensable para establecer la referencia de tensión de 0V que permite al driver ZK-5AD interpretar con precisión los pulsos PWM enviados desde los pines GPIO del ESP32.

---

## 2. Componentes Clave del Sistema

| Módulo | Componente Seleccionado | Especificaciones y Función |
| :--- | :--- | :--- |
| **Cerebro Principal** | **ESP32 NodeMCU-32S** | Dual-Core Xtensa LX6 (240 MHz). Stack Bluetooth nativo y receptor Wi-Fi UDP. |
| **Cámara / Visión** | **ESP32-CAM AI-Thinker** | Sensor OV2640. Servidor HTTP MJPEG en tiempo real hacia Taskforce GCS. |
| **Tracción** | **2x JGA16-130** | Reductora con piñonería íntegra de **acero templado** (torque elevado y cero holguras). |
| **Driver de Potencia** | **ZK-5AD** | Driver dual MOSFET de hasta 5A por canal con disipación pasiva y control bidireccional PWM. |
| **Dirección** | **TowerPro MG90S** | Servomotor digital de piñonería metálica (recorrido calibrado: 500–2400 μs en Pin 16). |
| **Sensor de Color (ONU)**| **Adafruit TCS34725** | Bus I2C, filtro IR integrado y LED blanco para reconocimiento de balizas de salvamento. |
| **Sensor Magnético (ONU)**| **A3144 (Efecto Hall)** | Detección digital de campos magnéticos y marcadores metálicos en pista. |
| **Periférico Manual** | **Mando Xbox Series X/S** | Conexión inalámbrica Bluetooth de baja latencia con gatillos progresivos RT/LT. |
| **Alimentación Tracción**| **Batería LiPo 2S 7.4V (2200 mAh)** | Entrega de alta corriente instantánea para arranques y subidas. |
| **Alimentación Lógica** | **Pack 18650 (6600 mAh) + Boost 5V** | Suministro filtrado de 5V/3A aislado de los motores. |

---

## 3. Ingeniería de Chasis 3D: Resiliencia y Zonas de Sacrificio

El chasis del Prometheus MK-IV ha sido diseñado combinando máxima ligereza con optimizaciones avanzadas de fabricación aditiva para resistir entornos hostiles:

* **Tornillería y Ensamblaje Estructural:** El montaje prescinde totalmente de pegamentos y utiliza tornillería métrica de precisión (**M2, M2.5 y M3**) con tuercas autoblocantes, consolidando las articulaciones mecánicas frente a fuertes vibraciones e impactos repetidos.
* **Geometría con Relleno Giroide (*Gyroid*):** Para resistir esfuerzos de torsión dinámicos sin añadir peso masivo, todas las piezas estructurales se imprimen con patrón interno **Giroide**. Este patrón matemático distribuye las cargas en tres dimensiones (X, Y, Z), absorbiendo energía cinética de forma isotrópica.
* **El PLA Llevado al Límite de sus Propiedades:** El chasis está fabricado en **PLA (Poliácido Láctico)** de alta densidad. Lejos de ser un plástico frágil, la ingeniería de nervaduras, redondeos de aristas y orientación de capas permite empujar el PLA hasta el límite absoluto de sus propiedades mecánicas, convirtiéndolo en una solución biodegradable, económica y de alto rendimiento.
* **Concepto de "Zonas de Sacrificio":** El coche no pretende ser un bloque indestructible. Ante un choque frontal o vuelco a máxima velocidad (15 km/h), las defensas y brazos exteriores están diseñados para flectar y fracturarse deliberadamente. Al romper el plástico impreso (reemplazable por unos céntimos en minutos), se disipa la energía del impacto y se **salvan los componentes críticos: la placa ESP32, los sensores, la cámara y los motores**.
* **Fabricación en Gran Formato (250 x 250 mm):** La pieza estructural principal del chasis aprovecha la práctica totalidad de una cama de impresión de **250 x 250 mm**, permitiendo fabricar el núcleo del vehículo de una sola pieza monolítica, eliminando puntos de fatiga por ensambles intermedios.

---

## 4. Cinemática de Dirección y Dinámica de Control

* **Dirección Cinemática Ackermann Pura:** A diferencia de vehículos de tracción diferencial tipo tanque que destruyen los neumáticos y consumen excesiva energía al girar derrapando, Prometheus integra geometría **Ackermann real**. La rueda interior del giro traza un ángulo más cerrado que la exterior, permitiendo curvas limpias a alta velocidad sin pérdida de tracción.
* **Aislamiento de Timers PWM en Software:** Para evitar interferencias eléctricas entre la señal PWM de velocidad de los motores (frecuencia de 1 kHz a 5 kHz) y el tren de pulsos del servomotor de dirección (50 Hz), el firmware asigna timers independientes del ESP32, eliminando cualquier temblor (*jitter*) en la dirección.
* **Mapeo Lineal en Gatillos de Xbox:** Los gatillos RT (acelerador) y LT (freno / marcha atrás) del mando de Xbox Series cuentan con zonas muertas calibradas por software y curvas de aceleración progresivas para permitir tanto maniobras milimétricas a 1 km/h como aceleraciones directas a 15 km/h.

---

## 5. Modularidad Táctica: Ecosistema Taskforce y Remolque Ignis

El diseño físico de Prometheus MK-IV integra puntos de anclaje mecánicos en la zaga y en el techo para expandir sus misiones:

1. **Enganche Rápido para Remolque Ignis:** Sistema de pivote esférico reforzado en la parte posterior para remolcar módulos de carga pesada, cajas de herramientas o kits de baterías auxiliares.
2. **Torreta de Sensores e IA:** Espacio superior previsto para acoplar la ESP32-CAM o el módulo superior de computación (**Arduino UNO Q / Edge AI**) para detección de víctimas y obstáculos.
3. **Compatibilidad Dual de Enlace:**
   * **Modo ONU (Directo):** ESP32 emparejado directamente por Bluetooth al mando de Xbox.
   * **Modo Taskforce (Ignition):** ESP32 enlazado por Wi-Fi UDP (< 2 ms) a la estación **Taskforce GCS**, permitiendo pilotaje FPV con HUD interactivo sobre la pantalla del PC.
