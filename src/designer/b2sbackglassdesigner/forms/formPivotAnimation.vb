Imports System.Drawing.Drawing2D
Imports System.Diagnostics
Imports System.Windows.Forms

Public Class formPivotAnimation
    Inherits B2SThemedForm

    Private ReadOnly snippet As Illumination.BulbInfo
    Private ReadOnly canvas As New PivotCanvas()
    Private ReadOnly enabledBox As New CheckBox With {.Text = "Enable pivot animation", .AutoSize = True}
    Private ReadOnly automaticBox As New CheckBox With {.Text = "Automatic Pivot Point: swing while trigger is on", .AutoSize = True}
    Private ReadOnly downAngle As New NumericUpDown()
    Private ReadOnly upAngle As New NumericUpDown()
    Private ReadOnly duration As New NumericUpDown()
    Private ReadOnly randomStrength As New NumericUpDown()
    Private ReadOnly triggerType As New ComboBox()
    Private ReadOnly triggerID As New NumericUpDown()
    Private ReadOnly downTrigger As New TextBox()
    Private ReadOnly upTrigger As New TextBox()
    Private ReadOnly automaticPreview As New Button With {.Text = "Automatic Preview", .Width = 310, .Height = 31}
    Private ReadOnly previewTimer As New Timer With {.Interval = 15}
    Private ReadOnly previewClock As New Stopwatch()
    Private previewStartAngle As Single
    Private previewTargetAngle As Single
    Private previewRunning As Boolean

    Public Sub New(ByVal source As Illumination.BulbInfo, ByVal backglassImage As Image)
        snippet = source
        Text = "Pivot Animation — " & If(String.IsNullOrWhiteSpace(source.Name), "Snippet", source.Name)
        StartPosition = FormStartPosition.CenterParent : Size = New Size(920, 650) : MinimumSize = New Size(800, 560)
        enabledBox.Checked = source.SnippitInfo.PivotAnimationEnabled
        automaticBox.Checked = source.SnippitInfo.PivotAutomaticOscillation
        SetupNumber(downAngle, -360D, 360D, source.SnippitInfo.PivotDownAngle, 1)
        SetupNumber(upAngle, -360D, 360D, source.SnippitInfo.PivotUpAngle, 1)
        SetupNumber(duration, 10D, 5000D, source.SnippitInfo.PivotDuration, 0)
        SetupNumber(randomStrength, 0D, 100D, source.SnippitInfo.PivotRandomStrength, 1)
        triggerType.DropDownStyle = ComboBoxStyle.DropDownList : triggerType.Dock = DockStyle.Fill
        triggerType.Items.AddRange(New Object() {"Named commands (advanced)", "ROM solenoid", "ROM lamp", "B2S ID", "Automatic when backglass starts"})
        Dim detectedNamedPair As Boolean = (Not source.SnippitInfo.PivotAnimationEnabled AndAlso source.Name.EndsWith("_down", StringComparison.OrdinalIgnoreCase))
        triggerType.SelectedIndex = If(detectedNamedPair, 0, Math.Max(0, Math.Min(4, source.SnippitInfo.PivotTriggerType)))
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
        AddHandler previewDown.Click, Sub()
                                          StopAutomaticPreview()
                                          canvas.PreviewAngle = CSng(downAngle.Value)
                                      End Sub
        AddHandler previewUp.Click, Sub()
                                            StopAutomaticPreview()
                                            If Not automaticBox.Checked Then upAngle.Value = CDec(DirectedUpAngle())
                                            canvas.PreviewAngle = CSng(upAngle.Value)
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
        AddHandler save.Click, AddressOf SaveValues
        Controls.Add(canvas) : Controls.Add(sidebar) : canvas.Dock = DockStyle.Fill
        AcceptButton = save : CancelButton = cancel
        TriggerTypeChanged(Nothing, EventArgs.Empty)
    End Sub

    Private Sub CanvasPointEditStarted(ByVal sender As Object, ByVal e As MouseEventArgs)
        If e.Button <> MouseButtons.Left Then Return
        StopAutomaticPreview()
        canvas.PreviewAngle = 0.0F
    End Sub

    Private Sub ToggleAutomaticPreview(ByVal sender As Object, ByVal e As EventArgs)
        If previewRunning Then
            StopAutomaticPreview()
            Return
        End If
        If Not automaticBox.Checked Then Return
        If downAngle.Value = upAngle.Value Then
            MessageBox.Show(Me, "Choose two different angles to preview the automatic swing.", "Pivot Animation", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        previewRunning = True
        automaticPreview.Text = "Stop Automatic Preview"
        canvas.PreviewAngle = CSng(downAngle.Value)
        StartPreviewLeg(CSng(downAngle.Value), CSng(upAngle.Value))
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
        canvas.PreviewAngle = previewStartAngle + (previewTargetAngle - previewStartAngle) * eased
        If progress >= 1.0F Then
            canvas.PreviewAngle = previewTargetAngle
            StartPreviewLeg(previewTargetAngle, If(Math.Abs(previewTargetAngle - CSng(upAngle.Value)) < 0.01F,
                                                    CSng(downAngle.Value), CSng(upAngle.Value)))
        End If
    End Sub

    Private Sub PreviewSettingsChanged(ByVal sender As Object, ByVal e As EventArgs)
        If Not previewRunning Then Return
        previewTimer.Stop()
        canvas.PreviewAngle = CSng(downAngle.Value)
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
        automaticPreview.Text = "Automatic Preview"
        canvas.PreviewAngle = 0.0F
    End Sub

    Protected Overrides Sub OnFormClosed(ByVal e As FormClosedEventArgs)
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
        If enabledBox.Checked AndAlso triggerType.SelectedIndex = 0 AndAlso (String.IsNullOrWhiteSpace(upTrigger.Text) OrElse String.IsNullOrWhiteSpace(downTrigger.Text)) Then
            MessageBox.Show(Me, "Enter both the Up and Down command names.", "Pivot Animation", MessageBoxButtons.OK, MessageBoxIcon.Information) : DialogResult = DialogResult.None : Return
        End If
        If enabledBox.Checked AndAlso triggerType.SelectedIndex > 0 AndAlso triggerType.SelectedIndex < 4 AndAlso triggerID.Value <= 0 Then
            MessageBox.Show(Me, "Choose the trigger ID.", "Pivot Animation", MessageBoxButtons.OK, MessageBoxIcon.Information) : DialogResult = DialogResult.None : Return
        End If
        If enabledBox.Checked AndAlso triggerType.SelectedIndex = 4 AndAlso Not automaticBox.Checked Then
            MessageBox.Show(Me, "Turn on Automatic Pivot Point for a swing that starts with the backglass.", "Pivot Animation", MessageBoxButtons.OK, MessageBoxIcon.Information) : DialogResult = DialogResult.None : Return
        End If
        If enabledBox.Checked AndAlso automaticBox.Checked AndAlso triggerType.SelectedIndex = 0 Then
            MessageBox.Show(Me, "Automatic Pivot Point needs a ROM solenoid, ROM lamp, or B2S ID with an on/off state.", "Pivot Animation", MessageBoxButtons.OK, MessageBoxIcon.Information) : DialogResult = DialogResult.None : Return
        End If
        If enabledBox.Checked AndAlso automaticBox.Checked AndAlso Math.Abs(CSng(upAngle.Value - downAngle.Value)) < 0.1F Then
            MessageBox.Show(Me, "Choose two different angles for the automatic swing.", "Pivot Animation", MessageBoxButtons.OK, MessageBoxIcon.Information) : DialogResult = DialogResult.None : Return
        End If
        If Not automaticBox.Checked Then upAngle.Value = CDec(DirectedUpAngle())
        With snippet.SnippitInfo
            .PivotAnimationEnabled = enabledBox.Checked : .PivotX = canvas.Pivot.X : .PivotY = canvas.Pivot.Y
            .PivotTipX = canvas.Tip.X : .PivotTipY = canvas.Tip.Y
            .PivotDownAngle = CSng(downAngle.Value) : .PivotUpAngle = CSng(upAngle.Value) : .PivotDuration = CInt(duration.Value)
            .PivotAutomaticOscillation = automaticBox.Checked
            .PivotRandomStrength = CSng(randomStrength.Value)
            .PivotTriggerType = triggerType.SelectedIndex : .PivotTriggerID = CInt(triggerID.Value)
            .PivotDownTrigger = downTrigger.Text.Trim() : .PivotUpTrigger = upTrigger.Text.Trim()
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

    Private Sub TriggerTypeChanged(ByVal sender As Object, ByVal e As EventArgs)
        Dim named As Boolean = (triggerType.SelectedIndex = 0)
        If triggerType.SelectedIndex = 4 Then automaticBox.Checked = True
        triggerID.Enabled = triggerType.SelectedIndex > 0 AndAlso triggerType.SelectedIndex < 4
        downTrigger.Enabled = named : upTrigger.Enabled = named
    End Sub

    Private Class PivotCanvas
        Inherits Control
        Public Enum PointMode
            Hinge
            Tip
        End Enum
        Public BackglassImage As Image
        Public SnippetBounds As RectangleF
        Public SourceImage As Image
        Private pivotValue As New PointF(0.5F, 0.5F), tipValue As New PointF(0.9F, 0.5F), angleValue As Single
        Private editPointValue As PointMode = PointMode.Hinge
        Private draggingPoint As Nullable(Of PointMode) = Nothing
        Private dragOffset As PointF = PointF.Empty
        Private zoom As Single = 1.0F
        Private pan As PointF = PointF.Empty
        Private panning As Boolean
        Private lastPanPoint As Point
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
            If hingeDistance <= markerRadiusSquared AndAlso hingeDistance <= referenceDistance Then
                draggingPoint = PointMode.Hinge
                dragOffset = New PointF(hinge.X - e.X, hinge.Y - e.Y)
            ElseIf referenceDistance <= markerRadiusSquared Then
                draggingPoint = PointMode.Tip
                dragOffset = New PointF(reference.X - e.X, reference.Y - e.Y)
            ElseIf editPointValue = PointMode.Hinge OrElse r.Contains(e.Location) Then
                draggingPoint = editPointValue
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
            draggingPoint = Nothing
            Capture = False
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
            If draggingPoint.Value = PointMode.Hinge Then Pivot = point Else Tip = point
        End Sub
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e) : If SourceImage Is Nothing Then Return
            Dim r = ImageRect(), px = r.X + pivotValue.X * r.Width, py = r.Y + pivotValue.Y * r.Height
            Dim tx = r.X + tipValue.X * r.Width, ty = r.Y + tipValue.Y * r.Height
            If BackglassImage IsNot Nothing Then e.Graphics.DrawImage(BackglassImage, BackglassRect())
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality : e.Graphics.TranslateTransform(px, py) : e.Graphics.RotateTransform(angleValue) : e.Graphics.TranslateTransform(-px, -py)
            e.Graphics.DrawImage(SourceImage, r) : e.Graphics.ResetTransform()
            Using p As New Pen(Color.Cyan, 2) : e.Graphics.DrawEllipse(p, px - 8, py - 8, 16, 16) : e.Graphics.DrawLine(p, px - 12, py, px + 12, py) : e.Graphics.DrawLine(p, px, py - 12, px, py + 12) : End Using
            Using p As New Pen(Color.Lime, 2) : e.Graphics.DrawLine(p, px, py, tx, ty) : e.Graphics.DrawEllipse(p, tx - 7, ty - 7, 14, 14) : End Using
            e.Graphics.DrawString(If(editPointValue = PointMode.Hinge, "Click to place hinge; drag markers. Wheel zooms, right-drag pans.", "Click to place reference; drag markers. Wheel zooms, right-drag pans."), Font, Brushes.White, 10, 10)
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
