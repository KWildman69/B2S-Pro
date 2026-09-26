Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Windows.Forms

Public Module StableMonitorIdentity
    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
    Private Structure DISPLAY_DEVICE
        Public cb As Integer
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=32)> Public DeviceName As String
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=128)> Public DeviceString As String
        Public StateFlags As Integer
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=128)> Public DeviceID As String
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=128)> Public DeviceKey As String
    End Structure

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Function EnumDisplayDevices(ByVal device As String, ByVal deviceNumber As UInteger,
                                        ByRef displayDevice As DISPLAY_DEVICE, ByVal flags As UInteger) As Boolean
    End Function

    Public Function GetStableId(ByVal screen As Screen) As String
        If screen Is Nothing Then Return String.Empty
        Dim monitor As New DISPLAY_DEVICE With {.cb = Marshal.SizeOf(GetType(DISPLAY_DEVICE))}
        Dim index As UInteger = 0
        While EnumDisplayDevices(screen.DeviceName, index, monitor, 0)
            If Not String.IsNullOrWhiteSpace(monitor.DeviceID) Then
                Return monitor.DeviceID.Trim() & "|" & monitor.DeviceString.Trim()
            End If
            index += 1UI
            monitor = New DISPLAY_DEVICE With {.cb = Marshal.SizeOf(GetType(DISPLAY_DEVICE))}
        End While
        Return String.Empty
    End Function

    Public Function FindByStableId(ByVal stableId As String) As Screen
        If String.IsNullOrWhiteSpace(stableId) Then Return Nothing
        Return Screen.AllScreens.FirstOrDefault(Function(candidate) String.Equals(GetStableId(candidate), stableId, StringComparison.OrdinalIgnoreCase))
    End Function

    Public Function EncodeStableId(ByVal value As String) As String
        If String.IsNullOrEmpty(value) Then Return String.Empty
        Return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
    End Function

    Public Function DecodeStableId(ByVal value As String) As String
        If String.IsNullOrWhiteSpace(value) Then Return String.Empty
        Try
            Return Encoding.UTF8.GetString(Convert.FromBase64String(value.Trim()))
        Catch
            Return String.Empty
        End Try
    End Function
End Module
