Imports System.Diagnostics

Namespace DX12GameProgramming
    Public Class GameTimer
        Private ReadOnly _secondsPerCount As Double
        Private _deltaTime As Double
        Private _baseTime As Long
        Private _pausedTime As Long
        Private _stopTime As Long
        Private _prevTime As Long
        Private _currTime As Long
        Private _stopped As Boolean

        Public Sub New()
            Debug.Assert(Stopwatch.IsHighResolution, "System does not support high-resolution performance counter")
            _secondsPerCount = 0.0
            _deltaTime = -1.0
            _baseTime = 0
            _pausedTime = 0
            _prevTime = 0
            _currTime = 0
            _stopped = False
            Dim countsPerSec As Long = Stopwatch.Frequency
            _secondsPerCount = 1.0 / countsPerSec
        End Sub

        Public ReadOnly Property TotalTime As Single
            Get
                If _stopped Then Return CSng((((_stopTime - _pausedTime) - _baseTime) * _secondsPerCount))
                Return CSng((((_currTime - _pausedTime) - _baseTime) * _secondsPerCount))
            End Get
        End Property

        Public ReadOnly Property DeltaTime As Single
            Get
                Return CSng(_deltaTime)
            End Get
        End Property

        Public Sub Reset()
            Dim curTime As Long = Stopwatch.GetTimestamp()
            _baseTime = curTime
            _prevTime = curTime
            _stopTime = 0
            _stopped = False
        End Sub

        Public Sub Start()
            Dim startTime As Long = Stopwatch.GetTimestamp()

            If _stopped Then
                _pausedTime += (startTime - _stopTime)
                _prevTime = startTime
                _stopTime = 0
                _stopped = False
            End If
        End Sub

        Public Sub [Stop]()
            If Not _stopped Then
                Dim curTime As Long = Stopwatch.GetTimestamp()
                _stopTime = curTime
                _stopped = True
            End If
        End Sub

        Public Sub Tick()
            If _stopped Then
                _deltaTime = 0.0
                Return
            End If

            Dim curTime As Long = Stopwatch.GetTimestamp()
            _currTime = curTime
            _deltaTime = (_currTime - _prevTime) * _secondsPerCount
            _prevTime = _currTime
            If _deltaTime < 0.0 Then _deltaTime = 0.0
        End Sub
    End Class
End Namespace
