Imports System.Drawing.Drawing2D
Imports System.Diagnostics
Imports System.Windows.Forms

Public Class formPivotAnimation
    Inherits B2SThemedForm

    Private ReadOnly snippet As Illumination.BulbInfo
    Private ReadOnly canvas As New PivotCanvas()
    Private ReadOnly enabledBox As New CheckBox With {.Text = "Enable pivot animation", .AutoSize = True}
    Private ReadOnly physicsOnlyBox As New CheckBox With {.Text = "Physics impact only (one pivot point)", .AutoSize = True}
    Private ReadOnly automaticBox As New CheckBox With {.Text = "Automatic Pivot Point: swing while trigger is on", .AutoSize = True}
    Private ReadOnly downAngle As New NumericUpDown()
    Private ReadOnly upAngle As New NumericUpDown()
    Private ReadOnly duration As New NumericUpDown()
    Private ReadOnly randomStrength As New NumericUpDown()
    Private ReadOnly triggerType As New ComboBox()
    Private ReadOnly triggerID As New NumericUpDown()
    Private ReadOnly downTrigger As New TextBox()
    Private ReadOnly upTrigger As New TextBox()
    Private ReadOnly reverseBox As New CheckBox With {.Text = "Reverse trigger: rest while ON, move up while OFF", .AutoSize = True}
    Private ReadOnly automaticPreview As New Button With {.Text = "Automatic Preview", .Width = 310, .Height = 31}
    Private ReadOnly previewTimer As New Timer With {.Interval = 15}
    Private ReadOnly previewClock As New Stopwatch()
    Private previewStartAngle As Single
    Private previewTargetAngle As Single
    Private previewRunning As Boolean
    Private previewLoops As Boolean
    ' Which one-time position preview is playing (True = Up, False = Down).
    Private previewOneShotUp As Boolean
    Private previewCurrentAngle As Single
    ' Pivot-to-pivot strike settings and the Test Strike simulation.
    Private ReadOnly strikeBox As New CheckBox With {.Text = "Strikes a physics pivot", .AutoSize = True}
    Private ReadOnly strikeTarget As New ComboBox()
    Private ReadOnly strikeStrength As New NumericUpDown()
    Private ReadOnly strikeContactBox As New CheckBox With {.Text = "Contact: shapes must touch", .AutoSize = True}
    Private ReadOnly strikeDirection As New ComboBox()
    Private ReadOnly strikeKick As New PhysicsPreview.B2SData.PivotKickState()
    Private ReadOnly testStrike As New Button With {.Text = "Test Strike", .Width = 310, .Height = 31}
    Private ReadOnly strikeTimer As New Timer With {.Interval = 15}
    Private ReadOnly strikeClock As New Stopwatch()
    Private ReadOnly strikeCandidates As New List(Of Illumination.BulbInfo)()
    Private strikeRunning As Boolean
    Private strikeTestTarget As Illumination.BulbInfo
    Private strikeDriverShape As PhysicsPreview.B2SData.PivotStrikeShape
    Private strikeTargetShape As PhysicsPreview.B2SData.PivotStrikeShape
    Private strikeTargetAngle As Single
    Private strikeTargetVelocity As Single
    Private strikeDriverAngle As Single
    Private strikeTimeMs As Double
    Private strikeLastClockMs As Double
    Private strikeDownValue As Single
    Private strikeUpValue As Single
    ' Counterweight for a physics-impact pivot, and its Test Spin.
    Private ReadOnly weightBox As New CheckBox With {.Text = "Counterweight: gravity pulls this point down", .AutoSize = True}
    Private ReadOnly weightStrength As New NumericUpDown()
    Private ReadOnly placeWeight As New Button With {.Text = "Place Counterweight", .Width = 310, .Height = 31}
    Private ReadOnly testSpin As New Button With {.Text = "Test Spin", .Width = 310, .Height = 31}
    Private ReadOnly weightLabel As New Label With {.Text = "Click Place Counterweight, then click the image where the weight sits (for example the boots). After a spin it rocks and always settles with that point hanging straight down.",
                                                    .ForeColor = Color.White, .Width = 310, .Height = 70}
    Private ReadOnly spinTimer As New Timer With {.Interval = 15}
    Private ReadOnly spinClock As New Stopwatch()
    Private spinRunning As Boolean
    Private spinAngle As Single
    Private spinVelocity As Single
    Private spinLastClockMs As Double
    Private spinTimeMs As Double

    Public Sub New(ByVal source As Illumination.BulbInfo, ByVal backglassImage As Image,
                   Optional ByVal snippets As IEnumerable(Of Illumination.BulbInfo) = Nothing)
        snippet = source
        If snippets IsNot Nothing Then
            For Each candidate As Illumination.BulbInfo In snippets
                If candidate IsNot source AndAlso candidate.IsImageSnippit AndAlso candidate.Image IsNot Nothing AndAlso
                   candidate.SnippitInfo.PivotAnimationEnabled AndAlso Not String.IsNullOrWhiteSpace(candidate.Name) Then
                    strikeCandidates.Add(candidate)
                End If
            Next
        End If
        Text = "Pivot Animation — " & If(String.IsNullOrWhiteSpace(source.Name), "Snippet", source.Name)
        StartPosition = FormStartPosition.CenterParent : Size = New Size(920, 650) : MinimumSize = New Size(800, 560)
        enabledBox.Checked = source.SnippitInfo.PivotAnimationEnabled
        physicsOnlyBox.Checked = source.SnippitInfo.PivotPhysicsEnabled
        automaticBox.Checked = source.SnippitInfo.PivotAutomaticOscillation
        SetupNumber(downAngle, -360D, 360D, source.SnippitInfo.PivotDownAngle, 1)
        SetupNumber(upAngle, -360D, 360D, source.SnippitInfo.PivotUpAngle, 1)
        SetupNumber(duration, 10D, 5000D, source.SnippitInfo.PivotDuration, 0)
        SetupNumber(randomStrength, 0D, 100D, source.SnippitInfo.PivotRandomStrength, 1)
        triggerType.DropDownStyle = ComboBoxStyle.DropDownList : triggerType.Dock = DockStyle.Fill
        triggerType.Items.AddRange(New Object() {"Named commands (advanced)", "ROM solenoid", "ROM lamp", "B2S ID", "Automatic when backglass starts", "SW"})
        Dim detectedNamedPair As Boolean = (Not source.SnippitInfo.PivotAnimationEnabled AndAlso source.Name.EndsWith("_down", StringComparison.OrdinalIgnoreCase))
        triggerType.SelectedIndex = If(detectedNamedPair, 0, Math.Max(0, Math.Min(5, source.SnippitInfo.PivotTriggerType)))
        SetupNumber(triggerID, 0D, 255D, source.SnippitInfo.PivotTriggerID, 0)
        downTrigger.Text = If(detectedNamedPair, source.Name, source.SnippitInfo.PivotDownTrigger)
        upTrigger.Text = If(detectedNamedPair, source.Name.Substring(0, source.Name.Length - 5) & "_up", source.SnippitInfo.PivotUpTrigger)
        downTrigger.MaxLength = 64 : upTrigger.MaxLength = 64
        canvas.BackglassImage = backglassImage
        canvas.SnippetBounds = New RectangleF(source.Location.X, source.Location.Y,
                                               Math.Max(1, source.Size.Width), Math.Max(1, source.Size.Height))
        canvas.SourceImage = source.Image : canvas.Pivot = New PointF(source.SnippitInfo.PivotX, source.SnippitInfo.PivotY)
        canvas.Tip = New PointF(source.SnippitInfo.PivotTipX, source.SnippitInfo.PivotTipY)
        canvas.PreviewAngle = 0.0F

        Dim sidebar As New Panel With {.Dock = DockStyle.Right, .Width = 350,
                                      .BackColor = Color.FromArgb(22, 25, 38)}
        Dim fields As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(10),
                                                .FlowDirection = FlowDirection.TopDown, .WrapContents = False,
                                                .AutoScroll = True, .BackColor = Color.FromArgb(22, 25, 38)}
        enabledBox.ForeColor = Color.White : enabledBox.AutoSize = False : enabledBox.Width = 310 : enabledBox.Height = 26
        fields.Controls.Add(SectionHeader("ANIMATION"))
        fields.Controls.Add(enabledBox)
        physicsOnlyBox.ForeColor = Color.White : physicsOnlyBox.AutoSize = False : physicsOnlyBox.Width = 310 : physicsOnlyBox.Height = 30
        fields.Controls.Add(physicsOnlyBox)
        fields.Controls.Add(New Label With {.Text = "Physics impact only: place one hinge point. Ball contact controls the direction and speed.",
                                             .ForeColor = Color.White, .Width = 310, .Height = 42})
        weightBox.ForeColor = Color.White : weightBox.AutoSize = False : weightBox.Width = 310 : weightBox.Height = 30
        weightBox.Checked = source.SnippitInfo.PivotWeightEnabled
        fields.Controls.Add(weightBox)
        SetupNumber(weightStrength, 1D, 100D, CDec(source.SnippitInfo.PivotWeightStrength), 0)
        fields.Controls.Add(FieldRow("Weight strength", weightStrength))
        fields.Controls.Add(placeWeight)
        fields.Controls.Add(testSpin)
        fields.Controls.Add(weightLabel)
        canvas.WeightPoint = New PointF(source.SnippitInfo.PivotWeightX, source.SnippitInfo.PivotWeightY)
        AddHandler weightBox.CheckedChanged, Sub() UpdatePhysicsOnlyState()
        AddHandler placeWeight.Click, Sub()
                                          StopSpinTest()
                                          canvas.PlacingWeight = Not canvas.PlacingWeight
                                          placeWeight.Text = If(canvas.PlacingWeight, "Click the image to place the weight", "Place Counterweight")
                                      End Sub
        AddHandler canvas.WeightPlaced, Sub() placeWeight.Text = "Place Counterweight"
        AddHandler testSpin.Click, AddressOf ToggleSpinTest
        AddHandler spinTimer.Tick, AddressOf SpinTestTick
        automaticBox.ForeColor = Color.White : automaticBox.AutoSize = False : automaticBox.Width = 310 : automaticBox.Height = 40
        fields.Controls.Add(automaticBox)
        fields.Controls.Add(FieldRow("Rest/down angle", downAngle))
        fields.Controls.Add(FieldRow("Other/up limit", upAngle))
        fields.Controls.Add(FieldRow("Swing time (ms)", duration))
        fields.Controls.Add(FieldRow("Random strength (%)", randomStrength))
        fields.Controls.Add(New Label With {.Text = "Ball impact variation: 0% = fixed; 20% = 80–120% of normal strength.",
                                             .ForeColor = Color.White, .Width = 310, .Height = 36})
        fields.Controls.Add(SectionHeader("PIVOT POINTS AND PREVIEW"))
        Dim zoomIn As New Button With {.Text = "Zoom In (+)", .Width = 310, .Height = 31}
        Dim zoomOut As New Button With {.Text = "Zoom Out (-)", .Width = 310, .Height = 31}
        AddHandler zoomIn.Click, Sub() canvas.ZoomAt(1.25F, New Point(canvas.ClientSize.Width \ 2, canvas.ClientSize.Height \ 2))
        AddHandler zoomOut.Click, Sub() canvas.ZoomAt(0.8F, New Point(canvas.ClientSize.Width \ 2, canvas.ClientSize.Height \ 2))
        fields.Controls.Add(zoomIn) : fields.Controls.Add(zoomOut)
        Dim previewDown As New Button With {.Text = "Preview Down Position", .Width = 310, .Height = 31}
        Dim previewUp As New Button With {.Text = "Preview Up Position", .Width = 310, .Height = 31}
        AddHandler previewDown.Click, Sub() StartPositionPreview(CSng(downAngle.Value), False)
        AddHandler previewUp.Click, Sub()
                                            If Not automaticBox.Checked Then upAngle.Value = CDec(DirectedUpAngle())
                                            StartPositionPreview(CSng(upAngle.Value), True)
                                        End Sub
        fields.Controls.Add(previewDown) : fields.Controls.Add(previewUp) : fields.Controls.Add(automaticPreview)
        automaticPreview.Visible = automaticBox.Checked
        AddHandler automaticBox.CheckedChanged, Sub()
                                                    If Not automaticBox.Checked Then StopAutomaticPreview()
                                                    automaticPreview.Visible = automaticBox.Checked
                                                End Sub
        AddHandler automaticPreview.Click, AddressOf ToggleAutomaticPreview
        AddHandler previewTimer.Tick, AddressOf AutomaticPreviewTick
        AddHandler canvas.MouseDown, AddressOf CanvasPointEditStarted
        AddHandler downAngle.ValueChanged, AddressOf PreviewSettingsChanged
        AddHandler upAngle.ValueChanged, AddressOf PreviewSettingsChanged
        AddHandler duration.ValueChanged, AddressOf PreviewSettingsChanged
        fields.Controls.Add(SectionHeader("TRIGGER"))
        fields.Controls.Add(FieldRow("Trigger type", triggerType))
        fields.Controls.Add(FieldRow("Trigger ID", triggerID))
        reverseBox.ForeColor = Color.White : reverseBox.AutoSize = False : reverseBox.Width = 310 : reverseBox.Height = 30
        reverseBox.Checked = source.SnippitInfo.PivotTriggerReverse
        fields.Controls.Add(reverseBox)
        fields.Controls.Add(FieldRow("Down command", downTrigger))
        fields.Controls.Add(FieldRow("Up command", upTrigger))
        fields.Controls.Add(SectionHeader("STRIKE A PHYSICS PIVOT"))
        strikeBox.ForeColor = Color.White : strikeBox.AutoSize = False : strikeBox.Width = 310 : strikeBox.Height = 26
        fields.Controls.Add(strikeBox)
        strikeTarget.DropDownStyle = ComboBoxStyle.DropDownList
        For Each candidate As Illumination.BulbInfo In strikeCandidates
            strikeTarget.Items.Add(candidate.Name)
        Next
        Dim savedTarget As Integer = strikeTarget.FindStringExact(source.SnippitInfo.PivotStrikeTarget)
        strikeTarget.SelectedIndex = If(savedTarget >= 0, savedTarget, If(strikeTarget.Items.Count > 0, 0, -1))
        strikeBox.Checked = savedTarget >= 0
        SetupNumber(strikeStrength, 1D, 300D, CDec(source.SnippitInfo.PivotStrikeStrength), 0)
        fields.Controls.Add(FieldRow("Target pivot", strikeTarget))
        fields.Controls.Add(FieldRow("Strike strength (%)", strikeStrength))
        strikeContactBox.ForeColor = Color.White : strikeContactBox.AutoSize = False : strikeContactBox.Width = 310 : strikeContactBox.Height = 26
        strikeContactBox.Checked = source.SnippitInfo.PivotStrikeContact
        fields.Controls.Add(strikeContactBox)
        strikeDirection.DropDownStyle = ComboBoxStyle.DropDownList
        strikeDirection.Items.AddRange(New Object() {"Clockwise", "Counter-clockwise"})
        strikeDirection.SelectedIndex = If(source.SnippitInfo.PivotStrikeDirection < 0, 1, 0)
        fields.Controls.Add(FieldRow("Spin direction", strikeDirection))
        fields.Controls.Add(New Label With {.Text = "Contact on: this pivot's shape must hit the target to spin it. Contact off: each kick up spins the target (no touching needed); a faster kick spins it harder. Turn on Physics impact only for the target; its friction sets how fast it slows.",
                                             .ForeColor = Color.White, .Width = 310, .Height = 98})
        AddHandler strikeContactBox.CheckedChanged, Sub() UpdateStrikeState()
        fields.Controls.Add(testStrike)
        AddHandler strikeBox.CheckedChanged, Sub() UpdateStrikeState()
        AddHandler strikeTarget.SelectedIndexChanged, Sub() UpdateStrikeState()
        AddHandler testStrike.Click, AddressOf StartStrikeTest
        AddHandler strikeTimer.Tick, AddressOf StrikeTestTick
        Dim save As New Button With {.Text = "Save Pivot Animation", .DialogResult = DialogResult.OK, .Width = 310, .Height = 32}
        Dim cancel As New Button With {.Text = "Cancel", .DialogResult = DialogResult.Cancel, .Width = 310, .Height = 32}
        Dim actions As New TableLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 44, .ColumnCount = 2,
                                                  .Padding = New Padding(10, 5, 10, 0)}
        actions.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 55.0F))
        actions.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 45.0F))
        save.Dock = DockStyle.Fill : cancel.Dock = DockStyle.Fill
        save.Margin = New Padding(0, 0, 4, 0) : cancel.Margin = New Padding(4, 0, 0, 0)
        actions.Controls.Add(save, 0, 0) : actions.Controls.Add(cancel, 1, 0)
        sidebar.Controls.Add(fields) : sidebar.Controls.Add(actions)
        AddHandler triggerType.SelectedIndexChanged, AddressOf TriggerTypeChanged
        AddHandler physicsOnlyBox.CheckedChanged, AddressOf PhysicsOnlyChanged
        AddHandler save.Click, AddressOf SaveValues
        Controls.Add(canvas) : Controls.Add(sidebar) : canvas.Dock = DockStyle.Fill
        AcceptButton = save : CancelButton = cancel
        TriggerTypeChanged(Nothing, EventArgs.Empty)
        UpdatePhysicsOnlyState()
    End Sub

    Private Function SelectedStrikeTarget() As Illumination.BulbInfo
        If strikeTarget.SelectedIndex < 0 OrElse strikeTarget.SelectedIndex >= strikeCandidates.Count Then Return Nothing
        Return strikeCandidates(strikeTarget.SelectedIndex)
    End Function

    Private Sub UpdateStrikeState()
        Dim available As Boolean = Not physicsOnlyBox.Checked AndAlso strikeCandidates.Count > 0
        strikeBox.Enabled = available
        Dim active As Boolean = available AndAlso strikeBox.Checked
        strikeTarget.Enabled = active
        strikeStrength.Enabled = active
        strikeContactBox.Enabled = active
        strikeDirection.Enabled = active AndAlso Not strikeContactBox.Checked
        testStrike.Enabled = active AndAlso SelectedStrikeTarget() IsNot Nothing
        If Not active Then StopStrikeTest(False)
        Dim target As Illumination.BulbInfo = If(active, SelectedStrikeTarget(), Nothing)
        If target Is Nothing Then
            canvas.StrikeImage = Nothing
        Else
            canvas.StrikeImage = target.Image
            canvas.StrikeBounds = New RectangleF(target.Location.X, target.Location.Y,
                                                 Math.Max(1, target.Size.Width), Math.Max(1, target.Size.Height))
            canvas.StrikePivot = New PointF(target.SnippitInfo.PivotX, target.SnippitInfo.PivotY)
        End If
        If Not strikeRunning Then canvas.StrikeAngle = 0.0F
    End Sub

    ' Builds the same collision outline the Server builds from the authored size.
    Private Shared Function BuildStrikeShape(ByVal source As Image, ByVal authoredSize As Size,
                                             ByVal pivot As PointF) As PhysicsPreview.B2SData.PivotStrikeShape
        If source Is Nothing OrElse authoredSize.Width <= 0 OrElse authoredSize.Height <= 0 Then Return Nothing
        Dim maskWidth As Integer = Math.Max(1, Math.Min(512, authoredSize.Width))
        Dim maskHeight As Integer = Math.Max(1, Math.Min(512, authoredSize.Height))
        Dim alpha(maskWidth * maskHeight - 1) As Byte
        Dim hasSolid As Boolean = False
        Using bitmap As New Bitmap(maskWidth, maskHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb)
            Using graphics As Graphics = Graphics.FromImage(bitmap)
                graphics.CompositingMode = CompositingMode.SourceCopy
                graphics.CompositingQuality = CompositingQuality.HighQuality
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality
                graphics.Clear(Color.Transparent)
                graphics.DrawImage(source, New Rectangle(0, 0, maskWidth, maskHeight))
            End Using
            Dim data As System.Drawing.Imaging.BitmapData = bitmap.LockBits(New Rectangle(0, 0, maskWidth, maskHeight),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb)
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
        If Not hasSolid Then Return Nothing
        Dim contour As PointF() = PhysicsPreview.B2SData.BuildPhysicsContour(alpha, maskWidth, maskHeight, pivot.X, pivot.Y)
        If contour Is Nothing Then Return Nothing
        Dim shape As New PhysicsPreview.B2SData.PivotStrikeShape(contour, maskWidth, maskHeight, pivot.X, pivot.Y)
        Return If(shape.IsUsable, shape, Nothing)
    End Function

    Private Sub StartStrikeTest(ByVal sender As Object, ByVal e As EventArgs)
        Dim target As Illumination.BulbInfo = SelectedStrikeTarget()
        If target Is Nothing Then Return
        StopStrikeTest(False)
        StopAutomaticPreview()
        strikeDownValue = CSng(downAngle.Value)
        strikeUpValue = If(automaticBox.Checked, CSng(upAngle.Value), DirectedUpAngle())
        If Math.Abs(strikeUpValue - strikeDownValue) < 0.1F Then
            MessageBox.Show(Me, "Choose two different angles so this pivot can swing into the target.", "Test Strike", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        strikeDriverShape = BuildStrikeShape(snippet.Image, snippet.Size, canvas.Pivot)
        strikeTargetShape = BuildStrikeShape(target.Image, target.Size, New PointF(target.SnippitInfo.PivotX, target.SnippitInfo.PivotY))
        If strikeContactBox.Checked AndAlso (strikeDriverShape Is Nothing OrElse strikeTargetShape Is Nothing) Then
            MessageBox.Show(Me, "One of the two images has no solid pixels to collide with.", "Test Strike", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        strikeTestTarget = target
        strikeKick.Reset()
        strikeTargetAngle = 0.0F
        strikeTargetVelocity = 0.0F
        strikeDriverAngle = strikeDownValue
        strikeTimeMs = 0.0R
        strikeLastClockMs = 0.0R
        strikeRunning = True
        testStrike.Text = "Stop Test"
        RemoveHandler testStrike.Click, AddressOf StartStrikeTest
        AddHandler testStrike.Click, AddressOf StopStrikeTestClicked
        SetPreviewAngle(strikeDriverAngle)
        canvas.StrikeAngle = 0.0F
        strikeClock.Restart()
        strikeTimer.Start()
    End Sub

    Private Sub StopStrikeTestClicked(ByVal sender As Object, ByVal e As EventArgs)
        StopStrikeTest(True)
    End Sub

    Private Sub StopStrikeTest(ByVal keepPose As Boolean)
        If Not strikeRunning Then Return
        strikeTimer.Stop()
        strikeClock.Reset()
        strikeRunning = False
        testStrike.Text = "Test Strike"
        RemoveHandler testStrike.Click, AddressOf StopStrikeTestClicked
        AddHandler testStrike.Click, AddressOf StartStrikeTest
        If Not keepPose Then
            canvas.StrikeAngle = 0.0F
            SetPreviewAngle(0.0F)
        End If
    End Sub

    ' Swing up then back down at the saved swing time, stepping 1 ms at a time
    ' like the Server: move both pivots, then resolve the strike.
    Private Function StrikeDriverAngleAt(ByVal timeMs As Double) As Single
        Dim swing As Double = CDbl(duration.Value)
        Dim legStart As Single, legEnd As Single, legTime As Double
        If timeMs < swing Then
            legStart = strikeDownValue : legEnd = strikeUpValue : legTime = timeMs
        Else
            legStart = strikeUpValue : legEnd = strikeDownValue : legTime = timeMs - swing
        End If
        Dim progress As Single = CSng(Math.Max(0.0R, Math.Min(1.0R, legTime / swing)))
        Dim eased As Single = progress * progress * (3.0F - 2.0F * progress)
        Return legStart + (legEnd - legStart) * eased
    End Function

    Private Sub StrikeTestTick(ByVal sender As Object, ByVal e As EventArgs)
        If Not strikeRunning OrElse strikeTestTarget Is Nothing Then Return
        Dim nowMs As Double = strikeClock.Elapsed.TotalMilliseconds
        Dim steps As Integer = CInt(Math.Min(100.0R, Math.Floor(nowMs - strikeLastClockMs)))
        If steps <= 0 Then Return
        strikeLastClockMs += steps
        Dim driverBounds As New RectangleF(snippet.Location.X, snippet.Location.Y, Math.Max(1, snippet.Size.Width), Math.Max(1, snippet.Size.Height))
        Dim targetBounds As New RectangleF(strikeTestTarget.Location.X, strikeTestTarget.Location.Y,
                                           Math.Max(1, strikeTestTarget.Size.Width), Math.Max(1, strikeTestTarget.Size.Height))
        Dim friction As Single = Math.Max(0.0F, strikeTestTarget.SnippitInfo.PivotPhysicsFriction)
        Const stepSeconds As Single = 0.001F
        For index As Integer = 1 To steps
            strikeTimeMs += 1.0R
            Dim previousDriverAngle As Single = strikeDriverAngle
            strikeDriverAngle = StrikeDriverAngleAt(strikeTimeMs)
            With strikeTestTarget.SnippitInfo
                AdvanceTestPivot(strikeTargetAngle, strikeTargetVelocity, stepSeconds, .PivotWeightEnabled,
                                 (.PivotWeightX - .PivotX) * targetBounds.Width, (.PivotWeightY - .PivotY) * targetBounds.Height,
                                 .PivotWeightStrength, friction)
            End With
            Dim delta As Single
            If strikeContactBox.Checked Then
                delta = PhysicsPreview.B2SData.PivotStrikeAngularDelta(
                    strikeDriverShape, driverBounds, strikeDriverAngle, (strikeDriverAngle - previousDriverAngle) / stepSeconds,
                    strikeTargetShape, targetBounds, strikeTargetAngle, strikeTargetVelocity, CSng(strikeStrength.Value))
            Else
                Dim kickSpeed As Single = strikeKick.Advance(strikeDriverAngle - previousDriverAngle, stepSeconds,
                                                             Math.Sign(strikeUpValue - strikeDownValue))
                delta = PhysicsPreview.B2SData.PivotKickAngularDelta(kickSpeed, If(strikeDirection.SelectedIndex = 1, -1, 1),
                                                                     CSng(strikeStrength.Value))
            End If
            If delta <> 0.0F Then strikeTargetVelocity = Math.Max(-2160.0F, Math.Min(2160.0F, strikeTargetVelocity + delta))
        Next
        SetPreviewAngle(strikeDriverAngle)
        canvas.StrikeAngle = strikeTargetAngle
        Dim swingDone As Boolean = strikeTimeMs >= 2.0R * CDbl(duration.Value)
        Dim targetResting As Boolean = strikeTargetVelocity = 0.0F
        If strikeTestTarget.SnippitInfo.PivotWeightEnabled Then
            With strikeTestTarget.SnippitInfo
                targetResting = targetResting AndAlso Math.Abs(PhysicsPreview.B2SData.PivotWeightAngularAcceleration(
                    strikeTargetAngle, (.PivotWeightX - .PivotX) * targetBounds.Width, (.PivotWeightY - .PivotY) * targetBounds.Height,
                    .PivotWeightStrength)) < 0.5F
            End With
        End If
        If swingDone AndAlso (targetResting OrElse strikeTimeMs > 2.0R * CDbl(duration.Value) + 15000.0R) Then
            StopStrikeTest(True)
        End If
    End Sub

    Private Sub PhysicsOnlyChanged(ByVal sender As Object, ByVal e As EventArgs)
        UpdatePhysicsOnlyState()
    End Sub

    Private Sub UpdatePhysicsOnlyState()
        Dim physicsOnly As Boolean = physicsOnlyBox.Checked
        If physicsOnly Then
            enabledBox.Checked = True
            automaticBox.Checked = False
            StopAutomaticPreview()
        End If
        canvas.PhysicsOnlyMode = physicsOnly
        automaticBox.Enabled = Not physicsOnly
        downAngle.Enabled = Not physicsOnly
        upAngle.Enabled = Not physicsOnly
        duration.Enabled = Not physicsOnly
        randomStrength.Enabled = Not physicsOnly
        triggerType.Enabled = Not physicsOnly
        triggerID.Enabled = Not physicsOnly AndAlso triggerType.SelectedIndex > 0 AndAlso triggerType.SelectedIndex <> 4
        downTrigger.Enabled = Not physicsOnly AndAlso triggerType.SelectedIndex = 0
        upTrigger.Enabled = downTrigger.Enabled
        reverseBox.Enabled = Not physicsOnly AndAlso ReverseTriggerAllowed()
        automaticPreview.Visible = Not physicsOnly AndAlso automaticBox.Checked
        weightBox.Enabled = physicsOnly
        weightStrength.Enabled = physicsOnly AndAlso weightBox.Checked
        placeWeight.Enabled = physicsOnly AndAlso weightBox.Checked
        testSpin.Enabled = physicsOnly
        canvas.ShowWeight = physicsOnly AndAlso weightBox.Checked
        If Not canvas.ShowWeight Then
            canvas.PlacingWeight = False
            placeWeight.Text = "Place Counterweight"
        End If
        If Not physicsOnly Then StopSpinTest()
        UpdateStrikeState()
    End Sub

    ' Spins this physics pivot at 900 deg/s and shows it slow down, using the
    ' counterweight and friction the Server uses.
    Private Sub ToggleSpinTest(ByVal sender As Object, ByVal e As EventArgs)
        If spinRunning Then
            StopSpinTest()
            Return
        End If
        StopStrikeTest(False)
        StopAutomaticPreview()
        canvas.PlacingWeight = False
        placeWeight.Text = "Place Counterweight"
        spinAngle = 0.0F
        spinVelocity = 900.0F
        spinTimeMs = 0.0R
        spinLastClockMs = 0.0R
        spinRunning = True
        testSpin.Text = "Stop Test Spin"
        SetPreviewAngle(0.0F)
        spinClock.Restart()
        spinTimer.Start()
    End Sub

    Private Sub StopSpinTest()
        If Not spinRunning Then Return
        spinTimer.Stop()
        spinClock.Reset()
        spinRunning = False
        testSpin.Text = "Test Spin"
    End Sub

    Private Sub SpinTestTick(ByVal sender As Object, ByVal e As EventArgs)
        If Not spinRunning Then Return
        Dim nowMs As Double = spinClock.Elapsed.TotalMilliseconds
        Dim steps As Integer = CInt(Math.Min(100.0R, Math.Floor(nowMs - spinLastClockMs)))
        If steps <= 0 Then Return
        spinLastClockMs += steps
        Dim friction As Single = Math.Max(0.0F, snippet.SnippitInfo.PivotPhysicsFriction)
        Dim moving As Boolean = True
        For index As Integer = 1 To steps
            spinTimeMs += 1.0R
            moving = AdvanceTestPivot(spinAngle, spinVelocity, 0.001R, weightBox.Checked,
                                      (canvas.WeightPoint.X - canvas.Pivot.X) * Math.Max(1, snippet.Size.Width),
                                      (canvas.WeightPoint.Y - canvas.Pivot.Y) * Math.Max(1, snippet.Size.Height),
                                      CSng(weightStrength.Value), friction)
        Next
        SetPreviewAngle(spinAngle)
        If Not moving OrElse spinTimeMs > 30000.0R Then StopSpinTest()
    End Sub

    ' One 1 ms free-spin step, matching the Server's AdvancePhysicsPivot.
    Private Shared Function AdvanceTestPivot(ByRef angle As Single, ByRef velocity As Single, ByVal elapsed As Double,
                                             ByVal useWeight As Boolean, ByVal armX As Single, ByVal armY As Single,
                                             ByVal strength As Single, ByVal friction As Single) As Boolean
        If useWeight Then
            Return PhysicsPreview.B2SData.AdvanceWeightedPivot(angle, velocity, elapsed, armX, armY, strength, friction)
        End If
        If Math.Abs(velocity) < 0.0001F Then Return False
        angle = CSng((angle + velocity * elapsed) Mod 360.0R)
        velocity = CSng(velocity * Math.Exp(-friction * elapsed))
        If Math.Abs(velocity) < 0.01F Then velocity = 0.0F
        Return True
    End Function

    Private Sub CanvasPointEditStarted(ByVal sender As Object, ByVal e As MouseEventArgs)
        If e.Button <> MouseButtons.Left Then Return
        StopSpinTest()
        StopStrikeTest(False)
        StopAutomaticPreview()
        SetPreviewAngle(0.0F)
    End Sub

    Private Sub ToggleAutomaticPreview(ByVal sender As Object, ByVal e As EventArgs)
        ' Only a running automatic swing is stopped here. A one-time Down/Up
        ' position preview falls through so the automatic swing starts.
        If previewRunning AndAlso previewLoops Then
            StopAutomaticPreview()
            Return
        End If
        If Not automaticBox.Checked Then Return
        StopStrikeTest(False)
        If downAngle.Value = upAngle.Value Then
            MessageBox.Show(Me, "Choose two different angles to preview the automatic swing.", "Pivot Animation", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        previewRunning = True
        previewLoops = True
        automaticPreview.Text = "Stop Automatic Preview"
        SetPreviewAngle(CSng(downAngle.Value))
        StartPreviewLeg(CSng(downAngle.Value), CSng(upAngle.Value))
    End Sub

    Private Sub StartPositionPreview(ByVal targetAngle As Single, ByVal isUp As Boolean)
        StopSpinTest()
        If strikeRunning Then
            StopStrikeTest(False)
            previewCurrentAngle = 0.0F
        End If
        previewTimer.Stop()
        previewClock.Reset()
        previewRunning = True
        previewLoops = False
        previewOneShotUp = isUp
        automaticPreview.Text = "Automatic Preview"
        StartPreviewLeg(previewCurrentAngle, targetAngle)
    End Sub

    Private Sub StartPreviewLeg(ByVal startAngle As Single, ByVal targetAngle As Single)
        previewStartAngle = startAngle
        previewTargetAngle = targetAngle
        previewClock.Restart()
        previewTimer.Start()
    End Sub

    Private Sub AutomaticPreviewTick(ByVal sender As Object, ByVal e As EventArgs)
        Dim progress As Single = CSng(Math.Min(1.0R, previewClock.Elapsed.TotalMilliseconds / CDbl(duration.Value)))
        Dim eased As Single = progress * progress * (3.0F - 2.0F * progress)
        SetPreviewAngle(previewStartAngle + (previewTargetAngle - previewStartAngle) * eased)
        If progress >= 1.0F Then
            SetPreviewAngle(previewTargetAngle)
            If previewLoops Then
                StartPreviewLeg(previewTargetAngle, If(Math.Abs(previewTargetAngle - CSng(upAngle.Value)) < 0.01F,
                                                        CSng(downAngle.Value), CSng(upAngle.Value)))
            Else
                previewTimer.Stop()
                previewClock.Reset()
                previewRunning = False
            End If
        End If
    End Sub

    Private Sub PreviewSettingsChanged(ByVal sender As Object, ByVal e As EventArgs)
        If Not previewRunning Then Return
        If Not previewLoops Then
            ' One-time Down/Up preview: continue from the current angle to the
            ' requested position using the changed values.
            previewTimer.Stop()
            StartPreviewLeg(previewCurrentAngle, If(previewOneShotUp, CSng(upAngle.Value), CSng(downAngle.Value)))
            Return
        End If
        previewTimer.Stop()
        SetPreviewAngle(CSng(downAngle.Value))
        If downAngle.Value = upAngle.Value Then
            StopAutomaticPreview()
        Else
            StartPreviewLeg(CSng(downAngle.Value), CSng(upAngle.Value))
        End If
    End Sub

    Private Sub StopAutomaticPreview()
        previewTimer.Stop()
        previewClock.Reset()
        previewRunning = False
        previewLoops = False
        automaticPreview.Text = "Automatic Preview"
        SetPreviewAngle(0.0F)
    End Sub

    Private Sub SetPreviewAngle(ByVal angle As Single)
        previewCurrentAngle = angle
        canvas.PreviewAngle = angle
    End Sub

    Protected Overrides Sub OnFormClosed(ByVal e As FormClosedEventArgs)
        StopSpinTest()
        spinTimer.Dispose()
        StopStrikeTest(True)
        strikeTimer.Dispose()
        StopAutomaticPreview()
        previewTimer.Dispose()
        MyBase.OnFormClosed(e)
    End Sub

    Private Shared Sub SetupNumber(ByVal box As NumericUpDown, ByVal min As Decimal, ByVal max As Decimal, ByVal value As Decimal, ByVal decimals As Integer)
        box.Minimum = min : box.Maximum = max : box.DecimalPlaces = decimals : box.Value = Math.Max(min, Math.Min(max, value)) : box.Dock = DockStyle.Fill
    End Sub
    Private Shared Function FieldRow(ByVal caption As String, ByVal control As Control) As TableLayoutPanel
        Dim row As New TableLayoutPanel With {.Width = 310, .Height = 34, .ColumnCount = 2, .Margin = New Padding(3)}
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 48.0F))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 52.0F))
        row.Controls.Add(New Label With {.Text = caption, .ForeColor = Color.White, .Dock = DockStyle.Fill,
                                         .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
        control.Dock = DockStyle.Fill
        row.Controls.Add(control, 1, 0)
        Return row
    End Function

    Private Shared Function SectionHeader(ByVal caption As String) As Label
        Return New Label With {.Text = caption, .ForeColor = Color.FromArgb(105, 220, 255),
                               .Font = New Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                               .AutoSize = False, .Width = 310, .Height = 26,
                               .TextAlign = ContentAlignment.BottomLeft, .Margin = New Padding(3, 10, 3, 2)}
    End Function
    Private Sub SaveValues(ByVal sender As Object, ByVal e As EventArgs)
        If Not physicsOnlyBox.Checked AndAlso enabledBox.Checked AndAlso triggerType.SelectedIndex = 0 AndAlso (String.IsNullOrWhiteSpace(upTrigger.Text) OrElse String.IsNullOrWhiteSpace(downTrigger.Text)) Then
            MessageBox.Show(Me, "Enter both the Up and Down command names.", "Pivot Animation", MessageBoxButtons.OK, MessageBoxIcon.Information) : DialogResult = DialogResult.None : Return
        End If
        If Not physicsOnlyBox.Checked AndAlso enabledBox.Checked AndAlso triggerType.SelectedIndex > 0 AndAlso triggerType.SelectedIndex <> 4 AndAlso triggerID.Value <= 0 Then
            MessageBox.Show(Me, "Choose the trigger ID.", "Pivot Animation", MessageBoxButtons.OK, MessageBoxIcon.Information) : DialogResult = DialogResult.None : Return
        End If
        If Not physicsOnlyBox.Checked AndAlso enabledBox.Checked AndAlso triggerType.SelectedIndex = 4 AndAlso Not automaticBox.Checked Then
            MessageBox.Show(Me, "Turn on Automatic Pivot Point for a swing that starts with the backglass.", "Pivot Animation", MessageBoxButtons.OK, MessageBoxIcon.Information) : DialogResult = DialogResult.None : Return
        End If
        If Not physicsOnlyBox.Checked AndAlso enabledBox.Checked AndAlso automaticBox.Checked AndAlso triggerType.SelectedIndex = 0 Then
            MessageBox.Show(Me, "Automatic Pivot Point needs a ROM solenoid, ROM lamp, or B2S ID with an on/off state.", "Pivot Animation", MessageBoxButtons.OK, MessageBoxIcon.Information) : DialogResult = DialogResult.None : Return
        End If
        If Not physicsOnlyBox.Checked AndAlso enabledBox.Checked AndAlso automaticBox.Checked AndAlso Math.Abs(CSng(upAngle.Value - downAngle.Value)) < 0.1F Then
            MessageBox.Show(Me, "Choose two different angles for the automatic swing.", "Pivot Animation", MessageBoxButtons.OK, MessageBoxIcon.Information) : DialogResult = DialogResult.None : Return
        End If
        Dim strikeName As String = String.Empty
        If Not physicsOnlyBox.Checked AndAlso enabledBox.Checked AndAlso strikeBox.Checked Then
            Dim target As Illumination.BulbInfo = SelectedStrikeTarget()
            If target Is Nothing Then
                MessageBox.Show(Me, "Choose the target pivot for the strike.", "Pivot Animation", MessageBoxButtons.OK, MessageBoxIcon.Information) : DialogResult = DialogResult.None : Return
            End If
            If Not target.SnippitInfo.PivotPhysicsEnabled AndAlso
               MessageBox.Show(Me, target.Name & " does not have Physics impact only turned on, so it will not spin until you turn it on." &
                               Environment.NewLine & Environment.NewLine & "Save anyway?", "Pivot Animation",
                               MessageBoxButtons.YesNo, MessageBoxIcon.Information) <> DialogResult.Yes Then
                DialogResult = DialogResult.None : Return
            End If
            strikeName = target.Name.Trim()
        End If
        If Not physicsOnlyBox.Checked AndAlso Not automaticBox.Checked Then upAngle.Value = CDec(DirectedUpAngle())
        With snippet.SnippitInfo
            .PivotStrikeTarget = strikeName
            .PivotStrikeStrength = CSng(strikeStrength.Value)
            .PivotStrikeContact = strikeContactBox.Checked
            .PivotStrikeDirection = If(strikeDirection.SelectedIndex = 1, -1, 1)
            .PivotAnimationEnabled = enabledBox.Checked : .PivotX = canvas.Pivot.X : .PivotY = canvas.Pivot.Y
            .PivotTipX = canvas.Tip.X : .PivotTipY = canvas.Tip.Y
            .PivotDownAngle = CSng(downAngle.Value) : .PivotUpAngle = CSng(upAngle.Value) : .PivotDuration = CInt(duration.Value)
            .PivotAutomaticOscillation = automaticBox.Checked AndAlso Not physicsOnlyBox.Checked
            .PivotPhysicsEnabled = physicsOnlyBox.Checked
            .PivotWeightEnabled = physicsOnlyBox.Checked AndAlso weightBox.Checked
            .PivotWeightX = canvas.WeightPoint.X : .PivotWeightY = canvas.WeightPoint.Y
            .PivotWeightStrength = CSng(weightStrength.Value)
            .PivotRandomStrength = CSng(randomStrength.Value)
            .PivotTriggerType = triggerType.SelectedIndex : .PivotTriggerID = CInt(triggerID.Value)
            .PivotDownTrigger = downTrigger.Text.Trim() : .PivotUpTrigger = upTrigger.Text.Trim()
            .PivotTriggerReverse = reverseBox.Checked AndAlso Not physicsOnlyBox.Checked AndAlso ReverseTriggerAllowed()
        End With
    End Sub

    Private Function DirectedUpAngle() As Single
        Dim downValue As Single = CSng(downAngle.Value)
        Dim amount As Single = Math.Abs(CSng(upAngle.Value) - downValue)
        If amount < 0.1F Then amount = Math.Max(1.0F, Math.Abs(CSng(upAngle.Value)))
        Dim dx As Double = canvas.Tip.X - canvas.Pivot.X
        Dim dy As Double = canvas.Tip.Y - canvas.Pivot.Y
        If dx * dx + dy * dy < 0.0001 Then Return CSng(upAngle.Value)
        Dim positive As Single = downValue + amount
        Dim negative As Single = downValue - amount
        Return If(RotatedTipY(dx, dy, positive) < RotatedTipY(dx, dy, negative), positive, negative)
    End Function

    Private Shared Function RotatedTipY(ByVal dx As Double, ByVal dy As Double, ByVal angle As Single) As Double
        Dim radians As Double = angle * Math.PI / 180.0R
        Return dx * Math.Sin(radians) + dy * Math.Cos(radians)
    End Function

    ' Only on/off triggers can be reversed: ROM solenoid, ROM lamp, B2S ID and SW.
    Private Function ReverseTriggerAllowed() As Boolean
        Dim index As Integer = triggerType.SelectedIndex
        Return index = 1 OrElse index = 2 OrElse index = 3 OrElse index = 5
    End Function

    Private Sub TriggerTypeChanged(ByVal sender As Object, ByVal e As EventArgs)
        reverseBox.Enabled = Not physicsOnlyBox.Checked AndAlso ReverseTriggerAllowed()
        If physicsOnlyBox.Checked Then
            triggerID.Enabled = False : downTrigger.Enabled = False : upTrigger.Enabled = False
            Return
        End If
        Dim named As Boolean = (triggerType.SelectedIndex = 0)
        If triggerType.SelectedIndex = 4 Then automaticBox.Checked = True
        triggerID.Enabled = triggerType.SelectedIndex > 0 AndAlso triggerType.SelectedIndex <> 4
        downTrigger.Enabled = named : upTrigger.Enabled = named
    End Sub

    Private Class PivotCanvas
        Inherits Control
        Public Enum PointMode
            Hinge
            Tip
            Weight
        End Enum
        ' Counterweight marker of a physics pivot.
        Private weightValue As New PointF(0.5F, 1.0F)
        Private showWeightValue As Boolean
        Public PlacingWeight As Boolean
        Public Event WeightPlaced As EventHandler
        Public Property WeightPoint As PointF
            Get
                Return weightValue
            End Get
            Set(value As PointF)
                weightValue = value : Invalidate()
            End Set
        End Property
        Public Property ShowWeight As Boolean
            Get
                Return showWeightValue
            End Get
            Set(value As Boolean)
                showWeightValue = value : Invalidate()
            End Set
        End Property
        ' The weight marker turns with the image around the hinge.
        Private Function WeightScreenPoint(ByVal r As RectangleF) As PointF
            Dim px As Single = r.X + pivotValue.X * r.Width, py As Single = r.Y + pivotValue.Y * r.Height
            Dim wx As Single = r.X + weightValue.X * r.Width - px, wy As Single = r.Y + weightValue.Y * r.Height - py
            Dim radians As Double = angleValue * Math.PI / 180.0R
            Return New PointF(CSng(px + wx * Math.Cos(radians) - wy * Math.Sin(radians)),
                              CSng(py + wx * Math.Sin(radians) + wy * Math.Cos(radians)))
        End Function
        Public BackglassImage As Image
        Public SnippetBounds As RectangleF
        Public SourceImage As Image
        ' Strike target shown behind the edited pivot (backglass coordinates).
        Private strikeImageValue As Image
        Public StrikeBounds As RectangleF
        Public StrikePivot As PointF
        Private strikeAngleValue As Single
        Public Property StrikeImage As Image
            Get
                Return strikeImageValue
            End Get
            Set(value As Image)
                strikeImageValue = value : Invalidate()
            End Set
        End Property
        Public Property StrikeAngle As Single
            Get
                Return strikeAngleValue
            End Get
            Set(value As Single)
                strikeAngleValue = value : Invalidate()
            End Set
        End Property
        Private pivotValue As New PointF(0.5F, 0.5F), tipValue As New PointF(0.9F, 0.5F), angleValue As Single
        Private editPointValue As PointMode = PointMode.Hinge
        Private draggingPoint As Nullable(Of PointMode) = Nothing
        Private dragOffset As PointF = PointF.Empty
        Private zoom As Single = 1.0F
        Private pan As PointF = PointF.Empty
        Private panning As Boolean
        Private lastPanPoint As Point
        Private physicsOnlyValue As Boolean
        Public Property PhysicsOnlyMode As Boolean
            Get
                Return physicsOnlyValue
            End Get
            Set(value As Boolean)
                physicsOnlyValue = value
                If value Then
                    editPointValue = PointMode.Hinge
                    draggingPoint = Nothing
                End If
                Invalidate()
            End Set
        End Property
        Public Property EditPoint As PointMode
            Get
                Return editPointValue
            End Get
            Set(value As PointMode)
                editPointValue = value : Invalidate()
            End Set
        End Property
        Public Property Pivot As PointF
            Get
                Return pivotValue
            End Get
            Set(value As PointF)
                pivotValue = value
                Invalidate()
            End Set
        End Property
        Public Property Tip As PointF
            Get
                Return tipValue
            End Get
            Set(value As PointF)
                tipValue = value : Invalidate()
            End Set
        End Property
        Public WriteOnly Property PreviewAngle As Single
            Set(value As Single)
                angleValue = value
                Invalidate()
            End Set
        End Property
        Public Sub New()
            DoubleBuffered = True
            BackColor = Color.FromArgb(28, 28, 32)
            Cursor = Cursors.Cross
            TabStop = True
        End Sub
        Public Sub ZoomAt(ByVal factor As Single, ByVal center As Point)
            If BackglassImage Is Nothing Then Return
            Dim nextZoom As Single = Math.Max(1.0F, Math.Min(10.0F, zoom * factor))
            If Math.Abs(nextZoom - zoom) < 0.001F Then Return
            Dim before As RectangleF = BackglassRect()
            Dim authoredX As Single = (center.X - before.X) * BackglassImage.Width / before.Width
            Dim authoredY As Single = (center.Y - before.Y) * BackglassImage.Height / before.Height
            zoom = nextZoom
            Dim after As RectangleF = BackglassRect()
            pan = New PointF(pan.X + center.X - (after.X + authoredX * after.Width / BackglassImage.Width),
                             pan.Y + center.Y - (after.Y + authoredY * after.Height / BackglassImage.Height))
            Invalidate()
        End Sub
        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            Focus()
        End Sub

        Protected Overrides Sub OnMouseWheel(ByVal e As MouseEventArgs)
            MyBase.OnMouseWheel(e)
            If e.Delta <> 0 Then ZoomAt(CSng(Math.Pow(1.2R, e.Delta / 120.0R)), e.Location)
        End Sub
        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            Focus()
            If e.Button = MouseButtons.Right OrElse e.Button = MouseButtons.Middle OrElse (e.Button = MouseButtons.Left AndAlso EditorZoomCanvas.IsSpaceHeld()) Then
                panning = True
                lastPanPoint = e.Location
                Capture = True
                Cursor = Cursors.SizeAll
                Return
            End If
            If e.Button <> MouseButtons.Left Then Return
            Dim r As RectangleF = ImageRect()
            If r.Width <= 0 OrElse r.Height <= 0 Then Return
            Dim hinge As New PointF(r.X + pivotValue.X * r.Width, r.Y + pivotValue.Y * r.Height)
            Dim reference As New PointF(r.X + tipValue.X * r.Width, r.Y + tipValue.Y * r.Height)
            Dim hingeDistance As Single = (e.X - hinge.X) * (e.X - hinge.X) + (e.Y - hinge.Y) * (e.Y - hinge.Y)
            Dim referenceDistance As Single = (e.X - reference.X) * (e.X - reference.X) + (e.Y - reference.Y) * (e.Y - reference.Y)
            Dim markerRadiusSquared As Single = 14.0F * 14.0F
            If showWeightValue Then
                Dim weightPoint As PointF = WeightScreenPoint(r)
                Dim weightDistance As Single = (e.X - weightPoint.X) * (e.X - weightPoint.X) + (e.Y - weightPoint.Y) * (e.Y - weightPoint.Y)
                If PlacingWeight OrElse (weightDistance <= markerRadiusSquared AndAlso weightDistance < hingeDistance) Then
                    draggingPoint = PointMode.Weight
                    If PlacingWeight Then
                        dragOffset = PointF.Empty
                        MoveSelectedPoint(e.Location)
                    Else
                        dragOffset = New PointF(weightPoint.X - e.X, weightPoint.Y - e.Y)
                    End If
                    Capture = True
                    Return
                End If
            End If
            If hingeDistance <= markerRadiusSquared AndAlso hingeDistance <= referenceDistance Then
                draggingPoint = PointMode.Hinge
                dragOffset = New PointF(hinge.X - e.X, hinge.Y - e.Y)
            ElseIf Not physicsOnlyValue AndAlso referenceDistance <= markerRadiusSquared Then
                draggingPoint = PointMode.Tip
                dragOffset = New PointF(reference.X - e.X, reference.Y - e.Y)
            ElseIf physicsOnlyValue OrElse editPointValue = PointMode.Hinge OrElse r.Contains(e.Location) Then
                draggingPoint = If(physicsOnlyValue, PointMode.Hinge, editPointValue)
                dragOffset = PointF.Empty
                MoveSelectedPoint(e.Location)
            Else
                Return
            End If
            Capture = True
        End Sub
        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            If panning Then
                pan = New PointF(pan.X + e.X - lastPanPoint.X, pan.Y + e.Y - lastPanPoint.Y)
                lastPanPoint = e.Location
                Invalidate()
                Return
            End If
            If draggingPoint.HasValue AndAlso e.Button = MouseButtons.Left Then MoveSelectedPoint(e.Location)
        End Sub
        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            If panning Then
                panning = False
                Capture = False
                Cursor = Cursors.Cross
                Return
            End If
            If e.Button <> MouseButtons.Left OrElse Not draggingPoint.HasValue Then Return
            MoveSelectedPoint(e.Location)
            Dim placedWeight As Boolean = draggingPoint.Value = PointMode.Weight AndAlso PlacingWeight
            draggingPoint = Nothing
            Capture = False
            If placedWeight Then
                PlacingWeight = False
                RaiseEvent WeightPlaced(Me, EventArgs.Empty)
            End If
        End Sub
        Private Sub MoveSelectedPoint(ByVal location As Point)
            If Not draggingPoint.HasValue Then Return
            Dim r As RectangleF = ImageRect()
            If r.Width <= 0 OrElse r.Height <= 0 Then Return
            Dim x As Single = (location.X + dragOffset.X - r.X) / r.Width
            Dim y As Single = (location.Y + dragOffset.Y - r.Y) / r.Height
            If draggingPoint.Value = PointMode.Tip Then
                x = Math.Max(0.0F, Math.Min(1.0F, x))
                y = Math.Max(0.0F, Math.Min(1.0F, y))
            End If
            Dim point As New PointF(x, y)
            If draggingPoint.Value = PointMode.Weight Then
                WeightPoint = point
            ElseIf draggingPoint.Value = PointMode.Hinge Then
                Pivot = point
            Else
                Tip = point
            End If
        End Sub
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e) : If SourceImage Is Nothing Then Return
            Dim r = ImageRect(), px = r.X + pivotValue.X * r.Width, py = r.Y + pivotValue.Y * r.Height
            Dim tx = r.X + tipValue.X * r.Width, ty = r.Y + tipValue.Y * r.Height
            If BackglassImage IsNot Nothing Then e.Graphics.DrawImage(BackglassImage, BackglassRect())
            If strikeImageValue IsNot Nothing AndAlso BackglassImage IsNot Nothing Then
                Dim view As RectangleF = BackglassRect()
                Dim backglassScale As Single = view.Width / BackglassImage.Width
                Dim sr As New RectangleF(view.X + StrikeBounds.X * backglassScale, view.Y + StrikeBounds.Y * backglassScale,
                                         StrikeBounds.Width * backglassScale, StrikeBounds.Height * backglassScale)
                Dim sx As Single = sr.X + StrikePivot.X * sr.Width, sy As Single = sr.Y + StrikePivot.Y * sr.Height
                e.Graphics.SmoothingMode = SmoothingMode.HighQuality
                e.Graphics.TranslateTransform(sx, sy) : e.Graphics.RotateTransform(strikeAngleValue) : e.Graphics.TranslateTransform(-sx, -sy)
                e.Graphics.DrawImage(strikeImageValue, sr) : e.Graphics.ResetTransform()
                Using p As New Pen(Color.Orange, 2) : e.Graphics.DrawEllipse(p, sx - 6, sy - 6, 12, 12) : End Using
            End If
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality : e.Graphics.TranslateTransform(px, py) : e.Graphics.RotateTransform(angleValue) : e.Graphics.TranslateTransform(-px, -py)
            e.Graphics.DrawImage(SourceImage, r) : e.Graphics.ResetTransform()
            If showWeightValue Then
                Dim wp As PointF = WeightScreenPoint(r)
                Using p As New Pen(Color.Magenta, 2), b As New SolidBrush(Color.FromArgb(200, 255, 0, 200))
                    e.Graphics.DrawLine(p, px, py, wp.X, wp.Y)
                    e.Graphics.FillEllipse(b, wp.X - 7, wp.Y - 7, 14, 14)
                    e.Graphics.DrawEllipse(Pens.White, wp.X - 7, wp.Y - 7, 14, 14)
                End Using
            End If
            Using p As New Pen(Color.Cyan, 2) : e.Graphics.DrawEllipse(p, px - 8, py - 8, 16, 16) : e.Graphics.DrawLine(p, px - 12, py, px + 12, py) : e.Graphics.DrawLine(p, px, py - 12, px, py + 12) : End Using
            If Not physicsOnlyValue Then
                Using p As New Pen(Color.Lime, 2) : e.Graphics.DrawLine(p, px, py, tx, ty) : e.Graphics.DrawEllipse(p, tx - 7, ty - 7, 14, 14) : End Using
            End If
            Dim instruction As String = If(physicsOnlyValue,
                                           "Click once to place the physics hinge. Wheel zooms, right-drag pans.",
                                           If(editPointValue = PointMode.Hinge, "Click to place hinge; drag markers. Wheel zooms, right-drag pans.", "Click to place reference; drag markers. Wheel zooms, right-drag pans."))
            e.Graphics.DrawString(instruction, Font, Brushes.White, 10, 10)
        End Sub
        Private Function ImageRect() As RectangleF
            If SourceImage Is Nothing Then Return RectangleF.Empty
            If BackglassImage IsNot Nothing Then
                Dim view As RectangleF = BackglassRect()
                Dim backglassScale As Single = view.Width / BackglassImage.Width
                Return New RectangleF(view.X + SnippetBounds.X * backglassScale, view.Y + SnippetBounds.Y * backglassScale,
                                      SnippetBounds.Width * backglassScale, SnippetBounds.Height * backglassScale)
            End If
            Dim scale = Math.Min((ClientSize.Width - 30.0F) / SourceImage.Width, (ClientSize.Height - 30.0F) / SourceImage.Height)
            Dim w = SourceImage.Width * scale, h = SourceImage.Height * scale
            Return New RectangleF((ClientSize.Width - w) / 2, (ClientSize.Height - h) / 2, w, h)
        End Function
        Private Function BackglassRect() As RectangleF
            If BackglassImage Is Nothing Then Return RectangleF.Empty
            Dim fit As Single = Math.Max(0.01F, Math.Min((ClientSize.Width - 30.0F) / BackglassImage.Width,
                                                        (ClientSize.Height - 30.0F) / BackglassImage.Height))
            Dim width As Single = BackglassImage.Width * fit * zoom
            Dim height As Single = BackglassImage.Height * fit * zoom
            Return New RectangleF((ClientSize.Width - width) / 2.0F + pan.X,
                                  (ClientSize.Height - height) / 2.0F + pan.Y, width, height)
        End Function
    End Class
End Class
