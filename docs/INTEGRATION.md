# Integración de main y funcionalidad BogoMaps

## Qué se comparó

- Principal: `nicoarciniegas/BogoMaps`, rama `main`, merge `221d554` (PR #1: `feature/mapa-explorar`).
- Funcionalidad: `feature/funcionalidad-bogomaps`, commit `f4f4b77`.
- Base común: `b6f6a5a`. Son desarrollos paralelos, no una versión que sustituye por completo a la otra.
- Resultado: `integration/main-y-funcionalidad`, creado sobre main e integrando la rama de funcionalidad con un merge normal. Las ramas originales y sus commits se conservan.

## Decisiones de los conflictos

| Área | Resolución |
|---|---|
| Diseño y navegación | Mantener el diseño Stitch revisado, marco móvil centrado, iconos y pantallas independientes de Guardados/Detalle. |
| Identidad y reportes | Mantener AuthenticationService, ReportData, LocalRepository y la conexión inicial a Firestore. Las reglas e índices siguen requiriendo publicación y validación como explica FIRESTORE_SETUP.md. |
| Dependencias | Adoptar Mapsui 5.1.0 de main, compatible con .NET 9; conservar MAUI Controls 9.0.50 y las dependencias de autenticación. Una sola referencia a Mapsui y una sola inicialización de SkiaSharp. |
| Mapa | Mantener el modelo unificado PedestrianReport y la comunidad; incorporar el centrado después de medir el control, la ubicación del dispositivo y la selección del punto cercano según filtros. Colores por gravedad/estado con su leyenda. |
| GPS y formulario | Conservar la selección manual en el mapa y añadir la dirección aproximada por geocodificación inversa, con alternativa de coordenadas cuando no esté disponible. |
| Fotos | Incorporar elección entre cámara y galería, previsualización y permisos de las plataformas. Cámara opcional en Android; galería disponible sin cámara. Las fotos compartidas siguen pendientes de Cloud Storage. |
| Datos de main | Conservar PuntoUrbano, puntos.json y el servicio anterior como compatibilidad. Importación explícita al modelo actual, sin cambiar los archivos originales. No mantener dos servicios de escritura activos en las pantallas. |

Se resolvieron conflictos en BogoMaps.csproj, MauiProgram.cs, ExplorarPage.xaml/.cs, ReportarPage.xaml/.cs y AndroidManifest.xml. No se empleó una estrategia global `ours` o `theirs`: se eligieron los cambios por función y se adaptaron al modelo actual.

## Archivos y datos anteriores

Si usaste el main anterior y guardaste reportes en el dispositivo, Perfil detecta `FileSystem.AppDataDirectory/reportes.json` y muestra **Revisar e importar reportes anteriores**. La importación:

- Solicita confirmar la cuenta/invitado destino; permanece privada y local.
- Mantiene el JSON anterior y las fotos de `fotos/` sin borrarlos ni reemplazarlos.
- Genera identificadores estables y no duplica ni sobrescribe registros ya importados.
- Mapea las categorías del formulario anterior a las actuales; conserva coordenadas, descripción, gravedad, fecha y foto existente.
- Informa registros incompatibles para revisión en lugar de descartarlos silenciosamente. La fuente queda disponible para recuperarlos.
- No traslada calificaciones ni cifras de confirmaciones de los ejemplos como si fueran votos reales.

En Debug, **Cargar ejemplos originales de Stitch** permite consultar el catálogo `puntos.json` de main. Se identifica como `[Prueba]`, permanece local, no se publica en Firestore y se retira con los otros datos de prueba.

## Cómo revisar antes de integrar a main

No reemplazar main copiando toda una carpeta ni forzar el push de una rama sobre otra. Usar un PR desde `integration/main-y-funcionalidad` hacia `main` en el repositorio principal.

En una copia cuyo `origin` sea el repositorio principal, primero comprueba `git status` y conserva cualquier cambio local. Para la primera revisión de la rama:

```sh
git fetch origin
git switch --track origin/integration/main-y-funcionalidad
dotnet run --project BogoMaps.csproj -f net9.0-windows10.0.19041.0 -c Debug
```

Si esa rama ya existe localmente, usa `git switch integration/main-y-funcionalidad` y `git pull --ff-only`.

Comprobar en Windows: login/invitado, navegación, mapa al abrir, filtros/zoom, GPS con y sin servicio de dirección, cámara/galería/cancelación, guardado local, datos de prueba e importación si existe un archivo anterior. La comunidad requiere las reglas/índices de Firestore; no se activan por hacer el merge.

Las pruebas del repositorio cubren compatibilidad de archivos anteriores, fotos, categorías, identificadores, duplicados, cambios ya guardados, datos fuera de zona, JSON corrupto, exclusión de ejemplos y las pruebas de autenticación/Firestore ya existentes. La compilación Android no sustituye una prueba de Windows, cámara real o dos cuentas conectadas al proyecto.

Si main vuelve a avanzar antes del PR, traerlo e integrarlo en esta rama de integración, resolver los cambios puntuales y repetir las comprobaciones. Evitar reescribir el historial de ramas que otros colaboradores estén usando. El PR de integración debe fusionarse solo después de la revisión y prueba en Windows.
