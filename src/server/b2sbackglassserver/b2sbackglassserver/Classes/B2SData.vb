#Disable Warning BC42016, BC42017, BC42018, BC42019, BC42032
Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Text.RegularExpressions
Imports System.IO.Pipes
Imports System.Threading

Public Class B2SData

    Public Class PhysicsSwitchZone
        Public Bounds As RectangleF
        Public SwitchID As Integer
        Public Angle As Single
    End Class
    Public Class PhysicsLauncher
        Public TriggerType As Integer
        Public TriggerID As Integer
        Public Origin As PointF
        Public Angle As Single
        Public Strength As Single
        Public RandomAngle As Single
        Public RandomStrength As Single
        Public CaptureRadius As Single
        Public FollowPivot As Boolean
    End Class

    Public Shared Property PhysicsSwitchPulseTestHandler As Action(Of Integer)

    Public Shared Property SwitchPulsePipeName As String = String.Empty
    Public Shared Property PhysicsLauncherPipeName As String = String.Empty
    Public Shared Property TesterSwitchesEnabled As Boolean = False
    Public Shared Property UsedRomSwitchIDs As New Generic.SortedList(Of Integer, B2SBaseBox())
#If B2S = "DLL" Then
    Private Shared switchPipeThread As Thread
    Private Shared switchPipeStopping As Boolean

    Public Shared Sub StartPhysicsLauncherBridge(ByVal pipeName As String)
        ' The DLL sends launcher triggers; the EXE owns the receiving bridge.
    End Sub

    Public Shared Sub StartSwitchPulseBridge(ByVal pipeName As String)
        StopSwitchPulseBridge()
        If String.IsNullOrWhiteSpace(pipeName) Then Return
        SwitchPulsePipeName = pipeName
        PhysicsLauncherPipeName = pipeName & "_Launcher"
        switchPipeStopping = False
        switchPipeThread = New Thread(AddressOf ListenForSwitchPulses) With {.IsBackground = True, .Name = "B2S switch pulse bridge"}
        switchPipeThread.Start()
    End Sub

    Public Shared Sub StopSwitchPulseBridge()
        switchPipeStopping = True
        Dim name As String = SwitchPulsePipeName
        If name.Length > 0 Then
            Try
                Using client As New NamedPipeClientStream(".", name, PipeDirection.Out)
                    client.Connect(25)
                    Using writer As New BinaryWriter(client)
                        writer.Write(0)
                    End Using
                End Using
            Catch
            End Try
        End If
        SwitchPulsePipeName = String.Empty
        PhysicsLauncherPipeName = String.Empty
        TesterSwitchesEnabled = False
    End Sub

    Private Shared Sub ListenForSwitchPulses()
        While Not switchPipeStopping
            Try
                Using server As New NamedPipeServerStream(SwitchPulsePipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None)
                    server.WaitForConnection()
                    Using reader As New BinaryReader(server)
                        Dim switchID As Integer = reader.ReadInt32()
                        If Not switchPipeStopping Then
                            If switchID = -5 Then
                                TesterSwitchesEnabled = True
                            ElseIf switchID > 0 Then
                                B2SAnimation.SetSwitch(switchID)
                            End If
                        End If
                    End Using
                End Using
            Catch
                If Not switchPipeStopping Then Thread.Sleep(10)
            End Try
        End While
    End Sub

    Public Shared Sub PulsePhysicsSwitch(ByVal switchID As Integer)
        If PhysicsSwitchPulseTestHandler IsNot Nothing Then PhysicsSwitchPulseTestHandler.Invoke(switchID) : Return
        If switchID > 0 Then B2SAnimation.SetSwitch(switchID)
    End Sub

    Private Shared Sub SendPhysicsLauncherTrigger(ByVal triggerType As Integer, ByVal triggerID As Integer, ByVal value As Integer)
        If (value = 0 AndAlso triggerType <> -5) OrElse String.IsNullOrWhiteSpace(PhysicsLauncherPipeName) Then Return
        Try
            Using client As New NamedPipeClientStream(".", PhysicsLauncherPipeName, PipeDirection.Out)
                client.Connect(100)
                Using writer As New BinaryWriter(client)
                    writer.Write(triggerType)
                    writer.Write(triggerID)
                    writer.Write(value)
                End Using
            End Using
        Catch
        End Try
    End Sub

    Public Shared Sub SendTesterSwitchState(ByVal switchID As Integer, ByVal state As Boolean)
        SendPhysicsLauncherTrigger(-5, switchID, If(state, 1, 0))
    End Sub
#Else
    Private Shared launcherPipeThread As Thread
    Private Shared launcherPipeStopping As Boolean
    Private Shared ReadOnly testerSwitchQueue As New Generic.Queue(Of Generic.KeyValuePair(Of Integer, Boolean))()
    Private Shared testerSwitchAnnounced As Boolean = False

    Public Shared Sub StartPhysicsLauncherBridge(ByVal pipeName As String)
        If String.IsNullOrWhiteSpace(pipeName) Then Return
        PhysicsLauncherPipeName = pipeName
        launcherPipeStopping = False
        launcherPipeThread = New Thread(AddressOf ListenForPhysicsLauncherTriggers) With {.IsBackground = True, .Name = "B2S physics launcher bridge"}
        launcherPipeThread.Start()
    End Sub

    Private Shared Sub ListenForPhysicsLauncherTriggers()
        While Not launcherPipeStopping
            Try
                Using server As New NamedPipeServerStream(PhysicsLauncherPipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None)
                    server.WaitForConnection()
                    Using reader As New BinaryReader(server)
                        Dim triggerType As Integer = reader.ReadInt32()
                        Dim triggerID As Integer = reader.ReadInt32()
                        Dim value As Integer = reader.ReadInt32()
                        If Not launcherPipeStopping Then
                            If triggerType = -5 Then
                                SyncLock testerSwitchQueue
                                    testerSwitchQueue.Enqueue(New Generic.KeyValuePair(Of Integer, Boolean)(triggerID, value <> 0))
                                End SyncLock
                            Else
                                FirePhysicsLaunchers(triggerType, triggerID, value)
                            End If
                        End If
                    End Using
                End Using
            Catch
                If Not launcherPipeStopping Then Thread.Sleep(10)
            End Try
        End While
    End Sub

    Public Shared Sub AnnounceTesterSwitches()
        If testerSwitchAnnounced OrElse String.IsNullOrWhiteSpace(SwitchPulsePipeName) Then Return
        Try
            Using client As New NamedPipeClientStream(".", SwitchPulsePipeName, PipeDirection.Out)
                client.Connect(100)
                Using writer As New BinaryWriter(client)
                    writer.Write(-5)
                End Using
            End Using
            testerSwitchAnnounced = True
        Catch
        End Try
    End Sub

    Public Shared Function TryDequeueTesterSwitchState(ByRef switchID As Integer, ByRef state As Boolean) As Boolean
        SyncLock testerSwitchQueue
            If testerSwitchQueue.Count = 0 Then Return False
            Dim item As Generic.KeyValuePair(Of Integer, Boolean) = testerSwitchQueue.Dequeue()
            switchID = item.Key
            state = item.Value
            Return True
        End SyncLock
    End Function

    Public Shared Sub PulsePhysicsSwitch(ByVal switchID As Integer)
        If PhysicsSwitchPulseTestHandler IsNot Nothing Then PhysicsSwitchPulseTestHandler.Invoke(switchID) : Return
        If switchID <= 0 OrElse String.IsNullOrWhiteSpace(SwitchPulsePipeName) Then Return
        Try
            Using client As New NamedPipeClientStream(".", SwitchPulsePipeName, PipeDirection.Out)
                client.Connect(100)
                Using writer As New BinaryWriter(client)
                    writer.Write(switchID)
                End Using
            End Using
        Catch
        End Try
    End Sub
#End If

    Private Declare Function GetShortPathName Lib "kernel32" Alias "GetShortPathNameA" (ByVal LongName As String, ShortName As String, ByVal bufsize As Integer) As Long
    Public Enum eDMDType
        NotDefined = 0
        NoB2SDMD = 1
        B2SAlwaysOnSecondMonitor = 2
        B2SAlwaysOnThirdMonitor = 3
        B2SOnSecondOrThirdMonitor = 4
    End Enum
    Public Enum eDualMode
        Both = 0
        Authentic = 1
        Fantasy = 2
    End Enum
#If B2S = "DLL" Then
    Private Shared _vpinmame As Object = Nothing
    Public Shared VPMHasTimeFence As Boolean = False
    Public Shared ReadOnly Property VPinMAME() As Object
        Get
            If _vpinmame Is Nothing OrElse IsStopped Then
                _vpinmame = CreateObject("VPinMAME.Controller")
                VPMHasTimeFence = _vpinmame.GetType.GetProperty("TimeFence") IsNot Nothing
                If IsStopped Then
                    _vpinmame.GameName = stoppedGameName
                    IsStopped = False
                End If
            End If
            Return _vpinmame
        End Get
    End Property

    Private Shared IsStopped As Boolean = False
    Private Shared stoppedGameName As String = String.Empty
    Public Shared Sub [Stop]()
        If _vpinmame IsNot Nothing Then
            stoppedGameName = _vpinmame.GameName
            _vpinmame.Stop()
            _vpinmame = Nothing
        End If
        IsStopped = True
    End Sub


    Private Shared IsLampsInfoDirty As Boolean = True
    Private Shared IsSolenoidsInfoDirty As Boolean = True
    Private Shared IsGIStringsInfoDirty As Boolean = True
    Private Shared IsLEDInfoDirty As Boolean = True
    Public Shared Property IsInfoDirty() As Boolean
        Get
            Return (IsLampsInfoDirty OrElse IsSolenoidsInfoDirty OrElse IsGIStringsInfoDirty OrElse IsLEDInfoDirty)
        End Get
        Set(ByVal value As Boolean)
            IsLampsInfoDirty = value
            IsSolenoidsInfoDirty = value
            IsGIStringsInfoDirty = value
            IsLEDInfoDirty = value
        End Set
    End Property

    Public Shared ReadOnly Property IsBackglassRunning() As Boolean
        Get
            Return (LaunchBackglass AndAlso IsBackglassVisible)
        End Get
    End Property

    Private Shared _LaunchBackglass As Boolean = True
    Public Shared Property LaunchBackglass() As Boolean
        Get
            Return _LaunchBackglass
        End Get
        Set(ByVal value As Boolean)
            _LaunchBackglass = value
            IsInfoDirty = True
        End Set
    End Property
    Private Shared _IsBackglassVisible As Boolean = False
    Public Shared Property IsBackglassVisible() As Boolean
        Get
            Return _IsBackglassVisible
        End Get
        Set(ByVal value As Boolean)
            _IsBackglassVisible = value
            IsInfoDirty = True
        End Set
    End Property
    Private Shared Property _IsBackglassStartedAsEXE() As Boolean = False
    Public Shared Property IsBackglassStartedAsEXE() As Boolean
        Get
            Return _IsBackglassStartedAsEXE
        End Get
        Set(ByVal value As Boolean)
            _IsBackglassStartedAsEXE = value
            IsInfoDirty = True
        End Set
    End Property
#End If

    Public Shared Property OnAndOffImage() As Boolean = False
    Public Shared Property IsOffImageVisible() As Boolean = False

    Public Shared ReadOnly Property UsedRomLampIDs() As Generic.SortedList(Of Integer, B2SBaseBox())
        Get
            Return If(B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy, UsedRomLampIDs4Fantasy, UsedRomLampIDs4Authentic)
        End Get
    End Property
    Public Shared ReadOnly Property UsedRomSolenoidIDs() As Generic.SortedList(Of Integer, B2SBaseBox())
        Get
            Return If(B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy, UsedRomSolenoidIDs4Fantasy, UsedRomSolenoidIDs4Authentic)
        End Get
    End Property
    Public Shared ReadOnly Property UsedRomGIStringIDs() As Generic.SortedList(Of Integer, B2SBaseBox())
        Get
            Return If(B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy, UsedRomGIStringIDs4Fantasy, UsedRomGIStringIDs4Authentic)
        End Get
    End Property
    Public Shared ReadOnly Property UsedRomMechIDs() As Generic.SortedList(Of Integer, B2SBaseBox())
        Get
            Return If(B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy, UsedRomMechIDs4Fantasy, UsedRomMechIDs4Authentic)
        End Get
    End Property
    Public Shared Property UsedRomLampIDs4Authentic() As Generic.SortedList(Of Integer, B2SBaseBox()) = New Generic.SortedList(Of Integer, B2SBaseBox())
    Public Shared Property UsedRomSolenoidIDs4Authentic() As Generic.SortedList(Of Integer, B2SBaseBox()) = New Generic.SortedList(Of Integer, B2SBaseBox())
    Public Shared Property UsedRomGIStringIDs4Authentic() As Generic.SortedList(Of Integer, B2SBaseBox()) = New Generic.SortedList(Of Integer, B2SBaseBox())
    Public Shared Property UsedRomMechIDs4Authentic() As Generic.SortedList(Of Integer, B2SBaseBox()) = New Generic.SortedList(Of Integer, B2SBaseBox())
    Public Shared Property UsedRomLampIDs4Fantasy() As Generic.SortedList(Of Integer, B2SBaseBox()) = New Generic.SortedList(Of Integer, B2SBaseBox())
    Public Shared Property UsedRomSolenoidIDs4Fantasy() As Generic.SortedList(Of Integer, B2SBaseBox()) = New Generic.SortedList(Of Integer, B2SBaseBox())
    Public Shared Property UsedMotionPathSolenoidIDs() As Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox)) = New Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox))()
    Public Shared Property UsedMotionPathLampIDs() As Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox)) = New Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox))()
    Public Shared Property UsedMotionPathB2SIDs() As Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox)) = New Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox))()
    Public Shared Property UsedMotionPathStopB2SIDs() As Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox)) = New Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox))()
    Public Shared Property UsedMotionPathResumeB2SIDs() As Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox)) = New Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox))()
    Public Shared Property UsedMotionPathRemoveSolenoidIDs() As Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox)) = New Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox))()
    Public Shared Property UsedMotionPathRemoveLampIDs() As Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox)) = New Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox))()
    Public Shared Property UsedMotionPathRemoveB2SIDs() As Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox)) = New Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox))()
    Private Shared ReadOnly MotionPathSequenceGroups As New Generic.Dictionary(Of String, MotionPathSequenceState)(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly ExternalPositionGrids As New Generic.Dictionary(Of String, ExternalPositionGridState)(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly ExternalPositionGridTargets As New Generic.Dictionary(Of String, ExternalPositionGridState)(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly ExternalPivotGroups As New Generic.Dictionary(Of String, ExternalPivotState)(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly ExternalPivotTargets As New Generic.Dictionary(Of String, ExternalPivotState)(StringComparer.OrdinalIgnoreCase)
    Public Shared ReadOnly PivotSolenoidIDs As New Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox))()
    Public Shared ReadOnly PivotLampIDs As New Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox))()
    Public Shared ReadOnly PivotB2SIDs As New Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox))()
    Public Shared ReadOnly StartupPivotPictures As New Generic.List(Of B2SPictureBox)()
    Private Shared ReadOnly PivotPicturesByName As New Generic.Dictionary(Of String, B2SPictureBox)(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly PhysicsBalls As New Generic.List(Of PhysicsBallState)()
    Private Shared ReadOnly PhysicsLauncherSolenoidIDs As New Generic.SortedList(Of Integer, Generic.List(Of PhysicsBallState))()
    Private Shared ReadOnly PhysicsLauncherB2SIDs As New Generic.SortedList(Of Integer, Generic.List(Of PhysicsBallState))()

    Public Shared Sub RegisterPivotTrigger(ByVal pictureBox As B2SPictureBox, ByVal triggerType As Integer, ByVal triggerID As Integer,
                                           ByVal downAngle As Single, ByVal upAngle As Single, ByVal duration As Integer,
                                           Optional ByVal automaticOscillation As Boolean = False)
        If pictureBox Is Nothing OrElse (triggerType <> 4 AndAlso triggerID <= 0) Then Return
        pictureBox.PivotRotation = True : pictureBox.NativeRotation = True
        pictureBox.PivotDownAngle = downAngle : pictureBox.PivotUpAngle = upAngle
        pictureBox.PivotMoveDuration = Math.Max(10, Math.Min(5000, duration))
        pictureBox.PivotAutomaticOscillation = automaticOscillation OrElse triggerType = 4
        pictureBox.RotationAngle = If(pictureBox.PivotAutomaticOscillation, 0.0F, downAngle)
        If Not String.IsNullOrWhiteSpace(pictureBox.GroupName) Then PivotPicturesByName(pictureBox.GroupName.Trim()) = pictureBox
        If triggerType = 4 Then
            StartupPivotPictures.Add(pictureBox)
            Return
        End If
        Dim targets As Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox)) = If(triggerType = 1, PivotSolenoidIDs, If(triggerType = 2, PivotLampIDs, PivotB2SIDs))
        If Not targets.ContainsKey(triggerID) Then targets.Add(triggerID, New Generic.List(Of B2SPictureBox)())
        targets(triggerID).Add(pictureBox)
    End Sub

    ' Compatibility overload preserves the original physics prototype API and
    ' its exact restitution/strength defaults.
    Public Shared Sub RegisterPhysicsBall(ByVal pictureBox As B2SPictureBox, ByVal flipperName As String,
                                          ByVal bounds As RectangleF, ByVal gravity As Single,
                                          Optional ByVal floorPoints As Generic.List(Of PointF) = Nothing)
        RegisterPhysicsBall(pictureBox, flipperName, bounds, gravity, 1.0F, 0.12F, floorPoints)
    End Sub

    Public Shared Sub RegisterPhysicsBall(ByVal pictureBox As B2SPictureBox, ByVal flipperName As String,
                                          ByVal bounds As RectangleF, ByVal gravity As Single,
                                          ByVal flipperStrength As Single, ByVal boundaryBounce As Single,
                                          Optional ByVal floorPoints As Generic.List(Of PointF) = Nothing)
        Dim paths As New Generic.List(Of Generic.List(Of PointF))()
        If floorPoints IsNot Nothing AndAlso floorPoints.Count >= 2 Then paths.Add(floorPoints)
        RegisterPhysicsBall(pictureBox, flipperName, bounds, gravity, flipperStrength, boundaryBounce, paths)
    End Sub

    Public Shared Sub RegisterPhysicsBall(ByVal pictureBox As B2SPictureBox, ByVal flipperName As String,
                                          ByVal bounds As RectangleF, ByVal gravity As Single,
                                          ByVal flipperStrength As Single, ByVal boundaryBounce As Single,
                                          ByVal boundaryPaths As Generic.List(Of Generic.List(Of PointF)))
        RegisterPhysicsBall(pictureBox, flipperName, bounds, gravity, flipperStrength, boundaryBounce, boundaryPaths, Nothing)
    End Sub

    Public Shared Sub RegisterPhysicsBall(ByVal pictureBox As B2SPictureBox, ByVal flipperName As String,
                                          ByVal bounds As RectangleF, ByVal gravity As Single,
                                          ByVal flipperStrength As Single, ByVal boundaryBounce As Single,
                                          ByVal boundaryPaths As Generic.List(Of Generic.List(Of PointF)),
                                          ByVal obstacles As Generic.List(Of RectangleF))
        RegisterPhysicsBall(pictureBox, flipperName, bounds, gravity, flipperStrength, boundaryBounce, boundaryPaths, obstacles, Nothing)
    End Sub

    Public Shared Sub RegisterPhysicsBall(ByVal pictureBox As B2SPictureBox, ByVal flipperName As String,
                                          ByVal bounds As RectangleF, ByVal gravity As Single,
                                          ByVal flipperStrength As Single, ByVal boundaryBounce As Single,
                                          ByVal boundaryPaths As Generic.List(Of Generic.List(Of PointF)),
                                          ByVal obstacles As Generic.List(Of RectangleF),
                                          ByVal switchZones As Generic.List(Of PhysicsSwitchZone))
        RegisterPhysicsBall(pictureBox, flipperName, bounds, gravity, flipperStrength, boundaryBounce, boundaryPaths, obstacles, switchZones, Nothing)
    End Sub

    Public Shared Sub RegisterPhysicsBall(ByVal pictureBox As B2SPictureBox, ByVal flipperName As String,
                                          ByVal bounds As RectangleF, ByVal gravity As Single,
                                          ByVal flipperStrength As Single, ByVal boundaryBounce As Single,
                                          ByVal boundaryPaths As Generic.List(Of Generic.List(Of PointF)),
                                          ByVal obstacles As Generic.List(Of RectangleF),
                                          ByVal switchZones As Generic.List(Of PhysicsSwitchZone),
                                          ByVal launcher As PhysicsLauncher,
                                          Optional ByVal boundarySegmentBounces As Generic.List(Of Generic.List(Of Single)) = Nothing)
        If pictureBox Is Nothing OrElse bounds.IsEmpty Then Return
        Dim normalizedFlipperName As String = If(flipperName, String.Empty).Trim()
        Dim state As New PhysicsBallState(pictureBox, normalizedFlipperName, bounds,
                                          Math.Max(0.0F, Math.Min(10000.0F, gravity)),
                                          Math.Max(0.0F, Math.Min(5.0F, flipperStrength)),
                                          Math.Max(0.0F, Math.Min(1.0F, boundaryBounce)), boundaryPaths, obstacles, switchZones, launcher,
                                          boundarySegmentBounces)
        PhysicsBalls.Add(state)
        If launcher IsNot Nothing AndAlso launcher.TriggerID > 0 Then
            Dim routes = If(launcher.TriggerType = 3, PhysicsLauncherB2SIDs, PhysicsLauncherSolenoidIDs)
            If Not routes.ContainsKey(launcher.TriggerID) Then routes.Add(launcher.TriggerID, New Generic.List(Of PhysicsBallState)())
            routes(launcher.TriggerID).Add(state)
        End If
        state.Start()
    End Sub

    Public Shared Function FirePhysicsLaunchers(ByVal triggerType As Integer, ByVal triggerID As Integer, ByVal value As Integer) As Boolean
        If value = 0 Then Return False
        Dim routes = If(triggerType = 3, PhysicsLauncherB2SIDs, PhysicsLauncherSolenoidIDs)
        If Not routes.ContainsKey(triggerID) Then
#If B2S = "DLL" Then
            SendPhysicsLauncherTrigger(triggerType, triggerID, value)
#End If
            Return False
        End If
        For Each state As PhysicsBallState In routes(triggerID)
            state.Launch()
        Next
        Return True
    End Function


    Public Shared Function GetPhysicsLauncherIDs(ByVal triggerType As Integer) As Integer()
        Return If(triggerType = 3, PhysicsLauncherB2SIDs.Keys.ToArray(), PhysicsLauncherSolenoidIDs.Keys.ToArray())
    End Function

    Private Shared Function FindPivotPicture(ByVal name As String) As B2SPictureBox
        Dim pictureBox As B2SPictureBox = Nothing
        If Not String.IsNullOrWhiteSpace(name) Then PivotPicturesByName.TryGetValue(name.Trim(), pictureBox)
        Return pictureBox
    End Function

    Public Shared Function SetPivotTriggerState(ByVal targets As Generic.SortedList(Of Integer, Generic.List(Of B2SPictureBox)), ByVal id As Integer, ByVal value As Integer) As Boolean
        If Not targets.ContainsKey(id) Then Return False
        For Each pictureBox As B2SPictureBox In targets(id)
            pictureBox.SetPivotTriggerState(value <> 0)
        Next
        Return True
    End Function

    Public Shared Sub RegisterExternalPivot(ByVal pictureBox As B2SPictureBox, ByVal groupName As String,
                                            ByVal targetName As String, ByVal representative As Boolean,
                                            ByVal angle As Single, ByVal duration As Integer)
        If pictureBox Is Nothing OrElse String.IsNullOrWhiteSpace(groupName) OrElse String.IsNullOrWhiteSpace(targetName) Then Return
        Dim state As ExternalPivotState = Nothing
        If Not ExternalPivotGroups.TryGetValue(groupName.Trim(), state) Then
            state = New ExternalPivotState()
            ExternalPivotGroups.Add(groupName.Trim(), state)
        End If
        state.Register(pictureBox, targetName.Trim(), representative, angle, duration)
        ExternalPivotTargets(targetName.Trim()) = state
    End Sub

    Public Shared Sub RegisterSingleImagePivot(ByVal pictureBox As B2SPictureBox, ByVal groupName As String,
                                               ByVal downTarget As String, ByVal downAngle As Single,
                                               ByVal upTarget As String, ByVal upAngle As Single,
                                               ByVal duration As Integer)
        If pictureBox Is Nothing OrElse String.IsNullOrWhiteSpace(groupName) OrElse
           String.IsNullOrWhiteSpace(downTarget) OrElse String.IsNullOrWhiteSpace(upTarget) Then Return
        Dim state As New ExternalPivotState()
        state.Register(pictureBox, downTarget.Trim(), True, downAngle, duration)
        state.Register(pictureBox, upTarget.Trim(), False, upAngle, duration)
        ExternalPivotGroups(groupName.Trim()) = state
        ExternalPivotTargets(downTarget.Trim()) = state
        ExternalPivotTargets(upTarget.Trim()) = state
    End Sub

    Public Shared Function TryMoveExternalPivot(ByVal targetName As String, ByVal value As Integer) As Boolean
        If String.IsNullOrWhiteSpace(targetName) Then Return False
        Dim state As ExternalPivotState = Nothing
        If Not ExternalPivotTargets.TryGetValue(targetName.Trim(), state) Then Return False
        If value <> 0 Then state.MoveTo(targetName.Trim())
        Return True
    End Function

    Public Shared Sub RegisterExternalPositionGrid(ByVal pictureBox As B2SPictureBox,
                                                   ByVal gridName As String,
                                                   ByVal targetName As String,
                                                   ByVal representative As Boolean,
                                                   ByVal duration As Integer)
        If pictureBox Is Nothing OrElse String.IsNullOrWhiteSpace(gridName) OrElse String.IsNullOrWhiteSpace(targetName) Then Return
        gridName = gridName.Trim()
        targetName = targetName.Trim()
        Dim state As ExternalPositionGridState = Nothing
        If Not ExternalPositionGrids.TryGetValue(gridName, state) Then
            state = New ExternalPositionGridState()
            ExternalPositionGrids.Add(gridName, state)
        End If
        state.Register(pictureBox, targetName, representative, duration)
        ExternalPositionGridTargets(targetName) = state
    End Sub

    Public Shared Function TryMoveExternalPositionGrid(ByVal targetName As String, ByVal value As Integer) As Boolean
        If String.IsNullOrWhiteSpace(targetName) Then Return False
        Dim state As ExternalPositionGridState = Nothing
        If Not ExternalPositionGridTargets.TryGetValue(targetName.Trim(), state) Then Return False
        If value <> 0 Then state.MoveTo(targetName.Trim())
        Return True
    End Function

    Public Shared Sub RegisterMotionPathSequence(ByVal pictureBox As B2SPictureBox)
        If pictureBox Is Nothing OrElse String.IsNullOrWhiteSpace(pictureBox.MotionPathSequenceGroup) OrElse pictureBox.MotionPathSequenceOrder <= 0 Then Return
        Dim groupName As String = pictureBox.MotionPathSequenceGroup.Trim()
        If Not MotionPathSequenceGroups.ContainsKey(groupName) Then MotionPathSequenceGroups.Add(groupName, New MotionPathSequenceState())
        MotionPathSequenceGroups(groupName).Register(pictureBox)
    End Sub

    Public Shared Sub StartMotionPaths(ByVal pictureBoxes As IEnumerable(Of B2SPictureBox))
        If pictureBoxes Is Nothing Then Return
        Dim requestedGroups As New Generic.HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each pictureBox As B2SPictureBox In pictureBoxes
            If pictureBox Is Nothing Then Continue For
            Dim groupName As String = If(pictureBox.MotionPathSequenceGroup, String.Empty).Trim()
            If groupName.Length > 0 AndAlso pictureBox.MotionPathSequenceOrder > 0 AndAlso MotionPathSequenceGroups.ContainsKey(groupName) Then
                requestedGroups.Add(groupName)
            Else
                pictureBox.StartMotionPath()
            End If
        Next
        For Each groupName As String In requestedGroups
            MotionPathSequenceGroups(groupName).RequestStart()
        Next
    End Sub

    Public Shared Sub RemoveMotionPaths(ByVal pictureBoxes As IEnumerable(Of B2SPictureBox))
        If pictureBoxes Is Nothing Then Return
        Dim requestedGroups As New Generic.HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each pictureBox As B2SPictureBox In pictureBoxes
            If pictureBox Is Nothing Then Continue For
            Dim groupName As String = If(pictureBox.MotionPathSequenceGroup, String.Empty).Trim()
            If groupName.Length > 0 AndAlso pictureBox.MotionPathSequenceOrder > 0 AndAlso MotionPathSequenceGroups.ContainsKey(groupName) Then
                requestedGroups.Add(groupName)
            ElseIf pictureBox.MotionPathExitPoints.Count >= 2 Then
                pictureBox.StartMotionPathExit()
            End If
        Next
        For Each groupName As String In requestedGroups
            MotionPathSequenceGroups(groupName).RequestRemove()
        Next
    End Sub

    Private Class MotionPathSequenceState
        Private ReadOnly members As New Generic.List(Of B2SPictureBox)()
        Private ReadOnly activeEntries As New Generic.HashSet(Of B2SPictureBox)()
        Private ReadOnly occupiedMembers As New Generic.HashSet(Of B2SPictureBox)()
        Private ReadOnly pendingOperations As New Generic.Queue(Of Boolean)()
        Private ReadOnly compactionTargets As New Generic.List(Of PointF)()
        Private activeRemoval As B2SPictureBox = Nothing
        Private launchingMember As B2SPictureBox = Nothing
        Private sourceAnchor As B2SPictureBox = Nothing
        Private sourceReady As Boolean = True

        Public Sub Register(ByVal pictureBox As B2SPictureBox)
            If pictureBox Is Nothing OrElse members.Contains(pictureBox) Then Return
            If pictureBox.MotionPathRollEnabled Then
                ' A stable golden-angle phase keeps striped trough balls from
                ' stopping in lockstep, even when their path lengths match.
                pictureBox.MotionPathRollAngle = CSng(((Math.Max(1, pictureBox.MotionPathSequenceOrder) - 1) * 137.507764R) Mod 360.0R)
            End If
            members.Add(pictureBox)
            members.Sort(Function(left As B2SPictureBox, right As B2SPictureBox)
                             Dim orderResult As Integer = left.MotionPathSequenceOrder.CompareTo(right.MotionPathSequenceOrder)
                             If orderResult <> 0 Then Return orderResult
                             Return String.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase)
                         End Function)
            ' The first ordered member supplies the artwork and authored source
            ' rectangle for the replenishing feeder shown at the path start.
            For Each member As B2SPictureBox In members
                member.SetMotionPathSequenceSourceAnchor(False)
            Next
            sourceAnchor = If(members.Count > 0, members(0), Nothing)
            If sourceAnchor IsNot Nothing Then
                sourceAnchor.SetMotionPathSequenceSourceAnchor(True)
                UpdateSourceAppearance()
                sourceAnchor.SetMotionPathSequenceSourceVisible(sourceReady)
            End If
        End Sub

        Public Sub RequestStart()
            If pendingOperations.Count > 0 Then
                QueueOperation(False)
                ProcessPendingOperations()
                Return
            End If
            If activeRemoval IsNot Nothing Then
                QueueOperation(False)
                Return
            End If
            If Not sourceReady Then
                If QueueEnabled() Then
                    QueueOperation(False)
                ElseIf launchingMember IsNot Nothing AndAlso activeEntries.Contains(launchingMember) Then
                    BeginEntry(launchingMember)
                End If
                Return
            End If
            StartEntry()
        End Sub

        Public Sub RequestRemove()
            If pendingOperations.Count > 0 OrElse activeEntries.Count > 0 Then
                QueueOperation(True)
                ProcessPendingOperations()
                Return
            End If
            If activeRemoval IsNot Nothing Then
                If QueueEnabled() Then QueueOperation(True) Else activeRemoval.StartMotionPathExit()
                Return
            End If
            StartRemoval()
        End Sub

        Private Sub QueueOperation(ByVal remove As Boolean)
            If QueueEnabled() AndAlso pendingOperations.Count < 32 Then pendingOperations.Enqueue(remove)
        End Sub

        Public Sub Clear()
            For Each member As B2SPictureBox In activeEntries
                RemoveHandler member.MotionPathCompleted, AddressOf EntryCompleted
                RemoveHandler member.MotionPathLaunchSegmentCompleted, AddressOf MemberLaunchSegmentCompleted
            Next
            If activeRemoval IsNot Nothing Then RemoveHandler activeRemoval.MotionPathCompleted, AddressOf RemovalCompleted
            If sourceAnchor IsNot Nothing Then RemoveHandler sourceAnchor.MotionPathSourceRespawnCompleted, AddressOf SourceRespawnCompleted
            activeEntries.Clear()
            activeRemoval = Nothing
            launchingMember = Nothing
            pendingOperations.Clear()
            compactionTargets.Clear()
            occupiedMembers.Clear()
            For Each member As B2SPictureBox In members
                If member IsNot Nothing Then member.SetMotionPathSequenceSourceAnchor(False)
            Next
            If sourceAnchor IsNot Nothing Then sourceAnchor.SetMotionPathSequenceSourceImage(Nothing)
            sourceAnchor = Nothing
            sourceReady = True
            members.Clear()
        End Sub

        Private Function QueueEnabled() As Boolean
            For Each member As B2SPictureBox In members
                If member IsNot Nothing AndAlso member.MotionPathQueueTriggers Then Return True
            Next
            Return False
        End Function

        Private Function StartEntry() As Boolean
            If Not sourceReady OrElse activeRemoval IsNot Nothing Then Return False
            Dim memberToStart As B2SPictureBox = Nothing
            For Each member As B2SPictureBox In members
                If Not occupiedMembers.Contains(member) AndAlso Not activeEntries.Contains(member) Then
                    memberToStart = member
                    Exit For
                End If
            Next
            If memberToStart Is Nothing Then Return False
            activeEntries.Add(memberToStart)
            RemoveHandler memberToStart.MotionPathCompleted, AddressOf EntryCompleted
            AddHandler memberToStart.MotionPathCompleted, AddressOf EntryCompleted
            BeginEntry(memberToStart)
            Return True
        End Function

        Private Function StartRemoval() As Boolean
            If activeRemoval IsNot Nothing OrElse activeEntries.Count > 0 Then Return False
            For index As Integer = 0 To members.Count - 1
                If occupiedMembers.Contains(members(index)) AndAlso members(index).MotionPathExitPoints.Count >= 2 Then
                    activeRemoval = members(index)
                    Exit For
                End If
            Next
            If activeRemoval Is Nothing Then Return False
            RemoveHandler activeRemoval.MotionPathCompleted, AddressOf RemovalCompleted
            AddHandler activeRemoval.MotionPathCompleted, AddressOf RemovalCompleted
            StartCompactionShifts(activeRemoval)
            activeRemoval.StartMotionPathExit()
            Return True
        End Function

        Private Sub BeginEntry(ByVal member As B2SPictureBox)
            If member Is Nothing Then Return
            launchingMember = member
            sourceReady = False
            If sourceAnchor IsNot Nothing Then
                sourceAnchor.SetMotionPathSequenceSourceImage(member.BackgroundImage)
                sourceAnchor.SetMotionPathSequenceSourceVisible(False)
            End If
            RemoveHandler member.MotionPathLaunchSegmentCompleted, AddressOf MemberLaunchSegmentCompleted
            AddHandler member.MotionPathLaunchSegmentCompleted, AddressOf MemberLaunchSegmentCompleted
            member.StartMotionPath()
        End Sub

        Private Sub MemberLaunchSegmentCompleted(ByVal sender As Object, ByVal e As EventArgs)
            Dim launchedMember As B2SPictureBox = TryCast(sender, B2SPictureBox)
            If launchedMember IsNot Nothing Then RemoveHandler launchedMember.MotionPathLaunchSegmentCompleted, AddressOf MemberLaunchSegmentCompleted
            If launchedMember Is launchingMember AndAlso activeEntries.Contains(launchedMember) Then
                BeginSourceRespawn()
            End If
        End Sub

        Private Sub BeginSourceRespawn()
            launchingMember = Nothing
            If sourceAnchor IsNot Nothing Then
                UpdateSourceAppearance()
                RemoveHandler sourceAnchor.MotionPathSourceRespawnCompleted, AddressOf SourceRespawnCompleted
                AddHandler sourceAnchor.MotionPathSourceRespawnCompleted, AddressOf SourceRespawnCompleted
                If sourceAnchor.StartMotionPathSequenceSourceRespawn() Then Return
                RemoveHandler sourceAnchor.MotionPathSourceRespawnCompleted, AddressOf SourceRespawnCompleted
            End If
            CompleteSourceRespawn()
        End Sub

        Private Sub SourceRespawnCompleted(ByVal sender As Object, ByVal e As EventArgs)
            Dim completedSource As B2SPictureBox = TryCast(sender, B2SPictureBox)
            If completedSource IsNot Nothing Then RemoveHandler completedSource.MotionPathSourceRespawnCompleted, AddressOf SourceRespawnCompleted
            CompleteSourceRespawn()
        End Sub

        Private Sub CompleteSourceRespawn()
            sourceReady = True
            If sourceAnchor IsNot Nothing Then sourceAnchor.SetMotionPathSequenceSourceVisible(True)
            ProcessPendingOperations()
        End Sub

        Private Sub ProcessPendingOperations()
            While pendingOperations.Count > 0
                Dim remove As Boolean = pendingOperations.Peek()
                If remove Then
                    If activeRemoval IsNot Nothing OrElse activeEntries.Count > 0 Then Return
                    pendingOperations.Dequeue()
                    If StartRemoval() Then Return
                Else
                    If activeRemoval IsNot Nothing OrElse Not sourceReady Then Return
                    pendingOperations.Dequeue()
                    If StartEntry() Then Return
                End If
            End While
        End Sub

        Private Sub StartCompactionShifts(ByVal removingMember As B2SPictureBox)
            compactionTargets.Clear()
            Dim occupiedInOrder As New Generic.List(Of B2SPictureBox)()
            For Each member As B2SPictureBox In members
                If occupiedMembers.Contains(member) Then occupiedInOrder.Add(member)
            Next
            If occupiedInOrder.Count = 0 OrElse occupiedInOrder(0) IsNot removingMember Then Return
            For index As Integer = 0 To occupiedInOrder.Count - 2
                compactionTargets.Add(occupiedInOrder(index).MotionPathCenter)
                occupiedInOrder(index + 1).StartMotionPathShift(compactionTargets(index), removingMember.MotionPathExitDuration)
            Next
        End Sub

        Private Sub NormalizeCompactedSlots(ByVal remainingCount As Integer)
            ' The visible survivors have already rolled into their new slots.
            ' Normalization swaps those controls for the lower-numbered logical
            ' members so the next entry uses the correct open slot. Carry the
            ' survivor angles across that swap or striped balls visibly jump
            ' back to the replacement controls' original randomized phases.
            Dim survivorImages As New Generic.List(Of Image)()
            Dim survivorRollAngles As New Generic.List(Of Single)()
            Dim unusedImages As New Generic.List(Of Image)()
            Dim unusedRollAngles As New Generic.List(Of Single)()
            For Each member As B2SPictureBox In members
                If occupiedMembers.Contains(member) Then
                    survivorImages.Add(member.BackgroundImage)
                    survivorRollAngles.Add(member.MotionPathRollAngle)
                Else
                    unusedImages.Add(member.BackgroundImage)
                    unusedRollAngles.Add(member.MotionPathRollAngle)
                End If
            Next
            occupiedMembers.Clear()
            For index As Integer = 0 To members.Count - 1
                ' Cancel every survivor shift, including the newly freed hidden
                ' member. Otherwise a queued add can reuse that member while
                ' its old shift timer is still active and misclassify the old
                ' completion as the new entry completion.
                If index < remainingCount AndAlso index < survivorRollAngles.Count Then
                    members(index).BackgroundImage = survivorImages(index)
                    members(index).MotionPathRollAngle = survivorRollAngles(index)
                Else
                    Dim unusedIndex As Integer = index - remainingCount
                    If unusedIndex >= 0 AndAlso unusedIndex < unusedRollAngles.Count Then
                        members(index).BackgroundImage = unusedImages(unusedIndex)
                        members(index).MotionPathRollAngle = unusedRollAngles(unusedIndex)
                    End If
                End If
                members(index).SetMotionPathPosition(members(index).MotionPathCenter)
                If index < remainingCount Then
                    If index < compactionTargets.Count Then members(index).SetMotionPathPosition(compactionTargets(index))
                    members(index).Visible = True
                    occupiedMembers.Add(members(index))
                Else
                    members(index).Visible = False
                End If
            Next
            compactionTargets.Clear()
            UpdateSourceAppearance()
        End Sub

        Private Sub UpdateSourceAppearance()
            If sourceAnchor Is Nothing Then Return
            For Each member As B2SPictureBox In members
                If member IsNot Nothing AndAlso Not occupiedMembers.Contains(member) AndAlso
                   Not activeEntries.Contains(member) AndAlso member IsNot activeRemoval Then
                    sourceAnchor.SetMotionPathSequenceSourceImage(member.BackgroundImage)
                    Return
                End If
            Next
        End Sub

        Private Sub EntryCompleted(ByVal sender As Object, ByVal e As EventArgs)
            Dim completedMember As B2SPictureBox = TryCast(sender, B2SPictureBox)
            If completedMember IsNot Nothing Then
                RemoveHandler completedMember.MotionPathCompleted, AddressOf EntryCompleted
                RemoveHandler completedMember.MotionPathLaunchSegmentCompleted, AddressOf MemberLaunchSegmentCompleted
            End If
            If completedMember IsNot Nothing AndAlso activeEntries.Remove(completedMember) Then
                occupiedMembers.Add(completedMember)
                If completedMember Is launchingMember Then
                    BeginSourceRespawn()
                End If
            End If
            ProcessPendingOperations()
        End Sub

        Private Sub RemovalCompleted(ByVal sender As Object, ByVal e As EventArgs)
            Dim completedMember As B2SPictureBox = TryCast(sender, B2SPictureBox)
            If completedMember IsNot Nothing Then RemoveHandler completedMember.MotionPathCompleted, AddressOf RemovalCompleted
            If completedMember Is activeRemoval Then
                occupiedMembers.Remove(completedMember)
                completedMember.Visible = False
                NormalizeCompactedSlots(occupiedMembers.Count)
                activeRemoval = Nothing
            End If
            ProcessPendingOperations()
        End Sub
    End Class
    Public Shared Property UsedRomGIStringIDs4Fantasy() As Generic.SortedList(Of Integer, B2SBaseBox()) = New Generic.SortedList(Of Integer, B2SBaseBox())
    Public Shared Property UsedRomMechIDs4Fantasy() As Generic.SortedList(Of Integer, B2SBaseBox()) = New Generic.SortedList(Of Integer, B2SBaseBox())

    Public Shared ReadOnly Property UsedTopRomIDType() As B2SBaseBox.eRomIDType
        Get
            Return If(B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy, UsedTopRomIDType4Fantasy, UsedTopRomIDType4Authentic)
        End Get
    End Property
    Public Shared ReadOnly Property UsedSecondRomIDType() As B2SBaseBox.eRomIDType
        Get
            Return If(B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy, UsedSecondRomIDType4Fantasy, UsedSecondRomIDType4Authentic)
        End Get
    End Property
    Public Shared Property UsedTopRomIDType4Authentic() As B2SBaseBox.eRomIDType = B2SBaseBox.eRomIDType.NotDefined
    Public Shared Property UsedSecondRomIDType4Authentic() As B2SBaseBox.eRomIDType = B2SBaseBox.eRomIDType.NotDefined
    Public Shared Property UsedTopRomIDType4Fantasy() As B2SBaseBox.eRomIDType = B2SBaseBox.eRomIDType.NotDefined
    Public Shared Property UsedSecondRomIDType4Fantasy() As B2SBaseBox.eRomIDType = B2SBaseBox.eRomIDType.NotDefined

    Public Shared Property UsedRomReelLampIDs() As Generic.SortedList(Of Integer, B2SReelBox()) = New Generic.SortedList(Of Integer, B2SReelBox())

    Public Shared Property UsedAnimationLampIDs() As AnimationCollection = New AnimationCollection()
    Public Shared Property UsedRandomAnimationLampIDs() As AnimationCollection = New AnimationCollection()
    Public Shared Property UsedAnimationSolenoidIDs() As AnimationCollection = New AnimationCollection()
    Public Shared Property UsedRandomAnimationSolenoidIDs() As AnimationCollection = New AnimationCollection()
    Public Shared Property UsedAnimationGIStringIDs() As AnimationCollection = New AnimationCollection()
    Public Shared Property UsedRandomAnimationGIStringIDs() As AnimationCollection = New AnimationCollection()

    Public Shared Property TableName() As String = String.Empty
    Public Shared Property TableFileName() As String = String.Empty
    Public Shared Property BackglassFileName() As String = String.Empty
    Public Shared Property TableType() As Integer = 0
    Public Shared Property DMDType() As Integer = 0
    Public Shared Property GrillHeight() As Integer = 0
    Public Shared Property SmallGrillHeight() As Integer = 0
    Public Shared Property DMDDefaultLocation() As Point = New Point(0, 0)
    Public Shared Property DualBackglass() As Boolean = False
#If B2S = "DLL" Then
    Private Shared Property _TestMode() As Boolean = False
    Public Shared Property TestMode() As Boolean
        Get
            Return _TestMode
        End Get
        Set(ByVal value As Boolean)
            _TestMode = value
            IsInfoDirty = True
        End Set
    End Property
#End If
    Public Class PictureBoxCollection
        Inherits Generic.SortedList(Of String, B2SPictureBox)

        Public Sub New()
            MyBase.New(StringComparer.OrdinalIgnoreCase)
        End Sub

        Public Shadows Sub Add(ByVal value As B2SPictureBox, Optional ByVal dualmode As B2SData.eDualMode = eDualMode.Both)
            If Not MyBase.ContainsKey(value.Name) Then MyBase.Add(value.Name, value)
            If value.RomID > 0 Then
#If B2S = "DLL" Then
                IsInfoDirty = True
#End If
                Dim UsedRomIDs4Authentic As Generic.SortedList(Of Integer, B2SBaseBox()) = Nothing
                Dim UsedRomIDs4Fantasy As Generic.SortedList(Of Integer, B2SBaseBox()) = Nothing
                If value.RomIDType = B2SBaseBox.eRomIDType.Lamp Then
                    UsedRomIDs4Authentic = UsedRomLampIDs4Authentic
                    UsedRomIDs4Fantasy = UsedRomLampIDs4Fantasy
                ElseIf value.RomIDType = B2SBaseBox.eRomIDType.Solenoid Then
                    UsedRomIDs4Authentic = UsedRomSolenoidIDs4Authentic
                    UsedRomIDs4Fantasy = UsedRomSolenoidIDs4Fantasy
                ElseIf value.RomIDType = B2SBaseBox.eRomIDType.GIString Then
                    UsedRomIDs4Authentic = UsedRomGIStringIDs4Authentic
                    UsedRomIDs4Fantasy = UsedRomGIStringIDs4Fantasy
                ElseIf value.RomIDType = B2SBaseBox.eRomIDType.Mech Then
                    UsedRomIDs4Authentic = UsedRomMechIDs4Authentic
                    UsedRomIDs4Fantasy = UsedRomMechIDs4Fantasy
                ElseIf value.RomIDType = B2SBaseBox.eRomIDType.Switch Then
                    If Not UsedRomSwitchIDs.ContainsKey(value.RomID) Then
                        UsedRomSwitchIDs.Add(value.RomID, New B2SBaseBox() {value})
                    End If
#If B2S = "EXE" Then
                    AnnounceTesterSwitches()
#End If
                End If
                If UsedRomIDs4Authentic IsNot Nothing AndAlso (dualmode = eDualMode.Both OrElse dualmode = eDualMode.Authentic) Then
                    If UsedRomIDs4Authentic.ContainsKey(value.RomID) Then
                        Dim baseboxes As B2SBaseBox() = UsedRomIDs4Authentic(value.RomID)
                        ReDim Preserve baseboxes(baseboxes.Length)
                        baseboxes(baseboxes.Length - 1) = value
                        UsedRomIDs4Authentic(value.RomID) = baseboxes
                    Else
                        Dim baseboxes As B2SBaseBox()
                        ReDim baseboxes(0)
                        baseboxes(0) = value
                        UsedRomIDs4Authentic.Add(value.RomID, baseboxes)
                    End If
                End If
                If UsedRomIDs4Fantasy IsNot Nothing AndAlso (dualmode = eDualMode.Both OrElse dualmode = eDualMode.Fantasy) Then
                    If UsedRomIDs4Fantasy.ContainsKey(value.RomID) Then
                        Dim baseboxes As B2SBaseBox() = UsedRomIDs4Fantasy(value.RomID)
                        ReDim Preserve baseboxes(baseboxes.Length)
                        baseboxes(baseboxes.Length - 1) = value
                        UsedRomIDs4Fantasy(value.RomID) = baseboxes
                    Else
                        Dim baseboxes As B2SBaseBox()
                        ReDim baseboxes(0)
                        baseboxes(0) = value
                        UsedRomIDs4Fantasy.Add(value.RomID, baseboxes)
                    End If
                End If
            End If
        End Sub
    End Class
    Public Class ReelBoxCollection
        Inherits Generic.Dictionary(Of String, B2SReelBox)

        Public Sub New()
            MyBase.New(StringComparer.OrdinalIgnoreCase)
        End Sub

        Public Shadows Sub Add(value As B2SReelBox)
            If Not MyBase.ContainsKey(value.Name) Then MyBase.Add(value.Name, value)
            If value.RomID > 0 Then
                If UsedRomReelLampIDs.ContainsKey(value.RomID) Then
                    Dim reelboxes As B2SReelBox() = UsedRomReelLampIDs(value.RomID)
                    ReDim Preserve reelboxes(reelboxes.Length)
                    reelboxes(reelboxes.Length - 1) = value
                    UsedRomReelLampIDs(value.RomID) = reelboxes
                Else
                    Dim reelboxes As B2SReelBox()
                    ReDim reelboxes(0)
                    reelboxes(0) = value
                    UsedRomReelLampIDs.Add(value.RomID, reelboxes)
                End If
            End If
        End Sub
    End Class
    Public Class ZOrderCollection
        Inherits Generic.SortedList(Of Integer, B2SPictureBox())

        Public Shadows Sub Add(value As B2SPictureBox)
            If value.ZOrder > 0 Then
                If MyBase.ContainsKey(value.ZOrder) Then
                    Dim pictureboxes As B2SPictureBox() = MyBase.Item(value.ZOrder)
                    ReDim Preserve pictureboxes(pictureboxes.Length)
                    pictureboxes(pictureboxes.Length - 1) = value
                    MyBase.Item(value.ZOrder) = pictureboxes
                Else
                    Dim pictureboxes As B2SPictureBox()
                    ReDim pictureboxes(0)
                    pictureboxes(0) = value
                    MyBase.Add(value.ZOrder, pictureboxes)
                End If
            End If
        End Sub
    End Class

    Public Class AnimationInfo
        Public AnimationName As String = String.Empty
        Public Inverted As Boolean = False

        Public Sub New(ByVal _name As String, ByVal _inverted As Boolean)
            AnimationName = _name
            Inverted = _inverted
        End Sub
    End Class
    Public Class AnimationCollection
        Inherits Generic.Dictionary(Of Integer, AnimationInfo())

        Public Shadows Sub Add(key As Integer, value As AnimationInfo)
#If B2S = "DLL" Then
            IsInfoDirty = True
#End If
            If Not Me.ContainsKey(key) Then
                MyBase.Add(key, New AnimationInfo() {value})
            Else
                Dim infos As AnimationInfo() = Me(key)
                ReDim Preserve infos(infos.Length)
                infos(infos.Length - 1) = value
                Me(key) = infos
            End If
        End Sub
    End Class

    Public Class IlluminationGroupCollection
        Inherits Generic.Dictionary(Of String, B2SPictureBox())

        Public Sub New()
            MyBase.New(StringComparer.OrdinalIgnoreCase)
        End Sub

        Public Shadows Sub Add(ByVal value As B2SPictureBox)
            If Not String.IsNullOrEmpty(value.GroupName) Then
                If Not Me.ContainsKey(value.GroupName) Then
                    MyBase.Add(value.GroupName, New B2SPictureBox() {value})
                Else
                    Dim picboxes As B2SPictureBox() = Me(value.GroupName)
                    ReDim Preserve picboxes(picboxes.Length)
                    picboxes(picboxes.Length - 1) = value
                    Me(value.GroupName) = picboxes
                End If
            End If
        End Sub
    End Class
#If B2S = "DLL" Then
    Public Shared ReadOnly Property GetLampsData() As Boolean
        Get
            Static ret As Boolean = False
            If IsLampsInfoDirty Then
                IsLampsInfoDirty = False
                ret = (IsBackglassRunning AndAlso
                       (IsBackglassStartedAsEXE OrElse UseRomLamps OrElse UsedMotionPathLampIDs.Count > 0 OrElse UsedMotionPathRemoveLampIDs.Count > 0 OrElse PivotLampIDs.Count > 0 OrElse UseAnimationLamps OrElse TestMode OrElse B2SSettings.IsLampsStateLogOn OrElse B2SStatistics.LogStatistics) AndAlso
                       Not B2SSettings.AllOff AndAlso Not B2SSettings.LampsOff)
            End If
            Return ret
        End Get
    End Property
    Public Shared ReadOnly Property GetSolenoidsData() As Boolean
        Get
            Static ret As Boolean = False
            If IsSolenoidsInfoDirty Then
                IsSolenoidsInfoDirty = False
                ret = (IsBackglassRunning AndAlso
                       (IsBackglassStartedAsEXE OrElse UseRomSolenoids OrElse UsedMotionPathSolenoidIDs.Count > 0 OrElse UsedMotionPathRemoveSolenoidIDs.Count > 0 OrElse PivotSolenoidIDs.Count > 0 OrElse UseAnimationSolenoids OrElse TestMode OrElse B2SSettings.IsSolenoidsStateLogOn OrElse B2SStatistics.LogStatistics) AndAlso
                       Not B2SSettings.AllOff AndAlso Not B2SSettings.SolenoidsOff)
            End If
            Return ret
        End Get
    End Property
    Public Shared ReadOnly Property GetGIStringsData() As Boolean
        Get
            Static ret As Boolean = False
            If IsGIStringsInfoDirty Then
                IsGIStringsInfoDirty = False
                ret = (IsBackglassRunning AndAlso
                       (IsBackglassStartedAsEXE OrElse UseRomGIStrings OrElse UseAnimationGIStrings OrElse TestMode OrElse B2SSettings.IsGIStringsStateLogOn OrElse B2SStatistics.LogStatistics) AndAlso
                       Not B2SSettings.AllOff AndAlso Not B2SSettings.GIStringsOff)
            End If
            Return ret
        End Get
    End Property
    Public Shared ReadOnly Property GetLEDsData() As Boolean
        Get
            Static ret As Boolean = False
            If IsLEDInfoDirty Then
                IsLEDInfoDirty = False
                ret = (IsBackglassRunning AndAlso
                       (IsBackglassStartedAsEXE OrElse UseLEDs OrElse UseLEDDisplays OrElse UseReels OrElse B2SSettings.IsLEDsStateLogOn) AndAlso
                       Not B2SSettings.AllOff AndAlso Not B2SSettings.LEDsOff)
            End If
            Return ret
        End Get
    End Property
#End If
    Public Shared ReadOnly Property UseRomLamps() As Boolean
        Get
            If B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy Then
                Return (UsedRomLampIDs4Fantasy.Count > 0 OrElse UsedTopRomIDType4Fantasy = B2SBaseBox.eRomIDType.Lamp OrElse UsedSecondRomIDType4Fantasy = B2SBaseBox.eRomIDType.Lamp)
            Else
                Return (UsedRomLampIDs4Authentic.Count > 0 OrElse UsedTopRomIDType4Authentic = B2SBaseBox.eRomIDType.Lamp OrElse UsedSecondRomIDType4Authentic = B2SBaseBox.eRomIDType.Lamp)
            End If
        End Get
    End Property
    Public Shared ReadOnly Property UseRomSolenoids() As Boolean
        Get
            If B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy Then
                Return (UsedRomSolenoidIDs4Fantasy.Count > 0 OrElse UsedTopRomIDType4Fantasy = B2SBaseBox.eRomIDType.Solenoid OrElse UsedSecondRomIDType4Fantasy = B2SBaseBox.eRomIDType.Solenoid)
            Else
                Return (UsedRomSolenoidIDs4Authentic.Count > 0 OrElse UsedTopRomIDType4Authentic = B2SBaseBox.eRomIDType.Solenoid OrElse UsedSecondRomIDType4Authentic = B2SBaseBox.eRomIDType.Solenoid)
            End If
        End Get
    End Property
    Public Shared ReadOnly Property UseRomGIStrings() As Boolean
        Get
            If B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy Then
                Return (UsedRomGIStringIDs4Fantasy.Count > 0 OrElse UsedTopRomIDType4Fantasy = B2SBaseBox.eRomIDType.GIString OrElse UsedSecondRomIDType4Fantasy = B2SBaseBox.eRomIDType.GIString)
            Else
                Return (UsedRomGIStringIDs4Authentic.Count > 0 OrElse UsedTopRomIDType4Authentic = B2SBaseBox.eRomIDType.GIString OrElse UsedSecondRomIDType4Authentic = B2SBaseBox.eRomIDType.GIString)
            End If
        End Get
    End Property
    Public Shared ReadOnly Property UseRomMechs() As Boolean
        Get
            If B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy Then
                Return (UsedRomMechIDs4Fantasy.Count > 0)
            Else
                Return (UsedRomMechIDs4Authentic.Count > 0)
            End If
        End Get
    End Property

    Public Shared ReadOnly Property UseAnimationLamps() As Boolean
        Get
            Return (UsedAnimationLampIDs.Count > 0 OrElse UsedRandomAnimationLampIDs.Count > 0)
        End Get
    End Property
    Public Shared ReadOnly Property UseAnimationSolenoids() As Boolean
        Get
            Return (UsedAnimationSolenoidIDs.Count > 0 OrElse UsedRandomAnimationSolenoidIDs.Count > 0)
        End Get
    End Property
    Public Shared ReadOnly Property UseAnimationGIStrings() As Boolean
        Get
            Return (UsedAnimationGIStringIDs.Count > 0 OrElse UsedRandomAnimationGIStringIDs.Count > 0)
        End Get
    End Property

    Public Shared ReadOnly Property UseRomReelLamps() As Boolean
        Get
            Return (UsedRomReelLampIDs.Count > 0)
        End Get
    End Property

    Public Shared ReadOnly Property UseLEDs() As Boolean
        Get
            Return (LEDs.Count > 0)
        End Get
    End Property
    Public Shared ReadOnly Property UseLEDDisplays() As Boolean
        Get
            Return (LEDDisplays.Count > 0)
        End Get
    End Property

    Public Shared ReadOnly Property UseReels() As Boolean
        Get
            Return (Reels.Count > 0)
        End Get
    End Property

    Private Shared _ScoreMaxDigit As Integer = 0
    Public Shared Property ScoreMaxDigit() As Integer
        Get
            Return _ScoreMaxDigit
        End Get
        Set(value As Integer)
            If _ScoreMaxDigit < value Then
                _ScoreMaxDigit = value
            End If
        End Set
    End Property

    Public Shared Property Players() As B2SPlayer = New B2SPlayer()
    Public Shared Property IsAPlayerAdded() As Boolean = False

    Public Shared Property Reels() As ReelBoxCollection = New ReelBoxCollection
    Public Shared Property ReelDisplays() As Generic.Dictionary(Of Integer, B2SReelDisplay) = New Generic.Dictionary(Of Integer, B2SReelDisplay)
    Public Shared Property ReelImages() As Generic.Dictionary(Of String, Image) = New Generic.Dictionary(Of String, Image)(StringComparer.OrdinalIgnoreCase)
    Public Shared Property ReelIntermediateImages() As Generic.Dictionary(Of String, Image) = New Generic.Dictionary(Of String, Image)(StringComparer.OrdinalIgnoreCase)
    Public Shared Property ReelIlluImages() As Generic.Dictionary(Of String, Image) = New Generic.Dictionary(Of String, Image)(StringComparer.OrdinalIgnoreCase)
    Public Shared Property ReelIntermediateIlluImages() As Generic.Dictionary(Of String, Image) = New Generic.Dictionary(Of String, Image)(StringComparer.OrdinalIgnoreCase)

    Public Shared Property Sounds() As Generic.Dictionary(Of String, Byte()) = New Generic.Dictionary(Of String, Byte())(StringComparer.OrdinalIgnoreCase)

    Public Shared Property LEDs() As Generic.Dictionary(Of String, B2SLEDBox) = New Generic.Dictionary(Of String, B2SLEDBox)(StringComparer.OrdinalIgnoreCase)
    Public Shared Property LEDAreas() As Generic.Dictionary(Of String, LEDAreaInfo) = New Generic.Dictionary(Of String, LEDAreaInfo)(StringComparer.OrdinalIgnoreCase)
    Public Class LEDAreaInfo
        Public Rect As Rectangle = Nothing
        Public IsOnDMD As Boolean = False

        Public Sub New(ByVal _rect As Rectangle, ByVal _isOnDMD As Boolean)
            Rect = _rect
            IsOnDMD = _isOnDMD
        End Sub
    End Class

    Public Shared Property LEDDisplays() As Generic.Dictionary(Of String, Dream7Display) = New Generic.Dictionary(Of String, Dream7Display)(StringComparer.OrdinalIgnoreCase)
    Public Shared Property LEDDisplayDigits() As Generic.Dictionary(Of Integer, LEDDisplayDigitLocation) = New Generic.Dictionary(Of Integer, LEDDisplayDigitLocation)
    Public Class LEDDisplayDigitLocation
        Public LEDDisplay As Dream7Display = Nothing
        Public Digit As Integer = 0
        Public LEDDisplayID As Integer = 0

        Public Sub New(ByRef _leddisplay As Dream7Display, ByVal _digit As Integer, ByVal _ledDisplayID As Integer)
            LEDDisplay = _leddisplay
            Digit = _digit
            LEDDisplayID = _ledDisplayID
        End Sub
    End Class

    Public Shared Property Illuminations() As PictureBoxCollection = New PictureBoxCollection()
    Public Shared Property DMDIlluminations() As PictureBoxCollection = New PictureBoxCollection()

    Public Shared Property UseIlluminationLocks() As Boolean = False
    Public Shared Property IlluminationGroups() As IlluminationGroupCollection = New IlluminationGroupCollection()
    Public Shared Property IlluminationLocks() As Generic.Dictionary(Of String, Integer) = New Generic.Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

    Public Shared Property UseZOrder() As Boolean = False
    Public Shared Property UseDMDZOrder() As Boolean = False
    Public Shared Property ZOrderImages() As ZOrderCollection = New ZOrderCollection()
    Public Shared Property ZOrderDMDImages() As ZOrderCollection = New ZOrderCollection()

    Public Shared Property UseRotatingImage() As Boolean = False
    Public Shared Property UseMechRotatingImage() As Boolean = False
    Public Shared Property RotatingImages() As Generic.Dictionary(Of Integer, Generic.Dictionary(Of Integer, Image)) = New Generic.Dictionary(Of Integer, Generic.Dictionary(Of Integer, Image))
    Public Shared Property RotatingPictureBox() As Generic.Dictionary(Of Integer, B2SPictureBox) = New Generic.Dictionary(Of Integer, B2SPictureBox)

    Public Shared Property IsHyperpinRunning() As Boolean = False

    Public Shared Property led8Seg() As Generic.List(Of PointF()) = New Generic.List(Of PointF())
    Public Shared Property led10Seg() As Generic.List(Of PointF()) = New Generic.List(Of PointF())
    Public Shared Property led14Seg() As Generic.List(Of PointF()) = New Generic.List(Of PointF())
    Public Shared Property led16Seg() As Generic.List(Of PointF()) = New Generic.List(Of PointF())
    Public Shared Property ledCoordMax() As Integer

    Public Shared Sub ClearAll(Optional ByVal donotclearnames As Boolean = False)
#If B2S = "DLL" Then
        IsInfoDirty = True
#End If
        If Not donotclearnames Then
            TableName = String.Empty
            TableFileName = String.Empty
            BackglassFileName = String.Empty
#If B2S = "DLL" Then
        Else
            LaunchBackglass = True
            IsBackglassVisible = False
            IsBackglassStartedAsEXE = False
#End If
        End If
        TableType = 0
        DMDType = 0
        GrillHeight = 0
        SmallGrillHeight = 0
        DMDDefaultLocation = New Point(0, 0)
        DualBackglass = False
        'TestMode = False ' do not add the test mode here
        UsedTopRomIDType4Authentic = B2SBaseBox.eRomIDType.NotDefined
        UsedTopRomIDType4Fantasy = B2SBaseBox.eRomIDType.NotDefined
        UsedSecondRomIDType4Authentic = B2SBaseBox.eRomIDType.NotDefined
        UsedSecondRomIDType4Fantasy = B2SBaseBox.eRomIDType.NotDefined
        UsedRomLampIDs4Authentic.Clear()
        UsedRomSolenoidIDs4Authentic.Clear()
        UsedRomGIStringIDs4Authentic.Clear()
        UsedRomMechIDs4Authentic.Clear()
        UsedRomSwitchIDs.Clear()
        TesterSwitchesEnabled = False
        UsedRomLampIDs4Fantasy.Clear()
        UsedRomSolenoidIDs4Fantasy.Clear()
        UsedMotionPathSolenoidIDs.Clear()
        UsedMotionPathLampIDs.Clear()
        UsedMotionPathB2SIDs.Clear()
        UsedMotionPathStopB2SIDs.Clear()
        UsedMotionPathResumeB2SIDs.Clear()
        UsedMotionPathRemoveSolenoidIDs.Clear()
        UsedMotionPathRemoveLampIDs.Clear()
        UsedMotionPathRemoveB2SIDs.Clear()
        For Each sequenceState As MotionPathSequenceState In MotionPathSequenceGroups.Values
            sequenceState.Clear()
        Next
        MotionPathSequenceGroups.Clear()
        ExternalPositionGrids.Clear()
        ExternalPositionGridTargets.Clear()
        ExternalPivotGroups.Clear()
        ExternalPivotTargets.Clear()
        PivotSolenoidIDs.Clear()
        PivotLampIDs.Clear()
        PivotB2SIDs.Clear()
        StartupPivotPictures.Clear()
        PivotPicturesByName.Clear()
        For Each state As PhysicsBallState In PhysicsBalls
            state.Dispose()
        Next
        PhysicsBalls.Clear()
        PhysicsLauncherSolenoidIDs.Clear()
        PhysicsLauncherB2SIDs.Clear()
        UsedRomGIStringIDs4Fantasy.Clear()
        UsedRomMechIDs4Fantasy.Clear()
        UsedRomReelLampIDs.Clear()
        UsedAnimationLampIDs.Clear()
        UsedRandomAnimationLampIDs.Clear()
        UsedAnimationSolenoidIDs.Clear()
        UsedRandomAnimationSolenoidIDs.Clear()
        UsedAnimationGIStringIDs.Clear()
        UsedRandomAnimationGIStringIDs.Clear()
        IsAPlayerAdded = False
        Players.Clear()
        For Each r In Reels : r.Value.Dispose() : Next
        Reels.Clear()
        For Each rd In ReelDisplays : rd.Value.Dispose() : Next
        ReelDisplays.Clear()
        ReelImages.Clear()
        ReelIntermediateImages.Clear()
        ReelIlluImages.Clear()
        ReelIntermediateIlluImages.Clear()
        'For Each s In Sounds : s.Value.Dispose() : Next
        Sounds.Clear()
        LEDs.Clear()
        LEDAreas.Clear()
        LEDDisplays.Clear()
        LEDDisplayDigits.Clear()
        Illuminations.Clear()
        DMDIlluminations.Clear()
        UseIlluminationLocks = False
        IlluminationGroups.Clear()
        IlluminationLocks.Clear()
        UseZOrder = False
        UseDMDZOrder = False
        ZOrderImages.Clear()
        ZOrderDMDImages.Clear()
        UseRotatingImage = False
        UseMechRotatingImage = False
        For Each r As KeyValuePair(Of Integer, Generic.Dictionary(Of Integer, Image)) In RotatingImages : r.Value.Clear() : Next
        RotatingImages.Clear()
        RotatingPictureBox.Clear()
    End Sub

    Private Class ExternalPositionGridState
        Private representative As B2SPictureBox
        Private duration As Integer = 45
        Private ReadOnly targets As New Generic.Dictionary(Of String, B2SPictureBox)(StringComparer.OrdinalIgnoreCase)
        Private pendingTargetName As String = String.Empty
        Private ReadOnly flushTimer As New Windows.Forms.Timer()

        Public Sub New()
            AddHandler flushTimer.Tick, AddressOf FlushPendingTarget
        End Sub

        Public Sub Register(ByVal pictureBox As B2SPictureBox, ByVal targetName As String, ByVal isRepresentative As Boolean, ByVal moveDuration As Integer)
            targets(targetName) = pictureBox
            If pictureBox IsNot representative Then pictureBox.Visible = False
            If isRepresentative Then representative = pictureBox
            duration = Math.Max(10, Math.Min(1000, moveDuration))
        End Sub

        Public Sub MoveTo(ByVal targetName As String)
            If representative Is Nothing OrElse Not targets.ContainsKey(targetName) Then Return
            flushTimer.Stop()
            ' Keep a short look-ahead window open beyond the movement duration.
            ' Natural gaps between source markers must not be mistaken for the
            ' end of motion or the renderer alternates between curved motion
            ' and single-marker catch-up steps.
            flushTimer.Interval = Math.Max(20, Math.Min(2000, duration * 2))
            If pendingTargetName.Length > 0 AndAlso targets.ContainsKey(pendingTargetName) Then
                representative.StartExternalGridShift(targets(pendingTargetName).MotionPathCenter,
                                                      duration,
                                                      targets(targetName).MotionPathCenter)
            End If
            pendingTargetName = targetName
            flushTimer.Start()
        End Sub

        Private Sub FlushPendingTarget(ByVal sender As Object, ByVal e As EventArgs)
            flushTimer.Stop()
            If representative Is Nothing OrElse pendingTargetName.Length = 0 OrElse Not targets.ContainsKey(pendingTargetName) Then Return
            Dim target As PointF = targets(pendingTargetName).MotionPathCenter
            pendingTargetName = String.Empty
            representative.StartExternalGridShift(target, duration, target)
        End Sub
    End Class

    Private Class PhysicsBallState
        Implements IDisposable

        Private Const PhysicsSubstepSeconds As Single = 0.001F
        Private Const ShallowSurfaceSlopeLimit As Single = 0.35F
        Private Const ShallowSurfaceRollMultiplier As Single = 2.0F
        Private Const BoundaryScatterDegrees As Single = 10.0F
        Private Const BoundaryScatterMinimumImpactSpeed As Single = 150.0F
        Private ReadOnly ball As B2SPictureBox
        Private ReadOnly flipperName As String
        Private ReadOnly bounds As RectangleF
        Private ReadOnly gravity As Single
        Private ReadOnly flipperStrength As Single
        Private ReadOnly boundaryBounce As Single
        Private ReadOnly boundaryPaths As New Generic.List(Of Generic.List(Of PointF))()
        Private ReadOnly boundarySegmentBounces As New Generic.List(Of Generic.List(Of Single))()
        Private ReadOnly obstacles As New Generic.List(Of RectangleF)()
        Private ReadOnly switchZones As New Generic.List(Of PhysicsSwitchZone)()
        Private ReadOnly switchZoneInside As New Generic.List(Of Boolean)()
        Private ReadOnly launcher As PhysicsLauncher
        Private Shared ReadOnly launcherRandom As New Random()
        Private Shared boundaryScatterRandom As New Random()
        Private launcherArmed As Boolean = True
        Private launcherHolding As Boolean
        Private launcherExitedCapture As Boolean
        Private ReadOnly authoredBallWidth As Single
        Private ReadOnly authoredBallHeight As Single
        Private ReadOnly authoredBallCenter As PointF
        Private ReadOnly timer As New Windows.Forms.Timer() With {.Interval = 16}
        Private ReadOnly clock As New Diagnostics.Stopwatch()
        Private velocity As PointF = PointF.Empty
        Private lastFlipperAngle As Single
        Private hasLastFlipperAngle As Boolean
        Public Sub New(ByVal pictureBox As B2SPictureBox, ByVal pivotName As String, ByVal playBounds As RectangleF,
                       ByVal gravityValue As Single, ByVal strengthValue As Single, ByVal bounceValue As Single,
                        ByVal returnBoundaries As Generic.List(Of Generic.List(Of PointF)),
                        ByVal returnObstacles As Generic.List(Of RectangleF),
                        ByVal returnSwitchZones As Generic.List(Of PhysicsSwitchZone),
                        ByVal returnLauncher As PhysicsLauncher,
                        ByVal returnBoundarySegmentBounces As Generic.List(Of Generic.List(Of Single)))
            ball = pictureBox
            flipperName = pivotName
            bounds = playBounds
            gravity = gravityValue
            flipperStrength = strengthValue
            boundaryBounce = bounceValue
            authoredBallWidth = If(pictureBox.Width > 0, pictureBox.Width, pictureBox.RectangleF.Width)
            authoredBallHeight = If(pictureBox.Height > 0, pictureBox.Height, pictureBox.RectangleF.Height)
            ' RectangleF is initialized later by screen scaling. The control
            ' bounds already contain the ball's saved editor placement here.
            authoredBallCenter = New PointF(pictureBox.Left + pictureBox.Width / 2.0F,
                                            pictureBox.Top + pictureBox.Height / 2.0F)
            If returnBoundaries IsNot Nothing Then
                For pathIndex As Integer = 0 To returnBoundaries.Count - 1
                    Dim path As Generic.List(Of PointF) = returnBoundaries(pathIndex)
                    If path IsNot Nothing AndAlso path.Count >= 2 Then
                        boundaryPaths.Add(New Generic.List(Of PointF)(path))
                        Dim values As New Generic.List(Of Single)()
                        Dim saved As Generic.List(Of Single) = If(returnBoundarySegmentBounces IsNot Nothing AndAlso
                                                                  pathIndex < returnBoundarySegmentBounces.Count,
                                                                  returnBoundarySegmentBounces(pathIndex), Nothing)
                        For segmentIndex As Integer = 0 To path.Count - 2
                            Dim value As Single = If(saved IsNot Nothing AndAlso segmentIndex < saved.Count, saved(segmentIndex), -1.0F)
                            values.Add(Math.Max(-1.0F, Math.Min(3.0F, value)))
                        Next
                        boundarySegmentBounces.Add(values)
                    End If
                Next
            End If
            If returnObstacles IsNot Nothing Then
                For Each obstacle As RectangleF In returnObstacles
                    If obstacle.Width > 0.0F AndAlso obstacle.Height > 0.0F Then obstacles.Add(obstacle)
                Next
            End If
            If returnSwitchZones IsNot Nothing Then
                For Each zone As PhysicsSwitchZone In returnSwitchZones
                    If zone IsNot Nothing AndAlso zone.Bounds.Width > 0.0F AndAlso zone.Bounds.Height > 0.0F AndAlso zone.SwitchID > 0 Then
                        switchZones.Add(New PhysicsSwitchZone With {.Bounds = zone.Bounds, .SwitchID = zone.SwitchID, .Angle = NormalizeSwitchAngle(zone.Angle)})
                        switchZoneInside.Add(False)
                    End If
                Next
            End If
            If returnLauncher IsNot Nothing Then
                launcher = New PhysicsLauncher With {.TriggerType = returnLauncher.TriggerType, .TriggerID = returnLauncher.TriggerID,
                    .Origin = returnLauncher.Origin, .Angle = returnLauncher.Angle, .Strength = returnLauncher.Strength,
                    .RandomAngle = returnLauncher.RandomAngle, .RandomStrength = returnLauncher.RandomStrength,
                    .CaptureRadius = Math.Max(5.0F, returnLauncher.CaptureRadius), .FollowPivot = returnLauncher.FollowPivot}
                launcherHolding = True
            End If
            AddHandler timer.Tick, AddressOf Tick
        End Sub

        Public Sub Launch()
            If launcher Is Nothing OrElse Not launcherArmed Then Return
            Dim scaleX As Single = If(authoredBallWidth <= 0.0F, 1.0F, ball.RectangleF.Width / authoredBallWidth)
            Dim scaleY As Single = If(authoredBallHeight <= 0.0F, 1.0F, ball.RectangleF.Height / authoredBallHeight)
            Dim randomAngleOffset As Single = CSng((launcherRandom.NextDouble() * 2.0R - 1.0R) * launcher.RandomAngle)
            Dim randomStrengthFactor As Single = 1.0F + CSng((launcherRandom.NextDouble() * 2.0R - 1.0R) * launcher.RandomStrength / 100.0R)
            Dim origin As PointF = PointF.Empty, angle As Single = 0.0F
            GetLauncherPose(scaleX, scaleY, origin, angle)
            Dim radians As Double = (angle + randomAngleOffset) * Math.PI / 180.0R
            Dim launchStrength As Single = Math.Max(0.0F, launcher.Strength * randomStrengthFactor)
            ball.SetMotionPathPosition(origin)
            velocity = New PointF(CSng(Math.Cos(radians)) * launchStrength * scaleX, CSng(Math.Sin(radians)) * launchStrength * scaleY)
            launcherArmed = False
            launcherHolding = False
            launcherExitedCapture = False
            For index As Integer = 0 To switchZoneInside.Count - 1
                switchZoneInside(index) = False
            Next
        End Sub

        Public Sub Start()
            ball.Visible = True
            clock.Restart()
            timer.Start()
        End Sub

        Private Sub Tick(ByVal sender As Object, ByVal e As EventArgs)
            Dim elapsed As Single = CSng(Math.Min(0.04, Math.Max(0.001, clock.Elapsed.TotalSeconds)))
            clock.Restart()
            If ball.RectangleF.Width <= 0.0F OrElse ball.RectangleF.Height <= 0.0F Then Return
            Dim flipper As B2SPictureBox = FindPivotPicture(flipperName)
            Dim angularVelocity As Single = 0.0F
            If flipper IsNot Nothing Then
                If hasLastFlipperAngle Then angularVelocity = CSng((flipper.RotationAngle - lastFlipperAngle) * Math.PI / 180.0F / elapsed)
                lastFlipperAngle = flipper.RotationAngle
                hasLastFlipperAngle = True
            End If
            Dim steps As Integer = Math.Max(1, CInt(Math.Ceiling(elapsed / PhysicsSubstepSeconds)))
            Dim stepTime As Single = elapsed / steps
            For index As Integer = 1 To steps
                StepPhysics(stepTime, flipper, angularVelocity)
            Next
        End Sub

        Private Sub StepPhysics(ByVal elapsed As Single, ByVal flipper As B2SPictureBox, ByVal angularVelocity As Single)
            Dim center As PointF = ball.MotionPathCenter
            Dim scaleX As Single = If(authoredBallWidth <= 0.0F, 1.0F, ball.RectangleF.Width / authoredBallWidth)
            Dim scaleY As Single = If(authoredBallHeight <= 0.0F, 1.0F, ball.RectangleF.Height / authoredBallHeight)
            If launcherHolding AndAlso launcher IsNot Nothing Then
                Dim origin As PointF = PointF.Empty, angle As Single = 0.0F
                GetLauncherPose(scaleX, scaleY, origin, angle)
                ball.SetMotionPathPosition(origin)
                velocity = PointF.Empty
                Return
            End If
            Dim runtimeBounds As RectangleF = RectangleF.FromLTRB(bounds.Left * scaleX, bounds.Top * scaleY, bounds.Right * scaleX, bounds.Bottom * scaleY)
            Dim previousCenter As PointF = center
            velocity.Y += gravity * scaleY * elapsed
            center.X += velocity.X * elapsed
            center.Y += velocity.Y * elapsed
            Dim radius As Single = Math.Max(2.0F, Math.Min(ball.RectangleF.Width, ball.RectangleF.Height) * 0.43F)
            If center.X - radius < runtimeBounds.Left Then center.X = runtimeBounds.Left + radius : velocity.X = Math.Abs(velocity.X) * 0.55F
            If center.X + radius > runtimeBounds.Right Then center.X = runtimeBounds.Right - radius : velocity.X = -Math.Abs(velocity.X) * 0.55F
            If center.Y - radius < runtimeBounds.Top Then center.Y = runtimeBounds.Top + radius : velocity.Y = Math.Abs(velocity.Y) * 0.55F
            If center.Y + radius > runtimeBounds.Bottom Then center.Y = runtimeBounds.Bottom - radius : velocity.Y = -Math.Abs(velocity.Y) * 0.55F
            ' An attached launcher uses a generic rotating snippet, not a flipper
            ' collision surface. Legacy physics balls keep their original contact.
            If flipper IsNot Nothing AndAlso (launcher Is Nothing OrElse Not launcher.FollowPivot) Then
                ResolveFlipperCollision(center, radius, velocity, flipper, angularVelocity)
            End If
            For pathIndex As Integer = 0 To boundaryPaths.Count - 1
                Dim path As Generic.List(Of PointF) = boundaryPaths(pathIndex)
                For segmentIndex As Integer = 0 To path.Count - 2
                    Dim fromPoint As New PointF(path(segmentIndex).X * scaleX, path(segmentIndex).Y * scaleY)
                    Dim toPoint As New PointF(path(segmentIndex + 1).X * scaleX, path(segmentIndex + 1).Y * scaleY)
                    Dim segmentBounce As Single = boundaryBounce
                    If pathIndex < boundarySegmentBounces.Count AndAlso segmentIndex < boundarySegmentBounces(pathIndex).Count AndAlso
                       boundarySegmentBounces(pathIndex)(segmentIndex) >= 0.0F Then
                        segmentBounce = boundarySegmentBounces(pathIndex)(segmentIndex)
                    End If
                    Dim beforeCollisionCenter As PointF = center
                    Dim beforeCollisionVelocity As PointF = velocity
                    ResolveStaticFloorCollision(center, previousCenter, radius, velocity, fromPoint, toPoint, segmentBounce)
                    Dim collisionResolved As Boolean =
                        Math.Abs(center.X - beforeCollisionCenter.X) > 0.0001F OrElse
                        Math.Abs(center.Y - beforeCollisionCenter.Y) > 0.0001F OrElse
                        Math.Abs(velocity.X - beforeCollisionVelocity.X) > 0.0001F OrElse
                        Math.Abs(velocity.Y - beforeCollisionVelocity.Y) > 0.0001F
                    If collisionResolved Then
                        ApplyShallowSurfaceRollingBoost(velocity, fromPoint, toPoint, elapsed, gravity * scaleY)
                    End If
                Next
            Next
            For Each obstacle As RectangleF In obstacles
                ResolveCircularObstacleCollision(center, radius, velocity, obstacle, scaleX, scaleY)
            Next
            CheckLauncherCapture(center, scaleX, scaleY)
            CheckSwitchZones(previousCenter, center, scaleX, scaleY)
            ball.SetMotionPathPosition(center, True)
        End Sub

        Private Shared Sub ApplyShallowSurfaceRollingBoost(ByRef ballVelocity As PointF,
                                                            ByVal fromPoint As PointF,
                                                            ByVal toPoint As PointF,
                                                            ByVal elapsed As Single,
                                                            ByVal gravityAcceleration As Single)
            Dim segmentX As Single = toPoint.X - fromPoint.X
            Dim segmentY As Single = toPoint.Y - fromPoint.Y
            Dim length As Single = CSng(Math.Sqrt(segmentX * segmentX + segmentY * segmentY))
            If length <= 0.01F Then Return

            Dim tangentX As Single = segmentX / length
            Dim tangentY As Single = segmentY / length
            Dim absoluteSlope As Single = Math.Abs(tangentY)
            If absoluteSlope <= 0.001F OrElse absoluteSlope > ShallowSurfaceSlopeLimit Then Return

            Dim extraProjectedGravity As Single =
                gravityAcceleration * tangentY * (ShallowSurfaceRollMultiplier - 1.0F)
            ballVelocity.X += tangentX * extraProjectedGravity * elapsed
            ballVelocity.Y += tangentY * extraProjectedGravity * elapsed
        End Sub

        Private Sub CheckLauncherCapture(ByRef center As PointF, ByVal scaleX As Single, ByVal scaleY As Single)
            If launcher Is Nothing OrElse launcherArmed Then Return
            Dim origin As PointF = PointF.Empty, angle As Single = 0.0F
            GetLauncherPose(scaleX, scaleY, origin, angle)
            Dim radius As Single = launcher.CaptureRadius * (scaleX + scaleY) / 2.0F
            Dim dx As Single = center.X - origin.X
            Dim dy As Single = center.Y - origin.Y
            Dim inside As Boolean = dx * dx + dy * dy <= radius * radius
            If Not launcherExitedCapture Then
                launcherExitedCapture = Not inside
            ElseIf inside Then
                center = origin
                velocity = PointF.Empty
                launcherArmed = True
                launcherHolding = True
            End If
        End Sub

        Private Sub GetLauncherPose(ByVal scaleX As Single, ByVal scaleY As Single,
                                    ByRef origin As PointF, ByRef angle As Single)
            origin = New PointF(launcher.Origin.X * scaleX, launcher.Origin.Y * scaleY)
            angle = launcher.Angle
            If Not launcher.FollowPivot Then Return
            Dim pivot As B2SPictureBox = FindPivotPicture(flipperName)
            If pivot Is Nothing Then Return
            If pivot.PivotAutomaticOscillation Then
                ' The main editor ball placement is the firing point for an attached
                ' automatic pivot. Keep the saved launch coordinates for other modes.
                origin = New PointF(authoredBallCenter.X * scaleX, authoredBallCenter.Y * scaleY)
            End If
            Dim hinge As New PointF(pivot.RectangleF.Left + pivot.RectangleF.Width * pivot.RotationPivotX,
                                    pivot.RectangleF.Top + pivot.RectangleF.Height * pivot.RotationPivotY)
            If pivot.PreservePhysicsArtworkAspect AndAlso pivot.Width > 0 AndAlso pivot.Height > 0 Then
                ' Keep the held ball at the point it occupied on the authored
                ' launcher. The background and physics boundaries retain their
                ' independent X/Y screen scaling; only this rigid pair uses one
                ' visual scale around the hinge.
                Dim authoredOrigin As PointF = If(pivot.PivotAutomaticOscillation, authoredBallCenter, launcher.Origin)
                Dim authoredHinge As New PointF(pivot.Left + pivot.Width * pivot.RotationPivotX,
                                                pivot.Top + pivot.Height * pivot.RotationPivotY)
                Dim visualScale As Single = Math.Min(pivot.RectangleF.Width / pivot.Width,
                                                    pivot.RectangleF.Height / pivot.Height)
                origin = New PointF(hinge.X + (authoredOrigin.X - authoredHinge.X) * visualScale,
                                    hinge.Y + (authoredOrigin.Y - authoredHinge.Y) * visualScale)
            End If
            Dim restAngle As Single = If(pivot.PivotAutomaticOscillation, 0.0F, pivot.PivotDownAngle)
            Dim delta As Single = pivot.RotationAngle - restAngle
            Dim radians As Double = delta * Math.PI / 180.0R
            Dim dx As Single = origin.X - hinge.X, dy As Single = origin.Y - hinge.Y
            If pivot.PivotAutomaticOscillation AndAlso pivot.Width > 0 AndAlso pivot.Height > 0 Then
                ' Match the automatic pivot artwork: rotate the editor point in
                ' authored coordinates, then apply the backglass X/Y scales.
                Dim authoredHinge As New PointF(pivot.Left + pivot.Width * pivot.RotationPivotX,
                                                pivot.Top + pivot.Height * pivot.RotationPivotY)
                Dim authoredX As Single = authoredBallCenter.X - authoredHinge.X
                Dim authoredY As Single = authoredBallCenter.Y - authoredHinge.Y
                Dim pivotScaleX As Single = pivot.RectangleF.Width / pivot.Width
                Dim pivotScaleY As Single = pivot.RectangleF.Height / pivot.Height
                origin = New PointF(hinge.X + CSng((authoredX * Math.Cos(radians) - authoredY * Math.Sin(radians)) * pivotScaleX),
                                    hinge.Y + CSng((authoredX * Math.Sin(radians) + authoredY * Math.Cos(radians)) * pivotScaleY))
            Else
                origin = New PointF(hinge.X + CSng(dx * Math.Cos(radians) - dy * Math.Sin(radians)),
                                    hinge.Y + CSng(dx * Math.Sin(radians) + dy * Math.Cos(radians)))
            End If
            angle += delta
        End Sub

        Private Sub CheckSwitchZones(ByVal previousCenter As PointF, ByVal center As PointF,
                                     ByVal scaleX As Single, ByVal scaleY As Single)
            If Math.Abs(scaleX) < 0.000001F OrElse Math.Abs(scaleY) < 0.000001F Then Return
            Dim authoredPrevious As New PointF(previousCenter.X / scaleX, previousCenter.Y / scaleY)
            Dim authoredCurrent As New PointF(center.X / scaleX, center.Y / scaleY)
            For index As Integer = 0 To switchZones.Count - 1
                Dim zone As PhysicsSwitchZone = switchZones(index)
                Dim inside As Boolean = PointInSwitchZone(authoredCurrent, zone)
                Dim crossed As Boolean = Not inside AndAlso SegmentIntersectsSwitchZone(authoredPrevious, authoredCurrent, zone)
                If Not switchZoneInside(index) AndAlso (inside OrElse crossed) Then PulsePhysicsSwitch(zone.SwitchID)
                switchZoneInside(index) = inside
            Next
        End Sub

        Private Shared Function PointInSwitchZone(ByVal point As PointF, ByVal zone As PhysicsSwitchZone) As Boolean
            Dim local As PointF = SwitchZoneLocalPoint(point, zone)
            Return zone.Bounds.Contains(local.X, local.Y)
        End Function

        Private Shared Function SegmentIntersectsSwitchZone(ByVal fromPoint As PointF, ByVal toPoint As PointF,
                                                             ByVal zone As PhysicsSwitchZone) As Boolean
            Dim localFrom As PointF = SwitchZoneLocalPoint(fromPoint, zone)
            Dim localTo As PointF = SwitchZoneLocalPoint(toPoint, zone)
            If zone.Bounds.Contains(localFrom.X, localFrom.Y) OrElse zone.Bounds.Contains(localTo.X, localTo.Y) Then Return True
            Dim deltaX As Double = localTo.X - localFrom.X, deltaY As Double = localTo.Y - localFrom.Y
            Dim first As Double = 0.0R, last As Double = 1.0R
            Return ClipSwitchSegment(-deltaX, localFrom.X - zone.Bounds.Left, first, last) AndAlso
                   ClipSwitchSegment(deltaX, zone.Bounds.Right - localFrom.X, first, last) AndAlso
                   ClipSwitchSegment(-deltaY, localFrom.Y - zone.Bounds.Top, first, last) AndAlso
                   ClipSwitchSegment(deltaY, zone.Bounds.Bottom - localFrom.Y, first, last)
        End Function

        Private Shared Function ClipSwitchSegment(ByVal direction As Double, ByVal distance As Double,
                                                   ByRef first As Double, ByRef last As Double) As Boolean
            If Math.Abs(direction) < 0.0000001R Then Return distance >= 0.0R
            Dim ratio As Double = distance / direction
            If direction < 0.0R Then
                If ratio > last Then Return False
                If ratio > first Then first = ratio
            Else
                If ratio < first Then Return False
                If ratio < last Then last = ratio
            End If
            Return True
        End Function

        Private Shared Function SwitchZoneLocalPoint(ByVal point As PointF, ByVal zone As PhysicsSwitchZone) As PointF
            Dim angle As Single = NormalizeSwitchAngle(zone.Angle)
            If Math.Abs(angle) < 0.001F Then Return point
            Dim centerX As Single = zone.Bounds.Left + zone.Bounds.Width / 2.0F
            Dim centerY As Single = zone.Bounds.Top + zone.Bounds.Height / 2.0F
            Dim radians As Double = -angle * Math.PI / 180.0R
            Dim cosine As Double = Math.Cos(radians), sine As Double = Math.Sin(radians)
            Dim x As Double = point.X - centerX, y As Double = point.Y - centerY
            Return New PointF(CSng(centerX + x * cosine - y * sine),
                              CSng(centerY + x * sine + y * cosine))
        End Function

        Private Shared Function NormalizeSwitchAngle(ByVal angle As Single) As Single
            Dim normalized As Single = angle Mod 360.0F
            If normalized > 180.0F Then normalized -= 360.0F
            If normalized <= -180.0F Then normalized += 360.0F
            Return normalized
        End Function

        Private Sub ResolveCircularObstacleCollision(ByRef center As PointF, ByVal ballRadius As Single,
                                                      ByRef ballVelocity As PointF, ByVal obstacle As RectangleF,
                                                      ByVal scaleX As Single, ByVal scaleY As Single)
            Dim obstacleCenterX As Single = (obstacle.X + obstacle.Width / 2.0F) * scaleX
            Dim obstacleCenterY As Single = (obstacle.Y + obstacle.Height / 2.0F) * scaleY
            Dim obstacleRadius As Single = Math.Max(1.0F, (obstacle.Width * scaleX + obstacle.Height * scaleY) / 4.0F)
            Dim dx As Single = center.X - obstacleCenterX
            Dim dy As Single = center.Y - obstacleCenterY
            Dim minimumDistance As Single = ballRadius + obstacleRadius
            Dim distanceSquared As Single = dx * dx + dy * dy
            If distanceSquared >= minimumDistance * minimumDistance Then Return
            Dim distance As Single = CSng(Math.Sqrt(distanceSquared))
            Dim normalX As Single
            Dim normalY As Single
            If distance < 0.001F Then
                normalX = 0.0F
                normalY = -1.0F
                distance = 0.0F
            Else
                normalX = dx / distance
                normalY = dy / distance
            End If
            center.X += normalX * (minimumDistance - distance)
            center.Y += normalY * (minimumDistance - distance)
            Dim normalSpeed As Single = ballVelocity.X * normalX + ballVelocity.Y * normalY
            If normalSpeed < 0.0F Then
                ballVelocity.X -= (1.0F + boundaryBounce) * normalSpeed * normalX
                ballVelocity.Y -= (1.0F + boundaryBounce) * normalSpeed * normalY
            End If
        End Sub

        Private Sub ResolveStaticFloorCollision(ByRef center As PointF, ByVal previousCenter As PointF,
                                                 ByVal radius As Single, ByRef ballVelocity As PointF,
                                                 ByVal fromPoint As PointF, ByVal toPoint As PointF,
                                                 ByVal bounce As Single)
            Dim segmentX As Single = toPoint.X - fromPoint.X
            Dim segmentY As Single = toPoint.Y - fromPoint.Y
            Dim lengthSquared As Single = segmentX * segmentX + segmentY * segmentY
            If lengthSquared <= 0.01F Then Return
            Dim rawFraction As Single =
                ((center.X - fromPoint.X) * segmentX + (center.Y - fromPoint.Y) * segmentY) / lengthSquared
            Dim fraction As Single = Math.Max(0.0F, Math.Min(1.0F, rawFraction))
            Dim contactX As Single = fromPoint.X + segmentX * fraction
            Dim contactY As Single = fromPoint.Y + segmentY * fraction
            Dim offsetX As Single = center.X - contactX
            Dim offsetY As Single = center.Y - contactY
            Dim distanceSquared As Single = offsetX * offsetX + offsetY * offsetY
            If distanceSquared >= radius * radius Then
                ' A fast ball can cross the entire boundary between timer steps and
                ' finish beyond it without overlapping. Sweep the ball's full path
                ' against the segment capsule before treating this as no collision.
                Dim sweptCenter As PointF
                Dim sweptNormal As PointF
                If Not TryGetSweptStaticFloorCollision(previousCenter, center, radius,
                                                       fromPoint, toPoint,
                                                       sweptCenter, sweptNormal) Then Return

                center.X = sweptCenter.X + sweptNormal.X * 0.01F
                center.Y = sweptCenter.Y + sweptNormal.Y * 0.01F
                Dim sweptNormalSpeed As Single =
                    ballVelocity.X * sweptNormal.X + ballVelocity.Y * sweptNormal.Y
                If sweptNormalSpeed < 0.0F Then
                    ReflectBoundaryVelocity(ballVelocity, sweptNormal.X, sweptNormal.Y, bounce, sweptNormalSpeed)
                End If
                Return
            End If

            Dim distance As Single = CSng(Math.Sqrt(distanceSquared))
            Dim normalX As Single
            Dim normalY As Single
            If rawFraction < 0.0F OrElse rawFraction > 1.0F Then
                Dim previousOffsetX As Single = previousCenter.X - contactX
                Dim previousOffsetY As Single = previousCenter.Y - contactY
                Dim previousDistance As Single = CSng(Math.Sqrt(previousOffsetX * previousOffsetX + previousOffsetY * previousOffsetY))
                ' Resolve against the ball's current radial direction so a slow
                ' contact can roll around a segment endpoint instead of being
                ' anchored to its previous position until enough speed builds.
                If distance >= 0.001F Then
                    normalX = offsetX / distance
                    normalY = offsetY / distance
                ElseIf previousDistance >= 0.001F Then
                    normalX = previousOffsetX / previousDistance
                    normalY = previousOffsetY / previousDistance
                Else
                    Return
                End If
            Else
                Dim segmentLength As Single = CSng(Math.Sqrt(lengthSquared))
                normalX = segmentY / segmentLength
                normalY = -segmentX / segmentLength
                Dim previousSide As Single =
                    (previousCenter.X - contactX) * normalX + (previousCenter.Y - contactY) * normalY
                If Math.Abs(previousSide) < 0.001F Then
                    previousSide = offsetX * normalX + offsetY * normalY
                End If
                If Math.Abs(previousSide) < 0.001F Then
                    If ballVelocity.X * normalX + ballVelocity.Y * normalY > 0.0F Then
                        normalX = -normalX
                        normalY = -normalY
                    End If
                ElseIf previousSide < 0.0F Then
                    normalX = -normalX
                    normalY = -normalY
                End If
            End If
            center.X = contactX + normalX * radius
            center.Y = contactY + normalY * radius
            Dim normalSpeed As Single = ballVelocity.X * normalX + ballVelocity.Y * normalY
            If normalSpeed < 0.0F Then
                ReflectBoundaryVelocity(ballVelocity, normalX, normalY, bounce, normalSpeed)
            End If
        End Sub

        Private Shared Sub ReflectBoundaryVelocity(ByRef ballVelocity As PointF,
                                                    ByVal normalX As Single,
                                                    ByVal normalY As Single,
                                                    ByVal bounce As Single,
                                                    ByVal incomingNormalSpeed As Single)
            ballVelocity.X -= (1.0F + bounce) * incomingNormalSpeed * normalX
            ballVelocity.Y -= (1.0F + bounce) * incomingNormalSpeed * normalY
            If -incomingNormalSpeed < BoundaryScatterMinimumImpactSpeed Then Return

            Dim scatterDegrees As Single
            SyncLock boundaryScatterRandom
                scatterDegrees = CSng((boundaryScatterRandom.NextDouble() * 2.0R - 1.0R) * BoundaryScatterDegrees)
            End SyncLock
            RotateBoundaryVelocity(ballVelocity, normalX, normalY, scatterDegrees)
        End Sub

        Private Shared Sub RotateBoundaryVelocity(ByRef ballVelocity As PointF,
                                                   ByVal normalX As Single,
                                                   ByVal normalY As Single,
                                                   ByVal degrees As Single)
            Dim radians As Double = degrees * Math.PI / 180.0R
            Dim cosine As Single = CSng(Math.Cos(radians))
            Dim sine As Single = CSng(Math.Sin(radians))
            Dim rotatedX As Single = ballVelocity.X * cosine - ballVelocity.Y * sine
            Dim rotatedY As Single = ballVelocity.X * sine + ballVelocity.Y * cosine
            If rotatedX * normalX + rotatedY * normalY <= 0.0F Then Return
            ballVelocity.X = rotatedX
            ballVelocity.Y = rotatedY
        End Sub

        Private Function TryGetSweptStaticFloorCollision(ByVal previousCenter As PointF,
                                                         ByVal currentCenter As PointF,
                                                         ByVal radius As Single,
                                                         ByVal fromPoint As PointF,
                                                         ByVal toPoint As PointF,
                                                         ByRef impactCenter As PointF,
                                                         ByRef impactNormal As PointF) As Boolean
            Dim motionX As Single = currentCenter.X - previousCenter.X
            Dim motionY As Single = currentCenter.Y - previousCenter.Y
            Dim motionLengthSquared As Single = motionX * motionX + motionY * motionY
            If motionLengthSquared <= 0.000001F Then Return False

            Dim segmentX As Single = toPoint.X - fromPoint.X
            Dim segmentY As Single = toPoint.Y - fromPoint.Y
            Dim segmentLengthSquared As Single = segmentX * segmentX + segmentY * segmentY
            If segmentLengthSquared <= 0.01F Then Return False

            Dim segmentLength As Single = CSng(Math.Sqrt(segmentLengthSquared))
            Dim tangentX As Single = segmentX / segmentLength
            Dim tangentY As Single = segmentY / segmentLength
            Dim baseNormalX As Single = tangentY
            Dim baseNormalY As Single = -tangentX
            Dim previousSignedDistance As Single =
                (previousCenter.X - fromPoint.X) * baseNormalX +
                (previousCenter.Y - fromPoint.Y) * baseNormalY
            Dim currentSignedDistance As Single =
                (currentCenter.X - fromPoint.X) * baseNormalX +
                (currentCenter.Y - fromPoint.Y) * baseNormalY
            Dim signedDistanceChange As Single = currentSignedDistance - previousSignedDistance
            Dim bestTime As Single = Single.MaxValue
            Dim found As Boolean = False

            ' A collision with a neighboring corner segment can push the ball
            ' inside this segment after it was already processed for the step.
            ' If the next movement crosses from that embedded position to the
            ' opposite side, restore it to the side it occupied before crossing.
            Dim previousProjection As Single =
                (previousCenter.X - fromPoint.X) * tangentX +
                (previousCenter.Y - fromPoint.Y) * tangentY
            Dim currentProjection As Single =
                (currentCenter.X - fromPoint.X) * tangentX +
                (currentCenter.Y - fromPoint.Y) * tangentY
            If previousProjection >= 0.0F AndAlso previousProjection <= segmentLength AndAlso
               currentProjection >= 0.0F AndAlso currentProjection <= segmentLength AndAlso
               Math.Abs(previousSignedDistance) < radius AndAlso
               previousSignedDistance * currentSignedDistance <= 0.0F AndAlso
               Math.Abs(currentSignedDistance) >= radius Then
                Dim retainedSide As Single
                If Math.Abs(previousSignedDistance) >= 0.000001F Then
                    retainedSide = If(previousSignedDistance > 0.0F, 1.0F, -1.0F)
                Else
                    retainedSide = If(signedDistanceChange < 0.0F, 1.0F, -1.0F)
                End If
                impactCenter = New PointF(fromPoint.X + tangentX * previousProjection + baseNormalX * retainedSide * radius,
                                          fromPoint.Y + tangentY * previousProjection + baseNormalY * retainedSide * radius)
                impactNormal = New PointF(baseNormalX * retainedSide, baseNormalY * retainedSide)
                Return True
            End If

            ' Test the two parallel sides of the segment's radius-expanded capsule.
            For sideIndex As Integer = 0 To 1
                Dim sideSign As Single = If(sideIndex = 0, 1.0F, -1.0F)
                If sideSign * signedDistanceChange < -0.000001F Then
                    Dim hitTime As Single =
                        (sideSign * radius - previousSignedDistance) / signedDistanceChange
                    If hitTime >= 0.0F AndAlso hitTime <= 1.0F AndAlso hitTime < bestTime Then
                        Dim hitX As Single = previousCenter.X + motionX * hitTime
                        Dim hitY As Single = previousCenter.Y + motionY * hitTime
                        Dim segmentProjection As Single =
                            (hitX - fromPoint.X) * tangentX + (hitY - fromPoint.Y) * tangentY
                        If segmentProjection >= 0.0F AndAlso segmentProjection <= segmentLength Then
                            bestTime = hitTime
                            impactCenter = New PointF(hitX, hitY)
                            impactNormal = New PointF(baseNormalX * sideSign, baseNormalY * sideSign)
                            found = True
                        End If
                    End If
                End If
            Next

            ' Test the round caps so high-speed movement cannot skip a boundary end.
            For endpointIndex As Integer = 0 To 1
                Dim endpoint As PointF = If(endpointIndex = 0, fromPoint, toPoint)
                Dim relativeX As Single = previousCenter.X - endpoint.X
                Dim relativeY As Single = previousCenter.Y - endpoint.Y
                Dim circleConstant As Single =
                    relativeX * relativeX + relativeY * relativeY - radius * radius
                If circleConstant >= 0.0F Then
                    Dim circleLinear As Single = 2.0F * (relativeX * motionX + relativeY * motionY)
                    Dim discriminant As Double =
                        CDbl(circleLinear) * CDbl(circleLinear) -
                        4.0R * CDbl(motionLengthSquared) * CDbl(circleConstant)
                    If discriminant >= 0.0R Then
                        Dim hitTime As Single =
                            CSng((-CDbl(circleLinear) - Math.Sqrt(discriminant)) /
                                 (2.0R * CDbl(motionLengthSquared)))
                        If hitTime >= 0.0F AndAlso hitTime <= 1.0F AndAlso hitTime < bestTime Then
                            Dim hitX As Single = previousCenter.X + motionX * hitTime
                            Dim hitY As Single = previousCenter.Y + motionY * hitTime
                            Dim normalX As Single = hitX - endpoint.X
                            Dim normalY As Single = hitY - endpoint.Y
                            Dim normalLength As Single = CSng(Math.Sqrt(normalX * normalX + normalY * normalY))
                            If normalLength >= 0.001F Then
                                bestTime = hitTime
                                impactCenter = New PointF(hitX, hitY)
                                impactNormal = New PointF(normalX / normalLength, normalY / normalLength)
                                found = True
                            End If
                        End If
                    End If
                End If
            Next

            Return found
        End Function

        Private Sub ResolveFlipperCollision(ByRef center As PointF, ByVal ballRadius As Single, ByRef ballVelocity As PointF,
                                            ByVal flipper As B2SPictureBox, ByVal angularVelocity As Single)
            Dim hinge As New PointF(flipper.RectangleF.Left + flipper.RectangleF.Width * flipper.RotationPivotX,
                                    flipper.RectangleF.Top + flipper.RectangleF.Height * flipper.RotationPivotY)
            Dim baseTipX As Single = flipper.RectangleF.Left + flipper.RectangleF.Width * flipper.RotationTipX
            Dim baseTipY As Single = flipper.RectangleF.Top + flipper.RectangleF.Height * flipper.RotationTipY
            Dim radians As Double = flipper.RotationAngle * Math.PI / 180.0R
            Dim sourceX As Double = baseTipX - hinge.X
            Dim sourceY As Double = baseTipY - hinge.Y
            Dim tip As New PointF(CSng(hinge.X + sourceX * Math.Cos(radians) - sourceY * Math.Sin(radians)),
                                  CSng(hinge.Y + sourceX * Math.Sin(radians) + sourceY * Math.Cos(radians)))
            Dim segmentX As Single = tip.X - hinge.X
            Dim segmentY As Single = tip.Y - hinge.Y
            Dim lengthSquared As Single = segmentX * segmentX + segmentY * segmentY
            If lengthSquared <= 0.01F Then Return
            Dim rawFraction As Single = ((center.X - hinge.X) * segmentX + (center.Y - hinge.Y) * segmentY) / lengthSquared
            Dim fraction As Single = Math.Max(0.0F, Math.Min(1.0F, rawFraction))
            Dim contact As New PointF(hinge.X + segmentX * fraction, hinge.Y + segmentY * fraction)
            Dim segmentLength As Single = CSng(Math.Sqrt(lengthSquared))
            Dim normalX As Single = segmentY / segmentLength
            Dim normalY As Single = -segmentX / segmentLength
            If normalY > 0.0F Then normalX = -normalX : normalY = -normalY
            Dim flipperRadius As Single = Math.Max(3.0F, Math.Min(flipper.RectangleF.Width, flipper.RectangleF.Height) * 0.07F)
            Dim minimumDistance As Single = ballRadius + flipperRadius
            If rawFraction < 0.0F OrElse rawFraction > 1.0F Then
                Dim endX As Single = center.X - contact.X
                Dim endY As Single = center.Y - contact.Y
                Dim endDistance As Single = CSng(Math.Sqrt(endX * endX + endY * endY))
                If endDistance >= minimumDistance OrElse endDistance < 0.001F Then Return
                normalX = endX / endDistance : normalY = endY / endDistance
                If normalY > 0.0F Then Return
                center.X += normalX * (minimumDistance - endDistance)
                center.Y += normalY * (minimumDistance - endDistance)
            Else
                Dim signedDistance As Single = (center.X - contact.X) * normalX + (center.Y - contact.Y) * normalY
                If signedDistance >= minimumDistance Then Return
                center.X += normalX * (minimumDistance - signedDistance)
                center.Y += normalY * (minimumDistance - signedDistance)
            End If
            Dim armX As Single = contact.X - hinge.X
            Dim armY As Single = contact.Y - hinge.Y
            Dim surfaceX As Single = -angularVelocity * armY * flipperStrength
            Dim surfaceY As Single = angularVelocity * armX * flipperStrength
            Dim relativeX As Single = ballVelocity.X - surfaceX
            Dim relativeY As Single = ballVelocity.Y - surfaceY
            Dim towardSurface As Single = relativeX * normalX + relativeY * normalY
            If towardSurface < 0.0F Then
                Const restitution As Single = 0.58F
                relativeX -= (1.0F + restitution) * towardSurface * normalX
                relativeY -= (1.0F + restitution) * towardSurface * normalY
                ballVelocity.X = surfaceX + relativeX
                ballVelocity.Y = surfaceY + relativeY
            End If
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            timer.Stop()
            RemoveHandler timer.Tick, AddressOf Tick
            timer.Dispose()
            clock.Stop()
        End Sub
    End Class

    Private Class ExternalPivotState
        Private representative As B2SPictureBox
        Private duration As Integer = 80
        Private ReadOnly angles As New Generic.Dictionary(Of String, Single)(StringComparer.OrdinalIgnoreCase)

        Public Sub Register(ByVal pictureBox As B2SPictureBox, ByVal targetName As String,
                            ByVal isRepresentative As Boolean, ByVal angle As Single, ByVal moveDuration As Integer)
            angles(targetName) = angle
            pictureBox.Visible = False
            If isRepresentative Then
                representative = pictureBox
                representative.PivotRotation = True
                representative.NativeRotation = True
                representative.RotationAngle = angle
            End If
            duration = Math.Max(10, Math.Min(1000, moveDuration))
        End Sub

        Public Sub MoveTo(ByVal targetName As String)
            If representative Is Nothing OrElse Not angles.ContainsKey(targetName) Then Return
            representative.SetPivotRotationTarget(angles(targetName), duration)
        End Sub
    End Class

    Shared Sub New()

        ' set coordinates maximum
        ledCoordMax = 103

        ' add led segments
        Const toleft As Integer = 8
        ' 8 segments
        led8Seg.Add({New PointF(22, 5), New PointF(26, 2), New PointF(88, 2), New PointF(92, 5), New PointF(85, 11), New PointF(29, 11)})
        led8Seg.Add({New PointF(93, 7), New PointF(96, 10), New PointF(96 - toleft, 46), New PointF(93 - toleft, 49), New PointF(87 - toleft, 43), New PointF(87, 12)})
        led8Seg.Add({New PointF(92 - toleft, 51), New PointF(95 - toleft, 54), New PointF(96 - 2 * toleft, 90), New PointF(93 - 2 * toleft, 93), New PointF(87 - 2 * toleft, 88), New PointF(86 - toleft, 57)})
        led8Seg.Add({New PointF(22 - 2 * toleft, 95), New PointF(29 - 2 * toleft, 89), New PointF(85 - 2 * toleft, 89), New PointF(92 - 2 * toleft, 95), New PointF(88 - 2 * toleft, 98), New PointF(26 - 2 * toleft, 98)})
        led8Seg.Add({New PointF(20 - toleft, 51), New PointF(26 - toleft, 57), New PointF(27 - 2 * toleft, 88), New PointF(21 - 2 * toleft, 93), New PointF(17 - 2 * toleft, 90), New PointF(17 - toleft, 54)})
        led8Seg.Add({New PointF(21, 7), New PointF(27, 12), New PointF(27 - toleft, 43), New PointF(21 - toleft, 49), New PointF(18 - toleft, 46), New PointF(18, 10)})
        led8Seg.Add({New PointF(23 - toleft, 50), New PointF(27 - toleft, 46), New PointF(86 - toleft, 46), New PointF(90 - toleft, 50), New PointF(86 - toleft, 54), New PointF(27 - toleft, 54)})
        ' 10 segments
        led10Seg.Add({New PointF(22, 5), New PointF(26, 2), New PointF(88, 2), New PointF(92, 5), New PointF(85, 11), New PointF(72, 11), New PointF(67, 6), New PointF(62, 11), New PointF(29, 11)})
        led10Seg.Add({New PointF(93, 7), New PointF(96, 10), New PointF(96 - toleft, 46), New PointF(93 - toleft, 49), New PointF(87 - toleft, 43), New PointF(87, 12)})
        led10Seg.Add({New PointF(92 - toleft, 51), New PointF(95 - toleft, 54), New PointF(96 - 2 * toleft, 90), New PointF(93 - 2 * toleft, 93), New PointF(87 - 2 * toleft, 88), New PointF(86 - toleft, 57)})
        led10Seg.Add({New PointF(22 - 2 * toleft, 95), New PointF(29 - 2 * toleft, 89), New PointF(61 - 2 * toleft, 89), New PointF(66 - 2 * toleft, 94), New PointF(71 - 2 * toleft, 89), New PointF(85 - 2 * toleft, 89), New PointF(92 - 2 * toleft, 95), New PointF(88 - 2 * toleft, 98), New PointF(26 - 2 * toleft, 98)})
        led10Seg.Add({New PointF(20 - toleft, 51), New PointF(26 - toleft, 57), New PointF(27 - 2 * toleft, 88), New PointF(21 - 2 * toleft, 93), New PointF(17 - 2 * toleft, 90), New PointF(17 - toleft, 54)})
        led10Seg.Add({New PointF(21, 7), New PointF(27, 12), New PointF(27 - toleft, 43), New PointF(21 - toleft, 49), New PointF(18 - toleft, 46), New PointF(18, 10)})
        led10Seg.Add({New PointF(23 - toleft, 50), New PointF(27 - toleft, 46), New PointF(63 - toleft, 46), New PointF(68 - toleft, 51), New PointF(73 - toleft, 46), New PointF(86 - toleft, 46), New PointF(90 - toleft, 50), New PointF(86 - toleft, 54), New PointF(72 - toleft, 54), New PointF(67 - toleft, 49), New PointF(62 - toleft, 54), New PointF(27 - toleft, 54)})
        led10Seg.Add({New PointF(67, 9), New PointF(71, 13), New PointF(71 - toleft, 45), New PointF(67 - toleft, 49), New PointF(63 - toleft, 45), New PointF(63, 13)})
        led10Seg.Add({New PointF(68, 7), New PointF(72, 11), New PointF(72 - toleft, 43), New PointF(68 - toleft, 47), New PointF(64 - toleft, 43), New PointF(64, 7)})
        led10Seg.Add({New PointF(66 - toleft, 51), New PointF(70 - toleft, 55), New PointF(70 - 2 * toleft, 88), New PointF(66 - 2 * toleft, 92), New PointF(62 - 2 * toleft, 88), New PointF(62 - toleft, 51)})
        ' 14 segments
        led14Seg.Add({New PointF(22, 5), New PointF(26, 2), New PointF(88, 2), New PointF(92, 5), New PointF(85, 11), New PointF(29, 11)})
        led14Seg.Add({New PointF(93, 7), New PointF(96, 10), New PointF(96 - toleft, 46), New PointF(93 - toleft, 49), New PointF(87 - toleft, 43), New PointF(87, 12)})
        led14Seg.Add({New PointF(92 - toleft, 51), New PointF(95 - toleft, 54), New PointF(96 - 2 * toleft, 90), New PointF(93 - 2 * toleft, 93), New PointF(87 - 2 * toleft, 88), New PointF(86 - toleft, 57)})
        led14Seg.Add({New PointF(22 - 2 * toleft, 95), New PointF(29 - 2 * toleft, 89), New PointF(85 - 2 * toleft, 89), New PointF(92 - 2 * toleft, 95), New PointF(88 - 2 * toleft, 98), New PointF(26 - 2 * toleft, 98)})
        led14Seg.Add({New PointF(20 - toleft, 51), New PointF(26 - toleft, 57), New PointF(27 - 2 * toleft, 88), New PointF(21 - 2 * toleft, 93), New PointF(17 - 2 * toleft, 90), New PointF(17 - toleft, 54)})
        led14Seg.Add({New PointF(21, 7), New PointF(27, 12), New PointF(27 - toleft, 43), New PointF(21 - toleft, 49), New PointF(18 - toleft, 46), New PointF(18, 10)})
        led14Seg.Add({New PointF(23 - toleft, 50), New PointF(27 - toleft, 46), New PointF(52 - toleft, 46), New PointF(55 - toleft, 50), New PointF(52 - toleft, 54), New PointF(27 - toleft, 54)})
        led14Seg.Add({New PointF(104 - 2 * toleft, 87), New PointF(109 - 2 * toleft, 90), New PointF(109 - 2 * toleft, 95), New PointF(104 - 2 * toleft, 99), New PointF(100 - 2 * toleft, 95), New PointF(100 - 2 * toleft, 90)})
        led14Seg.Add({New PointF(30, 13), New PointF(34, 17), New PointF(54 - toleft, 38), New PointF(51 - toleft, 43), New PointF(48 - toleft, 40), New PointF(27, 16)})
        led14Seg.Add({New PointF(57, 13), New PointF(61, 13), New PointF(61 - toleft, 46), New PointF(57 - toleft, 48), New PointF(53 - toleft, 46), New PointF(53, 13)})
        led14Seg.Add({New PointF(82, 13), New PointF(85, 16), New PointF(68 - toleft, 42), New PointF(65 - toleft, 44), New PointF(63 - toleft, 39), New PointF(77, 17)})
        led14Seg.Add({New PointF(58 - toleft, 50), New PointF(62 - toleft, 46), New PointF(86 - toleft, 46), New PointF(90 - toleft, 50), New PointF(86 - toleft, 54), New PointF(62 - toleft, 54)})
        led14Seg.Add({New PointF(82 - 2 * toleft, 85), New PointF(87 - 2 * toleft, 86), New PointF(67 - toleft, 57), New PointF(62 - toleft, 57), New PointF(62 - toleft, 60), New PointF(79 - 2 * toleft, 86)})
        led14Seg.Add({New PointF(57 - toleft, 52), New PointF(61 - toleft, 54), New PointF(61 - 2 * toleft, 88), New PointF(57 - 2 * toleft, 88), New PointF(53 - 2 * toleft, 88), New PointF(53 - toleft, 54)})
        led14Seg.Add({New PointF(30 - 2 * toleft, 83), New PointF(33 - 2 * toleft, 86), New PointF(50 - toleft, 60), New PointF(47 - toleft, 57), New PointF(42 - toleft, 61), New PointF(27 - 2 * toleft, 86)})
        led14Seg.Add({New PointF(102 - 2 * toleft, 97), New PointF(107 - 2 * toleft, 100), New PointF(107 - 2 * toleft, 105), New PointF(102 - 2 * toleft, 109), New PointF(98 - 2 * toleft, 105), New PointF(98 - 2 * toleft, 100)})
        ' 16 segments
        led16Seg.Add({New PointF(22, 5), New PointF(26, 2), New PointF(88, 2), New PointF(92, 5), New PointF(85, 11), New PointF(29, 11)})
        'led16Seg.Add({New PointF(93, 7), New PointF(96, 10), New PointF(96 - toleft, 46), New PointF(93 - toleft, 49), New PointF(87 - toleft, 43), New PointF(87, 12)})
        'led16Seg.Add({New PointF(92 - toleft, 51), New PointF(95 - toleft, 54), New PointF(96 - 2 * toleft, 90), New PointF(93 - 2 * toleft, 93), New PointF(87 - 2 * toleft, 88), New PointF(86 - toleft, 57)})
        'led16Seg.Add({New PointF(22 - 2 * toleft, 95), New PointF(29 - 2 * toleft, 89), New PointF(85 - 2 * toleft, 89), New PointF(92 - 2 * toleft, 95), New PointF(88 - 2 * toleft, 98), New PointF(26 - 2 * toleft, 98)})
        'led16Seg.Add({New PointF(20 - toleft, 51), New PointF(26 - toleft, 57), New PointF(27 - 2 * toleft, 88), New PointF(21 - 2 * toleft, 93), New PointF(17 - 2 * toleft, 90), New PointF(17 - toleft, 54)})
        'led16Seg.Add({New PointF(21, 7), New PointF(27, 12), New PointF(27 - toleft, 43), New PointF(21 - toleft, 49), New PointF(18 - toleft, 46), New PointF(18, 10)})
        'led16Seg.Add({New PointF(23 - toleft, 50), New PointF(27 - toleft, 46), New PointF(86 - toleft, 46), New PointF(90 - toleft, 50), New PointF(86 - toleft, 54), New PointF(27 - toleft, 54)})

    End Sub

    Public Shared Function ShortFileName(ByVal longFileName As String) As String
        ' Cut filename after the first parenthesis

        Dim dir As String = Path.GetDirectoryName(longFileName)
        Dim fileNameOnly As String = Path.GetFileNameWithoutExtension(longFileName)

        If fileNameOnly.Contains(")") Then
            Return Path.Combine(dir, longFileName.Substring(0, longFileName.IndexOf(")") + 1))
        End If

        Return longFileName

    End Function

    Public Class FuzzyFileName

        ' Optimized function to calculate the Levenshtein distance between two strings
        Public Shared Function LevenshteinDistance(ByVal s As String, ByVal t As String) As Integer
            Dim n As Integer = s.Length
            Dim m As Integer = t.Length

            ' If one of the strings is empty, return the length of the other string
            If n = 0 Then Return m
            If m = 0 Then Return n

            ' Ensure n <= m to use less space
            If n > m Then
                Dim temp As String = s
                s = t
                t = temp
                n = s.Length
                m = t.Length
            End If

            ' Create two work vectors of integer distances
            Dim previousRow(n) As Integer
            Dim currentRow(n) As Integer

            ' Initialize the previous row
            For i As Integer = 0 To n
                previousRow(i) = i
            Next

            ' Compute the distance
            For j As Integer = 1 To m
                currentRow(0) = j
                For i As Integer = 1 To n
                    Dim cost As Integer = If(s(i - 1) = t(j - 1), 0, 1)
                    currentRow(i) = Math.Min(Math.Min(currentRow(i - 1) + 1, previousRow(i) + 1), previousRow(i - 1) + cost)
                Next
                ' Swap the current and previous rows
                Dim tempRow() As Integer = previousRow
                previousRow = currentRow
                currentRow = tempRow
            Next

            Return previousRow(n)
        End Function

        ' Function to calculate the percentage match between two strings
        Public Shared Function PercentageMatch(ByVal s As String, ByVal t As String) As Double
            Dim maxLength As Integer = Math.Max(s.Length, t.Length)
            If maxLength = 0 Then
                Return 100.0
            End If

            Dim distance As Integer = LevenshteinDistance(s, t)
            Return (1.0 - CDbl(distance) / maxLength) * 100.0
        End Function

        ' Function to normalize a string
        Public Shared Function NormalizeString(ByVal input As String) As String
            ' Convert to lowercase, remove special characters, and trim whitespace
            Dim normalized As String = input.ToLower()
            normalized = Regex.Replace(normalized, "[^\w\s]", "")
            normalized = normalized.Trim()
            Return normalized
        End Function

        ' Function to tokenize a string
        Public Shared Function TokenizeString(ByVal input As String) As List(Of String)
            ' Split the string into words
            Dim tokens As List(Of String) = New List(Of String)(input.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries))
            Return tokens
        End Function

        ' Function to calculate the Jaccard similarity between two sets of tokens
        Public Shared Function JaccardSimilarity(ByVal tokens1 As List(Of String), ByVal tokens2 As List(Of String)) As Double
            Dim intersection As Integer = tokens1.Intersect(tokens2).Count()
            Dim union As Integer = tokens1.Union(tokens2).Count()
            Return CDbl(intersection) / union
        End Function

        ' Function to find the best match among candidates
        Public Shared Function FindBestMatch(ByVal target As String, ByVal candidates As List(Of String)) As String
            Dim bestMatch As String = String.Empty
            Dim highestMatchScore As Double = 0.0

            ' Normalize and tokenize the target string
            Dim normalizedTarget As String = NormalizeString(target)
            Dim targetTokens As List(Of String) = TokenizeString(normalizedTarget)

            For Each candidate As String In candidates
                ' Normalize and tokenize the candidate string
                Dim normalizedCandidate As String = NormalizeString(candidate)
                Dim candidateTokens As List(Of String) = TokenizeString(normalizedCandidate)

                ' Calculate the Jaccard similarity
                Dim jaccardScore As Double = JaccardSimilarity(targetTokens, candidateTokens)

                ' Calculate the Levenshtein distance percentage match
                Dim levenshteinScore As Double = PercentageMatch(normalizedTarget, normalizedCandidate)

                ' Combine the scores (you can adjust the weights as needed)
                Dim combinedScore As Double = (jaccardScore + levenshteinScore) / 2

                If combinedScore > highestMatchScore Then
                    highestMatchScore = combinedScore
                    bestMatch = candidate
                End If
            Next

            Return bestMatch
        End Function
    End Class
End Class
