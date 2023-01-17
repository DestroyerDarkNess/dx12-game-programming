Imports System
Imports System.Diagnostics
Imports System.Drawing
Imports System.Threading
Imports System.Windows.Forms
Imports System.Windows.Input
Imports SharpDX
Imports SharpDX.Direct3D
Imports SharpDX.Direct3D12
Imports SharpDX.DXGI
Imports Device = SharpDX.Direct3D12.Device
Imports Feature = SharpDX.Direct3D12.Feature
Imports Point = SharpDX.Point
Imports Resource = SharpDX.Direct3D12.Resource
Imports RectangleF = SharpDX.RectangleF

Namespace DX12GameProgramming

    Public Class D3DApp
        ' Inherits IDisposable

        Public Const NumFrameResources As Integer = 3
        Public Const SwapChainBufferCount As Integer = 2
        Private _window As Form
        Private _appPaused As Boolean
        Private _minimized As Boolean
        Private _maximized As Boolean
        Private _resizing As Boolean
        Private _running As Boolean
        Private _m4xMsaaState As Boolean
        Private _m4xMsaaQuality As Integer
        Private _lastWindowState As FormWindowState = FormWindowState.Normal
        Private _frameCount As Integer
        Private _timeElapsed As Single
        Private _factory As Factory4
        Private ReadOnly _swapChainBuffers As Resource() = New Resource(1) {}
        Private _fenceEvent As AutoResetEvent

        Public Property M4xMsaaState As Boolean
            Get
                Return _m4xMsaaState
            End Get
            Set(ByVal value As Boolean)

                If _m4xMsaaState <> value Then
                    _m4xMsaaState = value

                    If _running Then
                        CreateSwapChain()
                        OnResize()
                    End If
                End If
            End Set
        End Property

        Protected Property RtvHeap As DescriptorHeap
        Protected Property DsvHeap As DescriptorHeap

        Protected ReadOnly Property MsaaCount As Integer
            Get
                Return If(M4xMsaaState, 4, 1)
            End Get
        End Property

        Protected ReadOnly Property MsaaQuality As Integer
            Get
                Return If(M4xMsaaState, _m4xMsaaQuality - 1, 0)
            End Get
        End Property

        Protected ReadOnly Property Timer As GameTimer = New GameTimer()
        Protected Property Device As Device
        Protected Property Fence As Fence
        Protected Property CurrentFence As Long
        Protected Property RtvDescriptorSize As Integer
        Protected Property DsvDescriptorSize As Integer
        Protected Property CbvSrvUavDescriptorSize As Integer
        Protected Property CommandQueue As CommandQueue
        Protected Property DirectCmdListAlloc As CommandAllocator
        Protected Property CommandList As GraphicsCommandList
        Protected Property SwapChain As SwapChain3
        Protected Property DepthStencilBuffer As Resource
        Protected Property Viewport As ViewportF
        Protected Property ScissorRectangle As RectangleF
        Protected Property MainWindowCaption As String = "D3D12 Application"
        Protected Property ClientWidth As Integer = 1280
        Protected Property ClientHeight As Integer = 720

        Protected ReadOnly Property AspectRatio As Single
            Get
                Return CSng(ClientWidth) / ClientHeight
            End Get
        End Property

        Protected ReadOnly Property BackBufferFormat As Format = Format.R8G8B8A8_UNorm
        Protected ReadOnly Property DepthStencilFormat As Format = Format.D24_UNorm_S8_UInt


        Protected ReadOnly Property CurrentBackBuffer As Resource
            Get
                Return _swapChainBuffers(SwapChain.CurrentBackBufferIndex)
            End Get
        End Property

        Protected ReadOnly Property CurrentBackBufferView As CpuDescriptorHandle
            Get
                Return RtvHeap.CPUDescriptorHandleForHeapStart + SwapChain.CurrentBackBufferIndex * RtvDescriptorSize
            End Get
        End Property

        Protected ReadOnly Property DepthStencilView As CpuDescriptorHandle
            Get
                Return DsvHeap.CPUDescriptorHandleForHeapStart
            End Get
        End Property

        Public Overridable Sub Initialize()
            InitMainWindow()
            InitDirect3D()
            OnResize()
            _running = True
        End Sub

        Public Sub Run()
            Timer.Reset()

            While _running
                Application.DoEvents()
                Timer.Tick()

                If Not _appPaused Then
                    CalculateFrameRateStats()
                    Update(Timer)
                    Draw(Timer)
                Else
                    Thread.Sleep(100)
                End If
            End While
        End Sub

        Public Sub Dispose() 'Implements IDisposable.Dispose
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub

        Protected Overridable Sub Dispose(ByVal disposing As Boolean)
            If disposing Then
                FlushCommandQueue()
                RtvHeap?.Dispose()
                DsvHeap?.Dispose()
                SwapChain?.Dispose()

                For Each buffer As Resource In _swapChainBuffers
                    buffer?.Dispose()
                Next

                DepthStencilBuffer?.Dispose()
                CommandList?.Dispose()
                DirectCmdListAlloc?.Dispose()
                CommandQueue?.Dispose()
                Fence?.Dispose()
                Device?.Dispose()
            End If
        End Sub

        Protected Overridable Sub OnResize()
            Debug.Assert(Device IsNot Nothing)
            Debug.Assert(SwapChain IsNot Nothing)
            Debug.Assert(DirectCmdListAlloc IsNot Nothing)
            FlushCommandQueue()
            CommandList.Reset(DirectCmdListAlloc, Nothing)

            For Each buffer As Resource In _swapChainBuffers
                buffer?.Dispose()
            Next

            DepthStencilBuffer?.Dispose()
            SwapChain.ResizeBuffers(SwapChainBufferCount, ClientWidth, ClientHeight, BackBufferFormat, SwapChainFlags.AllowModeSwitch)
            Dim rtvHeapHandle As CpuDescriptorHandle = RtvHeap.CPUDescriptorHandleForHeapStart

            For i As Integer = 0 To SwapChainBufferCount - 1
                Dim backBuffer As Resource = SwapChain.GetBackBuffer(Of Resource)(i)
                _swapChainBuffers(i) = backBuffer
                Device.CreateRenderTargetView(backBuffer, Nothing, rtvHeapHandle)
                rtvHeapHandle += RtvDescriptorSize
            Next

            Dim depthStencilDesc = New ResourceDescription With {
                .Dimension = ResourceDimension.Texture2D,
                .Alignment = 0,
                .Width = ClientWidth,
                .Height = ClientHeight,
                .DepthOrArraySize = 1,
                .MipLevels = 1,
                .Format = Format.R24G8_Typeless,
                .SampleDescription = New SampleDescription With {
                    .Count = MsaaCount,
                    .Quality = MsaaQuality
                },
                .Layout = TextureLayout.Unknown,
                .Flags = ResourceFlags.AllowDepthStencil
            }
            Dim optClear = New ClearValue With {
                .Format = DepthStencilFormat,
                .DepthStencil = New DepthStencilValue With {
                    .Depth = 1.0F,
                    .Stencil = 0
                }
            }
            DepthStencilBuffer = Device.CreateCommittedResource(New HeapProperties(HeapType.[Default]), HeapFlags.None, depthStencilDesc, ResourceStates.Common, optClear)
            Dim depthStencilViewDesc = New DepthStencilViewDescription With {
                .Dimension = If(M4xMsaaState, DepthStencilViewDimension.Texture2DMultisampled, DepthStencilViewDimension.Texture2D),
                .Format = DepthStencilFormat
            }
            Dim dsvHeapHandle As CpuDescriptorHandle = DsvHeap.CPUDescriptorHandleForHeapStart
            Device.CreateDepthStencilView(DepthStencilBuffer, depthStencilViewDesc, dsvHeapHandle)
            CommandList.ResourceBarrierTransition(DepthStencilBuffer, ResourceStates.Common, ResourceStates.DepthWrite)
            CommandList.Close()
            CommandQueue.ExecuteCommandList(CommandList)
            FlushCommandQueue()
            Viewport = New ViewportF(0, 0, ClientWidth, ClientHeight, 0.0F, 1.0F)
            ScissorRectangle = New RectangleF(0, 0, ClientWidth, ClientHeight)
        End Sub

        Protected Overridable Sub Update(ByVal gt As GameTimer)
        End Sub

        Protected Overridable Sub Draw(ByVal gt As GameTimer)
        End Sub

        Protected Sub InitMainWindow()
            _window = New Form With {
                .Text = MainWindowCaption,
                .Name = "D3DWndClassName",
                .FormBorderStyle = FormBorderStyle.Sizable,
                .ClientSize = New Size(ClientWidth, ClientHeight),
                .StartPosition = FormStartPosition.CenterScreen,
                .MinimumSize = New Size(200, 200)
            }
            AddHandler _window.MouseDown, Function(sender, e)
                                              OnMouseDown(CType(e.Button, MouseButtons), New Point(e.X, e.Y))
                                          End Function
            AddHandler _window.MouseUp, Function(sender, e)
                                            OnMouseUp(CType(e.Button, MouseButtons), New Point(e.X, e.Y))
                                        End Function
            AddHandler _window.MouseMove, Function(sender, e)
                                              OnMouseMove(CType(e.Button, MouseButtons), New Point(e.X, e.Y))
                                          End Function
            AddHandler _window.KeyDown, Function(sender, e)
                                            OnKeyDown(CType(e.KeyCode, Keys))
                                        End Function
            AddHandler _window.KeyUp, Function(sender, e)
                                          OnKeyUp(CType(e.KeyCode, Keys))
                                      End Function
            AddHandler _window.ResizeBegin, Function(sender, e)
                                                _appPaused = True
                                                _resizing = True
                                                Timer.[Stop]()
                                            End Function

            AddHandler _window.ResizeEnd, Function(sender, e)
                                              _appPaused = False
                                              _resizing = False
                                              Timer.Start()
                                              OnResize()
                                          End Function

            AddHandler _window.Activated, Function(sender, e)
                                              _appPaused = False
                                              Timer.Start()
                                          End Function

            AddHandler _window.Deactivate, Function(sender, e)
                                               _appPaused = True
                                               Timer.[Stop]()
                                           End Function

            AddHandler _window.HandleDestroyed, Function(sender, e)
                                                    _running = False
                                                End Function
            AddHandler _window.Resize, Function(sender, e)
                                           ClientWidth = _window.ClientSize.Width
                                           ClientHeight = _window.ClientSize.Height

                                           If _window.WindowState <> _lastWindowState Then
                                               _lastWindowState = _window.WindowState

                                               If _window.WindowState = FormWindowState.Maximized Then
                                                   _appPaused = False
                                                   _minimized = False
                                                   _maximized = True
                                                   OnResize()
                                               ElseIf _window.WindowState = FormWindowState.Minimized Then
                                                   _appPaused = True
                                                   _minimized = True
                                                   _maximized = False
                                               ElseIf _window.WindowState = FormWindowState.Normal Then

                                                   If _minimized Then
                                                       _appPaused = False
                                                       _minimized = False
                                                       OnResize()
                                                   ElseIf _maximized Then
                                                       _appPaused = False
                                                       _maximized = False
                                                       OnResize()
                                                   ElseIf _resizing Then
                                                   Else
                                                       OnResize()
                                                   End If
                                               End If
                                           ElseIf Not _resizing Then
                                               OnResize()
                                           End If
                                       End Function

            _window.Show()
            _window.Update()
        End Sub

        Protected Overridable Sub OnMouseDown(ByVal button As MouseButtons, ByVal location As Point)
            _window.Capture = True
        End Sub

        Protected Overridable Sub OnMouseUp(ByVal button As MouseButtons, ByVal location As Point)
            _window.Capture = False
        End Sub

        Protected Overridable Sub OnMouseMove(ByVal button As MouseButtons, ByVal location As Point)
        End Sub

        Protected Overridable Sub OnKeyDown(ByVal keyCode As Keys)
        End Sub

        Protected Overridable Sub OnKeyUp(ByVal keyCode As Keys)
            Select Case keyCode
                Case Keys.Escape
                    _running = False
                Case Keys.F2
                    M4xMsaaState = Not M4xMsaaState
            End Select
        End Sub

        Protected Function IsKeyDown(ByVal keyCode As Keys) As Boolean
            Return Keyboard.IsKeyDown(KeyInterop.KeyFromVirtualKey(CInt(keyCode)))
        End Function

        Protected Sub InitDirect3D()
#If DEBUG Then
            Try
                DebugInterface.[Get]().EnableDebugLayer()
            Catch ex As SharpDXException When ex.Descriptor.NativeApiCode = "DXGI_ERROR_SDK_COMPONENT_MISSING"
                Debug.WriteLine("Failed to enable debug layer. Please ensure ""Graphics Tools"" feature is enabled in Windows ""Manage optional feature"" settings page")
            End Try
#End If

            _factory = New Factory4()

            Try
                Device = New Device(Nothing, FeatureLevel.Level_11_0)
            Catch __unusedSharpDXException1__ As SharpDXException
                Dim warpAdapter As Adapter = _factory.GetWarpAdapter()
                Device = New Device(warpAdapter, FeatureLevel.Level_11_0)
            End Try

            Fence = Device.CreateFence(0, FenceFlags.None)
            _fenceEvent = New AutoResetEvent(False)
            RtvDescriptorSize = Device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView)
            DsvDescriptorSize = Device.GetDescriptorHandleIncrementSize(DescriptorHeapType.DepthStencilView)
            CbvSrvUavDescriptorSize = Device.GetDescriptorHandleIncrementSize(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView)
            Dim msQualityLevels As FeatureDataMultisampleQualityLevels
            msQualityLevels.Format = BackBufferFormat
            msQualityLevels.SampleCount = 4
            msQualityLevels.Flags = MultisampleQualityLevelFlags.None
            msQualityLevels.QualityLevelCount = 0
            Debug.Assert(Device.CheckFeatureSupport(Feature.MultisampleQualityLevels, msQualityLevels))
            _m4xMsaaQuality = msQualityLevels.QualityLevelCount
#If DEBUG Then
            LogAdapters()
#End If
            CreateCommandObjects()
            CreateSwapChain()
            CreateRtvAndDsvDescriptorHeaps()
        End Sub

        Protected Sub FlushCommandQueue()
            CurrentFence += 1
            CommandQueue.Signal(Fence, CurrentFence)

            If Fence.CompletedValue < CurrentFence Then
                Fence.SetEventOnCompletion(CurrentFence, _fenceEvent.SafeWaitHandle.DangerousGetHandle())
                _fenceEvent.WaitOne()
            End If
        End Sub


        Protected Overridable ReadOnly Property RtvDescriptorCount As Integer
            Get
                Return SwapChainBufferCount
            End Get
        End Property

        Protected Overridable ReadOnly Property DsvDescriptorCount As Integer
            Get
                Return 1
            End Get
        End Property


        Private Sub CreateCommandObjects()
            Dim queueDesc = New CommandQueueDescription(CommandListType.Direct)
            CommandQueue = Device.CreateCommandQueue(queueDesc)
            DirectCmdListAlloc = Device.CreateCommandAllocator(CommandListType.Direct)
            CommandList = Device.CreateCommandList(0, CommandListType.Direct, DirectCmdListAlloc, Nothing)
            CommandList.Close()
        End Sub

        Private Sub CreateSwapChain()
            SwapChain?.Dispose()
            Dim sd = New SwapChainDescription With {
                .ModeDescription = New ModeDescription With {
                    .Width = ClientWidth,
                    .Height = ClientHeight,
                    .Format = BackBufferFormat,
                    .RefreshRate = New Rational(60, 1),
                    .Scaling = DisplayModeScaling.Unspecified,
                    .ScanlineOrdering = DisplayModeScanlineOrder.Unspecified
                },
                .SampleDescription = New SampleDescription With {
                    .Count = 1,
                    .Quality = 0
                },
                .Usage = Usage.RenderTargetOutput,
                .BufferCount = SwapChainBufferCount,
                .SwapEffect = SwapEffect.FlipDiscard,
                .Flags = SwapChainFlags.AllowModeSwitch,
                .OutputHandle = _window.Handle,
                .IsWindowed = True
            }

            Using tempSwapChain = New SwapChain(_factory, CommandQueue, sd)
                SwapChain = tempSwapChain.QueryInterface(Of SwapChain3)()
            End Using
        End Sub

        Private Sub CreateRtvAndDsvDescriptorHeaps()
            Dim rtvHeapDesc = New DescriptorHeapDescription With {
                .DescriptorCount = RtvDescriptorCount,
                .Type = DescriptorHeapType.RenderTargetView
            }
            RtvHeap = Device.CreateDescriptorHeap(rtvHeapDesc)
            Dim dsvHeapDesc = New DescriptorHeapDescription With {
                .DescriptorCount = DsvDescriptorCount,
                .Type = DescriptorHeapType.DepthStencilView
            }
            DsvHeap = Device.CreateDescriptorHeap(dsvHeapDesc)
        End Sub

        Private Sub LogAdapters()
            For Each adapter As Adapter In _factory.Adapters
                Debug.WriteLine($"***Adapter: {adapter.Description.Description}")
                LogAdapterOutputs(adapter)
            Next
        End Sub

        Private Sub LogAdapterOutputs(ByVal adapter As Adapter)
            For Each output As Output In adapter.Outputs
                Debug.WriteLine($"***Output: {output.Description.DeviceName}")
                LogOutputDisplayModes(output, BackBufferFormat)
            Next
        End Sub

        Private Sub LogOutputDisplayModes(ByVal output As Output, ByVal format As Format)
            For Each displayMode As ModeDescription In output.GetDisplayModeList(format, 0)
                Debug.WriteLine($"Width = {displayMode.Width} Height = {displayMode.Height} Refresh = {displayMode.RefreshRate}")
            Next
        End Sub

        Private Sub CalculateFrameRateStats()
            _frameCount += 1

            If Timer.TotalTime - _timeElapsed >= 1.0F Then
                Dim fps As Single = _frameCount
                Dim mspf As Single = 1000.0F / fps
                _window.Text = $"{MainWindowCaption}    fps: {fps}   mspf: {mspf}"
                _frameCount = 0
                _timeElapsed += 1.0F
            End If
        End Sub

    End Class


End Namespace
