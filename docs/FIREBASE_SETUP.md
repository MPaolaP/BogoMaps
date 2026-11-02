# Configurar Firebase Authentication para BogoMaps

1. Crea o selecciona un proyecto en https://console.firebase.google.com/.
2. En Authentication → Comenzar → Método de acceso, habilita Google y elige el correo de asistencia. Habilita también Correo electrónico/contraseña para registro y recuperación. No hace falta habilitar acceso telefónico en esta fase.
3. En Configuración del proyecto → Tus apps, registra una aplicación Android con el paquete exacto `com.companyname.bogomaps`.
4. Registra las huellas siguientes de la firma **de desarrollo del entorno actual**:

```text
SHA-1: 83:35:72:A9:05:B0:60:08:76:E3:D6:4A:05:A8:A4:17:ED:E6:35:90
SHA-256: DA:03:43:E6:87:27:7E:1E:4C:9A:BC:D7:56:E5:2B:B2:84:DA:CA:6F:E7:A8:97:45:F1:BB:B4:0C:00:77:A1:4B
```

5. Descarga de nuevo `google-services.json` después de habilitar Google y registrar las huellas. Comparte ese archivo de configuración cliente para continuar la integración. Si no incluye un cliente OAuth de tipo Web (`client_type: 3`), comparte también su ID público desde Google Cloud → APIs y servicios → Credenciales.

Este archivo es configuración de la aplicación cliente, no una cuenta de servicio. No compartas un JSON que contenga `private_key`, secretos OAuth, contraseñas ni el archivo de firma. No hace falta crear Firestore, Storage ni desplegar un backend para esta fase de autenticación.

Las huellas anteriores no sirven para cualquier APK: al firmar desde Visual Studio con otro certificado debes añadir sus huellas; para publicar habrá que añadir las del certificado de distribución o de Play App Signing. Si cambia el paquete de la aplicación, registra el nuevo paquete en Firebase.

Para iOS se necesita registrar por separado su bundle ID y obtener `GoogleService-Info.plist`; no reutilizar la configuración Android.

## Estado de la aplicación

La pantalla conserva los controles de acceso del mockup. Hasta integrar y verificar Firebase, Google, Apple, correo, registro y recuperación informan que falta la configuración y no crean una sesión. El acceso invitado mantiene disponible el modo local. La integración y la prueba de cuentas reales siguen pendientes.
