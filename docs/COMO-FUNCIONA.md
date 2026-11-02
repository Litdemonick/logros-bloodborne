# Cómo funciona

## De dónde salen los datos

| Qué | De dónde |
|---|---|
| Trofeos ganados | `bbport\user\trophies.log`: una línea por trofeo, `id timestamp_unix`, que escribe bbport al desbloquearlo |
| Nombres, descripciones, grado, ocultos, expansión | `CUSA03173\sce_sys\trophy\trophy00.trp`, archivo del juego del propio jugador |
| Clave para leer el `.trp` | `trophyKey` en `bbport\user\config.toml` (la trae bbport) |
| ID de trofeos del juego | `sce_sys\npbind.dat` (offset `0x84`, 12 bytes) |
| Idioma | `language` en `%APPDATA%\bbport-launcher\settings.json` (mismo idioma que bbport) |
| Carpetas del juego y de usuario | `game_dir` / `user_dir` del mismo `settings.json` |
| Arte y tipografías | `sce_sys\icon0.png`, `sce_sys\pic1.png` y `bbport\_internal\assets\fonts` (Cinzel, Inter) |

### Lectura del `.trp`

El `.trp` tiene una cabecera (magic `0xDCA24D00`) y una tabla de entradas de 64 bytes (nombre de 32 bytes, offset y tamaño big-endian). Los XML de trofeos (`TROP.ESFM`, `TROP_xx.ESFM` por idioma) están cifrados:

1. `clave = AES-128-CBC(trophyKey, IV = 0)` aplicado al ID de trofeos completado a 16 bytes.
2. Cada `.ESFM`: los primeros 16 bytes son el IV; el resto se descifra con AES-128-CBC usando esa clave.

Los íconos (`TROPnnn.PNG`, `GR001.PNG`) van sin cifrar dentro del mismo archivo. Todo se lee en memoria y no se guarda nada del juego.

## Sincronización

- `trophies.log` es la fuente de verdad. Se lee en modo compartido, cada segundo, solo si cambió la fecha o el tamaño del archivo. Una línea sin terminar (bbport escribiéndola) se ignora.
- Si el archivo de golpe tiene menos trofeos, se espera a que se repita 3 veces antes de aceptarlo, para no reaccionar a una lectura a medias.
- `vistos-<carpeta>.txt` guarda qué trofeos ya se mostraron. Los nuevos se anuncian en orden cronológico, con el platino al final. Si bbport borra trofeos (otra partida), también se olvidan acá.
- La primera sincronización es manual. Con las notificaciones apagadas, los trofeos nuevos quedan pendientes y salen al volver a activarlas.

## Detección del juego

- Procesos cuya ruta está dentro de la carpeta de bbport. Los que ya se sabe que no lo son se guardan y no se vuelven a consultar.
- La ventana del juego es la de clase `SDL_app`; el launcher es Tk y se ignora.
- Los avisos de trofeos solo se muestran si esa ventana está al frente (`GetForegroundWindow`). Si no, esperan; si salís mientras uno se muestra, se pausa.
- El aviso se ubica y se ajusta de tamaño dentro del rectángulo de la ventana del juego (`DWMWA_EXTENDED_FRAME_BOUNDS`), y la sigue si la movés.

## Tiempo

bbport no registra el tiempo jugado (`play_time.txt` es de shadPS4 y no se actualiza). Cada sesión se identifica por la hora de arranque del proceso del juego y se guarda cada 30 s en `tiempo.txt`. Si el programa se reinicia, la misma sesión no se cuenta dos veces.

## Notificación

La tarjeta se dibuja una vez en memoria. En cada cuadro solo se pinta lo que se anima (destello, brillo y barra de cuenta regresiva), con temporizador de 1 ms durante la animación. La ventana no toma el foco y deja pasar los clics (`WS_EX_NOACTIVATE | WS_EX_TRANSPARENT`), así el juego no se entera.

## Datos del programa (`%APPDATA%\LogrosBloodborne`)

| Archivo | Para qué |
|---|---|
| `ajustes.ini` | configuración |
| `vistos-*.txt` | trofeos ya mostrados |
| `tiempo.txt` | sesiones medidas |
| `respaldos\trofeos-AAAA-MM-DD.txt` | respaldo diario, importable (se guardan 15) |
| `errores.log` | errores (se limita a 512 KB) |

Todo se escribe primero en un `.tmp` y se reemplaza de forma atómica, dejando una copia `.bak`.
