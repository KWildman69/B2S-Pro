Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

' B2S Pro Path Animation editor. Draw a path in any direction, choose what
' moves the snippet, and test it. Movement uses shared\PathAnimation.vb, the
' same code the Server runs, so the preview matches the backglass.
' The menu only shows the settings the chosen drive needs.
Public Class formPathAnimation
    Inherits B2SThemedForm

    ' "What moves it" choices, in menu order.
    Private Shared ReadOnly MoveModes As PathAnimDriveMode() = {
        PathAnimDriveMode.RunStop, PathAnimDriveMode.StepStops, PathAnimDriveMode.GoToStop, PathAnimDriveMode.PositionValue,
        PathAnimDriveMode.PopDrop}
    Private Shared ReadOnly MoveModeNames As String() = {
        "Motor: moves while a trigger is on", "One stop each time a trigger turns on",
        "One ID per stop", "Stop number from a B2S ID value",
        "Pop up / drop: to the end while on, back when off"}

    Private ReadOnly sourceSnippet As Illumination.BulbInfo
    Private ReadOnly working As PathAnimSettings
    Private ReadOnly canvas As New PathCanvas()
    Private ReadOnly timer As New Timer()
    Private ReadOnly clock As New Diagnostics.Stopwatch()
    Private lastTick As Double

    Private ReadOnly closedCheck As New CheckBox()
    Private ReadOnly smoothCheck As New CheckBox()
    Private ReadOnly moveBox As New ComboBox()
    Private ReadOnly moveTrigger As New TriggerRow("Trigger")
    Private ReadOnly numberTypeCaption As Label = Caption("Trigger type")
    Private ReadOnly numberTypeBox As New ComboBox()
    Private ReadOnly numberFromCaption As Label = Caption("ID of the first stop (the rest count up)")
    Private ReadOnly numberFromBox As New NumericUpDown()
    Private ReadOnly valueTrigger As New TriggerRow("B2S ID whose value is the stop number")
    Private ReadOnly stopCountCaption As Label = Caption("How many stops (spaced evenly)")
    Private ReadOnly stopCountBox As New NumericUpDown()
    Private ReadOnly pointsCheck As New CheckBox()
    Private ReadOnly homeTrigger As New TriggerRow("Send it back to the start (optional)")
    Private ReadOnly speedBox As New NumericUpDown()
    Private ReadOnly returnSpeedCaption As Label = Caption("Speed back to the start (0 = jump)")
    Private ReadOnly returnSpeedBox As New NumericUpDown()
    Private ReadOnly bounceCaption As Label = Caption("Bounce where it lands (0 = none, 100 = most)")
    Private ReadOnly bounceBox As New NumericUpDown()
    Private ReadOnly rotateCheck As New CheckBox()
    Private ReadOnly rotateOffsetCaption As Label = Caption("Artwork faces (0 = right, 90 = down, 180 = left)")
    Private ReadOnly rotateOffsetBox As New NumericUpDown()
    Private ReadOnly switchList As New ListBox()
    Private ReadOnly switchIDBox As New NumericUpDown()
    Private ReadOnly testMoveButton As New Button()
    Private ReadOnly statusLabel As New Label()
    Private lastSwitchText As String = String.Empty
    Private lastSwitchTime As Double = -10000
    Private ReadOnly carryBox As New ComboBox()
    Private ReadOnly dropTrigger As New TriggerRow("Let go when this turns on")
    Private ReadOnly returnCheck As New CheckBox()
    Private ReadOnly dropAtEndCheck As New CheckBox()
    Private ReadOnly letGoButton As New Button()
    Private ReadOnly carryChoices As New List(Of Illumination.BulbInfo)()
    Private ReadOnly testMagnet As New PathAnimMagnet()
    Private riderTestRect As RectangleF
    ' After a test drop, a physics ball runs in the Designer's physics
    ' simulator (the same shared physics the Server runs) until picked up again.
    Private riderPhysics As PhysicsPreview.B2SData
    Private riderPhysicsPendingSeconds As Double
    Private loading As Boolean

    Public ReadOnly Property Result As PathAnimSettings
        Get
            Return working
        End Get
    End Property

    Public Sub New(ByVal snippet As Illumination.BulbInfo, ByVal backglassImage As Image)
        sourceSnippet = snippet
        loading = True
        working = snippet.SnippitInfo.PathAnim.Clone()
        Dim center As New PointF(snippet.Location.X + snippet.Size.Width / 2.0F, snippet.Location.Y + snippet.Size.Height / 2.0F)
        If working.Points.Count < 2 Then
            working.Points.Clear()
            working.Points.Add(center)
            working.Points.Add(New PointF(center.X + Math.Max(150.0F, snippet.Size.Width * 2.0F), center.Y))
            working.Stops.Clear()
            working.Stops.AddRange(PathAnimSettings.EvenStops(2, False))
            working.StopTriggers.Clear()
            working.Mode = PathAnimDriveMode.StepStops
        End If
        working.Enabled = True
        working.Normalize()
        While working.StopTriggers.Count < working.Stops.Count
            working.StopTriggers.Add(New PathAnimTrigger())
        End While

        Text = "Path Animation - " & If(String.IsNullOrWhiteSpace(snippet.Name), "Snippet", snippet.Name)
        StartPosition = FormStartPosition.CenterParent
        Width = 1180
        Height = 820
        MinimumSize = New Size(960, 600)
        BackColor = Color.FromArgb(9, 12, 20)

        Dim sidebar As New Panel With {.Dock = DockStyle.Right, .Width = 340, .BackColor = Color.FromArgb(22, 25, 38)}
        Dim toolbar As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(10, 8, 10, 8),
                                                 .FlowDirection = FlowDirection.TopDown, .WrapContents = False,
                                                 .AutoScroll = True, .BackColor = Color.FromArgb(22, 25, 38)}

        ' 1. PATH
        closedCheck.Text = "Closed loop (goes around and around)" : closedCheck.Checked = working.Closed
        smoothCheck.Text = "Smooth curve" : smoothCheck.Checked = working.Smooth
        AddHandler closedCheck.CheckedChanged, Sub()
                                                   If loading Then Return
                                                   ' A closed loop keeps going around; an open path stops at its ends.
                                                   working.Closed = closedCheck.Checked
                                                   canvas.RebuildGeometry()
                                                   RespaceStops()
                                               End Sub
        AddHandler smoothCheck.CheckedChanged, Sub() If Not loading Then working.Smooth = smoothCheck.Checked : PathChanged()
        Dim reverseButton As Button = MakeButton("Reverse direction", AddressOf ReversePath)
        Dim clearButton As Button = MakeButton("Clear path", AddressOf ClearPath)
        reverseButton.Width = 145 : clearButton.Width = 145
        Dim pathHelp As Label = Caption("Click to add points, drag to move, right-click to delete.")
        pathHelp.Height = 36
        Dim pathButtons As New FlowLayoutPanel With {.Height = 32, .Width = 296, .WrapContents = False, .Margin = New Padding(0)}
        reverseButton.Margin = New Padding(0, 0, 6, 0) : clearButton.Margin = New Padding(0)
        pathButtons.Controls.AddRange(New Control() {reverseButton, clearButton})

        ' 2. WHAT MOVES IT
        moveBox.DropDownStyle = ComboBoxStyle.DropDownList
        moveBox.Items.AddRange(MoveModeNames)
        moveBox.SelectedIndex = Math.Max(0, Array.IndexOf(MoveModes, working.Mode))
        AddHandler moveBox.SelectedIndexChanged, Sub()
                                                     If loading Then Return
                                                     working.Mode = MoveModes(moveBox.SelectedIndex)
                                                     ShowMoveSettings()
                                                     ResetTest()
                                                     canvas.Invalidate()
                                                 End Sub
        AddHandler moveTrigger.Changed, Sub()
                                            If loading Then Return
                                            If working.Mode = PathAnimDriveMode.RunStop OrElse working.Mode = PathAnimDriveMode.PopDrop Then
                                                working.RunTrigger = moveTrigger.Value
                                            Else
                                                working.ForwardTrigger = moveTrigger.Value
                                            End If
                                        End Sub
        numberTypeBox.DropDownStyle = ComboBoxStyle.DropDownList
        numberTypeBox.Items.AddRange(New Object() {"Solenoid", "Lamp", "B2S ID", "SW"})
        Dim firstStop As PathAnimTrigger = working.StopTrigger(0)
        numberTypeBox.SelectedIndex = If(firstStop.IsSet, CInt(firstStop.Type) - 1, 2)
        numberFromBox.Minimum = 1 : numberFromBox.Maximum = 999
        numberFromBox.Value = Math.Max(1, Math.Min(999, If(firstStop.IsSet, firstStop.ID, 1)))
        AddHandler numberTypeBox.SelectedIndexChanged, Sub() If Not loading Then NumberStops()
        AddHandler numberFromBox.ValueChanged, Sub() If Not loading Then NumberStops()
        valueTrigger.Value = working.ValueTrigger
        AddHandler valueTrigger.Changed, Sub() If Not loading Then working.ValueTrigger = valueTrigger.Value
        stopCountBox.Minimum = 1 : stopCountBox.Maximum = PathAnimSettings.MaxStops : stopCountBox.Value = Math.Max(1, working.Stops.Count)
        AddHandler stopCountBox.ValueChanged, Sub()
                                                  If loading OrElse working.StopsAtPoints Then Return
                                                  RespaceStops()
                                                  If working.Mode = PathAnimDriveMode.GoToStop Then NumberStops()
                                              End Sub
        pointsCheck.Text = "Use my path points as the stops" : pointsCheck.Checked = working.StopsAtPoints
        stopCountBox.Enabled = Not working.StopsAtPoints
        AddHandler pointsCheck.CheckedChanged, Sub()
                                                   If loading Then Return
                                                   working.StopsAtPoints = pointsCheck.Checked
                                                   stopCountBox.Enabled = Not working.StopsAtPoints
                                                   RespaceStops()
                                                   If working.Mode = PathAnimDriveMode.GoToStop Then NumberStops()
                                               End Sub
        homeTrigger.Value = working.HomeTrigger
        AddHandler homeTrigger.Changed, Sub() If Not loading Then working.HomeTrigger = homeTrigger.Value

        ' 3. MOVEMENT
        speedBox.Minimum = 0 : speedBox.Maximum = 20000 : speedBox.Increment = 50
        speedBox.Value = CDec(Math.Max(0.0F, Math.Min(20000.0F, working.Speed)))
        AddHandler speedBox.ValueChanged, Sub()
                                              If loading Then Return
                                              working.Speed = CSng(speedBox.Value)
                                              ' Speed back follows Speed until it is set on its own.
                                              If working.ReturnSpeed < 0 Then
                                                  loading = True
                                                  returnSpeedBox.Value = speedBox.Value
                                                  loading = False
                                              End If
                                          End Sub
        returnSpeedBox.Minimum = 0 : returnSpeedBox.Maximum = 20000 : returnSpeedBox.Increment = 50
        returnSpeedBox.Value = CDec(Math.Max(0.0F, Math.Min(20000.0F, If(working.ReturnSpeed >= 0, working.ReturnSpeed, working.Speed))))
        AddHandler returnSpeedBox.ValueChanged, Sub() If Not loading Then working.ReturnSpeed = CSng(returnSpeedBox.Value)
        bounceBox.Minimum = 0 : bounceBox.Maximum = 100 : bounceBox.Increment = 5
        bounceBox.Value = Math.Max(0, Math.Min(100, working.Bounce))
        AddHandler bounceBox.ValueChanged, Sub() If Not loading Then working.Bounce = CInt(bounceBox.Value)
        rotateCheck.Text = "Turn to follow the path" : rotateCheck.Checked = working.Rotate
        AddHandler rotateCheck.CheckedChanged, Sub()
                                                   If loading Then Return
                                                   working.Rotate = rotateCheck.Checked
                                                   rotateOffsetBox.Enabled = rotateCheck.Checked
                                                   canvas.Invalidate()
                                               End Sub
        rotateOffsetBox.Minimum = -360 : rotateOffsetBox.Maximum = 360 : rotateOffsetBox.DecimalPlaces = 1
        rotateOffsetBox.Value = CDec(Math.Max(-360.0F, Math.Min(360.0F, working.RotateOffset)))
        rotateOffsetBox.Enabled = working.Rotate
        AddHandler rotateOffsetBox.ValueChanged, Sub()
                                                     If loading Then Return
                                                     working.RotateOffset = CSng(rotateOffsetBox.Value)
                                                     canvas.Invalidate()
                                                 End Sub

        ' 4. SWITCHES ON THE PATH
        switchList.Height = 70 : switchList.IntegralHeight = False
        switchList.BackColor = Color.FromArgb(14, 17, 28) : switchList.ForeColor = Color.White
        AddHandler switchList.SelectedIndexChanged, AddressOf SwitchSelected
        switchIDBox.Minimum = 1 : switchIDBox.Maximum = PathAnimSettings.MaxSwitchID : switchIDBox.Value = 1
        AddHandler switchIDBox.ValueChanged, AddressOf SwitchIDChanged
        Dim addSwitchButton As Button = MakeButton("Add switch", AddressOf AddSwitch)
        Dim switchHelp As Label = Caption("Pulsed when the front of the image passes it. Drag it along the path.")
        switchHelp.Height = 36
        Dim removeSwitchButton As Button = MakeButton("Remove selected switch", AddressOf RemoveSwitch)

        ' 5. MAGNET: pick up another snippet by touch, carry it, let it go.
        carryBox.DropDownStyle = ComboBoxStyle.DropDownList
        carryBox.Items.Add("Nothing")
        If Backglass.currentBulbs IsNot Nothing Then
            For Each other As Illumination.BulbInfo In Backglass.currentBulbs
                If other Is snippet OrElse Not other.IsImageSnippit OrElse other.Image Is Nothing OrElse other.ParentForm <> snippet.ParentForm Then Continue For
                carryChoices.Add(other)
                carryBox.Items.Add(String.Format("{0}  (ID {1})", If(String.IsNullOrWhiteSpace(other.Name), "Snippet", other.Name), other.ID))
            Next
        End If
        carryBox.SelectedIndex = Math.Max(0, carryChoices.FindIndex(Function(b) b.ID = working.CarryID) + 1)
        If carryBox.SelectedIndex = 0 Then working.CarryID = 0
        AddHandler carryBox.SelectedIndexChanged, Sub()
                                                      If loading Then Return
                                                      working.CarryID = If(carryBox.SelectedIndex > 0, carryChoices(carryBox.SelectedIndex - 1).ID, 0)
                                                      ShowMagnetSettings()
                                                      ResetTest()
                                                  End Sub
        dropTrigger.Value = working.DropTrigger
        AddHandler dropTrigger.Changed, Sub() If Not loading Then working.DropTrigger = dropTrigger.Value
        dropAtEndCheck.Text = "Let go automatically at the end of the path" : dropAtEndCheck.Checked = working.DropAtEnd
        AddHandler dropAtEndCheck.CheckedChanged, Sub() If Not loading Then working.DropAtEnd = dropAtEndCheck.Checked
        returnCheck.Text = "Return to start after letting go" : returnCheck.Checked = working.ReturnAfterDrop
        AddHandler returnCheck.CheckedChanged, Sub() If Not loading Then working.ReturnAfterDrop = returnCheck.Checked
        letGoButton.Text = "Test: let go now" : letGoButton.AutoSize = False : letGoButton.Height = 28
        AddHandler letGoButton.Click, Sub() TestLetGo()
        Dim magnetHelp As Label = Caption("Picks it up when this image touches it and carries it until the trigger lets go. A physics ball then falls from there.")
        magnetHelp.Height = 52

        ' TEST: one button that does what the chosen trigger does, and a reset.
        testMoveButton.AutoSize = False : testMoveButton.Width = 145 : testMoveButton.Height = 28
        AddHandler testMoveButton.MouseDown, Sub(s, e)
                                                 If e.Button = MouseButtons.Left AndAlso working.Mode = PathAnimDriveMode.RunStop Then
                                                     TestAction(Sub(m) m.Run())
                                                 End If
                                             End Sub
        AddHandler testMoveButton.MouseUp, Sub(s, e)
                                               If e.Button = MouseButtons.Left AndAlso working.Mode = PathAnimDriveMode.RunStop Then
                                                   TestAction(Sub(m) m.Halt())
                                               End If
                                           End Sub
        AddHandler testMoveButton.Click, Sub()
                                             If working.Mode = PathAnimDriveMode.PopDrop Then
                                                 ' Trigger on (to the end) / off (back to the start).
                                                 TestAction(Sub(m) m.GoToStop(If(m.CurrentStop = 1, m.StopCount, 1)))
                                             ElseIf working.Mode <> PathAnimDriveMode.RunStop Then
                                                 TestAction(Sub(m) m.StepBy(1))
                                             End If
                                         End Sub
        Dim homeButton As Button = MakeActionButton("Back to start", Sub() TestAction(Sub(m) m.Home()))
        homeButton.Width = 145
        Dim testButtons As New FlowLayoutPanel With {.Height = 32, .Width = 296, .WrapContents = False, .Margin = New Padding(0)}
        testMoveButton.Margin = New Padding(0, 0, 6, 0) : homeButton.Margin = New Padding(0)
        testButtons.Controls.AddRange(New Control() {testMoveButton, homeButton})
        statusLabel.ForeColor = Color.FromArgb(180, 230, 180) : statusLabel.AutoSize = False : statusLabel.Height = 36

        toolbar.Controls.AddRange(New Control() {
            Header("1. PATH"), pathHelp,
            closedCheck, smoothCheck, pathButtons,
            Header("2. WHAT MOVES IT"), moveBox, moveTrigger,
            numberTypeCaption, numberTypeBox, numberFromCaption, numberFromBox, valueTrigger,
            stopCountCaption, stopCountBox, pointsCheck, homeTrigger,
            Header("3. MOVEMENT"), Caption("Speed (backglass pixels per second, 0 = jump)"), speedBox, returnSpeedCaption, returnSpeedBox, bounceCaption, bounceBox,
            rotateCheck, rotateOffsetCaption, rotateOffsetBox,
            Header("4. SWITCHES ON THE PATH"), switchHelp,
            switchList, Caption("Switch number"), switchIDBox, addSwitchButton, removeSwitchButton,
            Header("5. MAGNET"), magnetHelp, Caption("Carry"), carryBox, dropTrigger, dropAtEndCheck, returnCheck, letGoButton,
            Header("TEST"), testButtons, statusLabel})
        For Each control As Control In toolbar.Controls
            If control IsNot pathButtons AndAlso control IsNot testButtons Then control.Margin = New Padding(3, 3, 3, 3)
            If TypeOf control Is CheckBox Then control.ForeColor = Color.White
            If Not TypeOf control Is Label OrElse control Is statusLabel Then
                If control IsNot pathButtons AndAlso control IsNot testButtons Then control.Width = 296
            End If
        Next

        Dim actions As New TableLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 80, .ColumnCount = 2, .RowCount = 2}
        actions.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        actions.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        Dim saveButton As Button = MakeButton("Save", AddressOf SaveClicked)
        Dim cancelButton As Button = MakeActionButton("Cancel", Sub()
                                                               DialogResult = DialogResult.Cancel
                                                               Close()
                                                           End Sub)
        Dim removeButton As Button = MakeButton("Remove Path Animation", AddressOf RemoveClicked)
        For Each b As Button In {saveButton, cancelButton, removeButton}
            b.Dock = DockStyle.Fill : b.Margin = New Padding(3, 4, 3, 0)
        Next
        actions.Controls.Add(saveButton, 0, 0) : actions.Controls.Add(cancelButton, 1, 0)
        actions.Controls.Add(removeButton, 0, 1) : actions.SetColumnSpan(removeButton, 2)
        sidebar.Controls.Add(toolbar) : sidebar.Controls.Add(actions)

        Dim help As New Label With {.Dock = DockStyle.Bottom, .Height = 28, .TextAlign = ContentAlignment.MiddleCenter,
                                    .ForeColor = Color.White, .BackColor = Color.FromArgb(22, 25, 38),
                                    .Text = "Path: click to add a point, drag to move, right-click to delete.  Drag a red switch bar along the path.  Wheel = zoom, middle-drag = pan."}

        canvas.Dock = DockStyle.Fill
        canvas.BackglassImage = backglassImage
        canvas.SnippetImage = snippet.Image
        canvas.SnippetSize = New SizeF(Math.Max(1, snippet.Size.Width), Math.Max(1, snippet.Size.Height))
        canvas.Settings = working
        AddHandler canvas.PathEdited, Sub() PathChanged()
        AddHandler canvas.StopsEdited, Sub() StopsChanged()
        AddHandler canvas.SwitchesEdited, Sub() SwitchesChanged()

        timer.Interval = 16
        AddHandler timer.Tick, AddressOf TestTick
        Controls.Add(canvas)
        Controls.Add(help)
        Controls.Add(sidebar)

        loading = False
        StopsChanged()
        SwitchesChanged()
        ShowMoveSettings()
        ShowMagnetSettings()
        ResetTest()
        clock.Start()
        timer.Start()
    End Sub

    ' ---- small UI helpers ----

    Private Function MakeButton(ByVal caption As String, ByVal handler As EventHandler) As Button
        Dim b As New Button With {.Text = caption, .AutoSize = False, .Width = 296, .Height = 28}
        AddHandler b.Click, handler
        Return b
    End Function

    Private Function MakeActionButton(ByVal caption As String, ByVal handler As Action) As Button
        Return MakeButton(caption, New EventHandler(Sub(s, e) handler()))
    End Function

    Private Shared Function Header(ByVal caption As String) As Label
        Return New Label With {.Text = caption, .ForeColor = Color.FromArgb(105, 220, 255), .Font = New Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                               .AutoSize = False, .Width = 296, .Height = 24, .TextAlign = ContentAlignment.BottomLeft}
    End Function

    Private Shared Function Caption(ByVal text As String) As Label
        Return New Label With {.Text = text, .ForeColor = Color.White, .AutoSize = False, .Width = 296, .Height = 20, .TextAlign = ContentAlignment.BottomLeft}
    End Function

    ' Shows only what the chosen "What moves it" needs.
    Private Sub ShowMoveSettings()
        Dim mode As PathAnimDriveMode = working.Mode
        Dim motor As Boolean = (mode = PathAnimDriveMode.RunStop)
        Dim stepping As Boolean = (mode = PathAnimDriveMode.StepStops)
        Dim perStop As Boolean = (mode = PathAnimDriveMode.GoToStop)
        Dim byValue As Boolean = (mode = PathAnimDriveMode.PositionValue)
        Dim popDrop As Boolean = (mode = PathAnimDriveMode.PopDrop)
        loading = True
        If popDrop Then
            ' Two places only: the path start (off) and the path end (on).
            working.Closed = False
            closedCheck.Checked = False
            working.StopsAtPoints = False
            pointsCheck.Checked = False
            working.Stops.Clear()
            working.Stops.AddRange(PathAnimSettings.EvenStops(2, False))
            working.StopTriggers.Clear()
            working.StopTriggers.Add(New PathAnimTrigger())
            working.StopTriggers.Add(New PathAnimTrigger())
            canvas.RebuildGeometry()
        End If
        closedCheck.Enabled = Not popDrop
        bounceCaption.Visible = popDrop : bounceBox.Visible = popDrop
        returnSpeedCaption.Visible = popDrop : returnSpeedBox.Visible = popDrop
        If popDrop AndAlso working.ReturnSpeed < 0 Then returnSpeedBox.Value = CDec(Math.Max(0.0F, Math.Min(20000.0F, working.Speed)))
        moveTrigger.Visible = motor OrElse stepping OrElse popDrop
        If popDrop Then
            moveTrigger.CaptionText = "On = go to the end, off = back to the start"
            moveTrigger.Value = working.RunTrigger
        ElseIf motor Then
            moveTrigger.CaptionText = "Moves while this is on (like a motor)"
            moveTrigger.Value = working.RunTrigger
        ElseIf stepping Then
            moveTrigger.CaptionText = "Each time this turns on, go to the next stop"
            moveTrigger.Value = working.ForwardTrigger
        End If
        numberTypeCaption.Visible = perStop : numberTypeBox.Visible = perStop
        numberFromCaption.Visible = perStop : numberFromBox.Visible = perStop
        valueTrigger.Visible = byValue
        ' A motor does not use stops; every other choice does.
        stopCountCaption.Visible = Not motor AndAlso Not popDrop : stopCountBox.Visible = Not motor AndAlso Not popDrop
        pointsCheck.Visible = Not motor AndAlso Not popDrop
        testMoveButton.Text = If(motor, "Hold to run", If(popDrop, "Trigger on / off", "Next stop  >"))
        loading = False
    End Sub

    ' ---- path and stops ----

    Private Sub PathChanged()
        canvas.RebuildGeometry()
        If working.StopsAtPoints Then
            StopsAtPoints()
        Else
            ResetTest()
        End If
    End Sub

    Private Sub StopsChanged()
        working.Normalize()
        While working.StopTriggers.Count < working.Stops.Count
            working.StopTriggers.Add(New PathAnimTrigger())
        End While
        loading = True
        stopCountBox.Value = Math.Max(1, Math.Min(PathAnimSettings.MaxStops, working.Stops.Count))
        loading = False
        ResetTest()
    End Sub

    ' Stops follow the simple settings: evenly spaced, or one on each drawn point.
    Private Sub RespaceStops()
        If working.StopsAtPoints Then
            StopsAtPoints()
        Else
            SpaceStops()
        End If
    End Sub

    Private Sub SpaceStops()
        Dim count As Integer = CInt(stopCountBox.Value)
        Dim oldTriggers As New List(Of PathAnimTrigger)(working.StopTriggers)
        working.Stops.Clear()
        working.Stops.AddRange(PathAnimSettings.EvenStops(count, working.Closed))
        working.StopTriggers.Clear()
        For index As Integer = 0 To working.Stops.Count - 1
            working.StopTriggers.Add(If(index < oldTriggers.Count, oldTriggers(index), New PathAnimTrigger()))
        Next
        StopsChanged()
        canvas.Invalidate()
    End Sub

    ' One stop exactly on each drawn point, in drawing order. Useful when the
    ' points were clicked where the object has to stop (e.g. legacy positions).
    Private Sub StopsAtPoints()
        If working.Points.Count < 2 Then Return
        canvas.RebuildGeometry()
        Dim geometry As PathAnimGeometry = canvas.Geometry
        If geometry.Length <= 0 Then Return
        Dim oldTriggers As New List(Of PathAnimTrigger)(working.StopTriggers)
        working.Stops.Clear()
        working.StopTriggers.Clear()
        Dim previous As Double = -1
        For index As Integer = 0 To working.Points.Count - 1
            Dim gap As Double
            Dim d As Double = geometry.NearestDistance(working.Points(index), gap)
            If index = 0 Then d = 0
            ' A point crossed twice keeps its place in drawing order.
            If d < previous Then d = previous
            previous = d
            working.Stops.Add(CSng(d / geometry.Length))
            working.StopTriggers.Add(If(index < oldTriggers.Count, oldTriggers(index), New PathAnimTrigger()))
        Next
        StopsChanged()
        canvas.Invalidate()
    End Sub

    ' "One ID per stop": stop 1 gets the first ID, the rest count up.
    Private Sub NumberStops()
        Dim type As PathAnimTriggerType = CType(numberTypeBox.SelectedIndex + 1, PathAnimTriggerType)
        Dim first As Integer = CInt(numberFromBox.Value)
        For index As Integer = 0 To working.Stops.Count - 1
            working.StopTriggers(index) = New PathAnimTrigger(type, Math.Min(999, first + index))
        Next
    End Sub

    Private Sub ReversePath(ByVal sender As Object, ByVal e As EventArgs)
        working.Points.Reverse()
        Dim pairs As New List(Of KeyValuePair(Of Single, PathAnimTrigger))()
        For index As Integer = 0 To working.Stops.Count - 1
            Dim f As Single = 1.0F - working.Stops(index)
            If working.Closed AndAlso f >= 1.0F Then f = 0.0F
            pairs.Add(New KeyValuePair(Of Single, PathAnimTrigger)(f, working.StopTrigger(index)))
        Next
        working.Stops.Clear() : working.StopTriggers.Clear()
        For Each pair In pairs
            working.Stops.Add(pair.Key) : working.StopTriggers.Add(pair.Value)
        Next
        ' Switches stay on the same spot of the track.
        For index As Integer = 0 To working.Switches.Count - 1
            Dim sw As PathAnimSwitch = working.Switches(index)
            Dim f As Single = 1.0F - sw.Position
            If working.Closed AndAlso f >= 1.0F Then f = 0.0F
            working.Switches(index) = New PathAnimSwitch(sw.SwitchID, f)
        Next
        canvas.RebuildGeometry()
        StopsChanged()
        SwitchesChanged()
    End Sub

    ' ---- switches on the track ----

    Private Shared Function SwitchText(ByVal sw As PathAnimSwitch) As String
        Return String.Format("Switch {0}  at {1:0.#}% of the path", sw.SwitchID, sw.Position * 100.0F)
    End Function

    Private Sub SwitchesChanged()
        working.Normalize()
        loading = True
        ' A switch picked on the canvas becomes the selected row.
        Dim selected As Integer = If(canvas.SelectedSwitch >= 0, canvas.SelectedSwitch, switchList.SelectedIndex)
        switchList.BeginUpdate()
        switchList.Items.Clear()
        For Each sw As PathAnimSwitch In working.Switches
            switchList.Items.Add(SwitchText(sw))
        Next
        switchList.EndUpdate()
        If switchList.Items.Count > 0 Then switchList.SelectedIndex = Math.Max(0, Math.Min(switchList.Items.Count - 1, selected))
        loading = False
        SwitchSelected(Nothing, EventArgs.Empty)
    End Sub

    Private Sub SwitchSelected(ByVal sender As Object, ByVal e As EventArgs)
        Dim index As Integer = switchList.SelectedIndex
        canvas.SelectedSwitch = index
        If index >= 0 AndAlso index < working.Switches.Count Then
            loading = True
            switchIDBox.Value = Math.Max(1, Math.Min(PathAnimSettings.MaxSwitchID, working.Switches(index).SwitchID))
            loading = False
        End If
        canvas.Invalidate()
    End Sub

    Private Sub SwitchIDChanged(ByVal sender As Object, ByVal e As EventArgs)
        If loading Then Return
        Dim index As Integer = switchList.SelectedIndex
        If index < 0 OrElse index >= working.Switches.Count Then Return
        working.Switches(index) = New PathAnimSwitch(CInt(switchIDBox.Value), working.Switches(index).Position)
        loading = True
        switchList.Items(index) = SwitchText(working.Switches(index))
        loading = False
        canvas.Invalidate()
    End Sub

    Private Sub AddSwitch(ByVal sender As Object, ByVal e As EventArgs)
        If canvas.Geometry Is Nothing Then canvas.RebuildGeometry()
        If working.Points.Count < 2 OrElse canvas.Geometry.Length <= 0 Then
            MessageBox.Show(Me, "Draw the path first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If working.Switches.Count >= PathAnimSettings.MaxSwitches Then Return
        Dim f As Single = 0.0F
        ' A new switch goes at the front of the image, where it would trip.
        If canvas.Mover IsNot Nothing Then f = CSng(canvas.Geometry.NormalizeDistance(canvas.Mover.FrontDistance) / canvas.Geometry.Length)
        working.Switches.Add(New PathAnimSwitch(CInt(switchIDBox.Value), f))
        canvas.SelectedSwitch = working.Switches.Count - 1
        SwitchesChanged()
    End Sub

    Private Sub RemoveSwitch(ByVal sender As Object, ByVal e As EventArgs)
        Dim index As Integer = switchList.SelectedIndex
        If index < 0 OrElse index >= working.Switches.Count Then Return
        working.Switches.RemoveAt(index)
        canvas.SelectedSwitch = -1
        SwitchesChanged()
    End Sub

    Private Sub ClearPath(ByVal sender As Object, ByVal e As EventArgs)
        working.Points.Clear()
        canvas.RebuildGeometry()
        ResetTest()
    End Sub

    ' ---- test panel ----

    Private Sub ResetTest()
        If canvas.Geometry Is Nothing Then canvas.RebuildGeometry()
        If working.Points.Count < 2 OrElse working.Stops.Count = 0 Then
            canvas.Mover = Nothing
        Else
            canvas.Mover = New PathAnimMover(working, canvas.Geometry)
            canvas.Mover.SnippetSize = canvas.SnippetSize
        End If
        ' The carried snippet starts where it is placed on the backglass.
        StopRiderPhysics()
        testMagnet.Reset()
        Dim rider As Illumination.BulbInfo = If(working.CarryID > 0, carryChoices.Find(Function(b) b.ID = working.CarryID), Nothing)
        If rider IsNot Nothing Then
            canvas.RiderImage = rider.Image
            riderTestRect = New RectangleF(rider.Location.X, rider.Location.Y, Math.Max(1, rider.Size.Width), Math.Max(1, rider.Size.Height))
        Else
            canvas.RiderImage = Nothing
            riderTestRect = RectangleF.Empty
        End If
        canvas.RiderRect = riderTestRect
        UpdateTestMagnet()
        UpdateStatus()
        canvas.Invalidate()
    End Sub

    ' ---- magnet ----

    ' The dropped snippet's own physics, set up the way the Server sets it up
    ' (same saved settings; no physics without a play area, like the Server).
    ' Pivots/flippers are not simulated in this preview.
    Private Sub StartRiderPhysics()
        StopRiderPhysics()
        Dim rider As Illumination.BulbInfo = If(working.CarryID > 0, carryChoices.Find(Function(b) b.ID = working.CarryID), Nothing)
        If rider Is Nothing OrElse Not rider.SnippitInfo.PhysicsBall OrElse riderTestRect.IsEmpty Then Return
        Dim playBounds As RectangleF = ParsePlayBounds(rider.SnippitInfo.PhysicsBounds)
        If playBounds.IsEmpty Then Return
        Dim info = rider.SnippitInfo
        Dim zones As New List(Of PhysicsPreview.B2SData.PhysicsSwitchZone)()
        For index As Integer = 0 To info.PhysicsSwitchZones.Count - 1
            If index >= info.PhysicsSwitchIDs.Count Then Exit For
            zones.Add(New PhysicsPreview.B2SData.PhysicsSwitchZone With {
                .Bounds = info.PhysicsSwitchZones(index), .SwitchID = info.PhysicsSwitchIDs(index),
                .Angle = If(index < info.PhysicsSwitchAngles.Count, info.PhysicsSwitchAngles(index), 0.0F)})
        Next
        Dim launcher As PhysicsPreview.B2SData.PhysicsLauncher = Nothing
        If info.PhysicsLauncherEnabled Then
            launcher = New PhysicsPreview.B2SData.PhysicsLauncher With {.Origin = New PointF(info.PhysicsLauncherX, info.PhysicsLauncherY),
                .Angle = info.PhysicsLauncherAngle, .Strength = info.PhysicsLauncherStrength, .RandomAngle = info.PhysicsLauncherRandomAngle,
                .RandomStrength = info.PhysicsLauncherRandomStrength, .CaptureRadius = info.PhysicsLauncherCaptureRadius,
                .FollowPivot = info.PhysicsLauncherFollowPivot}
        End If
        riderPhysics = New PhysicsPreview.B2SData()
        riderPhysics.Configure(Rectangle.Round(riderTestRect), playBounds, info.PhysicsGravity, info.PhysicsFlipperStrength,
                               info.PhysicsBoundaryBounce, info.PhysicsBoundaryPaths, info.PhysicsObstacles, zones, launcher,
                               info.PhysicsBoundarySegmentBounces, True, info.PhysicsObstacleBounces)
        riderPhysics.ReleaseAtCurrentPosition()
        riderPhysicsPendingSeconds = 0.0R
    End Sub

    Private Sub StopRiderPhysics()
        If riderPhysics IsNot Nothing Then riderPhysics.Dispose()
        riderPhysics = Nothing
        riderPhysicsPendingSeconds = 0.0R
    End Sub

    ' Same 1 ms steps as the Server and the physics editor.
    Private Sub AdvanceRiderPhysics(ByVal elapsedMs As Double)
        If riderPhysics Is Nothing OrElse elapsedMs <= 0 Then Return
        riderPhysicsPendingSeconds += elapsedMs / 1000.0R
        Dim steps As Integer = CInt(Math.Floor((riderPhysicsPendingSeconds + 0.000000001R) / 0.001R))
        For index As Integer = 1 To steps
            riderPhysics.Advance(0.001R)
        Next
        riderPhysicsPendingSeconds = Math.Max(0.0R, riderPhysicsPendingSeconds - steps * 0.001R)
        riderTestRect = riderPhysics.Body.RectangleF
        canvas.RiderRect = riderTestRect
        ' A ball rolling back into the carrier is picked up again.
        UpdateTestMagnet()
        canvas.Invalidate()
    End Sub

    Private Shared Function ParsePlayBounds(ByVal value As String) As RectangleF
        If String.IsNullOrWhiteSpace(value) Then Return RectangleF.Empty
        Dim parts() As String = value.Split(","c)
        If parts.Length <> 4 Then Return RectangleF.Empty
        Dim inv As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture
        Dim l, t, r, b As Single
        If Not Single.TryParse(parts(0), Globalization.NumberStyles.Float, inv, l) OrElse
           Not Single.TryParse(parts(1), Globalization.NumberStyles.Float, inv, t) OrElse
           Not Single.TryParse(parts(2), Globalization.NumberStyles.Float, inv, r) OrElse
           Not Single.TryParse(parts(3), Globalization.NumberStyles.Float, inv, b) OrElse
           r <= l OrElse b <= t Then Return RectangleF.Empty
        Return RectangleF.FromLTRB(l, t, r, b)
    End Function

    Private Sub ShowMagnetSettings()
        Dim used As Boolean = working.CarryID > 0
        dropTrigger.Visible = used : dropAtEndCheck.Visible = used : returnCheck.Visible = used : letGoButton.Visible = used
    End Sub

    ' Same pick-up and carry rule as the Server (shared PathAnimMagnet).
    Private Sub UpdateTestMagnet()
        If canvas.Mover Is Nothing OrElse canvas.RiderImage Is Nothing OrElse riderTestRect.IsEmpty Then Return
        Dim m As PathAnimMover = canvas.Mover
        If Not testMagnet.Carrying Then
            If Not testMagnet.Update(m.Position, m.SnippetSize, m.Angle(), riderTestRect) Then Return
            ' Picked up: a falling ball stops simulating while it is carried.
            StopRiderPhysics()
            lastSwitchText = "Picked up"
            lastSwitchTime = clock.Elapsed.TotalMilliseconds
        End If
        Dim c As PointF = testMagnet.RiderCenter(m.Position, m.Angle())
        riderTestRect = New RectangleF(c.X - riderTestRect.Width / 2.0F, c.Y - riderTestRect.Height / 2.0F, riderTestRect.Width, riderTestRect.Height)
        canvas.RiderRect = riderTestRect
    End Sub

    Private Sub TestLetGo()
        If canvas.Mover Is Nothing OrElse Not testMagnet.Carrying Then Return
        testMagnet.Drop()
        StartRiderPhysics()
        lastSwitchText = If(riderPhysics IsNot Nothing, "Let go (physics running)", "Let go")
        lastSwitchTime = clock.Elapsed.TotalMilliseconds
        If working.ReturnAfterDrop Then
            ' Magnet off on the way back to start.
            testMagnet.StartReturn()
            TestAction(Sub(m) m.Home())
        Else
            UpdateStatus()
            canvas.Invalidate()
        End If
    End Sub

    Private Sub TestAction(ByVal action As Action(Of PathAnimMover))
        If canvas.Mover Is Nothing Then ResetTest()
        If canvas.Mover Is Nothing Then Return
        action(canvas.Mover)
        ' A jump (speed 0) moves at once; report switches it crossed.
        For Each switchID As Integer In canvas.Mover.TakeSwitchHits()
            lastSwitchText = "Switch " & switchID.ToString() & " pressed"
            lastSwitchTime = clock.Elapsed.TotalMilliseconds
        Next
        UpdateTestMagnet()
        CheckTestEndDrop()
        UpdateStatus()
        canvas.Invalidate()
    End Sub

    ' Same auto-drop rule as the Server: let go on arriving at the path end.
    Private Sub CheckTestEndDrop()
        If canvas.Mover Is Nothing Then Return
        If canvas.Mover.TakeEndReached() AndAlso working.DropAtEnd AndAlso testMagnet.Carrying Then TestLetGo()
    End Sub

    Private Sub TestTick(ByVal sender As Object, ByVal e As EventArgs)
        Dim now As Double = clock.Elapsed.TotalMilliseconds
        Dim elapsed As Double = Math.Min(100.0, now - lastTick)
        lastTick = now
        AdvanceRiderPhysics(elapsed)
        ' Back at the start after a drop: the magnet is on again.
        If testMagnet.Returning AndAlso canvas.Mover IsNot Nothing AndAlso Not canvas.Mover.IsMoving Then testMagnet.ReturnFinished()
        If canvas.Mover IsNot Nothing AndAlso canvas.Mover.Advance(elapsed) Then
            For Each switchID As Integer In canvas.Mover.TakeSwitchHits()
                lastSwitchText = "Switch " & switchID.ToString() & " pressed"
                lastSwitchTime = now
            Next
            UpdateTestMagnet()
            CheckTestEndDrop()
            UpdateStatus()
            canvas.Invalidate()
        ElseIf lastSwitchText.Length > 0 AndAlso now - lastSwitchTime > 1500 Then
            UpdateStatus()
        End If
    End Sub

    Private Sub UpdateStatus()
        If canvas.Mover Is Nothing Then
            statusLabel.Text = "Draw at least two points."
            Return
        End If
        Dim switchNote As String = String.Empty
        If lastSwitchText.Length > 0 AndAlso clock.Elapsed.TotalMilliseconds - lastSwitchTime <= 1500 Then
            switchNote = "   " & lastSwitchText
        Else
            lastSwitchText = String.Empty
        End If
        If working.Mode = PathAnimDriveMode.RunStop Then
            statusLabel.Text = If(canvas.Mover.IsMoving, "Running", "Stopped") & switchNote
        ElseIf working.Mode = PathAnimDriveMode.PopDrop Then
            statusLabel.Text = If(canvas.Mover.CurrentStop = 1, "Trigger off: at the start", "Trigger on: at the end") &
                               If(canvas.Mover.IsMoving, "  (moving)", "") & switchNote
        Else
            statusLabel.Text = String.Format("Heading to stop {0} of {1}{2}{3}", canvas.Mover.CurrentStop, canvas.Mover.StopCount,
                                             If(canvas.Mover.IsMoving, "  (moving)", ""), switchNote)
        End If
    End Sub

    ' ---- save ----

    Private Sub SaveClicked(ByVal sender As Object, ByVal e As EventArgs)
        If working.Points.Count < 2 Then
            MessageBox.Show(Me, "Draw at least two path points first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        working.Enabled = True
        working.ClearUnusedForMode()
        working.Normalize()
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Sub RemoveClicked(ByVal sender As Object, ByVal e As EventArgs)
        If MessageBox.Show(Me, "Remove the Path Animation from this snippet?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
        working.Enabled = False
        DialogResult = DialogResult.Abort
        Close()
    End Sub

    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        If disposing Then
            timer.Stop()
            timer.Dispose()
            StopRiderPhysics()
        End If
        MyBase.Dispose(disposing)
    End Sub
    ' ---- trigger picker: type + ID ----

    Private Class TriggerRow
        Inherits Panel

        Private ReadOnly typeBox As New ComboBox()
        Private ReadOnly idBox As New NumericUpDown()
        Private ReadOnly captionLabel As New Label()
        Private quiet As Boolean
        Public Event Changed As EventHandler

        Public WriteOnly Property CaptionText As String
            Set(ByVal value As String)
                captionLabel.Text = value
            End Set
        End Property

        Public Sub New(ByVal caption As String)
            Height = 48
            captionLabel.Text = caption : captionLabel.ForeColor = Color.White : captionLabel.AutoSize = False
            captionLabel.Left = 0 : captionLabel.Top = 0 : captionLabel.Width = 296 : captionLabel.Height = 18
            typeBox.DropDownStyle = ComboBoxStyle.DropDownList
            typeBox.Items.AddRange(New Object() {"None", "Solenoid", "Lamp", "B2S ID", "SW"})
            typeBox.SelectedIndex = 0
            typeBox.Left = 0 : typeBox.Top = 20 : typeBox.Width = 150
            idBox.Minimum = 1 : idBox.Maximum = 999 : idBox.Value = 1
            idBox.Left = 158 : idBox.Top = 20 : idBox.Width = 80
            idBox.Enabled = False
            AddHandler typeBox.SelectedIndexChanged, Sub()
                                                         idBox.Enabled = typeBox.SelectedIndex > 0
                                                         If Not quiet Then RaiseEvent Changed(Me, EventArgs.Empty)
                                                     End Sub
            AddHandler idBox.ValueChanged, Sub() If Not quiet Then RaiseEvent Changed(Me, EventArgs.Empty)
            Controls.AddRange(New Control() {captionLabel, typeBox, idBox})
        End Sub

        Public Property Value As PathAnimTrigger
            Get
                If typeBox.SelectedIndex <= 0 Then Return New PathAnimTrigger()
                Return New PathAnimTrigger(CType(typeBox.SelectedIndex, PathAnimTriggerType), CInt(idBox.Value))
            End Get
            Set(ByVal v As PathAnimTrigger)
                quiet = True
                typeBox.SelectedIndex = If(v.IsSet, CInt(v.Type), 0)
                idBox.Value = Math.Max(1, Math.Min(999, If(v.IsSet, v.ID, 1)))
                idBox.Enabled = v.IsSet
                quiet = False
            End Set
        End Property
    End Class

    ' ---- canvas ----

    Private Class PathCanvas
        Inherits EditorZoomCanvas

        Public BackglassImage As Image
        Public SnippetImage As Image
        Public SnippetSize As SizeF
        Public Settings As PathAnimSettings
        Public Geometry As PathAnimGeometry
        Public Mover As PathAnimMover
        Public PlacingStops As Boolean
        Public SelectedStop As Integer = -1
        Public SelectedSwitch As Integer = -1
        ' Magnet preview: the snippet that gets carried, in backglass-image pixels.
        Public RiderImage As Image
        Public RiderRect As RectangleF
        Public Event PathEdited As EventHandler
        Public Event StopsEdited As EventHandler
        Public Event SwitchesEdited As EventHandler

        Private draggingPoint As Integer = -1
        Private draggingStop As Integer = -1
        Private draggingSwitch As Integer = -1

        Public Sub New()
            DoubleBuffered = True
            SetStyle(ControlStyles.ResizeRedraw Or ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer, True)
            BackColor = Color.Black
        End Sub

        Public Sub RebuildGeometry()
            Geometry = New PathAnimGeometry(Settings.Points, Settings.Closed, Settings.Smooth)
            Invalidate()
        End Sub

        Protected Overrides Sub OnHandleCreated(ByVal e As EventArgs)
            MyBase.OnHandleCreated(e)
            If Geometry Is Nothing AndAlso Settings IsNot Nothing Then RebuildGeometry()
        End Sub

        Protected Overrides Function BaseImageView() As RectangleF
            If BackglassImage Is Nothing OrElse Width <= 0 OrElse Height <= 0 Then Return RectangleF.Empty
            Dim scale As Single = Math.Min(Width / CSng(BackglassImage.Width), Height / CSng(BackglassImage.Height))
            Dim size As New SizeF(BackglassImage.Width * scale, BackglassImage.Height * scale)
            Return New RectangleF((Width - size.Width) / 2.0F, (Height - size.Height) / 2.0F, size.Width, size.Height)
        End Function

        Private Function ViewScale() As Single
            Dim view As RectangleF = ZoomedImageView()
            If BackglassImage Is Nothing OrElse view.Width <= 0 Then Return 1.0F
            Return view.Width / BackglassImage.Width
        End Function

        Private Function ClientToImage(ByVal point As Point) As PointF
            Dim view As RectangleF = ZoomedImageView()
            Dim scale As Single = ViewScale()
            Return New PointF((point.X - view.X) / scale, (point.Y - view.Y) / scale)
        End Function

        Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
            MyBase.OnPaint(e)
            If BackglassImage Is Nothing OrElse Settings Is Nothing Then Return
            If Geometry Is Nothing Then RebuildGeometry()
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
            Dim view As RectangleF = ZoomedImageView()
            e.Graphics.DrawImage(BackglassImage, view)
            Dim scale As Single = ViewScale()
            Dim state As GraphicsState = e.Graphics.Save()
            e.Graphics.TranslateTransform(view.X, view.Y)
            e.Graphics.ScaleTransform(scale, scale)

            ' Magnet preview: the snippet that gets carried (keeps its own orientation).
            If RiderImage IsNot Nothing AndAlso Not RiderRect.IsEmpty Then
                e.Graphics.DrawImage(RiderImage, RiderRect)
            End If

            ' The moving snippet.
            If SnippetImage IsNot Nothing AndAlso Mover IsNot Nothing Then
                Dim center As PointF = Mover.Position
                Dim imageState As GraphicsState = e.Graphics.Save()
                e.Graphics.TranslateTransform(center.X, center.Y)
                e.Graphics.RotateTransform(Mover.Angle())
                e.Graphics.DrawImage(SnippetImage, New RectangleF(-SnippetSize.Width / 2.0F, -SnippetSize.Height / 2.0F, SnippetSize.Width, SnippetSize.Height))
                e.Graphics.Restore(imageState)
            End If

            ' Path line with a direction arrow.
            Dim samples As IList(Of PointF) = Geometry.SamplePoints
            If samples.Count > 1 Then
                Using pathPen As New Pen(Color.FromArgb(235, 0, 220, 255), 3.0F / scale)
                    pathPen.DashStyle = DashStyle.Dash
                    Dim arr(samples.Count - 1) As PointF
                    samples.CopyTo(arr, 0)
                    e.Graphics.DrawLines(pathPen, arr)
                End Using
                If Geometry.Length > 0 Then
                    Dim a As PointF = Geometry.PositionAt(Geometry.Length * 0.48)
                    Dim b As PointF = Geometry.PositionAt(Geometry.Length * 0.52)
                    Using arrowPen As New Pen(Color.Yellow, 3.0F / scale)
                        arrowPen.CustomEndCap = New AdjustableArrowCap(4.0F, 5.0F)
                        e.Graphics.DrawLine(arrowPen, a, b)
                    End Using
                End If
            End If

            ' Path points (when drawing).
            Using labelFont As New Font(Font.FontFamily, Math.Max(6.0F, 9.0F / scale), FontStyle.Bold)
                Dim radius As Single = 7.0F / scale
                For index As Integer = 0 To Settings.Points.Count - 1
                    Dim p As PointF = Settings.Points(index)
                    Dim fill As Brush = If(index = 0, Brushes.LimeGreen, If(PlacingStops, Brushes.Gray, Brushes.DeepSkyBlue))
                    e.Graphics.FillEllipse(fill, p.X - radius, p.Y - radius, radius * 2, radius * 2)
                    Using outline As New Pen(Color.White, 1.5F / scale)
                        e.Graphics.DrawEllipse(outline, p.X - radius, p.Y - radius, radius * 2, radius * 2)
                    End Using
                Next

                ' Stops as numbered diamonds (a motor does not use stops).
                Dim diamondSize As Single = 9.0F / scale
                Dim stopsShown As Integer = If(Settings.Mode = PathAnimDriveMode.RunStop OrElse Settings.Mode = PathAnimDriveMode.PopDrop, 0, Settings.Stops.Count)
                For index As Integer = 0 To stopsShown - 1
                    Dim p As PointF = Geometry.PositionAt(Settings.Stops(index) * Geometry.Length)
                    Dim diamond As PointF() = {New PointF(p.X, p.Y - diamondSize), New PointF(p.X + diamondSize, p.Y), New PointF(p.X, p.Y + diamondSize), New PointF(p.X - diamondSize, p.Y)}
                    Dim stopColor As Color = If(index = SelectedStop, Color.OrangeRed, Color.Orange)
                    Using fill As New SolidBrush(stopColor)
                        e.Graphics.FillPolygon(fill, diamond)
                    End Using
                    Using outline As New Pen(Color.Black, 1.0F / scale)
                        e.Graphics.DrawPolygon(outline, diamond)
                    End Using
                    e.Graphics.DrawString((index + 1).ToString(), labelFont, Brushes.White, p.X + diamondSize, p.Y - diamondSize * 2)
                Next

                ' Track switches as red bars across the path.
                If Geometry.Length > 0 Then
                    Dim halfBar As Single = 16.0F / scale
                    For index As Integer = 0 To Settings.Switches.Count - 1
                        Dim sw As PathAnimSwitch = Settings.Switches(index)
                        Dim d As Double = sw.Position * Geometry.Length
                        Dim p As PointF = Geometry.PositionAt(d)
                        Dim radians As Double = Geometry.AngleAt(d) * Math.PI / 180.0
                        Dim nx As Single = CSng(-Math.Sin(radians)) * halfBar
                        Dim ny As Single = CSng(Math.Cos(radians)) * halfBar
                        Dim barColor As Color = If(index = SelectedSwitch, Color.Yellow, Color.Red)
                        Using barPen As New Pen(barColor, 5.0F / scale)
                            e.Graphics.DrawLine(barPen, p.X - nx, p.Y - ny, p.X + nx, p.Y + ny)
                        End Using
                        Using barBrush As New SolidBrush(barColor)
                            e.Graphics.DrawString("SW " & sw.SwitchID.ToString(), labelFont, barBrush, p.X + nx, p.Y + ny)
                        End Using
                    Next
                End If
            End Using
            e.Graphics.Restore(state)
        End Sub

        Private Function HitSwitch(ByVal p As PointF) As Integer
            If Geometry Is Nothing OrElse Geometry.Length <= 0 Then Return -1
            Dim radius As Single = 14.0F / ViewScale()
            For index As Integer = Settings.Switches.Count - 1 To 0 Step -1
                Dim s As PointF = Geometry.PositionAt(Settings.Switches(index).Position * Geometry.Length)
                Dim dx As Single = s.X - p.X
                Dim dy As Single = s.Y - p.Y
                If dx * dx + dy * dy <= radius * radius Then Return index
            Next
            Return -1
        End Function

        Private Function HitPoint(ByVal p As PointF) As Integer
            Dim radius As Single = 12.0F / ViewScale()
            For index As Integer = Settings.Points.Count - 1 To 0 Step -1
                Dim dx As Single = Settings.Points(index).X - p.X
                Dim dy As Single = Settings.Points(index).Y - p.Y
                If dx * dx + dy * dy <= radius * radius Then Return index
            Next
            Return -1
        End Function

        Private Function HitStop(ByVal p As PointF) As Integer
            Dim radius As Single = 12.0F / ViewScale()
            For index As Integer = Settings.Stops.Count - 1 To 0 Step -1
                Dim s As PointF = Geometry.PositionAt(Settings.Stops(index) * Geometry.Length)
                Dim dx As Single = s.X - p.X
                Dim dy As Single = s.Y - p.Y
                If dx * dx + dy * dy <= radius * radius Then Return index
            Next
            Return -1
        End Function

        Private Function FractionAt(ByVal p As PointF, ByRef gap As Double) As Single
            Dim d As Double = Geometry.NearestDistance(p, gap)
            If Geometry.Length <= 0 Then Return 0.0F
            Return CSng(d / Geometry.Length)
        End Function

        Protected Overrides Sub OnMouseDown(ByVal e As MouseEventArgs)
            If BeginNavigation(e) Then Return
            MyBase.OnMouseDown(e)
            If BackglassImage Is Nothing OrElse Settings Is Nothing Then Return
            Dim p As PointF = ClientToImage(e.Location)
            ' A track switch is dragged along the path in either mode.
            If e.Button = MouseButtons.Left Then
                Dim switchHit As Integer = HitSwitch(p)
                If switchHit >= 0 Then
                    draggingSwitch = switchHit
                    SelectedSwitch = switchHit
                    Capture = True
                    Invalidate()
                    Return
                End If
            End If
            If PlacingStops Then
                Dim hit As Integer = HitStop(p)
                If e.Button = MouseButtons.Right AndAlso hit >= 0 AndAlso Settings.Stops.Count > 1 Then
                    Settings.Stops.RemoveAt(hit)
                    If hit < Settings.StopTriggers.Count Then Settings.StopTriggers.RemoveAt(hit)
                    RaiseEvent StopsEdited(Me, EventArgs.Empty)
                ElseIf e.Button = MouseButtons.Left Then
                    If hit >= 0 Then
                        draggingStop = hit
                        Capture = True
                    ElseIf Settings.Points.Count >= 2 AndAlso Settings.Stops.Count < PathAnimSettings.MaxStops Then
                        Dim gap As Double
                        Dim f As Single = FractionAt(p, gap)
                        Dim limit As Double = 20.0 / ViewScale()
                        If gap <= limit * limit Then
                            Settings.Stops.Add(f)
                            Settings.StopTriggers.Add(New PathAnimTrigger())
                            RaiseEvent StopsEdited(Me, EventArgs.Empty)
                        End If
                    End If
                End If
                Return
            End If
            Dim pointHit As Integer = HitPoint(p)
            If e.Button = MouseButtons.Right AndAlso pointHit >= 0 Then
                Settings.Points.RemoveAt(pointHit)
                RaiseEvent PathEdited(Me, EventArgs.Empty)
            ElseIf e.Button = MouseButtons.Left Then
                If pointHit >= 0 Then
                    draggingPoint = pointHit
                ElseIf ZoomedImageView().Contains(e.Location) Then
                    Settings.Points.Add(ClampToImage(p))
                    draggingPoint = Settings.Points.Count - 1
                    RaiseEvent PathEdited(Me, EventArgs.Empty)
                End If
                Capture = draggingPoint >= 0
            End If
        End Sub

        Private Function ClampToImage(ByVal p As PointF) As PointF
            Return New PointF(Math.Max(0, Math.Min(BackglassImage.Width, p.X)), Math.Max(0, Math.Min(BackglassImage.Height, p.Y)))
        End Function

        Protected Overrides Sub OnMouseMove(ByVal e As MouseEventArgs)
            If MoveNavigation(e) Then Return
            MyBase.OnMouseMove(e)
            If BackglassImage Is Nothing OrElse Settings Is Nothing Then Return
            Dim p As PointF = ClientToImage(e.Location)
            If draggingPoint >= 0 AndAlso draggingPoint < Settings.Points.Count Then
                Settings.Points(draggingPoint) = ClampToImage(p)
                RebuildGeometry()
            ElseIf draggingStop >= 0 AndAlso draggingStop < Settings.Stops.Count Then
                Dim gap As Double
                Settings.Stops(draggingStop) = FractionAt(p, gap)
                Invalidate()
            ElseIf draggingSwitch >= 0 AndAlso draggingSwitch < Settings.Switches.Count Then
                Dim gap As Double
                Settings.Switches(draggingSwitch) = New PathAnimSwitch(Settings.Switches(draggingSwitch).SwitchID, FractionAt(p, gap))
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseUp(ByVal e As MouseEventArgs)
            If EndNavigation() Then Return
            MyBase.OnMouseUp(e)
            If draggingPoint >= 0 Then
                draggingPoint = -1
                RaiseEvent PathEdited(Me, EventArgs.Empty)
            End If
            If draggingStop >= 0 Then
                draggingStop = -1
                RaiseEvent StopsEdited(Me, EventArgs.Empty)
            End If
            If draggingSwitch >= 0 Then
                draggingSwitch = -1
                RaiseEvent SwitchesEdited(Me, EventArgs.Empty)
            End If
            Capture = False
        End Sub
    End Class
End Class
