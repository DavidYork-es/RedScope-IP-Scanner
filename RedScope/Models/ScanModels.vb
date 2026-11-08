Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Net

Namespace RedScope.Models
    ''' <summary>
    ''' Describe una dirección IPv4 asignada al PC y el rango de exploración calculado
    ''' a partir de su máscara. Una misma interfaz puede aportar varias instancias.
    ''' </summary>
    Public NotInheritable Class LocalNetworkInfo
        Public Property AdapterName As String = String.Empty
        Public Property AdapterDescription As String = String.Empty
        Public Property AdapterType As String = String.Empty
        Public Property AdapterStatus As String = String.Empty
        Public Property DetectionSource As String = String.Empty
        Public Property Address As IPAddress
        Public Property Mask As IPAddress
        Public Property Gateway As String = String.Empty
        Public Property SuggestedRange As String = String.Empty
    End Class

    ''' <summary>
    ''' Identidad observada anteriormente en un escaneo o en la caché ARP local.
    ''' LastSeen expresa la última evidencia disponible, no que el host siga conectado.
    ''' </summary>
    Public NotInheritable Class HostHistoryEntry
        Public Property Address As IPAddress
        Public Property HostName As String = String.Empty
        Public Property MacAddress As String = String.Empty
        Public Property Manufacturer As String = String.Empty
        Public Property LastSeen As DateTime
        Public Property Source As String = String.Empty
    End Class

    ''' <summary>
    ''' Configuración inmutable utilizada por un barrido completo.
    ''' Tomar una copia al iniciar evita que un cambio en la ventana de opciones
    ''' altere un escaneo que ya está en marcha.
    ''' </summary>
    Public NotInheritable Class ScanSettings
        Public Property PingTimeoutMs As Integer = 450
        Public Property TcpTimeoutMs As Integer = 350
        Public Property DnsTimeoutMs As Integer = 900
        Public Property MaxConcurrency As Integer = 12
        Public Property MaxPortConcurrency As Integer = 24
        Public Property ResolveDns As Boolean = True
        Public Property ResolveMac As Boolean = True
        Public Property Ports As List(Of Integer) = New List(Of Integer)()

        ''' <summary>
        ''' Crea una copia profunda para aislar el motor de la interfaz.
        ''' </summary>
        Public Function CloneSettings() As ScanSettings
            Return New ScanSettings With {
                .PingTimeoutMs = PingTimeoutMs,
                .TcpTimeoutMs = TcpTimeoutMs,
                .DnsTimeoutMs = DnsTimeoutMs,
                .MaxConcurrency = MaxConcurrency,
                .MaxPortConcurrency = MaxPortConcurrency,
                .ResolveDns = ResolveDns,
                .ResolveMac = ResolveMac,
                .Ports = New List(Of Integer)(Ports)
            }
        End Function
    End Class

    ''' <summary>
    ''' Resultado completo de una dirección IP. Se conserva también para equipos
    ''' inactivos, aunque la vista pueda ocultarlos.
    ''' </summary>
    Public NotInheritable Class ScanResult
        Public Property Address As IPAddress
        Public Property IsOnline As Boolean
        Public Property PingSucceeded As Boolean
        Public Property LatencyMs As Long?
        Public Property HostName As String = String.Empty
        Public Property MacAddress As String = String.Empty
        Public Property Manufacturer As String = String.Empty
        Public Property OpenPorts As List(Of Integer) = New List(Of Integer)()
        Public Property ErrorText As String = String.Empty
        Public Property IsHistorical As Boolean
        Public Property LastSeen As DateTime?
        Public Property HistorySource As String = String.Empty

        ''' <summary>
        ''' Considera activo un equipo si responde a ICMP o acepta al menos una conexión TCP.
        ''' Esto permite descubrir dispositivos que bloquean el ping mediante firewall.
        ''' </summary>
        Public ReadOnly Property StatusText As String
            Get
                If IsHistorical Then
                    If String.Equals(HistorySource, "Caché ARP", StringComparison.OrdinalIgnoreCase) Then Return "Rastro ARP"
                    Return "Histórico"
                End If
                If PingSucceeded Then Return "Activo"
                If OpenPorts.Count > 0 Then Return "Activo (TCP)"
                Return "Sin respuesta"
            End Get
        End Property
    End Class

    ''' <summary>
    ''' Contadores de avance que la interfaz presenta sin conocer la implementación
    ''' interna de los trabajadores asíncronos.
    ''' </summary>
    Public NotInheritable Class ScanProgress
        Public Property Total As Integer
        Public Property Completed As Integer
        Public Property Online As Integer
        Public Property Offline As Integer
        Public Property CurrentAddress As String = String.Empty
    End Class
End Namespace
