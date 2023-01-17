Imports Common.DX12GameProgramming
Imports SharpDX
Imports SharpDX.Direct3D

Namespace DX12GameProgramming
    Friend Class RenderItem
        Public Property World As Matrix = Matrix.Identity
        Public Property NumFramesDirty As Integer = D3DApp.NumFrameResources
        Public Property ObjCBIndex As Integer = -1
        Public Property Mat As Material
        Public Property Geo As MeshGeometry
        Public Property PrimitiveType As PrimitiveTopology = PrimitiveTopology.TriangleList
        Public Property IndexCount As Integer
        Public Property StartIndexLocation As Integer
        Public Property BaseVertexLocation As Integer
    End Class

    Friend Enum RenderLayer
        Opaque
    End Enum
End Namespace
