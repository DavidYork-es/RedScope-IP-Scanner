Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Collections.Concurrent
Imports System.Data
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports RedScope.Core
Imports RedScope.Models

Namespace RedScope.Forms
    ''' <summary>
    ''' Ventana principal de RedScope. Coordina el motor, la presentación de resultados,
    ''' los accesos rápidos de red y las opciones de la sesión; no contiene lógica de sondeo
    ''' ni guarda configuración en el equipo.
    ''' </summary>
    Public NotInheritable Class MainForm
        Inherits Form

        Private Shared ReadOnly Navy As Color = Color.FromArgb(18, 35, 58)
        Private Shared ReadOnly Blue As Color = Color.FromArgb(42, 113, 204)
        Private Shared ReadOnly Green As Color = Color.FromArgb(28, 151, 103)
        Private Shared ReadOnly Amber As Color = Color.FromArgb(218, 145, 32)
        Private Shared ReadOnly Red As Color = Color.FromArgb(207, 68, 72)
        Private Shared ReadOnly Surface As Color = Color.FromArgb(244, 247, 251)

        Private ReadOnly _rangeText As New TextBox()
        Private ReadOnly _searchText As New TextBox()
        Private ReadOnly _showOffline As New CheckBox()
        Private ReadOnly _startButton As New Button()
        Private ReadOnly _pauseButton As New Button()
        Private ReadOnly _stopButton As New Button()
        Private ReadOnly _portsButton As New Button()
        Private ReadOnly _exportButton As New Button()
        Private ReadOnly _localRangeButton As New Button()
        Private ReadOnly _publicIpButton As New Button()
        Private ReadOnly _infoButton As New Button()
        Private ReadOnly _grid As New BufferedDataGridView()
        Private ReadOnly _progressBar As New ProgressBar()
        Private ReadOnly _statusLabel As New Label()
        Private ReadOnly _countersLabel As New Label()
        Private ReadOnly _portSummaryLabel As New Label()
        Private ReadOnly _toolTip As New ToolTip()
        Private ReadOnly _table As New DataTable("ScanResults")
        Private ReadOnly _view As DataView
        Private ReadOnly _allResults As New Dictionary(Of String, ScanResult)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly _sessionComments As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly _scanner As New NetworkScanner()
        Private ReadOnly _pauseGate As New ManualResetEventSlim(True)

        Private _settings As ScanSettings
        Private _scanCancellation As CancellationTokenSource
        Private _scanRunning As Boolean
        Private _scanPaused As Boolean
        Private _ipSortAscending As Boolean = True
        Private _boldStatusFont As Font

        ' Una sola notificación de UI vacía una cola de resultados, en lugar de
        ' publicar un BeginInvoke por IP. El progreso sólo conserva el valor más reciente.
        Private ReadOnly _pendingResults As New ConcurrentQueue(Of ScanResult)()
        Private _resultFlushScheduled As Integer
        Private _latestProgress As ScanProgress
        Private _progressFlushScheduled As Integer
        Private _scanSession As Integer
        Private _closeRequested As Boolean
        Private _isClosing As Boolean

        Public Sub New()
            _settings = New ScanSettings With {
                .Ports = PortProfiles.BasicPorts()
            }
            CreateDataTable()
            _view = New DataView(_table) With {.Sort = "NumericIp ASC"}
            InitializeInterface()

            _rangeText.Text = NativeNetwork.GetRecommendedLocalRange()
            _showOffline.Checked = False
            UpdatePortSummary()
            UpdateCommandState(False)
        End Sub

        ''' <summary>
        ''' Define el esquema de datos una sola vez. NumericIp permanece oculto y permite
        ''' ordenar direcciones como números, no alfabéticamente (evita 1, 10, 100, 2).
        ''' </summary>
        Private Sub CreateDataTable()
            _table.Columns.Add("Estado", GetType(String))
            _table.Columns.Add("Nombre", GetType(String))
            Dim ipColumn As DataColumn = _table.Columns.Add("IP", GetType(String))
            _table.Columns.Add("NumericIp", GetType(ULong))
            _table.Columns.Add("MAC", GetType(String))
            _table.Columns.Add("Fabricante", GetType(String))
            _table.Columns.Add("Latencia", GetType(String))
            _table.Columns.Add("Puertos", GetType(String))
            _table.Columns.Add("Última evidencia", GetType(String))
            _table.Columns.Add("Comentario", GetType(String))
            _table.PrimaryKey = New DataColumn() {ipColumn}
        End Sub

        ''' <summary>
        ''' Construye una interfaz de alta densidad con tres franjas y una rejilla central.
        ''' Todos los recursos gráficos se generan en ejecución para mantener un único EXE.
        ''' </summary>
        Private Sub InitializeInterface()
            Text = "RedScope · Escáner IPv4"
            StartPosition = FormStartPosition.CenterScreen
            ClientSize = New Size(1180, 700)
            MinimumSize = New Size(1020, 560)
            BackColor = Surface
            Font = New Font("Segoe UI", 9.0F, FontStyle.Regular, GraphicsUnit.Point)
            KeyPreview = True
            Try
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
            Catch ex As Exception
                ' Visual Studio puede ejecutar el formulario antes de copiar el icono.
            End Try

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 5,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty,
                .BackColor = Surface
            }
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 76.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 43.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
            Controls.Add(root)

            root.Controls.Add(CreateHeader(), 0, 0)
            root.Controls.Add(CreateCommandBar(), 0, 1)
            root.Controls.Add(CreateFilterBar(), 0, 2)
            ConfigureGrid()
            root.Controls.Add(_grid, 0, 3)
            root.Controls.Add(CreateStatusBar(), 0, 4)

            CreateContextMenu()
            ConfigureEvents()
            ConfigureToolTips()
        End Sub

        ''' <summary>
        ''' Cabecera propia, visualmente distinta del programa tomado como referencia.
        ''' </summary>
        Private Function CreateHeader() As Control
            Dim panel As New Panel With {.Dock = DockStyle.Fill, .BackColor = Navy, .Margin = Padding.Empty}
            Dim iconPicture As New PictureBox With {
                .Location = New Point(15, 10),
                .Size = New Size(38, 38),
                .SizeMode = PictureBoxSizeMode.Zoom,
                .BackColor = Color.Transparent
            }
            AppIconHelper.AssignTo(iconPicture, 38)
            Dim title As New Label With {
                .Text = "REDSCOPE",
                .Font = New Font("Segoe UI Semibold", 15.0F),
                .ForeColor = Color.White,
                .AutoSize = True,
                .Location = New Point(64, 8)
            }
            Dim subtitle As New Label With {
                .Text = "Escáner IPv4 · inventario y diagnóstico",
                .ForeColor = Color.FromArgb(171, 194, 222),
                .AutoSize = True,
                .Location = New Point(66, 33)
            }

            ConfigureButton(_publicIpButton, "IP PÚBLICA", Blue, 105)
            _publicIpButton.Anchor = AnchorStyles.Top Or AnchorStyles.Right
            ConfigureButton(_infoButton, "?", Color.FromArgb(42, 61, 85), 38)
            _infoButton.Anchor = AnchorStyles.Top Or AnchorStyles.Right
            _infoButton.Location = New Point(panel.Width - 53, 10)
            _publicIpButton.Location = New Point(_infoButton.Left - _publicIpButton.Width - 8, 10)
            AddHandler panel.Resize,
                Sub(sender As Object, e As EventArgs)
                    _infoButton.Left = Math.Max(0, panel.ClientSize.Width - _infoButton.Width - 15)
                    _publicIpButton.Left = Math.Max(0, _infoButton.Left - _publicIpButton.Width - 8)
                End Sub

            panel.Controls.Add(iconPicture)
            panel.Controls.Add(title)
            panel.Controls.Add(subtitle)
            panel.Controls.Add(_publicIpButton)
            panel.Controls.Add(_infoButton)
            Return panel
        End Function

        ''' <summary>
        ''' Barra principal con rango expansible y acciones de exploración.
        ''' </summary>
        Private Function CreateCommandBar() As Control
            Dim outer As New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.White, .Padding = New Padding(14, 8, 14, 8), .Margin = Padding.Empty}
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .Margin = Padding.Empty, .Padding = Padding.Empty}
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            layout.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

            Dim rangePanel As New Panel With {.Dock = DockStyle.Fill, .Margin = New Padding(0, 0, 10, 0)}
            Dim rangeLabel As New Label With {
                .Text = "RANGO IPv4",
                .Font = New Font("Segoe UI Semibold", 8.5F),
                .ForeColor = Color.FromArgb(73, 87, 106),
                .AutoSize = True,
                .Location = New Point(0, 0)
            }
            _rangeText.Location = New Point(0, 23)
            _rangeText.Height = 28
            _rangeText.Anchor = AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Top
            _rangeText.PlaceholderTextCompat("192.168.1.1-254; 10.0.0.0/24")
            AddHandler rangePanel.Resize,
                Sub(sender As Object, e As EventArgs)
                    _rangeText.Width = Math.Max(210, rangePanel.ClientSize.Width - 2)
                End Sub
            rangePanel.Controls.Add(rangeLabel)
            rangePanel.Controls.Add(_rangeText)

            Dim buttons As New FlowLayoutPanel With {
                .AutoSize = True,
                .WrapContents = False,
                .FlowDirection = FlowDirection.LeftToRight,
                .Dock = DockStyle.Fill,
                .Margin = Padding.Empty,
                .Padding = New Padding(0, 16, 0, 0)
            }
            ConfigureButton(_localRangeButton, "Mi red", Color.FromArgb(90, 105, 124), 78)
            ConfigureButton(_startButton, "Iniciar", Green, 88)
            ConfigureButton(_pauseButton, "Pausar", Amber, 88)
            ConfigureButton(_stopButton, "Detener", Red, 88)
            ConfigureButton(_portsButton, "Puertos", Blue, 91)
            ConfigureButton(_exportButton, "Exportar", Color.FromArgb(96, 81, 164), 91)
            buttons.Controls.Add(_localRangeButton)
            buttons.Controls.Add(_startButton)
            buttons.Controls.Add(_pauseButton)
            buttons.Controls.Add(_stopButton)
            buttons.Controls.Add(_portsButton)
            buttons.Controls.Add(_exportButton)

            layout.Controls.Add(rangePanel, 0, 0)
            layout.Controls.Add(buttons, 1, 0)
            outer.Controls.Add(layout)
            Return outer
        End Function

        ''' <summary>
        ''' Segunda barra reservada a criterios de visualización; no modifica el escaneo activo.
        ''' </summary>
        Private Function CreateFilterBar() As Control
            Dim panel As New Panel With {
                .Dock = DockStyle.Fill,
                .BackColor = Color.FromArgb(232, 238, 246),
                .Padding = New Padding(14, 8, 14, 6),
                .Margin = Padding.Empty
            }

            _portSummaryLabel.AutoSize = True
            _portSummaryLabel.ForeColor = Color.FromArgb(57, 72, 91)
            _portSummaryLabel.Location = New Point(15, 13)
            panel.Controls.Add(_portSummaryLabel)

            _showOffline.Text = "Mostrar sin respuesta"
            _showOffline.AutoSize = True
            _showOffline.ForeColor = Color.FromArgb(57, 72, 91)
            _showOffline.Location = New Point(278, 11)
            panel.Controls.Add(_showOffline)

            Dim historyLegend As New Label With {
                .Text = "◌ Histórico / rastro ARP",
                .AutoSize = True,
                .ForeColor = Color.FromArgb(112, 120, 132),
                .Location = New Point(430, 13)
            }
            panel.Controls.Add(historyLegend)

            Dim searchCaption As New Label With {
                .Text = "BUSCAR",
                .Font = New Font("Segoe UI Semibold", 8.0F),
                .ForeColor = Color.FromArgb(73, 87, 106),
                .AutoSize = True,
                .Anchor = AnchorStyles.Top Or AnchorStyles.Right,
                .Location = New Point(panel.Width - 314, 13)
            }
            _searchText.Size = New Size(230, 25)
            _searchText.Anchor = AnchorStyles.Top Or AnchorStyles.Right
            _searchText.Location = New Point(panel.Width - 245, 8)
            _searchText.PlaceholderTextCompat("nombre, IP, MAC, puerto...")
            AddHandler panel.Resize,
                Sub(sender As Object, e As EventArgs)
                    searchCaption.Left = Math.Max(500, panel.ClientSize.Width - 314)
                    _searchText.Left = Math.Max(565, panel.ClientSize.Width - 245)
                End Sub
            panel.Controls.Add(searchCaption)
            panel.Controls.Add(_searchText)
            Return panel
        End Function

        ''' <summary>
        ''' Crea una barra de estado sencilla con progreso y contadores legibles.
        ''' </summary>
        Private Function CreateStatusBar() As Control
            Dim panel As New Panel With {.Dock = DockStyle.Fill, .BackColor = Navy, .Margin = Padding.Empty}
            _statusLabel.Text = "Preparado"
            _statusLabel.ForeColor = Color.White
            _statusLabel.AutoEllipsis = True
            _statusLabel.Location = New Point(14, 8)
            _statusLabel.Size = New Size(420, 19)
            _statusLabel.Anchor = AnchorStyles.Left Or AnchorStyles.Top Or AnchorStyles.Right

            _countersLabel.Text = "0 activos · 0 sin respuesta"
            _countersLabel.ForeColor = Color.FromArgb(190, 208, 229)
            _countersLabel.TextAlign = ContentAlignment.MiddleRight
            _countersLabel.Size = New Size(330, 20)
            _countersLabel.Location = New Point(panel.Width - 656, 7)
            _countersLabel.Anchor = AnchorStyles.Top Or AnchorStyles.Right

            _progressBar.Size = New Size(300, 12)
            _progressBar.Location = New Point(panel.Width - 314, 11)
            _progressBar.Anchor = AnchorStyles.Top Or AnchorStyles.Right
            _progressBar.Style = ProgressBarStyle.Continuous
            _progressBar.Minimum = 0
            _progressBar.Maximum = 1

            AddHandler panel.Resize,
                Sub(sender As Object, e As EventArgs)
                    _progressBar.Left = Math.Max(700, panel.ClientSize.Width - _progressBar.Width - 14)
                    _countersLabel.Left = Math.Max(430, _progressBar.Left - _countersLabel.Width - 12)
                    _statusLabel.Width = Math.Max(200, _countersLabel.Left - 24)
                End Sub

            panel.Controls.Add(_statusLabel)
            panel.Controls.Add(_countersLabel)
            panel.Controls.Add(_progressBar)
            Return panel
        End Function

        ''' <summary>
        ''' Aplica un estilo propio a todos los botones de acción.
        ''' </summary>
        Private Shared Sub ConfigureButton(button As Button, text As String, background As Color, width As Integer)
            button.Text = text
            button.BackColor = background
            button.ForeColor = Color.White
            button.FlatStyle = FlatStyle.Flat
            button.FlatAppearance.BorderSize = 0
            button.Cursor = Cursors.Hand
            button.Size = New Size(width, 34)
            button.Margin = New Padding(3, 0, 3, 0)
            button.Font = New Font("Segoe UI Semibold", 9.0F)
        End Sub

        ''' <summary>
        ''' Define columnas, comportamiento de edición y colores de la rejilla.
        ''' </summary>
        Private Sub ConfigureGrid()
            _grid.Dock = DockStyle.Fill
            _grid.Margin = New Padding(12, 8, 12, 8)
            _grid.BackgroundColor = Color.White
            _grid.BorderStyle = BorderStyle.None
            _grid.AutoGenerateColumns = False
            _grid.AllowUserToAddRows = False
            _grid.AllowUserToDeleteRows = False
            _grid.AllowUserToResizeRows = False
            _grid.RowHeadersVisible = False
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            _grid.MultiSelect = False
            _grid.ReadOnly = False
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
            _grid.RowTemplate.Height = 29
            _grid.EnableHeadersVisualStyles = False
            _grid.ColumnHeadersHeight = 36
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(42, 57, 78)
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White
            _grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI Semibold", 9.0F)
            _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(42, 57, 78)
            ' Fuente reutilizable para todas las celdas de estado; se libera al cerrar.
            _boldStatusFont = New Font(_grid.Font, FontStyle.Bold)
            _grid.DefaultCellStyle.BackColor = Color.White
            _grid.DefaultCellStyle.ForeColor = Color.FromArgb(39, 52, 68)
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(211, 229, 248)
            _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(21, 40, 62)
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253)
            _grid.GridColor = Color.FromArgb(226, 232, 240)
            _grid.DataSource = _view

            _grid.Columns.Add(CreateTextColumn("Estado", "Estado", 112, True))
            _grid.Columns.Add(CreateTextColumn("Nombre", "Nombre", 155, True))
            Dim ipColumn As DataGridViewTextBoxColumn = CreateTextColumn("IP", "IP", 112, True)
            ipColumn.SortMode = DataGridViewColumnSortMode.Programmatic
            _grid.Columns.Add(ipColumn)
            _grid.Columns.Add(CreateTextColumn("MAC", "Dirección MAC", 125, True))
            _grid.Columns.Add(CreateTextColumn("Fabricante", "Fabricante", 135, True))
            _grid.Columns.Add(CreateTextColumn("Latencia", "Latencia", 70, True))
            _grid.Columns.Add(CreateTextColumn("Puertos", "Puertos abiertos", 180, True))
            _grid.Columns.Add(CreateTextColumn("Última evidencia", "Última evidencia", 125, True))
            Dim commentColumn As DataGridViewTextBoxColumn = CreateTextColumn("Comentario", "Comentario", 190, False)
            commentColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            commentColumn.MinimumWidth = 130
            _grid.Columns.Add(commentColumn)
        End Sub

        Private Shared Function CreateTextColumn(dataProperty As String,
                                                 header As String,
                                                 width As Integer,
                                                 isReadOnly As Boolean) As DataGridViewTextBoxColumn
            Return New DataGridViewTextBoxColumn With {
                .DataPropertyName = dataProperty,
                .Name = dataProperty,
                .HeaderText = header,
                .Width = width,
                .ReadOnly = isReadOnly,
                .SortMode = DataGridViewColumnSortMode.Automatic
            }
        End Function

        ''' <summary>
        ''' Menú contextual con acciones útiles sin ocupar espacio permanente.
        ''' </summary>
        Private Sub CreateContextMenu()
            Dim menu As New ContextMenuStrip()
            menu.Items.Add("Copiar IP", Nothing, Sub(sender, e) CopySelectedValue("IP"))
            menu.Items.Add("Copiar MAC", Nothing, Sub(sender, e) CopySelectedValue("MAC"))
            menu.Items.Add(New ToolStripSeparator())
            menu.Items.Add("Abrir HTTP", Nothing, Sub(sender, e) OpenSelectedUrl(False))
            menu.Items.Add("Abrir HTTPS", Nothing, Sub(sender, e) OpenSelectedUrl(True))
            menu.Items.Add("Abrir carpeta compartida", Nothing, Sub(sender, e) OpenSharedFolder())
            menu.Items.Add("Conectar por RDP", Nothing, Sub(sender, e) OpenRdp())
            menu.Items.Add("Ping continuo", Nothing, Sub(sender, e) OpenContinuousPing())
            _grid.ContextMenuStrip = menu
        End Sub

        ''' <summary>
        ''' Centraliza eventos para que el constructor sea fácil de revisar.
        ''' </summary>
        Private Sub ConfigureEvents()
            AddHandler _startButton.Click, AddressOf OnStartButtonClick
            AddHandler _pauseButton.Click, AddressOf TogglePause
            AddHandler _stopButton.Click, AddressOf StopScan
            AddHandler _portsButton.Click, AddressOf ShowPortOptions
            AddHandler _exportButton.Click, AddressOf ExportCsv
            AddHandler _localRangeButton.Click, AddressOf SetLocalRange
            AddHandler _publicIpButton.Click, AddressOf ShowPublicIp
            AddHandler _infoButton.Click, AddressOf ShowAbout
            AddHandler _searchText.TextChanged, AddressOf ApplyFilter
            AddHandler _showOffline.CheckedChanged, AddressOf ShowOfflineChanged
            AddHandler _grid.CellFormatting, AddressOf FormatGridCell
            AddHandler _grid.CellEndEdit, AddressOf SaveEditedComment
            AddHandler _grid.CellDoubleClick, AddressOf OnGridDoubleClick
            AddHandler _grid.ColumnHeaderMouseClick, AddressOf OnColumnHeaderClick
            AddHandler _grid.MouseDown, AddressOf OnGridMouseDown
            AddHandler FormClosing, AddressOf HandleFormClosing
        End Sub

        Private Sub ConfigureToolTips()
            _toolTip.SetToolTip(_rangeText, "Formatos: 192.168.1.1-254 · rango completo · IP única · 192.168.1.0/24 · varios separados por ;")
            _toolTip.SetToolTip(_localRangeButton, "Muestra todas las interfaces IPv4 y permite elegir un rango")
            _toolTip.SetToolTip(_publicIpButton, "Consulta y muestra la IP pública sólo bajo demanda")
            _toolTip.SetToolTip(_startButton, "Iniciar un nuevo barrido (F5)")
            _toolTip.SetToolTip(_pauseButton, "Pausar o reanudar la asignación de nuevas IP (F6)")
            _toolTip.SetToolTip(_stopButton, "Cancelar el barrido actual (Esc)")
            _toolTip.SetToolTip(_portsButton, "Elegir servicios TCP, timeouts y concurrencia")
            _toolTip.SetToolTip(_exportButton, "Exportar las filas visibles a CSV UTF-8")
            _toolTip.SetToolTip(_searchText, "Filtra todas las columnas visibles")
            _toolTip.SetToolTip(_grid, "Las filas grises proceden únicamente de la caché ARP actual de Windows")
        End Sub

        ''' <summary>
        ''' Valida entradas, congela las opciones y ejecuta el motor sin bloquear la UI.
        ''' </summary>
        Private Async Function StartScanAsync() As Task
            If _scanRunning OrElse _closeRequested Then Return

            Dim addresses As List(Of IPAddress) = Nothing
            Dim errorMessage As String = String.Empty
            If Not IpRangeParser.TryParse(_rangeText.Text, addresses, errorMessage) Then
                MessageBox.Show(errorMessage, "Rango IPv4", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                _rangeText.Focus()
                Return
            End If

            Dim maximumTcpAttempts As Long = CLng(addresses.Count) * CLng(_settings.Ports.Count)
            If maximumTcpAttempts > 4000L Then
                Dim answer As DialogResult = MessageBox.Show(
                    "Este barrido puede realizar hasta " & maximumTcpAttempts.ToString("N0") &
                    " conexiones TCP (" & addresses.Count.ToString("N0") & " IP × " &
                    _settings.Ports.Count.ToString("N0") & " puertos)." & Environment.NewLine & Environment.NewLine &
                    "Las herramientas de seguridad corporativa pueden identificarlo como un escaneo intensivo." &
                    Environment.NewLine & "Continúa únicamente si la red está autorizada.",
                    "Barrido intensivo", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                If answer <> DialogResult.Yes Then Return
            End If

            _allResults.Clear()
            _table.Clear()
            ApplyFilter(Nothing, EventArgs.Empty)
            ' La sesión diferencia cualquier callback pendiente de un escaneo anterior.
            Dim sessionId As Integer = Interlocked.Increment(_scanSession)
            Dim staleResult As ScanResult = Nothing
            While _pendingResults.TryDequeue(staleResult)
            End While
            Interlocked.Exchange(_latestProgress, DirectCast(Nothing, ScanProgress))
            _scanCancellation = New CancellationTokenSource()
            _pauseGate.Set()
            _scanPaused = False
            _scanRunning = True
            _progressBar.Minimum = 0
            _progressBar.Maximum = Math.Max(1, addresses.Count)
            _progressBar.Value = 0
            _statusLabel.Text = "Preparando " & addresses.Count.ToString("N0") & " direcciones..."
            _countersLabel.Text = "0 activos · 0 sin respuesta"
            UpdateCommandState(True)

            Try
                Await _scanner.ScanAsync(addresses, _settings.CloneSettings(), _pauseGate,
                                         _scanCancellation.Token,
                                         Sub(result) PostResultToUi(result, sessionId),
                                         Sub(progress) PostProgressToUi(progress, sessionId))

                ' El Await retorna al hilo de UI: aplicar toda la cola antes de integrar
                ' ARP y calcular los contadores, sin depender de un Task.Yield arbitrario.
                FlushAllQueuedResults()
                FlushLatestProgress()
                If Not _scanCancellation.IsCancellationRequested Then
                    Dim arpTraceCount As Integer = Await IntegrateArpTracesAsync(addresses, _scanCancellation.Token)
                    ' La colección Values expone una propiedad Count que en VB oculta la
                    ' sobrecarga LINQ con predicado; se invoca Enumerable.Count explícitamente.
                    Dim activeCount As Integer = Enumerable.Count(
                        _allResults.Values,
                        Function(item As ScanResult) item.IsOnline)
                    Dim unansweredCount As Integer = Math.Max(0, addresses.Count - activeCount - arpTraceCount)
                    _statusLabel.Text = "Escaneo completado · " & arpTraceCount.ToString("N0") & " rastros ARP"
                    _countersLabel.Text = activeCount.ToString("N0") & " activos · " &
                                          arpTraceCount.ToString("N0") & " rastros ARP · " &
                                          unansweredCount.ToString("N0") & " sin rastro"
                End If
            Catch ex As OperationCanceledException
                If Not _closeRequested Then _statusLabel.Text = "Escaneo detenido por el usuario"
            Catch ex As Exception
                If Not _closeRequested Then
                    _statusLabel.Text = "Error durante el escaneo"
                    MessageBox.Show("No se pudo completar el escaneo:" & Environment.NewLine & ex.Message,
                                    "RedScope", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End If
            Finally
                ' Task.WhenAll ya ha concluido: es seguro liberar CTS y puerta de pausa.
                If Not _closeRequested Then
                    FlushAllQueuedResults()
                    FlushLatestProgress()
                End If
                _scanRunning = False
                _scanPaused = False
                _pauseGate.Set()
                If _scanCancellation IsNot Nothing Then
                    _scanCancellation.Dispose()
                    _scanCancellation = Nothing
                End If
                If _closeRequested Then
                    ' El primer FormClosing se canceló: ahora sí pueden liberarse recursos.
                    Close()
                Else
                    UpdateCommandState(False)
                End If
            End Try
        End Function

        ''' <summary>
        ''' Adaptador Async Sub exigido por el evento Click; el trabajo real permanece
        ''' en una función Task que puede esperarse y controlar sus errores.
        ''' </summary>
        Private Async Sub OnStartButtonClick(sender As Object, e As EventArgs)
            Await StartScanAsync()
        End Sub

        ''' <summary>
        ''' Guarda resultados sin bloquear a los trabajadores y publica como máximo una
        ''' petición de vaciado. El identificador impide aplicar eventos de otra sesión.
        ''' </summary>
        Private Sub PostResultToUi(result As ScanResult, sessionId As Integer)
            If _isClosing OrElse _closeRequested OrElse sessionId <> Volatile.Read(_scanSession) Then Return
            _pendingResults.Enqueue(result)
            ScheduleResultFlush()
        End Sub

        ''' <summary>
        ''' Publica sólo el progreso más reciente para no saturar la cola de Windows Forms.
        ''' </summary>
        Private Sub PostProgressToUi(progress As ScanProgress, sessionId As Integer)
            If _isClosing OrElse _closeRequested OrElse sessionId <> Volatile.Read(_scanSession) Then Return
            Interlocked.Exchange(_latestProgress, progress)
            ScheduleProgressFlush()
        End Sub

        ''' <summary>
        ''' Encola una única llamada al hilo de UI para procesar un lote de resultados.
        ''' </summary>
        Private Sub ScheduleResultFlush()
            If _isClosing OrElse _closeRequested OrElse IsDisposed OrElse Not IsHandleCreated Then Return
            If Interlocked.CompareExchange(_resultFlushScheduled, 1, 0) <> 0 Then Return
            Try
                BeginInvoke(New Action(AddressOf FlushQueuedResultBatch))
            Catch ex As InvalidOperationException
                ' El control se puede destruir durante el cierre del formulario.
                Interlocked.Exchange(_resultFlushScheduled, 0)
            End Try
        End Sub

        Private Sub FlushQueuedResultBatch()
            If Not _isClosing AndAlso Not _closeRequested Then
                Dim result As ScanResult = Nothing
                Dim processed As Integer = 0
                While processed < 128 AndAlso _pendingResults.TryDequeue(result)
                    AddOrUpdateResult(result)
                    processed += 1
                End While
            End If
            Interlocked.Exchange(_resultFlushScheduled, 0)
            If Not _pendingResults.IsEmpty Then ScheduleResultFlush()
        End Sub

        ''' <summary>
        ''' Barrido definitivo en el hilo de UI cuando termina Task.WhenAll, para que
        ''' el estado final y el resumen ARP se calculen con todas las filas recibidas.
        ''' </summary>
        Private Sub FlushAllQueuedResults()
            Dim result As ScanResult = Nothing
            While _pendingResults.TryDequeue(result)
                If Not _closeRequested Then AddOrUpdateResult(result)
            End While
        End Sub

        Private Sub ScheduleProgressFlush()
            If _isClosing OrElse _closeRequested OrElse IsDisposed OrElse Not IsHandleCreated Then Return
            If Interlocked.CompareExchange(_progressFlushScheduled, 1, 0) <> 0 Then Return
            Try
                BeginInvoke(New Action(AddressOf FlushLatestProgress))
            Catch ex As InvalidOperationException
                Interlocked.Exchange(_progressFlushScheduled, 0)
            End Try
        End Sub

        ''' <summary>
        ''' Consolida el progreso pendiente y reprograma si ha llegado otro valor entre
        ''' la lectura y la liberación del indicador atómico.
        ''' </summary>
        Private Sub FlushLatestProgress()
            Dim latest As ScanProgress = Interlocked.Exchange(_latestProgress, DirectCast(Nothing, ScanProgress))
            If latest IsNot Nothing AndAlso Not _closeRequested Then UpdateProgress(latest)
            Interlocked.Exchange(_progressFlushScheduled, 0)
            If Volatile.Read(_latestProgress) IsNot Nothing Then ScheduleProgressFlush()
        End Sub

        ''' <summary>
        ''' Conserva todos los resultados en memoria y muestra sólo los permitidos por la vista.
        ''' </summary>
        Private Sub AddOrUpdateResult(result As ScanResult)
            Dim key As String = result.Address.ToString()
            If result.IsOnline Then
                result.LastSeen = DateTime.Now
                result.HistorySource = "Escaneo"
            End If
            _allResults(key) = result
            If result.IsOnline OrElse result.IsHistorical OrElse _showOffline.Checked Then UpsertTableRow(result)
        End Sub

        ''' <summary>
        ''' Consulta exclusivamente la caché ARP que Windows mantiene en ese instante y añade
        ''' las IP del rango actual no verificadas como activas. No conserva ningún historial.
        ''' </summary>
        Private Async Function IntegrateArpTracesAsync(addresses As IList(Of IPAddress),
                                                       cancellationToken As CancellationToken) As Task(Of Integer)
            ' Si se pide cerrar mientras Windows consulta ARP, no se agregan filas
            ' tardías a la interfaz ni se retrasa innecesariamente el cierre.
            Dim arpTraces As List(Of HostHistoryEntry) = Await Task.Run(Function() NativeNetwork.GetArpCacheTraces())
            cancellationToken.ThrowIfCancellationRequested()
            Dim requestedAddresses As New HashSet(Of String)(addresses.Select(Function(address) address.ToString()),
                                                              StringComparer.OrdinalIgnoreCase)
            Dim arpTraceCount As Integer
            For Each trace As HostHistoryEntry In arpTraces
                cancellationToken.ThrowIfCancellationRequested()
                Dim key As String = trace.Address.ToString()
                If Not requestedAddresses.Contains(key) Then Continue For

                Dim currentResult As ScanResult = Nothing
                If _allResults.TryGetValue(key, currentResult) AndAlso currentResult.IsOnline Then Continue For

                Dim arpTrace As New ScanResult With {
                    .Address = trace.Address,
                    .HostName = trace.HostName,
                    .MacAddress = trace.MacAddress,
                    .Manufacturer = trace.Manufacturer,
                    .IsOnline = False,
                    .IsHistorical = True,
                    .LastSeen = Nothing,
                    .HistorySource = "Caché ARP"
                }
                _allResults(key) = arpTrace
                UpsertTableRow(arpTrace)
                arpTraceCount += 1
            Next
            Return arpTraceCount
        End Function

        ''' <summary>
        ''' Inserta o actualiza una fila, preservando el comentario asociado a la IP.
        ''' </summary>
        Private Sub UpsertTableRow(result As ScanResult)
            Dim key As String = result.Address.ToString()
            Dim row As DataRow = _table.Rows.Find(key)
            If row Is Nothing Then
                row = _table.NewRow()
                row("IP") = key
                Dim comment As String = String.Empty
                _sessionComments.TryGetValue(key, comment)
                row("Comentario") = comment
                _table.Rows.Add(row)
            End If

            row("Estado") = result.StatusText
            row("Nombre") = If(String.IsNullOrWhiteSpace(result.HostName), key, result.HostName)
            row("NumericIp") = CULng(IpRangeParser.ToUInt32(result.Address))
            row("MAC") = result.MacAddress
            row("Fabricante") = If(String.IsNullOrWhiteSpace(result.Manufacturer), "—", result.Manufacturer)
            row("Latencia") = If(result.LatencyMs.HasValue, result.LatencyMs.Value.ToString() & " ms", "—")
            row("Puertos") = FormatOpenPorts(result.OpenPorts)
            If result.IsHistorical AndAlso String.Equals(result.HistorySource, "Caché ARP", StringComparison.OrdinalIgnoreCase) Then
                row("Última evidencia") = "Caché ARP actual"
            Else
                row("Última evidencia") = If(result.LastSeen.HasValue,
                                               result.LastSeen.Value.ToString("dd/MM/yyyy HH:mm"),
                                               "—")
            End If
        End Sub

        ''' <summary>
        ''' Presenta número y servicio para que el resultado sea inmediatamente operativo.
        ''' </summary>
        Private Shared Function FormatOpenPorts(ports As IEnumerable(Of Integer)) As String
            If ports Is Nothing Then Return String.Empty
            Return String.Join(" · ", ports.Select(Function(port) port.ToString() & " " & PortProfiles.ServiceName(port)))
        End Function

        Private Sub UpdateProgress(progress As ScanProgress)
            ' BeginInvoke desde varios trabajadores puede entregar un contador antiguo
            ' después de uno nuevo; se descarta para que el progreso nunca retroceda.
            If progress.Completed < _progressBar.Value Then Return
            _progressBar.Maximum = Math.Max(1, progress.Total)
            _progressBar.Value = Math.Max(_progressBar.Minimum, Math.Min(_progressBar.Maximum, progress.Completed))
            _countersLabel.Text = progress.Online.ToString("N0") & " activos · " &
                                  progress.Offline.ToString("N0") & " sin respuesta"
            If Not _scanPaused Then
                _statusLabel.Text = "Explorando " & progress.CurrentAddress & "  ·  " &
                                    progress.Completed.ToString("N0") & "/" & progress.Total.ToString("N0")
            End If
        End Sub

        ''' <summary>
        ''' Pausa únicamente la toma de nuevas direcciones; las conexiones ya iniciadas
        ''' concluyen para no dejar sockets en un estado indeterminado.
        ''' </summary>
        Private Sub TogglePause(sender As Object, e As EventArgs)
            If Not _scanRunning Then Return
            _scanPaused = Not _scanPaused
            If _scanPaused Then
                _pauseGate.Reset()
                _pauseButton.Text = "Reanudar"
                _pauseButton.BackColor = Color.FromArgb(45, 138, 196)
                _statusLabel.Text = "Escaneo pausado"
            Else
                _pauseGate.Set()
                _pauseButton.Text = "Pausar"
                _pauseButton.BackColor = Amber
                _statusLabel.Text = "Reanudando escaneo..."
            End If
        End Sub

        ''' <summary>
        ''' Señala cancelación y libera una posible pausa para que todos los trabajadores terminen.
        ''' </summary>
        Private Sub StopScan(sender As Object, e As EventArgs)
            If Not _scanRunning OrElse _scanCancellation Is Nothing Then Return
            _statusLabel.Text = "Deteniendo..."
            _pauseGate.Set()
            _scanCancellation.Cancel()
            _stopButton.Enabled = False
        End Sub

        Private Sub UpdateCommandState(scanning As Boolean)
            _startButton.Enabled = Not scanning
            _rangeText.Enabled = Not scanning
            _localRangeButton.Enabled = Not scanning
            _portsButton.Enabled = Not scanning
            _pauseButton.Enabled = scanning
            _stopButton.Enabled = scanning
            _exportButton.Enabled = _table.Rows.Count > 0 OrElse Not scanning
            If Not scanning Then
                _pauseButton.Text = "Pausar"
                _pauseButton.BackColor = Amber
            End If
        End Sub

        ''' <summary>
        ''' Abre opciones y sólo sustituye la configuración si el usuario confirma.
        ''' </summary>
        Private Sub ShowPortOptions(sender As Object, e As EventArgs)
            If _scanRunning Then Return
            Using dialog As New PortOptionsForm(_settings)
                If dialog.ShowDialog(Me) = DialogResult.OK Then
                    _settings = dialog.Settings.CloneSettings()
                    UpdatePortSummary()
                End If
            End Using
        End Sub

        ''' <summary>
        ''' Muestra información de versión y uso responsable sin depender de un menú clásico.
        ''' </summary>
        Private Sub ShowAbout(sender As Object, e As EventArgs)
            Using dialog As New AboutForm()
                dialog.ShowDialog(Me)
            End Using
        End Sub

        ''' <summary>
        ''' Abre la consulta de IP pública en una ventana independiente. La petición no se
        ''' ejecuta durante el inicio de RedScope ni forma parte del escaneo de la red local.
        ''' </summary>
        Private Sub ShowPublicIp(sender As Object, e As EventArgs)
            Using dialog As New PublicIpForm()
                dialog.ShowDialog(Me)
            End Using
        End Sub

        Private Sub UpdatePortSummary()
            _portSummaryLabel.Text = "Puertos: " & _settings.Ports.Count.ToString() &
                                     If(_settings.Ports.Count = 0, " (sólo ping)", "  ·  " & PortProfiles.ToCsv(_settings.Ports))
            _portSummaryLabel.MaximumSize = New Size(250, 20)
            _portSummaryLabel.AutoEllipsis = True
        End Sub

        Private Sub SetLocalRange(sender As Object, e As EventArgs)
            Dim networks As List(Of LocalNetworkInfo) = NativeNetwork.GetLocalNetworks()
            If networks.Count = 0 Then
                MessageBox.Show("Windows no ha proporcionado ninguna dirección IPv4 configurada.",
                                "Redes locales", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            ' Se muestra siempre el selector, incluso si sólo existe una IP, para que el
            ' usuario pueda comprobar qué interfaz y máscara originan el rango.
            Using dialog As New LocalNetworksForm(networks)
                If dialog.ShowDialog(Me) = DialogResult.OK AndAlso dialog.SelectedNetwork IsNot Nothing Then
                    _rangeText.Text = dialog.SelectedNetwork.SuggestedRange
                    _rangeText.SelectAll()
                    _rangeText.Focus()
                End If
            End Using
        End Sub

        ''' <summary>
        ''' Reconstruye la vista al alternar equipos inactivos sin perder los resultados ya recibidos.
        ''' </summary>
        Private Sub ShowOfflineChanged(sender As Object, e As EventArgs)
            _table.BeginLoadData()
            Try
                _table.Clear()
                For Each result As ScanResult In _allResults.Values
                    If result.IsOnline OrElse result.IsHistorical OrElse _showOffline.Checked Then UpsertTableRow(result)
                Next
            Finally
                _table.EndLoadData()
            End Try
            ApplyFilter(Nothing, EventArgs.Empty)
        End Sub

        ''' <summary>
        ''' Aplica búsqueda sobre todas las columnas textuales relevantes.
        ''' </summary>
        Private Sub ApplyFilter(sender As Object, e As EventArgs)
            Dim value As String = EscapeLikeValue(_searchText.Text.Trim())
            If value.Length = 0 Then
                _view.RowFilter = String.Empty
            Else
                Dim pattern As String = "'%" & value & "%'"
                _view.RowFilter = "Estado LIKE " & pattern & " OR Nombre LIKE " & pattern &
                                  " OR IP LIKE " & pattern & " OR MAC LIKE " & pattern &
                                  " OR Fabricante LIKE " & pattern & " OR Puertos LIKE " & pattern &
                                  " OR [Última evidencia] LIKE " & pattern &
                                  " OR Comentario LIKE " & pattern
            End If
        End Sub

        Private Shared Function EscapeLikeValue(value As String) As String
            Return value.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]")
        End Function

        ''' <summary>
        ''' Colorea el estado manteniendo selección legible.
        ''' </summary>
        Private Sub FormatGridCell(sender As Object, e As DataGridViewCellFormattingEventArgs)
            If e.RowIndex < 0 Then Return
            Dim row As DataGridViewRow = _grid.Rows(e.RowIndex)
            Dim state As String = Convert.ToString(row.Cells("Estado").Value,
                                                    Globalization.CultureInfo.CurrentCulture)
            Dim isHistorical As Boolean = state.StartsWith("Histórico", StringComparison.OrdinalIgnoreCase) OrElse
                                          state.StartsWith("Rastro ARP", StringComparison.OrdinalIgnoreCase)
            If isHistorical Then
                If Not row.Selected Then
                    e.CellStyle.BackColor = Color.FromArgb(232, 235, 239)
                    e.CellStyle.ForeColor = Color.FromArgb(112, 120, 132)
                End If
                If _grid.Columns(e.ColumnIndex).Name = "Estado" Then
                    e.Value = "◌  " & state
                    e.CellStyle.Font = _boldStatusFont
                End If
                Return
            End If

            If _grid.Columns(e.ColumnIndex).Name <> "Estado" Then Return
            Dim value As String = Convert.ToString(e.Value, Globalization.CultureInfo.CurrentCulture)
            If value.StartsWith("Activo", StringComparison.OrdinalIgnoreCase) Then
                e.Value = "●  " & value
                If Not _grid.Rows(e.RowIndex).Selected Then e.CellStyle.ForeColor = Green
                e.CellStyle.Font = _boldStatusFont
            Else
                e.Value = "○  " & value
                If Not _grid.Rows(e.RowIndex).Selected Then e.CellStyle.ForeColor = Color.FromArgb(143, 153, 166)
            End If
        End Sub

        Private Sub SaveEditedComment(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 OrElse _grid.Columns(e.ColumnIndex).Name <> "Comentario" Then Return
            Dim row As DataGridViewRow = _grid.Rows(e.RowIndex)
            Dim ipAddress As String = Convert.ToString(row.Cells("IP").Value, Globalization.CultureInfo.CurrentCulture)
            Dim comment As String = Convert.ToString(row.Cells("Comentario").Value, Globalization.CultureInfo.CurrentCulture)
            If String.IsNullOrWhiteSpace(comment) Then
                _sessionComments.Remove(ipAddress)
            Else
                _sessionComments(ipAddress) = comment.Trim()
            End If
        End Sub

        ''' <summary>
        ''' Ordena IP mediante la columna numérica oculta; el resto conserva ordenación estándar.
        ''' </summary>
        Private Sub OnColumnHeaderClick(sender As Object, e As DataGridViewCellMouseEventArgs)
            If e.ColumnIndex < 0 OrElse _grid.Columns(e.ColumnIndex).Name <> "IP" Then Return
            _ipSortAscending = Not _ipSortAscending
            _view.Sort = "NumericIp " & If(_ipSortAscending, "ASC", "DESC")
        End Sub

        Private Sub OnGridMouseDown(sender As Object, e As MouseEventArgs)
            If e.Button <> MouseButtons.Right Then Return
            Dim hit As DataGridView.HitTestInfo = _grid.HitTest(e.X, e.Y)
            If hit.RowIndex >= 0 Then
                _grid.ClearSelection()
                _grid.Rows(hit.RowIndex).Selected = True
                _grid.CurrentCell = _grid.Rows(hit.RowIndex).Cells("IP")
            End If
        End Sub

        Private Sub OnGridDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex < 0 Then Return
            Dim result As ScanResult = GetSelectedResult()
            If result Is Nothing Then Return
            If result.OpenPorts.Contains(443) Then
                OpenSelectedUrl(True)
            ElseIf result.OpenPorts.Contains(80) OrElse result.OpenPorts.Contains(8080) OrElse result.OpenPorts.Contains(8000) Then
                OpenSelectedUrl(False)
            End If
        End Sub

        Private Function GetSelectedResult() As ScanResult
            If _grid.CurrentRow Is Nothing Then Return Nothing
            Dim key As String = Convert.ToString(_grid.CurrentRow.Cells("IP").Value, Globalization.CultureInfo.CurrentCulture)
            Dim result As ScanResult = Nothing
            If _allResults.TryGetValue(key, result) Then Return result
            Return Nothing
        End Function

        Private Sub CopySelectedValue(columnName As String)
            If _grid.CurrentRow Is Nothing Then Return
            Dim value As String = Convert.ToString(_grid.CurrentRow.Cells(columnName).Value, Globalization.CultureInfo.CurrentCulture)
            If value.Length > 0 Then Clipboard.SetText(value)
        End Sub

        ''' <summary>
        ''' Abre el puerto web más apropiado entre los detectados.
        ''' </summary>
        Private Sub OpenSelectedUrl(useHttps As Boolean)
            Dim result As ScanResult = GetSelectedResult()
            If result Is Nothing Then Return

            Dim port As Integer = If(useHttps, 443, 80)
            If useHttps AndAlso Not result.OpenPorts.Contains(443) AndAlso result.OpenPorts.Contains(8443) Then port = 8443
            If Not useHttps AndAlso Not result.OpenPorts.Contains(80) Then
                If result.OpenPorts.Contains(8080) Then
                    port = 8080
                ElseIf result.OpenPorts.Contains(8000) Then
                    port = 8000
                End If
            End If

            Dim defaultPort As Boolean = (useHttps AndAlso port = 443) OrElse (Not useHttps AndAlso port = 80)
            Dim url As String = If(useHttps, "https://", "http://") & result.Address.ToString() &
                                If(defaultPort, String.Empty, ":" & port.ToString())
            ShellOpen(url, String.Empty)
        End Sub

        Private Sub OpenSharedFolder()
            Dim result As ScanResult = GetSelectedResult()
            If result Is Nothing Then Return
            ShellOpen("explorer.exe", "\\" & result.Address.ToString())
        End Sub

        Private Sub OpenRdp()
            Dim result As ScanResult = GetSelectedResult()
            If result Is Nothing Then Return
            ShellOpen("mstsc.exe", "/v:" & result.Address.ToString())
        End Sub

        Private Sub OpenContinuousPing()
            Dim result As ScanResult = GetSelectedResult()
            If result Is Nothing Then Return
            ShellOpen("cmd.exe", "/k ping -t " & result.Address.ToString())
        End Sub

        ''' <summary>
        ''' Encapsula la apertura mediante ShellExecute y presenta un error comprensible.
        ''' Los argumentos proceden sólo de IP ya validadas por el analizador.
        ''' </summary>
        Private Sub ShellOpen(fileName As String, arguments As String)
            Try
                Dim info As New ProcessStartInfo With {
                    .FileName = fileName,
                    .UseShellExecute = True,
                    .Arguments = arguments
                }
                Process.Start(info)
            Catch ex As Exception
                MessageBox.Show("Windows no pudo abrir el recurso:" & Environment.NewLine & ex.Message,
                                "Abrir recurso", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End Sub

        ''' <summary>
        ''' Exporta exactamente las filas visibles, respetando búsqueda y opción de inactivos.
        ''' </summary>
        Private Sub ExportCsv(sender As Object, e As EventArgs)
            If _grid.Rows.Count = 0 Then
                MessageBox.Show("No hay resultados visibles para exportar.", "Exportar CSV",
                                MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Using dialog As New SaveFileDialog With {
                .Title = "Exportar resultados",
                .Filter = "Archivo CSV (*.csv)|*.csv",
                .FileName = "RedScope_" & DateTime.Now.ToString("yyyyMMdd_HHmmss") & ".csv",
                .AddExtension = True,
                .DefaultExt = "csv"
            }
                If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
                Try
                    Using writer As New StreamWriter(dialog.FileName, False, New UTF8Encoding(True))
                        Dim exportColumns As DataGridViewColumn() = _grid.Columns.Cast(Of DataGridViewColumn)().
                            Where(Function(column) column.Visible).OrderBy(Function(column) column.DisplayIndex).ToArray()
                        writer.WriteLine(String.Join(";", exportColumns.Select(Function(column) CsvValue(column.HeaderText))))

                        For Each row As DataGridViewRow In _grid.Rows
                            If row.IsNewRow Then Continue For
                            writer.WriteLine(String.Join(";", exportColumns.Select(
                                Function(column) CsvValue(Convert.ToString(row.Cells(column.Name).Value,
                                                                          Globalization.CultureInfo.CurrentCulture)))))
                        Next
                    End Using
                    _statusLabel.Text = "CSV exportado: " & Path.GetFileName(dialog.FileName)
                Catch ex As Exception
                    MessageBox.Show("No se pudo crear el CSV:" & Environment.NewLine & ex.Message,
                                    "Exportar CSV", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End Using
        End Sub

        ''' <summary>
        ''' CSV pensado para abrirse en hojas de cálculo. Saltos y tabulaciones se
        ''' sustituyen por espacios; si el primer carácter relevante es =, +, - o @,
        ''' se antepone una comilla simple para que Excel lo trate como texto.
        ''' Se conserva el entrecomillado CSV y las comillas interiores duplicadas.
        ''' </summary>
        Friend Shared Function CsvValue(value As String) As String
            Dim quote As String = Convert.ToChar(34).ToString()
            ' Los caracteres de control se expresan mediante Char explícitos para
            ' evitar dependencias de constantes y funciones de VB en este proyecto.
            ' El reemplazo Char/Char conserva la limpieza original de saltos y tabuladores.
            Dim safe As String = If(value, String.Empty).
                Replace(Convert.ToChar(13), " "c).
                Replace(Convert.ToChar(10), " "c).
                Replace(Convert.ToChar(9), " "c).
                Replace(Convert.ToChar(&HFEFF).ToString(), String.Empty)
            Dim visible As String = safe.TrimStart()
            If visible.Length > 0 AndAlso "=+-@".IndexOf(visible(0)) >= 0 Then
                ' La comilla simple inicial evita que se interprete como fórmula.
                safe = "'" & safe
            End If
            Return quote & safe.Replace(quote, quote & quote) & quote
        End Function

        ''' <summary>
        ''' Atajos de teclado compatibles con el trabajo repetitivo de soporte.
        ''' </summary>
        Protected Overrides Function ProcessCmdKey(ByRef message As Message, keys As Keys) As Boolean
            If keys = Keys.F5 AndAlso _startButton.Enabled Then
                _startButton.PerformClick()
                Return True
            End If
            If keys = Keys.F6 AndAlso _pauseButton.Enabled Then
                _pauseButton.PerformClick()
                Return True
            End If
            If keys = Keys.Escape AndAlso _stopButton.Enabled Then
                _stopButton.PerformClick()
                Return True
            End If
            If keys = (Keys.Control Or Keys.F) Then
                _searchText.Focus()
                _searchText.SelectAll()
                Return True
            End If
            Return MyBase.ProcessCmdKey(message, keys)
        End Function

        ''' <summary>
        ''' El primer cierre durante un escaneo se aplaza: se solicita cancelación
        ''' y se permite que StartScanAsync llegue a Finally antes de destruir controles.
        ''' </summary>
        Private Sub HandleFormClosing(sender As Object, e As FormClosingEventArgs)
            If _scanRunning Then
                e.Cancel = True
                _closeRequested = True
                _statusLabel.Text = "Finalizando sondas antes de cerrar..."
                _startButton.Enabled = False
                _pauseButton.Enabled = False
                _stopButton.Enabled = False
                _pauseGate.Set()
                If _scanCancellation IsNot Nothing Then _scanCancellation.Cancel()
                Return
            End If
            _isClosing = True
        End Sub

        ''' <summary>
        ''' Sólo libera CTS y puerta de pausa después de que los trabajadores terminen.
        ''' La fuente GDI compartida se destruye una vez, no durante cada repintado.
        ''' </summary>
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                If _scanCancellation IsNot Nothing Then _scanCancellation.Dispose()
                _pauseGate.Dispose()
                If _boldStatusFont IsNot Nothing Then _boldStatusFont.Dispose()
                _toolTip.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class

    ''' <summary>
    ''' DataGridView con doble búfer para evitar parpadeos cuando llegan muchas filas.
    ''' </summary>
    Friend NotInheritable Class BufferedDataGridView
        Inherits DataGridView

        Public Sub New()
            DoubleBuffered = True
        End Sub
    End Class
End Namespace
