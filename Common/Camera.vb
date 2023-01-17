Imports SharpDX

Namespace DX12GameProgramming
    Public Class Camera
        Private _viewDirty As Boolean = True

        Public Sub New()
            SetLens(MathUtil.PiOverFour, 1.0F, 1.0F, 1000.0F)
        End Sub

        Public Property Position As Vector3
        Public Property Right As Vector3 = Vector3.UnitX
        Public Property Up As Vector3 = Vector3.UnitY
        Public Property Look As Vector3 = Vector3.UnitZ
        Public Property NearZ As Single
        Public Property FarZ As Single
        Public Property Aspect As Single
        Public Property FovY As Single

        Public ReadOnly Property FovX As Single
            Get
                Dim halfWidth As Single = 0.5F * NearWindowWidth
                Return 2.0F * MathHelper.Atanf(halfWidth / NearZ)
            End Get
        End Property

        Public Property NearWindowHeight As Single

        Public ReadOnly Property NearWindowWidth As Single
            Get
                Return Aspect * NearWindowHeight
            End Get
        End Property

        Public Property FarWindowHeight As Single

        Public ReadOnly Property FarWindowWidth As Single
            Get
                Return Aspect * FarWindowHeight
            End Get
        End Property

        Public Property View As Matrix = Matrix.Identity
        Public Property Proj As Matrix = Matrix.Identity

        Public ReadOnly Property ViewProj As Matrix
            Get
                Return View * Proj
            End Get
        End Property

        Public ReadOnly Property Frustum As BoundingFrustum
            Get
                Return New BoundingFrustum(ViewProj)
            End Get
        End Property

        Public Sub SetLens(ByVal fovY As Single, ByVal aspect As Single, ByVal zn As Single, ByVal zf As Single)
            fovY = fovY
            aspect = aspect
            NearZ = zn
            FarZ = zf
            NearWindowHeight = 2.0F * zn * MathHelper.Tanf(0.5F * fovY)
            FarWindowHeight = 2.0F * zf * MathHelper.Tanf(0.5F * fovY)
            Proj = Matrix.PerspectiveFovLH(fovY, aspect, zn, zf)
        End Sub

        Public Sub LookAt(ByVal pos As Vector3, ByVal target As Vector3, ByVal up As Vector3)
            Position = pos
            Look = Vector3.Normalize(target - pos)
            Right = Vector3.Normalize(Vector3.Cross(up, Look))
            up = Vector3.Cross(Look, Right)
            _viewDirty = True
        End Sub

        Public Sub Strafe(ByVal d As Single)
            Position += Right * d
            _viewDirty = True
        End Sub

        Public Sub Walk(ByVal d As Single)
            Position += Look * d
            _viewDirty = True
        End Sub

        Public Sub Pitch(ByVal angle As Single)
            Dim r As Matrix = Matrix.RotationAxis(Right, angle)
            Up = Vector3.TransformNormal(Up, r)
            Look = Vector3.TransformNormal(Look, r)
            _viewDirty = True
        End Sub

        Public Sub RotateY(ByVal angle As Single)
            Dim r As Matrix = Matrix.RotationY(angle)
            Right = Vector3.TransformNormal(Right, r)
            Up = Vector3.TransformNormal(Up, r)
            Look = Vector3.TransformNormal(Look, r)
            _viewDirty = True
        End Sub

        Public Sub UpdateViewMatrix()
            If Not _viewDirty Then Return
            Look = Vector3.Normalize(Look)
            Up = Vector3.Normalize(Vector3.Cross(Look, Right))
            Right = Vector3.Cross(Up, Look)
            Dim x As Single = -Vector3.Dot(Position, Right)
            Dim y As Single = -Vector3.Dot(Position, Up)
            Dim z As Single = -Vector3.Dot(Position, Look)
            View = New Matrix(Right.X, Up.X, Look.X, 0.0F, Right.Y, Up.Y, Look.Y, 0.0F, Right.Z, Up.Z, Look.Z, 0.0F, x, y, z, 1.0F)
            _viewDirty = False
        End Sub

        Public Function GetPickingRay(ByVal sp As Point, ByVal clientWidth As Integer, ByVal clientHeight As Integer) As Ray
            Dim p As Matrix = Proj
            Dim vx As Single = (2.0F * sp.X / clientWidth - 1.0F) / p.M11
            Dim vy As Single = (-2.0F * sp.Y / clientHeight + 1.0F) / p.M22
            Dim ray = New Ray(Vector3.Zero, New Vector3(vx, vy, 1))
            Dim v As Matrix = View
            Dim invView As Matrix = Matrix.Invert(v)
            Dim toWorld As Matrix = invView
            ray = New Ray(Vector3.TransformCoordinate(ray.Position, toWorld), Vector3.TransformNormal(ray.Direction, toWorld))
            Return ray
        End Function
    End Class
End Namespace
