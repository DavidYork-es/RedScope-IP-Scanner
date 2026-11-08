Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Linq
Imports System.Net
Imports System.Net.NetworkInformation
Imports System.Net.Sockets
Imports System.Threading
Imports System.Threading.Tasks
Imports RedScope.Models

Namespace RedScope.Core
    ''' <summary>
    ''' Motor asíncrono del escáner. Usa una cola compartida y un número acotado de
    ''' trabajadores para mantener estable el consumo de memoria y de sockets.
    ''' </summary>
    Public NotInheritable Class NetworkScanner
        ''' <summary>
        ''' Contenedor por referencia para que Interlocked pueda actualizar los contadores
        ''' desde métodos Async, que no admiten parámetros ByRef.
        ''' </summary>
        Private NotInheritable Class ScanCounters
            Public Completed As Integer
            Public Online As Integer
        End Class

        ''' <summary>
        ''' Ejecuta el barrido completo y entrega cada resultado mediante callbacks.
        ''' Los callbacks se ejecutan desde hilos de trabajo; la interfaz debe reenviarlos
        ''' a su hilo mediante BeginInvoke.
        ''' </summary>
        Public Async Function ScanAsync(addresses As IList(Of IPAddress),
                                        settings As ScanSettings,
                                        pauseGate As ManualResetEventSlim,
                                        cancellationToken As CancellationToken,
                                        resultCallback As Action(Of ScanResult),
                                        progressCallback As Action(Of ScanProgress)) As Task
            If addresses Is Nothing Then Throw New ArgumentNullException(NameOf(addresses))
            If settings Is Nothing Then Throw New ArgumentNullException(NameOf(settings))
            If pauseGate Is Nothing Then Throw New ArgumentNullException(NameOf(pauseGate))

            Dim queue As New ConcurrentQueue(Of IPAddress)(addresses)
            Dim portGate As New SemaphoreSlim(Math.Max(1, settings.MaxPortConcurrency))
            Dim workerCount As Integer = Math.Max(1, Math.Min(settings.MaxConcurrency, addresses.Count))
            Dim workers As New List(Of Task)(workerCount)
            Dim counters As New ScanCounters()

            ' Cada trabajador toma la siguiente IP sólo cuando la pausa está liberada.
            For index As Integer = 1 To workerCount
                workers.Add(Task.Run(Function() WorkerAsync(queue, settings, pauseGate, portGate,
                                                            cancellationToken, resultCallback,
                                                            progressCallback, addresses.Count,
                                                            counters), cancellationToken))
            Next

            Try
                Await Task.WhenAll(workers).ConfigureAwait(False)
            Finally
                portGate.Dispose()
            End Try
        End Function

        ''' <summary>
        ''' Consume direcciones de la cola hasta terminar o recibir cancelación.
        ''' Interlocked mantiene contadores correctos sin bloquear a los trabajadores.
        ''' </summary>
        Private Async Function WorkerAsync(queue As ConcurrentQueue(Of IPAddress),
                                           settings As ScanSettings,
                                           pauseGate As ManualResetEventSlim,
                                           portGate As SemaphoreSlim,
                                           cancellationToken As CancellationToken,
                                           resultCallback As Action(Of ScanResult),
                                           progressCallback As Action(Of ScanProgress),
                                           total As Integer,
                                           counters As ScanCounters) As Task
            Dim address As IPAddress = Nothing
            While queue.TryDequeue(address)
                cancellationToken.ThrowIfCancellationRequested()
                pauseGate.Wait(cancellationToken)

                Dim result As ScanResult = Await ProbeAddressAsync(address, settings, portGate, cancellationToken).ConfigureAwait(False)
                If resultCallback IsNot Nothing Then resultCallback(result)

                Interlocked.Increment(counters.Completed)
                If result.IsOnline Then Interlocked.Increment(counters.Online)
                Dim done As Integer = Volatile.Read(counters.Completed)
                Dim online As Integer = Volatile.Read(counters.Online)

                If progressCallback IsNot Nothing Then
                    progressCallback(New ScanProgress With {
                        .Total = total,
                        .Completed = done,
                        .Online = online,
                        .Offline = done - online,
                        .CurrentAddress = address.ToString()
                    })
                End If
            End While
        End Function

        ''' <summary>
        ''' Combina ICMP y TCP. El TCP se prueba incluso si falla el ping para descubrir
        ''' firewalls o dispositivos que deshabilitan las respuestas ICMP.
        ''' </summary>
        Private Async Function ProbeAddressAsync(address As IPAddress,
                                                 settings As ScanSettings,
                                                 portGate As SemaphoreSlim,
                                                 cancellationToken As CancellationToken) As Task(Of ScanResult)
            Dim result As New ScanResult With {.Address = address}

            Dim pingTask As Task(Of Long?) = ProbePingAsync(address, settings.PingTimeoutMs, cancellationToken)
            Dim portsTask As Task(Of List(Of Integer)) = ProbePortsAsync(address, settings.Ports,
                                                                         settings.TcpTimeoutMs, portGate,
                                                                         cancellationToken)

            ' Se esperan juntas para observar correctamente cualquier cancelación o fallo
            ' de ambas ramas y no dejar una tarea huérfana.
            Await Task.WhenAll(New Task() {pingTask, portsTask}).ConfigureAwait(False)
            result.LatencyMs = pingTask.Result
            result.PingSucceeded = result.LatencyMs.HasValue
            result.OpenPorts = portsTask.Result
            result.IsOnline = result.PingSucceeded OrElse result.OpenPorts.Count > 0

            If result.IsOnline Then
                Dim dnsTask As Task(Of String)
                If settings.ResolveDns Then
                    dnsTask = ResolveHostNameAsync(address, settings.DnsTimeoutMs, cancellationToken)
                Else
                    dnsTask = Task.FromResult(String.Empty)
                End If

                ' SendARP es síncrono y muy breve; se ejecuta en un trabajador para no
                ' retrasar la continuación de las operaciones de red asíncronas.
                Dim macTask As Task(Of String)
                If settings.ResolveMac Then
                    macTask = Task.Run(Function() NativeNetwork.TryGetMacAddress(address), cancellationToken)
                Else
                    macTask = Task.FromResult(String.Empty)
                End If

                Await Task.WhenAll(New Task() {dnsTask, macTask}).ConfigureAwait(False)
                result.HostName = dnsTask.Result
                result.MacAddress = macTask.Result
                result.Manufacturer = OuiResolver.Resolve(result.MacAddress)
            End If

            Return result
        End Function

        ''' <summary>
        ''' Realiza un eco ICMP con un paquete pequeño y TTL acotado.
        ''' </summary>
        Private Shared Async Function ProbePingAsync(address As IPAddress,
                                                     timeoutMs As Integer,
                                                     cancellationToken As CancellationToken) As Task(Of Long?)
            cancellationToken.ThrowIfCancellationRequested()
            Try
                Using ping As New Ping()
                    Dim options As New PingOptions(32, False)
                    Dim buffer As Byte() = {82, 101, 100, 83, 99, 111, 112, 101}
                    Dim reply As PingReply = Await ping.SendPingAsync(address, timeoutMs, buffer, options).ConfigureAwait(False)
                    cancellationToken.ThrowIfCancellationRequested()
                    If reply IsNot Nothing AndAlso reply.Status = IPStatus.Success Then Return reply.RoundtripTime
                End Using
            Catch ex As PingException
                ' Un fallo ICMP es esperado en hosts filtrados; TCP aún puede descubrirlos.
            Catch ex As ObjectDisposedException
                ' Puede aparecer durante una cancelación; se trata como host no respondedor.
            Catch ex As OperationCanceledException
                Throw
            Catch ex As Exception
                ' Determinadas políticas locales pueden impedir ICMP. El barrido TCP
                ' continúa siendo válido y no debe abortarse por esa restricción.
            End Try
            Return Nothing
        End Function

        ''' <summary>
        ''' Explora los puertos mediante un máximo de ocho trabajadores por dirección.
        ''' El semáforo global limita además los sockets activos. No se crean 1.024
        ''' tareas en espera por cada IP cuando el perfil es intensivo.
        ''' </summary>
        Private Shared Async Function ProbePortsAsync(address As IPAddress,
                                                      ports As IList(Of Integer),
                                                      timeoutMs As Integer,
                                                      portGate As SemaphoreSlim,
                                                      cancellationToken As CancellationToken) As Task(Of List(Of Integer))
            If ports Is Nothing OrElse ports.Count = 0 Then Return New List(Of Integer)()

            Dim queue As New ConcurrentQueue(Of Integer)(ports)
            Dim openPorts As New ConcurrentBag(Of Integer)()
            Dim workers As New List(Of Task)(Math.Min(8, ports.Count))
            For index As Integer = 1 To Math.Min(8, ports.Count)
                workers.Add(ProbePortQueueAsync(address, queue, openPorts, timeoutMs,
                                                portGate, cancellationToken))
            Next
            Await Task.WhenAll(workers).ConfigureAwait(False)
            Return openPorts.OrderBy(Function(port) port).ToList()
        End Function

        ''' <summary>
        ''' Extrae un puerto cada vez y sólo crea una conexión cuando tiene turno.
        ''' La bolsa concurrente acumula únicamente los puertos TCP confirmados abiertos.
        ''' </summary>
        Private Shared Async Function ProbePortQueueAsync(address As IPAddress,
                                                          queue As ConcurrentQueue(Of Integer),
                                                          openPorts As ConcurrentBag(Of Integer),
                                                          timeoutMs As Integer,
                                                          portGate As SemaphoreSlim,
                                                          cancellationToken As CancellationToken) As Task
            Dim port As Integer
            While queue.TryDequeue(port)
                cancellationToken.ThrowIfCancellationRequested()
                Dim found As Integer? = Await ProbePortAsync(address, port, timeoutMs, portGate,
                                                              cancellationToken).ConfigureAwait(False)
                If found.HasValue Then openPorts.Add(found.Value)
            End While
        End Function

        ''' <summary>
        ''' Prueba una conexión TCP con timeout sin dejar TcpClient abiertos.
        ''' </summary>
        Private Shared Async Function ProbePortAsync(address As IPAddress,
                                                     port As Integer,
                                                     timeoutMs As Integer,
                                                     portGate As SemaphoreSlim,
                                                     cancellationToken As CancellationToken) As Task(Of Integer?)
            Await portGate.WaitAsync(cancellationToken).ConfigureAwait(False)
            Try
                Using client As New TcpClient(AddressFamily.InterNetwork)
                    Dim connectTask As Task = client.ConnectAsync(address, port)
                    Dim timeoutTask As Task = Task.Delay(timeoutMs, cancellationToken)
                    Dim completedTask As Task = Await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(False)

                    cancellationToken.ThrowIfCancellationRequested()
                    If completedTask Is connectTask Then
                        Try
                            Await connectTask.ConfigureAwait(False)
                            If client.Connected Then Return port
                        Catch ex As SocketException
                            ' Conexión rechazada: el puerto está cerrado, no es un error global.
                        End Try
                    End If
                End Using
            Catch ex As ObjectDisposedException
                ' Cierre esperado del socket durante timeout o cancelación.
            Finally
                portGate.Release()
            End Try
            Return Nothing
        End Function

        ''' <summary>
        ''' La API DNS no ofrece cancelación en .NET Framework 4.8. Se aplica un timeout
        ''' mediante WhenAny y se ignora el resultado tardío para no frenar la interfaz.
        ''' </summary>
        Private Shared Async Function ResolveHostNameAsync(address As IPAddress,
                                                           timeoutMs As Integer,
                                                           cancellationToken As CancellationToken) As Task(Of String)
            Try
                Dim lookupTask As Task(Of IPHostEntry) = Dns.GetHostEntryAsync(address)
                Dim timeoutTask As Task = Task.Delay(timeoutMs, cancellationToken)
                Dim completedTask As Task = Await Task.WhenAny(lookupTask, timeoutTask).ConfigureAwait(False)
                cancellationToken.ThrowIfCancellationRequested()
                If completedTask Is lookupTask Then
                    Dim entry As IPHostEntry = Await lookupTask.ConfigureAwait(False)
                    If entry IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(entry.HostName) Then Return entry.HostName
                End If
            Catch ex As SocketException
                ' No todos los equipos publican DNS inverso; se conserva la IP como identidad.
            Catch ex As ArgumentException
                ' Una respuesta DNS malformada no debe afectar al resultado principal.
            End Try
            Return String.Empty
        End Function
    End Class
End Namespace
