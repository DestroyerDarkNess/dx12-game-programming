Imports System
Imports System.Runtime.InteropServices
Imports Common.DX12GameProgramming
Imports SharpDX
Imports SharpDX.Direct3D12

Namespace DX12GameProgramming
    <StructLayout(LayoutKind.Sequential, Pack:=4)>
    Friend Structure ObjectConstants
        Public World As Matrix

        Public Shared ReadOnly Property [Default] As ObjectConstants
            Get
                Return New ObjectConstants With {
                    .World = Matrix.Identity
                }
            End Get
        End Property
    End Structure

    <StructLayout(LayoutKind.Sequential, Pack:=4)>
    Friend Structure PassConstants
        Public View As Matrix
        Public InvView As Matrix
        Public Proj As Matrix
        Public InvProj As Matrix
        Public ViewProj As Matrix
        Public InvViewProj As Matrix
        Public EyePosW As Vector3
        Public PerObjectPad1 As Single
        Public RenderTargetSize As Vector2
        Public InvRenderTargetSize As Vector2
        Public NearZ As Single
        Public FarZ As Single
        Public TotalTime As Single
        Public DeltaTime As Single

        Public Shared ReadOnly Property [Default] As PassConstants
            Get
                Return New PassConstants With {
                    .View = Matrix.Identity,
                    .InvView = Matrix.Identity,
                    .Proj = Matrix.Identity,
                    .InvProj = Matrix.Identity,
                    .ViewProj = Matrix.Identity,
                    .InvViewProj = Matrix.Identity
                }
            End Get
        End Property
    End Structure

    <StructLayout(LayoutKind.Sequential, Pack:=4)>
    Friend Structure Vertex
        Public Pos As Vector3
        Public Color As Vector4
    End Structure

    Friend Class FrameResource
        Implements IDisposable

        Public Sub New(ByVal device As Device, ByVal passCount As Integer, ByVal objectCount As Integer, ByVal waveVertCount As Integer)
            CmdListAlloc = device.CreateCommandAllocator(CommandListType.Direct)
            PassCB = New UploadBuffer(Of PassConstants)(device, passCount, True)
            ObjectCB = New UploadBuffer(Of ObjectConstants)(device, objectCount, True)
            WavesVB = New UploadBuffer(Of Vertex)(device, waveVertCount, False)
        End Sub

        Public ReadOnly Property CmdListAlloc As CommandAllocator
        Public ReadOnly Property PassCB As UploadBuffer(Of PassConstants)
        Public ReadOnly Property ObjectCB As UploadBuffer(Of ObjectConstants)
        Public ReadOnly Property WavesVB As UploadBuffer(Of Vertex)
        Public Property Fence As Long

        Public Sub Dispose() Implements IDisposable.Dispose
            WavesVB.Dispose()
            ObjectCB.Dispose()
            PassCB.Dispose()
            CmdListAlloc.Dispose()
        End Sub
    End Class
End Namespace

