Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading

''' <summary>
''' Watches only the B2S window belonging to the VPX run launched by ID Tester.
''' No server code, table, registry, or display setting is changed.
''' </summary>
Public Module IDTesterWindows
    Private Const WM_CLOSE As UInteger = &H10UI
    Private Const BackglassTitle As String = "B2S Backglass"
    Private Const ServerTitle As String = "B2S Backglass Server"

    Private Delegate Function EnumWindowsProc(ByVal hWnd As IntPtr, ByVal lParam As IntPtr) As Boolean

    <DllImport("user32.dll")>
    Private Function EnumWindows(ByVal callback As EnumWindowsProc, ByVal lParam As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Function IsWindowVisible(ByVal hWnd As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Function GetWindowTextLength(ByVal hWnd As IntPtr) As Integer
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Function GetWindowText(ByVal hWnd As IntPtr, ByVal text As StringBuilder, ByVal maxCount As Integer) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Function GetWindowThreadProcessId(ByVal hWnd As IntPtr, ByRef processId As UInteger) As UInteger
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Function PostMessage(ByVal hWnd As IntPtr, ByVal message As UInteger, ByVal wParam As IntPtr, ByVal lParam As IntPtr) As Boolean
    End Function

    Private Class WindowInfo
        Public Property Handle As IntPtr
        Public Property OwnerId As UInteger
        Public Property Title As String = String.Empty
    End Class

    Public Sub WatchBackglassAndCloseVPX(ByVal game As Process, ByVal expectedStartTicks As Long)
        If game Is Nothing OrElse game.HasExited OrElse game.StartTime.ToUniversalTime().Ticks <> expectedStartTicks Then Return

        Dim gameStart As DateTime = game.StartTime.ToUniversalTime()
        Dim observed As WindowInfo = Nothing
        Dim observedAt As DateTime = DateTime.MinValue
        Dim missingAt As DateTime = DateTime.MinValue
        Dim searchDeadline As DateTime = DateTime.UtcNow.AddSeconds(120)

        While Not game.HasExited
            If observed Is Nothing Then
                observed = FindWindow(game.Id, gameStart, String.Empty, 0UI)
                If observed IsNot Nothing Then observedAt = DateTime.UtcNow
                If observed Is Nothing AndAlso DateTime.UtcNow > searchDeadline Then Return
            Else
                ' The borderless B2S form can initially appear as the server window
                ' and then become the actual backglass. Prefer the latter when present.
                If String.Equals(observed.Title, ServerTitle, StringComparison.OrdinalIgnoreCase) Then
                    Dim actual As WindowInfo = FindWindow(game.Id, gameStart, BackglassTitle, observed.OwnerId)
                    If actual IsNot Nothing Then
                        observed = actual
                        observedAt = DateTime.UtcNow
                        missingAt = DateTime.MinValue
                    End If
                End If

                Dim current As WindowInfo = FindWindow(game.Id, gameStart, observed.Title, observed.OwnerId)
                If current IsNot Nothing Then
                    missingAt = DateTime.MinValue
                ElseIf (DateTime.UtcNow - observedAt).TotalSeconds < 2 Then
                    observed = Nothing
                    missingAt = DateTime.MinValue
                Else
                    If missingAt = DateTime.MinValue Then missingAt = DateTime.UtcNow
                    If (DateTime.UtcNow - missingAt).TotalSeconds >= 1 Then
                        CloseLaunchedVPX(game, expectedStartTicks)
                        Return
                    End If
                End If
            End If
            Thread.Sleep(250)
        End While
    End Sub

    Private Function FindWindow(ByVal gameId As Integer, ByVal gameStart As DateTime,
                                ByVal requiredTitle As String, ByVal requiredOwner As UInteger) As WindowInfo
        Dim found As New List(Of WindowInfo)()
        Dim callback As EnumWindowsProc =
            Function(hWnd As IntPtr, unused As IntPtr) As Boolean
                If Not IsWindowVisible(hWnd) Then Return True
                Dim length As Integer = GetWindowTextLength(hWnd)
                If length <= 0 Then Return True
                Dim titleText As New StringBuilder(length + 1)
                GetWindowText(hWnd, titleText, titleText.Capacity)
                Dim title As String = titleText.ToString()
                If Not String.Equals(title, BackglassTitle, StringComparison.OrdinalIgnoreCase) AndAlso
                   Not String.Equals(title, ServerTitle, StringComparison.OrdinalIgnoreCase) Then Return True
                If requiredTitle.Length > 0 AndAlso Not String.Equals(title, requiredTitle, StringComparison.OrdinalIgnoreCase) Then Return True
                Dim ownerId As UInteger = 0UI
                GetWindowThreadProcessId(hWnd, ownerId)
                If requiredOwner <> 0UI AndAlso ownerId <> requiredOwner Then Return True
                If Not BelongsToLaunchedGame(ownerId, gameId, gameStart) Then Return True
                found.Add(New WindowInfo() With {.Handle = hWnd, .OwnerId = ownerId, .Title = title})
                Return True
            End Function
        EnumWindows(callback, IntPtr.Zero)

        Dim chosen As WindowInfo = Nothing
        For Each candidate As WindowInfo In found
            If chosen Is Nothing OrElse
               (String.Equals(candidate.Title, BackglassTitle, StringComparison.OrdinalIgnoreCase) AndAlso
                Not String.Equals(chosen.Title, BackglassTitle, StringComparison.OrdinalIgnoreCase)) Then
                chosen = candidate
            ElseIf String.Equals(candidate.Title, chosen.Title, StringComparison.OrdinalIgnoreCase) AndAlso
                   candidate.OwnerId <> chosen.OwnerId Then
                ' Ambiguous backglass ownership: do not close any VPX process.
                Return Nothing
            End If
        Next
        Return chosen
    End Function

    Private Function BelongsToLaunchedGame(ByVal windowOwner As UInteger, ByVal gameId As Integer,
                                            ByVal gameStart As DateTime) As Boolean
        If windowOwner = CUInt(gameId) Then Return True
        Try
            Using owner As Process = Process.GetProcessById(CInt(windowOwner))
                Return owner.ProcessName.StartsWith("B2SBackglassServer", StringComparison.OrdinalIgnoreCase) AndAlso
                       owner.StartTime.ToUniversalTime() >= gameStart.AddSeconds(-2)
            End Using
        Catch ex As ArgumentException
            Return False
        Catch ex As InvalidOperationException
            Return False
        End Try
    End Function

    Private Sub CloseLaunchedVPX(ByVal game As Process, ByVal expectedStartTicks As Long)
        If game.HasExited OrElse game.StartTime.ToUniversalTime().Ticks <> expectedStartTicks Then Return
        Dim requested As Boolean = game.CloseMainWindow()
        If Not requested Then
            Dim playerHandle As IntPtr = FindPlayerWindow(game.Id)
            If playerHandle <> IntPtr.Zero Then requested = PostMessage(playerHandle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero)
        End If
        If requested AndAlso game.WaitForExit(10000) Then Return
        ' The user deliberately shut the test backglass. Terminate only the exact
        ' VPX process this test launched if its normal close request did not finish.
        If Not game.HasExited AndAlso game.StartTime.ToUniversalTime().Ticks = expectedStartTicks Then game.Kill()
    End Sub

    Private Function FindPlayerWindow(ByVal gameId As Integer) As IntPtr
        Dim result As IntPtr = IntPtr.Zero
        Dim callback As EnumWindowsProc =
            Function(hWnd As IntPtr, unused As IntPtr) As Boolean
                If Not IsWindowVisible(hWnd) Then Return True
                Dim ownerId As UInteger = 0UI
                GetWindowThreadProcessId(hWnd, ownerId)
                If ownerId <> CUInt(gameId) Then Return True
                Dim length As Integer = GetWindowTextLength(hWnd)
                If length <= 0 Then Return True
                Dim titleText As New StringBuilder(length + 1)
                GetWindowText(hWnd, titleText, titleText.Capacity)
                If titleText.ToString().StartsWith("Visual Pinball", StringComparison.OrdinalIgnoreCase) Then
                    result = hWnd
                    Return False
                End If
                Return True
            End Function
        EnumWindows(callback, IntPtr.Zero)
        Return result
    End Function
End Module
