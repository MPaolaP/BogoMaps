# BogoMaps

Aplicación nativa .NET MAUI 9 de movilidad peatonal para Bogotá. Conserva la paleta y las fuentes Plus Jakarta Sans/Inter del diseño exportado de Google Stitch.

## Versión local

El acceso invitado permite usar la app sin cuenta. Google en Android y correo/contraseña se verifican con Firebase Authentication. Registro y recuperación usan Firebase; Apple queda pendiente de su configuración independiente. Los reportes siguen siendo locales: autenticarse no publica ni sincroniza información comunitaria.

La configuración cliente de Firebase se incluye en `Resources/Raw/firebase-client.json`; contiene identificadores públicos y una API key de cliente, no una cuenta de servicio. No se incluyen secretos OAuth ni claves privadas. Las contraseñas se envían únicamente al endpoint HTTPS de Firebase y no se guardan. SecureStorage conserva el token de renovación para restaurar la sesión. Los reportes y el perfil se separan por UID; los reportes de invitado no se migran ni se comparten automáticamente al acceder con una cuenta.

- **Explorar:** mapa nativo Mapsui/OpenStreetMap con desplazamiento y zoom; marcadores de reportes, detalle al tocar un marcador, búsqueda por dirección/descripción y filtros por categoría. La búsqueda filtra tus reportes; no consulta un geocodificador.
- **Reportar:** ubicación GPS con permisos o coordenadas manuales (punto decimal o coma), referencia del lugar, seis categorías, gravedad, descripción y foto desde la galería. Valida una descripción de 1–250 caracteres, dirección y coordenadas dentro del área de Bogotá. El rectángulo de validación es aproximado, no una frontera administrativa.
- **Detalle:** favorito persistente, confirmación local única, resolver/reabrir e indicaciones peatonales mediante una aplicación de mapas instalada.
- **Guardados:** lista independiente de favoritos reales.
- **Perfil:** nombre/biografía locales editables, cifras de tus reportes y filtros por estado.

Los reportes de invitado se guardan como JSON en `FileSystem.AppDataDirectory/reports-v1.json`; las cuentas usan un archivo separado por hash de UID, con escrituras serializadas y reemplazo atómico. Las fotos se copian al almacenamiento privado; el perfil usa Preferences. Los datos corruptos no se reemplazan automáticamente. Desinstalar o borrar los datos de la app elimina esta información. Android tiene el backup desactivado para estos datos de ubicación. No se guardan contraseñas.

## Desarrollo

Requisitos: .NET SDK 9, workload `maui-android`, JDK 17 y Android SDK 35. Para compilar Android en Linux, evitar que la restauración incluya iOS/MacCatalyst:

```sh
dotnet build BogoMaps.csproj -f net9.0-android \
  -p:TargetFrameworks=net9.0-android \
  -p:AndroidSdkDirectory="$ANDROID_HOME" -p:JavaSdkDirectory="$JAVA_HOME" -p:JavaMaximumHeapSize=3G -m:2
```

En el entorno preparado, las herramientas están en `/workspace/.bogomaps/{dotnet,jdk,android-sdk}`, el caché NuGet en `/workspace/.bogomaps/nuget` y `DOTNET_CLI_HOME=/workspace/.bogomaps/cli`.

Salida: `bin/Debug/net9.0-android/com.companyname.bogomaps-Signed.apk`. Para instalar en un dispositivo autorizado: `adb install -r <ruta-al-apk>`.

```sh
dotnet run --project Tests/BogoMaps.LocalTests.csproj
```

Pruebas ejecutables sin frameworks externos: usan el repositorio de persistencia real con un adaptador de ruta temporal; comprueban reinicio, actualizaciones, escrituras concurrentes, validación y protección de datos corruptos.

## Red y plataformas

El mapa requiere internet hacia `tile.openstreetmap.org`, con atribución OpenStreetMap y User-Agent de la aplicación. Sin red, los reportes y el formulario siguen siendo locales, pero no se garantiza un mapa base disponible. No hay descarga masiva ni mapas offline. El proveedor recibe las solicitudes de teselas del área visualizada. No se necesita clave Google Maps. Google Sign-In en Android requiere Google Play Services y un certificado SHA registrado en Firebase. El resto del acceso usa `identitytoolkit.googleapis.com` y `securetoken.googleapis.com`.

Android pide ubicación solo al pulsar GPS/Cerca de mí; las fotos se seleccionan con el selector del sistema. No se necesita permiso de cámara. iOS/MacCatalyst incluyen textos de permiso, pero requieren validación en macOS. Windows requiere validación en Windows.

## Verificación en dispositivo pendiente

1. Iniciar sin cuenta; denegar GPS y comprobar el mensaje y la alternativa manual.
2. Permitir GPS, centrar el mapa; desplazarse, hacer zoom y comprobar atribución y carga de teselas.
3. Crear un reporte con foto; abrir su marcador y comprobar datos y evidencia.
4. Guardar, confirmar y resolver; reiniciar la aplicación y comprobar persistencia y listas del perfil.
5. Probar cada filtro, búsqueda sin resultados, galería cancelada, ubicación fuera de Bogotá y modo sin conexión.
6. Comparar las pantallas con Stitch, incluyendo escalado de texto, navegación y lector de pantalla.

La compilación no demuestra que estos flujos de interfaz se hayan ejecutado. No hay emulador/dispositivo conectado en el entorno de implementación.

Una futura fase comunitaria necesita backend, autorización, almacenamiento de fotos y sincronización. La autenticación Firebase no proporciona por sí sola esas capacidades. El modo local no simula esas capacidades.
