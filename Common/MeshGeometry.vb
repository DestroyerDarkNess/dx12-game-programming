Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Linq
Imports SharpDX
Imports SharpDX.Direct3D12
Imports SharpDX.DXGI
Imports Device = SharpDX.Direct3D12.Device
Imports Resource = SharpDX.Direct3D12.Resource

Namespace DX12GameProgramming
    Public Class SubmeshGeometry
        Public Property IndexCount As Integer
        Public Property StartIndexLocation As Integer
        Public Property BaseVertexLocation As Integer
        Public Property Bounds As BoundingBox
    End Class

    Public Class MeshGeometry
        Implements IDisposable

        Private _toDispose As List(Of IDisposable) = New List(Of IDisposable)()

        Private Sub New()

        End Sub

        Public Property Name As String
        Public Property VertexBufferGPU As Resource
        Public Property IndexBufferGPU As Resource
        Public Property VertexBufferCPU As Object
        Public Property IndexBufferCPU As Object
        Public Property VertexByteStride As Integer
        Public Property VertexBufferByteSize As Integer
        Public Property IndexFormat As Format
        Public Property IndexBufferByteSize As Integer
        Public Property IndexCount As Integer
        Public ReadOnly Property DrawArgs As Dictionary(Of String, SubmeshGeometry) = New Dictionary(Of String, SubmeshGeometry)()

        Public ReadOnly Property VertexBufferView As VertexBufferView
            Get
                Return New VertexBufferView With {
                    .BufferLocation = VertexBufferGPU.GPUVirtualAddress,
                    .StrideInBytes = VertexByteStride,
                    .SizeInBytes = VertexBufferByteSize
                }
            End Get
        End Property

        Public ReadOnly Property IndexBufferView As IndexBufferView
            Get
                Return New IndexBufferView With {
                    .BufferLocation = IndexBufferGPU.GPUVirtualAddress,
                    .Format = IndexFormat,
                    .SizeInBytes = IndexBufferByteSize
                }
            End Get
        End Property


        Public Sub Dispose() Implements IDisposable.Dispose
            For Each disposable As IDisposable In _toDispose
                disposable.Dispose()
            Next
        End Sub

        Public Shared Function [New](Of TVertex As Structure, TIndex As Structure)(ByVal device As Device, ByVal commandList As GraphicsCommandList, ByVal vertices As IEnumerable(Of TVertex), ByVal indices As IEnumerable(Of TIndex), ByVal Optional name As String = "Default") As MeshGeometry
            Dim vertexArray As TVertex() = vertices.ToArray()
            Dim indexArray As TIndex() = indices.ToArray()
            Dim vertexBufferByteSize As Integer = Utilities.SizeOf(vertexArray)
            Dim vertexBufferUploader As Resource = Nothing
            Dim vertexBuffer As Resource = D3DUtil.CreateDefaultBuffer(device, commandList, vertexArray, vertexBufferByteSize, vertexBufferUploader)
            Dim indexBufferByteSize As Integer = Utilities.SizeOf(indexArray)
            Dim indexBufferUploader As Resource = Nothing
            Dim indexBuffer As Resource = D3DUtil.CreateDefaultBuffer(device, commandList, indexArray, indexBufferByteSize, indexBufferUploader)

            Return New MeshGeometry With
            {
                .Name = name,
                .VertexByteStride = Utilities.SizeOf(Of TVertex),
                .VertexBufferByteSize = vertexBufferByteSize,
                .VertexBufferGPU = vertexBuffer,
                .VertexBufferCPU = vertexArray,
                .IndexCount = indexArray.Length,
                .IndexFormat = GetIndexFormat(Of TIndex)(),
                .IndexBufferByteSize = indexBufferByteSize,
                .IndexBufferGPU = indexBuffer,
                .IndexBufferCPU = indexArray,
                ._toDispose = New List(Of IDisposable)({
                    vertexBuffer, vertexBufferUploader,
                    indexBuffer, indexBufferUploader
                })
            }

        End Function

        Public Shared Function [New](Of TIndex As Structure)(ByVal device As Device, ByVal commandList As GraphicsCommandList, ByVal indices As IEnumerable(Of TIndex), ByVal Optional name As String = "Default") As MeshGeometry
            Dim indexArray As TIndex() = indices.ToArray()
            Dim indexBufferByteSize As Integer = Utilities.SizeOf(indexArray)
            Dim indexBufferUploader As Resource = Nothing
            Dim indexBuffer As Resource = D3DUtil.CreateDefaultBuffer(device, commandList, indexArray, indexBufferByteSize, indexBufferUploader)

            Return New MeshGeometry With
            {
                .Name = name,
                .IndexCount = indexArray.Length,
                .IndexFormat = GetIndexFormat(Of TIndex)(),
                .IndexBufferByteSize = indexBufferByteSize,
                .IndexBufferGPU = indexBuffer,
                .IndexBufferCPU = indexArray,
                 ._toDispose = New List(Of IDisposable)({indexBuffer, indexBufferUploader})
            }
        End Function



        Private Shared Function GetIndexFormat(Of TIndex)() As Format
            Dim format As SharpDX.DXGI.Format = Format.Unknown

            If GetType(TIndex) = GetType(Integer) Then
                format = Format.R32_UInt
            ElseIf GetType(TIndex) = GetType(Short) Then
                format = Format.R16_UInt
            End If

            Debug.Assert(format <> Format.Unknown)
            Return format
        End Function

    End Class
End Namespace
