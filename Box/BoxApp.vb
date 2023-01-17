Imports Common.DX12GameProgramming
Imports System.Runtime.InteropServices
Imports SharpDX
Imports SharpDX.Direct3D
Imports SharpDX.Direct3D12
Imports SharpDX.DXGI

Namespace DX12GameProgramming
    <StructLayout(LayoutKind.Sequential, Pack:=4)>
    Friend Structure Vertex
        Public Pos As Vector3
        Public Color As Vector4
    End Structure

    <StructLayout(LayoutKind.Sequential, Pack:=4)>
    Friend Structure ObjectConstants
        Public WorldViewProj As Matrix
    End Structure

    Friend Class BoxApp
        Inherits D3DApp

        Private _rootSignature As RootSignature
        Private _cbvHeap As DescriptorHeap
        Private _descriptorHeaps As DescriptorHeap()
        Private _objectCB As UploadBuffer(Of ObjectConstants)
        Private _boxGeo As MeshGeometry
        Private _mvsByteCode As ShaderBytecode
        Private _mpsByteCode As ShaderBytecode
        Private _inputLayout As InputLayoutDescription
        Private _pso As PipelineState
        Private _proj As Matrix = Matrix.Identity
        Private _view As Matrix = Matrix.Identity
        Private _theta As Single = 1.5F * MathUtil.Pi
        Private _phi As Single = MathUtil.PiOverFour
        Private _radius As Single = 5.0F
        Private _lastMousePos As Point

        Public Sub New()
            MainWindowCaption = "Box"
        End Sub

        Public Overrides Sub Initialize()
            MyBase.Initialize()
            CommandList.Reset(DirectCmdListAlloc, Nothing)
            BuildDescriptorHeaps()
            BuildConstantBuffers()
            BuildRootSignature()
            BuildShadersAndInputLayout()
            BuildBoxGeometry()
            BuildPSO()
            CommandList.Close()
            CommandQueue.ExecuteCommandList(CommandList)
            FlushCommandQueue()
        End Sub

        Protected Overrides Sub OnResize()
            MyBase.OnResize()
            _proj = Matrix.PerspectiveFovLH(MathUtil.PiOverFour, AspectRatio, 1.0F, 1000.0F)
        End Sub

        Protected Overrides Sub Update(ByVal gt As GameTimer)
            Dim x As Single = _radius * MathHelper.Sinf(_phi) * MathHelper.Cosf(_theta)
            Dim z As Single = _radius * MathHelper.Sinf(_phi) * MathHelper.Sinf(_theta)
            Dim y As Single = _radius * MathHelper.Cosf(_phi)
            _view = Matrix.LookAtLH(New Vector3(x, y, z), Vector3.Zero, Vector3.Up)
            Dim world As Matrix = Matrix.Identity
            Dim cb = New ObjectConstants With {
                .WorldViewProj = Matrix.Transpose(world * _view * _proj)
            }
            _objectCB.CopyData(0, cb)
        End Sub

        Protected Overrides Sub Draw(ByVal gt As GameTimer)
            DirectCmdListAlloc.Reset()
            CommandList.Reset(DirectCmdListAlloc, _pso)
            CommandList.SetViewport(Viewport)
            CommandList.SetScissorRectangles(ScissorRectangle)
            CommandList.ResourceBarrierTransition(CurrentBackBuffer, ResourceStates.Present, ResourceStates.RenderTarget)
            CommandList.ClearRenderTargetView(CurrentBackBufferView, Color.LightSteelBlue)
            CommandList.ClearDepthStencilView(DepthStencilView, ClearFlags.FlagsDepth Or ClearFlags.FlagsStencil, 1.0F, 0)
            CommandList.SetRenderTargets(CurrentBackBufferView, DepthStencilView)
            CommandList.SetDescriptorHeaps(_descriptorHeaps.Length, _descriptorHeaps)
            CommandList.SetGraphicsRootSignature(_rootSignature)
            CommandList.SetVertexBuffer(0, _boxGeo.VertexBufferView)
            CommandList.SetIndexBuffer(_boxGeo.IndexBufferView)
            CommandList.PrimitiveTopology = PrimitiveTopology.TriangleList
            CommandList.SetGraphicsRootDescriptorTable(0, _cbvHeap.GPUDescriptorHandleForHeapStart)
            CommandList.DrawIndexedInstanced(_boxGeo.IndexCount, 1, 0, 0, 0)
            CommandList.ResourceBarrierTransition(CurrentBackBuffer, ResourceStates.RenderTarget, ResourceStates.Present)
            CommandList.Close()
            CommandQueue.ExecuteCommandList(CommandList)
            SwapChain.Present(0, PresentFlags.None)
            FlushCommandQueue()
        End Sub

        Protected Overrides Sub OnMouseDown(ByVal button As MouseButtons, ByVal location As Point)
            MyBase.OnMouseDown(button, location)
            _lastMousePos = location
        End Sub

        Protected Overrides Sub OnMouseMove(ByVal button As MouseButtons, ByVal location As Point)
            If (button And MouseButtons.Left) <> 0 Then
                Dim dx As Single = MathUtil.DegreesToRadians(0.25F * (location.X - _lastMousePos.X))
                Dim dy As Single = MathUtil.DegreesToRadians(0.25F * (location.Y - _lastMousePos.Y))
                _theta += dx
                _phi += dy
                _phi = MathUtil.Clamp(_phi, 0.1F, MathUtil.Pi - 0.1F)
            ElseIf (button And MouseButtons.Right) <> 0 Then
                Dim dx As Single = 0.005F * (location.X - _lastMousePos.X)
                Dim dy As Single = 0.005F * (location.Y - _lastMousePos.Y)
                _radius += dx - dy
                _radius = MathUtil.Clamp(_radius, 3.0F, 15.0F)
            End If

            _lastMousePos = location
        End Sub

        Protected Overrides Sub Dispose(ByVal disposing As Boolean)
            If disposing Then
                _rootSignature?.Dispose()
                _cbvHeap?.Dispose()
                _objectCB?.Dispose()
                _boxGeo?.Dispose()
                _pso?.Dispose()
            End If

            MyBase.Dispose(disposing)
        End Sub

        Private Sub BuildDescriptorHeaps()
            Dim cbvHeapDesc = New DescriptorHeapDescription With {
                .DescriptorCount = 1,
                .Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
                .Flags = DescriptorHeapFlags.ShaderVisible,
                .NodeMask = 0
            }
            _cbvHeap = Device.CreateDescriptorHeap(cbvHeapDesc)
            _descriptorHeaps = {_cbvHeap}
        End Sub

        Private Sub BuildConstantBuffers()
            Dim sizeInBytes As Integer = D3DUtil.CalcConstantBufferByteSize(Of ObjectConstants)
            _objectCB = New UploadBuffer(Of ObjectConstants)(Device, 1, True)
            Dim cbvDesc = New ConstantBufferViewDescription With {
                .BufferLocation = _objectCB.Resource.GPUVirtualAddress,
                .sizeInBytes = sizeInBytes
            }
            Dim cbvHeapHandle As CpuDescriptorHandle = _cbvHeap.CPUDescriptorHandleForHeapStart
            Device.CreateConstantBufferView(cbvDesc, cbvHeapHandle)
        End Sub

        Private Sub BuildRootSignature()
            Dim cbvTable = New DescriptorRange(DescriptorRangeType.ConstantBufferView, 1, 0)
            Dim rootSigDesc = New RootSignatureDescription(RootSignatureFlags.AllowInputAssemblerInputLayout, {New RootParameter(ShaderVisibility.Vertex, cbvTable)})
            _rootSignature = Device.CreateRootSignature(rootSigDesc.Serialize())
        End Sub

        Private Sub BuildShadersAndInputLayout()
            _mvsByteCode = D3DUtil.CompileShader("Shaders\Color.hlsl", "VS", "vs_5_0")
            _mpsByteCode = D3DUtil.CompileShader("Shaders\Color.hlsl", "PS", "ps_5_0")
            _inputLayout = New InputLayoutDescription({New InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0), New InputElement("COLOR", 0, Format.R32G32B32A32_Float, 12, 0)})
        End Sub

        Private Sub BuildBoxGeometry()
            Dim vertices As Vertex() = {New Vertex With {
                .Pos = New Vector3(-1.0F, -1.0F, -1.0F),
                .Color = Color.White.ToVector4()
            }, New Vertex With {
                .Pos = New Vector3(-1.0F, +1.0F, -1.0F),
                .Color = Color.Black.ToVector4()
            }, New Vertex With {
                .Pos = New Vector3(+1.0F, +1.0F, -1.0F),
                .Color = Color.Red.ToVector4()
            }, New Vertex With {
                .Pos = New Vector3(+1.0F, -1.0F, -1.0F),
                .Color = Color.Green.ToVector4()
            }, New Vertex With {
                .Pos = New Vector3(-1.0F, -1.0F, +1.0F),
                .Color = Color.Blue.ToVector4()
            }, New Vertex With {
                .Pos = New Vector3(-1.0F, +1.0F, +1.0F),
                .Color = Color.Yellow.ToVector4()
            }, New Vertex With {
                .Pos = New Vector3(+1.0F, +1.0F, +1.0F),
                .Color = Color.Cyan.ToVector4()
            }, New Vertex With {
                .Pos = New Vector3(+1.0F, -1.0F, +1.0F),
                .Color = Color.Magenta.ToVector4()
            }}
            Dim indices As Short() = {0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 4, 5, 1, 4, 1, 0, 3, 2, 6, 3, 6, 7, 1, 5, 6, 1, 6, 2, 4, 0, 3, 4, 3, 7}
            _boxGeo = MeshGeometry.[New](Device, CommandList, vertices, indices)
        End Sub

        Private Sub BuildPSO()
            Dim psoDesc = New GraphicsPipelineStateDescription With {
                .InputLayout = _inputLayout,
                .RootSignature = _rootSignature,
                .VertexShader = _mvsByteCode,
                .PixelShader = _mpsByteCode,
                .RasterizerState = RasterizerStateDescription.[Default](),
                .BlendState = BlendStateDescription.[Default](),
                .DepthStencilState = DepthStencilStateDescription.[Default](),
                .SampleMask = Integer.MaxValue,
                .PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
                .RenderTargetCount = 1,
                .SampleDescription = New SampleDescription(MsaaCount, MsaaQuality),
                .DepthStencilFormat = DepthStencilFormat
            }
            psoDesc.RenderTargetFormats(0) = BackBufferFormat
            _pso = Device.CreateGraphicsPipelineState(psoDesc)
        End Sub
    End Class
End Namespace
