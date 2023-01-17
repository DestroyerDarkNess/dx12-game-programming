Imports SharpDX
Imports System.Diagnostics
Imports System.Threading.Tasks

Namespace DX12GameProgramming
    Friend Class Waves
        Private ReadOnly _k1 As Single
        Private ReadOnly _k2 As Single
        Private ReadOnly _k3 As Single
        Private _t As Single
        Private ReadOnly _timeStep As Single
        Private ReadOnly _spatialStep As Single
        Private _prevSolution As Vector3()
        Private _currSolution As Vector3()
        Private ReadOnly _normals As Vector3()
        Private ReadOnly _tangentX As Vector3()

        Public Sub New(ByVal m As Integer, ByVal n As Integer, ByVal dx As Single, ByVal dt As Single, ByVal speed As Single, ByVal damping As Single)
            RowCount = m
            ColumnCount = n
            VertexCount = m * n
            TriangleCount = (m - 1) * (n - 1) * 2
            _timeStep = dt
            _spatialStep = dx
            Dim d As Single = damping * dt + 2.0F
            Dim e As Single = (speed * speed) * (dt * dt) / (dx * dx)
            _k1 = (damping * dt - 2.0F) / d
            _k2 = (4.0F - 8.0F * e) / d
            _k3 = (2.0F * e) / d
            _prevSolution = New Vector3(VertexCount - 1) {}
            _currSolution = New Vector3(VertexCount - 1) {}
            _normals = New Vector3(VertexCount - 1) {}
            _tangentX = New Vector3(VertexCount - 1) {}
            Dim halfWidth As Single = (n - 1) * dx * 0.5F
            Dim halfDepth As Single = (m - 1) * dx * 0.5F

            For i As Integer = 0 To m - 1
                Dim z As Single = halfDepth - i * dx

                For j As Integer = 0 To n - 1
                    Dim x As Single = -halfWidth + j * dx
                    _prevSolution(i * n + j) = New Vector3(x, 0.0F, z)
                    _currSolution(i * n + j) = New Vector3(x, 0.0F, z)
                    _normals(i * n + j) = Vector3.UnitY
                    _tangentX(i * n + j) = Vector3.UnitX
                Next
            Next
        End Sub

        Public ReadOnly Property RowCount As Integer
        Public ReadOnly Property ColumnCount As Integer
        Public ReadOnly Property VertexCount As Integer
        Public ReadOnly Property TriangleCount As Integer

        Public ReadOnly Property Width As Single
            Get
                Return ColumnCount * _spatialStep
            End Get
        End Property

        Public ReadOnly Property Height As Single
            Get
                Return RowCount * _spatialStep
            End Get
        End Property

        Public Function Position(ByVal i As Integer) As Vector3
            Return _currSolution(i)
        End Function

        Public Function Normal(ByVal i As Integer) As Vector3
            Return _normals(i)
        End Function

        Public Function TangentX(ByVal i As Integer) As Vector3
            Return _tangentX(i)
        End Function

        Public Sub Update(ByVal dt As Single)
            _t += dt

            If _t >= _timeStep Then
                Parallel.[For](1, RowCount - 1, Sub(i)

                                                    For j As Integer = 1 To ColumnCount - 1 - 1
                                                        _prevSolution(i * ColumnCount + j).Y = _k1 * _prevSolution(i * ColumnCount + j).Y + _k2 * _currSolution(i * ColumnCount + j).Y + _k3 * (_currSolution((i + 1) * ColumnCount + j).Y + _currSolution((i - 1) * ColumnCount + j).Y + _currSolution(i * ColumnCount + j + 1).Y + _currSolution(i * ColumnCount + j - 1).Y)
                                                    Next
                                                End Sub)
                Dim temp As Vector3() = _prevSolution
                _prevSolution = _currSolution
                _currSolution = temp
                _t = 0.0F
                Parallel.[For](1, RowCount - 1, Sub(i)

                                                    For j As Integer = 1 To ColumnCount - 1 - 1
                                                        Dim l As Single = _currSolution(i * ColumnCount + j - 1).Y
                                                        Dim r As Single = _currSolution(i * ColumnCount + j + 1).Y
                                                        Dim t As Single = _currSolution((i - 1) * ColumnCount + j).Y
                                                        Dim b As Single = _currSolution((i + 1) * ColumnCount + j).Y
                                                        _normals(i * ColumnCount + j) = Vector3.Normalize(New Vector3(-r + l, 2.0F * _spatialStep, b - t))
                                                        _tangentX(i * ColumnCount + j) = Vector3.Normalize(New Vector3(2.0F * _spatialStep, r - l, 0.0F))
                                                    Next
                                                End Sub)
            End If
        End Sub

        Public Sub Disturb(ByVal i As Integer, ByVal j As Integer, ByVal magnitude As Single)
            Debug.Assert(i > 1 AndAlso i < RowCount - 2)
            Debug.Assert(j > 1 AndAlso j < ColumnCount - 2)
            Dim halfMag As Single = 0.5F * magnitude
            _currSolution(i * ColumnCount + j).Y += magnitude
            _currSolution(i * ColumnCount + j + 1).Y += halfMag
            _currSolution(i * ColumnCount + j - 1).Y += halfMag
            _currSolution((i + 1) * ColumnCount + j).Y += halfMag
            _currSolution((i - 1) * ColumnCount + j).Y += halfMag
        End Sub
    End Class
End Namespace
