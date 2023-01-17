Imports SharpDX
Imports System
Imports System.Runtime.InteropServices

Namespace DX12GameProgramming
    Public Class MathHelper

        Private Shared ReadOnly _random As Random = New Random()

        Shared Function Rand(ByVal minValue As Integer, ByVal maxValue As Integer) As Integer
            Return _random.[Next](minValue, maxValue)
        End Function

        Shared Function Randf() As Single
            Return _random.NextFloat(0.0F, 1.0F)
        End Function

        Shared Function Randf(ByVal minValue As Single, ByVal maxValue As Single) As Single
            Return _random.NextFloat(minValue, maxValue)
        End Function

        Shared Function Sinf(ByVal a As Double) As Single
            Return CSng(Math.Sin(a))
        End Function

        Shared Function Cosf(ByVal d As Double) As Single
            Return CSng(Math.Cos(d))
        End Function

        Shared Function Tanf(ByVal a As Double) As Single
            Return CSng(Math.Tan(a))
        End Function

        Shared Function Atanf(ByVal d As Double) As Single
            Return CSng(Math.Atan(d))
        End Function

        Shared Function Atan2f(ByVal y As Double, ByVal x As Double) As Single
            Return CSng(Math.Atan2(y, x))
        End Function

        Shared Function Acosf(ByVal d As Double) As Single
            Return CSng(Math.Acos(d))
        End Function

        Shared Function Expf(ByVal d As Double) As Single
            Return CSng(Math.Exp(d))
        End Function

        Shared Function Sqrtf(ByVal d As Double) As Single
            Return CSng(Math.Sqrt(d))
        End Function

        Shared Function SphericalToCartesian(ByVal radius As Single, ByVal theta As Single, ByVal phi As Single) As Vector3
            Return New Vector3(radius * Sinf(phi) * Cosf(theta), radius * Cosf(phi), radius * Sinf(phi) * Sinf(theta))
        End Function

        Shared Function InverseTranspose(ByVal m As Matrix) As Matrix
            m.Row4 = Vector4.UnitW
            Return Matrix.Transpose(Matrix.Invert(m))
        End Function

        Shared Sub Reflection(ByRef plane As Plane, <Out> ByRef result As Matrix)
            Dim num1 As Single = plane.Normal.X
            Dim num2 As Single = plane.Normal.Y
            Dim num3 As Single = plane.Normal.Z
            Dim num4 As Single = -2.0F * num1
            Dim num5 As Single = -2.0F * num2
            Dim num6 As Single = -2.0F * num3
            result.M11 = CSng((CDbl(num4) * CDbl(num1) + 1.0))
            result.M12 = num5 * num1
            result.M13 = num6 * num1
            result.M14 = 0.0F
            result.M21 = num4 * num2
            result.M22 = CSng((CDbl(num5) * CDbl(num2) + 1.0))
            result.M23 = num6 * num2
            result.M24 = 0.0F
            result.M31 = num4 * num3
            result.M32 = num5 * num3
            result.M33 = CSng((CDbl(num6) * CDbl(num3) + 1.0))
            result.M34 = 0.0F
            result.M41 = num4 * plane.D
            result.M42 = num5 * plane.D
            result.M43 = num6 * plane.D
            result.M44 = 1.0F
        End Sub

        Shared Function Reflection(ByVal plane As Plane) As Matrix
            Dim result As Matrix
            Reflection(plane, result)
            Return result
        End Function

        Shared Sub Shadow(ByRef light As Vector4, ByRef plane As Plane, <Out> ByRef result As Matrix)
            Dim num1 As Single = CSng((CDbl(plane.Normal.X) * CDbl(light.X) + CDbl(plane.Normal.Y) * CDbl(light.Y) + CDbl(plane.Normal.Z) * CDbl(light.Z) + CDbl(plane.D) * CDbl(light.W)))
            Dim num2 As Single = -plane.Normal.X
            Dim num3 As Single = -plane.Normal.Y
            Dim num4 As Single = -plane.Normal.Z
            Dim num5 As Single = -plane.D
            result.M11 = num2 * light.X + num1
            result.M21 = num3 * light.X
            result.M31 = num4 * light.X
            result.M41 = num5 * light.X
            result.M12 = num2 * light.Y
            result.M22 = num3 * light.Y + num1
            result.M32 = num4 * light.Y
            result.M42 = num5 * light.Y
            result.M13 = num2 * light.Z
            result.M23 = num3 * light.Z
            result.M33 = num4 * light.Z + num1
            result.M43 = num5 * light.Z
            result.M14 = num2 * light.W
            result.M24 = num3 * light.W
            result.M34 = num4 * light.W
            result.M44 = num5 * light.W + num1
        End Sub

        Shared Function Shadow(ByVal light As Vector4, ByVal plane As Plane) As Matrix
            Dim result As Matrix
            Shadow(light, plane, result)
            Return result
        End Function
    End Class
End Namespace
