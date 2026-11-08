Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Windows.Forms

Namespace RedScope.Forms
    ''' <summary>
    ''' Extrae el icono incrustado en el EXE y lo convierte en bitmap para reutilizarlo
    ''' dentro de los formularios sin distribuir imágenes externas.
    ''' </summary>
    Friend NotInheritable Class AppIconHelper
        Private Sub New()
            ' Clase de utilidades visuales: no debe instanciarse.
        End Sub

        Public Shared Function CreateBitmap(size As Integer) As Bitmap
            Try
                Using executableIcon As Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
                    If executableIcon Is Nothing Then Return Nothing
                    Using originalBitmap As Bitmap = executableIcon.ToBitmap()
                        Return New Bitmap(originalBitmap, New Size(size, size))
                    End Using
                End Using
            Catch ex As Exception
                ' En el diseñador o durante una compilación parcial puede no existir icono.
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Asigna el bitmap y garantiza su liberación al destruir el PictureBox.
        ''' </summary>
        Public Shared Sub AssignTo(picture As PictureBox, size As Integer)
            If picture Is Nothing Then Return
            picture.Image = CreateBitmap(size)
            AddHandler picture.Disposed,
                Sub(sender As Object, e As EventArgs)
                    If picture.Image IsNot Nothing Then
                        picture.Image.Dispose()
                        picture.Image = Nothing
                    End If
                End Sub
        End Sub
    End Class
End Namespace
