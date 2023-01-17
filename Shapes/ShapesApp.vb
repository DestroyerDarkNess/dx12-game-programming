Imports Common.DX12GameProgramming
Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading
Imports SharpDX
Imports SharpDX.Direct3D12
Imports SharpDX.DXGI
Imports Resource = SharpDX.Direct3D12.Resource
Imports Common.DX12GameProgramming.GeometryGenerator

Namespace DX12GameProgramming
    Public Class ShapesApp
        Inherits D3DApp


        Private ReadOnly _frameResources As List(Of FrameResource) = New List(Of FrameResource)(NumFrameResources)
        Private ReadOnly _fenceEvents As List(Of AutoResetEvent) = New List(Of AutoResetEvent)(NumFrameResources)
        Private _currFrameResourceIndex As Integer
        Private _rootSignature As RootSignature
        Private _cbvHeap As DescriptorHeap
        Private _descriptorHeaps As DescriptorHeap()


        Private ReadOnly _geometries As Dictionary(Of String, MeshGeometry) = New Dictionary(Of String, MeshGeometry)()
        Private ReadOnly _shaders As Dictionary(Of String, ShaderBytecode) = New Dictionary(Of String, ShaderBytecode)()
        Private ReadOnly _psos As Dictionary(Of String, PipelineState) = New Dictionary(Of String, PipelineState)()
        Private _inputLayout As InputLayoutDescription

        Private ReadOnly _allRitems As List(Of RenderItem) = New List(Of RenderItem)()

        '  Private ReadOnly _ritemLayers As Dictionary(Of RenderLayer, List(Of RenderItem)) = New Dictionary(Of RenderLayer, List(Of RenderItem))

        Private ReadOnly _ritemLayers As Dictionary(Of RenderLayer, List(Of RenderItem)) = New Dictionary(Of RenderLayer, List(Of RenderItem))(1) From {
    {RenderLayer.Opaque, New List(Of RenderItem)()}
}

        Private _mainPassCB As PassConstants
        Private _passCbvOffset As Integer
        Private _isWireframe As Boolean = True
        Private _eyePos As Vector3
        Private _proj As Matrix = Matrix.Identity
        Private _view As Matrix = Matrix.Identity
        Private _theta As Single = 1.5F * MathUtil.Pi
        Private _phi As Single = 0.2F * MathUtil.Pi
        Private _radius As Single = 15.0F
        Private _lastMousePos As Point

        Public Sub New()
            MainWindowCaption = "Shapes"

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
            BuildRootSignature()
            BuildShadersAndInputLayout()
            BuildShapeGeometry()
            BuildRenderItems()
            BuildFrameResources()
            BuildDescriptorHeaps()
            BuildConstantBufferViews()
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
            CommandList.SetDescriptorHeaps(_descriptorHeaps.Length, _descriptorHeaps)
            CommandList.SetGraphicsRootSignature(_rootSignature)
            Dim passCbvIndex As Integer = _passCbvOffset + _currFrameResourceIndex
            Dim passCbvHandle As GpuDescriptorHandle = _cbvHeap.GPUDescriptorHandleForHeapStart
            passCbvHandle += passCbvIndex * CbvSrvUavDescriptorSize
            CommandList.SetGraphicsRootDescriptorTable(1, passCbvHandle)
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
                Dim dx As Single = 0.05F * (location.X - _lastMousePos.X)
                Dim dy As Single = 0.05F * (location.Y - _lastMousePos.Y)
                _radius += dx - dy
                _radius = MathUtil.Clamp(_radius, 5.0F, 150.0F)
            End If

            _lastMousePos = location
        End Sub

        Protected Overrides Sub OnKeyDown(ByVal keyCode As Keys)
            If keyCode = Keys.D1 Then _isWireframe = False
        End Sub

        Protected Overrides Sub OnKeyUp(ByVal keyCode As Keys)
            MyBase.OnKeyUp(keyCode)
            If keyCode = Keys.D1 Then _isWireframe = True
        End Sub

        Protected Overrides Sub Dispose(ByVal disposing As Boolean)
            If disposing Then
                _rootSignature?.Dispose()
                _cbvHeap?.Dispose()

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

        Private Sub BuildDescriptorHeaps()
            Dim objCount As Integer = _allRitems.Count
            Dim numDescriptors As Integer = (objCount + 1) * NumFrameResources
            _passCbvOffset = objCount * NumFrameResources
            Dim cbvHeapDesc = New DescriptorHeapDescription With {
                .DescriptorCount = numDescriptors,
                .Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
                .Flags = DescriptorHeapFlags.ShaderVisible
            }
            _cbvHeap = Device.CreateDescriptorHeap(cbvHeapDesc)
            _descriptorHeaps = {_cbvHeap}
        End Sub

        Private Sub BuildConstantBufferViews()
            Dim objCBByteSize As Integer = D3DUtil.CalcConstantBufferByteSize(Of ObjectConstants)()
            Dim objCount As Integer = _allRitems.Count

            For frameIndex As Integer = 0 To NumFrameResources - 1
                Dim objectCB As Resource = _frameResources(frameIndex).ObjectCB.Resource

                For i As Integer = 0 To objCount - 1
                    Dim cbAddress As Long = objectCB.GPUVirtualAddress
                    cbAddress += i * objCBByteSize
                    Dim heapIndex As Integer = frameIndex * objCount + i
                    Dim handle As CpuDescriptorHandle = _cbvHeap.CPUDescriptorHandleForHeapStart
                    handle += heapIndex * CbvSrvUavDescriptorSize
                    Dim cbvDesc = New ConstantBufferViewDescription With {
                        .BufferLocation = cbAddress,
                        .SizeInBytes = objCBByteSize
                    }
                    Device.CreateConstantBufferView(cbvDesc, handle)
                Next
            Next

            Dim passCBByteSize As Integer = D3DUtil.CalcConstantBufferByteSize(Of PassConstants)()

            For frameIndex As Integer = 0 To NumFrameResources - 1
                Dim passCB As Resource = _frameResources(frameIndex).PassCB.Resource
                Dim cbAddress As Long = passCB.GPUVirtualAddress
                Dim heapIndex As Integer = _passCbvOffset + frameIndex
                Dim handle As CpuDescriptorHandle = _cbvHeap.CPUDescriptorHandleForHeapStart
                handle += heapIndex * CbvSrvUavDescriptorSize
                Dim cbvDesc = New ConstantBufferViewDescription With {
                    .BufferLocation = cbAddress,
                    .SizeInBytes = passCBByteSize
                }
                Device.CreateConstantBufferView(cbvDesc, handle)
            Next
        End Sub

        Private Sub BuildRootSignature()
            Dim cbvTable0 = New DescriptorRange(DescriptorRangeType.ConstantBufferView, 1, 0)
            Dim cbvTable1 = New DescriptorRange(DescriptorRangeType.ConstantBufferView, 1, 1)
            Dim slotRootParameters = {New RootParameter(ShaderVisibility.Vertex, cbvTable0), New RootParameter(ShaderVisibility.Vertex, cbvTable1)}
            Dim rootSigDesc = New RootSignatureDescription(RootSignatureFlags.AllowInputAssemblerInputLayout, slotRootParameters)
            _rootSignature = Device.CreateRootSignature(rootSigDesc.Serialize())
        End Sub

        Private Sub BuildShadersAndInputLayout()
            _shaders("standardVS") = D3DUtil.CompileShader("Shaders\Color.hlsl", "VS", "vs_5_0")
            _shaders("opaquePS") = D3DUtil.CompileShader("Shaders\Color.hlsl", "PS", "ps_5_0")
            _inputLayout = New InputLayoutDescription({New InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0), New InputElement("COLOR", 0, Format.R32G32B32A32_Float, 12, 0)})
        End Sub

        Private Sub BuildShapeGeometry()
            Dim vertices = New List(Of Vertex)()
            Dim indices = New List(Of Short)()
            Dim box As SubmeshGeometry = AppendMeshData(GeometryGenerator.CreateBox(1.5F, 0.5F, 1.5F, 3), Color.DarkGreen, vertices, indices)
            Dim grid As SubmeshGeometry = AppendMeshData(GeometryGenerator.CreateGrid(20.0F, 30.0F, 60, 40), Color.ForestGreen, vertices, indices)
            Dim sphere As SubmeshGeometry = AppendMeshData(GeometryGenerator.CreateSphere(0.5F, 20, 20), Color.Crimson, vertices, indices)
            Dim cylinder As SubmeshGeometry = AppendMeshData(GeometryGenerator.CreateCylinder(0.5F, 0.3F, 3.0F, 20, 20), Color.SteelBlue, vertices, indices)
            Dim geo = MeshGeometry.[New](Device, CommandList, vertices, indices.ToArray(), "shapeGeo")
            geo.DrawArgs("box") = box
            geo.DrawArgs("grid") = grid
            geo.DrawArgs("sphere") = sphere
            geo.DrawArgs("cylinder") = cylinder
            _geometries(geo.Name) = geo
        End Sub

        Private Function AppendMeshData(ByVal meshData As GeometryGenerator.MeshData, ByVal color As Color, ByVal vertices As List(Of Vertex), ByVal indices As List(Of Short)) As SubmeshGeometry
            Dim submesh = New SubmeshGeometry With {
                .IndexCount = meshData.Indices32.Count,
                .StartIndexLocation = indices.Count,
                .BaseVertexLocation = vertices.Count
            }
            vertices.AddRange(meshData.Vertices.[Select](Function(vertex) New Vertex With {
                .Pos = vertex.Position,
                .Color = color.ToVector4()
            }))
            indices.AddRange(meshData.GetIndices16())
            Return submesh
        End Function

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
                _frameResources.Add(New FrameResource(Device, 1, _allRitems.Count))
                _fenceEvents.Add(New AutoResetEvent(False))
            Next
        End Sub

        Private Sub BuildRenderItems()
            AddRenderItem(RenderLayer.Opaque, 0, "shapeGeo", "box", world:=Matrix.Scaling(2.0F, 2.0F, 2.0F) * Matrix.Translation(0.0F, 0.5F, 0.0F))
            AddRenderItem(RenderLayer.Opaque, 1, "shapeGeo", "grid")
            Dim objCBIndex As Integer = 2

            For i As Integer = 0 To 5 - 1
                AddRenderItem(RenderLayer.Opaque, Math.Min(System.Threading.Interlocked.Increment(objCBIndex), objCBIndex - 1), "shapeGeo", "cylinder", world:=Matrix.Translation(-5.0F, 1.5F, -10.0F + i * 5.0F))
                AddRenderItem(RenderLayer.Opaque, Math.Min(System.Threading.Interlocked.Increment(objCBIndex), objCBIndex - 1), "shapeGeo", "cylinder", world:=Matrix.Translation(+5.0F, 1.5F, -10.0F + i * 5.0F))
                AddRenderItem(RenderLayer.Opaque, Math.Min(System.Threading.Interlocked.Increment(objCBIndex), objCBIndex - 1), "shapeGeo", "sphere", world:=Matrix.Translation(-5.0F, 3.5F, -10.0F + i * 5.0F))
                AddRenderItem(RenderLayer.Opaque, Math.Min(System.Threading.Interlocked.Increment(objCBIndex), objCBIndex - 1), "shapeGeo", "sphere", world:=Matrix.Translation(+5.0F, 3.5F, -10.0F + i * 5.0F))
            Next
        End Sub

        Private Sub AddRenderItem(ByVal layer As RenderLayer, ByVal objCBIndex As Integer, ByVal geoName As String, ByVal submeshName As String, ByVal Optional world As Matrix? = Nothing)
            Dim geo As MeshGeometry = _geometries(geoName)
            Dim submesh As SubmeshGeometry = geo.DrawArgs(submeshName)
            Dim renderItem = New RenderItem With {
                .ObjCBIndex = objCBIndex,
                .Geo = geo,
                .IndexCount = submesh.IndexCount,
                .StartIndexLocation = submesh.StartIndexLocation,
                .BaseVertexLocation = submesh.BaseVertexLocation,
                .World = If(world, Matrix.Identity)
            }
            _ritemLayers(layer).Add(renderItem)
            _allRitems.Add(renderItem)
        End Sub

        Private Sub DrawRenderItems(ByVal cmdList As GraphicsCommandList, ByVal ritems As List(Of RenderItem))
            For Each ri As RenderItem In ritems
                cmdList.SetVertexBuffer(0, ri.Geo.VertexBufferView)
                cmdList.SetIndexBuffer(ri.Geo.IndexBufferView)
                cmdList.PrimitiveTopology = ri.PrimitiveType
                Dim cbvIndex As Integer = _currFrameResourceIndex * _allRitems.Count + ri.ObjCBIndex
                Dim cbvHandle As GpuDescriptorHandle = _cbvHeap.GPUDescriptorHandleForHeapStart
                cbvHandle += cbvIndex * CbvSrvUavDescriptorSize
                cmdList.SetGraphicsRootDescriptorTable(0, cbvHandle)
                cmdList.DrawIndexedInstanced(ri.IndexCount, 1, ri.StartIndexLocation, ri.BaseVertexLocation, 0)
            Next
        End Sub

    End Class
End Namespace
