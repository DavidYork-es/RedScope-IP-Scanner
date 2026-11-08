Option Strict On
Option Explicit On
Option Infer On

Imports System
Imports System.Threading
Imports System.Windows.Forms

Namespace RedScope
    ''' <summary>
    ''' Punto de entrada único de la aplicación.
    ''' Se fuerza STA porque Windows Forms, el portapapeles y los diálogos del sistema
    ''' necesitan un hilo principal de apartamento único.
    ''' </summary>
    Friend NotInheritable Class Program
        Private Sub New()
            ' Clase estática: no debe instanciarse.
        End Sub

        <STAThread>
        Public Shared Sub Main()
            Dim ownsMutex As Boolean = False
            Using instanceMutex As New Mutex(True, "Local\RedScope_1_0", ownsMutex)
                If Not ownsMutex Then
                    MessageBox.Show("RedScope ya está abierto.", "RedScope",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Return
                End If

                ' Captura los errores no controlados de la interfaz para evitar cierres mudos.
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)
                AddHandler Application.ThreadException, AddressOf OnThreadException

                ' Activa los controles visuales nativos de Windows antes de crear el formulario.
                Application.EnableVisualStyles()
                Application.SetCompatibleTextRenderingDefault(False)
                Application.Run(New Forms.MainForm())
                GC.KeepAlive(instanceMutex)
            End Using
        End Sub

        ''' <summary>
        ''' Informa de un fallo inesperado sin publicar datos técnicos de la red.
        ''' </summary>
        Private Shared Sub OnThreadException(sender As Object, e As ThreadExceptionEventArgs)
            MessageBox.Show("Se ha producido un error inesperado:" & Environment.NewLine &
                            e.Exception.Message,
                            "RedScope", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Sub
    End Class
End Namespace
