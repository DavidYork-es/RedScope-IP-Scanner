Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Net
Imports System.Net.Http
Imports System.Net.Sockets
Imports System.Threading
Imports System.Threading.Tasks

Namespace RedScope.Core
    ''' <summary>
    ''' Resultado mínimo de la consulta. Se mantiene únicamente en memoria y permite que
    ''' la interfaz indique de qué servicio procede la respuesta sin conservar historial.
    ''' </summary>
    Public NotInheritable Class PublicIpResult
        Public Property Address As String
        Public Property Source As String
    End Class

    ''' <summary>
    ''' Consulta la dirección pública únicamente cuando el usuario lo solicita.
    ''' No transmite datos del escaneo, no usa el Registro y no escribe ningún archivo.
    ''' </summary>
    Public NotInheritable Class PublicIpService
        Private NotInheritable Class Endpoint
            Public Sub New(name As String, uri As String)
                Me.Name = name
                Me.Uri = uri
            End Sub

            Public ReadOnly Property Name As String
            Public ReadOnly Property Uri As String
        End Class

        ' Se emplean dos servicios sencillos para que un fallo puntual del primero no
        ' inutilice la función. Ambos reciben inevitablemente la IP de origen de la conexión.
        Private Shared ReadOnly Endpoints As IReadOnlyList(Of Endpoint) =
            New List(Of Endpoint) From {
                New Endpoint("api.ipify.org", "https://api.ipify.org"),
                New Endpoint("checkip.amazonaws.com", "https://checkip.amazonaws.com")
            }

        Private Sub New()
            ' Clase de servicio estática: no mantiene estado y no debe instanciarse.
        End Sub

        ''' <summary>
        ''' Prueba los proveedores en orden, exige una respuesta IP válida y respeta tanto
        ''' la cancelación de la ventana como un límite breve de espera por proveedor.
        ''' </summary>
        Public Shared Async Function QueryAsync(cancellationToken As CancellationToken) As Task(Of PublicIpResult)
            Dim lastFailure As String = "No se recibió una dirección IP válida."

            For Each endpoint As Endpoint In Endpoints
                cancellationToken.ThrowIfCancellationRequested()

                Try
                    ' El cliente vive sólo durante esta petición para no dejar conexiones ni
                    ' estado asociado después de cerrar el formulario.
                    Using client As New HttpClient()
                        client.Timeout = TimeSpan.FromSeconds(6)
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("RedScope/2.1")

                        Using response As HttpResponseMessage =
                            Await client.GetAsync(endpoint.Uri, HttpCompletionOption.ResponseContentRead, cancellationToken).
                                ConfigureAwait(False)

                            response.EnsureSuccessStatusCode()
                            Dim responseText As String = (Await response.Content.ReadAsStringAsync().ConfigureAwait(False)).Trim()
                            Dim parsedAddress As IPAddress = Nothing

                            ' Una IP literal puede ser privada, CGNAT o de documentación.
                            ' Sólo se admite una dirección de salida pública plausible;
                            ' no se interpreta como dirección de la interfaz local del PC.
                            If IPAddress.TryParse(responseText, parsedAddress) AndAlso
                               IsPublicInternetAddress(parsedAddress) Then
                                Return New PublicIpResult With {
                                    .Address = parsedAddress.ToString(),
                                    .Source = endpoint.Name
                                }
                            End If

                            lastFailure = endpoint.Name & " devolvió una respuesta no válida."
                        End Using
                    End Using
                Catch ex As OperationCanceledException When cancellationToken.IsCancellationRequested
                    ' Una cancelación solicitada al cerrar o actualizar se propaga para que
                    ' la interfaz no presente como error una operación cancelada adrede.
                    Throw
                Catch ex As TaskCanceledException
                    lastFailure = "Tiempo de espera agotado al consultar " & endpoint.Name & "."
                Catch ex As Exception
                    ' No se exponen detalles técnicos ni datos del proxy al usuario. Se prueba
                    ' el proveedor alternativo y sólo se conserva un mensaje genérico en memoria.
                    lastFailure = "No fue posible consultar " & endpoint.Name & "."
                End Try
            Next

            Throw New InvalidOperationException(lastFailure)
        End Function

        ''' <summary>
        ''' Valida conservadoramente direcciones unicast de Internet. Rechaza bloques
        ''' privados, CGNAT, loopback, enlace local, documentación y otros usos especiales.
        ''' La respuesta sigue siendo la IP de salida vista por el servidor (VPN/proxy posible).
        ''' </summary>
        Friend Shared Function IsPublicInternetAddress(address As IPAddress) As Boolean
            If address Is Nothing OrElse IPAddress.IsLoopback(address) Then Return False

            If address.AddressFamily = AddressFamily.InterNetwork Then
                Dim bytes As Byte() = address.GetAddressBytes()
                Dim a As Integer = bytes(0)
                Dim b As Integer = bytes(1)
                Dim c As Integer = bytes(2)

                ' 0/8, 10/8, 100.64/10, 127/8, 169.254/16, 172.16/12.
                If a = 0 OrElse a = 10 OrElse a = 127 Then Return False
                If a = 100 AndAlso b >= 64 AndAlso b <= 127 Then Return False
                If a = 169 AndAlso b = 254 Then Return False
                If a = 172 AndAlso b >= 16 AndAlso b <= 31 Then Return False
                ' 192.0.0/24, TEST-NET-1, 6to4 relay antiguo y 192.168/16.
                If a = 192 AndAlso ((b = 0 AndAlso c = 0) OrElse
                                     (b = 0 AndAlso c = 2) OrElse
                                     (b = 88 AndAlso c = 99) OrElse b = 168) Then Return False
                ' Benchmark 198.18/15, TEST-NET-2 y TEST-NET-3.
                If a = 198 AndAlso ((b = 18 OrElse b = 19) OrElse
                                     (b = 51 AndAlso c = 100)) Then Return False
                If a = 203 AndAlso b = 0 AndAlso c = 113 Then Return False
                ' Multicast 224/4, reservado 240/4 y broadcast.
                If a >= 224 Then Return False
                Return True
            End If

            If address.AddressFamily = AddressFamily.InterNetworkV6 Then
                If address.IsIPv4MappedToIPv6 Then Return IsPublicInternetAddress(address.MapToIPv4())
                Dim bytes As Byte() = address.GetAddressBytes()
                ' Se acepta exclusivamente el espacio unicast global 2000::/3.
                If (CInt(bytes(0)) And &HE0) <> &H20 Then Return False
                ' 2001:db8::/32 es documentación y no una IP pública de salida.
                If bytes(0) = &H20 AndAlso bytes(1) = &H1 AndAlso
                   bytes(2) = &HD AndAlso bytes(3) = &HB8 Then Return False
                Return True
            End If
            Return False
        End Function
    End Class
End Namespace
