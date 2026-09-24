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
        Dim s As New StringBuilder()
        For Each value As Object In New Object() {ResultEnabled, ResultRollEnabled, ResultFlipperName, ResultGravity, ResultFlipperStrength,
            ResultBoundaryBounce, ResultLauncherEnabled, ResultLauncherFollowPivot, ResultLauncherX, ResultLauncherY,
            ResultLauncherAngle, ResultLauncherStrength, ResultLauncherRandomAngle, ResultLauncherRandomStrength, ResultLauncherCaptureRadius}
            s.Append(value).Append("|")
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
        Next
        Return s.ToString()
    End Function

    Private Sub ConfigurePhysicsTest(ByVal reset As Boolean)
        If testSession Is Nothing Then
            testSession = New PhysicsPreview.B2SData()
            AddHandler testSession.SwitchHit, AddressOf PhysicsTestSwitchHit
            reset = True
        End If
        Dim signature As String = PhysicsTestSignature()
        If Not reset AndAlso signature = testSignature Then Return
        Dim pivot As Illumination.BulbInfo = physicsSnippets.FirstOrDefault(Function(item) item.Name = ResultFlipperName AndAlso item.SnippitInfo.PivotAnimationEnabled)
        If reset OrElse pivot IsNot testPivotSource Then
            pivotFirePulse = False
            testPivotSource = pivot
            testSession.Pivot = Nothing
            If pivot IsNot Nothing Then
                testSession.Pivot = New PhysicsPreview.B2SPictureBox With {
                    .Left = pivot.Location.X, .Top = pivot.Location.Y, .Width = pivot.Size.Width, .Height = pivot.Size.Height,
                    .RectangleF = New RectangleF(pivot.Location, pivot.Size), .RotationPivotX = pivot.SnippitInfo.PivotX,
                    .RotationPivotY = pivot.SnippitInfo.PivotY, .RotationTipX = pivot.SnippitInfo.PivotTipX,
                    .RotationTipY = pivot.SnippitInfo.PivotTipY, .PivotDownAngle = pivot.SnippitInfo.PivotDownAngle,
                    .PivotRandomStrength = pivot.SnippitInfo.PivotRandomStrength,
                    .PivotAutomaticOscillation = pivot.SnippitInfo.PivotAutomaticOscillation,
                    .RotationAngle = If(pivot.SnippitInfo.PivotAutomaticOscillation, 0.0F, pivot.SnippitInfo.PivotDownAngle)}
                pivotStartAngle = testSession.Pivot.RotationAngle
                pivotTargetAngle = If(pivot.SnippitInfo.PivotAutomaticOscillation, pivot.SnippitInfo.PivotUpAngle, pivotStartAngle)
                pivotElapsed = 0.0R
            End If
        End If
        Dim zones As New List(Of PhysicsPreview.B2SData.PhysicsSwitchZone)()
        For index As Integer = 0 To canvas.SwitchZones.Count - 1
            zones.Add(New PhysicsPreview.B2SData.PhysicsSwitchZone With {.Bounds = canvas.SwitchZones(index),
                      .SwitchID = canvas.SwitchIDs(index), .Angle = canvas.SwitchAngles(index)})
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
                              ResultBoundaryBounce, ResultBoundaryPaths, ResultObstacles, zones, launcher, ResultBoundarySegmentBounces, reset, ResultObstacleBounces)
        testSignature = signature
        canvas.PreviewPivot = testSession.Pivot : canvas.PreviewPivotName = ResultFlipperName
        pivotTestButton.Enabled = testSession.Pivot IsNot Nothing AndAlso Not testSession.Pivot.PivotAutomaticOscillation
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
        If testSession.Pivot IsNot Nothing AndAlso Not testSession.Pivot.PivotAutomaticOscillation Then
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
        Dim elapsed As Double = testClock.Elapsed.TotalSeconds
        testClock.Restart()
        If Not ResultEnabled Then
            EndPhysicsTest()
            Return
        End If
        ConfigurePhysicsTest(False)
        If Not testPaused Then
            AdvanceTestPivot(elapsed)
            testSession.Advance(elapsed)
        End If
        ShowPhysicsTestFrame()
    End Sub

    Private Sub AdvanceTestPivot(ByVal elapsed As Double)
        If testSession.Pivot Is Nothing OrElse testPivotSource Is Nothing Then Return
        Dim info = testPivotSource.SnippitInfo
        Dim pivot = testSession.Pivot
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

    Private Sub ShowPhysicsTestFrame()
        canvas.PreviewBallBounds = testSession.Body.RectangleF
        canvas.PreviewBallAngle = testSession.Body.RollAngle
        fireTestButton.Enabled = testSession.CanFire AndAlso Not pivotFirePulse
        testStatus.Text = If(testPaused, "Paused — adjustments apply on Resume.",
                             If(ResultLauncherEnabled AndAlso testSession.CanFire, "Ready — Fire Ball.", "Ball in play — adjust settings live or Reset Ball.")) & lastSwitchText
        canvas.Invalidate()
    End Sub

    Private Sub EndPhysicsTest()
        testTimer.Stop() : testClock.Reset()
        If testSession IsNot Nothing Then testSession.Dispose() : testSession = Nothing
        testSignature = Nothing : testPivotSource = Nothing : pivotHeld = False : pivotFirePulse = False
        canvas.PreviewBallBounds = Nothing : canvas.PreviewPivot = Nothing : canvas.PreviewSwitchHits.Clear()
        fireTestButton.Enabled = True : pauseTestButton.Enabled = False : pauseTestButton.Text = "Pause"
        testStatus.Text = "Test ended. Save Boundaries keeps edits; Cancel discards them."
        canvas.Invalidate()
    End Sub

    Private Sub DisposePhysicsTest()
        EndPhysicsTest()
        testTimer.Dispose()
    End Sub
End Class
