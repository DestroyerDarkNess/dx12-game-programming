Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Runtime.InteropServices
Imports SharpDX

Namespace DX12GameProgramming
    Public Class GeometryGenerator
        <StructLayout(LayoutKind.Sequential, Pack:=4)>
        Public Structure Vertex
            Public Position As Vector3
            Public Normal As Vector3
            Public TangentU As Vector3
            Public TexC As Vector2

            Public Sub New(ByVal p As Vector3, ByVal n As Vector3, ByVal t As Vector3, ByVal uv As Vector2)
                Position = p
                Normal = n
                TangentU = t
                TexC = uv
            End Sub

            Public Sub New(ByVal px As Single, ByVal py As Single, ByVal pz As Single, ByVal nx As Single, ByVal ny As Single, ByVal nz As Single, ByVal tx As Single, ByVal ty As Single, ByVal tz As Single, ByVal u As Single, ByVal v As Single)
                Me.New(New Vector3(px, py, pz), New Vector3(nx, ny, nz), New Vector3(tx, ty, tz), New Vector2(u, v))
            End Sub
        End Structure

        Public Class MeshData
            Public ReadOnly Property Vertices As List(Of Vertex) = New List(Of Vertex)()
            Public ReadOnly Property Indices32 As List(Of Integer) = New List(Of Integer)()

            Public Function GetIndices16() As List(Of Short)
                Return Indices32.[Select](Function(i) CShort(i)).ToList()
            End Function
        End Class

        Shared Function CreateBox(ByVal width As Single, ByVal height As Single, ByVal depth As Single, ByVal numSubdivisions As Integer) As MeshData
            Dim meshData = New MeshData()
            Dim w2 = 0.5F * width
            Dim h2 = 0.5F * height
            Dim d2 = 0.5F * depth
            meshData.Vertices.Add(New Vertex(-w2, -h2, -d2, 0, 0, -1, 1, 0, 0, 0, 1))
            meshData.Vertices.Add(New Vertex(-w2, +h2, -d2, 0, 0, -1, 1, 0, 0, 0, 0))
            meshData.Vertices.Add(New Vertex(+w2, +h2, -d2, 0, 0, -1, 1, 0, 0, 1, 0))
            meshData.Vertices.Add(New Vertex(+w2, -h2, -d2, 0, 0, -1, 1, 0, 0, 1, 1))
            meshData.Vertices.Add(New Vertex(-w2, -h2, +d2, 0, 0, 1, -1, 0, 0, 1, 1))
            meshData.Vertices.Add(New Vertex(+w2, -h2, +d2, 0, 0, 1, -1, 0, 0, 0, 1))
            meshData.Vertices.Add(New Vertex(+w2, +h2, +d2, 0, 0, 1, -1, 0, 0, 0, 0))
            meshData.Vertices.Add(New Vertex(-w2, +h2, +d2, 0, 0, 1, -1, 0, 0, 1, 0))
            meshData.Vertices.Add(New Vertex(-w2, +h2, -d2, 0, 1, 0, 1, 0, 0, 0, 1))
            meshData.Vertices.Add(New Vertex(-w2, +h2, +d2, 0, 1, 0, 1, 0, 0, 0, 0))
            meshData.Vertices.Add(New Vertex(+w2, +h2, +d2, 0, 1, 0, 1, 0, 0, 1, 0))
            meshData.Vertices.Add(New Vertex(+w2, +h2, -d2, 0, 1, 0, 1, 0, 0, 1, 1))
            meshData.Vertices.Add(New Vertex(-w2, -h2, -d2, 0, -1, 0, -1, 0, 0, 1, 1))
            meshData.Vertices.Add(New Vertex(+w2, -h2, -d2, 0, -1, 0, -1, 0, 0, 0, 1))
            meshData.Vertices.Add(New Vertex(+w2, -h2, +d2, 0, -1, 0, -1, 0, 0, 0, 0))
            meshData.Vertices.Add(New Vertex(-w2, -h2, +d2, 0, -1, 0, -1, 0, 0, 1, 0))
            meshData.Vertices.Add(New Vertex(-w2, -h2, +d2, -1, 0, 0, 0, 0, -1, 0, 1))
            meshData.Vertices.Add(New Vertex(-w2, +h2, +d2, -1, 0, 0, 0, 0, -1, 0, 0))
            meshData.Vertices.Add(New Vertex(-w2, +h2, -d2, -1, 0, 0, 0, 0, -1, 1, 0))
            meshData.Vertices.Add(New Vertex(-w2, -h2, -d2, -1, 0, 0, 0, 0, -1, 1, 1))
            meshData.Vertices.Add(New Vertex(+w2, -h2, -d2, 1, 0, 0, 0, 0, 1, 0, 1))
            meshData.Vertices.Add(New Vertex(+w2, +h2, -d2, 1, 0, 0, 0, 0, 1, 0, 0))
            meshData.Vertices.Add(New Vertex(+w2, +h2, +d2, 1, 0, 0, 0, 0, 1, 1, 0))
            meshData.Vertices.Add(New Vertex(+w2, -h2, +d2, 1, 0, 0, 0, 0, 1, 1, 1))
            meshData.Indices32.AddRange({0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7, 8, 9, 10, 8, 10, 11, 12, 13, 14, 12, 14, 15, 16, 17, 18, 16, 18, 19, 20, 21, 22, 20, 22, 23})
            numSubdivisions = Math.Min(numSubdivisions, 6)

            For i As Integer = 0 To numSubdivisions - 1
                Subdivide(meshData)
            Next

            Return meshData
        End Function

        Shared Function CreateSphere(ByVal radius As Single, ByVal sliceCount As Integer, ByVal stackCount As Integer) As MeshData
            Dim meshData = New MeshData()
            meshData.Vertices.Add(New Vertex(New Vector3(0, radius, 0), New Vector3(0, 1, 0), New Vector3(1, 0, 0), Vector2.Zero))
            Dim phiStep As Single = MathUtil.Pi / stackCount
            Dim thetaStep As Single = 2.0F * MathUtil.Pi / sliceCount

            For i As Integer = 1 To stackCount - 1
                Dim phi As Single = i * phiStep

                For j As Integer = 0 To sliceCount
                    Dim theta As Single = j * thetaStep
                    Dim pos = New Vector3(radius * MathHelper.Sinf(phi) * MathHelper.Cosf(theta), radius * MathHelper.Cosf(phi), radius * MathHelper.Sinf(phi) * MathHelper.Sinf(theta))
                    Dim tan = New Vector3(-radius * MathHelper.Sinf(phi) * MathHelper.Sinf(theta), 0, radius * MathHelper.Sinf(phi) * MathHelper.Cosf(theta))
                    tan.Normalize()
                    Dim norm As Vector3 = pos
                    norm.Normalize()
                    Dim texCoord = New Vector2(theta / (MathUtil.Pi * 2), phi / MathUtil.Pi)
                    meshData.Vertices.Add(New Vertex(pos, norm, tan, texCoord))
                Next
            Next

            meshData.Vertices.Add(New Vertex(0, -radius, 0, 0, -1, 0, 1, 0, 0, 0, 1))

            For i As Integer = 1 To sliceCount
                meshData.Indices32.Add(0)
                meshData.Indices32.Add(i + 1)
                meshData.Indices32.Add(i)
            Next

            Dim baseIndex As Integer = 1
            Dim ringVertexCount As Integer = sliceCount + 1

            For i As Integer = 0 To stackCount - 2 - 1

                For j As Integer = 0 To sliceCount - 1
                    meshData.Indices32.Add(baseIndex + i * ringVertexCount + j)
                    meshData.Indices32.Add(baseIndex + i * ringVertexCount + j + 1)
                    meshData.Indices32.Add(baseIndex + (i + 1) * ringVertexCount + j)
                    meshData.Indices32.Add(baseIndex + (i + 1) * ringVertexCount + j)
                    meshData.Indices32.Add(baseIndex + i * ringVertexCount + j + 1)
                    meshData.Indices32.Add(baseIndex + (i + 1) * ringVertexCount + j + 1)
                Next
            Next

            Dim southPoleIndex As Integer = meshData.Vertices.Count - 1
            baseIndex = southPoleIndex - ringVertexCount

            For i As Integer = 0 To sliceCount - 1
                meshData.Indices32.Add(southPoleIndex)
                meshData.Indices32.Add(baseIndex + i)
                meshData.Indices32.Add(baseIndex + i + 1)
            Next

            Return meshData
        End Function

        Shared Function CreateGeosphere(ByVal radius As Single, ByVal numSubdivisions As Integer) As MeshData
            Dim meshData = New MeshData()
            numSubdivisions = Math.Min(numSubdivisions, 6)
            Const x As Single = 0.525731F
            Const z As Single = 0.850651F
            Dim positions As Vector3() = {New Vector3(-x, 0, z), New Vector3(x, 0, z), New Vector3(-x, 0, -z), New Vector3(x, 0, -z), New Vector3(0, z, x), New Vector3(0, z, -x), New Vector3(0, -z, x), New Vector3(0, -z, -x), New Vector3(z, x, 0), New Vector3(-z, x, 0), New Vector3(z, -x, 0), New Vector3(-z, -x, 0)}
            Dim indices As Integer() = {1, 4, 0, 4, 9, 0, 4, 5, 9, 8, 5, 4, 1, 8, 4, 1, 10, 8, 10, 3, 8, 8, 3, 5, 3, 2, 5, 3, 7, 2, 3, 10, 7, 10, 6, 7, 6, 11, 7, 6, 0, 11, 6, 1, 0, 10, 1, 6, 11, 0, 9, 2, 11, 9, 5, 2, 9, 11, 2, 7}
            meshData.Vertices.AddRange(positions.[Select](Function(position) New Vertex With {
                .Position = position
            }))
            meshData.Indices32.AddRange(indices)

            For i As Integer = 0 To numSubdivisions - 1
                Subdivide(meshData)
            Next

            For i As Integer = 0 To positions.Length - 1
                Dim normal As Vector3 = Vector3.Normalize(positions(i))
                Dim position As Vector3 = radius * normal
                Dim theta As Single = MathHelper.Atan2f(positions(i).Z, positions(i).X) + MathUtil.Pi
                Dim phi As Single = MathHelper.Acosf(positions(i).Y / radius)
                Dim texCoord As Vector2 = New Vector2(theta / MathUtil.TwoPi, phi / MathUtil.TwoPi)
                Dim tangentU As Vector3 = New Vector3(-radius * MathHelper.Sinf(phi) * MathHelper.Sinf(theta), 0.0F, radius * MathHelper.Sinf(phi) * MathHelper.Cosf(theta))
                meshData.Vertices.Add(New Vertex(position, normal, tangentU, texCoord))
            Next

            Return meshData
        End Function

        Shared Function CreateCylinder(ByVal bottomRadius As Single, ByVal topRadius As Single, ByVal height As Single, ByVal sliceCount As Integer, ByVal stackCount As Integer) As MeshData
            Dim meshData = New MeshData()
            BuildCylinderSide(bottomRadius, topRadius, height, sliceCount, stackCount, meshData)
            BuildCylinderTopCap(topRadius, height, sliceCount, meshData)
            BuildCylinderBottomCap(bottomRadius, height, sliceCount, meshData)
            Return meshData
        End Function

        Shared Function CreateGrid(ByVal width As Single, ByVal depth As Single, ByVal m As Integer, ByVal n As Integer) As MeshData
            Dim meshData = New MeshData()
            Dim halfWidth As Single = 0.5F * width
            Dim halfDepth As Single = 0.5F * depth
            Dim dx As Single = width / (n - 1)
            Dim dz As Single = depth / (m - 1)
            Dim du As Single = 1.0F / (n - 1)
            Dim dv As Single = 1.0F / (m - 1)

            For i As Integer = 0 To m - 1
                Dim z As Single = halfDepth - i * dz

                For j As Integer = 0 To n - 1
                    Dim x As Single = -halfWidth + j * dx
                    meshData.Vertices.Add(New Vertex(New Vector3(x, 0, z), New Vector3(0, 1, 0), New Vector3(1, 0, 0), New Vector2(j * du, i * dv)))
                Next
            Next

            For i As Integer = 0 To m - 1 - 1

                For j As Integer = 0 To n - 1 - 1
                    meshData.Indices32.Add(i * n + j)
                    meshData.Indices32.Add(i * n + j + 1)
                    meshData.Indices32.Add((i + 1) * n + j)
                    meshData.Indices32.Add((i + 1) * n + j)
                    meshData.Indices32.Add(i * n + j + 1)
                    meshData.Indices32.Add((i + 1) * n + j + 1)
                Next
            Next

            Return meshData
        End Function

        Shared Function CreateQuad(ByVal x As Single, ByVal y As Single, ByVal w As Single, ByVal h As Single, ByVal depth As Single) As MeshData
            Dim meshData = New MeshData()
            meshData.Vertices.Add(New Vertex(x, y - h, depth, 0.0F, 0.0F, -1.0F, 1.0F, 0.0F, 0.0F, 0.0F, 1.0F))
            meshData.Vertices.Add(New Vertex(x, y, depth, 0.0F, 0.0F, -1.0F, 1.0F, 0.0F, 0.0F, 0.0F, 0.0F))
            meshData.Vertices.Add(New Vertex(x + w, y, depth, 0.0F, 0.0F, -1.0F, 1.0F, 0.0F, 0.0F, 1.0F, 0.0F))
            meshData.Vertices.Add(New Vertex(x + w, y - h, depth, 0.0F, 0.0F, -1.0F, 1.0F, 0.0F, 0.0F, 1.0F, 1.0F))
            meshData.Indices32.Add(0)
            meshData.Indices32.Add(1)
            meshData.Indices32.Add(2)
            meshData.Indices32.Add(0)
            meshData.Indices32.Add(2)
            meshData.Indices32.Add(3)
            Return meshData
        End Function

        Private Shared Sub Subdivide(ByVal meshData As MeshData)
            Dim verticesCopy As Vertex() = meshData.Vertices.ToArray()
            Dim indicesCopy As Integer() = meshData.Indices32.ToArray()
            meshData.Vertices.Clear()
            meshData.Indices32.Clear()
            Dim numTriangles As Integer = indicesCopy.Length / 3

            For i As Integer = 0 To numTriangles - 1
                Dim v0 As Vertex = verticesCopy(indicesCopy(i * 3 + 0))
                Dim v1 As Vertex = verticesCopy(indicesCopy(i * 3 + 1))
                Dim v2 As Vertex = verticesCopy(indicesCopy(i * 3 + 2))
                Dim m0 As Vertex = MidPoint(v0, v1)
                Dim m1 As Vertex = MidPoint(v1, v2)
                Dim m2 As Vertex = MidPoint(v0, v2)
                meshData.Vertices.Add(v0)
                meshData.Vertices.Add(v1)
                meshData.Vertices.Add(v2)
                meshData.Vertices.Add(m0)
                meshData.Vertices.Add(m1)
                meshData.Vertices.Add(m2)
                meshData.Indices32.Add(i * 6 + 0)
                meshData.Indices32.Add(i * 6 + 3)
                meshData.Indices32.Add(i * 6 + 5)
                meshData.Indices32.Add(i * 6 + 3)
                meshData.Indices32.Add(i * 6 + 4)
                meshData.Indices32.Add(i * 6 + 5)
                meshData.Indices32.Add(i * 6 + 5)
                meshData.Indices32.Add(i * 6 + 4)
                meshData.Indices32.Add(i * 6 + 2)
                meshData.Indices32.Add(i * 6 + 3)
                meshData.Indices32.Add(i * 6 + 1)
                meshData.Indices32.Add(i * 6 + 4)
            Next
        End Sub

        Private Shared Function MidPoint(ByVal v0 As Vertex, ByVal v1 As Vertex) As Vertex
            Dim pos As Vector3 = 0.5F * (v0.Position + v1.Position)
            Dim normal As Vector3 = Vector3.Normalize(0.5F * (v0.Normal + v1.Normal))
            Dim tangent As Vector3 = Vector3.Normalize(0.5F * (v0.TangentU + v1.TangentU))
            Dim tex As Vector2 = 0.5F * (v0.TexC + v1.TexC)
            Return New Vertex(pos, normal, tangent, tex)
        End Function

        Private Shared Sub BuildCylinderSide(ByVal bottomRadius As Single, ByVal topRadius As Single, ByVal height As Single, ByVal sliceCount As Integer, ByVal stackCount As Integer, ByVal meshData As MeshData)
            Dim stackHeight As Single = height / stackCount
            Dim radiusStep As Single = (topRadius - bottomRadius) / stackCount
            Dim ringCount As Integer = stackCount + 1

            For i As Integer = 0 To ringCount - 1
                Dim y As Single = -0.5F * height + i * stackHeight
                Dim r As Single = bottomRadius + i * radiusStep
                Dim dTheta As Single = 2.0F * MathUtil.Pi / sliceCount

                For j As Integer = 0 To sliceCount
                    Dim c As Single = MathHelper.Cosf(j * dTheta)
                    Dim s As Single = MathHelper.Sinf(j * dTheta)
                    Dim pos = New Vector3(r * c, y, r * s)
                    Dim uv = New Vector2(CSng(j) / sliceCount, 1.0F - CSng(i) / stackCount)
                    Dim tangent = New Vector3(-s, 0.0F, c)
                    Dim dr As Single = bottomRadius - topRadius
                    Dim bitangent = New Vector3(dr * c, -height, dr * s)
                    Dim normal = Vector3.Cross(tangent, bitangent)
                    normal.Normalize()
                    meshData.Vertices.Add(New Vertex(pos, normal, tangent, uv))
                Next
            Next

            Dim ringVertexCount As Integer = sliceCount + 1

            For i As Integer = 0 To stackCount - 1

                For j As Integer = 0 To sliceCount - 1
                    meshData.Indices32.Add(i * ringVertexCount + j)
                    meshData.Indices32.Add((i + 1) * ringVertexCount + j)
                    meshData.Indices32.Add((i + 1) * ringVertexCount + j + 1)
                    meshData.Indices32.Add(i * ringVertexCount + j)
                    meshData.Indices32.Add((i + 1) * ringVertexCount + j + 1)
                    meshData.Indices32.Add(i * ringVertexCount + j + 1)
                Next
            Next
        End Sub

        Private Shared Sub BuildCylinderTopCap(ByVal topRadius As Single, ByVal height As Single, ByVal sliceCount As Integer, ByVal meshData As MeshData)
            Dim baseIndex As Integer = meshData.Vertices.Count
            Dim y As Single = 0.5F * height
            Dim dTheta As Single = 2.0F * MathUtil.Pi / sliceCount

            For i As Integer = 0 To sliceCount
                Dim x As Single = topRadius * MathHelper.Cosf(i * dTheta)
                Dim z As Single = topRadius * MathHelper.Sinf(i * dTheta)
                Dim u As Single = x / height + 0.5F
                Dim v As Single = z / height + 0.5F
                meshData.Vertices.Add(New Vertex(New Vector3(x, y, z), New Vector3(0, 1, 0), New Vector3(1, 0, 0), New Vector2(u, v)))
            Next

            meshData.Vertices.Add(New Vertex(New Vector3(0, y, 0), New Vector3(0, 1, 0), New Vector3(1, 0, 0), New Vector2(0.5F, 0.5F)))
            Dim centerIndex As Integer = meshData.Vertices.Count - 1

            For i As Integer = 0 To sliceCount - 1
                meshData.Indices32.Add(centerIndex)
                meshData.Indices32.Add(baseIndex + i + 1)
                meshData.Indices32.Add(baseIndex + i)
            Next
        End Sub

        Private Shared Sub BuildCylinderBottomCap(ByVal bottomRadius As Single, ByVal height As Single, ByVal sliceCount As Integer, ByVal meshData As MeshData)
            Dim baseIndex As Integer = meshData.Vertices.Count
            Dim y As Single = -0.5F * height
            Dim dTheta As Single = 2.0F * MathUtil.Pi / sliceCount

            For i As Integer = 0 To sliceCount
                Dim x As Single = bottomRadius * MathHelper.Cosf(i * dTheta)
                Dim z As Single = bottomRadius * MathHelper.Sinf(i * dTheta)
                Dim u As Single = x / height + 0.5F
                Dim v As Single = z / height + 0.5F
                meshData.Vertices.Add(New Vertex(New Vector3(x, y, z), New Vector3(0, -1, 0), New Vector3(1, 0, 0), New Vector2(u, v)))
            Next

            meshData.Vertices.Add(New Vertex(New Vector3(0, y, 0), New Vector3(0, -1, 0), New Vector3(1, 0, 0), New Vector2(0.5F, 0.5F)))
            Dim centerIndex As Integer = meshData.Vertices.Count - 1

            For i As Integer = 0 To sliceCount - 1
                meshData.Indices32.Add(centerIndex)
                meshData.Indices32.Add(baseIndex + i + 1)
                meshData.Indices32.Add(baseIndex + i)
            Next
        End Sub

        Shared Function BuildFullscreenQuad() As MeshData
            Dim meshData = New MeshData()
            meshData.Vertices.Add(New Vertex(-1.0F, -1.0F, 0.0F, 0.0F, 0.0F, -1.0F, 1.0F, 0.0F, 0.0F, 0.0F, 1.0F))
            meshData.Vertices.Add(New Vertex(-1.0F, +1.0F, 0.0F, 0.0F, 0.0F, -1.0F, 1.0F, 0.0F, 0.0F, 0.0F, 0.0F))
            meshData.Vertices.Add(New Vertex(+1.0F, +1.0F, 0.0F, 0.0F, 0.0F, -1.0F, 1.0F, 0.0F, 0.0F, 1.0F, 0.0F))
            meshData.Vertices.Add(New Vertex(+1.0F, -1.0F, 0.0F, 0.0F, 0.0F, -1.0F, 1.0F, 0.0F, 0.0F, 1.0F, 1.0F))
            meshData.Indices32.Add(0)
            meshData.Indices32.Add(1)
            meshData.Indices32.Add(2)
            meshData.Indices32.Add(0)
            meshData.Indices32.Add(2)
            meshData.Indices32.Add(3)
            Return meshData
        End Function
    End Class
End Namespace
