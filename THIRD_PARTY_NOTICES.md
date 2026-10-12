# Avisos de terceros

EstudiApp MTC incluye o usa los siguientes componentes de terceros. Cada uno conserva su propia licencia; la
licencia del proyecto (ver [LICENSE](LICENSE)) no se aplica a ellos.

## Tipografías (SIL Open Font License 1.1)

Están en `EstudiAppMTC/Assets/_Project/Resources/UI/Fonts/`, cada una con el texto completo de la licencia al lado.

| Tipografía | Archivos | Copyright | Licencia |
|---|---|---|---|
| **Outfit** (modificada) | `Outfit-Regular.ttf`, `Outfit-SemiBold.ttf` | Copyright 2021 The Outfit Project Authors (https://github.com/Outfitio/Outfit-Fonts) | [OFL-Outfit.txt](EstudiAppMTC/Assets/_Project/Resources/UI/Fonts/OFL-Outfit.txt) |
| **Cormorant Garamond** | `CormorantGaramond-SemiBold.ttf` | Copyright 2015 The Cormorant Project Authors (github.com/CatharsisFonts/Cormorant) | [OFL-Cormorant.txt](EstudiAppMTC/Assets/_Project/Resources/UI/Fonts/OFL-Cormorant.txt) |
| **Noto Sans SC** (subconjunto) | `NotoSansSC-Subset.otf` | Copyright 2014-2021 Adobe (http://www.adobe.com/), with Reserved Font Name 'Source' | [OFL-NotoSansSC.txt](EstudiAppMTC/Assets/_Project/Resources/UI/Fonts/OFL-NotoSansSC.txt) |
| **Noto Sans SC** (completa, sólo para generar el subconjunto) | `Tools/vendor/NotoSansSC-Regular.otf` | Igual que la anterior | [Tools/vendor/OFL-NotoSansSC.txt](Tools/vendor/OFL-NotoSansSC.txt) |

Modificaciones (según la OFL 1.1):

- **Outfit**: `Tools/content/patch_fonts.py` le añade las vocales del pinyin con carón que no trae (ǐ ǒ ǔ Ǐ Ǒ Ǔ),
  compuestas con glifos de la propia fuente. Outfit no tiene nombres de fuente reservados (Reserved Font Name), así
  que la versión modificada puede conservar el nombre.
- **Noto Sans SC**: `Tools/content/build_content.py` genera un subconjunto con sólo los ideogramas usados en el
  temario. El único nombre reservado es «Source», que la fuente no usa.

Las tipografías se distribuyen junto con la app, pero no se venden por separado.

## PDFium

Lector de PDF. Binarios precompilados de [bblanchon/pdfium-binaries](https://github.com/bblanchon/pdfium-binaries)
(versión 157.0.8086.0), versionados en `EstudiAppMTC/Assets/Plugins/`:

- `PDFium/macOS/libpdfium.dylib`, `PDFium/Windows/pdfium.dll`, `PDFium/Linux/libpdfium.so`
- `Android/libs/arm64-v8a/libpdfium.so`
- `iOS/pdfium.framework`

Licencias:

- **PDFium**: Copyright 2014 The PDFium Authors. BSD de 3 cláusulas y Apache License 2.0
  ([licenses/pdfium.txt](EstudiAppMTC/Assets/Plugins/PDFium/licenses/pdfium.txt)).
- **pdfium-binaries** (scripts de compilación): Copyright 2014-2025 Benoit Blanchon, MIT
  ([LICENSE-pdfium.txt](EstudiAppMTC/Assets/Plugins/PDFium/LICENSE-pdfium.txt)).
- **Bibliotecas que PDFium lleva compiladas dentro** (abseil, AGG, dragonbox, fast_float, FreeType, HarfBuzz,
  ICU, Little CMS, libjpeg-turbo, OpenJPEG, libpng, LLVM libc, simdutf, zlib y, en Android, catapult,
  cpu_features y libunwind): cada licencia está en
  [EstudiAppMTC/Assets/Plugins/PDFium/licenses/](EstudiAppMTC/Assets/Plugins/PDFium/licenses/).

Las descargas originales de pdfium-binaries se guardan en `Tools/vendor/pdfium-*/`, que no se versiona.

## Newtonsoft.Json

Copyright (c) 2007 James Newton-King. Licencia MIT. No está incluido en el repositorio: Unity lo descarga como
paquete `com.unity.nuget.newtonsoft-json` (ver `EstudiAppMTC/Packages/manifest.json`), y las pruebas de .NET como
paquete NuGet `Newtonsoft.Json`.

## Herramientas de desarrollo (no se distribuyen con la app)

- **Unity 6** y sus paquetes/módulos (`com.unity.ugui` y módulos integrados), bajo los términos de Unity.
- **xUnit** (Apache 2.0) y **Microsoft.NET.Test.Sdk** (MIT), por NuGet, sólo para `Tools/DomainTests`.
- **fontTools** (MIT), **pypdf** (BSD-3) y **Pillow** (MIT-CMU), por pip, sólo para los scripts de `Tools/`.
