Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic

Namespace RedScope.Core
    ''' <summary>
    ''' Resuelve fabricantes frecuentes a partir de los tres primeros bytes de la MAC.
    ''' La tabla está embebida para que ninguna dirección de la red salga del equipo.
    ''' Al no ser una base IEEE completa, un prefijo desconocido se muestra sin inventar datos.
    ''' </summary>
    Public NotInheritable Class OuiResolver
        Private Shared ReadOnly _vendors As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"000C29", "VMware"}, {"001C14", "VMware"}, {"005056", "VMware"},
            {"00005E", "IANA / VRRP"}, {"000142", "Cisco"},
            {"00036B", "Cisco"}, {"000423", "Intel"}, {"000E0C", "Cisco"},
            {"001120", "Cisco"}, {"001A2F", "Cisco"}, {"0023EB", "Cisco"},
            {"0024C4", "Cisco"}, {"00D0BA", "Cisco"}, {"F4CF E2".Replace(" ", ""), "Cisco"},
            {"001B21", "Intel"}, {"001E64", "Intel"}, {"3C970E", "Intel"},
            {"4C796E", "Intel"}, {"5CC5D4", "Intel"}, {"7C5CF8", "Intel"},
            {"A4C3F0", "Intel"}, {"B4B676", "Intel"}, {"F83441", "Intel"},
            {"00155D", "Microsoft / Hyper-V"}, {"0003FF", "Microsoft"},
            {"0016CB", "Apple"}, {"0017F2", "Apple"}, {"0019E3", "Apple"},
            {"001CB3", "Apple"}, {"002312", "Apple"}, {"002332", "Apple"},
            {"002436", "Apple"}, {"3C0754", "Apple"}, {"40A6D9", "Apple"},
            {"5C969D", "Apple"}, {"A4D1D2", "Apple"}, {"F0D1A9", "Apple"},
            {"0013CE", "Intel"}, {"001E58", "D-Link"}, {"001F33", "Netgear"},
            {"001D0F", "TP-Link"}, {"14CF92", "TP-Link"}, {"1C3BF3", "TP-Link"},
            {"50C7BF", "TP-Link"}, {"60E327", "TP-Link"}, {"6466B3", "TP-Link"},
            {"9C5322", "TP-Link"}, {"B0487A", "TP-Link"}, {"D80D17", "TP-Link"},
            {"F4F26D", "TP-Link"}, {"DC2C6E", "Routerboard / MikroTik"},
            {"4C5E0C", "Routerboard / MikroTik"}, {"6C3B6B", "Routerboard / MikroTik"},
            {"D4CA6D", "Routerboard / MikroTik"}, {"E48D8C", "Routerboard / MikroTik"},
            {"000C42", "Routerboard / MikroTik"}, {"00156D", "Ubiquiti"},
            {"002722", "Ubiquiti"}, {"0418D6", "Ubiquiti"}, {"24A43C", "Ubiquiti"},
            {"44D9E7", "Ubiquiti"}, {"68D79A", "Ubiquiti"}, {"74ACB9", "Ubiquiti"},
            {"788A20", "Ubiquiti"}, {"80D21D", "Ubiquiti"}, {"B4FBE4", "Ubiquiti"},
            {"D021F9", "Ubiquiti"}, {"DC9FDB", "Ubiquiti"}, {"E063DA", "Ubiquiti"},
            {"F09FC2", "Ubiquiti"}, {"001A11", "Google"}, {"3C5AB4", "Google"},
            {"54BD79", "Google"}, {"F4F5D8", "Google"}, {"B827EB", "Raspberry Pi"},
            {"DCA632", "Raspberry Pi"}, {"E45F01", "Raspberry Pi"}, {"2CCF67", "Raspberry Pi"},
            {"240AC4", "Espressif"}, {"246F28", "Espressif"}, {"24D7EB", "Espressif"},
            {"30AEA4", "Espressif"}, {"3C6105", "Espressif"}, {"40F520", "Espressif"},
            {"7CDFA1", "Espressif"}, {"84CCA8", "Espressif"}, {"A4CF12", "Espressif"},
            {"AC67B2", "Espressif"}, {"C8C9A3", "Espressif"}, {"CC50E3", "Espressif"},
            {"EC62B4", "Espressif"}, {"0017C8", "Kyocera"}, {"0022F0", "3Com / HP"},
            {"001CC4", "Hewlett Packard"}, {"0024A8", "Hewlett Packard"},
            {"308D99", "Hewlett Packard"}, {"3CA82A", "Hewlett Packard"},
            {"64006A", "Dell"}, {"B8CA3A", "Dell"}, {"D09466", "Dell"},
            {"00055D", "D-Link"}, {"1C7EE5", "D-Link"}, {"28107B", "D-Link"},
            {"C8BE19", "D-Link"}, {"001B9E", "ASUS"}, {"2C4D54", "ASUS"},
            {"50465D", "ASUS"}, {"AC9E17", "ASUS"}, {"F832E4", "ASUS"},
            {"00188B", "Dell"}, {"001E4F", "Dell"}, {"002564", "Dell"},
            {"141877", "Dell"}, {"18A99B", "Dell"}, {"B083FE", "Dell"},
            {"001C25", "Hon Hai / Foxconn"}, {"000FE2", "Hangzhou Hikvision"},
            {"44A6E5", "Hangzhou Hikvision"}, {"4C1C2A", "Hangzhou Hikvision"},
            {"54C415", "Hangzhou Hikvision"}, {"6C1C71", "Hangzhou Hikvision"},
            {"BCAD28", "Hangzhou Hikvision"}, {"C056E3", "Hangzhou Hikvision"},
            {"E0CA3C", "Hangzhou Hikvision"}, {"3CEF8C", "Zhejiang Dahua"},
            {"4C11BF", "Zhejiang Dahua"}, {"5CE0CA", "Zhejiang Dahua"},
            {"90F652", "Zhejiang Dahua"}, {"A0BD1D", "Zhejiang Dahua"},
            {"B0A7B9", "Zhejiang Dahua"}, {"E0508B", "Zhejiang Dahua"},
            {"E4F4C6", "Netgear"}, {"A040A0", "Netgear"}, {"C03F0E", "Netgear"},
            {"00080D", "Toshiba"}, {"0000F0", "Samsung"}, {"0012FB", "Samsung"},
            {"001632", "Samsung"}, {"002454", "Samsung"}, {"286ABA", "Apple"},
            {"001DAA", "DrayTek"}, {"001D7E", "Cisco-Linksys"}, {"001E8F", "Canon"},
            {"001BA9", "Brother"}, {"0034F1", "Radwin"}, {"0080A3", "Lantronix"},
            {"0020BB", "Zax / control de acceso"}, {"0004A3", "Microchip"},
            {"001EC0", "Microchip"}, {"D88039", "Microchip"}, {"001EC2", "Apple"},
            {"0001E6", "Hewlett Packard"}, {"0001E7", "Hewlett Packard"},
            {"080027", "Oracle VirtualBox"}, {"525400", "QEMU / KVM"}
        }

        Private Sub New()
            ' Clase de utilidades: no debe instanciarse.
        End Sub

        ''' <summary>
        ''' Normaliza separadores y devuelve vacío cuando el prefijo no está en la base local.
        ''' </summary>
        Public Shared Function Resolve(macAddress As String) As String
            If String.IsNullOrWhiteSpace(macAddress) Then Return String.Empty
            Dim normalized As String = macAddress.Replace(":", String.Empty).
                                                   Replace("-", String.Empty).
                                                   Replace(".", String.Empty).
                                                   Trim().ToUpperInvariant()
            If normalized.Length < 6 Then Return String.Empty

            Dim vendor As String = String.Empty
            If _vendors.TryGetValue(normalized.Substring(0, 6), vendor) Then Return vendor
            Return String.Empty
        End Function
    End Class
End Namespace
