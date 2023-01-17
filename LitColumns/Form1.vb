Imports LitColumns.DX12GameProgramming

Public Class Form1
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Dim app = New LitColumnsApp()
        app.Initialize()
        app.Run()
    End Sub
End Class
