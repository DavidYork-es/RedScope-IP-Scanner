REDSCOPE - HISTORIAL DE VERSIONES
=================================

[2.1.0.3] - 07/11/2026 - CORRECCION DE COMPATIBILIDAD VB.NET 4.8
- Eliminadas las referencias a constantes de control y a funciones de conversion de
  codigo de caracteres de Visual Basic en la exportacion CSV de MainForm.
- Sustituidas por Convert.ToChar y literales Char, con comportamiento equivalente
  para CR, LF, TAB y el marcador Unicode U+FEFF.
- Codigo fuente revisado globalmente para comprobar que no se repiten esas llamadas.
- Versiones de AssemblyVersion, AssemblyFileVersion y manifiesto actualizadas a 2.1.0.3.
- LEEME, comprobaciones estaticas y checklist de aceptacion adaptados a 2.1.0.3.
- Sin cambios en interfaz, escaneo de red, conexiones, IP publica o portabilidad.
- No hay compilacion ejecutada: se requiere Visual Studio 2026 y .NET Framework 4.8.

[2.1.0.2] - 07/11/2026 - CORRECCIONES DE ROBUSTEZ ANTES DE PUBLICACION
- Reutilizada y liberada una única fuente GDI para los estados de la tabla; se evita crear Font en cada repintado.
- Exportación CSV: comillas y separadores conservados, saltos/tabulaciones normalizados y prevención de fórmulas (=,+,-,@).
- Límite de 1.024 puertos aplicado a entradas individuales, rangos y combinación final con el catálogo.
- IP pública: rechazo conservador de rangos privados, CGNAT, documentación, loopback, multicast y otros especiales IPv4/IPv6.
- Motor TCP: máximo de ocho trabajadores de puertos por IP, además del semáforo global de sockets; sin una tarea por puerto.
- Interfaz: cola concurrente con lotes de 128 resultados y progreso coalescido; sincronización final antes del resumen ARP.
- Sesiones diferenciadas para impedir que callbacks de barridos anteriores contaminen una nueva sesión.
- Cierre: si hay escaneo, se cancela y difiere la destrucción del formulario hasta concluir los trabajadores.
- Integración de rastros ARP: se cancela la incorporación de filas si se pide detener o cerrar.
- COMPILAR_RELEASE.cmd: se prefiere MSBuild de VS2026, manteniendo compatibilidad con VS2022.
- Metadatos, manifiesto, LEEME y script de compilación actualizados a 2.1.0.2.
- Mantiene el seudónimo DavidYork, sin empresa; no incorpora acceso al Registro ni persistencia adicional.
- Verificación estática y pruebas de consistencia realizadas; compilación y prueba en Windows pendientes.

[2.1.0.1] - 07/11/2026 - CORRECCION DE METADATOS DE AUTORIA
- Retirada la atribución empresarial previa en AssemblyCompany. El proyecto no declara afiliación empresarial.
- Conservado el seudonimo DavidYork en AssemblyCopyright y en Acerca de.
- Identificada la cuenta publica del autor como GitHub @DavidYork-es.
- Version de ensamblado, manifiesto, documentacion y script de compilacion actualizada.
- No se modifica la logica funcional ni se corrigen aun las incidencias de la auditoria 2.1.0.0.
- Codigo fuente pendiente de compilacion y pruebas finales en Windows con VS2026.

[2.1.0.0] - 07/11/2026 - CONSULTA VOLUNTARIA DE IP PUBLICA
- Añadido el boton IP PUBLICA junto al acceso de ayuda en la cabecera principal.
- Añadido un formulario compacto con resultado, proveedor, Actualizar, Copiar y Cerrar.
- La consulta se ejecuta exclusivamente al abrir el formulario o pulsar Actualizar.
- Consulta HTTPS con api.ipify.org y alternativa checkip.amazonaws.com.
- Validacion estricta de la respuesta como IPv4 o IPv6 antes de mostrarla.
- Timeout de 6 segundos por proveedor y cancelacion inmediata al cerrar la ventana.
- La IP obtenida permanece solo en memoria y nunca se escribe en Registro o fichero.
- No se envian rangos, resultados, direcciones MAC, nombres ni datos del escaneo.
- Textos de privacidad y Acerca de corregidos para declarar esta comunicacion opcional.
- Version de ensamblado, manifiesto y script de compilacion actualizada a 2.1.0.0.
- Entrega del proyecto fuente sin EXE precompilado.

[2.0.0.1] - 01/10/2026 - CORRECCION DEL SELECTOR DE REDES
- Corregida la superposicion de la rejilla bajo la cabecera de Redes IPv4 detectadas.
- La primera direccion IPv4 vuelve a quedar completamente visible y seleccionable.
- Restaurada la visibilidad permanente de los encabezados de las columnas.
- LocalNetworksForm reorganizado mediante TableLayoutPanel con tres filas independientes:
  cabecera, rejilla y pie de informacion/acciones.
- Eliminada la dependencia del orden Z y de BringToFront para distribuir los controles.
- Correccion resistente al redimensionado de la ventana y al escalado DPI de Windows.
- Sin cambios en deteccion de interfaces, rangos, escaneo o portabilidad sin Registro.
- Version de ensamblado, manifiesto y script de compilacion actualizada a 2.0.0.1.
- Entrega del proyecto fuente sin EXE precompilado.

[2.0.0.0] - 30/09/2026 - NUEVA GENERACION PORTABLE Y SIN ESTADO
- Eliminado completamente el acceso al Registro de Windows.
- Eliminado Core\UserPreferences.vb y todas sus llamadas de lectura y escritura.
- No se guardan rangos, puertos, timeouts, comentarios, preferencias ni historiales.
- Las claves dejadas por versiones 1.x no se consultan, modifican ni eliminan.
- Comentarios conservados unicamente en memoria durante la ejecucion actual.
- Eliminado el historial propio de 180 dias.
- Los rastros grises proceden solo de la cache ARP actual de Windows.
- Sustituida la ejecucion de arp.exe por lectura directa mediante GetIpNetTable.
- Perfil Basico reducido a 9 puertos: 22, 80, 135, 139, 443, 445, 3389, 8080 y 9100.
- Puerto 10001 conservado en el catalogo y en el perfil Industrial.
- Concurrencia inicial reducida de 48 a 12 equipos y de 128 a 24 conexiones de puerto.
- Limite global de conexiones de puerto simultaneas reducido a 64.
- Advertencia al seleccionar mas de 16 puertos y antes de superar 4.000 intentos TCP.
- Ayuda actualizada para declarar expresamente el comportamiento portable.
- Version de ensamblado, manifiesto y script de compilacion actualizada a 2.0.0.0.
- Entrega del proyecto fuente sin EXE precompilado.

[1.0.3.1] - 30/09/2026 - CORRECCION DE COMPILACION LINQ EN VB.NET
- Corregido BC32016 al contar equipos activos sobre Dictionary.ValueCollection.
- Sustituida la llamada ambigua Values.Count(predicado) por Enumerable.Count explicito.
- Sin cambios funcionales respecto a la version 1.0.3.0.
- Version de ensamblado, manifiesto y script de compilacion actualizada a 1.0.3.1.
- Entrega del proyecto fuente sin EXE precompilado.

[1.0.3.0] - 08/09/2026 - EQUIPOS HISTORICOS Y RASTROS ARP
- Historial persistente de IP, nombre, MAC, fabricante, origen y ultima deteccion.
- Conservacion local durante 180 dias en HKCU, sin telemetria ni ficheros auxiliares.
- Captura de relaciones IP/MAC presentes en la cache ARP de Windows.
- Fusion de rastros ARP con el historial, preservando nombres y fabricantes conocidos.
- Los historicos se muestran aunque este desactivado Mostrar sin respuesta.
- Filas historicas completas en gris claro para diferenciarlas de equipos activos.
- Estados separados Historico y Rastro ARP.
- Nueva columna Ultima evidencia y busqueda incluida sobre dicha columna.
- Contadores finales separados en activos, historicos y direcciones sin rastro.
- Version de ensamblado, manifiesto y script de compilacion actualizada a 1.0.3.0.
- Entrega del proyecto fuente sin EXE precompilado.

[1.0.2.0] - 07/09/2026 - CORRECCION DE DETECCION IPv4 LOCAL
- Corregida la omision completa de una interfaz cuando fallaba la lectura del gateway.
- Cada direccion, mascara y gateway se procesa de forma independiente.
- Segundo metodo de deteccion mediante Win32_NetworkAdapterConfiguration (WMI).
- Fusion de resultados de NetworkInterface y WMI por adaptador, IP y mascara.
- Las interfaces activas y con gateway aparecen antes que las desconectadas.
- Las direcciones automaticas APIPA 169.254.x.x se conservan, pero se relegan al final.
- El selector indica el origen de deteccion para facilitar futuros diagnosticos.
- Version de ensamblado, manifiesto y script de compilacion actualizada a 1.0.2.0.
- Entrega del proyecto fuente sin EXE precompilado.

[1.0.1.0] - 06/09/2026 - REDES LOCALES, PUERTO INDUSTRIAL Y AUTORIA
- Selector que muestra todas las direcciones IPv4 y grupos de red configurados del equipo.
- Compatibilidad visible con Ethernet, Wi-Fi, VPN, túneles e interfaces virtuales.
- Selección única del rango sugerido sin ocultar las demás interfaces detectadas.
- Puerto TCP 10001 identificado expresamente como Industrial / servidor serie.
- Sustitución de las siglas RS por el icono real en las cabeceras de la aplicación.
- Acerca de actualizado con creador, diseñador y propietario: DavidYork.
- Versión de ensamblado, manifiesto y script de compilación actualizada a 1.0.1.0.
- Entrega del proyecto fuente sin EXE precompilado, conforme a la preferencia indicada.

[1.0.0.0] - 06/09/2026 - PRIMERA VERSION FUNCIONAL
- Proyecto Windows Forms VB.NET para .NET Framework 4.8, Any CPU.
- Distribucion en un unico ejecutable sin dependencias de terceros.
- Motor asincrono con cola de direcciones y concurrencia configurable.
- Inicio, pausa/reanudacion y detencion mediante CancellationToken.
- Descubrimiento ICMP y TCP para detectar equipos con ping bloqueado.
- Un error o restriccion local de ICMP no interrumpe la exploracion TCP.
- Soporte de IP unica, rangos abreviados, rangos completos, CIDR y multiples bloques.
- Limites preventivos de 65.536 IP y 1.024 puertos.
- Resolucion DNS inversa con timeout.
- Recuperacion de MAC mediante SendARP en la red local.
- Identificacion de fabricantes habituales mediante prefijos OUI locales embebidos.
- Catalogo de servicios y perfiles Basico, Web + remoto, Industrial y Todos.
- Configuracion de puertos adicionales, timeouts, DNS, MAC y concurrencia.
- Interfaz compacta y profesional con resultados ordenables y filtrables.
- Columnas Estado, Nombre, IP, MAC, Fabricante, Latencia, Puertos y Comentario.
- Visualizacion opcional de direcciones sin respuesta.
- Comentarios persistentes por IP en el registro del usuario.
- Exportacion de las filas visibles a CSV UTF-8.
- Acciones contextuales HTTP, HTTPS, SMB, RDP, ping y copia al portapapeles.
- Deteccion automatica del rango IPv4 local con proteccion ante subredes extensas.
- Ejecucion de una sola instancia.
- Icono propio y metadatos Assembly incluidos.
- Ayuda integrada, LEEME y script de compilacion Release.
