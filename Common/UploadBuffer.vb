Imports System
Imports System.Runtime.InteropServices
Imports SharpDX.Direct3D12

Namespace DX12GameProgramming
    Public Class UploadBuffer(Of T As Structure)
        Implements IDisposable

        Private ReadOnly _elementByteSize As Integer
        Private ReadOnly _resourcePointer As IntPtr

        Public Sub New(ByVal device As Device, ByVal elementCount As Integer, ByVal isConstantBuffer As Boolean)
            _elementByteSize = If(isConstantBuffer, D3DUtil.CalcConstantBufferByteSize(Of T)(), Marshal.SizeOf(GetType(T)))
            Resource = device.CreateCommittedResource(New HeapProperties(HeapType.Upload), HeapFlags.None, ResourceDescription.Buffer(_elementByteSize * elementCount), ResourceStates.GenericRead)
            _resourcePointer = Resource.Map(0)
        End Sub

        Public ReadOnly Property Resource As Resource

        Public Sub CopyData(ByVal elementIndex As Integer, ByRef data As T)
            Marshal.StructureToPtr(data, _resourcePointer + elementIndex * _elementByteSize, True)
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Resource.Unmap(0)
            Resource.Dispose()
        End Sub
    End Class
End Namespace
