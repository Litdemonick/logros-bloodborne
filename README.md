<div align="center">

# 🏆 Logros Bloodborne

**Tus trofeos de Bloodborne sobre el juego, mientras jugás con [bbport](https://bbport.de).**

Avisos con sonido · lista de progreso hacia el Platino · sincronización con bbport

[![Release](https://img.shields.io/github/v/release/Litdemonick/logros-bloodborne?label=versi%C3%B3n&color=c9a86a)](https://github.com/Litdemonick/logros-bloodborne/releases/latest)
[![Descargas](https://img.shields.io/github/downloads/Litdemonick/logros-bloodborne/total?label=descargas&color=8a1c20)](https://github.com/Litdemonick/logros-bloodborne/releases)
[![Licencia](https://img.shields.io/github/license/Litdemonick/logros-bloodborne?label=licencia)](LICENSE)
![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078d4)

**[⬇ Descargar la última versión](https://github.com/Litdemonick/logros-bloodborne/releases/latest)**

</div>

> Proyecto de fans, **no oficial**. No está afiliado a Sony, FromSoftware ni a bbport.
> **No incluye ningún archivo del juego**: nombres, íconos y tipografías se leen de la instalación de bbport de cada jugador.

---

## Índice

- [Qué hace](#qué-hace)
- [Instalación](#instalación)
- [Primer uso](#primer-uso)
- [Atajos de teclado](#atajos-de-teclado)
- [Configuración](#configuración)
- [Cómo se sincroniza con bbport](#cómo-se-sincroniza-con-bbport)
- [Preguntas frecuentes](#preguntas-frecuentes)
- [Privacidad y seguridad](#privacidad-y-seguridad)
- [Actualizar y desinstalar](#actualizar-y-desinstalar)
- [Compilar desde el código](#compilar-desde-el-código)
- [Estructura del proyecto](#estructura-del-proyecto)
- [Licencia](#licencia)

## Qué hace

| | |
|---|---|
| 🎮 **Avisos sobre el juego** | Cada trofeo aparece arriba del juego, en ventana, ventana sin bordes o pantalla completa, y siempre dentro de su área. Solo sale cuando el juego está al frente: si cambiás de ventana, se pausa y sigue en orden cuando volvés. |
| 🔊 **Sonido** | Un sonido por grado (bronce, plata, oro, platino) y fanfarrias para los hitos: 25/50/75/90 %, todos los bronces, platas u oros, y la expansión completa. También podés usar tus propios sonidos. |
| 📜 **Lista de trofeos** | Mismo estilo que el launcher de bbport: progreso, contadores por grado, filtros (todos, obtenidos, me faltan, recientes), buscador, orden y fechas legibles ("hoy 23:08", "ayer", "el martes"). |
| 🔄 **Sincronización** | La primera vez la hacés vos con un botón. Después es automática: los trofeos que ganaste con el programa cerrado salen en orden, una sola vez. |
| ⏱️ **Tiempo** | Sesión en vivo mientras jugás y tiempo total registrado. bbport no guarda el tiempo jugado; el programa lo mide mientras está abierto. |
| 🛡️ **Respaldos** | Respaldo automático diario de tus trofeos, más exportar e importar cuando quieras. |
| ⬆️ **Actualizaciones** | Te avisa cuando hay versión nueva; la ves e instalás desde Configuración → Acerca de, siempre con tu confirmación. La descarga se verifica con SHA-256. |

## Instalación

**Requisitos:** Windows 10 u 11, y [bbport](https://bbport.de) con tu copia de Bloodborne. No hace falta instalar nada más: .NET Framework 4 ya viene con Windows.

1. Descargá **`Instalar-Logros-Bloodborne.exe`** de la [última release](https://github.com/Litdemonick/logros-bloodborne/releases/latest).
2. Ejecutalo. No pide permisos de administrador.
   - Si Windows muestra *"Windows protegió tu PC"*, tocá **Más información → Ejecutar de todas formas**. Aparece porque el instalador no tiene una firma digital paga, no porque tenga algo malo.
3. Dejá marcado **Iniciar con Windows** (recomendado): así el programa siempre está escuchando y no te perdés ningún trofeo.

## Primer uso

1. Abrí **Logros Bloodborne**. Busca solo tu bbport, esté donde esté: lo guardado, el launcher de bbport, bbport abierto en ese momento, accesos directos y las carpetas típicas de todos tus discos. Si no lo encuentra, te muestra qué carpeta elegir (la que tiene `Bloodborne.exe`; sirve también la de arriba) y, si abrís bbport mientras tanto, lo detecta solo.
2. Tocá **Sincronizar con bbport**. Vas a ver tus trofeos, y los que ya tenías salen en orden como avisos, una sola vez. Si en esa PC falta la clave de trofeos (bbport no la trae), funciona igual: ves tus trofeos con su ícono y grado, y los nombres aparecen cuando se agrega la clave (la busca sola, o en **Configuración → Sincronización → Elegir archivo…** elegís el `keys.json` exportado desde otra PC).
3. Jugá normalmente. Cerrar la ventana **no** cierra el programa: sigue en segundo plano, con el ícono de la copa al lado del reloj. Para cerrarlo del todo: clic derecho en la copa → **Salir**.

## Atajos de teclado

Funcionan con el juego abierto y no le sacan el foco, salvo abrir la lista.

| Atajo | Qué hace |
|---|---|
| `Ctrl + Alt + L` | Abrir o esconder la lista de trofeos |
| `Ctrl + Alt + O` | Resumen rápido sobre el juego: progreso, Platino y sesión |
| `Ctrl + Alt + M` | Mover el aviso: arrastralo y hacé doble clic o Enter para guardar |
| `Ctrl + Alt + S` | Sonido sí / no |
| `Ctrl + Alt + T` | Probar una notificación |

Con mando no hay atajos, pero todo está en el menú del ícono de la copa.

## Configuración

Está dentro de la misma ventana, en el botón **Configuración**.

| Pestaña | Opciones |
|---|---|
| **Notificaciones** | Activar/desactivar, posición o moverla a mano, tamaño, duración, velocidad de la animación, probar |
| **Sonido** | Sonido sí/no, volumen, probar cada sonido, sonidos propios (`trophy`, `platinum`, `hito` en `.wav` o `.mp3`, dentro de `bbport\user\custom_trophy`) |
| **Sincronización** | Sincronizar ahora, rehacer desde cero, trofeos perdidos uno por uno o en resumen, exportar/importar, cambiar la carpeta de bbport |
| **General** | Idioma de los trofeos (por defecto, igual que bbport), formato de hora 12/24 h, trofeos ocultos, atajos, iniciar con Windows |
| **Acerca de** | Versión, buscar actualizaciones, GitHub, reportar un problema, tus datos |

## Cómo se sincroniza con bbport

- bbport anota cada trofeo en `bbport\user\trophies.log`. El programa lo **lee** cada segundo, sin bloquearlo ni modificarlo.
- Recuerda qué trofeos ya te mostró, así nunca se repiten. Si ganaste trofeos con el programa cerrado o las notificaciones apagadas, salen después, en el orden en que los ganaste.
- Si usás **Traer logros de otra instalación** en el launcher de bbport, o **Importar** en el programa, los nuevos también salen en orden.

Más detalles técnicos en [docs/COMO-FUNCIONA.md](docs/COMO-FUNCIONA.md).

## Preguntas frecuentes

**No me aparecen los avisos.**
Revisá que hayas tocado *Sincronizar con bbport* al menos una vez, que las notificaciones estén activadas, y que el **juego esté al frente**: los avisos esperan si estás en otra ventana. Con `Ctrl + Alt + T` podés probar uno.

**¿Funciona con shadPS4 u otros emuladores?**
No. Está hecho para bbport, porque lee su registro de trofeos y detecta su ventana.

**¿Me pueden banear o romper la partida?**
No toca el juego ni tus partidas: solo lee el registro de trofeos que escribe bbport.

**¿Por qué el tiempo jugado es menor que el real?**
bbport no guarda el tiempo jugado, así que el programa lo mide solo mientras está abierto. Por eso dice *tiempo registrado*.

**Algo falla.**
Abrí un [issue](https://github.com/Litdemonick/logros-bloodborne/issues) y adjuntá `%APPDATA%\LogrosBloodborne\errores.log` (en *Configuración → Acerca de → Ver mis datos*).

## Privacidad y seguridad

- Solo **lee** el registro de trofeos de bbport. La única escritura en bbport es **Importar**, que pide confirmación y antes guarda una copia (`trophies.log.antes-de-importar`).
- La única conexión a internet es **buscar actualizaciones** en GitHub, y se puede desactivar. Cada descarga se verifica con la huella **SHA-256** que publica GitHub antes de instalarse.
- El programa no tiene base de datos ni ejecuta lo que escribís: el buscador solo compara texto en memoria y además se limpia.
- Tus datos quedan en `%APPDATA%\LogrosBloodborne` y se guardan de forma atómica, con copia `.bak` por si se corta la luz.

## Actualizar y desinstalar

- **Actualizar:** el programa avisa cuando hay versión nueva. En **Configuración → Acerca de** ves cuál es y tocás **Instalar**; después de confirmar, se abre el instalador y el programa se cierra. Al terminar, el instalador ofrece abrirlo de nuevo. Tus ajustes y trofeos se conservan. Nunca se instala nada sin que lo confirmes. También podés instalar la release nueva encima.
- **Desinstalar:** desde *Configuración de Windows → Aplicaciones*. Se quitan el programa, el inicio automático y los ajustes. Se conservan tus respaldos de trofeos en `%APPDATA%\LogrosBloodborne\respaldos`.

## Compilar desde el código

```powershell
git clone https://github.com/Litdemonick/logros-bloodborne
cd logros-bloodborne
.\build.ps1
```

- Usa el compilador de C# que trae Windows (.NET Framework 4.x): no necesita Visual Studio.
- Si tenés [Inno Setup 6](https://jrsoftware.org/isinfo.php), también arma el instalador en `dist\`.

**Publicar una versión:** subí `AssemblyFileVersion` en `src/LogrosBloodborne/Programa.cs`, agregá `docs/NOTAS-vX.Y.Z.md`, hacé commit y empujá la etiqueta `vX.Y.Z`. GitHub Actions compila y crea la release.

## Estructura del proyecto

```
src/LogrosBloodborne/
  Programa.cs        entrada, instancia única, registro de errores, ícono
  App.cs             lógica principal: sincronización, detección del juego, avisos, menú
  Atajos.cs          atajos de teclado globales
  Actualizador.cs    actualizaciones desde GitHub Releases (verificación SHA-256)
  Datos/             modelo, lectura de bbport y del .trp, ajustes, tiempo, fechas, escritura segura
  Audio/Sonido.cs    sonidos sintetizados (o propios del jugador)
  UI/                tema, aviso, lista, cabecera, ventana, configuración y controles
installer/           script de Inno Setup y LEEME para el usuario
assets/icono.ico     ícono (lo dibuja el propio programa: build.ps1 -RegenerarIcono)
docs/                funcionamiento interno y notas de cada versión
.github/workflows/   compilación y publicación automáticas
```

## Licencia

[MIT](LICENSE) © Litdemonick.
Bloodborne es marca de Sony Interactive Entertainment. Las tipografías Cinzel e Inter (OFL) se cargan desde la instalación de bbport de cada jugador y no se distribuyen en este repositorio.
