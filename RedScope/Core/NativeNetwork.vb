Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Management
Imports System.Net
Imports System.Net.NetworkInformation
Imports System.Net.Sockets
Imports System.Runtime.InteropServices
Imports RedScope.Models

Namespace RedScope.Core
    ''' <summary>
    ''' Operaciones de red dependientes de Windows: ARP y detección de la interfaz local.
    ''' Toda llamada nativa está encapsulada para que un fallo no interrumpa el barrido.
    ''' </summary>
    Public NotInheritable Class NativeNetwork
        <DllImport("iphlpapi.dll", ExactSpelling:=True)>
        Private Shared Function SendARP(destinationIp As Integer,
                                        sourceIp As Integer,
                                        macAddress As Byte(),
                                        ByRef physicalAddressLength As Integer) As Integer
        End Function

        <DllImport("iphlpapi.dll", SetLastError:=False)>
        Private Shared Function GetIpNetTable(table As IntPtr,
                                              ByRef size As Integer,
                                              order As Boolean) As Integer
        End Function

        <StructLayout(LayoutKind.Sequential)>
        Private Structure MibIpNetRow
            Public InterfaceIndex As UInteger
            Public PhysicalAddressLength As UInteger

            <MarshalAs(UnmanagedType.ByValArray, SizeConst:=8)>
            Public PhysicalAddress As Byte()

            Public Address As UInteger
            Public EntryType As UInteger
        End Structure

        Private Const ErrorInsufficientBuffer As Integer = 122

        Private Sub New()
            ' Clase de utilidades: no debe instanciarse.
        End Sub

        ''' <summary>
        ''' Solicita la MAC por ARP. Sólo es fiable dentro del mismo segmento de capa 2;
        ''' para redes remotas se devuelve vacío en lugar de atribuir la MAC del router.
        ''' </summary>
        Public Shared Function TryGetMacAddress(address As IPAddress) As String
            If address Is Nothing OrElse address.AddressFamily <> AddressFamily.InterNetwork Then Return String.Empty

            Try
                Dim destination As Integer = BitConverter.ToInt32(address.GetAddressBytes(), 0)
                Dim macBytes(5) As Byte
                Dim length As Integer = macBytes.Length
                Dim result As Integer = SendARP(destination, 0, macBytes, length)
                If result <> 0 OrElse length <= 0 Then Return String.Empty

                Dim usableLength As Integer = Math.Min(length, macBytes.Length)
                Dim parts As New List(Of String)(usableLength)
                For index As Integer = 0 To usableLength - 1
                    parts.Add(macBytes(index).ToString("X2"))
                Next
                Return String.Join(":", parts)
            Catch ex As Exception
                ' ARP es información complementaria; un fallo nunca invalida el host.
                Return String.Empty
            End Try
        End Function

        ''' <summary>
        ''' Lee la caché ARP local directamente mediante IP Helper API. No ejecuta arp.exe,
        ''' no crea procesos auxiliares y no conserva los resultados al cerrar RedScope.
        ''' </summary>
        Public Shared Function GetArpCacheTraces() As List(Of HostHistoryEntry)
            Dim traces As New List(Of HostHistoryEntry)()
            Dim known As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim buffer As IntPtr = IntPtr.Zero
            Try
                Dim bufferSize As Integer
                Dim result As Integer = GetIpNetTable(IntPtr.Zero, bufferSize, False)
                If result <> ErrorInsufficientBuffer OrElse bufferSize <= 4 Then Return traces

                buffer = Marshal.AllocHGlobal(bufferSize)
                result = GetIpNetTable(buffer, bufferSize, False)
                If result <> 0 Then Return traces

                Dim entryCount As Integer = Marshal.ReadInt32(buffer)
                Dim rowSize As Integer = Marshal.SizeOf(GetType(MibIpNetRow))
                For index As Integer = 0 To entryCount - 1
                    Dim rowPointer As IntPtr = IntPtr.Add(buffer, 4 + (index * rowSize))
                    Dim row As MibIpNetRow = DirectCast(
                        Marshal.PtrToStructure(rowPointer, GetType(MibIpNetRow)), MibIpNetRow)
                    If row.PhysicalAddress Is Nothing OrElse row.PhysicalAddressLength < 6UI Then Continue For

                    Dim address As New IPAddress(BitConverter.GetBytes(row.Address))
                    If Not IsUsableArpAddress(address) Then Continue For

                    Dim parts As New List(Of String)(6)
                    For byteIndex As Integer = 0 To 5
                        parts.Add(row.PhysicalAddress(byteIndex).ToString("X2"))
                    Next
                    Dim mac As String = String.Join(":", parts)
                    If Not IsUnicastMac(mac) OrElse Not known.Add(address.ToString()) Then Continue For

                    traces.Add(New HostHistoryEntry With {
                        .Address = address,
                        .MacAddress = mac,
                        .Manufacturer = OuiResolver.Resolve(mac),
                        .LastSeen = DateTime.Now,
                        .Source = "Caché ARP"
                    })
                Next
            Catch ex As Exception
                ' La caché ARP no está disponible en todos los entornos; es opcional.
            Finally
                If buffer <> IntPtr.Zero Then Marshal.FreeHGlobal(buffer)
            End Try
            Return traces
        End Function

        Private Shared Function IsUsableArpAddress(address As IPAddress) As Boolean
            If address Is Nothing OrElse address.AddressFamily <> AddressFamily.InterNetwork OrElse
               IPAddress.IsLoopback(address) Then Return False
            Dim bytes As Byte() = address.GetAddressBytes()
            Return bytes(0) > 0 AndAlso bytes(0) < 224
        End Function

        Private Shared Function IsUnicastMac(macAddress As String) As Boolean
            Dim parts As String() = If(macAddress, String.Empty).Split(":"c)
            If parts.Length <> 6 Then Return False
            Dim firstByte As Byte
            If Not Byte.TryParse(parts(0), Globalization.NumberStyles.HexNumber,
                                 Globalization.CultureInfo.InvariantCulture, firstByte) Then Return False
            If (firstByte And 1) <> 0 Then Return False
            Return Not String.Equals(macAddress, "00:00:00:00:00:00", StringComparison.OrdinalIgnoreCase) AndAlso
                   Not String.Equals(macAddress, "FF:FF:FF:FF:FF:FF", StringComparison.OrdinalIgnoreCase)
        End Function

        ''' <summary>
        ''' Obtiene todas las IPv4 configuradas. Primero consulta NetworkInterface y después
        ''' completa cualquier omisión mediante WMI, que refleja la configuración de Windows.
        ''' Un fallo en máscara o gateway nunca debe ocultar el adaptador completo.
        ''' </summary>
        Public Shared Function GetLocalNetworks() As List(Of LocalNetworkInfo)
            Dim networks As New List(Of LocalNetworkInfo)()
            Dim knownAddresses As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim adaptersById As New Dictionary(Of String, NetworkInterface)(StringComparer.OrdinalIgnoreCase)

            Try
                For Each adapter As NetworkInterface In NetworkInterface.GetAllNetworkInterfaces()
                    Try
                        Dim adapterIdentity As String = String.Empty
                        Dim adapterName As String = "Adaptador de red"
                        Dim adapterDescription As String = String.Empty
                        Dim adapterType As String = String.Empty
                        Dim adapterStatus As String = String.Empty
                        ReadAdapterMetadata(adapter, adapterIdentity, adapterName,
                                            adapterDescription, adapterType, adapterStatus)
                        If String.Equals(adapterType, NetworkInterfaceType.Loopback.ToString(),
                                         StringComparison.OrdinalIgnoreCase) Then Continue For

                        Dim normalizedId As String = NormalizeAdapterIdentity(adapterIdentity)
                        If Not String.IsNullOrWhiteSpace(normalizedId) Then adaptersById(normalizedId) = adapter

                        Dim properties As IPInterfaceProperties = adapter.GetIPProperties()
                        Dim gateway As String = TryGetIpv4Gateway(properties)
                        For Each unicast As UnicastIPAddressInformation In properties.UnicastAddresses
                            Try
                                If unicast.Address.AddressFamily <> AddressFamily.InterNetwork OrElse
                                   IPAddress.IsLoopback(unicast.Address) Then Continue For

                                Dim mask As IPAddress = unicast.IPv4Mask
                                If mask Is Nothing Then Continue For

                                AddNetwork(networks, knownAddresses, adapterIdentity,
                                           New LocalNetworkInfo With {
                                               .AdapterName = adapterName,
                                               .AdapterDescription = adapterDescription,
                                               .AdapterType = adapterType,
                                               .AdapterStatus = adapterStatus,
                                               .DetectionSource = "Interfaz de red",
                                               .Address = unicast.Address,
                                               .Mask = mask,
                                               .Gateway = gateway,
                                               .SuggestedRange = CalculateSuggestedRange(unicast.Address, mask)
                                           })
                            Catch ex As Exception
                                ' Una dirección problemática no invalida las demás del adaptador.
                            End Try
                        Next
                    Catch ex As Exception
                        ' Un adaptador defectuoso no impide mostrar las demás redes disponibles.
                    End Try
                Next
            Catch ex As Exception
                ' La lista parcial sigue siendo útil si falla la enumeración global.
            End Try

            ' WMI es un respaldo deliberado: algunas versiones/controladores de Windows
            ' omiten direcciones estáticas en NetworkInterface aunque estén operativas.
            AddWindowsConfiguredNetworks(networks, knownAddresses, adaptersById)

            ' Primero se muestran conexiones operativas y direcciones normales. Las APIPA
            ' y desconectadas siguen visibles, pero quedan relegadas al final.
            networks.Sort(
                Function(left As LocalNetworkInfo, right As LocalNetworkInfo) As Integer
                    Dim priorityComparison As Integer = GetNetworkPriority(left).CompareTo(GetNetworkPriority(right))
                    If priorityComparison <> 0 Then Return priorityComparison
                    Dim adapterComparison As Integer = StringComparer.CurrentCultureIgnoreCase.Compare(left.AdapterName, right.AdapterName)
                    If adapterComparison <> 0 Then Return adapterComparison
                    Return IpRangeParser.ToUInt32(left.Address).CompareTo(IpRangeParser.ToUInt32(right.Address))
                End Function)
            Return networks
        End Function

        ''' <summary>
        ''' Mantiene un valor inicial compatible con versiones anteriores. La ventana
        ''' principal usa GetLocalNetworks para permitir que el usuario elija entre todas.
        ''' </summary>
        Public Shared Function GetRecommendedLocalRange() As String
            Dim networks As List(Of LocalNetworkInfo) = GetLocalNetworks()
            If networks.Count > 0 Then Return networks(0).SuggestedRange

            Return "192.168.1.1-254"
        End Function

        ''' <summary>
        ''' Añade una red evitando duplicados entre NetworkInterface y WMI. Ambos métodos
        ''' proporcionan normalmente el mismo GUID de adaptador con distinto formato.
        ''' </summary>
        Private Shared Sub AddNetwork(networks As List(Of LocalNetworkInfo),
                                      knownAddresses As HashSet(Of String),
                                      adapterIdentity As String,
                                      network As LocalNetworkInfo)
            If network Is Nothing OrElse network.Address Is Nothing OrElse network.Mask Is Nothing Then Return

            Dim normalizedAdapter As String = NormalizeAdapterIdentity(adapterIdentity)
            Dim identity As String = normalizedAdapter & "|" & network.Address.ToString() & "|" & network.Mask.ToString()
            If knownAddresses.Add(identity) Then networks.Add(network)
        End Sub

        Private Shared Function NormalizeAdapterIdentity(adapterIdentity As String) As String
            Return If(adapterIdentity, String.Empty).Trim().Trim("{"c, "}"c)
        End Function

        ''' <summary>
        ''' Lee cada dato descriptivo por separado. Así, un controlador que falle al
        ''' devolver el nombre o el estado no provoca la pérdida de sus direcciones IP.
        ''' </summary>
        Private Shared Sub ReadAdapterMetadata(adapter As NetworkInterface,
                                               ByRef identity As String,
                                               ByRef name As String,
                                               ByRef description As String,
                                               ByRef adapterType As String,
                                               ByRef status As String)
            If adapter Is Nothing Then Return
            Try
                identity = adapter.Id
            Catch ex As Exception
                identity = String.Empty
            End Try
            Try
                name = adapter.Name
            Catch ex As Exception
                name = "Adaptador de red"
            End Try
            Try
                description = adapter.Description
            Catch ex As Exception
                description = String.Empty
            End Try
            Try
                adapterType = adapter.NetworkInterfaceType.ToString()
            Catch ex As Exception
                adapterType = String.Empty
            End Try
            Try
                status = adapter.OperationalStatus.ToString()
            Catch ex As Exception
                status = "Desconocido"
            End Try
        End Sub

        ''' <summary>
        ''' Lee la configuración IPv4 directamente desde WMI. Es una fuente de respaldo
        ''' incluida en Windows y permite recuperar IP estáticas omitidas por un controlador.
        ''' </summary>
        Private Shared Sub AddWindowsConfiguredNetworks(networks As List(Of LocalNetworkInfo),
                                                         knownAddresses As HashSet(Of String),
                                                         adaptersById As Dictionary(Of String, NetworkInterface))
            Try
                Using searcher As New ManagementObjectSearcher(
                    "SELECT SettingID, Description, IPAddress, IPSubnet, DefaultIPGateway " &
                    "FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE")

                    For Each configuration As ManagementObject In searcher.Get()
                        Try
                            Dim adapterIdentity As String = ReadManagementString(configuration, "SettingID")
                            Dim description As String = ReadManagementString(configuration, "Description")
                            Dim adapter As NetworkInterface = Nothing
                            adaptersById.TryGetValue(NormalizeAdapterIdentity(adapterIdentity), adapter)
                            Dim addresses As String() = ReadManagementStringArray(configuration, "IPAddress")
                            Dim masks As String() = ReadManagementStringArray(configuration, "IPSubnet")
                            Dim gateway As String = FirstIpv4(ReadManagementStringArray(configuration, "DefaultIPGateway"))
                            Dim upperBound As Integer = Math.Min(addresses.Length, masks.Length) - 1

                            Dim adapterName As String = If(String.IsNullOrWhiteSpace(description), "Adaptador Windows", description)
                            Dim adapterDescription As String = description
                            Dim adapterType As String = "Configuración IPv4 de Windows"
                            Dim adapterStatus As String = "Configurada"
                            Dim ignoredIdentity As String = adapterIdentity
                            If adapter IsNot Nothing Then
                                ReadAdapterMetadata(adapter, ignoredIdentity, adapterName,
                                                    adapterDescription, adapterType, adapterStatus)
                            End If

                            For index As Integer = 0 To upperBound
                                Dim address As IPAddress = Nothing
                                Dim mask As IPAddress = Nothing
                                If Not IPAddress.TryParse(addresses(index), address) OrElse
                                   address.AddressFamily <> AddressFamily.InterNetwork OrElse
                                   IPAddress.IsLoopback(address) Then Continue For
                                If Not IPAddress.TryParse(masks(index), mask) OrElse
                                   mask.AddressFamily <> AddressFamily.InterNetwork Then Continue For

                                AddNetwork(networks, knownAddresses, adapterIdentity,
                                           New LocalNetworkInfo With {
                                               .AdapterName = adapterName,
                                               .AdapterDescription = adapterDescription,
                                               .AdapterType = adapterType,
                                               .AdapterStatus = adapterStatus,
                                               .DetectionSource = "Configuración de Windows (WMI)",
                                               .Address = address,
                                               .Mask = mask,
                                               .Gateway = gateway,
                                               .SuggestedRange = CalculateSuggestedRange(address, mask)
                                           })
                            Next
                        Catch ex As Exception
                            ' Una entrada WMI incompleta no bloquea el resto de adaptadores.
                        End Try
                    Next
                End Using
            Catch ex As Exception
                ' WMI puede estar deshabilitado; los resultados primarios siguen siendo válidos.
            End Try
        End Sub

        Private Shared Function ReadManagementString(configuration As ManagementObject, propertyName As String) As String
            Try
                Return Convert.ToString(configuration(propertyName), Globalization.CultureInfo.InvariantCulture)
            Catch ex As Exception
                Return String.Empty
            End Try
        End Function

        Private Shared Function ReadManagementStringArray(value As Object) As String()
            If value Is Nothing Then Return New String() {}
            Dim directValues As String() = TryCast(value, String())
            If directValues IsNot Nothing Then Return directValues

            Dim source As Array = TryCast(value, Array)
            If source Is Nothing Then Return New String() {}
            Dim values As New List(Of String)(source.Length)
            For Each item As Object In source
                values.Add(Convert.ToString(item, Globalization.CultureInfo.InvariantCulture))
            Next
            Return values.ToArray()
        End Function

        Private Shared Function ReadManagementStringArray(configuration As ManagementObject,
                                                          propertyName As String) As String()
            Try
                Return ReadManagementStringArray(configuration(propertyName))
            Catch ex As Exception
                Return New String() {}
            End Try
        End Function

        Private Shared Function FirstIpv4(values As IEnumerable(Of String)) As String
            For Each value As String In values
                Dim address As IPAddress = Nothing
                If IPAddress.TryParse(value, address) AndAlso address.AddressFamily = AddressFamily.InterNetwork Then
                    Return address.ToString()
                End If
            Next
            Return String.Empty
        End Function

        Private Shared Function GetNetworkPriority(network As LocalNetworkInfo) As Integer
            Dim priority As Integer
            If String.Equals(network.AdapterStatus, OperationalStatus.Up.ToString(), StringComparison.OrdinalIgnoreCase) Then
                priority = 0
            ElseIf String.Equals(network.AdapterStatus, "Configurada", StringComparison.OrdinalIgnoreCase) Then
                priority = 10
            Else
                priority = 30
            End If

            If IsApipa(network.Address) Then priority += 20
            If String.IsNullOrWhiteSpace(network.Gateway) Then priority += 4
            Return priority
        End Function

        Private Shared Function IsApipa(address As IPAddress) As Boolean
            If address Is Nothing OrElse address.AddressFamily <> AddressFamily.InterNetwork Then Return False
            Dim bytes As Byte() = address.GetAddressBytes()
            Return bytes(0) = 169 AndAlso bytes(1) = 254
        End Function

        ''' <summary>
        ''' Calcula los hosts utilizables de la subred. Si supera 4096 direcciones se
        ''' propone el /24 de la IP actual para evitar un barrido accidental masivo.
        ''' </summary>
        Private Shared Function CalculateSuggestedRange(address As IPAddress, mask As IPAddress) As String
            Dim ipValue As UInteger = IpRangeParser.ToUInt32(address)
            Dim maskValue As UInteger = IpRangeParser.ToUInt32(mask)
            Dim networkValue As UInteger = ipValue And maskValue
            Dim broadcastValue As UInteger = networkValue Or Not maskValue
            Dim addressCount As ULong = CULng(broadcastValue) - CULng(networkValue) + 1UL

            If addressCount > 4096UL Then
                Dim bytes As Byte() = address.GetAddressBytes()
                Return String.Format(Globalization.CultureInfo.InvariantCulture,
                                     "{0}.{1}.{2}.1-254", bytes(0), bytes(1), bytes(2))
            End If

            If addressCount = 1UL Then Return address.ToString()
            If addressCount = 2UL Then
                Return IpRangeParser.FromUInt32(networkValue).ToString() & "-" &
                       IpRangeParser.FromUInt32(broadcastValue).ToString()
            End If

            Return IpRangeParser.FromUInt32(networkValue + 1UI).ToString() & "-" &
                   IpRangeParser.FromUInt32(broadcastValue - 1UI).ToString()
        End Function

        ''' <summary>
        ''' Recupera la primera puerta de enlace IPv4 de la interfaz para ayudar a
        ''' distinguir redes parecidas en el selector.
        ''' </summary>
        Private Shared Function TryGetIpv4Gateway(properties As IPInterfaceProperties) As String
            Try
                For Each gateway As GatewayIPAddressInformation In properties.GatewayAddresses
                    If gateway.Address IsNot Nothing AndAlso gateway.Address.AddressFamily = AddressFamily.InterNetwork Then
                        Return gateway.Address.ToString()
                    End If
                Next
            Catch ex As Exception
                ' La IP y máscara siguen siendo válidas aunque falle la consulta del gateway.
            End Try
            Return String.Empty
        End Function
    End Class
End Namespace
