Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports RedScope.Core
Imports RedScope.Models

Namespace RedScope.Forms
    ''' <summary>
    ''' Editor visual de puertos y tiempos. Se separa del formulario principal para
    ''' mantener el escáner compacto durante el uso diario.
    ''' </summary>
    Public NotInheritable Class PortOptionsForm
        Inherits Form

        Private ReadOnly _portsList As New CheckedListBox()
        Private ReadOnly _customPorts As New TextBox()
        Private ReadOnly _pingTimeout As New NumericUpDown()
        Private ReadOnly _tcpTimeout As New NumericUpDown()
        Private ReadOnly _dnsTimeout As New NumericUpDown()
        Private ReadOnly _concurrency As New NumericUpDown()
        Private ReadOnly _resolveDns As New CheckBox()
        Private ReadOnly _resolveMac As New CheckBox()
        Private ReadOnly _acceptButton As New Button()
        Private ReadOnly _cancelButton As New Button()

        Public Sub New(currentSettings As ScanSettings)
            If currentSettings Is Nothing Then Throw New ArgumentNullException(NameOf(currentSettings))
            Settings = currentSettings.CloneSettings()
            InitializeInterface()
            LoadCurrentSettings()
        End Sub

        Public Property Settings As ScanSettings

        ''' <summary>
        ''' Construye el diálogo por código para no depender de archivos .resx externos.
        ''' </summary>
        Private Sub InitializeInterface()
            Text = "Puertos y rendimiento"
            StartPosition = FormStartPosition.CenterParent
            FormBorderStyle = FormBorderStyle.FixedDialog
            MaximizeBox = False
            MinimizeBox = False
            ShowInTaskbar = False
            ClientSize = New Size(690, 570)
            BackColor = Color.FromArgb(245, 248, 252)
            Font = New Font("Segoe UI", 9.0F, FontStyle.Regular, GraphicsUnit.Point)
            Try
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
            Catch ex As Exception
                ' El icono no es crítico durante la ejecución desde Visual Studio.
            End Try

            Dim header As New Panel With {
                .Dock = DockStyle.Top,
                .Height = 70,
                .BackColor = Color.FromArgb(18, 35, 58)
            }
            Dim title As New Label With {
                .Text = "PUERTOS Y RENDIMIENTO",
                .ForeColor = Color.White,
                .Font = New Font("Segoe UI Semibold", 15.0F),
                .AutoSize = True,
                .Location = New Point(20, 12)
            }
            Dim subtitle As New Label With {
                .Text = "Selecciona sólo lo necesario. Los cambios duran únicamente esta sesión.",
                .ForeColor = Color.FromArgb(176, 196, 222),
                .AutoSize = True,
                .Location = New Point(22, 42)
            }
            header.Controls.Add(title)
            header.Controls.Add(subtitle)
            Controls.Add(header)

            Dim profileLabel As New Label With {
                .Text = "PERFILES RÁPIDOS",
                .ForeColor = Color.FromArgb(70, 84, 103),
                .Font = New Font("Segoe UI Semibold", 8.5F),
                .AutoSize = True,
                .Location = New Point(20, 86)
            }
            Controls.Add(profileLabel)

            Dim profileNames As String() = {"Básico", "Web + remoto", "Industrial", "Todos", "Ninguno"}
            Dim profileColors As Color() = {
                Color.FromArgb(42, 113, 204), Color.FromArgb(0, 145, 140),
                Color.FromArgb(132, 89, 183), Color.FromArgb(71, 85, 105), Color.FromArgb(160, 72, 72)
            }
            For index As Integer = 0 To profileNames.Length - 1
                Dim button As New Button With {
                    .Text = profileNames(index),
                    .Tag = profileNames(index),
                    .FlatStyle = FlatStyle.Flat,
                    .BackColor = profileColors(index),
                    .ForeColor = Color.White,
                    .Cursor = Cursors.Hand,
                    .Size = New Size(118, 31),
                    .Location = New Point(20 + (index * 128), 107)
                }
                button.FlatAppearance.BorderSize = 0
                AddHandler button.Click, AddressOf OnProfileClick
                Controls.Add(button)
            Next

            Dim portsLabel As New Label With {
                .Text = "SERVICIOS TCP",
                .ForeColor = Color.FromArgb(70, 84, 103),
                .Font = New Font("Segoe UI Semibold", 8.5F),
                .AutoSize = True,
                .Location = New Point(20, 151)
            }
            Controls.Add(portsLabel)

            _portsList.Location = New Point(20, 172)
            _portsList.Size = New Size(650, 172)
            _portsList.CheckOnClick = True
            _portsList.MultiColumn = True
            _portsList.ColumnWidth = 208
            _portsList.BorderStyle = BorderStyle.FixedSingle
            _portsList.BackColor = Color.White
            For Each definition As PortDefinition In PortProfiles.Catalog
                _portsList.Items.Add(definition)
            Next
            Controls.Add(_portsList)

            Dim customLabel As New Label With {
                .Text = "PUERTOS ADICIONALES",
                .ForeColor = Color.FromArgb(70, 84, 103),
                .Font = New Font("Segoe UI Semibold", 8.5F),
                .AutoSize = True,
                .Location = New Point(20, 356)
            }
            Controls.Add(customLabel)

            _customPorts.Location = New Point(20, 377)
            _customPorts.Size = New Size(318, 25)
            _customPorts.PlaceholderTextCompat("Ejemplo: 81, 10000-10010")
            Controls.Add(_customPorts)

            Dim optionsGroup As New GroupBox With {
                .Text = " Opciones de identificación ",
                .Location = New Point(356, 355),
                .Size = New Size(314, 83),
                .ForeColor = Color.FromArgb(52, 68, 88)
            }
            _resolveDns.Text = "Resolver nombre DNS"
            _resolveDns.Location = New Point(17, 23)
            _resolveDns.AutoSize = True
            _resolveMac.Text = "Obtener MAC y fabricante (red local)"
            _resolveMac.Location = New Point(17, 49)
            _resolveMac.AutoSize = True
            optionsGroup.Controls.Add(_resolveDns)
            optionsGroup.Controls.Add(_resolveMac)
            Controls.Add(optionsGroup)

            Dim performancePanel As New Panel With {
                .Location = New Point(20, 420),
                .Size = New Size(318, 79),
                .BackColor = Color.White,
                .BorderStyle = BorderStyle.FixedSingle
            }
            ConfigureNumeric(_pingTimeout, 100, 5000, 450, New Point(104, 9))
            ConfigureNumeric(_tcpTimeout, 100, 5000, 350, New Point(104, 42))
            performancePanel.Controls.Add(CreateCaption("Ping (ms)", New Point(10, 13)))
            performancePanel.Controls.Add(CreateCaption("Puerto (ms)", New Point(10, 46)))
            performancePanel.Controls.Add(_pingTimeout)
            performancePanel.Controls.Add(_tcpTimeout)
            ConfigureNumeric(_dnsTimeout, 100, 10000, 900, New Point(242, 9))
            ConfigureNumeric(_concurrency, 1, 256, 12, New Point(242, 42))
            performancePanel.Controls.Add(CreateCaption("DNS", New Point(183, 13)))
            performancePanel.Controls.Add(CreateCaption("Hilos", New Point(183, 46)))
            performancePanel.Controls.Add(_dnsTimeout)
            performancePanel.Controls.Add(_concurrency)
            Controls.Add(performancePanel)

            Dim hint As New Label With {
                .Text = "Más puertos y timeouts altos aumentan la precisión, pero alargan el escaneo.",
                .ForeColor = Color.FromArgb(91, 104, 120),
                .AutoSize = True,
                .Location = New Point(356, 460)
            }
            Controls.Add(hint)

            _acceptButton.Text = "Aceptar"
            _acceptButton.Size = New Size(105, 34)
            _acceptButton.Location = New Point(449, 518)
            _acceptButton.BackColor = Color.FromArgb(28, 132, 91)
            _acceptButton.ForeColor = Color.White
            _acceptButton.FlatStyle = FlatStyle.Flat
            _acceptButton.FlatAppearance.BorderSize = 0
            AddHandler _acceptButton.Click, AddressOf OnAccept
            Controls.Add(_acceptButton)

            _cancelButton.Text = "Cancelar"
            _cancelButton.Size = New Size(105, 34)
            _cancelButton.Location = New Point(565, 518)
            _cancelButton.BackColor = Color.FromArgb(226, 232, 240)
            _cancelButton.ForeColor = Color.FromArgb(45, 55, 72)
            _cancelButton.FlatStyle = FlatStyle.Flat
            _cancelButton.FlatAppearance.BorderSize = 0
            _cancelButton.DialogResult = DialogResult.Cancel
            Controls.Add(_cancelButton)

            AcceptButton = _acceptButton
            CancelButton = _cancelButton
        End Sub

        ''' <summary>
        ''' NumericUpDown centralizado para mantener límites y estética coherentes.
        ''' </summary>
        Private Shared Sub ConfigureNumeric(control As NumericUpDown,
                                            minimum As Integer,
                                            maximum As Integer,
                                            defaultValue As Integer,
                                            location As Point)
            control.Minimum = minimum
            control.Maximum = maximum
            control.Value = defaultValue
            control.Location = location
            control.Size = New Size(67, 25)
            control.TextAlign = HorizontalAlignment.Right
        End Sub

        Private Shared Function CreateCaption(text As String, location As Point) As Label
            Return New Label With {.Text = text, .AutoSize = True, .Location = location, .ForeColor = Color.FromArgb(65, 78, 96)}
        End Function

        ''' <summary>
        ''' Marca los puertos vigentes y carga los valores de rendimiento.
        ''' </summary>
        Private Sub LoadCurrentSettings()
            For index As Integer = 0 To _portsList.Items.Count - 1
                Dim definition As PortDefinition = DirectCast(_portsList.Items(index), PortDefinition)
                _portsList.SetItemChecked(index, Settings.Ports.Contains(definition.Port))
            Next
            _pingTimeout.Value = Math.Max(_pingTimeout.Minimum, Math.Min(_pingTimeout.Maximum, Settings.PingTimeoutMs))
            _tcpTimeout.Value = Math.Max(_tcpTimeout.Minimum, Math.Min(_tcpTimeout.Maximum, Settings.TcpTimeoutMs))
            _dnsTimeout.Value = Math.Max(_dnsTimeout.Minimum, Math.Min(_dnsTimeout.Maximum, Settings.DnsTimeoutMs))
            _concurrency.Value = Math.Max(_concurrency.Minimum, Math.Min(_concurrency.Maximum, Settings.MaxConcurrency))
            _resolveDns.Checked = Settings.ResolveDns
            _resolveMac.Checked = Settings.ResolveMac

            ' Los puertos que no pertenecen al catálogo se conservan como adicionales.
            Dim catalogPorts As New HashSet(Of Integer)(PortProfiles.Catalog.Select(Function(item) item.Port))
            Dim extraPorts As IEnumerable(Of Integer) = Settings.Ports.Where(Function(port) Not catalogPorts.Contains(port))
            _customPorts.Text = PortProfiles.ToCsv(extraPorts)
        End Sub

        ''' <summary>
        ''' Aplica una selección predefinida sin cerrar el diálogo.
        ''' </summary>
        Private Sub OnProfileClick(sender As Object, e As EventArgs)
            Dim button As Button = TryCast(sender, Button)
            If button Is Nothing Then Return

            Dim selectedPorts As List(Of Integer)
            Select Case Convert.ToString(button.Tag, Globalization.CultureInfo.InvariantCulture)
                Case "Básico"
                    selectedPorts = PortProfiles.BasicPorts()
                Case "Web + remoto"
                    selectedPorts = PortProfiles.WebAndRemotePorts()
                Case "Industrial"
                    selectedPorts = PortProfiles.IndustrialPorts()
                Case "Todos"
                    selectedPorts = PortProfiles.AllCatalogPorts()
                Case Else
                    selectedPorts = New List(Of Integer)()
            End Select

            For index As Integer = 0 To _portsList.Items.Count - 1
                Dim definition As PortDefinition = DirectCast(_portsList.Items(index), PortDefinition)
                _portsList.SetItemChecked(index, selectedPorts.Contains(definition.Port))
            Next
            _customPorts.Clear()
        End Sub

        ''' <summary>
        ''' Valida y combina puertos del catálogo con los adicionales antes de confirmar.
        ''' </summary>
        Private Sub OnAccept(sender As Object, e As EventArgs)
            Dim selectedPorts As New HashSet(Of Integer)()
            For Each item As Object In _portsList.CheckedItems
                selectedPorts.Add(DirectCast(item, PortDefinition).Port)
            Next

            Dim custom As List(Of Integer) = Nothing
            Dim errorMessage As String = String.Empty
            If Not PortProfiles.TryParse(_customPorts.Text, custom, errorMessage) Then
                MessageBox.Show(errorMessage, "Puertos", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                _customPorts.Focus()
                Return
            End If
            selectedPorts.UnionWith(custom)

            ' El límite es para la unión final, pues cada entrada puede cumplirlo por separado.
            If selectedPorts.Count > 1024 Then
                MessageBox.Show("Se permiten como máximo 1.024 puertos distintos por barrido.",
                                "Puertos", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                _customPorts.Focus()
                Return
            End If

            If selectedPorts.Count > 16 Then
                Dim intensiveAnswer As DialogResult = MessageBox.Show(
                    "Has seleccionado " & selectedPorts.Count.ToString() & " puertos por cada dirección IP." &
                    Environment.NewLine & Environment.NewLine &
                    "Este perfil puede ser identificado como un escaneo intensivo por herramientas de seguridad." &
                    Environment.NewLine & "¿Deseas utilizarlo durante esta sesión?",
                    "Perfil intensivo", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                If intensiveAnswer <> DialogResult.Yes Then Return
            End If

            If selectedPorts.Count = 0 Then
                Dim answer As DialogResult = MessageBox.Show(
                    "No se comprobará ningún puerto. Sólo se descubrirán equipos que respondan a ping." & Environment.NewLine &
                    "¿Deseas continuar?", "Sin puertos TCP", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                If answer <> DialogResult.Yes Then Return
            End If

            Settings = New ScanSettings With {
                .PingTimeoutMs = Decimal.ToInt32(_pingTimeout.Value),
                .TcpTimeoutMs = Decimal.ToInt32(_tcpTimeout.Value),
                .DnsTimeoutMs = Decimal.ToInt32(_dnsTimeout.Value),
                .MaxConcurrency = Decimal.ToInt32(_concurrency.Value),
                .MaxPortConcurrency = Math.Max(8, Math.Min(64, Decimal.ToInt32(_concurrency.Value) * 2)),
                .ResolveDns = _resolveDns.Checked,
                .ResolveMac = _resolveMac.Checked,
                .Ports = selectedPorts.OrderBy(Function(port) port).ToList()
            }

            DialogResult = DialogResult.OK
            Close()
        End Sub
    End Class

    ''' <summary>
    ''' Extensión compatible con .NET Framework: simula un placeholder sin exigir
    ''' propiedades disponibles únicamente en WinForms moderno.
    ''' </summary>
    Friend Module TextBoxExtensions
        <Global.System.Runtime.CompilerServices.Extension>
        Public Sub PlaceholderTextCompat(textBox As TextBox, hint As String)
            If textBox Is Nothing OrElse String.IsNullOrWhiteSpace(hint) Then Return
            AddHandler textBox.HandleCreated,
                Sub(sender As Object, e As EventArgs)
                    NativeMethods.SendMessage(textBox.Handle, &H1501, New IntPtr(1), hint)
                End Sub
        End Sub

        Private NotInheritable Class NativeMethods
            Private Sub New()
            End Sub

            <Global.System.Runtime.InteropServices.DllImport("user32.dll", CharSet:=Global.System.Runtime.InteropServices.CharSet.Unicode)>
            Public Shared Function SendMessage(windowHandle As IntPtr,
                                               message As Integer,
                                               value As IntPtr,
                                               text As String) As IntPtr
            End Function
        End Class
    End Module
End Namespace
