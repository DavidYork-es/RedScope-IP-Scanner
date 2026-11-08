Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Net
Imports System.Net.Sockets

Namespace RedScope.Core
    ''' <summary>
    ''' Interpreta rangos IPv4 escritos por una persona. Admite IP única, último octeto,
    ''' rango completo y CIDR. No se acepta IPv6 en esta versión para conservar una
    ''' estimación de avance exacta y evitar barridos inabarcables.
    ''' </summary>
    Public NotInheritable Class IpRangeParser
        Public Const MaximumAddresses As Integer = 65536

        Private Sub New()
            ' Clase de utilidades: no debe instanciarse.
        End Sub

        ''' <summary>
        ''' Convierte uno o varios bloques separados por punto y coma o salto de línea.
        ''' Los duplicados se eliminan para no explorar dos veces el mismo equipo.
        ''' </summary>
        Public Shared Function TryParse(text As String,
                                        ByRef addresses As List(Of IPAddress),
                                        ByRef errorMessage As String) As Boolean
            addresses = New List(Of IPAddress)()
            errorMessage = String.Empty

            If String.IsNullOrWhiteSpace(text) Then
                errorMessage = "Introduce una IP, un rango o una red CIDR."
                Return False
            End If

            Dim lineFeed As String = Convert.ToChar(10).ToString()
            Dim normalized As String = text.Replace(Environment.NewLine, ";").Replace(lineFeed, ";")
            Dim blocks As String() = normalized.Split(New Char() {";"c}, StringSplitOptions.RemoveEmptyEntries)
            Dim values As New HashSet(Of UInteger)()

            For Each rawBlock As String In blocks
                Dim block As String = rawBlock.Trim()
                If block.Length = 0 Then Continue For

                Dim startValue As UInteger
                Dim endValue As UInteger
                If Not TryParseBlock(block, startValue, endValue, errorMessage) Then Return False

                Dim blockCount As ULong = CULng(endValue) - CULng(startValue) + 1UL
                If blockCount > CULng(MaximumAddresses) OrElse
                   CULng(values.Count) + blockCount > CULng(MaximumAddresses) Then
                    errorMessage = "El barrido está limitado a " & MaximumAddresses.ToString("N0", CultureInfo.CurrentCulture) &
                                   " direcciones por seguridad. Divide la red en varios rangos."
                    Return False
                End If

                Dim current As ULong = startValue
                While current <= endValue
                    values.Add(CUInt(current))
                    current += 1UL
                End While
            Next

            addresses = values.OrderBy(Function(value) value).
                               Select(Function(value) FromUInt32(value)).ToList()

            If addresses.Count = 0 Then
                errorMessage = "El rango no contiene ninguna dirección utilizable."
                Return False
            End If

            Return True
        End Function

        ''' <summary>
        ''' Decide el tipo de bloque y obtiene sus límites numéricos inclusivos.
        ''' </summary>
        Private Shared Function TryParseBlock(block As String,
                                              ByRef startValue As UInteger,
                                              ByRef endValue As UInteger,
                                              ByRef errorMessage As String) As Boolean
            If block.Contains("/"c) Then
                Return TryParseCidr(block, startValue, endValue, errorMessage)
            End If

            Dim dashIndex As Integer = block.IndexOf("-"c)
            If dashIndex >= 0 Then
                Return TryParseRange(block, dashIndex, startValue, endValue, errorMessage)
            End If

            Dim singleAddress As IPAddress = Nothing
            If Not TryParseIpv4(block, singleAddress) Then
                errorMessage = "Dirección IPv4 no válida: " & block
                Return False
            End If

            startValue = ToUInt32(singleAddress)
            endValue = startValue
            Return True
        End Function

        ''' <summary>
        ''' Admite 192.168.1.1-254 y 192.168.1.1-192.168.1.254.
        ''' </summary>
        Private Shared Function TryParseRange(block As String,
                                              dashIndex As Integer,
                                              ByRef startValue As UInteger,
                                              ByRef endValue As UInteger,
                                              ByRef errorMessage As String) As Boolean
            Dim leftText As String = block.Substring(0, dashIndex).Trim()
            Dim rightText As String = block.Substring(dashIndex + 1).Trim()
            Dim firstAddress As IPAddress = Nothing
            If Not TryParseIpv4(leftText, firstAddress) Then
                errorMessage = "Inicio de rango no válido: " & leftText
                Return False
            End If

            Dim lastAddress As IPAddress = Nothing
            Dim lastOctet As Integer
            If Integer.TryParse(rightText, NumberStyles.None, CultureInfo.InvariantCulture, lastOctet) AndAlso
               lastOctet >= 0 AndAlso lastOctet <= 255 Then
                Dim bytes As Byte() = firstAddress.GetAddressBytes()
                bytes(3) = CByte(lastOctet)
                lastAddress = New IPAddress(bytes)
            ElseIf Not TryParseIpv4(rightText, lastAddress) Then
                errorMessage = "Final de rango no válido: " & rightText
                Return False
            End If

            startValue = ToUInt32(firstAddress)
            endValue = ToUInt32(lastAddress)
            If endValue < startValue Then
                errorMessage = "El final del rango es anterior al inicio: " & block
                Return False
            End If

            Return True
        End Function

        ''' <summary>
        ''' Convierte una notación CIDR en el primer y último host. En /31 y /32 se
        ''' conservan todas las direcciones porque no existe un par red/broadcast clásico.
        ''' </summary>
        Private Shared Function TryParseCidr(block As String,
                                             ByRef startValue As UInteger,
                                             ByRef endValue As UInteger,
                                             ByRef errorMessage As String) As Boolean
            Dim parts As String() = block.Split("/"c)
            Dim baseAddress As IPAddress = Nothing
            Dim prefix As Integer
            If parts.Length <> 2 OrElse Not TryParseIpv4(parts(0).Trim(), baseAddress) OrElse
               Not Integer.TryParse(parts(1).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, prefix) OrElse
               prefix < 0 OrElse prefix > 32 Then
                errorMessage = "Red CIDR no válida: " & block
                Return False
            End If

            Dim mask As UInteger
            If prefix = 0 Then
                mask = 0UI
            Else
                mask = UInteger.MaxValue << (32 - prefix)
            End If

            Dim network As UInteger = ToUInt32(baseAddress) And mask
            Dim broadcast As UInteger = network Or Not mask
            If prefix <= 30 Then
                startValue = network + 1UI
                endValue = broadcast - 1UI
            Else
                startValue = network
                endValue = broadcast
            End If

            Return True
        End Function

        ''' <summary>
        ''' Valida explícitamente IPv4 para que IPAddress.TryParse no introduzca IPv6.
        ''' </summary>
        Private Shared Function TryParseIpv4(text As String, ByRef address As IPAddress) As Boolean
            Return IPAddress.TryParse(text, address) AndAlso address.AddressFamily = AddressFamily.InterNetwork
        End Function

        ''' <summary>
        ''' Pasa una IPv4 a entero en orden de red para comparar y ordenar rangos.
        ''' </summary>
        Public Shared Function ToUInt32(address As IPAddress) As UInteger
            Dim bytes As Byte() = address.GetAddressBytes()
            Return (CUInt(bytes(0)) << 24) Or (CUInt(bytes(1)) << 16) Or
                   (CUInt(bytes(2)) << 8) Or CUInt(bytes(3))
        End Function

        ''' <summary>
        ''' Reconstruye una IPv4 desde el entero en orden de red.
        ''' </summary>
        Public Shared Function FromUInt32(value As UInteger) As IPAddress
            Dim bytes As Byte() = {
                CByte((value >> 24) And &HFFUI),
                CByte((value >> 16) And &HFFUI),
                CByte((value >> 8) And &HFFUI),
                CByte(value And &HFFUI)
            }
            Return New IPAddress(bytes)
        End Function
    End Class
End Namespace
