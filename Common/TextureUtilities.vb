Imports System
Imports System.Linq
Imports System.Runtime.InteropServices
Imports SharpDX.Direct3D12
Imports SharpDX.DXGI
Imports Resource = SharpDX.Direct3D12.Resource
Imports Device = SharpDX.Direct3D12.Device

Namespace DX12GameProgramming
    Public Class TextureUtilities
        Const DDS_MAGIC As Integer = &H20534444

        <StructLayout(LayoutKind.Sequential, Pack:=4)>
        Structure DDS_PIXELFORMAT
            Public size As Integer
            Public flags As Integer
            Public fourCC As Integer
            Public RGBBitCount As Integer
            Public RBitMask As UInteger
            Public GBitMask As UInteger
            Public BBitMask As UInteger
            Public ABitMask As UInteger
        End Structure

        Const DDS_FOURCC As Integer = &H4
        Const DDS_RGB As Integer = &H40
        Const DDS_RGBA As Integer = &H41
        Const DDS_LUMINANCE As Integer = &H20000
        Const DDS_LUMINANCEA As Integer = &H20001
        Const DDS_ALPHA As Integer = &H2
        Const DDS_PAL8 As Integer = &H20
        Const DDS_HEADER_FLAGS_TEXTURE As Integer = &H1007
        Const DDS_HEADER_FLAGS_MIPMAP As Integer = &H20000
        Const DDS_HEADER_FLAGS_VOLUME As Integer = &H800000
        Const DDS_HEADER_FLAGS_PITCH As Integer = &H8
        Const DDS_HEADER_FLAGS_LINEARSIZE As Integer = &H80000
        Const DDS_HEIGHT As Integer = &H2
        Const DDS_WIDTH As Integer = &H4
        Const DDS_SURFACE_FLAGS_TEXTURE As Integer = &H1000
        Const DDS_SURFACE_FLAGS_MIPMAP As Integer = &H400008
        Const DDS_SURFACE_FLAGS_CUBEMAP As Integer = &H8
        Const DDS_CUBEMAP_POSITIVEX As Integer = &H600
        Const DDS_CUBEMAP_NEGATIVEX As Integer = &HA00
        Const DDS_CUBEMAP_POSITIVEY As Integer = &H1200
        Const DDS_CUBEMAP_NEGATIVEY As Integer = &H2200
        Const DDS_CUBEMAP_POSITIVEZ As Integer = &H4200
        Const DDS_CUBEMAP_NEGATIVEZ As Integer = &H8200
        Const DDS_CUBEMAP_ALLFACES As Integer = (DDS_CUBEMAP_POSITIVEX Or DDS_CUBEMAP_NEGATIVEX Or DDS_CUBEMAP_POSITIVEY Or DDS_CUBEMAP_NEGATIVEY Or DDS_CUBEMAP_POSITIVEZ Or DDS_CUBEMAP_NEGATIVEZ)
        Const DDS_CUBEMAP As Integer = &H200
        Const DDS_FLAGS_VOLUME As Integer = &H200000

        <StructLayout(LayoutKind.Sequential, Pack:=4)>
        Structure DDS_HEADER
            Public size As Integer
            Public flags As Integer
            Public height As Integer
            Public width As Integer
            Public pitchOrLinearSize As Integer
            Public depth As Integer
            Public mipMapCount As Integer
            <MarshalAs(UnmanagedType.ByValArray, SizeConst:=11)>
            Public reserved1 As Integer()
            Public ddspf As DDS_PIXELFORMAT
            Public caps As Integer
            Public caps2 As Integer
            Public caps3 As Integer
            Public caps4 As Integer
            Public reserved2 As Integer
        End Structure

        <StructLayout(LayoutKind.Sequential, Pack:=4)>
        Structure DDS_HEADER_DXT10
            Public dxgiFormat As Format
            Public resourceDimension As Integer
            Public miscFlag As Integer
            Public arraySize As Integer
            Public reserved As Integer
        End Structure

        Private Shared Function BitsPerPixel(ByVal fmt As Format) As Integer
            Select Case fmt
                Case Format.R32G32B32A32_Typeless, Format.R32G32B32A32_Float, Format.R32G32B32A32_UInt, Format.R32G32B32A32_SInt
                    Return 128
                Case Format.R32G32B32_Typeless, Format.R32G32B32_Float, Format.R32G32B32_UInt, Format.R32G32B32_SInt
                    Return 96
                Case Format.R16G16B16A16_Typeless, Format.R16G16B16A16_Float, Format.R16G16B16A16_UNorm, Format.R16G16B16A16_UInt, Format.R16G16B16A16_SNorm, Format.R16G16B16A16_SInt, Format.R32G32_Typeless, Format.R32G32_Float, Format.R32G32_UInt, Format.R32G32_SInt, Format.R32G8X24_Typeless, Format.D32_Float_S8X24_UInt, Format.R32_Float_X8X24_Typeless, Format.X32_Typeless_G8X24_UInt
                    Return 64
                Case Format.R10G10B10A2_Typeless, Format.R10G10B10A2_UNorm, Format.R10G10B10A2_UInt, Format.R11G11B10_Float, Format.R8G8B8A8_Typeless, Format.R8G8B8A8_UNorm, Format.R8G8B8A8_UNorm_SRgb, Format.R8G8B8A8_UInt, Format.R8G8B8A8_SNorm, Format.R8G8B8A8_SInt, Format.R16G16_Typeless, Format.R16G16_Float, Format.R16G16_UNorm, Format.R16G16_UInt, Format.R16G16_SNorm, Format.R16G16_SInt, Format.R32_Typeless, Format.D32_Float, Format.R32_Float, Format.R32_UInt, Format.R32_SInt, Format.R24G8_Typeless, Format.D24_UNorm_S8_UInt, Format.R24_UNorm_X8_Typeless, Format.X24_Typeless_G8_UInt, Format.R9G9B9E5_Sharedexp, Format.R8G8_B8G8_UNorm, Format.G8R8_G8B8_UNorm, Format.B8G8R8A8_UNorm, Format.B8G8R8X8_UNorm, Format.R10G10B10_Xr_Bias_A2_UNorm, Format.B8G8R8A8_Typeless, Format.B8G8R8A8_UNorm_SRgb, Format.B8G8R8X8_Typeless, Format.B8G8R8X8_UNorm_SRgb
                    Return 32
                Case Format.R8G8_Typeless, Format.R8G8_UNorm, Format.R8G8_UInt, Format.R8G8_SNorm, Format.R8G8_SInt, Format.R16_Typeless, Format.R16_Float, Format.D16_UNorm, Format.R16_UNorm, Format.R16_UInt, Format.R16_SNorm, Format.R16_SInt, Format.B5G6R5_UNorm, Format.B5G5R5A1_UNorm, Format.B4G4R4A4_UNorm
                    Return 16
                Case Format.R8_Typeless, Format.R8_UNorm, Format.R8_UInt, Format.R8_SNorm, Format.R8_SInt, Format.A8_UNorm
                    Return 8
                Case Format.R1_UNorm
                    Return 1
                Case Format.BC1_Typeless, Format.BC1_UNorm, Format.BC1_UNorm_SRgb, Format.BC4_Typeless, Format.BC4_UNorm, Format.BC4_SNorm
                    Return 4
                Case Format.BC2_Typeless, Format.BC2_UNorm, Format.BC2_UNorm_SRgb, Format.BC3_Typeless, Format.BC3_UNorm, Format.BC3_UNorm_SRgb, Format.BC5_Typeless, Format.BC5_UNorm, Format.BC5_SNorm, Format.BC6H_Typeless, Format.BC6H_Uf16, Format.BC6H_Sf16, Format.BC7_Typeless, Format.BC7_UNorm, Format.BC7_UNorm_SRgb
                    Return 8
                Case Else
                    Return 0
            End Select
        End Function

        Private Shared Sub GetSurfaceInfo(ByVal width As Integer, ByVal height As Integer, ByVal fmt As Format, <Out> ByRef outNumBytes As Integer, <Out> ByRef outRowBytes As Integer, <Out> ByRef outNumRows As Integer)
            Dim numBytes As Integer = 0
            Dim rowBytes As Integer = 0
            Dim numRows As Integer = 0
            Dim bc As Boolean = False
            Dim packed As Boolean = False
            Dim bcnumBytesPerBlock As Integer = 0

            Select Case fmt
                Case Format.BC1_Typeless, Format.BC1_UNorm, Format.BC1_UNorm_SRgb, Format.BC4_Typeless, Format.BC4_UNorm, Format.BC4_SNorm
                    bc = True
                    bcnumBytesPerBlock = 8
                Case Format.BC2_Typeless, Format.BC2_UNorm, Format.BC2_UNorm_SRgb, Format.BC3_Typeless, Format.BC3_UNorm, Format.BC3_UNorm_SRgb, Format.BC5_Typeless, Format.BC5_UNorm, Format.BC5_SNorm, Format.BC6H_Typeless, Format.BC6H_Uf16, Format.BC6H_Sf16, Format.BC7_Typeless, Format.BC7_UNorm, Format.BC7_UNorm_SRgb
                    bc = True
                    bcnumBytesPerBlock = 16
                Case Format.R8G8_B8G8_UNorm, Format.G8R8_G8B8_UNorm
                    packed = True
            End Select

            If bc Then
                Dim numBlocksWide As Integer = 0

                If width > 0 Then
                    numBlocksWide = Math.Max(1, (width + 3) / 4)
                End If

                Dim numBlocksHigh As Integer = 0

                If height > 0 Then
                    numBlocksHigh = Math.Max(1, (height + 3) / 4)
                End If

                rowBytes = numBlocksWide * bcnumBytesPerBlock
                numRows = numBlocksHigh
            ElseIf packed Then
                rowBytes = ((width + 1) >> 1) * 4

                numRows = height
            Else
                Dim bpp As Integer = BitsPerPixel(fmt)
                rowBytes = (width * bpp + 7) / 8
                numRows = height
            End If

            numBytes = rowBytes * numRows
            outNumBytes = numBytes
            outRowBytes = rowBytes
            outNumRows = numRows
        End Sub

        Private Shared Function ISBITMASK(ByVal ddpf As DDS_PIXELFORMAT, ByVal r As Integer, ByVal g As Integer, ByVal b As Integer, ByVal a As Integer) As Boolean
            Return (ddpf.RBitMask = r AndAlso ddpf.GBitMask = g AndAlso ddpf.BBitMask = b AndAlso ddpf.ABitMask = a)
        End Function

        Private Shared Function MAKEFOURCC(ByVal ch0 As Integer, ByVal ch1 As Integer, ByVal ch2 As Integer, ByVal ch3 As Integer) As Integer
            Return (CInt(CByte(Math.Truncate(ch0))) Or (CInt(CByte(Math.Truncate(ch1))) << 8) Or (CInt(CByte(Math.Truncate(ch2))) << 16) Or (CInt(CByte(Math.Truncate(ch3))) << 24))
        End Function

        Private Shared Function GetDXGIFormat(ByVal ddpf As DDS_PIXELFORMAT) As Format
            If (ddpf.flags And DDS_RGB) > 0 Then

                Select Case ddpf.RGBBitCount
                    Case 32

                        If ISBITMASK(ddpf, &HFF, &HFF00, &HFF0000, &HFF000000) Then
                            Return Format.R8G8B8A8_UNorm
                        End If

                        If ISBITMASK(ddpf, &HFF0000, &HFF00, &HFF, &HFF000000) Then
                            Return Format.B8G8R8A8_UNorm
                        End If

                        If ISBITMASK(ddpf, &HFF0000, &HFF00, &HFF, &H0) Then
                            Return Format.B8G8R8X8_UNorm
                        End If

                        If ISBITMASK(ddpf, &H3FF00000, &HFFC00, &H3FF, &HC0000000) Then
                            Return Format.R10G10B10A2_UNorm
                        End If

                        If ISBITMASK(ddpf, &HFFFF, &HFFFF0000, &H0, &H0) Then
                            Return Format.R16G16_UNorm
                        End If

                        If ISBITMASK(ddpf, &HFFFFFFFF, &H0, &H0, &H0) Then
                            Return Format.R32_Float
                        End If

                    Case 24
                    Case 16

                        If ISBITMASK(ddpf, &H7C00, &H3E0, &H1F, &H8000) Then
                            Return Format.B5G5R5A1_UNorm
                        End If

                        If ISBITMASK(ddpf, &HF800, &H7E0, &H1F, &H0) Then
                            Return Format.B5G6R5_UNorm
                        End If

                        If ISBITMASK(ddpf, &HF00, &HF0, &HF, &HF000) Then
                            Return Format.B4G4R4A4_UNorm
                        End If
                End Select
            ElseIf (ddpf.flags And DDS_LUMINANCE) > 0 Then

                If 8 = ddpf.RGBBitCount Then

                    If ISBITMASK(ddpf, &HFF, &H0, &H0, &H0) Then
                        Return Format.R8_UNorm
                    End If
                End If

                If 16 = ddpf.RGBBitCount Then

                    If ISBITMASK(ddpf, &HFFFF, &H0, &H0, &H0) Then
                        Return Format.R16_UNorm
                    End If

                    If ISBITMASK(ddpf, &HFF, &H0, &H0, &HFF00) Then
                        Return Format.R8G8_UNorm
                    End If
                End If
            ElseIf (ddpf.flags And DDS_ALPHA) > 0 Then

                If 8 = ddpf.RGBBitCount Then
                    Return Format.A8_UNorm
                End If
            ElseIf (ddpf.flags And DDS_FOURCC) > 0 Then

                If MAKEFOURCC("D", "X", "T", "1") = ddpf.fourCC Then
                    Return Format.BC1_UNorm
                End If

                If MAKEFOURCC("D", "X", "T", "3") = ddpf.fourCC Then
                    Return Format.BC2_UNorm
                End If

                If MAKEFOURCC("D", "X", "T", "5") = ddpf.fourCC Then
                    Return Format.BC3_UNorm
                End If

                If MAKEFOURCC("D", "X", "T", "2") = ddpf.fourCC Then
                    Return Format.BC2_UNorm
                End If

                If MAKEFOURCC("D", "X", "T", "4") = ddpf.fourCC Then
                    Return Format.BC3_UNorm
                End If

                If MAKEFOURCC("A", "T", "I", "1") = ddpf.fourCC Then
                    Return Format.BC4_UNorm
                End If

                If MAKEFOURCC("B", "C", "4", "U") = ddpf.fourCC Then
                    Return Format.BC4_UNorm
                End If

                If MAKEFOURCC("B", "C", "4", "S") = ddpf.fourCC Then
                    Return Format.BC4_SNorm
                End If

                If MAKEFOURCC("A", "T", "I", "2") = ddpf.fourCC Then
                    Return Format.BC5_UNorm
                End If

                If MAKEFOURCC("B", "C", "5", "U") = ddpf.fourCC Then
                    Return Format.BC5_UNorm
                End If

                If MAKEFOURCC("B", "C", "5", "S") = ddpf.fourCC Then
                    Return Format.BC5_SNorm
                End If

                If MAKEFOURCC("R", "G", "B", "G") = ddpf.fourCC Then
                    Return Format.R8G8_B8G8_UNorm
                End If

                If MAKEFOURCC("G", "R", "G", "B") = ddpf.fourCC Then
                    Return Format.G8R8_G8B8_UNorm
                End If

                Select Case ddpf.fourCC
                    Case 36
                        Return Format.R16G16B16A16_UNorm
                    Case 110
                        Return Format.R16G16B16A16_SNorm
                    Case 111
                        Return Format.R16_Float
                    Case 112
                        Return Format.R16G16_Float
                    Case 113
                        Return Format.R16G16B16A16_Float
                    Case 114
                        Return Format.R32_Float
                    Case 115
                        Return Format.R32G32_Float
                    Case 116
                        Return Format.R32G32B32A32_Float
                End Select
            End If

            Return Format.Unknown
        End Function

        Private Shared Function ByteArrayToStructure(Of T As Structure)(ByVal bytes As Byte(), ByVal start As Integer, ByVal count As Integer) As T
            Dim temp As Byte() = bytes.Skip(start).Take(count).ToArray()
            Dim handle As GCHandle = GCHandle.Alloc(temp, GCHandleType.Pinned)
            Dim stuff As T = CType(Marshal.PtrToStructure(handle.AddrOfPinnedObject(), GetType(T)), T)
            handle.Free()
            Return stuff
        End Function

        Private Shared Sub FillInitData(ByVal texture As Resource, ByVal width As Integer, ByVal height As Integer, ByVal depth As Integer, ByVal mipCount As Integer, ByVal arraySize As Integer, ByVal format As Format, ByVal maxsize As Integer, ByVal bitSize As Integer, ByVal bitData As Byte(), ByVal offset As Integer)
            Dim NumBytes As Integer = 0
            Dim RowBytes As Integer = 0
            Dim NumRows As Integer = 0
            Dim pSrcBits As Byte() = bitData
            Dim pEndBits As Byte() = bitData
            Dim index As Integer = 0
            Dim k As Integer = offset

            For j As Integer = 0 To arraySize - 1
                Dim w As Integer = width
                Dim h As Integer = height
                Dim d As Integer = depth

                For i As Integer = 0 To mipCount - 1
                    GetSurfaceInfo(w, h, format, NumBytes, RowBytes, NumRows)
                    Dim handle As GCHandle = GCHandle.Alloc(bitData, GCHandleType.Pinned)
                    Dim ptr As IntPtr = Marshal.UnsafeAddrOfPinnedArrayElement(bitData, k)
                    texture.WriteToSubresource(index, Nothing, ptr, RowBytes, NumBytes)
                    handle.Free()
                    index += 1
                    k += NumBytes * d

                    w = w >> 1
                    h = h >> 1
                    d = d >> 1

                    If w = 0 Then
                        w = 1
                    End If

                    If h = 0 Then
                        h = 1
                    End If

                    If d = 0 Then
                        d = 1
                    End If
                Next
            Next
        End Sub

        Private Shared Function CreateTextureFromDDS(ByVal d3dDevice As Device, ByVal header As DDS_HEADER, ByVal header10 As DDS_HEADER_DXT10?, ByVal bitData As Byte(), ByVal offset As Integer, ByVal maxsize As Integer, <Out> ByRef isCubeMap As Boolean) As Resource
            Dim width As Integer = header.width
            Dim height As Integer = header.height
            Dim depth As Integer = header.depth
            Dim resDim As ResourceDimension = ResourceDimension.Unknown
            Dim arraySize As Integer = 1
            Dim format As Format = Format.Unknown
            isCubeMap = False
            Dim mipCount As Integer = header.mipMapCount

            If 0 = mipCount Then
                mipCount = 1
            End If

            If ((header.ddspf.flags And DDS_FOURCC) > 0) AndAlso (MAKEFOURCC("D", "X", "1", "0") = header.ddspf.fourCC) Then
                Dim d3d10ext As DDS_HEADER_DXT10 = header10.Value
                arraySize = d3d10ext.arraySize

                If arraySize = 0 Then
                    Throw New Exception()
                End If

                If BitsPerPixel(d3d10ext.dxgiFormat) = 0 Then
                    Throw New Exception()
                End If

                format = d3d10ext.dxgiFormat

                Select Case CType(d3d10ext.resourceDimension, ResourceDimension)
                    Case ResourceDimension.Texture1D

                        If (header.flags And DDS_HEIGHT) > 0 AndAlso height <> 1 Then
                            Throw New Exception()
                        End If

                        depth = 1
                        height = depth
                    Case ResourceDimension.Texture2D

                        If (d3d10ext.miscFlag And &H4) > 0 Then
                            arraySize *= 6
                            isCubeMap = True
                        End If

                        depth = 1
                    Case ResourceDimension.Texture3D

                        If Not ((header.flags And DDS_HEADER_FLAGS_VOLUME) > 0) Then
                            Throw New Exception()
                        End If

                        If arraySize > 1 Then
                            Throw New Exception()
                        End If

                    Case Else
                        Throw New Exception()
                End Select

                resDim = CType(d3d10ext.resourceDimension, ResourceDimension)
            Else
                format = GetDXGIFormat(header.ddspf)

                If format = Format.Unknown Then
                    Throw New Exception()
                End If

                If (header.flags And DDS_HEADER_FLAGS_VOLUME) > 0 Then
                    resDim = ResourceDimension.Texture3D
                Else

                    If (header.caps2 And DDS_CUBEMAP) > 0 Then

                        If (header.caps2 And DDS_CUBEMAP_ALLFACES) <> DDS_CUBEMAP_ALLFACES Then
                            Throw New Exception()
                        End If

                        arraySize = 6
                        isCubeMap = True
                    End If

                    depth = 1
                    resDim = ResourceDimension.Texture2D
                End If
            End If

            Dim resource = d3dDevice.CreateCommittedResource(New HeapProperties(CpuPageProperty.WriteBack, MemoryPool.L0), HeapFlags.None, New ResourceDescription() With {
                .Dimension = resDim,
                .DepthOrArraySize = CShort(arraySize),
                .Flags = ResourceFlags.None,
                .Format = format,
                .Height = height,
                .Layout = TextureLayout.Unknown,
                .MipLevels = CShort(mipCount),
                .SampleDescription = New SampleDescription(1, 0),
                .Width = width
            }, ResourceStates.GenericRead)
            FillInitData(resource, width, height, depth, mipCount, arraySize, format, 0, 0, bitData, offset)
            Return resource
        End Function

        Public Shared Function CreateTextureFromDDS(ByVal device As Device, ByVal data As Byte(), <Out> ByRef isCubeMap As Boolean) As Resource
            Dim header As DDS_HEADER = New DDS_HEADER()
            Dim ddsHeaderSize As Integer = Marshal.SizeOf(header)
            Dim ddspfSize As Integer = Marshal.SizeOf(New DDS_PIXELFORMAT())
            Dim ddsHeader10Size As Integer = Marshal.SizeOf(New DDS_HEADER_DXT10())

            If (data.Length < (Len(New UInteger()) + ddsHeaderSize)) Then
                Throw New Exception()
            End If

            Dim dwMagicNumber As Integer = BitConverter.ToInt32(data, 0)

                If dwMagicNumber <> DDS_MAGIC Then
                    Throw New Exception()
                End If

                header = ByteArrayToStructure(Of DDS_HEADER)(data, 4, ddsHeaderSize)

                If header.size <> ddsHeaderSize OrElse header.ddspf.size <> ddspfSize Then
                    Throw New Exception()
                End If

                Dim dx10Header As DDS_HEADER_DXT10? = Nothing

                If ((header.ddspf.flags And DDS_FOURCC) > 0) AndAlso (MAKEFOURCC("D", "X", "1", "0") = header.ddspf.fourCC) Then

                    If data.Length < (ddsHeaderSize + 4 + ddsHeader10Size) Then
                        Throw New Exception()
                    End If

                    dx10Header = ByteArrayToStructure(Of DDS_HEADER_DXT10)(data, 4 + ddsHeaderSize, ddsHeader10Size)
                End If

                Dim offset As Integer = 4 + ddsHeaderSize + (If(dx10Header.HasValue, ddsHeader10Size, 0))
                Return CreateTextureFromDDS(device, header, dx10Header, data, offset, 0, isCubeMap)
        End Function

        Public Shared Function CreateTextureFromDDS(ByVal device As Device, ByVal filename As String) As Resource
            Dim isCube As Boolean
            Return CreateTextureFromDDS(device, System.IO.File.ReadAllBytes(filename), isCube)
        End Function

        Public Shared Function CreateTextureFromBitmap(ByVal device As Device, ByVal filename As String) As Resource
            Dim bitmap As System.Drawing.Bitmap = New System.Drawing.Bitmap(filename)
            Dim width As Integer = bitmap.Width
            Dim height As Integer = bitmap.Height
            Dim textureDesc As ResourceDescription = New ResourceDescription() With {
                .MipLevels = 1,
                .Format = Format.B8G8R8A8_UNorm,
                .Width = width,
                .Height = height,
                .Flags = ResourceFlags.None,
                .DepthOrArraySize = 1,
                .SampleDescription = New SampleDescription(1, 0),
                .Dimension = ResourceDimension.Texture2D
            }
            Dim buffer = device.CreateCommittedResource(New HeapProperties(HeapType.Upload), HeapFlags.None, textureDesc, ResourceStates.GenericRead)
            Dim data As System.Drawing.Imaging.BitmapData = bitmap.LockBits(New System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.[ReadOnly], System.Drawing.Imaging.PixelFormat.Format32bppArgb)
            buffer.WriteToSubresource(0, New ResourceRegion() With {
                .Back = 1,
                .Bottom = height,
                .Right = width
            }, data.Scan0, 4 * width, 4 * width * height)
            Dim bufferSize As Integer = data.Height * data.Stride
            bitmap.UnlockBits(data)
            Return buffer
        End Function


    End Class
End Namespace
