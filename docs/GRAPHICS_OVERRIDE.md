# Guía de Override de Gráficos de Mayor Resolución (HD CustomArt)

ClassicUO incluye un motor de reemplazo gráfico nativo (`CustomArtOverride`) que permite sustituir los gráficos clásicos del cliente (land tiles, statics/items y texmaps) por imágenes de **alta resolución (HD)** en formato **PNG** (con canal alfa completo de 8-bit) o **BMP**.

---

## 1. Estructura de Directorios

Los gráficos de reemplazo se ubican en la carpeta `CustomArt`:
```
ClassicUO/
  └── Data/
        └── Client/
              └── CustomArt/
                    ├── art/
                    │    ├── land/       <-- Gráficos de terreno (suelo / land tiles)
                    │    └── static/     <-- Gráficos de estáticos, objetos, items, paredes
                    └── texmaps/         <-- Texturas de mapas / texmaps
```
*(Nota: ClassicUO también busca de forma automática en `<DirectorioDeInstalacionUO>/CustomArt/`)*

---

## 2. Nomenclatura de Archivos e IDs

Los archivos deben nombrarse según el **ID hexadecimal** o **decimal** del arte a reemplazar:

### Formatos soportados:
- **Hexadecimal** (Recomendado): `0x0002.png`, `0x0F51.png`, `0x1F03.png`
- **Decimal**: `2.png`, `3921.png`, `7939.png`

### Ejemplos prácticos:
| Tipo de Arte | Carpeta | Nombre del Archivo | Gráfico Original |
| :--- | :--- | :--- | :--- |
| **Land (Suelo)** | `Data/Client/CustomArt/art/land/` | `0x0002.png` | Baldosa de hierba / pasto |
| **Static (Item)** | `Data/Client/CustomArt/art/static/` | `0x0F51.png` | Daga (Dagger) en HD |
| **Static (Item)** | `Data/Client/CustomArt/art/static/` | `0x0EED.png` | Pila de monedas de oro |
| **Texmap** | `Data/Client/CustomArt/texmaps/` | `0x0001.png` | Textura de terreno |

---

## 3. Especificaciones y Características de las Imágenes

1. **Mayor Resolución (HD Scaling)**:
   - El motor comprueba si las dimensiones de la imagen de reemplazo son mayores o iguales al tamaño original.
   - Si la imagen es más grande (ej. 2x, 4x o resolución libre), ClassicUO la escala y dibuja automáticamente en alta fidelidad.
2. **Canal Alfa / Transparencia**:
   - **PNG**: Soporta transparencia suave completa de 8-bit (gradientes de opacidad, sombras suaves, antialiasing).
   - **BMP de 24-bit**: Utiliza color magenta puro `RGB(255, 0, 255)` o negro `RGB(0, 0, 0)` como clave de transparencia.
3. **Prioridad de Formato**:
   - Si existen un `.png` y un `.bmp` para el mismo ID, el formato `.png` tiene prioridad automática.

---

## 4. Modo de Activación y Depuración

El motor indexa las carpetas al iniciar el cliente. En los logs de ClassicUO (`Logs/`) se registrará una línea indicando la cantidad de archivos cargados:
```
CustomArt: X land, Y static, Z texmap overrides
```
No se requiere ninguna configuración adicional en `settings.json`; cualquier archivo colocado en las rutas indicadas será cargado inmediatamente al arrancar el cliente.
