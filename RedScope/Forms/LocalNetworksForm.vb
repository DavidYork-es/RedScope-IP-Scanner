Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Windows.Forms
Imports RedScope.Models

Namespace RedScope.Forms
    ''' <summary>
    ''' Presenta todas las IPv4 locales detectadas y permite escoger exactamente un
    ''' rango. La separación evita sobrecargar la barra principal cuando hay VPN,
    ''' Ethernet, Wi-Fi o varias IP en un mismo adaptador.
    ''' </summary>
    Public NotInheritable Class LocalNetworksForm
        Inherits Form

        Private ReadOnly _grid As New DataGridView()
        Private ReadOnly _useButton As New Button()
        Private ReadOnly _detailsLabel As New Label()

        Public Sub New(networks As IList(Of LocalNetworkInfo))
            If networks Is Nothing Then Throw New ArgumentNullException(NameOf(networks))
            InitializeInterface()
            LoadNetworks(networks)
        End Sub

        Public Property SelectedNetwork As LocalNetworkInfo

        Private Sub InitializeInterface()
            Text = "Seleccionar red local"
            StartPosition = FormStartPosition.CenterParent
            FormBorderStyle = FormBorderStyle.Sizable
            MinimizeBox = False
            ShowInTaskbar = False
            ClientSize = New Size(860, 455)
            MinimumSize = New Size(720, 400)
            BackColor = Color.FromArgb(244, 247, 251)
            Font = New Font("Segoe UI", 9.0F)
            Try
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
            Catch ex As Exception
                ' El formulario continúa operativo aunque no haya icono en depuración.
            End Try

            ' Tres filas explícitas impiden que la rejilla se extienda por debajo de la
            ' cabecera o del pie, independientemente del orden Z, DPI o redimensionado.
            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 3,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty,
                .BackColor = BackColor
            }
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 76.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 76.0F))
            Controls.Add(root)

            Dim header As New Panel With {
                .Dock = DockStyle.Fill,
                .Margin = Padding.Empty,
                .BackColor = Color.FromArgb(18, 35, 58)
            }
            Dim iconPicture As New PictureBox With {
                .Location = New Point(18, 14),
                .Size = New Size(48, 48),
                .SizeMode = PictureBoxSizeMode.Zoom,
                .BackColor = Color.Transparent
            }
            AppIconHelper.AssignTo(iconPicture, 48)
            Dim title As New Label With {
                .Text = "REDES IPv4 DETECTADAS",
                .Font = New Font("Segoe UI Semibold", 15.0F),
                .ForeColor = Color.White,
                .AutoSize = True,
                .Location = New Point(80, 13)
            }
            Dim subtitle As New Label With {
                .Text = "Se muestran todas las IPv4 configuradas. Selecciona un único rango para explorar.",
                .ForeColor = Color.FromArgb(180, 201, 226),
                .AutoSize = True,
                .Location = New Point(82, 43)
            }
            header.Controls.Add(iconPicture)
            header.Controls.Add(title)
            header.Controls.Add(subtitle)
            root.Controls.Add(header, 0, 0)

            _grid.Dock = DockStyle.Fill
            _grid.Margin = Padding.Empty
            _grid.BackgroundColor = Color.White
            _grid.BorderStyle = BorderStyle.None
            _grid.ReadOnly = True
            _grid.AllowUserToAddRows = False
            _grid.AllowUserToDeleteRows = False
            _grid.AllowUserToResizeRows = False
            _grid.AutoGenerateColumns = False
            _grid.RowHeadersVisible = False
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            _grid.MultiSelect = False
            _grid.RowTemplate.Height = 30
            _grid.EnableHeadersVisualStyles = False
            _grid.ColumnHeadersHeight = 36
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(42, 57, 78)
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White
            _grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI Semibold", 9.0F)
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(211, 229, 248)
            _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(21, 40, 62)
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253)
            _grid.Columns.Add(CreateColumn("Estado", 85))
            _grid.Columns.Add(CreateColumn("Adaptador", 130))
            _grid.Columns.Add(CreateColumn("Dirección IPv4", 115))
            _grid.Columns.Add(CreateColumn("Máscara", 115))
            _grid.Columns.Add(CreateColumn("Puerta de enlace", 115))
            Dim rangeColumn As DataGridViewTextBoxColumn = CreateColumn("Rango propuesto", 220)
            rangeColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            _grid.Columns.Add(rangeColumn)

            Dim gridPanel As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(14, 12, 14, 8), .BackColor = BackColor}
            gridPanel.Controls.Add(_grid)
            root.Controls.Add(gridPanel, 0, 1)

            Dim footer As New Panel With {
                .Dock = DockStyle.Fill,
                .Margin = Padding.Empty,
                .BackColor = Color.White,
                .Padding = New Padding(14)
            }
            _detailsLabel.Text = "Selecciona una dirección IPv4."
            _detailsLabel.AutoEllipsis = True
            _detailsLabel.ForeColor = Color.FromArgb(66, 81, 101)
            _detailsLabel.Location = New Point(16, 12)
            _detailsLabel.Size = New Size(570, 46)
            _detailsLabel.Anchor = AnchorStyles.Left Or AnchorStyles.Top Or AnchorStyles.Right
            footer.Controls.Add(_detailsLabel)

            Dim cancelButton As New Button With {
                .Text = "Cancelar",
                .DialogResult = DialogResult.Cancel,
                .FlatStyle = FlatStyle.Flat,
                .BackColor = Color.FromArgb(226, 232, 240),
                .ForeColor = Color.FromArgb(45, 55, 72),
                .Size = New Size(104, 35),
                .Anchor = AnchorStyles.Top Or AnchorStyles.Right,
                .Location = New Point(628, 20)
            }
            cancelButton.FlatAppearance.BorderSize = 0
            _useButton.Text = "Usar este rango"
            _useButton.FlatStyle = FlatStyle.Flat
            _useButton.BackColor = Color.FromArgb(28, 151, 103)
            _useButton.ForeColor = Color.White
            _useButton.Size = New Size(130, 35)
            _useButton.Anchor = AnchorStyles.Top Or AnchorStyles.Right
            _useButton.Location = New Point(742, 20)
            _useButton.Enabled = False
            _useButton.FlatAppearance.BorderSize = 0
            footer.Controls.Add(cancelButton)
            footer.Controls.Add(_useButton)
            root.Controls.Add(footer, 0, 2)

            AddHandler footer.Resize,
                Sub(sender As Object, e As EventArgs)
                    _useButton.Left = footer.ClientSize.Width - _useButton.Width - 14
                    cancelButton.Left = _useButton.Left - cancelButton.Width - 10
                    _detailsLabel.Width = Math.Max(250, cancelButton.Left - 28)
                End Sub
            AddHandler _grid.SelectionChanged, AddressOf UpdateSelection
            AddHandler _grid.CellDoubleClick, AddressOf AcceptDoubleClick
            AddHandler _useButton.Click, AddressOf AcceptSelection
            AcceptButton = _useButton
            CancelButton = cancelButton
        End Sub

        Private Shared Function CreateColumn(header As String, width As Integer) As DataGridViewTextBoxColumn
            Return New DataGridViewTextBoxColumn With {
                .HeaderText = header,
                .Name = header,
                .Width = width,
                .SortMode = DataGridViewColumnSortMode.Automatic
            }
        End Function

        ''' <summary>
        ''' Cada fila conserva su objeto LocalNetworkInfo en Tag para no reconstruir
        ''' datos desde textos formateados.
        ''' </summary>
        Private Sub LoadNetworks(networks As IList(Of LocalNetworkInfo))
            For Each network As LocalNetworkInfo In networks
                Dim index As Integer = _grid.Rows.Add(network.AdapterStatus,
                                                      network.AdapterName,
                                                      network.Address.ToString(),
                                                      network.Mask.ToString(),
                                                      If(String.IsNullOrWhiteSpace(network.Gateway), "—", network.Gateway),
                                                      network.SuggestedRange)
                _grid.Rows(index).Tag = network
            Next
            If _grid.Rows.Count > 0 Then
                _grid.Rows(0).Selected = True
                _grid.CurrentCell = _grid.Rows(0).Cells(0)
            End If
        End Sub

        Private Sub UpdateSelection(sender As Object, e As EventArgs)
            SelectedNetwork = GetCurrentNetwork()
            _useButton.Enabled = SelectedNetwork IsNot Nothing
            If SelectedNetwork Is Nothing Then
                _detailsLabel.Text = "Selecciona una dirección IPv4."
            Else
                _detailsLabel.Text = SelectedNetwork.AdapterDescription & Environment.NewLine &
                                     "Tipo: " & SelectedNetwork.AdapterType & "  ·  Estado: " & SelectedNetwork.AdapterStatus &
                                     "  ·  Origen: " & SelectedNetwork.DetectionSource & "  ·  Rango: " & SelectedNetwork.SuggestedRange
            End If
        End Sub

        Private Function GetCurrentNetwork() As LocalNetworkInfo
            If _grid.CurrentRow Is Nothing Then Return Nothing
            Return TryCast(_grid.CurrentRow.Tag, LocalNetworkInfo)
        End Function

        Private Sub AcceptDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex >= 0 Then AcceptSelection(sender, EventArgs.Empty)
        End Sub

        Private Sub AcceptSelection(sender As Object, e As EventArgs)
            SelectedNetwork = GetCurrentNetwork()
            If SelectedNetwork Is Nothing Then Return
            DialogResult = DialogResult.OK
            Close()
        End Sub
    End Class
End Namespace
