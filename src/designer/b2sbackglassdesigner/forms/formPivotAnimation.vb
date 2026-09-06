Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Public Class formPivotAnimation
    Inherits B2SThemedForm

    Private ReadOnly snippet As Illumination.BulbInfo
    Private ReadOnly canvas As New PivotCanvas()
    Private ReadOnly enabledBox As New CheckBox With {.Text = "Enable pivot animation", .AutoSize = True}
    Private ReadOnly downAngle As New NumericUpDown()
    Private ReadOnly upAngle As New NumericUpDown()
    Private ReadOnly duration As New NumericUpDown()
    Private ReadOnly triggerType As New ComboBox()
    Private ReadOnly triggerID As New NumericUpDown()
    Private ReadOnly downTrigger As New TextBox()
    Private ReadOnly upTrigger As New TextBox()

    Public Sub New(ByVal source As Illumination.BulbInfo)
        snippet = source
        Text = "Pivot Animation — " & If(String.IsNullOrWhiteSpace(source.Name), "Snippet", source.Name)
        StartPosition = FormStartPosition.CenterParent : Size = New Size(920, 650) : MinimumSize = New Size(800, 560)
        enabledBox.Checked = source.SnippitInfo.PivotAnimationEnabled
        SetupNumber(downAngle, -360D, 360D, source.SnippitInfo.PivotDownAngle, 1)
        SetupNumber(upAngle, -360D, 360D, source.SnippitInfo.PivotUpAngle, 1)
        SetupNumber(duration, 10D, 5000D, source.SnippitInfo.PivotDuration, 0)
        triggerType.DropDownStyle = ComboBoxStyle.DropDownList : triggerType.Dock = DockStyle.Fill
        triggerType.Items.AddRange(New Object() {"Named commands (advanced)", "ROM solenoid", "ROM lamp", "B2S ID"})
        Dim detectedNamedPair As Boolean = (Not source.SnippitInfo.PivotAnimationEnabled AndAlso source.Name.EndsWith("_down", StringComparison.OrdinalIgnoreCase))
        triggerType.SelectedIndex = If(detectedNamedPair, 0, Math.Max(0, Math.Min(3, source.SnippitInfo.PivotTriggerType)))
        SetupNumber(triggerID, 0D, 255D, source.SnippitInfo.PivotTriggerID, 0)
        downTrigger.Text = If(detectedNamedPair, source.Name, source.SnippitInfo.PivotDownTrigger)
        upTrigger.Text = If(detectedNamedPair, source.Name.Substring(0, source.Name.Length - 5) & "_up", source.SnippitInfo.PivotUpTrigger)
        downTrigger.MaxLength = 64 : upTrigger.MaxLength = 64
        canvas.SourceImage = source.Image : canvas.Pivot = New PointF(source.SnippitInfo.PivotX, source.SnippitInfo.PivotY)
        canvas.Tip = New PointF(source.SnippitInfo.PivotTipX, source.SnippitInfo.PivotTipY)

        Dim sidebar As New Panel With {.Dock = DockStyle.Right, .Width = 350,
                                      .BackColor = Color.FromArgb(22, 25, 38)}
        Dim fields As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(10),
                                                .FlowDirection = FlowDirection.TopDown, .WrapContents = False,
                                                .AutoScroll = True, .BackColor = Color.FromArgb(22, 25, 38)}
        enabledBox.ForeColor = Color.White : enabledBox.AutoSize = False : enabledBox.Width = 310 : enabledBox.Height = 26
        fields.Controls.Add(SectionHeader("ANIMATION"))
        fields.Controls.Add(enabledBox)
        fields.Controls.Add(FieldRow("Down angle", downAngle))
        fields.Controls.Add(FieldRow("Up angle", upAngle))
        fields.Controls.Add(FieldRow("Move time (ms)", duration))
        fields.Controls.Add(SectionHeader("PIVOT POINTS AND PREVIEW"))
        Dim setHinge As New Button With {.Text = "Set Hinge Point", .Width = 310, .Height = 31}
        Dim setTip As New Button With {.Text = "Set Flipper Tip", .Width = 310, .Height = 31}
        AddHandler setHinge.Click, Sub() canvas.EditPoint = PivotCanvas.PointMode.Hinge
        AddHandler setTip.Click, Sub() canvas.EditPoint = PivotCanvas.PointMode.Tip
        fields.Controls.Add(setHinge) : fields.Controls.Add(setTip)
        Dim previewDown As New Button With {.Text = "Preview Down Position", .Width = 310, .Height = 31}
        Dim previewUp As New Button With {.Text = "Preview Up Position", .Width = 310, .Height = 31}
        AddHandler previewDown.Click, Sub() canvas.PreviewAngle = CSng(downAngle.Value)
        AddHandler previewUp.Click, Sub()
                                            upAngle.Value = CDec(DirectedUpAngle())
                                            canvas.PreviewAngle = CSng(upAngle.Value)
                                        End Sub
        fields.Controls.Add(previewDown) : fields.Controls.Add(previewUp)
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
        If enabledBox.Checked AndAlso triggerType.SelectedIndex > 0 AndAlso triggerID.Value <= 0 Then
            MessageBox.Show(Me, "Choose the trigger ID.", "Pivot Animation", MessageBoxButtons.OK, MessageBoxIcon.Information) : DialogResult = DialogResult.None : Return
        End If
        upAngle.Value = CDec(DirectedUpAngle())
        With snippet.SnippitInfo
            .PivotAnimationEnabled = enabledBox.Checked : .PivotX = canvas.Pivot.X : .PivotY = canvas.Pivot.Y
            .PivotTipX = canvas.Tip.X : .PivotTipY = canvas.Tip.Y
            .PivotDownAngle = CSng(downAngle.Value) : .PivotUpAngle = CSng(upAngle.Value) : .PivotDuration = CInt(duration.Value)
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
        triggerID.Enabled = Not named : downTrigger.Enabled = named : upTrigger.Enabled = named
    End Sub

    Private Class PivotCanvas
        Inherits Control
        Public Enum PointMode
            Hinge
            Tip
        End Enum
        Public SourceImage As Image
        Private pivotValue As New PointF(0.5F, 0.5F), tipValue As New PointF(0.9F, 0.5F), angleValue As Single
        Private editPointValue As PointMode = PointMode.Hinge
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
        End Sub
        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e) : Dim r = ImageRect() : If r.Width <= 0 OrElse Not r.Contains(e.Location) Then Return
            Dim point As New PointF(CSng((e.X - r.X) / r.Width), CSng((e.Y - r.Y) / r.Height))
            If editPointValue = PointMode.Hinge Then Pivot = point Else Tip = point
        End Sub
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e) : If SourceImage Is Nothing Then Return
            Dim r = ImageRect(), px = r.X + pivotValue.X * r.Width, py = r.Y + pivotValue.Y * r.Height
            Dim tx = r.X + tipValue.X * r.Width, ty = r.Y + tipValue.Y * r.Height
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality : e.Graphics.TranslateTransform(px, py) : e.Graphics.RotateTransform(angleValue) : e.Graphics.TranslateTransform(-px, -py)
            e.Graphics.DrawImage(SourceImage, r) : e.Graphics.ResetTransform()
            Using p As New Pen(Color.Cyan, 2) : e.Graphics.DrawEllipse(p, px - 8, py - 8, 16, 16) : e.Graphics.DrawLine(p, px - 12, py, px + 12, py) : e.Graphics.DrawLine(p, px, py - 12, px, py + 12) : End Using
            Using p As New Pen(Color.Lime, 2) : e.Graphics.DrawLine(p, px, py, tx, ty) : e.Graphics.DrawEllipse(p, tx - 7, ty - 7, 14, 14) : End Using
            e.Graphics.DrawString(If(editPointValue = PointMode.Hinge, "Click the hinge point", "Click the moving flipper tip"), Font, Brushes.White, 10, 10)
        End Sub
        Private Function ImageRect() As RectangleF
            If SourceImage Is Nothing Then Return RectangleF.Empty
            Dim scale = Math.Min((ClientSize.Width - 30.0F) / SourceImage.Width, (ClientSize.Height - 30.0F) / SourceImage.Height)
            Dim w = SourceImage.Width * scale, h = SourceImage.Height * scale
            Return New RectangleF((ClientSize.Width - w) / 2, (ClientSize.Height - h) / 2, w, h)
        End Function
    End Class
End Class
