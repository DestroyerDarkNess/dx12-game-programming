Imports Common.DX12GameProgramming
Imports SharpDX
Imports SharpDX.Direct3D12
Imports SharpDX.DXGI

Namespace DX12GameProgramming
    Public Class InitDirect3DApp
        Inherits D3DApp

        Public Sub New()
            MainWindowCaption = "Init Direct3D"
        End Sub

        Protected Overrides Sub Draw(ByVal gt As GameTimer)
            DirectCmdListAlloc.Reset()
            CommandList.Reset(DirectCmdListAlloc, Nothing)
            CommandList.ResourceBarrierTransition(CurrentBackBuffer, ResourceStates.Present, ResourceStates.RenderTarget)
            CommandList.SetViewport(Viewport)
            CommandList.SetScissorRectangles(ScissorRectangle)
            CommandList.ClearRenderTargetView(CurrentBackBufferView, Color.LightSteelBlue)
            CommandList.ClearDepthStencilView(DepthStencilView, ClearFlags.FlagsDepth Or ClearFlags.FlagsStencil, 1.0F, 0)
            CommandList.SetRenderTargets(CurrentBackBufferView, DepthStencilView)
            CommandList.ResourceBarrierTransition(CurrentBackBuffer, ResourceStates.RenderTarget, ResourceStates.Present)
            CommandList.Close()
            CommandQueue.ExecuteCommandList(CommandList)
            SwapChain.Present(0, PresentFlags.None)
            FlushCommandQueue()
        End Sub

    End Class
End Namespace
