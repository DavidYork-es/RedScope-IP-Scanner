Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Threading
Imports System.Windows.Forms
Imports RedScope.Core

Namespace RedScope.Forms
    ''' <summary>
    ''' Formulario compacto que consulta y muestra la IP pública bajo demanda.
    ''' La dirección sólo permanece en los controles mientras la ventana está abierta.
    ''' </summary>
    Public NotInheritable Class PublicIpForm
        Inherits Form

        Private ReadOnly _statusLabel As New Label()
        Private ReadOnly _addressLabel As New Label()
        Private ReadOnly _sourceLabel As New Label()
        Private ReadOnly _copyButton As New Button()
        Private ReadOnly _refreshButton As New Button()
        Private _queryCancellation As CancellationTokenSource

        Public Sub New()
            ConfigureWindow()
            CreateInterface()
            ConfigureEvents()
        End Sub

        ''' <summary>
        ''' Fija un tamaño estable y evita una ventana adicional en la barra de tareas.
        ''' </summary>
        Private Sub ConfigureWindow()
            Text = "IP pública"
            StartPosition = FormStartPosition.CenterParent
            FormBorderStyle = FormBorderStyle.FixedDialog
            MaximizeBox = False
            MinimizeBox = False
            ShowInTaskbar = False
            ClientSize = New Size(510, 318)
            BackColor = Color.White
            Font = New Font("Segoe UI", 9.0F)

            Try
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
            Catch ex As Exception
                ' El formulario continúa funcionando si el icono no existe en depuración.
            End Try
        End Sub

        ''' <summary>
        ''' Construye la cabecera y separa visualmente el resultado, la explicación de
        ''' privacidad y las acciones para facilitar una revisión rápida del usuario.
        ''' </summary>
        Private Sub CreateInterface()
            Dim header As New Panel With {
                .Dock = DockStyle.Top,
                .Height = 82,
                .BackColor = Color.FromArgb(18, 35, 58)
            }
            Dim iconPicture As New PictureBox With {
                .Location = New Point(20, 16),
                .Size = New Size(48, 48),
                .SizeMode = PictureBoxSizeMode.Zoom,
                .BackColor = Color.Transparent
            }
            AppIconHelper.AssignTo(iconPicture, 48)
            Dim title As New Label With {
                .Text = "IP PÚBLICA",
                .Font = New Font("Segoe UI Semibold", 17.0F),
                .ForeColor = Color.White,
                .AutoSize = True,
                .Location = New Point(84, 15)
            }
            Dim subtitle As New Label With {
                .Text = "Consulta externa únicamente bajo demanda",
                .ForeColor = Color.FromArgb(182, 202, 226),
                .AutoSize = True,
                .Location = New Point(87, 49)
            }
            header.Controls.Add(iconPicture)
            header.Controls.Add(title)
            header.Controls.Add(subtitle)
            Controls.Add(header)

            _statusLabel.Text = "Consultando la dirección pública..."
            _statusLabel.ForeColor = Color.FromArgb(90, 105, 124)
            _statusLabel.AutoSize = True
            _statusLabel.Location = New Point(24, 102)
            Controls.Add(_statusLabel)

            _addressLabel.Text = "—"
            _addressLabel.Font = New Font("Segoe UI Semibold", 21.0F)
            _addressLabel.ForeColor = Color.FromArgb(28, 151, 103)
            _addressLabel.AutoEllipsis = True
            _addressLabel.TextAlign = ContentAlignment.MiddleLeft
            _addressLabel.Location = New Point(23, 126)
            _addressLabel.Size = New Size(462, 43)
            Controls.Add(_addressLabel)

            _sourceLabel.Text = "Fuente: pendiente"
            _sourceLabel.ForeColor = Color.FromArgb(90, 105, 124)
            _sourceLabel.AutoSize = True
            _sourceLabel.Location = New Point(25, 173)
            Controls.Add(_sourceLabel)

            Dim privacyPanel As New Panel With {
                .Location = New Point(24, 199),
                .Size = New Size(462, 57),
                .BackColor = Color.FromArgb(235, 244, 255)
            }
            Dim privacyLabel As New Label With {
                .Text = "Al abrir o actualizar se contacta un servicio externo por HTTPS. " &
                        "No se envían datos del escaneo y la IP obtenida no se guarda.",
                .ForeColor = Color.FromArgb(42, 77, 119),
                .AutoSize = False,
                .Location = New Point(12, 9),
                .Size = New Size(438, 40)
            }
            privacyPanel.Controls.Add(privacyLabel)
            Controls.Add(privacyPanel)

            ConfigureActionButton(_copyButton, "Copiar", Color.FromArgb(28, 151, 103), New Point(174, 270), 92)
            ConfigureActionButton(_refreshButton, "Actualizar", Color.FromArgb(42, 113, 204), New Point(273, 270), 102)
            Dim closeButton As New Button With {
                .Text = "Cerrar",
                .DialogResult = DialogResult.Cancel,
                .BackColor = Color.FromArgb(90, 105, 124),
                .ForeColor = Color.White,
                .FlatStyle = FlatStyle.Flat,
                .Location = New Point(382, 270),
                .Size = New Size(104, 34)
            }
            closeButton.FlatAppearance.BorderSize = 0
            Controls.Add(_copyButton)
            Controls.Add(_refreshButton)
            Controls.Add(closeButton)
            CancelButton = closeButton
        End Sub

        ''' <summary>
        ''' Mantiene el mismo lenguaje visual en las acciones del formulario.
        ''' </summary>
        Private Shared Sub ConfigureActionButton(button As Button, text As String, background As Color,
                                                 location As Point, width As Integer)
            button.Text = text
            button.BackColor = background
            button.ForeColor = Color.White
            button.FlatStyle = FlatStyle.Flat
            button.FlatAppearance.BorderSize = 0
            button.Cursor = Cursors.Hand
            button.Location = location
            button.Size = New Size(width, 34)
        End Sub

        ''' <summary>
        ''' Centraliza los eventos y cancela cualquier petición al cerrar para impedir que
        ''' una respuesta tardía intente modificar controles ya destruidos.
        ''' </summary>
        Private Sub ConfigureEvents()
            AddHandler Shown, AddressOf RefreshPublicIp
            AddHandler _refreshButton.Click, AddressOf RefreshPublicIp
            AddHandler _copyButton.Click, AddressOf CopyAddress
            AddHandler FormClosing,
                Sub(sender As Object, e As FormClosingEventArgs)
                    If _queryCancellation IsNot Nothing Then _queryCancellation.Cancel()
                End Sub
        End Sub

        ''' <summary>
        ''' Lanza la consulta sin bloquear la interfaz. Cada actualización cancela la
        ''' anterior, y el CancellationTokenSource local se libera siempre al terminar.
        ''' </summary>
        Private Async Sub RefreshPublicIp(sender As Object, e As EventArgs)
            If _queryCancellation IsNot Nothing Then _queryCancellation.Cancel()

            Dim currentCancellation As New CancellationTokenSource()
            _queryCancellation = currentCancellation
            SetBusyState(True)

            Try
                Dim result As PublicIpResult = Await PublicIpService.QueryAsync(currentCancellation.Token)
                If currentCancellation.IsCancellationRequested OrElse IsDisposed Then Return

                _addressLabel.Text = result.Address
                _sourceLabel.Text = "Fuente: " & result.Source
                _statusLabel.Text = "Dirección pública detectada"
                _statusLabel.ForeColor = Color.FromArgb(28, 151, 103)
                _copyButton.Enabled = True
            Catch ex As OperationCanceledException
                ' Cerrar o actualizar cancela deliberadamente la petición; no es un fallo.
            Catch ex As Exception
                If currentCancellation.IsCancellationRequested OrElse IsDisposed Then Return

                _addressLabel.Text = "No disponible"
                _addressLabel.ForeColor = Color.FromArgb(207, 68, 72)
                _sourceLabel.Text = "Fuente: sin respuesta"
                _statusLabel.Text = "No se pudo obtener la IP. Comprueba Internet, proxy o firewall."
                _statusLabel.ForeColor = Color.FromArgb(207, 68, 72)
            Finally
                If Not IsDisposed AndAlso ReferenceEquals(_queryCancellation, currentCancellation) Then
                    _queryCancellation = Nothing
                    SetBusyState(False)
                End If
                currentCancellation.Dispose()
            End Try
        End Sub

        ''' <summary>
        ''' Actualiza controles durante la consulta y evita peticiones simultáneas desde
        ''' el mismo formulario, manteniendo disponible el botón Cerrar.
        ''' </summary>
        Private Sub SetBusyState(busy As Boolean)
            If busy Then
                _addressLabel.Text = "Consultando..."
                _addressLabel.ForeColor = Color.FromArgb(42, 113, 204)
                _sourceLabel.Text = "Fuente: pendiente"
                _statusLabel.Text = "Consultando la dirección pública..."
                _statusLabel.ForeColor = Color.FromArgb(90, 105, 124)
                _copyButton.Enabled = False
            End If
            _refreshButton.Enabled = Not busy
        End Sub

        ''' <summary>
        ''' Copia exclusivamente el texto validado que se muestra. El portapapeles sólo
        ''' se modifica por una acción explícita y no se utiliza como almacenamiento propio.
        ''' </summary>
        Private Sub CopyAddress(sender As Object, e As EventArgs)
            If Not _copyButton.Enabled OrElse String.IsNullOrWhiteSpace(_addressLabel.Text) Then Return

            Try
                Clipboard.SetText(_addressLabel.Text)
                _statusLabel.Text = "Dirección copiada al portapapeles"
                _statusLabel.ForeColor = Color.FromArgb(28, 151, 103)
            Catch ex As Exception
                MessageBox.Show("Windows no permitió acceder al portapapeles.", "IP pública",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End Sub
    End Class
End Namespace
