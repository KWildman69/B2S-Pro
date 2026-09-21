Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

' First isolated motion-path milestone. This is deliberately editor-only:
' it does not write project/directB2S XML and it does not affect the server.
Public Class formMotionPathTest
    Inherits B2SThemedForm

    Private ReadOnly sourceSnippet As Illumination.BulbInfo
    Private ReadOnly editingExitPath As Boolean
    Private ReadOnly previewBackglassImage As Image
    Private ReadOnly canvas As New MotionPathCanvas()
    Private ReadOnly playButton As New Button()
    Private ReadOnly clearButton As New Button()
    Private ReadOnly resetButton As New Button()
    Private ReadOnly speedBox As New NumericUpDown()
    Private ReadOnly saveButton As New Button()
    Private ReadOnly pathCancelButton As New Button()
    Private ReadOnly loopCheckBox As New CheckBox()
    Private ReadOnly rollBallCheckBox As New CheckBox()
    Private ReadOnly triggerTypeBox As New ComboBox()
    Private ReadOnly triggerIDBox As New NumericUpDown()
    Private ReadOnly stopIDBox As New NumericUpDown()
    Private ReadOnly resumeIDBox As New NumericUpDown()
    Private ReadOnly queueTriggersCheckBox As New CheckBox()
    Private ReadOnly sequenceGroupBox As New TextBox()
    Private ReadOnly sequenceOrderBox As New NumericUpDown()
    Private ReadOnly respawnCheckBox As New CheckBox()
    Private ReadOnly respawnDurationBox As New NumericUpDown()
    Private ReadOnly timer As New Timer()

    Public Sub New(ByVal snippet As Illumination.BulbInfo, ByVal backglassImage As Image, Optional ByVal editExitPath As Boolean = False)
        sourceSnippet = snippet
        editingExitPath = editExitPath
        previewBackglassImage = New Bitmap(backglassImage)

        Text = If(editingExitPath, "Exit Motion Path — ", "Entry Motion Path — ") & If(String.IsNullOrWhiteSpace(snippet.Name), "Snippet", snippet.Name)
        StartPosition = FormStartPosition.CenterParent
        Width = 1050
        Height = 760
        MinimumSize = New Size(900, 560)
        BackColor = Color.FromArgb(9, 12, 20)

        Dim sidebar As New Panel With {.Dock = DockStyle.Right, .Width = 330,
                                      .BackColor = Color.FromArgb(22, 25, 38)}
        Dim toolbar As New FlowLayoutPanel With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(10, 8, 10, 8),
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .AutoScroll = True,
            .BackColor = Color.FromArgb(22, 25, 38)
        }
        ConfigureButton(playButton, "Play Preview", AddressOf TogglePreview)
        ConfigureButton(resetButton, "Reset Path", AddressOf ResetPath)
        ConfigureButton(clearButton, "Clear Path", AddressOf ClearPath)
        ConfigureButton(saveButton, "Save Path", AddressOf SavePath)
        ConfigureButton(pathCancelButton, "Cancel", AddressOf CancelPath)
        Dim speedLabel As New Label With {
            .Text = "Travel time (ms):",
            .ForeColor = Color.White,
            .AutoSize = True,
            .Margin = New Padding(18, 8, 4, 0)
        }
        speedBox.Minimum = 250
        speedBox.Maximum = 30000
        speedBox.Increment = 250
        Dim savedDuration As Integer = If(editingExitPath, snippet.SnippitInfo.MotionPathExitDuration, snippet.SnippitInfo.MotionPathDuration)
        speedBox.Value = Math.Max(CInt(speedBox.Minimum), Math.Min(CInt(speedBox.Maximum), savedDuration))
        speedBox.Width = 82
        speedBox.Margin = New Padding(0, 4, 0, 0)
        loopCheckBox.Text = "Loop continuously"
        loopCheckBox.Checked = If(editingExitPath, False, snippet.SnippitInfo.MotionPathLoop)
        loopCheckBox.ForeColor = Color.White
        loopCheckBox.AutoSize = True
        loopCheckBox.Margin = New Padding(14, 7, 3, 0)
        AddHandler loopCheckBox.CheckedChanged, Sub() canvas.LoopPath = loopCheckBox.Checked
        rollBallCheckBox.Text = "Roll ball while moving"
        rollBallCheckBox.Checked = snippet.SnippitInfo.MotionPathRollEnabled
        rollBallCheckBox.ForeColor = Color.White
        rollBallCheckBox.AutoSize = True
        rollBallCheckBox.Margin = New Padding(14, 7, 3, 0)
        AddHandler rollBallCheckBox.CheckedChanged, Sub()
                                                        canvas.RollEnabled = rollBallCheckBox.Checked
                                                        canvas.Invalidate()
                                                    End Sub
        triggerTypeBox.DropDownStyle = ComboBoxStyle.DropDownList
        triggerTypeBox.Items.AddRange(New Object() {"Automatic", "Solenoid", "Lamp", "B2S ID"})
        Dim savedSolenoidID As Integer = If(editingExitPath, snippet.SnippitInfo.MotionPathRemoveSolenoidID, snippet.SnippitInfo.MotionPathSolenoidID)
        Dim savedLampID As Integer = If(editingExitPath, snippet.SnippitInfo.MotionPathRemoveLampID, snippet.SnippitInfo.MotionPathLampID)
        Dim savedB2SID As Integer = If(editingExitPath, snippet.SnippitInfo.MotionPathRemoveB2SID, snippet.SnippitInfo.MotionPathB2SID)
        triggerTypeBox.SelectedIndex = If(savedB2SID > 0, 3, If(savedLampID > 0, 2, If(savedSolenoidID > 0, 1, 0)))
        triggerTypeBox.Width = 88
        triggerTypeBox.Margin = New Padding(12, 4, 3, 0)
        triggerIDBox.Minimum = 1
        triggerIDBox.Maximum = 250
        Dim savedTriggerID As Integer = If(savedB2SID > 0, savedB2SID, If(savedLampID > 0, savedLampID, savedSolenoidID))
        triggerIDBox.Value = Math.Max(1, Math.Min(250, savedTriggerID))
        triggerIDBox.Width = 48
        triggerIDBox.Enabled = (triggerTypeBox.SelectedIndex > 0)
        triggerIDBox.Margin = New Padding(0, 4, 3, 0)
        AddHandler triggerTypeBox.SelectedIndexChanged, Sub() triggerIDBox.Enabled = (triggerTypeBox.SelectedIndex > 0)
        Dim stopLabel As New Label With {.Text = "Stop B2S ID:", .ForeColor = Color.White, .AutoSize = True, .Margin = New Padding(10, 8, 3, 0)}
        stopIDBox.Minimum = 0
        stopIDBox.Maximum = 250
        stopIDBox.Value = Math.Max(0, Math.Min(250, snippet.SnippitInfo.MotionPathStopB2SID))
        stopIDBox.Width = 48
        stopIDBox.Margin = New Padding(0, 4, 3, 0)
        Dim resumeLabel As New Label With {.Text = "Resume ID:", .ForeColor = Color.White, .AutoSize = True, .Margin = New Padding(8, 8, 3, 0)}
        resumeIDBox.Minimum = 0
        resumeIDBox.Maximum = 250
        resumeIDBox.Value = Math.Max(0, Math.Min(250, snippet.SnippitInfo.MotionPathResumeB2SID))
        resumeIDBox.Width = 48
        resumeIDBox.Margin = New Padding(0, 4, 3, 0)
        queueTriggersCheckBox.Text = "Queue repeated triggers"
        queueTriggersCheckBox.Checked = snippet.SnippitInfo.MotionPathQueueTriggers
        queueTriggersCheckBox.ForeColor = Color.White
        queueTriggersCheckBox.AutoSize = True
        queueTriggersCheckBox.Margin = New Padding(8, 7, 3, 0)
        Dim groupLabel As New Label With {.Text = "Sequence group:", .ForeColor = Color.White, .AutoSize = True, .Margin = New Padding(8, 8, 3, 0)}
        sequenceGroupBox.Text = snippet.SnippitInfo.MotionPathSequenceGroup
        sequenceGroupBox.Width = 120
        sequenceGroupBox.MaxLength = 64
        sequenceGroupBox.Margin = New Padding(0, 4, 3, 0)
        Dim orderLabel As New Label With {.Text = "Order:", .ForeColor = Color.White, .AutoSize = True, .Margin = New Padding(6, 8, 3, 0)}
        sequenceOrderBox.Minimum = 0
        sequenceOrderBox.Maximum = 999
        sequenceOrderBox.Value = Math.Max(0, Math.Min(999, snippet.SnippitInfo.MotionPathSequenceOrder))
        sequenceOrderBox.Width = 52
        sequenceOrderBox.Margin = New Padding(0, 4, 3, 0)
        respawnCheckBox.Text = "Animate trough feeder respawn"
        respawnCheckBox.Checked = snippet.SnippitInfo.MotionPathRespawnEnabled
        respawnCheckBox.ForeColor = Color.White
        respawnCheckBox.AutoSize = True
        Dim respawnDurationLabel As Label = SidebarLabel("Respawn slide time (ms):")
        respawnDurationBox.Minimum = 50
        respawnDurationBox.Maximum = 5000
        respawnDurationBox.Increment = 25
        respawnDurationBox.Value = Math.Max(CInt(respawnDurationBox.Minimum), Math.Min(CInt(respawnDurationBox.Maximum), snippet.SnippitInfo.MotionPathRespawnDuration))
        respawnDurationBox.Enabled = respawnCheckBox.Checked
        AddHandler respawnCheckBox.CheckedChanged, Sub()
                                                       respawnDurationBox.Enabled = respawnCheckBox.Checked
                                                       canvas.RespawnEnabled = respawnCheckBox.Checked
                                                       canvas.Invalidate()
                                                   End Sub
        If editingExitPath Then
            loopCheckBox.Enabled = False
            stopLabel.Visible = False
            stopIDBox.Visible = False
            resumeLabel.Visible = False
            resumeIDBox.Visible = False
            queueTriggersCheckBox.Visible = False
            groupLabel.Visible = False
            sequenceGroupBox.Visible = False
            orderLabel.Visible = False
            sequenceOrderBox.Visible = False
            respawnCheckBox.Visible = False
            respawnDurationLabel.Visible = False
            respawnDurationBox.Visible = False
        End If
        Dim previewHeader As Label = SidebarHeader("PREVIEW")
        Dim pathHeader As Label = SidebarHeader("PATH SETTINGS")
        Dim triggerHeader As Label = SidebarHeader("TRIGGERS")
        Dim triggerTypeLabel As Label = SidebarLabel("Start trigger type:")
        Dim triggerIDLabel As Label = SidebarLabel("Start trigger ID:")
        Dim sequenceHeader As Label = SidebarHeader("SEQUENCE")
        Dim respawnHeader As Label = SidebarHeader("TROUGH FEEDER RESPAWN")
        toolbar.Controls.AddRange(New Control() {previewHeader, playButton, resetButton, clearButton,
                                                  pathHeader, speedLabel, speedBox, loopCheckBox, rollBallCheckBox,
                                                  triggerHeader, triggerTypeLabel, triggerTypeBox, triggerIDLabel, triggerIDBox,
                                                  stopLabel, stopIDBox, resumeLabel, resumeIDBox, queueTriggersCheckBox,
                                                  sequenceHeader, groupLabel, sequenceGroupBox, orderLabel, sequenceOrderBox,
                                                  respawnHeader, respawnCheckBox, respawnDurationLabel, respawnDurationBox})
        respawnHeader.Visible = Not editingExitPath
        For Each control As Control In toolbar.Controls
            control.Margin = New Padding(3, 3, 3, 3)
            If Not TypeOf control Is Label Then control.Width = 290
        Next

        Dim actions As New TableLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 44, .ColumnCount = 2}
        actions.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        actions.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        saveButton.Dock = DockStyle.Fill : pathCancelButton.Dock = DockStyle.Fill
        saveButton.Margin = New Padding(0, 5, 4, 0) : pathCancelButton.Margin = New Padding(4, 5, 0, 0)
        actions.Controls.Add(saveButton, 0, 0) : actions.Controls.Add(pathCancelButton, 1, 0)
        sidebar.Controls.Add(toolbar) : sidebar.Controls.Add(actions)

        Dim help As New Label With {
            .Dock = DockStyle.Bottom,
            .Height = 28,
            .TextAlign = ContentAlignment.MiddleCenter,
            .ForeColor = Color.White,
            .BackColor = Color.FromArgb(22, 25, 38),
            .Text = If(editingExitPath,
                       "Click empty space to add a point. Drag a numbered point to move it. Right-click a point to delete it.",
                       "Drag the orange RESPAWN START handle to set how the feeder ball enters its original starting position.")
        }

        canvas.Dock = DockStyle.Fill
        canvas.BackglassImage = previewBackglassImage
        canvas.SnippetImage = snippet.Image
        canvas.SnippetBounds = New RectangleF(snippet.Location.X, snippet.Location.Y,
                                               Math.Max(1, snippet.Size.Width), Math.Max(1, snippet.Size.Height))
        canvas.LoopPath = loopCheckBox.Checked
        canvas.RollEnabled = rollBallCheckBox.Checked
        Dim savedPoints As List(Of PointF) = If(editingExitPath, snippet.SnippitInfo.MotionPathExitPoints, snippet.SnippitInfo.MotionPathPoints)
        If savedPoints.Count >= 2 Then
            canvas.PathPoints.AddRange(savedPoints)
            canvas.StopPreview()
        Else
            canvas.ResetDefaultPath()
        End If
        canvas.RespawnEnabled = Not editingExitPath AndAlso respawnCheckBox.Checked
        Dim sourceCenter As New PointF(canvas.SnippetBounds.X + canvas.SnippetBounds.Width / 2.0F,
                                       canvas.SnippetBounds.Y + canvas.SnippetBounds.Height / 2.0F)
        canvas.RespawnStart = If(snippet.SnippitInfo.MotionPathRespawnEnabled,
                                 snippet.SnippitInfo.MotionPathRespawnPoint,
                                 New PointF(sourceCenter.X - Math.Max(60.0F, snippet.Size.Width * 2.0F), sourceCenter.Y))

        timer.Interval = 16
        AddHandler timer.Tick, AddressOf PreviewTick
        Controls.Add(canvas)
        Controls.Add(help)
        Controls.Add(sidebar)
    End Sub

    Public ReadOnly Property ResultPoints As List(Of PointF)
        Get
            Return New List(Of PointF)(canvas.PathPoints)
        End Get
    End Property

    Public ReadOnly Property ResultDuration As Integer
        Get
            Return CInt(speedBox.Value)
        End Get
    End Property

    Public ReadOnly Property ResultLoop As Boolean
        Get
            Return Not editingExitPath AndAlso loopCheckBox.Checked
        End Get
    End Property

    Public ReadOnly Property ResultRollEnabled As Boolean
        Get
            Return rollBallCheckBox.Checked
        End Get
    End Property

    Public ReadOnly Property ResultSolenoidID As Integer
        Get
            Return If(triggerTypeBox.SelectedIndex = 1, CInt(triggerIDBox.Value), 0)
        End Get
    End Property

    Public ReadOnly Property ResultLampID As Integer
        Get
            Return If(triggerTypeBox.SelectedIndex = 2, CInt(triggerIDBox.Value), 0)
        End Get
    End Property

    Public ReadOnly Property ResultB2SID As Integer
        Get
            Return If(triggerTypeBox.SelectedIndex = 3, CInt(triggerIDBox.Value), 0)
        End Get
    End Property

    Public ReadOnly Property ResultStopB2SID As Integer
        Get
            Return CInt(stopIDBox.Value)
        End Get
    End Property

    Public ReadOnly Property ResultResumeB2SID As Integer
        Get
            Return CInt(resumeIDBox.Value)
        End Get
    End Property

    Public ReadOnly Property ResultQueueTriggers As Boolean
        Get
            Return queueTriggersCheckBox.Checked
        End Get
    End Property

    Public ReadOnly Property ResultSequenceGroup As String
        Get
            Return sequenceGroupBox.Text.Trim()
        End Get
    End Property

    Public ReadOnly Property ResultSequenceOrder As Integer
        Get
            Return CInt(sequenceOrderBox.Value)
        End Get
    End Property

    Public ReadOnly Property ResultRespawnEnabled As Boolean
        Get
            Return Not editingExitPath AndAlso respawnCheckBox.Checked
        End Get
    End Property

    Public ReadOnly Property ResultRespawnStart As PointF
        Get
            Return canvas.RespawnStart
        End Get
    End Property

    Public ReadOnly Property ResultRespawnDuration As Integer
        Get
            Return CInt(respawnDurationBox.Value)
        End Get
    End Property

    Private Sub ConfigureButton(ByVal button As Button, ByVal caption As String, ByVal handler As EventHandler)
        button.Text = caption
        button.AutoSize = False
        button.Width = 290
        button.Height = 29
        button.Margin = New Padding(3, 1, 3, 1)
        AddHandler button.Click, handler
    End Sub

    Private Shared Function SidebarHeader(ByVal caption As String) As Label
        Return New Label With {.Text = caption, .ForeColor = Color.FromArgb(105, 220, 255), .Font = New Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                               .AutoSize = False, .Width = 290, .Height = 24, .TextAlign = ContentAlignment.BottomLeft,
                               .Margin = New Padding(3, 10, 3, 2)}
    End Function

    Private Shared Function SidebarLabel(ByVal caption As String) As Label
        Return New Label With {.Text = caption, .ForeColor = Color.White, .AutoSize = False, .Width = 290, .Height = 20,
                               .TextAlign = ContentAlignment.BottomLeft, .Margin = New Padding(3, 3, 3, 0)}
    End Function

    Private Sub TogglePreview(ByVal sender As Object, ByVal e As EventArgs)
        If timer.Enabled Then
            StopPreview()
        ElseIf canvas.PathPoints.Count >= 2 Then
            canvas.StartPreview(CInt(speedBox.Value))
            timer.Start()
            playButton.Text = "Stop Preview"
        End If
    End Sub

    Private Sub PreviewTick(ByVal sender As Object, ByVal e As EventArgs)
        If Not canvas.AdvancePreview() Then StopPreview()
    End Sub

    Private Sub StopPreview()
        timer.Stop()
        canvas.StopPreview()
        playButton.Text = "Play Preview"
    End Sub

    Private Sub ResetPath(ByVal sender As Object, ByVal e As EventArgs)
        StopPreview()
        canvas.ResetDefaultPath()
    End Sub

    Private Sub ClearPath(ByVal sender As Object, ByVal e As EventArgs)
        StopPreview()
        canvas.PathPoints.Clear()
        canvas.Invalidate()
    End Sub

    Private Sub SavePath(ByVal sender As Object, ByVal e As EventArgs)
        If canvas.PathPoints.Count < 2 Then
            MessageBox.Show(Me, "A motion path needs at least two points.", "Motion Path Test",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If Not editingExitPath AndAlso triggerTypeBox.SelectedIndex = 3 AndAlso stopIDBox.Value > 0 AndAlso triggerIDBox.Value = stopIDBox.Value Then
            MessageBox.Show(Me, "Start and Stop must use different B2S IDs.", "Motion Path Test",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim assignedIDs As New Generic.HashSet(Of Decimal)()
        If Not editingExitPath Then
        If triggerTypeBox.SelectedIndex = 3 Then assignedIDs.Add(triggerIDBox.Value)
        If stopIDBox.Value > 0 AndAlso Not assignedIDs.Add(stopIDBox.Value) Then
            MessageBox.Show(Me, "Start, Stop, and Resume must use different B2S IDs.", "Motion Path Test", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If resumeIDBox.Value > 0 AndAlso Not assignedIDs.Add(resumeIDBox.Value) Then
            MessageBox.Show(Me, "Start, Stop, and Resume must use different B2S IDs.", "Motion Path Test", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        End If
        If Not editingExitPath AndAlso sequenceGroupBox.Text.Trim().Length > 0 AndAlso sequenceOrderBox.Value = 0 Then
            MessageBox.Show(Me, "A sequence group member needs an order of 1 or higher.", "Motion Path Test", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If Not editingExitPath AndAlso sequenceGroupBox.Text.Trim().Length = 0 AndAlso sequenceOrderBox.Value > 0 Then
            MessageBox.Show(Me, "Enter a sequence group name or set the order to 0.", "Motion Path Test", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        StopPreview()
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Sub CancelPath(ByVal sender As Object, ByVal e As EventArgs)
        DialogResult = DialogResult.Cancel
        Close()
    End Sub

    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        If disposing Then
            timer.Dispose()
            previewBackglassImage.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

    Private Class MotionPathCanvas
        Inherits EditorZoomCanvas

        Public BackglassImage As Image
        Public SnippetImage As Image
        Public SnippetBounds As RectangleF
        Public ReadOnly PathPoints As New List(Of PointF)()
        Public LoopPath As Boolean
        Public RollEnabled As Boolean
        Public RespawnEnabled As Boolean
        Public RespawnStart As PointF

        Private draggingIndex As Integer = -1
        Private draggingRespawn As Boolean
        Private previewStarted As DateTime
        Private previewDuration As Integer
        Private previewPosition As PointF
        Private previewing As Boolean
        Private previewRollAngle As Single

        Public Sub New()
            DoubleBuffered = True
            SetStyle(ControlStyles.ResizeRedraw Or ControlStyles.UserPaint Or
                     ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer, True)
            BackColor = Color.Black
        End Sub

        Public Sub ResetDefaultPath()
            PathPoints.Clear()
            Dim start As New PointF(SnippetBounds.X + SnippetBounds.Width / 2.0F,
                                    SnippetBounds.Y + SnippetBounds.Height / 2.0F)
            PathPoints.Add(start)
            PathPoints.Add(New PointF(start.X + Math.Max(100.0F, SnippetBounds.Width), start.Y))
            previewPosition = start
            Invalidate()
        End Sub

        Public Sub StartPreview(ByVal duration As Integer)
            previewDuration = Math.Max(1, duration)
            previewStarted = DateTime.UtcNow
            previewing = True
            previewPosition = PathPoints(0)
            previewRollAngle = 0.0F
            Invalidate()
        End Sub

        Public Function AdvancePreview() As Boolean
            If Not previewing OrElse PathPoints.Count < 2 Then Return False
            Dim progress As Double = (DateTime.UtcNow - previewStarted).TotalMilliseconds / previewDuration
            If progress >= 1.0 Then
                If LoopPath Then
                    previewStarted = DateTime.UtcNow
                    progress = 0.0
                Else
                    previewPosition = PathPoints(PathPoints.Count - 1)
                    previewing = False
                    Invalidate()
                    Return False
                End If
            End If
            Dim nextPosition As PointF = PointAlongPath(CSng(Math.Max(0.0, progress)))
            AdvancePreviewRoll(previewPosition, nextPosition)
            previewPosition = nextPosition
            Invalidate()
            Return True
        End Function

        Public Sub StopPreview()
            previewing = False
            If PathPoints.Count > 0 Then previewPosition = PathPoints(0)
            previewRollAngle = 0.0F
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
            MyBase.OnPaint(e)
            If BackglassImage Is Nothing Then Return
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic

            Dim view As RectangleF = ImageView()
            e.Graphics.DrawImage(BackglassImage, view)
            Dim scale As Single = view.Width / BackglassImage.Width
            Dim state As GraphicsState = e.Graphics.Save()
            e.Graphics.TranslateTransform(view.X, view.Y)
            e.Graphics.ScaleTransform(scale, scale)

            If PathPoints.Count > 1 Then
                Using pathPen As New Pen(Color.FromArgb(235, 0, 220, 255), 3.0F / scale)
                    pathPen.DashStyle = DashStyle.Dash
                    e.Graphics.DrawLines(pathPen, PathPoints.ToArray())
                End Using
            End If

            If RespawnEnabled AndAlso PathPoints.Count > 0 Then
                Dim target As New PointF(SnippetBounds.X + SnippetBounds.Width / 2.0F,
                                         SnippetBounds.Y + SnippetBounds.Height / 2.0F)
                ' Keep the start handle at the true feeder point, but use a
                ' compact half-length direction indicator so it does not cover
                ' the artwork and path controls around the trough.
                Dim arrowEnd As New PointF(RespawnStart.X + (target.X - RespawnStart.X) * 0.5F,
                                           RespawnStart.Y + (target.Y - RespawnStart.Y) * 0.5F)
                Using arrowPen As New Pen(Color.Orange, 2.25F / scale)
                    arrowPen.StartCap = LineCap.Round
                    arrowPen.CustomEndCap = New AdjustableArrowCap(4.0F / scale, 5.0F / scale)
                    e.Graphics.DrawLine(arrowPen, RespawnStart, arrowEnd)
                End Using
                Dim handleRadius As Single = 6.0F / scale
                e.Graphics.FillEllipse(Brushes.Orange, RespawnStart.X-handleRadius, RespawnStart.Y-handleRadius, handleRadius*2.0F, handleRadius*2.0F)
                Using outline As New Pen(Color.White, 1.5F / scale)
                    e.Graphics.DrawEllipse(outline, RespawnStart.X-handleRadius, RespawnStart.Y-handleRadius, handleRadius*2.0F, handleRadius*2.0F)
                End Using
                Using labelFont As New Font(Font.FontFamily, Math.Max(7.0F, 8.0F / scale), FontStyle.Bold)
                    e.Graphics.DrawString("SPAWN", labelFont, Brushes.Orange, RespawnStart.X+9.0F/scale, RespawnStart.Y-7.0F/scale)
                End Using
            End If

            If SnippetImage IsNot Nothing AndAlso PathPoints.Count > 0 Then
                Dim center As PointF = If(previewing, previewPosition, PathPoints(0))
                Dim drawBounds As New RectangleF(center.X - SnippetBounds.Width / 2.0F,
                                                 center.Y - SnippetBounds.Height / 2.0F,
                                                 SnippetBounds.Width, SnippetBounds.Height)
                If RollEnabled AndAlso previewing AndAlso Math.Abs(previewRollAngle) > 0.001F Then
                    Dim imageState As GraphicsState = e.Graphics.Save()
                    e.Graphics.DrawImage(SnippetImage,
                                         BallRollDestinationPoints(drawBounds, previewRollAngle),
                                         New RectangleF(0, 0, SnippetImage.Width, SnippetImage.Height),
                                         GraphicsUnit.Pixel)
                    e.Graphics.Restore(imageState)
                Else
                    e.Graphics.DrawImage(SnippetImage, drawBounds)
                End If
            End If

            For index As Integer = 0 To PathPoints.Count - 1
                Dim radius As Single = 9.0F / scale
                Dim p As PointF = PathPoints(index)
                Dim marker As New RectangleF(p.X - radius, p.Y - radius, radius * 2, radius * 2)
                Using fill As New SolidBrush(If(index = 0, Color.LimeGreen, Color.OrangeRed))
                    e.Graphics.FillEllipse(fill, marker)
                End Using
                Using outline As New Pen(Color.White, 2.0F / scale)
                    e.Graphics.DrawEllipse(outline, marker)
                End Using
                Using labelFont As New Font(Font.FontFamily, Math.Max(7.0F, 9.0F / scale), FontStyle.Bold)
                    Dim label As String = (index + 1).ToString()
                    Dim size As SizeF = e.Graphics.MeasureString(label, labelFont)
                    e.Graphics.DrawString(label, labelFont, Brushes.Black, p.X - size.Width / 2.0F, p.Y - size.Height / 2.0F)
                End Using
            Next
            e.Graphics.Restore(state)
        End Sub

        Private Sub AdvancePreviewRoll(ByVal previous As PointF, ByVal current As PointF)
            If Not RollEnabled Then Return
            Dim dx As Single = current.X - previous.X
            Dim dy As Single = current.Y - previous.Y
            Dim distance As Double = Math.Sqrt(dx * dx + dy * dy)
            If distance <= 0.0001R Then Return
            Dim direction As Single = If(Math.Abs(dx) >= Math.Abs(dy), Math.Sign(dx), Math.Sign(dy))
            If direction = 0.0F Then Return
            Dim radius As Single = Math.Max(1.0F, Math.Min(SnippetBounds.Width, SnippetBounds.Height) / 2.0F)
            previewRollAngle = CSng((previewRollAngle + direction * distance / radius * 180.0R / Math.PI) Mod 360.0R)
        End Sub

        ' Rotate in normalized sphere coordinates, then restore the authored
        ' width/height. This keeps a non-square ball silhouette from turning
        ' into a rotating egg while its internal artwork visibly rolls.
        Private Shared Function BallRollDestinationPoints(ByVal bounds As RectangleF, ByVal angle As Single) As PointF()
            Dim radians As Double = angle * Math.PI / 180.0R
            Dim cosine As Double = Math.Cos(radians)
            Dim sine As Double = Math.Sin(radians)
            Dim centerX As Single = bounds.Left + bounds.Width / 2.0F
            Dim centerY As Single = bounds.Top + bounds.Height / 2.0F
            Return New PointF() {
                BallRollDestinationPoint(centerX, centerY, bounds.Width, bounds.Height, -0.5R, -0.5R, cosine, sine),
                BallRollDestinationPoint(centerX, centerY, bounds.Width, bounds.Height, 0.5R, -0.5R, cosine, sine),
                BallRollDestinationPoint(centerX, centerY, bounds.Width, bounds.Height, -0.5R, 0.5R, cosine, sine)
            }
        End Function

        Private Shared Function BallRollDestinationPoint(ByVal centerX As Single, ByVal centerY As Single,
                                                          ByVal width As Single, ByVal height As Single,
                                                          ByVal x As Double, ByVal y As Double,
                                                          ByVal cosine As Double, ByVal sine As Double) As PointF
            Return New PointF(CSng(centerX + width * (x * cosine - y * sine)),
                              CSng(centerY + height * (x * sine + y * cosine)))
        End Function

        Protected Overrides Sub OnMouseDown(ByVal e As MouseEventArgs)
            If BeginNavigation(e) Then Return
            MyBase.OnMouseDown(e)
            If previewing Then Return
            Dim imagePoint As PointF = ClientToImage(e.Location)
            If e.Button = MouseButtons.Left AndAlso RespawnEnabled AndAlso HitRespawnPoint(imagePoint) Then
                draggingRespawn = True
                Capture = True
                Return
            End If
            Dim hit As Integer = HitPoint(imagePoint)
            If e.Button = MouseButtons.Right AndAlso hit >= 0 Then
                PathPoints.RemoveAt(hit)
                Invalidate()
            ElseIf e.Button = MouseButtons.Left Then
                If hit >= 0 Then
                    draggingIndex = hit
                    Capture = True
                ElseIf IsInsideImage(e.Location) Then
                    PathPoints.Add(imagePoint)
                    draggingIndex = PathPoints.Count - 1
                    Capture = True
                    Invalidate()
                End If
            End If
        End Sub

        Protected Overrides Sub OnMouseMove(ByVal e As MouseEventArgs)
            If MoveNavigation(e) Then Return
            MyBase.OnMouseMove(e)
            If draggingRespawn Then
                Dim respawn As PointF = ClientToImage(e.Location)
                respawn.X = Math.Max(0, Math.Min(BackglassImage.Width, respawn.X))
                respawn.Y = Math.Max(0, Math.Min(BackglassImage.Height, respawn.Y))
                RespawnStart = respawn
                Invalidate()
                Return
            End If
            If draggingIndex < 0 OrElse draggingIndex >= PathPoints.Count Then Return
            Dim p As PointF = ClientToImage(e.Location)
            p.X = Math.Max(0, Math.Min(BackglassImage.Width, p.X))
            p.Y = Math.Max(0, Math.Min(BackglassImage.Height, p.Y))
            PathPoints(draggingIndex) = p
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseUp(ByVal e As MouseEventArgs)
            If EndNavigation() Then Return
            MyBase.OnMouseUp(e)
            draggingIndex = -1
            draggingRespawn = False
            Capture = False
        End Sub

        Private Function PointAlongPath(ByVal progress As Single) As PointF
            Dim lengths As New List(Of Double)()
            Dim total As Double = 0
            Dim segmentCount As Integer = PathPoints.Count - 1 + If(LoopPath, 1, 0)
            For index As Integer = 0 To segmentCount - 1
                Dim fromPoint As PointF = PathPoints(index Mod PathPoints.Count)
                Dim toPoint As PointF = PathPoints((index + 1) Mod PathPoints.Count)
                Dim dx As Double = toPoint.X - fromPoint.X
                Dim dy As Double = toPoint.Y - fromPoint.Y
                Dim length As Double = Math.Sqrt(dx * dx + dy * dy)
                lengths.Add(length)
                total += length
            Next
            If total <= 0 Then Return PathPoints(0)
            Dim target As Double = progress * total
            For index As Integer = 0 To lengths.Count - 1
                If target <= lengths(index) OrElse index = lengths.Count - 1 Then
                    Dim part As Single = If(lengths(index) <= 0, 0.0F, CSng(target / lengths(index)))
                    Dim fromPoint As PointF = PathPoints(index Mod PathPoints.Count)
                    Dim toPoint As PointF = PathPoints((index + 1) Mod PathPoints.Count)
                    Return New PointF(fromPoint.X + (toPoint.X - fromPoint.X) * part,
                                      fromPoint.Y + (toPoint.Y - fromPoint.Y) * part)
                End If
                target -= lengths(index)
            Next
            Return PathPoints(PathPoints.Count - 1)
        End Function

        Private Function ImageView() As RectangleF
            Return ZoomedImageView()
        End Function

        Protected Overrides Function BaseImageView() As RectangleF
            If BackglassImage Is Nothing OrElse Width <= 0 OrElse Height <= 0 Then Return RectangleF.Empty
            Dim scale As Single = Math.Min(Width / CSng(BackglassImage.Width), Height / CSng(BackglassImage.Height))
            Dim size As New SizeF(BackglassImage.Width * scale, BackglassImage.Height * scale)
            Return New RectangleF((Width - size.Width) / 2.0F, (Height - size.Height) / 2.0F, size.Width, size.Height)
        End Function

        Private Function ClientToImage(ByVal point As Point) As PointF
            Dim view As RectangleF = ImageView()
            If view.Width <= 0 Then Return PointF.Empty
            Dim scale As Single = view.Width / BackglassImage.Width
            Return New PointF((point.X - view.X) / scale, (point.Y - view.Y) / scale)
        End Function

        Private Function IsInsideImage(ByVal point As Point) As Boolean
            Return ImageView().Contains(point)
        End Function

        Private Function HitPoint(ByVal point As PointF) As Integer
            Dim view As RectangleF = ImageView()
            If view.Width <= 0 Then Return -1
            Dim scale As Single = view.Width / BackglassImage.Width
            Dim radius As Single = 13.0F / scale
            For index As Integer = PathPoints.Count - 1 To 0 Step -1
                Dim dx As Single = PathPoints(index).X - point.X
                Dim dy As Single = PathPoints(index).Y - point.Y
                If dx * dx + dy * dy <= radius * radius Then Return index
            Next
            Return -1
        End Function

        Private Function HitRespawnPoint(ByVal point As PointF) As Boolean
            Dim view As RectangleF = ImageView()
            If view.Width <= 0 Then Return False
            Dim scale As Single = view.Width / BackglassImage.Width
            Dim radius As Single = 16.0F / scale
            Dim dx As Single = RespawnStart.X - point.X
            Dim dy As Single = RespawnStart.Y - point.Y
            Return dx * dx + dy * dy <= radius * radius
        End Function
    End Class
End Class

' Guided group creator layered on the proven single-path editor. The selected
' snippet supplies the shared image. Users draw entry/exit once, drag the first
' and last slot, choose a count, and the Layers tool creates every member.
Public Class formTroughWizard
    Inherits B2SThemedForm

    Private ReadOnly source As Illumination.BulbInfo
    Private ReadOnly wizardBackgroundImage As Image
    Private ReadOnly slotCanvas As New TroughSlotCanvas()
    Private ReadOnly countBox As New NumericUpDown()
    Private ReadOnly rollBallCheckBox As New CheckBox()
    Private ReadOnly groupBox As New TextBox()
    Private ReadOnly entryButton As New Button()
    Private ReadOnly exitButton As New Button()
    Private ReadOnly selectedBallLabel As New Label()
    Private ReadOnly replaceBallImageButton As New Button()
    Private ReadOnly resetBallImageButton As New Button()
    Private ReadOnly createButton As New Button()
    Private ReadOnly wizardCancelButton As New Button()
    Private ReadOnly statusLabel As New Label()
    Private ReadOnly ballImages As New List(Of Image)()
    Private ReadOnly _ballSize As Size
    Private selectedBallIndex As Integer = 0
    Private entryPoints As New List(Of PointF)()
    Private exitPoints As New List(Of PointF)()
    Private entryDuration As Integer
    Private exitDuration As Integer
    Private troughRespawnEnabled As Boolean
    Private troughRespawnStart As PointF
    Private troughRespawnDuration As Integer
    Private entrySolenoid, entryLamp, entryB2S, stopB2S, resumeB2S As Integer
    Private removeSolenoid, removeLamp, removeB2S As Integer

    Public Sub New(snippet As Illumination.BulbInfo, backglassImage As Image,
                   Optional existingGroupMembers As IEnumerable(Of Illumination.BulbInfo) = Nothing)
        Dim orderedMembers As New List(Of Illumination.BulbInfo)()
        If existingGroupMembers IsNot Nothing Then
            For Each member As Illumination.BulbInfo In existingGroupMembers
                If member IsNot Nothing AndAlso Not orderedMembers.Contains(member) Then orderedMembers.Add(member)
            Next
            orderedMembers.Sort(Function(left, right) left.SnippitInfo.MotionPathSequenceOrder.CompareTo(right.SnippitInfo.MotionPathSequenceOrder))
        End If
        source = If(orderedMembers.Count > 0, orderedMembers(0), snippet)
        Dim diameter As Integer = Math.Max(1, Math.Min(source.Size.Width, source.Size.Height))
        _ballSize = New Size(diameter, diameter)
        wizardBackgroundImage = New Bitmap(backglassImage)
        entryPoints.AddRange(source.SnippitInfo.MotionPathPoints)
        exitPoints.AddRange(source.SnippitInfo.MotionPathExitPoints)
        entryDuration = source.SnippitInfo.MotionPathDuration
        exitDuration = source.SnippitInfo.MotionPathExitDuration
        troughRespawnEnabled = source.SnippitInfo.MotionPathRespawnEnabled
        troughRespawnDuration = source.SnippitInfo.MotionPathRespawnDuration
        Dim originalStart As New PointF(source.Location.X + source.Size.Width / 2.0F,
                                        source.Location.Y + source.Size.Height / 2.0F)
        troughRespawnStart = If(troughRespawnEnabled, source.SnippitInfo.MotionPathRespawnPoint,
                                New PointF(originalStart.X - Math.Max(60.0F, source.Size.Width * 2.0F), originalStart.Y))
        entrySolenoid = source.SnippitInfo.MotionPathSolenoidID : entryLamp = source.SnippitInfo.MotionPathLampID : entryB2S = source.SnippitInfo.MotionPathB2SID
        stopB2S = source.SnippitInfo.MotionPathStopB2SID : resumeB2S = source.SnippitInfo.MotionPathResumeB2SID
        removeSolenoid = source.SnippitInfo.MotionPathRemoveSolenoidID : removeLamp = source.SnippitInfo.MotionPathRemoveLampID : removeB2S = source.SnippitInfo.MotionPathRemoveB2SID

        Text = "Create Trough Animation"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(1000, 720)
        MinimumSize = New Size(900, 560)
        BackColor = Color.FromArgb(9, 12, 20)

        Dim sidebar As New FlowLayoutPanel With {
            .Dock = DockStyle.Right, .Width = 330, .Padding = New Padding(10, 8, 10, 8),
            .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .AutoScroll = True,
            .BackColor = Color.FromArgb(22, 25, 38)
        }
        Dim setupHeader As Label = WizardHeader("TROUGH SETUP")
        Dim groupLabel As Label = WizardLabel("Group name:")
        groupBox.Width = 290 : groupBox.MaxLength = 64 : groupBox.Text = If(String.IsNullOrWhiteSpace(source.SnippitInfo.MotionPathSequenceGroup), "Trough", source.SnippitInfo.MotionPathSequenceGroup)
        groupBox.Margin = New Padding(3)
        Dim countLabel As Label = WizardLabel("Number of balls:")
        countBox.Minimum = 2 : countBox.Maximum = 32
        countBox.Value = Math.Max(CInt(countBox.Minimum), Math.Min(CInt(countBox.Maximum), If(orderedMembers.Count >= 2, orderedMembers.Count, 10)))
        countBox.Width = 290 : countBox.Margin = New Padding(3)
        InitializeBallImages(orderedMembers, CInt(countBox.Value))
        rollBallCheckBox.Text = "Roll balls while moving"
        rollBallCheckBox.Checked = source.SnippitInfo.MotionPathRollEnabled
        rollBallCheckBox.ForeColor = Color.White
        rollBallCheckBox.AutoSize = False
        rollBallCheckBox.Width = 290
        rollBallCheckBox.Height = 25
        rollBallCheckBox.Margin = New Padding(3)
        ConfigureButton(entryButton, "1. Draw Entry Path", AddressOf EditEntry)
        ConfigureButton(exitButton, "2. Draw Exit Path", AddressOf EditExit)
        selectedBallLabel.Text = "Selected ball: 1"
        selectedBallLabel.ForeColor = Color.White
        selectedBallLabel.AutoSize = False
        selectedBallLabel.Width = 290
        selectedBallLabel.Height = 22
        selectedBallLabel.Margin = New Padding(3)
        ConfigureButton(replaceBallImageButton, "Choose PNG for Selected Ball...", AddressOf ReplaceSelectedBallImage)
        ConfigureButton(resetBallImageButton, "Use Original Ball Image", AddressOf ResetSelectedBallImage)
        ConfigureButton(createButton, "5. Create Trough", AddressOf CreateTrough)
        ConfigureButton(wizardCancelButton, "Cancel", Sub() DialogResult = DialogResult.Cancel)
        sidebar.Controls.AddRange(New Control() {setupHeader, groupLabel, groupBox, countLabel, countBox, rollBallCheckBox,
                                                 WizardHeader("PATHS"), entryButton, exitButton,
                                                 WizardHeader("BALL IMAGES"), selectedBallLabel, replaceBallImageButton, resetBallImageButton,
                                                 WizardHeader("SAVE OR CANCEL"), createButton, wizardCancelButton})

        statusLabel.Dock = DockStyle.Bottom : statusLabel.Height = 30 : statusLabel.TextAlign = ContentAlignment.MiddleCenter
        statusLabel.ForeColor = Color.White : statusLabel.BackColor = Color.FromArgb(22, 25, 38)
        statusLabel.Text = "3. Drag FIRST/LAST to place the trough. Click any ball to select its PNG."
        slotCanvas.Dock = DockStyle.Fill : slotCanvas.BackglassImage = wizardBackgroundImage : slotCanvas.SnippetImage = ballImages(0)
        slotCanvas.BallImages = ballImages
        Dim first As PointF = If(entryPoints.Count > 0, entryPoints(entryPoints.Count - 1), New PointF(snippet.Location.X + snippet.Size.Width / 2.0F, snippet.Location.Y + snippet.Size.Height / 2.0F))
        slotCanvas.SnippetSize = _ballSize
        slotCanvas.FirstCenter = first
        slotCanvas.LastCenter = If(orderedMembers.Count >= 2,
                                   ExistingPathEndpoint(orderedMembers(orderedMembers.Count - 1)),
                                   New PointF(first.X + _ballSize.Width * (CInt(countBox.Value) - 1), first.Y))
        ' NumericUpDown does not commit keyboard input to Value until focus
        ' changes. Listen to both paths so typing "5" redraws five slots at once.
        AddHandler countBox.ValueChanged, AddressOf BallCountEditorChanged
        AddHandler countBox.TextChanged, AddressOf BallCountEditorChanged
        AddHandler slotCanvas.SelectedSlotChanged, AddressOf SelectedBallChanged
        slotCanvas.SlotCount = DisplayedBallCount()
        UpdateSelectedBallLabel()
        Controls.Add(slotCanvas) : Controls.Add(statusLabel) : Controls.Add(sidebar)
    End Sub

    Private Sub BallCountEditorChanged(sender As Object, e As EventArgs)
        Dim displayedCount As Integer = DisplayedBallCount()
        EnsureBallImages(displayedCount)
        If selectedBallIndex >= displayedCount Then selectedBallIndex = displayedCount - 1
        slotCanvas.SelectedSlotIndex = selectedBallIndex
        slotCanvas.SlotCount = displayedCount
        UpdateSelectedBallLabel()
        slotCanvas.Invalidate()
    End Sub

    Private Sub InitializeBallImages(ByVal orderedMembers As IList(Of Illumination.BulbInfo), ByVal count As Integer)
        For Each image As Image In ballImages
            image.Dispose()
        Next
        ballImages.Clear()
        For index As Integer = 0 To count - 1
            Dim image As Image = source.Image
            If orderedMembers IsNot Nothing AndAlso index < orderedMembers.Count AndAlso orderedMembers(index).Image IsNot Nothing Then
                image = orderedMembers(index).Image
            End If
            ballImages.Add(NormalizeBallImage(image, _ballSize))
        Next
    End Sub

    Private Sub EnsureBallImages(ByVal count As Integer)
        While ballImages.Count < count
            ballImages.Add(NormalizeBallImage(source.Image, _ballSize))
        End While
    End Sub

    Private Sub SelectedBallChanged(sender As Object, e As EventArgs)
        selectedBallIndex = Math.Max(0, Math.Min(DisplayedBallCount() - 1, slotCanvas.SelectedSlotIndex))
        UpdateSelectedBallLabel()
    End Sub

    Private Sub UpdateSelectedBallLabel()
        selectedBallLabel.Text = "Selected ball: " & (selectedBallIndex + 1).ToString() & " of " & DisplayedBallCount().ToString()
    End Sub

    Private Sub ReplaceSelectedBallImage(sender As Object, e As EventArgs)
        Using dialog As New OpenFileDialog With {
            .Title = "Choose PNG for Ball " & (selectedBallIndex + 1).ToString(),
            .Filter = "PNG images (*.png)|*.png",
            .CheckFileExists = True,
            .Multiselect = False
        }
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                Dim normalized As Image = LoadNormalizedBallPng(dialog.FileName)
                Dim oldImage As Image = ballImages(selectedBallIndex)
                ballImages(selectedBallIndex) = normalized
                If oldImage IsNot Nothing Then oldImage.Dispose()
                slotCanvas.Invalidate()
            Catch ex As Exception
                MessageBox.Show(Me, "The selected PNG could not be loaded." & Environment.NewLine & Environment.NewLine & ex.Message,
                                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End Using
    End Sub

    Private Sub ResetSelectedBallImage(sender As Object, e As EventArgs)
        Dim oldImage As Image = ballImages(selectedBallIndex)
        ballImages(selectedBallIndex) = NormalizeBallImage(source.Image, _ballSize)
        If oldImage IsNot Nothing Then oldImage.Dispose()
        slotCanvas.Invalidate()
    End Sub

    Private Function LoadNormalizedBallPng(ByVal fileName As String) As Image
        Using stream As New IO.FileStream(fileName, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read)
            Using loaded As Image = Image.FromStream(stream, True, True)
                Return NormalizeBallImage(loaded, _ballSize)
            End Using
        End Using
    End Function

    Private Shared Function NormalizeBallImage(ByVal imported As Image, ByVal targetSize As Size) As Image
        If imported Is Nothing Then Throw New ArgumentNullException(NameOf(imported))

        Using importedBitmap As Bitmap = ToArgbBitmap(imported)
            Dim sourceBounds As Rectangle = AlphaBounds(importedBitmap)
            If sourceBounds.IsEmpty Then Throw New ArgumentException("The PNG does not contain any visible pixels.")

            Dim diameter As Integer = Math.Max(1, Math.Min(targetSize.Width, targetSize.Height))
            Dim result As New Bitmap(diameter, diameter, PixelFormat.Format32bppArgb)
            ' Preserve the complete visible PNG and its aspect ratio. The image
            ' is contained inside a true square ball canvas; no template mask,
            ' fill crop, or non-uniform stretching is applied.
            Dim scale As Single = Math.Min(diameter / CSng(sourceBounds.Width), diameter / CSng(sourceBounds.Height))
            Dim drawWidth As Single = sourceBounds.Width * scale
            Dim drawHeight As Single = sourceBounds.Height * scale
            Dim destination As New RectangleF((diameter - drawWidth) / 2.0F,
                                              (diameter - drawHeight) / 2.0F,
                                              drawWidth, drawHeight)
            Using visibleImage As Bitmap = importedBitmap.Clone(sourceBounds, PixelFormat.Format32bppArgb)
                Using graphics As Graphics = Graphics.FromImage(result)
                    graphics.Clear(Color.Transparent)
                    graphics.CompositingMode = CompositingMode.SourceCopy
                    graphics.CompositingQuality = CompositingQuality.HighQuality
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality
                    graphics.SmoothingMode = SmoothingMode.HighQuality
                    ' Drawing the prepared image as a whole avoids GDI+'s
                    ' source-rectangle/DPI path, which discarded the lower
                    ' portion of high-DPI replacement PNGs.
                    graphics.DrawImage(visibleImage, Rectangle.Round(destination))
                End Using
            End Using
            Return result
        End Using
    End Function

    Private Shared Function ToArgbBitmap(ByVal image As Image) As Bitmap
        Dim bitmap As New Bitmap(Math.Max(1, image.Width), Math.Max(1, image.Height), PixelFormat.Format32bppArgb)
        Using graphics As Graphics = Graphics.FromImage(bitmap)
            graphics.Clear(Color.Transparent)
            graphics.CompositingMode = CompositingMode.SourceCopy
            ' DrawImageUnscaled still honors physical DPI in this GDI+ path.
            ' Copy into the exact pixel rectangle so 72-DPI PNGs are not
            ' enlarged and clipped by the 96-DPI ARGB destination bitmap.
            graphics.DrawImage(image, New Rectangle(0, 0, bitmap.Width, bitmap.Height))
        End Using
        Return bitmap
    End Function

    Private Shared Function AlphaBounds(ByVal bitmap As Bitmap) As Rectangle
        Dim rectangle As New Rectangle(0, 0, bitmap.Width, bitmap.Height)
        Dim data As BitmapData = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
        Try
            Dim stride As Integer = Math.Abs(data.Stride)
            Dim bytes(stride * bitmap.Height - 1) As Byte
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length)
            Dim minX As Integer = bitmap.Width, minY As Integer = bitmap.Height, maxX As Integer = -1, maxY As Integer = -1
            For y As Integer = 0 To bitmap.Height - 1
                Dim row As Integer = If(data.Stride >= 0, y, bitmap.Height - 1 - y) * stride
                For x As Integer = 0 To bitmap.Width - 1
                    If bytes(row + x * 4 + 3) <> 0 Then
                        minX = Math.Min(minX, x) : minY = Math.Min(minY, y)
                        maxX = Math.Max(maxX, x) : maxY = Math.Max(maxY, y)
                    End If
                Next
            Next
            If maxX < minX OrElse maxY < minY Then Return Rectangle.Empty
            Return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1)
        Finally
            bitmap.UnlockBits(data)
        End Try
    End Function

    Private Function DisplayedBallCount() As Integer
        Dim typedCount As Integer
        If Integer.TryParse(countBox.Text.Trim(), typedCount) Then
            Return Math.Max(CInt(countBox.Minimum), Math.Min(CInt(countBox.Maximum), typedCount))
        End If
        Return CInt(countBox.Value)
    End Function

    Private Sub ConfigureButton(button As Button, caption As String, handler As EventHandler)
        button.Text = caption : button.AutoSize = False : button.Width = 290 : button.Height = 32 : button.Margin = New Padding(3)
        AddHandler button.Click, handler
    End Sub

    Private Shared Function WizardHeader(caption As String) As Label
        Return New Label With {.Text = caption, .ForeColor = Color.FromArgb(105, 220, 255),
                               .Font = New Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                               .AutoSize = False, .Width = 290, .Height = 26,
                               .TextAlign = ContentAlignment.BottomLeft, .Margin = New Padding(3, 10, 3, 2)}
    End Function

    Private Shared Function WizardLabel(caption As String) As Label
        Return New Label With {.Text = caption, .ForeColor = Color.White, .AutoSize = False,
                               .Width = 290, .Height = 20, .TextAlign = ContentAlignment.BottomLeft,
                               .Margin = New Padding(3, 3, 3, 0)}
    End Function

    Private Sub EditEntry(sender As Object, e As EventArgs)
        Dim editorSource As Illumination.BulbInfo = CreatePathEditorSource()
        Using editor As New formMotionPathTest(editorSource, wizardBackgroundImage, False)
            If editor.ShowDialog(Me) <> DialogResult.OK Then Return
            entryPoints = editor.ResultPoints : entryDuration = editor.ResultDuration
            entrySolenoid = editor.ResultSolenoidID : entryLamp = editor.ResultLampID : entryB2S = editor.ResultB2SID
            stopB2S = editor.ResultStopB2SID : resumeB2S = editor.ResultResumeB2SID
            troughRespawnEnabled = editor.ResultRespawnEnabled
            troughRespawnStart = editor.ResultRespawnStart
            troughRespawnDuration = editor.ResultRespawnDuration
            rollBallCheckBox.Checked = editor.ResultRollEnabled
            slotCanvas.FirstCenter = entryPoints(entryPoints.Count - 1) : slotCanvas.Invalidate()
            entryButton.Text = "✓ Entry Path"
        End Using
    End Sub

    Private Sub EditExit(sender As Object, e As EventArgs)
        Dim editorSource As Illumination.BulbInfo = CreatePathEditorSource()
        Using editor As New formMotionPathTest(editorSource, wizardBackgroundImage, True)
            If editor.ShowDialog(Me) <> DialogResult.OK Then Return
            exitPoints = editor.ResultPoints : exitDuration = editor.ResultDuration
            removeSolenoid = editor.ResultSolenoidID : removeLamp = editor.ResultLampID : removeB2S = editor.ResultB2SID
            rollBallCheckBox.Checked = editor.ResultRollEnabled
            exitButton.Text = "✓ Exit Path"
        End Using
    End Sub

    Private Function CreatePathEditorSource() As Illumination.BulbInfo
        Dim originalCenter As New PointF(source.Location.X + source.Size.Width / 2.0F,
                                         source.Location.Y + source.Size.Height / 2.0F)
        Dim editorSource As New Illumination.BulbInfo With {
            .Name = source.Name,
            .Image = ballImages(0),
            .Location = New Point(CInt(Math.Round(originalCenter.X - _ballSize.Width / 2.0F)),
                                  CInt(Math.Round(originalCenter.Y - _ballSize.Height / 2.0F))),
            .Size = _ballSize,
            .ParentForm = source.ParentForm,
            .IsImageSnippit = True
        }
        With editorSource.SnippitInfo
            .MotionPathPoints.AddRange(entryPoints)
            .MotionPathExitPoints.AddRange(exitPoints)
            .MotionPathDuration = entryDuration
            .MotionPathExitDuration = exitDuration
            .MotionPathSolenoidID = entrySolenoid : .MotionPathLampID = entryLamp : .MotionPathB2SID = entryB2S
            .MotionPathStopB2SID = stopB2S : .MotionPathResumeB2SID = resumeB2S
            .MotionPathRemoveSolenoidID = removeSolenoid : .MotionPathRemoveLampID = removeLamp : .MotionPathRemoveB2SID = removeB2S
            .MotionPathQueueTriggers = source.SnippitInfo.MotionPathQueueTriggers
            .MotionPathRollEnabled = rollBallCheckBox.Checked
            .MotionPathSequenceGroup = source.SnippitInfo.MotionPathSequenceGroup
            .MotionPathSequenceOrder = source.SnippitInfo.MotionPathSequenceOrder
            .MotionPathRespawnEnabled = troughRespawnEnabled
            .MotionPathRespawnPoint = troughRespawnStart
            .MotionPathRespawnDuration = troughRespawnDuration
        End With
        Return editorSource
    End Function

    Private Shared Function ExistingPathEndpoint(snippet As Illumination.BulbInfo) As PointF
        If snippet IsNot Nothing AndAlso snippet.SnippitInfo.MotionPathPoints.Count > 0 Then
            Return snippet.SnippitInfo.MotionPathPoints(snippet.SnippitInfo.MotionPathPoints.Count - 1)
        End If
        Return New PointF(snippet.Location.X + snippet.Size.Width / 2.0F,
                          snippet.Location.Y + snippet.Size.Height / 2.0F)
    End Function

    Private Sub CreateTrough(sender As Object, e As EventArgs)
        If groupBox.Text.Trim().Length = 0 Then MessageBox.Show(Me, "Enter a group name.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information) : Return
        If entryPoints.Count < 2 Then MessageBox.Show(Me, "Draw the entry path first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information) : Return
        If exitPoints.Count < 2 Then MessageBox.Show(Me, "Draw the exit path first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information) : Return
        DialogResult = DialogResult.OK : Close()
    End Sub

    Public ReadOnly Property BallCount As Integer
        Get
            Return DisplayedBallCount()
        End Get
    End Property
    Public Function BallImage(ByVal index As Integer) As Image
        If index < 0 OrElse index >= BallCount OrElse index >= ballImages.Count Then Return NormalizeBallImage(source.Image, _ballSize)
        Return New Bitmap(ballImages(index))
    End Function
    Public ReadOnly Property BallSize As Size
        Get
            Return _ballSize
        End Get
    End Property
    Public ReadOnly Property GroupName As String
        Get
            Return groupBox.Text.Trim()
        End Get
    End Property
    Public ReadOnly Property FirstCenter As PointF
        Get
            Return slotCanvas.FirstCenter
        End Get
    End Property
    Public ReadOnly Property LastCenter As PointF
        Get
            Return slotCanvas.LastCenter
        End Get
    End Property
    Public ReadOnly Property RespawnEnabled As Boolean
        Get
            Return troughRespawnEnabled
        End Get
    End Property
    Public ReadOnly Property RollEnabled As Boolean
        Get
            Return rollBallCheckBox.Checked
        End Get
    End Property
    Public ReadOnly Property RespawnStart As PointF
        Get
            Return troughRespawnStart
        End Get
    End Property
    Public ReadOnly Property RespawnTime As Integer
        Get
            Return troughRespawnDuration
        End Get
    End Property
    Public ReadOnly Property EntryPath As List(Of PointF)
        Get
            Return New List(Of PointF)(entryPoints)
        End Get
    End Property
    Public ReadOnly Property ExitPath As List(Of PointF)
        Get
            Return New List(Of PointF)(exitPoints)
        End Get
    End Property
    Public ReadOnly Property EntryTime As Integer
        Get
            Return entryDuration
        End Get
    End Property
    Public ReadOnly Property ExitTime As Integer
        Get
            Return exitDuration
        End Get
    End Property
    Public ReadOnly Property EntrySolenoidID As Integer
        Get
            Return entrySolenoid
        End Get
    End Property
    Public ReadOnly Property EntryLampID As Integer
        Get
            Return entryLamp
        End Get
    End Property
    Public ReadOnly Property EntryB2SID As Integer
        Get
            Return entryB2S
        End Get
    End Property
    Public ReadOnly Property StopB2SID As Integer
        Get
            Return stopB2S
        End Get
    End Property
    Public ReadOnly Property ResumeB2SID As Integer
        Get
            Return resumeB2S
        End Get
    End Property
    Public ReadOnly Property RemoveSolenoidID As Integer
        Get
            Return removeSolenoid
        End Get
    End Property
    Public ReadOnly Property RemoveLampID As Integer
        Get
            Return removeLamp
        End Get
    End Property
    Public ReadOnly Property RemoveB2SID As Integer
        Get
            Return removeB2S
        End Get
    End Property

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            wizardBackgroundImage.Dispose()
            For Each image As Image In ballImages
                image.Dispose()
            Next
            ballImages.Clear()
        End If
        MyBase.Dispose(disposing)
    End Sub

    Private Class TroughSlotCanvas
        Inherits EditorZoomCanvas
        Public BackglassImage As Image, SnippetImage As Image, SnippetSize As Size
        Public BallImages As IList(Of Image)
        Private _firstCenter As PointF
        Private _lastCenter As PointF
        Public SlotCount As Integer = 10
        Public SelectedSlotIndex As Integer = 0
        Public Event SelectedSlotChanged As EventHandler
        Private dragIndex As Integer = -1
        Public Sub New()
            DoubleBuffered = True
            BackColor = Color.Black
        End Sub
        Private Function ViewRect() As RectangleF
            Return ZoomedImageView()
        End Function

        Protected Overrides Function BaseImageView() As RectangleF
            If BackglassImage Is Nothing Then Return ClientRectangle
            Dim scale As Single = Math.Min(ClientSize.Width / CSng(BackglassImage.Width), ClientSize.Height / CSng(BackglassImage.Height))
            Return New RectangleF((ClientSize.Width - BackglassImage.Width * scale) / 2.0F, (ClientSize.Height - BackglassImage.Height * scale) / 2.0F, BackglassImage.Width * scale, BackglassImage.Height * scale)
        End Function
        Private Function ToView(p As PointF) As PointF
            Dim r = ViewRect() : Return New PointF(r.Left + p.X * r.Width / BackglassImage.Width, r.Top + p.Y * r.Height / BackglassImage.Height)
        End Function
        Private Function ToImage(p As PointF) As PointF
            Dim r = ViewRect()
            Return ClampCenter(New PointF((p.X-r.Left)*BackglassImage.Width/r.Width,
                                          (p.Y-r.Top)*BackglassImage.Height/r.Height))
        End Function
        Public Property FirstCenter As PointF
            Get
                Return _firstCenter
            End Get
            Set(value As PointF)
                _firstCenter = ClampCenter(value)
            End Set
        End Property
        Public Property LastCenter As PointF
            Get
                Return _lastCenter
            End Get
            Set(value As PointF)
                _lastCenter = ClampCenter(value)
            End Set
        End Property
        Private Function ClampCenter(value As PointF) As PointF
            If BackglassImage Is Nothing Then Return value

            Dim halfWidth As Single = Math.Min(BackglassImage.Width / 2.0F, Math.Max(0.0F, SnippetSize.Width / 2.0F))
            Dim halfHeight As Single = Math.Min(BackglassImage.Height / 2.0F, Math.Max(0.0F, SnippetSize.Height / 2.0F))
            Return New PointF(Math.Max(halfWidth, Math.Min(BackglassImage.Width - halfWidth, value.X)),
                              Math.Max(halfHeight, Math.Min(BackglassImage.Height - halfHeight, value.Y)))
        End Function
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e) : If BackglassImage Is Nothing Then Return
            Dim r=ViewRect() : e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic : e.Graphics.DrawImage(BackglassImage,r)
            If SlotCount < 2 Then Return
            For i As Integer=0 To SlotCount-1
                Dim t As Single=i/CSng(SlotCount-1), c As New PointF(FirstCenter.X+(LastCenter.X-FirstCenter.X)*t,FirstCenter.Y+(LastCenter.Y-FirstCenter.Y)*t), v=ToView(c)
                Dim scale As Single=r.Width/BackglassImage.Width, w As Single=Math.Max(8,SnippetSize.Width*scale), h As Single=Math.Max(8,SnippetSize.Height*scale)
                Dim image As Image = SnippetImage
                If BallImages IsNot Nothing AndAlso i < BallImages.Count AndAlso BallImages(i) IsNot Nothing Then image = BallImages(i)
                If image IsNot Nothing Then e.Graphics.DrawImage(image,v.X-w/2,v.Y-h/2,w,h)
                Using pen As New Pen(If(i=0,Color.Lime,If(i=SlotCount-1,Color.DeepSkyBlue,Color.FromArgb(170,Color.White))),If(i=0 OrElse i=SlotCount-1,3,1))
                    e.Graphics.DrawEllipse(pen,v.X-w/2,v.Y-h/2,w,h)
                End Using
                If i = SelectedSlotIndex Then
                    Using selectedPen As New Pen(Color.Gold, 3)
                        e.Graphics.DrawEllipse(selectedPen, v.X-w/2-4, v.Y-h/2-4, w+8, h+8)
                    End Using
                End If
                If i=0 OrElse i=SlotCount-1 Then e.Graphics.DrawString(If(i=0,"FIRST","LAST"),Font,If(i=0,Brushes.Lime,Brushes.DeepSkyBlue),v.X-w/2,v.Y-h/2-16)
            Next
        End Sub
        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            If BeginNavigation(e) Then Return
            Dim nearestIndex As Integer = -1
            Dim nearestDistance As Double = Double.MaxValue
            For index As Integer = 0 To SlotCount - 1
                Dim t As Single = If(SlotCount <= 1, 0.0F, index / CSng(SlotCount - 1))
                Dim center As PointF = ToView(New PointF(FirstCenter.X + (LastCenter.X - FirstCenter.X) * t,
                                                        FirstCenter.Y + (LastCenter.Y - FirstCenter.Y) * t))
                Dim distanceToSlot As Double = Distance(e.Location, center)
                If distanceToSlot < nearestDistance Then
                    nearestDistance = distanceToSlot
                    nearestIndex = index
                End If
            Next
            If nearestIndex >= 0 AndAlso nearestDistance <= 40 Then
                SelectedSlotIndex = nearestIndex
                RaiseEvent SelectedSlotChanged(Me, EventArgs.Empty)
                Invalidate()
            End If
            Dim a=ToView(FirstCenter), b=ToView(LastCenter)
            If nearestIndex = 0 AndAlso Distance(e.Location,a)<40 Then
                dragIndex=0
            ElseIf nearestIndex = SlotCount - 1 AndAlso Distance(e.Location,b)<40 Then
                dragIndex=1
            End If
        End Sub
        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            If MoveNavigation(e) Then Return
            If dragIndex<0 Then Return
            If dragIndex=0 Then FirstCenter=ToImage(e.Location) Else LastCenter=ToImage(e.Location)
            Invalidate()
        End Sub
        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            If EndNavigation() Then Return
            dragIndex=-1
        End Sub
        Private Function Distance(a As PointF,b As PointF) As Double
            Return Math.Sqrt((a.X-b.X)^2+(a.Y-b.Y)^2)
        End Function
    End Class
End Class
