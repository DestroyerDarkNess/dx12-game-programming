﻿Imports LitWaves.DX12GameProgramming

Public Class Form1
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Dim app = New LitWavesApp()
        app.Initialize()
        app.Run()
    End Sub
End Class
