# BogoMaps

Aplicación nativa .NET MAUI 9 de movilidad peatonal para Bogotá. Conserva la paleta y las fuentes Plus Jakarta Sans/Inter del diseño exportado de Google Stitch.

## Versión local y reportes compartidos

El acceso invitado permite usar la app sin cuenta. Google en Android y correo/contraseña se verifican con Firebase Authentication. Registro y recuperación usan Firebase; Apple queda pendiente de su configuración independiente. Los reportes son locales por defecto. Al crear uno puedes activar «Compartir con la comunidad» para guardarlo en Cloud Firestore. Las reglas e índices deben publicarse antes de probarlo con otras cuentas; sigue [la guía de Firestore](docs/FIRESTORE_SETUP.md).

La configuración cliente de Firebase se incluye en `Resources/Raw/firebase-client.json`; contiene identificadores públicos y una API key de cliente, no una cuenta de servicio. No se incluyen secretos OAuth ni claves privadas. Las contraseñas se envían únicamente al endpoint HTTPS de Firebase y no se guardan. SecureStorage conserva el token de renovación para restaurar la sesión. Los reportes locales, la copia comunitaria y el perfil se separan por UID; los reportes de invitado no se publican automáticamente al acceder con una cuenta.

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

El mapa requiere internet hacia `tile.openstreetmap.org`, con atribución OpenStreetMap y User-Agent de la aplicación. Sin red, los reportes y el formulario siguen siendo locales, pero no se garantiza un mapa base disponible. No hay descarga masiva ni mapas offline. El proveedor recibe las solicitudes de teselas del área visualizada. No se necesita clave Google Maps. Google Sign-In en Android requiere Google Play Services y un certificado SHA registrado en Firebase. La autenticación usa `identitytoolkit.googleapis.com` y `securetoken.googleapis.com`; la comunidad usa `firestore.googleapis.com`.

Android pide ubicación solo al pulsar GPS/Cerca de mí; las fotos se seleccionan con el selector del sistema. No se necesita permiso de cámara. iOS/MacCatalyst incluyen textos de permiso, pero requieren validación en macOS. Windows requiere validación en Windows.

## Verificación en dispositivo pendiente

1. Iniciar sin cuenta; denegar GPS y comprobar el mensaje y la alternativa manual.
2. Permitir GPS, centrar el mapa; desplazarse, hacer zoom y comprobar atribución y carga de teselas.
3. Crear un reporte con foto; abrir su marcador y comprobar datos y evidencia.
4. Guardar, confirmar y resolver; reiniciar la aplicación y comprobar persistencia y listas del perfil.
5. Probar cada filtro, búsqueda sin resultados, galería cancelada, ubicación fuera de Bogotá y modo sin conexión.
6. Comparar las pantallas con Stitch, incluyendo escalado de texto, navegación y lector de pantalla.

La compilación no demuestra que estos flujos de interfaz se hayan ejecutado. No hay emulador/dispositivo conectado en el entorno de implementación.

La primera conexión comunitaria usa Firestore para texto, favoritos y confirmaciones. Faltan la validación de reglas en emulador/proyecto real, fotografías compartidas, perfiles remotos, consultas geoespaciales y controles de abuso antes de abrirla al público. Consulta [alcance y activación](docs/FIRESTORE_SETUP.md).

## Composición visual de Stitch

Las pantallas de Explorar, Detalle, Reportar, Guardados y Perfil usan `Controls/StitchPage`: un marco móvil de hasta 440 unidades, centrado en ventanas Windows y ajustado al ancho disponible en teléfonos. La cabecera y la navegación inferior son controles MAUI compartidos; se ocultan las barras Shell duplicadas.

Explorar conserva el mapa, filtros, zoom y una tarjeta del punto seleccionado. El botón «Ver en mapa» del detalle vuelve al marcador correspondiente. Reportar permite tocar el mapa para ubicar el incidente; las coordenadas manuales siguen disponibles como alternativa. Las fotos adjuntas tienen previsualización. Perfil muestra avatar editable y contadores calculados sobre los reportes de la cuenta actual.

En Explorar, el mapa ocupa toda el área sobre la navegación inferior y permanece fijo detrás del panel. Arrastra la cabecera o el tirador del panel para pasar entre la franja inferior, media pantalla y la vista ampliada. También están disponibles «Más», «Menos» y «Ver mapa». Tocar un marcador abre la posición intermedia; «Más información» muestra el detalle, las valoraciones y las acciones de guardar, confirmar y resolver dentro del mismo panel. En la posición ampliada, su contenido se puede desplazar. En Windows se conserva el marco móvil centrado y se admite arrastrar la cabecera con el botón izquierdo del ratón; validar este gesto en Windows además de los botones y los gestos táctiles en dispositivo.

Las tarjetas de iluminación, pavimento, accesibilidad y ruido se alimentan de la evaluación opcional del formulario. Los registros anteriores muestran «Sin evaluar», sin inventar porcentajes. Las fotografías del mockup y sus cifras comunitarias no se cargan como si fueran datos reales; se muestran las fotos y aportes guardados por la persona.

Validación visual pendiente en Windows: revisar a distintos tamaños de ventana, con escalado de pantalla y texto, desplazamiento del mapa y del formulario, navegación inferior, reportes con/sin foto y todas las pestañas de Perfil. El entorno Linux puede compilar Android y ejecutar las pruebas del modelo, pero no capturar las pantallas nativas de Windows.

Los iconos se distribuyen como SVG locales y sus versiones rasterizadas las genera MAUI para cada plataforma. `CivicVisuals` y `IconBadge` aplican los colores semánticos de Stitch: azul para acciones/accesibilidad, ámbar para iluminación/obras, rojo para cruces y verde para puntos recomendados y confirmaciones. La pestaña activa tiene icono y texto azules. Las etiquetas de estado distinguen activo, gravedad y resuelto; los valores «Sin evaluar» se muestran neutros. Los estados también tienen texto, para que la información no dependa solo del color. No se necesita descargar una fuente de iconos al iniciar la aplicación.

## Datos de prueba y ubicación de los datos

Firebase Authentication guarda las cuentas. Los reportes privados y ejemplos siguen locales; los reportes publicados usan el esquema `bogoReports` de Firestore una vez activadas las reglas e índices de la guía. Añadir documentos arbitrarios en otra colección no los mostrará en la app.

Con `dotnet run --project BogoMaps.csproj -f net9.0-windows10.0.19041.0` se ejecuta normalmente Debug. En **Perfil → Pruebas locales · Desarrollo → Cargar datos de prueba** puedes añadir seis ejemplos de Bogotá. Cubren todas las categorías, tres favoritos, dos resueltos, dos confirmados y distintas valoraciones. Están identificados con `[Prueba]` y no reflejan condiciones reales ni se envían a Firebase. No incluyen fotos; para probar la galería crea un reporte propio con una foto adjunta.

La carga no duplica ejemplos ni reemplaza sus modificaciones. **Retirar datos de prueba** elimina exclusivamente esos ejemplos de la cuenta/invitado actual, incluidos sus cambios, y conserva los reportes propios. El panel solo es visible en compilaciones Debug.

Para comprobar el flujo: carga los ejemplos, abre Explorar, prueba filtros y búsqueda por «Prueba» o dirección, selecciona un marcador y abre el detalle, cambia favorito/confirmación/estado, revisa Guardados y Perfil, y reinicia para comprobar persistencia. Al cambiar de cuenta los datos son independientes; carga ejemplos en esa cuenta si los necesitas allí.

**Dónde está el archivo:** el panel muestra la ruta absoluta de `LocalRepository.StoragePath` y permite copiarla. El directorio se obtiene de `FileSystem.AppDataDirectory`; su ruta concreta depende de si Windows ejecuta la app empaquetada o sin empaquetar. Invitado usa `reports-v1.json`; cada cuenta usa `reports-<SHA256-del-UID>.json` en ese mismo directorio. Las fotos se almacenan allí y el JSON conserva sus rutas. El perfil usa Preferences y el token de sesión usa SecureStorage.

No necesitas editar el JSON para cargar ejemplos. Si vas a inspeccionarlo o editarlo manualmente, copia la ruta del panel, cierra la aplicación y conserva una copia de seguridad antes de modificarlo. Un archivo corrupto se informa como error en lugar de borrarse automáticamente. Estos archivos no se suben a GitHub ni se comparten con otro dispositivo.

## Integración con el repositorio principal

La rama `integration/main-y-funcionalidad` reúne el merge `221d554` de `nicoarciniegas/BogoMaps/main` y la rama de funcionalidad. Conserva la mejora de cámara/galería, dirección por GPS, catálogo original de ejemplos y Mapsui 5.1, junto con autenticación, Firestore, diseño y almacenamiento por cuenta. En Perfil se ofrece importar `reportes.json` anterior si existe, preservando los archivos y fotos originales. [Decisiones y pasos de revisión del merge](docs/INTEGRATION.md).
