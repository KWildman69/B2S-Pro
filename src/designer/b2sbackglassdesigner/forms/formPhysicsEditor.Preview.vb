Imports System.Drawing
Imports System.Linq
Imports System.Text
Imports System.Windows.Forms

Public Partial Class formPhysicsEditor
    Private ReadOnly physicsSnippets As List(Of Illumination.BulbInfo)
    Private ReadOnly testTimer As New Timer With {.Interval = 16}
    Private ReadOnly testClock As New Diagnostics.Stopwatch()
    Private ReadOnly fireTestButton As New Button()
    Private ReadOnly pauseTestButton As New Button()
    Private ReadOnly testStatus As New Label()
    Private ReadOnly pivotTestButton As New Button()
    Private testSession As PhysicsPreview.B2SData
    Private testPaused As Boolean
    Private testSignature As String
    Private testPivotSource As Illumination.BulbInfo
    Private pivotHeld As Boolean
    Private pivotFirePulse As Boolean
    Private pivotElapsed As Double
    Private pendingTestSeconds As Double
    Private pivotStartAngle As Single
    Private pivotTargetAngle As Single
    Private lastSwitchText As String = ""

    Private Function CreatePhysicsTestControls() As Control
        Dim panel As New TableLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 140, .ColumnCount = 4, .RowCount = 3, .Padding = New Padding(3)}
        For index As Integer = 0 To 3 : panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F)) : Next
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Dim reset As New Button(), finish As New Button()
        ConfigureButton(fireTestButton, "Fire Ball", AddressOf FireTestBall)
        ConfigureButton(pauseTestButton, "Pause", AddressOf PausePhysicsTest)
        ConfigureButton(reset, "Reset Ball", Sub() ResetPhysicsTest())
        ConfigureButton(finish, "End Test", Sub() EndPhysicsTest())
        ConfigureButton(pivotTestButton, "Hold Pivot", Sub() Return)
        AddHandler pivotTestButton.MouseDown, AddressOf HoldTestPivot
        AddHandler pivotTestButton.MouseUp, Sub() pivotHeld = False
        AddHandler pivotTestButton.MouseCaptureChanged, Sub()
                                                           If Not pivotTestButton.Capture Then pivotHeld = False
                                                       End Sub
        testStatus.AutoSize = False : testStatus.Width = 395 : testStatus.Height = 46
        testStatus.ForeColor = Color.White
        testStatus.Text = "LOCAL PHYSICS TEST — Fire to begin. Adjust settings while it runs. Save Boundaries keeps your changes."
        panel.Controls.Add(fireTestButton, 0, 0) : panel.Controls.Add(pauseTestButton, 1, 0)
        panel.Controls.Add(reset, 2, 0) : panel.Controls.Add(finish, 3, 0)
        panel.Controls.Add(pivotTestButton, 0, 1) : panel.SetColumnSpan(pivotTestButton, 2)
        panel.Controls.Add(testStatus, 0, 2) : panel.SetColumnSpan(testStatus, 4)
        For Each control As Control In panel.Controls
            control.AutoSize = False : control.Dock = DockStyle.Fill
        Next
        pauseTestButton.Enabled = False
        AddHandler testTimer.Tick, AddressOf PhysicsTestTick
        Return panel
    End Function

    Private Function PhysicsTestSignature() As String
        CaptureActivePivotPhysicsSettings()
        Dim s As New StringBuilder()
        For Each value As Object In New Object() {ResultEnabled, ResultRollEnabled, ResultFlipperName,
            ResultPivotPhysicsEnabled, ResultPivotPhysicsWeight, ResultPivotPhysicsFriction, ResultPivotPhysicsGrip,
            ResultPivotPhysicsResponseMode, ResultPivotPhysicsKnockdownAngle, ResultPivotPhysicsKnockdownDuration, ResultPivotPhysicsResetDelay,
            ResultGravity, ResultFlipperStrength,
            ResultBoundaryBounce, ResultLauncherEnabled, ResultLauncherFollowPivot, ResultLauncherX, ResultLauncherY,
            ResultLauncherAngle, ResultLauncherStrength, ResultLauncherRandomAngle, ResultLauncherRandomStrength, ResultLauncherCaptureRadius}
            s.Append(value).Append("|")
        Next
        For Each targetName As String In ResultPivotTargetNames.OrderBy(Function(name) name)
            s.Append("T").Append(targetName).Append("|")
            Dim targetSource As Illumination.BulbInfo = physicsSnippets.FirstOrDefault(Function(item) item.Name = targetName AndAlso item.SnippitInfo.PivotAnimationEnabled)
            If targetSource IsNot Nothing Then
                Dim targetState = PivotPhysicsSettingsFor(targetSource)
                s.Append(targetState.Weight).Append("|").Append(targetState.Friction).Append("|").Append(targetState.Grip).Append("|")
                s.Append(targetState.ResponseMode).Append("|").Append(targetState.KnockdownAngle).Append("|")
                s.Append(targetState.KnockdownDuration).Append("|").Append(targetState.ResetDelay).Append("|")
            End If
        Next
        For Each path As List(Of PointF) In canvas.Paths
            s.Append("P")
            For Each point As PointF In path : s.Append(point.X).Append(",").Append(point.Y).Append(";") : Next
        Next
        For Each values As List(Of Single) In canvas.BoundarySegmentBounces
            s.Append("B") : For Each value As Single In values : s.Append(value).Append(";") : Next
        Next
        For Each value As Single In canvas.ObstacleBounces : s.Append(value.ToString("R", Globalization.CultureInfo.InvariantCulture)).Append(";") : Next
        For Each obstacle As RectangleF In canvas.Obstacles : s.Append(obstacle.ToString()).Append(";") : Next
        For index As Integer = 0 To canvas.SwitchZones.Count - 1
            s.Append(canvas.SwitchZones(index).ToString()).Append(",").Append(canvas.SwitchIDs(index)).Append(",").Append(canvas.SwitchAngles(index)).Append(";")
            If index < canvas.SwitchSoundNames.Count Then s.Append("S").Append(canvas.SwitchSoundNames(index)).Append("|")
            If index < canvas.SwitchSoundVolumes.Count Then s.Append("V").Append(canvas.SwitchSoundVolumes(index)).Append("|")
        Next
        Return s.ToString()
    End Function

    Private Sub ConfigurePhysicsTest(ByVal reset As Boolean)
        If testSession Is Nothing Then
            testSession = New PhysicsPreview.B2SData()
            AddHandler testSession.SwitchHit, AddressOf PhysicsTestSwitchHit
            AddHandler testSession.SoundHit, AddressOf PhysicsTestSoundHit
            reset = True
        End If
        Dim signature As String = PhysicsTestSignature()
        If Not reset AndAlso signature = testSignature Then Return
        Dim targetNames As List(Of String) = ResultPivotTargetNames
        Dim pivot As Illumination.BulbInfo = physicsSnippets.FirstOrDefault(Function(item) item.Name = ResultFlipperName AndAlso item.SnippitInfo.PivotAnimationEnabled)
        Dim previewNames As New List(Of String)(targetNames)
        If pivot IsNot Nothing AndAlso Not previewNames.Any(Function(name) String.Equals(name, pivot.Name, StringComparison.OrdinalIgnoreCase)) Then previewNames.Add(pivot.Name)
        Dim pivotSetChanged As Boolean = testSession.Pivots.Count <> previewNames.Count OrElse previewNames.Any(Function(name) Not testSession.Pivots.ContainsKey(name))
        If reset OrElse pivot IsNot testPivotSource OrElse pivotSetChanged Then
            pivotFirePulse = False
            testPivotSource = pivot
            testSession.Pivot = Nothing
            testSession.Pivots.Clear()
            For Each targetName As String In previewNames
                Dim targetSource As Illumination.BulbInfo = physicsSnippets.FirstOrDefault(Function(item) item.Name = targetName AndAlso item.SnippitInfo.PivotAnimationEnabled)
                If targetSource Is Nothing Then Continue For
                Dim targetState = PivotPhysicsSettingsFor(targetSource)
                Dim previewPivot As New PhysicsPreview.B2SPictureBox With {
                    .Left = targetSource.Location.X, .Top = targetSource.Location.Y, .Width = targetSource.Size.Width, .Height = targetSource.Size.Height,
                    .RectangleF = New RectangleF(targetSource.Location, targetSource.Size), .RotationPivotX = targetSource.SnippitInfo.PivotX,
                    .RotationPivotY = targetSource.SnippitInfo.PivotY, .RotationTipX = targetSource.SnippitInfo.PivotTipX,
                    .RotationTipY = targetSource.SnippitInfo.PivotTipY, .PivotDownAngle = targetSource.SnippitInfo.PivotDownAngle,
                    .PivotRandomStrength = targetSource.SnippitInfo.PivotRandomStrength,
                    .PivotAutomaticOscillation = targetSource.SnippitInfo.PivotAutomaticOscillation,
                    .PivotPhysicsEnabled = targetNames.Any(Function(name) String.Equals(name, targetName, StringComparison.OrdinalIgnoreCase)),
                    .PivotPhysicsWeight = targetState.Weight,
                    .PivotPhysicsFriction = targetState.Friction,
                    .PivotPhysicsGrip = targetState.Grip,
                    .PivotPhysicsResponseMode = targetState.ResponseMode,
                    .PivotPhysicsKnockdownAngle = targetState.KnockdownAngle,
                    .PivotPhysicsKnockdownDuration = targetState.KnockdownDuration,
                    .PivotPhysicsResetDelay = targetState.ResetDelay,
                    .RotationAngle = If(targetSource.SnippitInfo.PivotAutomaticOscillation, 0.0F, targetSource.SnippitInfo.PivotDownAngle)}
                Dim targetScene As SceneItem = canvas.Scene.FirstOrDefault(
                    Function(item) String.Equals(item.Name, targetName, StringComparison.OrdinalIgnoreCase))
                If targetScene IsNot Nothing Then ConfigurePivotCollisionOutline(previewPivot, targetScene.Image, targetSource.Size)
                testSession.Pivots(targetName) = previewPivot
            Next
            If pivot IsNot Nothing Then
                testSession.Pivots.TryGetValue(pivot.Name, testSession.Pivot)
            End If
            If testSession.Pivot IsNot Nothing Then
                pivotStartAngle = testSession.Pivot.RotationAngle
                pivotTargetAngle = If(pivot.SnippitInfo.PivotAutomaticOscillation, pivot.SnippitInfo.PivotUpAngle, pivotStartAngle)
                pivotElapsed = 0.0R
            End If
        End If
        If testSession.Pivot IsNot Nothing Then
            testSession.Pivot.PivotPhysicsEnabled = ResultPivotPhysicsEnabled
            testSession.Pivot.PivotPhysicsWeight = ResultPivotPhysicsWeight
            testSession.Pivot.PivotPhysicsFriction = ResultPivotPhysicsFriction
            testSession.Pivot.PivotPhysicsGrip = ResultPivotPhysicsGrip
            testSession.Pivot.PivotPhysicsResponseMode = ResultPivotPhysicsResponseMode
            testSession.Pivot.PivotPhysicsKnockdownAngle = ResultPivotPhysicsKnockdownAngle
            testSession.Pivot.PivotPhysicsKnockdownDuration = ResultPivotPhysicsKnockdownDuration
            testSession.Pivot.PivotPhysicsResetDelay = ResultPivotPhysicsResetDelay
        End If
        Dim zones As New List(Of PhysicsPreview.B2SData.PhysicsSwitchZone)()
        For index As Integer = 0 To canvas.SwitchZones.Count - 1
            zones.Add(New PhysicsPreview.B2SData.PhysicsSwitchZone With {.Bounds = canvas.SwitchZones(index),
                      .SwitchID = canvas.SwitchIDs(index), .Angle = canvas.SwitchAngles(index),
                      .SoundName = If(index < canvas.SwitchSoundNames.Count, canvas.SwitchSoundNames(index), String.Empty),
                      .SoundVolume = Math.Max(0.0F, Math.Min(2.0F, If(index < canvas.SwitchSoundVolumes.Count, canvas.SwitchSoundVolumes(index) / 100.0F, 1.0F)))})
        Next
        Dim launcher As PhysicsPreview.B2SData.PhysicsLauncher = Nothing
        If ResultLauncherEnabled Then
            launcher = New PhysicsPreview.B2SData.PhysicsLauncher With {.Origin = New PointF(ResultLauncherX, ResultLauncherY),
                .Angle = ResultLauncherAngle, .Strength = ResultLauncherStrength, .RandomAngle = ResultLauncherRandomAngle,
                .RandomStrength = ResultLauncherRandomStrength, .CaptureRadius = ResultLauncherCaptureRadius, .FollowPivot = ResultLauncherFollowPivot}
        End If
        Dim ballBounds As New Rectangle(sourceBall.Location, sourceBall.Size)
        If ResultLauncherEnabled AndAlso ResultLauncherFollowPivot Then
            ballBounds.Location = New Point(CInt(Math.Round(ResultLauncherX - ballBounds.Width / 2.0F)),
                                            CInt(Math.Round(ResultLauncherY - ballBounds.Height / 2.0F)))
        End If
        testSession.Body.RollEnabled = ResultRollEnabled
        testSession.Configure(ballBounds, New RectangleF(PointF.Empty, canvas.AuthoredSize), ResultGravity, ResultFlipperStrength,
                              ResultBoundaryBounce, ResultBoundaryPaths, ResultObstacles, zones, launcher, ResultBoundarySegmentBounces, reset,
                              ResultObstacleBounces, targetNames, ResultFlipperName)
        testSignature = signature
        canvas.PreviewPivot = testSession.Pivot : canvas.PreviewPivotName = ResultFlipperName
        canvas.PreviewPivots.Clear()
        For Each pair In testSession.Pivots
            canvas.PreviewPivots(pair.Key) = pair.Value
        Next
        pivotTestButton.Enabled = testSession.Pivot IsNot Nothing AndAlso Not testSession.Pivot.PivotAutomaticOscillation AndAlso
                                  Not testSession.Pivot.PivotPhysicsEnabled
    End Sub

    Private Function CanStartPhysicsTest() As Boolean
        If Not ResultEnabled Then
            testStatus.Text = "Enable ball physics on the Ball tab to test."
            Return False
        End If
        If ResultLauncherEnabled AndAlso ResultLauncherFollowPivot AndAlso ResultFlipperName.Length = 0 Then
            testStatus.Text = "Select a pivot snippet on the Ball tab for the attached launcher."
            Return False
        End If
        Return True
    End Function

    Private Sub ResetPhysicsTest()
        If Not CanStartPhysicsTest() Then Return
        pendingTestSeconds = 0.0R
        pivotHeld = False
        ConfigurePhysicsTest(True)
        canvas.PreviewSwitchHits.Clear() : lastSwitchText = ""
        testPaused = False : pauseTestButton.Text = "Pause" : pauseTestButton.Enabled = True
        testSession.Advance(0.001R)
        ShowPhysicsTestFrame()
        testClock.Restart() : testTimer.Start()
    End Sub

    Private Sub FireTestBall(ByVal sender As Object, ByVal e As EventArgs)
        If Not CanStartPhysicsTest() Then Return
        If testSession Is Nothing Then ResetPhysicsTest()
        ConfigurePhysicsTest(False)
        If testSession.Pivot IsNot Nothing AndAlso Not testSession.Pivot.PivotAutomaticOscillation AndAlso
           Not testSession.Pivot.PivotPhysicsEnabled Then
            pivotFirePulse = True
        End If
        testSession.Fire()
        testPaused = False : pauseTestButton.Text = "Pause"
        testClock.Restart() : testTimer.Start()
        ShowPhysicsTestFrame()
    End Sub

    Private Sub HoldTestPivot(ByVal sender As Object, ByVal e As MouseEventArgs)
        If e.Button <> MouseButtons.Left OrElse Not CanStartPhysicsTest() Then Return
        If testSession Is Nothing Then ResetPhysicsTest()
        ConfigurePhysicsTest(False)
        If testSession.Pivot Is Nothing OrElse testSession.Pivot.PivotAutomaticOscillation Then Return
        pivotHeld = True
        testPaused = False : pauseTestButton.Text = "Pause"
        testClock.Restart() : testTimer.Start()
        ShowPhysicsTestFrame()
    End Sub

    Private Sub PausePhysicsTest(ByVal sender As Object, ByVal e As EventArgs)
        If testSession Is Nothing Then Return
        testPaused = Not testPaused
        pauseTestButton.Text = If(testPaused, "Resume", "Pause")
        testClock.Restart()
        ShowPhysicsTestFrame()
    End Sub

    Private Sub PhysicsTestTick(ByVal sender As Object, ByVal e As EventArgs)
        If testSession Is Nothing Then Return
        ' NumericUpDown keeps partially typed text (for example "." while the
        ' user is entering ".25") separate from its last committed Value.
        ' Reading Value during that edit validates the partial text and causes
        ' the next digit to be combined with the old value.  Do not poll any
        ' physics values until the active number editor has committed on leave.
        If PhysicsNumberEditorHasFocus(Me) Then
            testClock.Restart()
            Return
        End If
        Dim elapsed As Double = testClock.Elapsed.TotalSeconds
        testClock.Restart()
        If Not ResultEnabled Then
            EndPhysicsTest()
            Return
        End If
        ConfigurePhysicsTest(False)
        If Not testPaused Then
            AdvancePhysicsTest(elapsed)
        End If
        ShowPhysicsTestFrame()
    End Sub

    Private Shared Function PhysicsNumberEditorHasFocus(ByVal parent As Control) As Boolean
        For Each child As Control In parent.Controls
            If TypeOf child Is NumericUpDown AndAlso child.ContainsFocus Then Return True
            If child.HasChildren AndAlso PhysicsNumberEditorHasFocus(child) Then Return True
        Next
        Return False
    End Function

    Private Sub AdvancePhysicsTest(ByVal elapsed As Double)
        If elapsed <= 0.0R OrElse Double.IsNaN(elapsed) OrElse Double.IsInfinity(elapsed) Then Return
        pendingTestSeconds += elapsed
        Dim steps As Integer = CInt(Math.Min(250, Math.Floor((pendingTestSeconds + 0.000000001R) / 0.001R)))
        ' Sample the pivot at each physics step, not just at the rendered frame.
        ' Otherwise a delayed frame changes the contact angle and impact speed.
        For index As Integer = 1 To steps
            AdvanceTestPivot(0.001R)
            testSession.Advance(0.001R)
        Next
        pendingTestSeconds = Math.Max(0.0R, pendingTestSeconds - steps * 0.001R)
    End Sub

    Private Sub AdvanceTestPivot(ByVal elapsed As Double)
        For Each pair In testSession.Pivots
            If Not Object.ReferenceEquals(pair.Value, testSession.Pivot) AndAlso pair.Value.PivotPhysicsEnabled Then
                pair.Value.AdvancePhysicsPivot(elapsed)
            End If
        Next
        If testSession.Pivot Is Nothing OrElse testPivotSource Is Nothing Then Return
        Dim info = testPivotSource.SnippitInfo
        Dim pivot = testSession.Pivot
        If pivot.PivotPhysicsEnabled Then
            pivot.AdvancePhysicsPivot(elapsed)
            Return
        End If
        If Not info.PivotAutomaticOscillation Then
            Dim target As Single = If(pivotHeld OrElse pivotFirePulse, info.PivotUpAngle, info.PivotDownAngle)
            If target <> pivotTargetAngle Then
                pivotStartAngle = pivot.RotationAngle : pivotTargetAngle = target : pivotElapsed = 0.0R
            End If
        End If
        pivotElapsed += elapsed * 1000.0R
        Dim duration As Double = Math.Max(10, Math.Min(If(info.PivotAutomaticOscillation, 5000, 1000), info.PivotDuration))
        Dim progress As Single = CSng(Math.Min(1.0R, pivotElapsed / duration))
        Dim eased As Single = progress * progress * (3.0F - 2.0F * progress)
        pivot.RotationAngle = pivotStartAngle + (pivotTargetAngle - pivotStartAngle) * eased
        If progress >= 1.0F AndAlso Not info.PivotAutomaticOscillation Then pivotFirePulse = False
        If progress >= 1.0F AndAlso info.PivotAutomaticOscillation Then
            pivotStartAngle = pivotTargetAngle
            pivotTargetAngle = If(Math.Abs(pivotTargetAngle - info.PivotUpAngle) < 0.01F, info.PivotDownAngle, info.PivotUpAngle)
            pivotElapsed = 0.0R
        End If
    End Sub

    Private Sub PhysicsTestSwitchHit(ByVal id As Integer)
        canvas.PreviewSwitchHits(id) = DateTime.UtcNow.AddMilliseconds(500)
        lastSwitchText = " • Switch " & id.ToString() & " activated"
    End Sub

    Private Sub PhysicsTestSoundHit(ByVal soundName As String, ByVal soundVolume As Single)
        Dim index As Integer = canvas.SwitchSoundNames.FindIndex(Function(name) String.Equals(name, soundName, StringComparison.OrdinalIgnoreCase))
        If index < 0 OrElse index >= canvas.SwitchSoundData.Count OrElse String.IsNullOrWhiteSpace(canvas.SwitchSoundData(index)) Then Return
        Try
            Dim waveData As Byte() = Convert.FromBase64String(canvas.SwitchSoundData(index))
            My.Computer.Audio.Play(PhysicsPreview.PhysicsSoundVolume.Apply(waveData, soundVolume), AudioPlayMode.Background)
            testStatus.Text = "Sound sensor: " & soundName & " at " & CInt(Math.Round(soundVolume * 100.0F)).ToString() & "%"
        Catch ex As Exception
            testStatus.Text = "Sound sensor WAV could not be played."
        End Try
    End Sub

    Private Sub ShowPhysicsTestFrame()
        canvas.PreviewBallBounds = testSession.Body.RectangleF
        canvas.PreviewBallAngle = testSession.Body.RollAngle
        fireTestButton.Enabled = testSession.CanFire AndAlso Not pivotFirePulse
        testStatus.Text = If(testPaused, "Paused — adjustments apply on Resume.",
                             If(ResultLauncherEnabled AndAlso testSession.CanFire, "Ready — Fire Ball.", "Ball in play — adjust settings live or Reset Ball.")) & lastSwitchText
        canvas.Invalidate()
    End Sub

    Private Shared Sub ConfigurePivotCollisionOutline(ByVal pivot As PhysicsPreview.B2SPictureBox,
                                                      ByVal source As Image,
                                                      ByVal authoredSize As Size)
        pivot.PhysicsCollisionMaskAlpha = Nothing
        pivot.PhysicsCollisionMaskSize = Size.Empty
        pivot.PhysicsCollisionContour = Nothing
        If source Is Nothing OrElse authoredSize.Width <= 0 OrElse authoredSize.Height <= 0 Then Return

        Dim maskWidth As Integer = Math.Max(1, Math.Min(512, authoredSize.Width))
        Dim maskHeight As Integer = Math.Max(1, Math.Min(512, authoredSize.Height))
        Dim alpha(maskWidth * maskHeight - 1) As Byte
        Dim hasSolid As Boolean = False

        Using bitmap As New Bitmap(maskWidth, maskHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb)
            Using graphics As Graphics = Graphics.FromImage(bitmap)
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality
                graphics.Clear(Color.Transparent)
                graphics.DrawImage(source, New Rectangle(0, 0, maskWidth, maskHeight))
            End Using

            Dim data As System.Drawing.Imaging.BitmapData = bitmap.LockBits(
                New Rectangle(0, 0, maskWidth, maskHeight),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb)
            Try
                Dim row(maskWidth * 4 - 1) As Byte
                For y As Integer = 0 To maskHeight - 1
                    Runtime.InteropServices.Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length)
                    For x As Integer = 0 To maskWidth - 1
                        Dim value As Byte = If(row(x * 4 + 3) > 0, CByte(255), CByte(0))
                        alpha(y * maskWidth + x) = value
                        If value <> 0 Then hasSolid = True
                    Next
                Next
            Finally
                bitmap.UnlockBits(data)
            End Try
        End Using

        If hasSolid Then
            pivot.PhysicsCollisionMaskAlpha = alpha
            pivot.PhysicsCollisionMaskSize = New Size(maskWidth, maskHeight)
            pivot.PhysicsCollisionContour = PhysicsPreview.B2SData.BuildPhysicsContour(
                alpha, maskWidth, maskHeight, pivot.RotationPivotX, pivot.RotationPivotY)
        End If
    End Sub

    Private Sub EndPhysicsTest()
        testTimer.Stop() : testClock.Reset()
        pendingTestSeconds = 0.0R
        If testSession IsNot Nothing Then testSession.Dispose() : testSession = Nothing
        testSignature = Nothing : testPivotSource = Nothing : pivotHeld = False : pivotFirePulse = False
        canvas.PreviewBallBounds = Nothing : canvas.PreviewPivot = Nothing : canvas.PreviewPivots.Clear() : canvas.PreviewSwitchHits.Clear()
        fireTestButton.Enabled = True : pauseTestButton.Enabled = False : pauseTestButton.Text = "Pause"
        testStatus.Text = "Test ended. Save Boundaries keeps edits; Cancel discards them."
        canvas.Invalidate()
    End Sub

    Private Sub DisposePhysicsTest()
        EndPhysicsTest()
        testTimer.Dispose()
    End Sub
End Class
