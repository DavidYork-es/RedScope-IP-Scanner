Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq

Namespace RedScope.Core
    ''' <summary>
    ''' Catálogo local de servicios TCP. Los perfiles reducen el tiempo de barrido
    ''' y permiten adaptar la búsqueda a oficina, soporte remoto o automatización.
    ''' </summary>
    Public NotInheritable Class PortDefinition
        Public Sub New(port As Integer, serviceName As String, groupName As String)
            Me.Port = port
            Me.ServiceName = serviceName
            Me.GroupName = groupName
        End Sub

        Public ReadOnly Property Port As Integer
        Public ReadOnly Property ServiceName As String
        Public ReadOnly Property GroupName As String

        Public Overrides Function ToString() As String
            Return Port.ToString(CultureInfo.InvariantCulture).PadLeft(5) & "  " & ServiceName
        End Function
    End Class

    Public NotInheritable Class PortProfiles
        Private Shared ReadOnly _catalog As New List(Of PortDefinition) From {
            New PortDefinition(20, "FTP datos", "Básico"),
            New PortDefinition(21, "FTP", "Básico"),
            New PortDefinition(22, "SSH / SFTP", "Básico"),
            New PortDefinition(23, "Telnet", "Básico"),
            New PortDefinition(25, "SMTP", "Básico"),
            New PortDefinition(53, "DNS", "Básico"),
            New PortDefinition(80, "HTTP", "Web"),
            New PortDefinition(110, "POP3", "Básico"),
            New PortDefinition(135, "RPC", "Windows"),
            New PortDefinition(139, "NetBIOS", "Windows"),
            New PortDefinition(143, "IMAP", "Básico"),
            New PortDefinition(443, "HTTPS", "Web"),
            New PortDefinition(445, "SMB", "Windows"),
            New PortDefinition(502, "Modbus TCP", "Industrial"),
            New PortDefinition(515, "Impresión LPD", "Impresión"),
            New PortDefinition(631, "IPP", "Impresión"),
            New PortDefinition(1433, "SQL Server", "Datos"),
            New PortDefinition(1883, "MQTT", "Industrial"),
            New PortDefinition(2001, "Terminal / serie", "Industrial"),
            New PortDefinition(3306, "MySQL", "Datos"),
            New PortDefinition(3389, "RDP", "Remoto"),
            New PortDefinition(4001, "Industrial", "Industrial"),
            New PortDefinition(4899, "Radmin", "Remoto"),
            New PortDefinition(5000, "Servicio TCP", "Industrial"),
            New PortDefinition(5432, "PostgreSQL", "Datos"),
            New PortDefinition(5900, "VNC", "Remoto"),
            New PortDefinition(7000, "Servicio TCP", "Industrial"),
            New PortDefinition(8000, "HTTP alternativo", "Web"),
            New PortDefinition(8080, "HTTP proxy", "Web"),
            New PortDefinition(8443, "HTTPS alternativo", "Web"),
            New PortDefinition(9100, "Impresión RAW", "Impresión"),
            New PortDefinition(10001, "Industrial / servidor serie", "Industrial"),
            New PortDefinition(20000, "DNP3 / servicio", "Industrial")
        }

        Private Sub New()
            ' Clase de catálogo: no debe instanciarse.
        End Sub

        Public Shared ReadOnly Property Catalog As IReadOnlyList(Of PortDefinition)
            Get
                Return _catalog.AsReadOnly()
            End Get
        End Property

        ''' <summary>
        ''' Perfil inicial moderado para inventario general. Los servicios más específicos
        ''' siguen disponibles en los demás perfiles o mediante selección manual.
        ''' </summary>
        Public Shared Function BasicPorts() As List(Of Integer)
            Return New List(Of Integer) From {22, 80, 135, 139, 443, 445, 3389, 8080, 9100}
        End Function

        Public Shared Function WebAndRemotePorts() As List(Of Integer)
            Return New List(Of Integer) From {22, 80, 443, 3389, 4899, 5900, 8000, 8080, 8443}
        End Function

        Public Shared Function IndustrialPorts() As List(Of Integer)
            Return New List(Of Integer) From {21, 22, 23, 80, 443, 502, 1883, 2001, 4001, 5000, 7000, 8000, 8080, 10001, 20000}
        End Function

        Public Shared Function AllCatalogPorts() As List(Of Integer)
            Return _catalog.Select(Function(item) item.Port).Distinct().OrderBy(Function(port) port).ToList()
        End Function

        ''' <summary>
        ''' Interpreta puertos sueltos y rangos, por ejemplo 80,443,8000-8010.
        ''' El límite evita que una entrada accidental convierta la búsqueda en 65.535 pruebas por host.
        ''' </summary>
        Public Shared Function TryParse(text As String,
                                        ByRef ports As List(Of Integer),
                                        ByRef errorMessage As String,
                                        Optional maximumPorts As Integer = 1024) As Boolean
            ports = New List(Of Integer)()
            errorMessage = String.Empty
            If String.IsNullOrWhiteSpace(text) Then Return True

            Dim lineFeed As String = Convert.ToChar(10).ToString()
            Dim normalized As String = text.Replace(";", ",").Replace(" ", ",").
                                             Replace(Environment.NewLine, ",").Replace(lineFeed, ",")
            Dim uniquePorts As New HashSet(Of Integer)()
            For Each rawPart As String In normalized.Split(New Char() {","c}, StringSplitOptions.RemoveEmptyEntries)
                Dim part As String = rawPart.Trim()
                If part.Length = 0 Then Continue For

                Dim dashIndex As Integer = part.IndexOf("-"c)
                If dashIndex >= 0 Then
                    Dim firstPort As Integer
                    Dim lastPort As Integer
                    If Not Integer.TryParse(part.Substring(0, dashIndex), NumberStyles.None, CultureInfo.InvariantCulture, firstPort) OrElse
                       Not Integer.TryParse(part.Substring(dashIndex + 1), NumberStyles.None, CultureInfo.InvariantCulture, lastPort) OrElse
                       firstPort < 1 OrElse lastPort > 65535 OrElse lastPort < firstPort Then
                        errorMessage = "Rango de puertos no válido: " & part
                        Return False
                    End If

                    For port As Integer = firstPort To lastPort
                        uniquePorts.Add(port)
                        If uniquePorts.Count > maximumPorts Then
                            errorMessage = "Se permiten como máximo " & maximumPorts.ToString(CultureInfo.CurrentCulture) & " puertos por barrido."
                            Return False
                        End If
                    Next
                Else
                    Dim port As Integer
                    If Not Integer.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, port) OrElse port < 1 OrElse port > 65535 Then
                        errorMessage = "Puerto no válido: " & part
                        Return False
                    End If
                    uniquePorts.Add(port)
                    ' Los puertos sueltos cuentan también para el máximo, no sólo los rangos.
                    If uniquePorts.Count > maximumPorts Then
                        errorMessage = "Se permiten como máximo " & maximumPorts.ToString(CultureInfo.CurrentCulture) & " puertos por barrido."
                        Return False
                    End If
                End If
            Next

            ports = uniquePorts.OrderBy(Function(port) port).ToList()
            Return True
        End Function

        ''' <summary>
        ''' Devuelve el nombre humano de un puerto conocido sin depender de Internet.
        ''' </summary>
        Public Shared Function ServiceName(port As Integer) As String
            Dim definition As PortDefinition = _catalog.FirstOrDefault(Function(item) item.Port = port)
            If definition Is Nothing Then Return "TCP"
            Return definition.ServiceName
        End Function

        Public Shared Function ToCsv(ports As IEnumerable(Of Integer)) As String
            Return String.Join(",", ports.OrderBy(Function(port) port).Select(Function(port) port.ToString(CultureInfo.InvariantCulture)))
        End Function
    End Class
End Namespace
