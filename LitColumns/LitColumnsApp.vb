Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Threading
Imports Common.DX12GameProgramming
Imports SharpDX
Imports SharpDX.Direct3D12
Imports SharpDX.DXGI
Imports Resource = SharpDX.Direct3D12.Resource

Namespace DX12GameProgramming
    Public Class LitColumnsApp
        Inherits D3DApp

        ' Render items divided by PSO.
        Private ReadOnly _ritemLayers As Dictionary(Of RenderLayer, List(Of RenderItem)) = New Dictionary(Of RenderLayer, List(Of RenderItem))(1) From {
{RenderLayer.Opaque, New List(Of RenderItem)()}
}

        Private ReadOnly _frameResources As List(Of FrameResource) = New List(Of FrameResource)(NumFrameResources)
        Private ReadOnly _fenceEvents As List(Of AutoResetEvent) = New List(Of AutoResetEvent)(NumFrameResources)
        Private _currFrameResourceIndex As Integer
        Private _rootSignature As RootSignature
        Private ReadOnly _geometries As Dictionary(Of String, MeshGeometry) = New Dictionary(Of String, MeshGeometry)()
        Private ReadOnly _materials As Dictionary(Of String, Material) = New Dictionary(Of String, Material)()
        Private ReadOnly _shaders As Dictionary(Of String, ShaderBytecode) = New Dictionary(Of String, ShaderBytecode)()
        Private _opaquePso As PipelineState
        Private _inputLayout As InputLayoutDescription
        Private ReadOnly _allRitems As List(Of RenderItem) = New List(Of RenderItem)()
        Private _mainPassCB As PassConstants = PassConstants.[Default]
        Private _eyePos As Vector3
        Private _proj As Matrix = Matrix.Identity
        Private _view As Matrix = Matrix.Identity
        Private _theta As Single = 1.5F * MathUtil.Pi
        Private _phi As Single = 0.2F * MathUtil.Pi
        Private _radius As Single = 15.0F
        Private _lastMousePos As Point

        Public Sub New()
            MainWindowCaption = "Lit Columns"
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
            BuildSkullGeometry()
            BuildMaterials()
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
            UpdateMaterialCBs()
            UpdateMainPassCB(gt)
        End Sub

        Protected Overrides Sub Draw(ByVal gt As GameTimer)
            Dim cmdListAlloc As CommandAllocator = CurrFrameResource.CmdListAlloc
            cmdListAlloc.Reset()
            CommandList.Reset(cmdListAlloc, _opaquePso)
            CommandList.SetViewport(Viewport)
            CommandList.SetScissorRectangles(ScissorRectangle)
            CommandList.ResourceBarrierTransition(CurrentBackBuffer, ResourceStates.Present, ResourceStates.RenderTarget)
            CommandList.ClearRenderTargetView(CurrentBackBufferView, Color.LightSteelBlue)
            CommandList.ClearDepthStencilView(DepthStencilView, ClearFlags.FlagsDepth Or ClearFlags.FlagsStencil, 1.0F, 0)
            CommandList.SetRenderTargets(CurrentBackBufferView, DepthStencilView)
            CommandList.SetGraphicsRootSignature(_rootSignature)
            Dim passCB As Resource = CurrFrameResource.PassCB.Resource
            CommandList.SetGraphicsRootConstantBufferView(2, passCB.GPUVirtualAddress)
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

        Protected Overrides Sub Dispose(ByVal disposing As Boolean)
            If disposing Then
                _rootSignature?.Dispose()
                _opaquePso?.Dispose()

                For Each frameResource As FrameResource In _frameResources
                    frameResource.Dispose()
                Next

                For Each geometry As MeshGeometry In _geometries.Values
                    geometry.Dispose()
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

        Private Sub UpdateMaterialCBs()
            Dim currMaterialCB As UploadBuffer(Of MaterialConstants) = CurrFrameResource.MaterialCB

            For Each mat As Material In _materials.Values

                If mat.NumFramesDirty > 0 Then
                    Dim matConstants = New MaterialConstants With {
                        .DiffuseAlbedo = mat.DiffuseAlbedo,
                        .FresnelR0 = mat.FresnelR0,
                        .Roughness = mat.Roughness
                    }
                    currMaterialCB.CopyData(mat.MatCBIndex, matConstants)
                    mat.NumFramesDirty -= 1
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
            _mainPassCB.TotalTime = gt.TotalTime
            _mainPassCB.DeltaTime = gt.DeltaTime
            _mainPassCB.AmbientLight = New Vector4(0.25F, 0.25F, 0.35F, 1.0F)
            _mainPassCB.Lights(0).Direction = New Vector3(0.57735F, -0.57735F, 0.57735F)
            _mainPassCB.Lights(0).Strength = New Vector3(0.6F)
            _mainPassCB.Lights(1).Direction = New Vector3(-0.57735F, -0.57735F, 0.57735F)
            _mainPassCB.Lights(1).Strength = New Vector3(0.3F)
            _mainPassCB.Lights(2).Direction = New Vector3(0.0F, -0.707F, -0.707F)
            _mainPassCB.Lights(2).Strength = New Vector3(0.15F)
            CurrFrameResource.PassCB.CopyData(0, _mainPassCB)
        End Sub

        Private Sub BuildRootSignature()
            Dim descriptor1 = New RootDescriptor(0, 0)
            Dim descriptor2 = New RootDescriptor(1, 0)
            Dim descriptor3 = New RootDescriptor(2, 0)
            Dim slotRootParameters = {New RootParameter(ShaderVisibility.Vertex, descriptor1, RootParameterType.ConstantBufferView), New RootParameter(ShaderVisibility.Pixel, descriptor2, RootParameterType.ConstantBufferView), New RootParameter(ShaderVisibility.All, descriptor3, RootParameterType.ConstantBufferView)}
            Dim rootSigDesc = New RootSignatureDescription(RootSignatureFlags.AllowInputAssemblerInputLayout, slotRootParameters)
            _rootSignature = Device.CreateRootSignature(rootSigDesc.Serialize())
        End Sub

        Private Sub BuildShadersAndInputLayout()
            _shaders("standardVS") = D3DUtil.CompileShader("Shaders\Default.hlsl", "VS", "vs_5_0")
            _shaders("opaquePS") = D3DUtil.CompileShader("Shaders\Default.hlsl", "PS", "ps_5_0")
            _inputLayout = New InputLayoutDescription({New InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0), New InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0)})
        End Sub

        Private Sub BuildShapeGeometry()
            Dim vertices = New List(Of Vertex)()
            Dim indices = New List(Of Short)()
            Dim box As SubmeshGeometry = AppendMeshData(GeometryGenerator.CreateBox(1.5F, 0.5F, 1.5F, 3), vertices, indices)
            Dim grid As SubmeshGeometry = AppendMeshData(GeometryGenerator.CreateGrid(20.0F, 30.0F, 60, 40), vertices, indices)
            Dim sphere As SubmeshGeometry = AppendMeshData(GeometryGenerator.CreateSphere(0.5F, 20, 20), vertices, indices)
            Dim cylinder As SubmeshGeometry = AppendMeshData(GeometryGenerator.CreateCylinder(0.5F, 0.3F, 3.0F, 20, 20), vertices, indices)
            Dim geo = MeshGeometry.[New](Device, CommandList, vertices.ToArray(), indices.ToArray(), "shapeGeo")
            geo.DrawArgs("box") = box
            geo.DrawArgs("grid") = grid
            geo.DrawArgs("sphere") = sphere
            geo.DrawArgs("cylinder") = cylinder
            _geometries(geo.Name) = geo
        End Sub

        Private Shared Function AppendMeshData(ByVal meshData As GeometryGenerator.MeshData, ByVal vertices As List(Of Vertex), ByVal indices As List(Of Short)) As SubmeshGeometry
            Dim submesh = New SubmeshGeometry With {
                .IndexCount = meshData.Indices32.Count,
                .StartIndexLocation = indices.Count,
                .BaseVertexLocation = vertices.Count
            }
            vertices.AddRange(meshData.Vertices.[Select](Function(vertex) New Vertex With {
                .Pos = vertex.Position,
                .Normal = vertex.Normal
            }))
            indices.AddRange(meshData.GetIndices16())
            Return submesh
        End Function

        Private Sub BuildSkullGeometry()
            Dim vertices = New List(Of Vertex)()
            Dim indices = New List(Of Integer)()
            Dim vCount As Integer = 0, tCount As Integer = 0

            Using reader = New StreamReader("Models\Skull.txt")
                Dim input = reader.ReadLine()
                If input IsNot Nothing Then vCount = Convert.ToInt32(input.Split(":"c)(1).Trim())
                input = reader.ReadLine()
                If input IsNot Nothing Then tCount = Convert.ToInt32(input.Split(":"c)(1).Trim())

                Do
                    input = reader.ReadLine()
                Loop While input IsNot Nothing AndAlso Not input.StartsWith("{", StringComparison.Ordinal)

                For i As Integer = 0 To vCount - 1
                    input = reader.ReadLine()

                    If input IsNot Nothing Then
                        Dim vals = input.Split(" "c)
                        vertices.Add(New Vertex With {
                            .Pos = New Vector3(Convert.ToSingle(vals(0).Trim(), CultureInfo.InvariantCulture), Convert.ToSingle(vals(1).Trim(), CultureInfo.InvariantCulture), Convert.ToSingle(vals(2).Trim(), CultureInfo.InvariantCulture)),
                            .Normal = New Vector3(Convert.ToSingle(vals(3).Trim(), CultureInfo.InvariantCulture), Convert.ToSingle(vals(4).Trim(), CultureInfo.InvariantCulture), Convert.ToSingle(vals(5).Trim(), CultureInfo.InvariantCulture))
                        })
                    End If
                Next

                Do
                    input = reader.ReadLine()
                Loop While input IsNot Nothing AndAlso Not input.StartsWith("{", StringComparison.Ordinal)

                For i = 0 To tCount - 1
                    input = reader.ReadLine()

                    If input Is Nothing Then
                        Exit For
                    End If

                    Dim m = input.Trim().Split(" "c)
                    indices.Add(Convert.ToInt32(m(0).Trim()))
                    indices.Add(Convert.ToInt32(m(1).Trim()))
                    indices.Add(Convert.ToInt32(m(2).Trim()))
                Next
            End Using

            Dim geo = MeshGeometry.[New](Device, CommandList, vertices, indices, "skullGeo")
            Dim submesh = New SubmeshGeometry With {
                .IndexCount = indices.Count,
                .StartIndexLocation = 0,
                .BaseVertexLocation = 0
            }
            geo.DrawArgs("skull") = submesh
            _geometries(geo.Name) = geo
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
            _opaquePso = Device.CreateGraphicsPipelineState(opaquePsoDesc)
        End Sub

        Private Sub BuildFrameResources()
            For i As Integer = 0 To NumFrameResources - 1
                _frameResources.Add(New FrameResource(Device, 1, _allRitems.Count, _materials.Count))
                _fenceEvents.Add(New AutoResetEvent(False))
            Next
        End Sub

        Private Sub BuildMaterials()
            AddMaterial(New Material With {
                .Name = "bricks0",
                .MatCBIndex = 0,
                .DiffuseSrvHeapIndex = 0,
                .DiffuseAlbedo = Color.ForestGreen.ToVector4(),
                .FresnelR0 = New Vector3(0.02F),
                .Roughness = 0.1F
            })
            AddMaterial(New Material With {
                .Name = "stone0",
                .MatCBIndex = 1,
                .DiffuseSrvHeapIndex = 1,
                .DiffuseAlbedo = Color.LightSteelBlue.ToVector4(),
                .FresnelR0 = New Vector3(0.05F),
                .Roughness = 0.3F
            })
            AddMaterial(New Material With {
                .Name = "tile0",
                .MatCBIndex = 2,
                .DiffuseSrvHeapIndex = 2,
                .DiffuseAlbedo = Color.LightGray.ToVector4(),
                .FresnelR0 = New Vector3(0.02F),
                .Roughness = 0.2F
            })
            AddMaterial(New Material With {
                .Name = "skullMat",
                .MatCBIndex = 3,
                .DiffuseSrvHeapIndex = 3,
                .DiffuseAlbedo = Color.White.ToVector4(),
                .FresnelR0 = New Vector3(0.05F),
                .Roughness = 0.3F
            })
        End Sub

        Private Sub AddMaterial(ByVal mat As Material)
            _materials(mat.Name) = mat
        End Sub

        Private Sub BuildRenderItems()
            AddRenderItem(RenderLayer.Opaque, 0, "stone0", "shapeGeo", "box", world:=Matrix.Scaling(2.0F, 2.0F, 2.0F) * Matrix.Translation(0.0F, 0.5F, 0.0F))
            AddRenderItem(RenderLayer.Opaque, 1, "tile0", "shapeGeo", "grid")
            AddRenderItem(RenderLayer.Opaque, 2, "skullMat", "skullGeo", "skull", world:=Matrix.Scaling(0.5F) * Matrix.Translation(Vector3.UnitY))
            Dim objCBIndex As Integer = 3

            For i As Integer = 0 To 5 - 1
                AddRenderItem(RenderLayer.Opaque, Math.Min(System.Threading.Interlocked.Increment(objCBIndex), objCBIndex - 1), "bricks0", "shapeGeo", "cylinder", world:=Matrix.Translation(-5.0F, 1.5F, -10.0F + i * 5.0F))
                AddRenderItem(RenderLayer.Opaque, Math.Min(System.Threading.Interlocked.Increment(objCBIndex), objCBIndex - 1), "bricks0", "shapeGeo", "cylinder", world:=Matrix.Translation(+5.0F, 1.5F, -10.0F + i * 5.0F))
                AddRenderItem(RenderLayer.Opaque, Math.Min(System.Threading.Interlocked.Increment(objCBIndex), objCBIndex - 1), "stone0", "shapeGeo", "sphere", world:=Matrix.Translation(-5.0F, 3.5F, -10.0F + i * 5.0F))
                AddRenderItem(RenderLayer.Opaque, Math.Min(System.Threading.Interlocked.Increment(objCBIndex), objCBIndex - 1), "stone0", "shapeGeo", "sphere", world:=Matrix.Translation(+5.0F, 3.5F, -10.0F + i * 5.0F))
            Next
        End Sub

        Private Sub AddRenderItem(ByVal layer As RenderLayer, ByVal objCBIndex As Integer, ByVal matName As String, ByVal geoName As String, ByVal submeshName As String, ByVal Optional world As Matrix? = Nothing)
            Dim geo As MeshGeometry = _geometries(geoName)
            Dim submesh As SubmeshGeometry = geo.DrawArgs(submeshName)
            Dim renderItem = New RenderItem With {
                .ObjCBIndex = objCBIndex,
                .Mat = _materials(matName),
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
            Dim objCBByteSize As Integer = D3DUtil.CalcConstantBufferByteSize(Of ObjectConstants)()
            Dim matCBByteSize As Integer = D3DUtil.CalcConstantBufferByteSize(Of MaterialConstants)()
            Dim objectCB As Resource = CurrFrameResource.ObjectCB.Resource
            Dim matCB As Resource = CurrFrameResource.MaterialCB.Resource

            For Each ri As RenderItem In ritems
                cmdList.SetVertexBuffer(0, ri.Geo.VertexBufferView)
                cmdList.SetIndexBuffer(ri.Geo.IndexBufferView)
                cmdList.PrimitiveTopology = ri.PrimitiveType
                Dim objCBAddress As Long = objectCB.GPUVirtualAddress + ri.ObjCBIndex * objCBByteSize
                Dim matCBAddress As Long = matCB.GPUVirtualAddress + ri.Mat.MatCBIndex * matCBByteSize
                cmdList.SetGraphicsRootConstantBufferView(0, objCBAddress)
                cmdList.SetGraphicsRootConstantBufferView(1, matCBAddress)
                cmdList.DrawIndexedInstanced(ri.IndexCount, 1, ri.StartIndexLocation, ri.BaseVertexLocation, 0)
            Next
        End Sub

    End Class
End Namespace
