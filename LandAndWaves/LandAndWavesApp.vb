Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Threading
Imports Common.DX12GameProgramming
Imports SharpDX
Imports SharpDX.Direct3D12
Imports SharpDX.DXGI
Imports Resource = SharpDX.Direct3D12.Resource


Namespace DX12GameProgramming
    Public Class LandAndWavesApp
        Inherits D3DApp

        Private ReadOnly _frameResources As List(Of FrameResource) = New List(Of FrameResource)(NumFrameResources)
        Private ReadOnly _fenceEvents As List(Of AutoResetEvent) = New List(Of AutoResetEvent)(NumFrameResources)
        Private _currFrameResourceIndex As Integer
        Private _rootSignature As RootSignature
        Private ReadOnly _geometries As Dictionary(Of String, MeshGeometry) = New Dictionary(Of String, MeshGeometry)()
        Private ReadOnly _shaders As Dictionary(Of String, ShaderBytecode) = New Dictionary(Of String, ShaderBytecode)()
        Private ReadOnly _psos As Dictionary(Of String, PipelineState) = New Dictionary(Of String, PipelineState)()
        Private _inputLayout As InputLayoutDescription
        Private _wavesRitem As RenderItem
        Private ReadOnly _allRitems As List(Of RenderItem) = New List(Of RenderItem)()



        ' Render items divided by PSO.
        Private ReadOnly _ritemLayers As Dictionary(Of RenderLayer, List(Of RenderItem)) = New Dictionary(Of RenderLayer, List(Of RenderItem))(1) From {
{RenderLayer.Opaque, New List(Of RenderItem)()}
}


        Private _waves As Waves
        Private _mainPassCB As PassConstants
        Private _isWireframe As Boolean
        Private _eyePos As Vector3
        Private _proj As Matrix = Matrix.Identity
        Private _view As Matrix = Matrix.Identity
        Private _theta As Single = 1.5F * MathUtil.Pi
        Private _phi As Single = MathUtil.PiOverTwo - 0.1F
        Private _radius As Single = 50.0F
        Private _tBase As Single
        Private _lastMousePos As Point

        Public Sub New()
            MainWindowCaption = "Land and Waves"
        End Sub

        Private ReadOnly Property CurrFrameResource As FrameResource
            Get
                Return _frameResources(_currFrameResourceIndex)
            End Get
        End Property

        Private ReadOnly Property CurrentFenceEvent As AutoResetEvent
            Get
                Return _fenceEvents(_currFrameResourceIndex)
            End Get
        End Property

        Public Overrides Sub Initialize()
            MyBase.Initialize()
            CommandList.Reset(DirectCmdListAlloc, Nothing)
            _waves = New Waves(128, 128, 1.0F, 0.03F, 4.0F, 0.2F)
            BuildRootSignature()
            BuildShadersAndInputLayout()
            BuildLandGeometry()
            BuildWavesGeometry()
            BuildRenderItems()
            BuildFrameResources()
            BuildPSOs()
            CommandList.Close()
            CommandQueue.ExecuteCommandList(CommandList)
            FlushCommandQueue()
        End Sub

        Protected Overrides Sub OnResize()
            MyBase.OnResize()
            _proj = Matrix.PerspectiveFovLH(MathUtil.PiOverFour, AspectRatio, 1.0F, 1000.0F)
        End Sub

        Protected Overrides Sub Update(ByVal gt As GameTimer)
            UpdateCamera()
            _currFrameResourceIndex = (_currFrameResourceIndex + 1) Mod NumFrameResources

            If CurrFrameResource.Fence <> 0 AndAlso Fence.CompletedValue < CurrFrameResource.Fence Then
                Fence.SetEventOnCompletion(CurrFrameResource.Fence, CurrentFenceEvent.SafeWaitHandle.DangerousGetHandle())
                CurrentFenceEvent.WaitOne()
            End If

            UpdateObjectCBs()
            UpdateMainPassCB(gt)
            UpdateWaves(gt)
        End Sub

        Protected Overrides Sub Draw(ByVal gt As GameTimer)
            Dim cmdListAlloc As CommandAllocator = CurrFrameResource.CmdListAlloc
            cmdListAlloc.Reset()
            CommandList.Reset(cmdListAlloc, If(_isWireframe, _psos("opaque_wireframe"), _psos("opaque")))
            CommandList.SetViewport(Viewport)
            CommandList.SetScissorRectangles(ScissorRectangle)
            CommandList.ResourceBarrierTransition(CurrentBackBuffer, ResourceStates.Present, ResourceStates.RenderTarget)
            CommandList.ClearRenderTargetView(CurrentBackBufferView, Color.LightSteelBlue)
            CommandList.ClearDepthStencilView(DepthStencilView, ClearFlags.FlagsDepth Or ClearFlags.FlagsStencil, 1.0F, 0)
            CommandList.SetRenderTargets(CurrentBackBufferView, DepthStencilView)
            CommandList.SetGraphicsRootSignature(_rootSignature)
            Dim passCB As Resource = CurrFrameResource.PassCB.Resource
            CommandList.SetGraphicsRootConstantBufferView(1, passCB.GPUVirtualAddress)
            DrawRenderItems(CommandList, _ritemLayers(RenderLayer.Opaque))
            CommandList.ResourceBarrierTransition(CurrentBackBuffer, ResourceStates.RenderTarget, ResourceStates.Present)
            CommandList.Close()
            CommandQueue.ExecuteCommandList(CommandList)
            SwapChain.Present(0, PresentFlags.None)
            CurrFrameResource.Fence = System.Threading.Interlocked.Increment(CurrentFence)
            CommandQueue.Signal(Fence, CurrentFence)
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
                Dim dx As Single = 0.2F * (location.X - _lastMousePos.X)
                Dim dy As Single = 0.2F * (location.Y - _lastMousePos.Y)
                _radius += dx - dy
                _radius = MathUtil.Clamp(_radius, 5.0F, 150.0F)
            End If

            _lastMousePos = location
        End Sub

        Protected Overrides Sub OnKeyDown(ByVal keyCode As Keys)
            If keyCode = Keys.D1 Then _isWireframe = True
        End Sub

        Protected Overrides Sub OnKeyUp(ByVal keyCode As Keys)
            MyBase.OnKeyUp(keyCode)
            If keyCode = Keys.D1 Then _isWireframe = False
        End Sub

        Protected Overrides Sub Dispose(ByVal disposing As Boolean)
            If disposing Then
                _rootSignature?.Dispose()

                For Each frameResource As FrameResource In _frameResources
                    frameResource.Dispose()
                Next

                For Each geometry As MeshGeometry In _geometries.Values
                    geometry.Dispose()
                Next

                For Each pso As PipelineState In _psos.Values
                    pso.Dispose()
                Next
            End If

            MyBase.Dispose(disposing)
        End Sub

        Private Sub UpdateCamera()
            _eyePos.X = _radius * MathHelper.Sinf(_phi) * MathHelper.Cosf(_theta)
            _eyePos.Z = _radius * MathHelper.Sinf(_phi) * MathHelper.Sinf(_theta)
            _eyePos.Y = _radius * MathHelper.Cosf(_phi)
            _view = Matrix.LookAtLH(_eyePos, Vector3.Zero, Vector3.Up)
        End Sub

        Private Sub UpdateObjectCBs()
            For Each e As RenderItem In _allRitems

                If e.NumFramesDirty > 0 Then
                    Dim objConstants = New ObjectConstants With {
                        .World = Matrix.Transpose(e.World)
                    }
                    CurrFrameResource.ObjectCB.CopyData(e.ObjCBIndex, objConstants)
                    e.NumFramesDirty -= 1
                End If
            Next
        End Sub

        Private Sub UpdateMainPassCB(ByVal gt As GameTimer)
            Dim viewProj As Matrix = _view * _proj
            Dim invView As Matrix = Matrix.Invert(_view)
            Dim invProj As Matrix = Matrix.Invert(_proj)
            Dim invViewProj As Matrix = Matrix.Invert(viewProj)
            _mainPassCB.View = Matrix.Transpose(_view)
            _mainPassCB.InvView = Matrix.Transpose(invView)
            _mainPassCB.Proj = Matrix.Transpose(_proj)
            _mainPassCB.InvProj = Matrix.Transpose(invProj)
            _mainPassCB.ViewProj = Matrix.Transpose(viewProj)
            _mainPassCB.InvViewProj = Matrix.Transpose(invViewProj)
            _mainPassCB.EyePosW = _eyePos
            _mainPassCB.RenderTargetSize = New Vector2(ClientWidth, ClientHeight)
            _mainPassCB.InvRenderTargetSize = 1.0F / _mainPassCB.RenderTargetSize
            _mainPassCB.NearZ = 1.0F
            _mainPassCB.FarZ = 1000.0F
            _mainPassCB.TotalTime = gt.TotalTime
            _mainPassCB.DeltaTime = gt.DeltaTime
            CurrFrameResource.PassCB.CopyData(0, _mainPassCB)
        End Sub

        Private Sub UpdateWaves(ByVal gt As GameTimer)
            If (Timer.TotalTime - _tBase) >= 0.25F Then
                _tBase += 0.25F
                Dim i As Integer = MathHelper.Rand(4, _waves.RowCount - 5)
                Dim j As Integer = MathHelper.Rand(4, _waves.ColumnCount - 5)
                Dim r As Single = MathHelper.Randf(0.2F, 0.5F)
                _waves.Disturb(i, j, r)
            End If

            _waves.Update(gt.DeltaTime)
            Dim currWavesVB As UploadBuffer(Of Vertex) = CurrFrameResource.WavesVB

            For i As Integer = 0 To _waves.VertexCount - 1
                Dim v = New Vertex With {
                    .Pos = _waves.Position(i),
                    .Color = Color.Blue.ToVector4()
                }
                currWavesVB.CopyData(i, v)
            Next

            _wavesRitem.Geo.VertexBufferGPU = currWavesVB.Resource
        End Sub

        Private Sub BuildRootSignature()
            Dim slotRootParameters = {New RootParameter(ShaderVisibility.Vertex, New RootDescriptor(0, 0), RootParameterType.ConstantBufferView), New RootParameter(ShaderVisibility.Vertex, New RootDescriptor(1, 0), RootParameterType.ConstantBufferView)}
            Dim rootSigDesc = New RootSignatureDescription(RootSignatureFlags.AllowInputAssemblerInputLayout, slotRootParameters)
            _rootSignature = Device.CreateRootSignature(rootSigDesc.Serialize())
        End Sub

        Private Sub BuildShadersAndInputLayout()
            _shaders("standardVS") = D3DUtil.CompileShader("Shaders\Color.hlsl", "VS", "vs_5_0")
            _shaders("opaquePS") = D3DUtil.CompileShader("Shaders\Color.hlsl", "PS", "ps_5_0")
            _inputLayout = New InputLayoutDescription({New InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0), New InputElement("COLOR", 0, Format.R32G32B32A32_Float, 12, 0)})
        End Sub

        Private Sub BuildLandGeometry()
            Dim grid As GeometryGenerator.MeshData = GeometryGenerator.CreateGrid(160.0F, 160.0F, 50, 50)
            Dim vertices = New Vertex(grid.Vertices.Count - 1) {}

            For i As Integer = 0 To grid.Vertices.Count - 1
                Dim p As Vector3 = grid.Vertices(i).Position
                vertices(i).Pos = p
                vertices(i).Pos.Y = GetHillsHeight(p.X, p.Z)

                If vertices(i).Pos.Y < -10.0F Then
                    vertices(i).Color = New Vector4(1.0F, 0.96F, 0.62F, 1.0F)
                ElseIf vertices(i).Pos.Y < 5.0F Then
                    vertices(i).Color = New Vector4(0.48F, 0.77F, 0.46F, 1.0F)
                ElseIf vertices(i).Pos.Y < 12.0F Then
                    vertices(i).Color = New Vector4(0.1F, 0.48F, 0.19F, 1.0F)
                ElseIf vertices(i).Pos.Y < 20.0F Then
                    vertices(i).Color = New Vector4(0.45F, 0.39F, 0.34F, 1.0F)
                Else
                    vertices(i).Color = New Vector4(1.0F, 1.0F, 1.0F, 1.0F)
                End If
            Next

            Dim indices As List(Of Short) = grid.GetIndices16()
            Dim geo = MeshGeometry.[New](Device, CommandList, vertices, indices.ToArray(), "landGeo")
            Dim submesh = New SubmeshGeometry With {
                .IndexCount = indices.Count,
                .StartIndexLocation = 0,
                .BaseVertexLocation = 0
            }
            geo.DrawArgs("grid") = submesh
            _geometries("landGeo") = geo
        End Sub

        Private Sub BuildWavesGeometry()
            Dim indices = New Short(3 * _waves.TriangleCount - 1) {}
            Debug.Assert(_waves.VertexCount < &HFFFF)
            Dim m As Integer = _waves.RowCount
            Dim n As Integer = _waves.ColumnCount
            Dim k As Integer = 0

            For i As Integer = 0 To m - 1 - 1

                For j As Integer = 0 To n - 1 - 1
                    indices(k + 0) = CShort((i * n + j))
                    indices(k + 1) = CShort((i * n + j + 1))
                    indices(k + 2) = CShort(((i + 1) * n + j))
                    indices(k + 3) = CShort(((i + 1) * n + j))
                    indices(k + 4) = CShort((i * n + j + 1))
                    indices(k + 5) = CShort(((i + 1) * n + j + 1))
                    k += 6
                Next
            Next

            Dim geo = MeshGeometry.[New](Device, CommandList, indices, "waterGeo")
            geo.VertexByteStride = Utilities.SizeOf(Of Vertex)()
            geo.VertexBufferByteSize = geo.VertexByteStride * _waves.VertexCount
            Dim submesh = New SubmeshGeometry With {
                .IndexCount = indices.Length,
                .StartIndexLocation = 0,
                .BaseVertexLocation = 0
            }
            geo.DrawArgs("grid") = submesh
            _geometries("waterGeo") = geo
        End Sub

        Private Sub BuildPSOs()
            Dim opaquePsoDesc = New GraphicsPipelineStateDescription With {
                .InputLayout = _inputLayout,
                .RootSignature = _rootSignature,
                .VertexShader = _shaders("standardVS"),
                .PixelShader = _shaders("opaquePS"),
                .RasterizerState = RasterizerStateDescription.[Default](),
                .BlendState = BlendStateDescription.[Default](),
                .DepthStencilState = DepthStencilStateDescription.[Default](),
                .SampleMask = Integer.MaxValue,
                .PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
                .RenderTargetCount = 1,
                .SampleDescription = New SampleDescription(MsaaCount, MsaaQuality),
                .DepthStencilFormat = DepthStencilFormat
            }
            opaquePsoDesc.RenderTargetFormats(0) = BackBufferFormat
            _psos("opaque") = Device.CreateGraphicsPipelineState(opaquePsoDesc)
            Dim opaqueWireframePsoDesc = opaquePsoDesc
            opaqueWireframePsoDesc.RasterizerState.FillMode = FillMode.Wireframe
            _psos("opaque_wireframe") = Device.CreateGraphicsPipelineState(opaqueWireframePsoDesc)
        End Sub

        Private Sub BuildFrameResources()
            For i As Integer = 0 To NumFrameResources - 1
                _frameResources.Add(New FrameResource(Device, 1, _allRitems.Count, _waves.VertexCount))
                _fenceEvents.Add(New AutoResetEvent(False))
            Next
        End Sub

        Private Sub BuildRenderItems()
            _wavesRitem = AddRenderItem(RenderLayer.Opaque, 0, "waterGeo", "grid")
            AddRenderItem(RenderLayer.Opaque, 1, "landGeo", "grid")
        End Sub

        Private Function AddRenderItem(ByVal layer As RenderLayer, ByVal objCBIndex As Integer, ByVal geoName As String, ByVal submeshName As String) As RenderItem
            Dim geo As MeshGeometry = _geometries(geoName)
            Dim submesh As SubmeshGeometry = geo.DrawArgs(submeshName)
            Dim renderItem = New RenderItem With {
                .ObjCBIndex = objCBIndex,
                .Geo = geo,
                .IndexCount = submesh.IndexCount,
                .StartIndexLocation = submesh.StartIndexLocation,
                .BaseVertexLocation = submesh.BaseVertexLocation
            }
            _ritemLayers(layer).Add(renderItem)
            _allRitems.Add(renderItem)
            Return renderItem
        End Function

        Private Sub DrawRenderItems(ByVal cmdList As GraphicsCommandList, ByVal ritems As List(Of RenderItem))
            Dim objCBByteSize As Integer = D3DUtil.CalcConstantBufferByteSize(Of ObjectConstants)()
            Dim objectCB As Resource = CurrFrameResource.ObjectCB.Resource

            For Each ri As RenderItem In ritems
                cmdList.SetVertexBuffer(0, ri.Geo.VertexBufferView)
                cmdList.SetIndexBuffer(ri.Geo.IndexBufferView)
                cmdList.PrimitiveTopology = ri.PrimitiveType
                Dim objCBAddress As Long = objectCB.GPUVirtualAddress + ri.ObjCBIndex * objCBByteSize
                cmdList.SetGraphicsRootConstantBufferView(0, objCBAddress)
                cmdList.DrawIndexedInstanced(ri.IndexCount, 1, ri.StartIndexLocation, ri.BaseVertexLocation, 0)
            Next
        End Sub

        Private Shared Function GetHillsHeight(ByVal x As Single, ByVal z As Single) As Single
            Return 0.3F * (z * MathHelper.Sinf(0.1F * x) + x * MathHelper.Cosf(0.1F * z))
        End Function

    End Class
End Namespace
