# Conectar BogoMaps con Cloud Firestore

## Qué está preparado

La app usa el proyecto público de `Resources/Raw/firebase-client.json`, y por defecto la base `(default)`. No usa cuenta de servicio ni SDK de administrador: cada petición REST lleva el ID token Firebase de la persona autenticada. El token se renueva cuando caduca; solo el token de renovación y los metadatos se conservan en SecureStorage.

- `bogoReports/{id}`: reportes que la persona decide publicar. Cada documento incluye el UID del autor, contenido, coordenadas, valoraciones y fechas asignadas por el servidor. No contiene correo, contraseñas, rutas de fotos, favoritos ni banderas de ejemplos.
- `bogoUsers/{uid}/favorites/{reportId}`: favoritos privados por cuenta.
- `bogoUsers/{uid}/confirmations/{reportId}`: una confirmación por cuenta y reporte; no se puede sobrescribir para incrementar un contador.
- Reportes privados y `[Prueba]`: continúan en JSON local, sin publicarse automáticamente.

El UID del autor es un identificador seudónimo visible a las cuentas que leen el documento. La opción anónimo oculta el nombre en la interfaz, no constituye anonimización completa de ese identificador. La app no publica nombres ni correos en esta primera integración.

## 1. Verificar y publicar las reglas

Antes de usar datos reales, valida las reglas con el emulador. Desde `firebase/security-tests`, con Node 22 y Java 21 disponibles:

```sh
npm ci
npm test
```

Las pruebas usan exclusivamente `demo-bogomaps` en `127.0.0.1:8080`. No crean usuarios ni reportes en el proyecto real. El emulador descarga un JAR oficial de Google en su primera ejecución.

Después, en Firebase Console → Firestore Database → Reglas, publica las reglas de `firebase/firestore.rules`. Si este proyecto tiene otras aplicaciones/colecciones, incorpora los bloques `bogoReports` y `bogoUsers` conservando sus reglas específicas. Una regla general de modo prueba que permita todo también permitiría saltarse estas restricciones: hay que retirar ese permiso general antes de considerar seguros estos datos.

Las reglas permiten leer a personas autenticadas; crear reportes propios válidos; resolver/reabrir solo al autor o a un moderador designado mediante custom claims del servidor; y acceder a favoritos y confirmaciones únicamente al titular. No permiten borrar reportes ni cambiar su autor o contenido desde el cliente. No hay interfaz de administración de moderadores en esta entrega.

## 2. Crear índices

Crea los dos índices de `firebase/firestore.indexes.json` en la consola (colección `bogoReports`, ámbito Collection):

| Primer campo | Segundo campo |
|---|---|
| `city` Ascendente | `createdAt` Descendente |
| `ownerId` Ascendente | `createdAt` Descendente |

Firestore añade el campo de nombre del documento en orden descendente como desempate. Espera a que ambos estén listos.

Como alternativa, desde la raíz del repositorio, con Firebase CLI y una cuenta con permisos de despliegue:

```sh
firebase login
firebase deploy --only firestore:rules,firestore:indexes
```

Si el proyecto ya tiene índices de otras colecciones, conserva sus definiciones y no aceptes borrar índices ajenos a BogoMaps.

`.firebaserc` apunta al proyecto del archivo recibido. Verifica ese proyecto antes de desplegar. Este comando publica reglas y puede cambiar permisos existentes: revisa el archivo si compartes el proyecto con otra aplicación. Las reglas NO se publican al hacer `git pull` ni al compilar MAUI.

Si tu base tiene otro nombre, añade `FirestoreDatabaseId` a `Resources/Raw/firebase-client.json` y configura `database` en la entrada firestore de `firebase.json` antes de desplegar. No necesitas compartir secretos para hacerlo.

## 3. Probar dos cuentas

1. Actualiza la rama y ejecuta la aplicación como antes. Accede con la cuenta A.
2. En Reportar crea un reporte nuevo y activa **Compartir con la comunidad**. Los reportes existentes siguen privados hasta que se implemente una migración explícita; no se suben en segundo plano.
3. Guarda/publica y comprueba que aparece un documento en `bogoReports` en la consola.
4. En otra instalación o después de cerrar sesión, entra con la cuenta B y pulsa **Actualizar comunidad** en Explorar. Debe aparecer el reporte de A.
5. Desde B guárdalo y confírmalo. Sus documentos deben estar bajo `bogoUsers/B`; A no debe ver el favorito de B como propio. B no debe poder resolver/reabrir el reporte de A.
6. Repite la confirmación o reinicia: sigue existiendo un único documento por usuario y reporte.
7. Con A resuelve el reporte; actualiza B y comprueba el nuevo estado.
8. Desconecta la red después de una lectura correcta. La copia local debe indicarse como copia; los cambios comunitarios requieren conexión y no se presentan como guardados si el servidor los rechaza.

## Límites de esta primera integración

- Las lecturas se hacen al entrar o actualizar, con páginas de hasta 50 documentos y botón Cargar más. No hay listener en tiempo real, cola de escritura offline ni sincronización automática de los borradores privados.
- La consulta inicial cubre reportes recientes de Bogotá. No se filtra por el rectángulo visible del mapa; una búsqueda geoespacial por celdas/geohash queda como ampliación para escalar el mapa.
- Los contadores del perfil corresponden a registros cargados, no a totales globales agregados del servidor.
- Las fotos permanecen locales. No se envían rutas privadas a Firestore ni se cargan imágenes a ningún bucket. Para fotografías compartidas falta Cloud Storage, reglas y su integración.
- Perfil/avatar siguen siendo locales por cuenta. La copia de reportes comunitarios se guarda, por UID, junto al JSON local con sufijo `.community.json`; no es una réplica completa de toda la base.
- Solo las cuentas autenticadas leen la comunidad; invitado conserva el flujo local.
- No se ha configurado App Check, moderación de contenidos ni límites por usuario para publicación. Deben evaluarse antes de abrir el servicio al público; las reglas de propietario no sustituyen esas funciones.
- Los errores de permisos, índices o conexión se muestran y conservan los datos locales. Los ejemplos de desarrollo no se publican.

## Evidencia de validación

Las pruebas .NET con HTTP simulado ejercitan autenticación, renovación de tokens, formato REST de Firestore, consultas limitadas y cursores, favoritos privados, precondición de confirmación, exclusión de fotos/demos y rechazo de acceso. Las pruebas del repositorio comprueban aislamiento de la copia comunitaria por cuenta.

En el entorno de implementación, la descarga del emulador desde `storage.googleapis.com` fue bloqueada por la política de red (HTTP 403); las pruebas de reglas están preparadas pero NO se han ejecutado allí. Tampoco se han publicado reglas/índices ni probado dos cuentas contra el proyecto real. No considerar la conexión lista para producción hasta completar esas comprobaciones. Windows necesita validación manual en Windows; compilar Android no la sustituye.
