Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Reflection
Imports System.Windows.Forms

Namespace RedScope.Forms
    ''' <summary>
    ''' Ventana de información y uso responsable integrada en el propio ejecutable.
    ''' </summary>
    Public NotInheritable Class AboutForm
        Inherits Form

        Public Sub New()
            Text = "Acerca de RedScope"
            StartPosition = FormStartPosition.CenterParent
            FormBorderStyle = FormBorderStyle.FixedDialog
            MaximizeBox = False
            MinimizeBox = False
            ShowInTaskbar = False
            ClientSize = New Size(515, 401)
            BackColor = Color.White
            Font = New Font("Segoe UI", 9.0F)
            Try
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
            Catch ex As Exception
                ' El diálogo sigue siendo funcional sin icono durante depuración.
            End Try

            Dim header As New Panel With {.Dock = DockStyle.Top, .Height = 92, .BackColor = Color.FromArgb(18, 35, 58)}
            Dim iconPicture As New PictureBox With {
                .Location = New Point(22, 20),
                .Size = New Size(54, 54),
                .SizeMode = PictureBoxSizeMode.Zoom,
                .BackColor = Color.Transparent
            }
            AppIconHelper.AssignTo(iconPicture, 54)
            Dim title As New Label With {
                .Text = "RedScope",
                .Font = New Font("Segoe UI Semibold", 18.0F),
                .ForeColor = Color.White,
                .AutoSize = True,
                .Location = New Point(91, 19)
            }
            Dim version As String = Assembly.GetExecutingAssembly().GetName().Version.ToString()
            Dim versionLabel As New Label With {
                .Text = "Versión " & version & "  ·  .NET Framework 4.8",
                .ForeColor = Color.FromArgb(182, 202, 226),
                .AutoSize = True,
                .Location = New Point(94, 55)
            }
            header.Controls.Add(iconPicture)
            header.Controls.Add(title)
            header.Controls.Add(versionLabel)
            Controls.Add(header)

            Dim description As New Label With {
                .Text = "Escáner IPv4 compacto para inventario y diagnóstico." & Environment.NewLine & Environment.NewLine &
                        "• Descubrimiento ICMP y TCP incluso cuando el ping está filtrado." & Environment.NewLine &
                        "• Nombre DNS, dirección MAC y fabricante mediante tabla local." & Environment.NewLine &
                        "• Perfiles de puertos, pausa real, búsqueda y exportación CSV." & Environment.NewLine &
                        "• Rastros temporales de la caché ARP diferenciados en gris claro." & Environment.NewLine &
                        "• Portable: no usa el Registro ni conserva configuración o historial." & Environment.NewLine &
                        "• IP pública opcional: consulta HTTPS sólo al solicitarla." & Environment.NewLine &
                        "• Sin telemetría; el escaneo local no se envía a Internet.",
                .ForeColor = Color.FromArgb(45, 58, 75),
                .AutoSize = True,
                .Location = New Point(24, 116)
            }
            Controls.Add(description)

            Dim ownerLabel As New Label With {
                .Text = "Creador, diseñador y propietario: DavidYork · GitHub: @DavidYork-es",
                .ForeColor = Color.FromArgb(42, 113, 204),
                .Font = New Font("Segoe UI Semibold", 9.0F),
                .AutoSize = True,
                .Location = New Point(24, 264)
            }
            Controls.Add(ownerLabel)

            Dim warningPanel As New Panel With {
                .Location = New Point(24, 286),
                .Size = New Size(467, 61),
                .BackColor = Color.FromArgb(255, 248, 225)
            }
            Dim warning As New Label With {
                .Text = "Uso responsable: explora únicamente redes propias o para las que dispongas de autorización.",
                .ForeColor = Color.FromArgb(121, 82, 18),
                .AutoSize = False,
                .Location = New Point(13, 11),
                .Size = New Size(438, 40)
            }
            warningPanel.Controls.Add(warning)
            Controls.Add(warningPanel)

            Dim closeButton As New Button With {
                .Text = "Cerrar",
                .DialogResult = DialogResult.OK,
                .BackColor = Color.FromArgb(42, 113, 204),
                .ForeColor = Color.White,
                .FlatStyle = FlatStyle.Flat,
                .Size = New Size(105, 34),
                .Location = New Point(386, 360)
            }
            closeButton.FlatAppearance.BorderSize = 0
            Controls.Add(closeButton)
            AcceptButton = closeButton
            CancelButton = closeButton
        End Sub
    End Class
End Namespace
