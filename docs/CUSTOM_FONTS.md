# Guía de Uso de Tipografías Propias (.TTF / .OTF) en ClassicUO

Esta guía describe cómo utilizar fuentes TrueType/OpenType personalizadas en ClassicUO para textos de interfaz, banners (como `DiscoveredPlaceGump`) y futuros elementos gráficos.

---

## 1. Ubicación y Estructura de Fuentes

ClassicUO soporta la carga de fuentes vectoriales desde múltiples fuentes con el siguiente orden de prioridad:

### A. Directorio de Datos del Cliente (Recomendado para personalización sin recompilar)
Coloca los archivos `.ttf` o `.otf` en:
```
ClassicUO/
  └── Data/
        └── Client/
              └── Fonts/
                    ├── Avatar.ttf          <-- Tipografía clásica Ultima
                    └── CLF-UoClassic.ttf   <-- Tipografía alternativa
```

### B. Recursos Embebidos en el Binario (Compilación integrada)
Para que las fuentes viajen dentro del ejecutable `cuo.exe` sin depender de archivos externos:
1. Copia el archivo `.ttf` a `src/ClassicUO.Client/Resources/Fonts/`.
2. En `ClassicUO.Client.csproj`, agrégalo como `<EmbeddedResource>`:
```xml
<ItemGroup>
  <EmbeddedResource Include="$(ProjectDir)Resources\Fonts\Avatar.ttf" LogicalName="Avatar.ttf" />
</ItemGroup>
```

---

## 2. API de Renderizado de Fuentes (`SixLabors.Fonts` & `SixLabors.ImageSharp.Drawing`)

ClassicUO integra el framework vectorial de SixLabors para renderizar texto antialiased con efectos avanzados de sombreado y textura.

### Carga de Fuentes en C#
```csharp
using SixLabors.Fonts;

FontCollection collection = new FontCollection();
FontFamily family;

// Desde archivo:
family = collection.Add("Data/Client/Fonts/Avatar.ttf");

// O desde stream embebido:
using (Stream stream = assembly.GetManifestResourceStream("Avatar.ttf"))
{
    family = collection.Add(stream);
}

// Creación de fuentes en diferentes tamaños
Font bannerFont = family.CreateFont(54f, FontStyle.Regular);
Font subtitleFont = family.CreateFont(24f, FontStyle.Regular);
```

### Generación de Glifos y Medición
```csharp
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;

TextOptions options = new TextOptions(bannerFont)
{
    HorizontalAlignment = HorizontalAlignment.Center,
    VerticalAlignment = VerticalAlignment.Center
};

IPathCollection glyphs = TextBuilder.GenerateGlyphs("Trinsic", options);
FontRectangle bounds = glyphs.Bounds;
```

---

## 3. Efectos Visuales Disponibles

Al trabajar con `IPathCollection` se pueden aplicar transformaciones y filtros en capas:

1. **Sombra Proyectada (*Drop Shadow*)**:
   ```csharp
   ctx.Fill(Color.FromRgba(0, 0, 0, 200), glyphs.Translate(2.0f, 2.0f));
   ```
2. **Bisel 3D y Relieve (*Bevel & Emboss*)**:
   ```csharp
   // Destello especular superior/izquierdo
   ctx.Draw(new DrawingOptions(), new SolidPen(Color.FromRgb(255, 252, 238), 1.5f), glyphs.Translate(-1f, -1f));
   // Sombra inferior/derecha
   ctx.Draw(new DrawingOptions(), new SolidPen(Color.FromRgb(65, 48, 25), 2.5f), glyphs.Translate(1f, 1f));
   ```
3. **Gradientes Metálicos y Dorados**:
   ```csharp
   var gradientStops = new[]
   {
       new ColorStop(0.00f, Color.FromRgb(248, 238, 208)),
       new ColorStop(0.20f, Color.FromRgb(255, 252, 238)),
       new ColorStop(0.50f, Color.FromRgb(190, 155, 95)),
       new ColorStop(0.80f, Color.FromRgb(225, 192, 128)),
       new ColorStop(1.00f, Color.FromRgb(85, 60, 28))
   };
   var brush = new LinearGradientBrush(new PointF(0, bounds.Top), new PointF(0, bounds.Bottom), GradientRepetitionMode.None, gradientStops);
   ctx.Fill(brush, glyphs);
   ```

---

## 4. Conversión a `Texture2D` para FNA / XNA

Una vez generado el bitmap en memoria con ImageSharp, se convierte a `Texture2D` para dibujarse en el pipeline gráfico del juego:

```csharp
byte[] pixelBytes = new byte[width * height * 4];
image.CopyPixelDataTo(pixelBytes);

Texture2D texture = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
texture.SetData(pixelBytes);
```
