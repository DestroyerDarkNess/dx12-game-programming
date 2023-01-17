Imports System
Imports System.IO
Imports System.Linq
Imports System.Runtime.InteropServices
Imports SharpDX
Imports SharpDX.D3DCompiler
Imports SharpDX.Direct3D
Imports SharpDX.Direct3D12
Imports Device = SharpDX.Direct3D12.Device
Imports Resource = SharpDX.Direct3D12.Resource
Imports ShaderBytecode = SharpDX.Direct3D12.ShaderBytecode
Imports System.Runtime.CompilerServices

Namespace DX12GameProgramming
    Public Class D3DUtil
        Public Const DefaultShader4ComponentMapping As Integer = 5768

        Shared Function CreateDefaultBuffer(Of T As Structure)(ByVal device As Device, ByVal cmdList As GraphicsCommandList, ByVal initData As T(), ByVal byteSize As Long, <Out> ByRef uploadBuffer As Resource) As Resource
            Dim defaultBuffer As Resource = device.CreateCommittedResource(New HeapProperties(HeapType.[Default]), HeapFlags.None, ResourceDescription.Buffer(byteSize), ResourceStates.Common)
            uploadBuffer = device.CreateCommittedResource(New HeapProperties(HeapType.Upload), HeapFlags.None, ResourceDescription.Buffer(byteSize), ResourceStates.GenericRead)
            Dim ptr As IntPtr = uploadBuffer.Map(0)
            Utilities.Write(ptr, initData, 0, initData.Length)
            uploadBuffer.Unmap(0)
            cmdList.ResourceBarrierTransition(defaultBuffer, ResourceStates.Common, ResourceStates.CopyDestination)
            cmdList.CopyResource(defaultBuffer, uploadBuffer)
            cmdList.ResourceBarrierTransition(defaultBuffer, ResourceStates.CopyDestination, ResourceStates.GenericRead)
            Return defaultBuffer
        End Function

        Shared Function CalcConstantBufferByteSize(Of T As Structure)() As Integer
            Return (Marshal.SizeOf(GetType(T)) + 255) And Not 255
        End Function

        Shared Function CompileShader(ByVal fileName As String, ByVal entryPoint As String, ByVal profile As String, ByVal Optional defines As ShaderMacro() = Nothing) As ShaderBytecode
            Dim shaderFlags As D3DCompiler.ShaderFlags = D3DCompiler.ShaderFlags.None
            Dim result As CompilationResult = SharpDX.D3DCompiler.ShaderBytecode.CompileFromFile(fileName, entryPoint, profile, shaderFlags, include:=FileIncludeHandler.[Default], defines:=defines)
            Return New ShaderBytecode(result)
        End Function
    End Class

    <StructLayout(LayoutKind.Sequential, Pack:=4)>
    Public Structure Light
        Public Const MaxLights As Integer = 16
        Public Strength As Vector3
        Public FalloffStart As Single
        Public Direction As Vector3
        Public FalloffEnd As Single
        Public Position As Vector3
        Public SpotPower As Single

        Public Shared ReadOnly Property [Default] As Light
            Get
                Return New Light With {
                    .Strength = New Vector3(0.5F),
                    .FalloffStart = 1.0F,
                    .Direction = -Vector3.UnitY,
                    .FalloffEnd = 10.0F,
                    .Position = Vector3.Zero,
                    .SpotPower = 64.0F
                }
            End Get
        End Property

        Public Shared ReadOnly Property DefaultArray As Light()
            Get
                Return Enumerable.Repeat([Default], MaxLights).ToArray()
            End Get
        End Property
    End Structure

    <StructLayout(LayoutKind.Sequential, Pack:=4)>
    Public Structure MaterialConstants
        Public DiffuseAlbedo As Vector4
        Public FresnelR0 As Vector3
        Public Roughness As Single
        Public MatTransform As Matrix

        Public Shared ReadOnly Property [Default] As MaterialConstants
            Get
                Return New MaterialConstants With {
                    .DiffuseAlbedo = Vector4.One,
                    .FresnelR0 = New Vector3(0.01F),
                    .Roughness = 0.25F,
                    .MatTransform = Matrix.Identity
                }
            End Get
        End Property
    End Structure

    Public Class Material
        Public Property Name As String
        Public Property MatCBIndex As Integer = -1
        Public Property DiffuseSrvHeapIndex As Integer = -1
        Public Property NormalSrvHeapIndex As Integer = -1
        Public Property NumFramesDirty As Integer = D3DApp.NumFrameResources
        Public Property DiffuseAlbedo As Vector4 = Vector4.One
        Public Property FresnelR0 As Vector3 = New Vector3(0.01F)
        Public Property Roughness As Single = 0.25F
        Public Property MatTransform As Matrix = Matrix.Identity
    End Class

    Public Class Texture
        Implements IDisposable

        Public Property Name As String
        Public Property Filename As String
        Public Property Resource As Resource
        Public Property UploadHeap As Resource

        Public Sub Dispose() Implements IDisposable.Dispose
            Resource?.Dispose()
            UploadHeap?.Dispose()
        End Sub
    End Class

    Module D3DExtensions
        <Extension()>
        Function Copy(ByVal desc As GraphicsPipelineStateDescription) As GraphicsPipelineStateDescription
            Dim newDesc = New GraphicsPipelineStateDescription With {
                .BlendState = desc.BlendState,
                .CachedPSO = desc.CachedPSO,
                .DepthStencilFormat = desc.DepthStencilFormat,
                .DepthStencilState = desc.DepthStencilState,
                .SampleDescription = desc.SampleDescription,
                .DomainShader = desc.DomainShader,
                .Flags = desc.Flags,
                .GeometryShader = desc.GeometryShader,
                .HullShader = desc.HullShader,
                .IBStripCutValue = desc.IBStripCutValue,
                .InputLayout = desc.InputLayout,
                .NodeMask = desc.NodeMask,
                .PixelShader = desc.PixelShader,
                .PrimitiveTopologyType = desc.PrimitiveTopologyType,
                .RasterizerState = desc.RasterizerState,
                .RenderTargetCount = desc.RenderTargetCount,
                .SampleMask = desc.SampleMask,
                .StreamOutput = desc.StreamOutput,
                .VertexShader = desc.VertexShader,
                .RootSignature = desc.RootSignature
            }

            For i As Integer = 0 To desc.RenderTargetFormats.Length - 1
                newDesc.RenderTargetFormats(i) = desc.RenderTargetFormats(i)
            Next

            Return newDesc
        End Function
    End Module

    Friend Class FileIncludeHandler
        Inherits CallbackBase
        Implements Include

        Public Shared ReadOnly Property [Default] As FileIncludeHandler = New FileIncludeHandler()

        Public Function Open(ByVal type As IncludeType, ByVal fileName As String, ByVal parentStream As Stream) As Stream Implements Include.Open
            Dim filePath As String = fileName

            If Not Path.IsPathRooted(filePath) Then
                Dim selectedFile As String = Path.Combine(Environment.CurrentDirectory, fileName)
                If File.Exists(selectedFile) Then filePath = selectedFile
            End If

            Return New FileStream(filePath, FileMode.Open, FileAccess.Read)
        End Function

        '  Public Sub Close(ByVal stream As Stream)
        '    stream.Close()
        ' End Sub

        ' Private Function Open(type As IncludeType, fileName As String, parentStream As Stream) As Stream Implements Include.Open
        '     Return DirectCast([Default], Include).Open(type, fileName, parentStream)
        ' End Function

        Private Sub Close(stream As Stream) Implements Include.Close
            stream.Close()
        End Sub

    End Class

    <Flags>
    Public Enum MouseButtons
        Left = 1048576
        None = 0
        Right = 2097152
        Middle = 4194304
        XButton1 = 8388608
        XButton2 = 16777216
    End Enum

    Public Enum Keys
        KeyCode = 65535
        Modifiers = -65536
        None = 0
        LButton = 1
        RButton = 2
        Cancel = 3
        MButton = 4
        XButton1 = 5
        XButton2 = 6
        Back = 8
        Tab = 9
        LineFeed = 10
        Clear = 12
        [Return] = 13
        Enter = 13
        ShiftKey = 16
        ControlKey = 17
        Menu = 18
        Pause = 19
        Capital = 20
        CapsLock = 20
        KanaMode = 21
        HanguelMode = 21
        HangulMode = 21
        JunjaMode = 23
        FinalMode = 24
        HanjaMode = 25
        KanjiMode = 25
        Escape = 27
        IMEConvert = 28
        IMENonconvert = 29
        IMEAccept = 30
        IMEAceept = 30
        IMEModeChange = 31
        Space = 32
        Prior = 33
        PageUp = 33
        [Next] = 34
        PageDown = 34
        [End] = 35
        Home = 36
        Left = 37
        Up = 38
        Right = 39
        Down = 40
        [Select] = 41
        Print = 42
        Execute = 43
        Snapshot = 44
        PrintScreen = 44
        Insert = 45
        Delete = 46
        Help = 47
        D0 = 48
        D1 = 49
        D2 = 50
        D3 = 51
        D4 = 52
        D5 = 53
        D6 = 54
        D7 = 55
        D8 = 56
        D9 = 57
        A = 65
        B = 66
        C = 67
        D = 68
        E = 69
        F = 70
        G = 71
        H = 72
        I = 73
        J = 74
        K = 75
        L = 76
        M = 77
        N = 78
        O = 79
        P = 80
        Q = 81
        R = 82
        S = 83
        T = 84
        U = 85
        V = 86
        W = 87
        X = 88
        Y = 89
        Z = 90
        LWin = 91
        RWin = 92
        Apps = 93
        Sleep = 95
        NumPad0 = 96
        NumPad1 = 97
        NumPad2 = 98
        NumPad3 = 99
        NumPad4 = 100
        NumPad5 = 101
        NumPad6 = 102
        NumPad7 = 103
        NumPad8 = 104
        NumPad9 = 105
        Multiply = 106
        Add = 107
        Separator = 108
        Subtract = 109
        DecimalEx = 110
        Divide = 111
        F1 = 112
        F2 = 113
        F3 = 114
        F4 = 115
        F5 = 116
        F6 = 117
        F7 = 118
        F8 = 119
        F9 = 120
        F10 = 121
        F11 = 122
        F12 = 123
        F13 = 124
        F14 = 125
        F15 = 126
        F16 = 127
        F17 = 128
        F18 = 129
        F19 = 130
        F20 = 131
        F21 = 132
        F22 = 133
        F23 = 134
        F24 = 135
        NumLock = 144
        Scroll = 145
        LShiftKey = 160
        RShiftKey = 161
        LControlKey = 162
        RControlKey = 163
        LMenu = 164
        RMenu = 165
        BrowserBack = 166
        BrowserForward = 167
        BrowserRefresh = 168
        BrowserStop = 169
        BrowserSearch = 170
        BrowserFavorites = 171
        BrowserHome = 172
        VolumeMute = 173
        VolumeDown = 174
        VolumeUp = 175
        MediaNextTrack = 176
        MediaPreviousTrack = 177
        MediaStop = 178
        MediaPlayPause = 179
        LaunchMail = 180
        SelectMedia = 181
        LaunchApplication1 = 182
        LaunchApplication2 = 183
        OemSemicolon = 186
        Oem1 = 186
        Oemplus = 187
        Oemcomma = 188
        OemMinus = 189
        OemPeriod = 190
        OemQuestion = 191
        Oem2 = 191
        Oemtilde = 192
        Oem3 = 192
        OemOpenBrackets = 219
        Oem4 = 219
        OemPipe = 220
        Oem5 = 220
        OemCloseBrackets = 221
        Oem6 = 221
        OemQuotes = 222
        Oem7 = 222
        Oem8 = 223
        OemBackslash = 226
        Oem102 = 226
        ProcessKey = 229
        Packet = 231
        Attn = 246
        Crsel = 247
        Exsel = 248
        EraseEof = 249
        Play = 250
        Zoom = 251
        NoName = 252
        Pa1 = 253
        OemClear = 254
        Shift = 65536
        Control = 131072
        Alt = 262144
    End Enum
End Namespace
