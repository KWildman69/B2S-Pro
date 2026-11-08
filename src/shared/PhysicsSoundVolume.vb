Imports System

#If PHYSICS_PREVIEW Then
Namespace PhysicsPreview
#End If
Public Module PhysicsSoundVolume
    Public Function Apply(ByVal waveData As Byte(), ByVal volume As Single) As Byte()
        If waveData Is Nothing Then Return Nothing
        Dim adjusted As Byte() = CType(waveData.Clone(), Byte())
        Dim gain As Double = Math.Max(0.0R, Math.Min(2.0R, CDbl(volume)))
        If Math.Abs(gain - 1.0R) < 0.000001R Then Return adjusted
        If adjusted.Length < 12 OrElse ReadAscii(adjusted, 0, 4) <> "RIFF" OrElse ReadAscii(adjusted, 8, 4) <> "WAVE" Then Return adjusted

        Dim formatTag As Integer = -1
        Dim bitsPerSample As Integer = 0
        Dim offset As Integer = 12
        While offset <= adjusted.Length - 8
            Dim chunkSize As Integer = ReadInt32(adjusted, offset + 4)
            If chunkSize < 0 OrElse offset + 8L + chunkSize > adjusted.Length Then Exit While
            Dim chunkName As String = ReadAscii(adjusted, offset, 4)
            Dim chunkData As Integer = offset + 8
            If chunkName = "fmt " AndAlso chunkSize >= 16 Then
                formatTag = ReadUInt16(adjusted, chunkData)
                bitsPerSample = ReadUInt16(adjusted, chunkData + 14)
                If formatTag = &HFFFE AndAlso chunkSize >= 40 Then formatTag = ReadUInt16(adjusted, chunkData + 24)
            ElseIf chunkName = "data" AndAlso formatTag >= 0 Then
                ScaleSamples(adjusted, chunkData, chunkSize, formatTag, bitsPerSample, gain)
            End If
            offset = chunkData + chunkSize + (chunkSize And 1)
        End While
        Return adjusted
    End Function

    Private Sub ScaleSamples(ByVal data As Byte(), ByVal offset As Integer, ByVal length As Integer,
                             ByVal formatTag As Integer, ByVal bitsPerSample As Integer, ByVal gain As Double)
        Dim finish As Integer = Math.Min(data.Length, offset + length)
        If formatTag = 1 Then
            Select Case bitsPerSample
                Case 8
                    For index As Integer = offset To finish - 1
                        data(index) = CByte(Math.Max(0, Math.Min(255, CInt(Math.Round((data(index) - 128) * gain + 128)))))
                    Next
                Case 16
                    For index As Integer = offset To finish - 2 Step 2
                        Dim sample As Integer = CShort(ReadUInt16(data, index))
                        WriteInt16(data, index, Math.Max(Short.MinValue, Math.Min(Short.MaxValue, CInt(Math.Round(sample * gain)))))
                    Next
                Case 24
                    For index As Integer = offset To finish - 3 Step 3
                        Dim sample As Integer = data(index) Or (CInt(data(index + 1)) << 8) Or (CInt(data(index + 2)) << 16)
                        If (sample And &H800000) <> 0 Then sample = sample Or &HFF000000
                        Dim scaled As Integer = Math.Max(-8388608, Math.Min(8388607, CInt(Math.Round(sample * gain))))
                        data(index) = CByte(scaled And &HFF)
                        data(index + 1) = CByte((scaled >> 8) And &HFF)
                        data(index + 2) = CByte((scaled >> 16) And &HFF)
                    Next
                Case 32
                    For index As Integer = offset To finish - 4 Step 4
                        Dim sample As Integer = ReadInt32(data, index)
                        Dim scaled As Long = CLng(Math.Round(sample * gain))
                        scaled = Math.Max(Integer.MinValue, Math.Min(Integer.MaxValue, scaled))
                        WriteInt32(data, index, CInt(scaled))
                    Next
            End Select
        ElseIf formatTag = 3 Then
            Select Case bitsPerSample
                Case 32
                    For index As Integer = offset To finish - 4 Step 4
                        Dim sample As Single = BitConverter.ToSingle(data, index)
                        WriteSingle(data, index, CSng(Math.Max(-1.0R, Math.Min(1.0R, sample * gain))))
                    Next
                Case 64
                    For index As Integer = offset To finish - 8 Step 8
                        Dim sample As Double = BitConverter.ToDouble(data, index)
                        WriteDouble(data, index, Math.Max(-1.0R, Math.Min(1.0R, sample * gain)))
                    Next
            End Select
        End If
    End Sub

    Private Function ReadAscii(ByVal data As Byte(), ByVal offset As Integer, ByVal count As Integer) As String
        Return Text.Encoding.ASCII.GetString(data, offset, count)
    End Function

    Private Function ReadUInt16(ByVal data As Byte(), ByVal offset As Integer) As Integer
        Return data(offset) Or (CInt(data(offset + 1)) << 8)
    End Function

    Private Function ReadInt32(ByVal data As Byte(), ByVal offset As Integer) As Integer
        Return data(offset) Or (CInt(data(offset + 1)) << 8) Or (CInt(data(offset + 2)) << 16) Or (CInt(data(offset + 3)) << 24)
    End Function

    Private Sub WriteInt16(ByVal data As Byte(), ByVal offset As Integer, ByVal value As Integer)
        data(offset) = CByte(value And &HFF)
        data(offset + 1) = CByte((value >> 8) And &HFF)
    End Sub

    Private Sub WriteInt32(ByVal data As Byte(), ByVal offset As Integer, ByVal value As Integer)
        data(offset) = CByte(value And &HFF)
        data(offset + 1) = CByte((value >> 8) And &HFF)
        data(offset + 2) = CByte((value >> 16) And &HFF)
        data(offset + 3) = CByte((value >> 24) And &HFF)
    End Sub

    Private Sub WriteSingle(ByVal data As Byte(), ByVal offset As Integer, ByVal value As Single)
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, offset, 4)
    End Sub

    Private Sub WriteDouble(ByVal data As Byte(), ByVal offset As Integer, ByVal value As Double)
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, offset, 8)
    End Sub
End Module
#If PHYSICS_PREVIEW Then
End Namespace
#End If
