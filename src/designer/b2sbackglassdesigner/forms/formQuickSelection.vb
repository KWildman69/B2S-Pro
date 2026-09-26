Imports System
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Public Class formQuickSelection
    Inherits B2SThemedForm

    Private ReadOnly bulb As Illumination.BulbInfo
    Private ReadOnly sourceImage As Bitmap
    Private mask As Bitmap
    Private ReadOnly originalMask As String
    Private ReadOnly originalInFrontOfGlobalMask As Boolean
    Private ReadOnly preview As PictureBox
    Private ReadOnly toolRectangle As RadioButton
    Private ReadOnly toolLasso As RadioButton
    Private ReadOnly toolBrush As RadioButton
    Private ReadOnly toolAuto As RadioButton
    Private ReadOnly brushSize As TrackBar
    Private ReadOnly brushHardness As TrackBar
    Private ReadOnly zoomSlider As TrackBar
    Private ReadOnly brushSizeNumber As NumericUpDown
    Private ReadOnly brushHardnessNumber As NumericUpDown
    Private ReadOnly zoomNumber As NumericUpDown
    Private ReadOnly zoomValueLabel As Label
    Private ReadOnly undoMasks As New Collections.Generic.Stack(Of Bitmap)()
    Private ReadOnly redoMasks As New Collections.Generic.Stack(Of Bitmap)()
    Private lastBrushPoint As Point
    Private brushLineAnchor As Point
    Private brushLineAnchorValid As Boolean = False
    Private rectangleStartPoint As Point
    Private rectangleCurrentPoint As Point
    Private activeEllipse As Rectangle = Rectangle.Empty
    Private ellipseDragMode As Integer = 0 ' 0=new, 1=move, 2=NW, 3=NE, 4=SE, 5=SW
    Private ellipseDragAnchor As Point
    Private ellipseOriginalBounds As Rectangle
    Private Const EllipseHandleSize As Integer = 10
    Private Const AutoMaskTolerance As Integer = 28
    Private Const MagneticEdgeStrength As Integer = 28
    Private Const BrushOpacityPercent As Integer = 100
    Private isDragging As Boolean = False
    Private ReadOnly lassoPoints As New Collections.Generic.List(Of Point)()
    Private Const LassoSampleDistance As Integer = 3
    Private Const MagneticSearchRadius As Integer = 10
    Private brushCursorLocation As Point
    Private brushCursorVisible As Boolean = False
    Private zoomPercent As Integer = 100
    Private panOffset As PointF = PointF.Empty
    Private isPanning As Boolean = False
    Private panStartMouse As Point
    Private panStartOffset As PointF
    Private spaceHeld As Boolean = False
    Private Const PanEdgeMargin As Single = 80.0F

    Public ReadOnly Property SelectedBounds As Rectangle
        Get
            If mask Is Nothing Then Return Rectangle.Empty

            Dim left As Integer = mask.Width
            Dim top As Integer = mask.Height
            Dim right As Integer = -1
            Dim bottom As Integer = -1

            Dim bounds As New Rectangle(0, 0, mask.Width, mask.Height)
            Dim data As Imaging.BitmapData = mask.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
            Try
                Dim stride As Integer = Math.Abs(data.Stride)
                Dim pixels(stride * mask.Height - 1) As Byte
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length)
                For y As Integer = 0 To mask.Height - 1
                    Dim row As Integer = y * stride
                    For x As Integer = 0 To mask.Width - 1
                        If pixels(row + x * 4 + 3) > 0 Then
                            If x < left Then left = x
                            If y < top Then top = y
                            If x > right Then right = x
                            If y > bottom Then bottom = y
                        End If
                    Next
                Next
            Finally
                mask.UnlockBits(data)
            End Try

            If right < left OrElse bottom < top Then Return Rectangle.Empty
            Return Rectangle.FromLTRB(left, top, right + 1, bottom + 1)
        End Get
    End Property

    Public Sub New(ByVal selectedBulb As Illumination.BulbInfo, ByVal background As Image)
        bulb = selectedBulb
        originalMask = bulb.SelectionMaskData
        originalInFrontOfGlobalMask = bulb.InFrontOfGlobalMask
        sourceImage = New Bitmap(background)
        mask = DecodeMask(bulb.SelectionMaskData, sourceImage.Size)

        WindowStateManager.Attach(Me)
        Text = If(bulb.IlluMode = Illumination.eIlluMode.Flasher,
                  "Flasher Mask — Select Artwork Area",
                  "Quick Selection Mask")
        StartPosition = FormStartPosition.CenterParent
        ClientSize = New Size(940, 680)
        MinimumSize = New Size(940, 540)
        KeyPreview = True

        Dim tools As New TableLayoutPanel With {.Dock = DockStyle.Top, .Height = 170, .Padding = New Padding(6),
                                                .BackColor = SystemColors.Control, .ColumnCount = 2, .RowCount = 3}
        tools.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 58.0F))
        tools.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 42.0F))
        tools.RowStyles.Add(New RowStyle(SizeType.Absolute, 64.0F))
        tools.RowStyles.Add(New RowStyle(SizeType.Absolute, 64.0F))
        tools.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        ' Selection tools are grouped together so the workflow reads left-to-right.
        Dim selectionGroup As New GroupBox With {.Text = "Selection Tools", .Dock = DockStyle.Fill, .Margin = New Padding(3)}
        Dim selectionRow As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight,
                                                      .WrapContents = False, .Padding = New Padding(8, 8, 0, 0)}

        toolRectangle = New RadioButton With {.Text = "Circular Marquee", .Width = 125}
        toolLasso = New RadioButton With {.Text = "Lasso", .Width = 66}
        toolBrush = New RadioButton With {.Text = "Brush", .Width = 70, .Checked = True}
        toolAuto = New RadioButton With {.Text = "Auto Mask", .Width = 104}
        selectionRow.Controls.AddRange(New Control() {toolRectangle, toolLasso, toolBrush, toolAuto})
        selectionGroup.Controls.Add(selectionRow)
        tools.Controls.Add(selectionGroup, 0, 0)

        ' View controls stay separate from mask-editing controls.
        Dim viewGroup As New GroupBox With {.Text = "View", .Dock = DockStyle.Fill, .Margin = New Padding(3)}
        Dim viewRow As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight,
                                                 .WrapContents = False, .Padding = New Padding(8, 3, 0, 0)}
        viewRow.Controls.Add(New Label With {.Text = "Zoom", .Width = 42, .Height = 28, .TextAlign = ContentAlignment.MiddleLeft})
        zoomSlider = New TrackBar With {.Width = 135, .Height = 32, .Minimum = 25, .Maximum = 800, .Value = 100, .TickFrequency = 100, .SmallChange = 25, .LargeChange = 100}
        zoomNumber = New NumericUpDown With {.Width = 68, .Height = 24}
        zoomValueLabel = New Label With {.Text = "%", .Width = 18, .Height = 28, .TextAlign = ContentAlignment.MiddleLeft}
        TrackBarNumericLink.Bind(zoomSlider, zoomNumber, 25)
        AddHandler zoomSlider.ValueChanged, AddressOf ZoomSliderChanged
        viewRow.Controls.Add(zoomSlider) : viewRow.Controls.Add(zoomNumber) : viewRow.Controls.Add(zoomValueLabel)
        viewGroup.Controls.Add(viewRow)
        tools.Controls.Add(viewGroup, 1, 0)

        ' Brush controls are shown as one compact settings group.
        Dim brushGroup As New GroupBox With {.Text = "Brush Settings", .Dock = DockStyle.Fill, .Margin = New Padding(3)}
        Dim brushRow As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight,
                                                  .WrapContents = False, .Padding = New Padding(8, 3, 0, 0)}
        brushRow.Controls.Add(New Label With {.Text = "Size", .Width = 36, .Height = 28, .TextAlign = ContentAlignment.MiddleLeft})
        brushSize = New TrackBar With {.Width = 105, .Height = 32, .Minimum = 1, .Maximum = 150, .Value = 35, .TickFrequency = 25}
        brushSizeNumber = New NumericUpDown With {.Width = 55, .Height = 24}
        TrackBarNumericLink.Bind(brushSize, brushSizeNumber)
        brushRow.Controls.Add(brushSize)
        brushRow.Controls.Add(brushSizeNumber)
        brushRow.Controls.Add(New Label With {.Text = "Hardness", .Width = 65, .Height = 28, .TextAlign = ContentAlignment.MiddleLeft})
        brushHardness = New TrackBar With {.Width = 105, .Height = 32, .Minimum = 0, .Maximum = 100, .Value = 75, .TickFrequency = 20}
        brushHardnessNumber = New NumericUpDown With {.Width = 55, .Height = 24}
        TrackBarNumericLink.Bind(brushHardness, brushHardnessNumber)
        brushRow.Controls.Add(brushHardness)
        brushRow.Controls.Add(brushHardnessNumber)
        brushGroup.Controls.Add(brushRow)
        tools.Controls.Add(brushGroup, 0, 1)

        ' History and destructive actions are intentionally isolated from tool choices.
        Dim editGroup As New GroupBox With {.Text = "Edit Selection", .Dock = DockStyle.Fill, .Margin = New Padding(3)}
        Dim editRow As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight,
                                                 .WrapContents = False, .Padding = New Padding(8, 7, 0, 0)}
        Dim undoButton As New Button With {.Text = "Undo", .Width = 62, .Height = 26}
        Dim redoButton As New Button With {.Text = "Redo", .Width = 62, .Height = 26}
        Dim invertButton As New Button With {.Text = "Invert Mask", .Width = 100, .Height = 26}
        Dim clearButton As New Button With {.Text = "Clear Mask", .Width = 88, .Height = 26}
        AddHandler undoButton.Click, AddressOf UndoMask
        AddHandler redoButton.Click, AddressOf RedoMask
        AddHandler invertButton.Click, AddressOf InvertMask
        AddHandler clearButton.Click, AddressOf ClearSelection
        editRow.Controls.AddRange(New Control() {undoButton, redoButton, invertButton, clearButton})
        editGroup.Controls.Add(editRow)
        tools.Controls.Add(editGroup, 1, 1)

        Dim helpLabel As New Label With {
            .Text = "Choose a selection tool, then refine the mask. Brush: Shift-click draws from the previous point; Alt erases. Circular Marquee: Shift makes a perfect circle. Mouse wheel zooms; middle-drag or Space+drag pans. Ctrl+Z / Ctrl+Y undo and redo.",
            .Dock = DockStyle.Fill, .Height = 30, .TextAlign = ContentAlignment.MiddleLeft, .AutoEllipsis = True}
        tools.Controls.Add(helpLabel, 0, 2)
        tools.SetColumnSpan(helpLabel, 2)
        preview = New EditorPreviewPictureBox With {.Dock = DockStyle.Fill, .SizeMode = PictureBoxSizeMode.Normal, .BackColor = Color.DimGray, .TabStop = True}
        AddHandler preview.Paint, AddressOf PreviewPaint
        AddHandler preview.MouseDown, AddressOf PreviewMouseDown
        AddHandler preview.MouseMove, AddressOf PreviewMouseMove
        AddHandler preview.MouseUp, AddressOf PreviewMouseUp
        AddHandler preview.MouseLeave, AddressOf PreviewMouseLeave
        AddHandler preview.MouseDoubleClick, AddressOf PreviewMouseDoubleClick
        AddHandler preview.MouseWheel, AddressOf PreviewMouseWheel
        AddHandler preview.MouseEnter, AddressOf PreviewMouseEnter
        AddHandler preview.Resize, AddressOf PreviewResize

        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 42, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(6)}
        Dim okButton As New Button With {.Text = "OK", .Width = 90, .DialogResult = DialogResult.OK}
        Dim cancelButton As New Button With {.Text = "Cancel", .Width = 90, .DialogResult = DialogResult.Cancel}
        buttons.Controls.Add(okButton)
        buttons.Controls.Add(cancelButton)
        Dim mainLayout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3,
                                                     .Margin = New Padding(0), .Padding = New Padding(0)}
        mainLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 170.0F))
        mainLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        mainLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        tools.Dock = DockStyle.Fill : preview.Dock = DockStyle.Fill : buttons.Dock = DockStyle.Fill
        mainLayout.Controls.Add(tools, 0, 0)
        mainLayout.Controls.Add(preview, 0, 1)
        mainLayout.Controls.Add(buttons, 0, 2)
        Controls.Add(mainLayout)
        AcceptButton = okButton
        Me.CancelButton = cancelButton
    End Sub

    Private Sub ClearSelection(ByVal sender As Object, ByVal e As EventArgs)
        PushUndo()
        activeEllipse = Rectangle.Empty
        Using g As Graphics = Graphics.FromImage(mask)
            g.Clear(Color.Transparent)
        End Using
        preview.Invalidate()
    End Sub

    Private Sub InvertMask(ByVal sender As Object, ByVal e As EventArgs)
        If mask Is Nothing Then Return
        PushUndo()
        activeEllipse = Rectangle.Empty

        Dim bounds As New Rectangle(0, 0, mask.Width, mask.Height)
        Dim data As BitmapData = mask.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
        Try
            Dim stride As Integer = Math.Abs(data.Stride)
            Dim pixels(stride * mask.Height - 1) As Byte
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length)
            For y As Integer = 0 To mask.Height - 1
                Dim row As Integer = If(data.Stride >= 0, y, mask.Height - 1 - y) * stride
                For x As Integer = 0 To mask.Width - 1
                    Dim pixel As Integer = row + x * 4
                    pixels(pixel) = 255
                    pixels(pixel + 1) = 255
                    pixels(pixel + 2) = 255
                    pixels(pixel + 3) = CByte(255 - pixels(pixel + 3))
                Next
            Next
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length)
        Finally
            mask.UnlockBits(data)
        End Try
        preview.Invalidate()
    End Sub

    Private Sub PreviewMouseDown(ByVal sender As Object, ByVal e As MouseEventArgs)
        If e.Button = MouseButtons.Middle OrElse (e.Button = MouseButtons.Left AndAlso spaceHeld) Then
            isPanning = True
            panStartMouse = e.Location
            panStartOffset = panOffset
            preview.Capture = True
            preview.Cursor = Cursors.Hand
            Return
        End If
        If e.Button <> MouseButtons.Left Then Return
        Dim imagePoint As Point
        If Not TryPreviewToImagePoint(e.Location, imagePoint) Then Return

        PushUndo()
        isDragging = True
        preview.Capture = True
        If toolAuto.Checked Then
            FloodFillSelection(imagePoint)
            isDragging = False
            preview.Capture = False
        ElseIf toolBrush.Checked Then
            Dim modifiers As Keys = Control.ModifierKeys
            Dim drawFromAnchor As Boolean = brushLineAnchorValid AndAlso (modifiers And Keys.Shift) = Keys.Shift
            lastBrushPoint = imagePoint
            PaintBrushStroke(If(drawFromAnchor, brushLineAnchor, imagePoint), imagePoint,
                             (modifiers And Keys.Alt) = Keys.Alt)
            brushLineAnchor = imagePoint
            brushLineAnchorValid = True
        ElseIf toolRectangle.Checked Then
            ellipseDragMode = HitTestEllipse(imagePoint)
            ellipseDragAnchor = imagePoint
            ellipseOriginalBounds = activeEllipse
            If ellipseDragMode = 0 Then
                rectangleStartPoint = imagePoint
                rectangleCurrentPoint = imagePoint
                activeEllipse = Rectangle.Empty
            End If
        Else
            lassoPoints.Clear()
            lassoPoints.Add(GetMagneticPoint(imagePoint, Control.ModifierKeys))
        End If
        preview.Invalidate()
    End Sub

    Private Sub PreviewMouseMove(ByVal sender As Object, ByVal e As MouseEventArgs)
        If isPanning Then
            panOffset = New PointF(panStartOffset.X + (e.X - panStartMouse.X), panStartOffset.Y + (e.Y - panStartMouse.Y))
            ClampPanOffset()
            preview.Invalidate()
            Return
        End If

        brushCursorLocation = e.Location
        brushCursorVisible = toolBrush.Checked AndAlso GetImageRectangleF().Contains(e.Location)
        preview.Invalidate()

        If Not isDragging OrElse (e.Button And MouseButtons.Left) <> MouseButtons.Left Then Return

        Dim imagePoint As Point
        If Not TryPreviewToImagePoint(e.Location, imagePoint) Then Return
        Dim modifiers As Keys = Control.ModifierKeys
        If toolBrush.Checked Then
            PaintBrushStroke(lastBrushPoint, imagePoint, (modifiers And Keys.Alt) = Keys.Alt)
            lastBrushPoint = imagePoint
            brushLineAnchor = imagePoint
            brushLineAnchorValid = True
            preview.Invalidate()
            Return
        ElseIf toolAuto.Checked Then
            Return
        ElseIf toolRectangle.Checked Then
            UpdateEllipseDrag(imagePoint, (modifiers And Keys.Shift) = Keys.Shift)
            preview.Invalidate()
            Return
        End If
        Dim candidate As Point

        If (modifiers And Keys.Shift) = Keys.Shift Then
            candidate = imagePoint
        Else
            candidate = GetMagneticPoint(imagePoint, modifiers)
        End If

        If lassoPoints.Count > 0 Then
            Dim previous As Point = lassoPoints(lassoPoints.Count - 1)
            Dim dx As Integer = candidate.X - previous.X
            Dim dy As Integer = candidate.Y - previous.Y
            If dx * dx + dy * dy < LassoSampleDistance * LassoSampleDistance Then Return
        End If

        lassoPoints.Add(candidate)
        preview.Invalidate()
    End Sub

    Private Sub PreviewMouseUp(ByVal sender As Object, ByVal e As MouseEventArgs)
        If isPanning AndAlso (e.Button = MouseButtons.Middle OrElse e.Button = MouseButtons.Left) Then
            isPanning = False
            preview.Capture = False
            preview.Cursor = If(spaceHeld, Cursors.Hand, Cursors.Default)
            Return
        End If
        If e.Button <> MouseButtons.Left Then Return
        If toolBrush.Checked Then
            isDragging = False : preview.Capture = False : preview.Invalidate()
        ElseIf toolRectangle.Checked Then
            Dim imagePoint As Point
            If TryPreviewToImagePoint(e.Location, imagePoint) Then
                UpdateEllipseDrag(imagePoint, (Control.ModifierKeys And Keys.Shift) = Keys.Shift)
            End If
            ellipseDragMode = 0
            isDragging = False
            preview.Capture = False
            preview.Invalidate()
        Else
            FinishCurrentLasso(e.Location)
        End If
    End Sub

    Private Sub PreviewMouseDoubleClick(ByVal sender As Object, ByVal e As MouseEventArgs)
        If e.Button <> MouseButtons.Left Then Return
        FinishCurrentLasso(e.Location)
    End Sub

    Private Sub CommitActiveEllipseSelection()
        If activeEllipse.Width <= 0 OrElse activeEllipse.Height <= 0 Then Return
        Dim selectionRect As Rectangle = activeEllipse
        selectionRect.Intersect(New Rectangle(0, 0, mask.Width, mask.Height))
        If selectionRect.Width <= 0 OrElse selectionRect.Height <= 0 Then Return

        Using g As Graphics = Graphics.FromImage(mask)
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            Using brush As New SolidBrush(Color.White)
                g.FillEllipse(brush, selectionRect)
            End Using
        End Using
        activeEllipse = Rectangle.Empty
    End Sub

    Private Function HitTestEllipse(ByVal point As Point) As Integer
        If activeEllipse.IsEmpty Then Return 0
        Dim resizeHandles() As Point = {
            New Point(activeEllipse.Left, activeEllipse.Top),
            New Point(activeEllipse.Right, activeEllipse.Top),
            New Point(activeEllipse.Right, activeEllipse.Bottom),
            New Point(activeEllipse.Left, activeEllipse.Bottom)}
        Dim radius As Integer = Math.Max(4, CInt(EllipseHandleSize / Math.Max(0.25, GetFitScale() * zoomPercent / 100.0)))
        For i As Integer = 0 To resizeHandles.Length - 1
            If Math.Abs(point.X - resizeHandles(i).X) <= radius AndAlso Math.Abs(point.Y - resizeHandles(i).Y) <= radius Then Return i + 2
        Next
        If activeEllipse.Contains(point) Then Return 1
        Return 0
    End Function

    Private Sub UpdateEllipseDrag(ByVal imagePoint As Point, ByVal constrainCircle As Boolean)
        If ellipseDragMode = 1 Then
            Dim dx As Integer = imagePoint.X - ellipseDragAnchor.X
            Dim dy As Integer = imagePoint.Y - ellipseDragAnchor.Y
            activeEllipse = ellipseOriginalBounds
            activeEllipse.Offset(dx, dy)
            activeEllipse.X = Math.Max(0, Math.Min(mask.Width - activeEllipse.Width, activeEllipse.X))
            activeEllipse.Y = Math.Max(0, Math.Min(mask.Height - activeEllipse.Height, activeEllipse.Y))
            Return
        End If

        If ellipseDragMode >= 2 Then
            Dim left As Integer = ellipseOriginalBounds.Left
            Dim top As Integer = ellipseOriginalBounds.Top
            Dim right As Integer = ellipseOriginalBounds.Right
            Dim bottom As Integer = ellipseOriginalBounds.Bottom
            Select Case ellipseDragMode
                Case 2 : left = imagePoint.X : top = imagePoint.Y
                Case 3 : right = imagePoint.X : top = imagePoint.Y
                Case 4 : right = imagePoint.X : bottom = imagePoint.Y
                Case 5 : left = imagePoint.X : bottom = imagePoint.Y
            End Select
            activeEllipse = NormalizeEllipseBounds(left, top, right, bottom, constrainCircle)
            Return
        End If

        rectangleCurrentPoint = imagePoint
        activeEllipse = NormalizeEllipseBounds(rectangleStartPoint.X, rectangleStartPoint.Y, rectangleCurrentPoint.X, rectangleCurrentPoint.Y, constrainCircle)
    End Sub

    Private Function NormalizeEllipseBounds(ByVal x1 As Integer, ByVal y1 As Integer, ByVal x2 As Integer, ByVal y2 As Integer, ByVal constrainCircle As Boolean) As Rectangle
        Dim dx As Integer = x2 - x1
        Dim dy As Integer = y2 - y1
        If constrainCircle Then
            Dim side As Integer = Math.Max(Math.Abs(dx), Math.Abs(dy))
            dx = If(dx < 0, -side, side)
            dy = If(dy < 0, -side, side)
            x2 = x1 + dx
            y2 = y1 + dy
        End If
        Dim left As Integer = Math.Max(0, Math.Min(x1, x2))
        Dim top As Integer = Math.Max(0, Math.Min(y1, y2))
        Dim right As Integer = Math.Min(mask.Width, Math.Max(x1, x2) + 1)
        Dim bottom As Integer = Math.Min(mask.Height, Math.Max(y1, y2) + 1)
        Return Rectangle.FromLTRB(left, top, right, bottom)
    End Function

    Private Sub FinishCurrentLasso(ByVal previewLocation As Point)
        If isDragging Then
            Dim imagePoint As Point
            If TryPreviewToImagePoint(previewLocation, imagePoint) Then
                Dim finalPoint As Point = GetMagneticPoint(imagePoint, Control.ModifierKeys)
                If lassoPoints.Count = 0 OrElse lassoPoints(lassoPoints.Count - 1) <> finalPoint Then lassoPoints.Add(finalPoint)
            End If
            CommitLassoSelection()
        End If

        isDragging = False
        preview.Capture = False
        lassoPoints.Clear()
        preview.Invalidate()
    End Sub

    Private Sub PreviewMouseLeave(ByVal sender As Object, ByVal e As EventArgs)
        brushCursorVisible = False
        If Not preview.Capture Then
            isDragging = False
            lassoPoints.Clear()
            preview.Invalidate()
        End If
    End Sub

    Private Function GetMagneticPoint(ByVal rawPoint As Point, ByVal modifiers As Keys) As Point
        If (modifiers And Keys.Alt) = Keys.Alt OrElse MagneticEdgeStrength <= 0 Then Return rawPoint

        Dim radius As Integer = MagneticSearchRadius
        Dim threshold As Integer = CInt(MagneticEdgeStrength * 5.1)
        Dim bestPoint As Point = rawPoint
        Dim bestScore As Integer = threshold
        Dim minX As Integer = Math.Max(1, rawPoint.X - radius)
        Dim maxX As Integer = Math.Min(sourceImage.Width - 2, rawPoint.X + radius)
        Dim minY As Integer = Math.Max(1, rawPoint.Y - radius)
        Dim maxY As Integer = Math.Min(sourceImage.Height - 2, rawPoint.Y + radius)

        For y As Integer = minY To maxY
            For x As Integer = minX To maxX
                Dim dx As Integer = x - rawPoint.X
                Dim dy As Integer = y - rawPoint.Y
                Dim distanceSquared As Integer = dx * dx + dy * dy
                If distanceSquared <= radius * radius Then
                    Dim gradient As Integer = GetEdgeGradient(x, y)
                    Dim distancePenalty As Integer = CInt(Math.Sqrt(distanceSquared) * 5.0)
                    Dim score As Integer = gradient - distancePenalty
                    If score > bestScore Then
                        bestScore = score
                        bestPoint = New Point(x, y)
                    End If
                End If
            Next
        Next

        Return bestPoint
    End Function

    Private Function GetEdgeGradient(ByVal x As Integer, ByVal y As Integer) As Integer
        Dim leftColor As Color = sourceImage.GetPixel(x - 1, y)
        Dim rightColor As Color = sourceImage.GetPixel(x + 1, y)
        Dim topColor As Color = sourceImage.GetPixel(x, y - 1)
        Dim bottomColor As Color = sourceImage.GetPixel(x, y + 1)

        Dim gx As Integer = Math.Abs(CInt(rightColor.R) - CInt(leftColor.R)) +
                            Math.Abs(CInt(rightColor.G) - CInt(leftColor.G)) +
                            Math.Abs(CInt(rightColor.B) - CInt(leftColor.B))
        Dim gy As Integer = Math.Abs(CInt(bottomColor.R) - CInt(topColor.R)) +
                            Math.Abs(CInt(bottomColor.G) - CInt(topColor.G)) +
                            Math.Abs(CInt(bottomColor.B) - CInt(topColor.B))
        Return gx + gy
    End Function

    Private Function TryPreviewToImagePoint(ByVal previewPoint As Point, ByRef imagePoint As Point) As Boolean
        Dim imageRect As RectangleF = GetImageRectangleF()
        If imageRect.Width <= 0 OrElse imageRect.Height <= 0 OrElse Not imageRect.Contains(previewPoint) Then Return False

        Dim x As Integer = CInt((previewPoint.X - imageRect.X) * sourceImage.Width / CDbl(imageRect.Width))
        Dim y As Integer = CInt((previewPoint.Y - imageRect.Y) * sourceImage.Height / CDbl(imageRect.Height))
        x = Math.Max(0, Math.Min(sourceImage.Width - 1, x))
        y = Math.Max(0, Math.Min(sourceImage.Height - 1, y))
        imagePoint = New Point(x, y)
        Return True
    End Function


    Protected Overrides Sub OnKeyDown(ByVal e As KeyEventArgs)
        If e.KeyCode = Keys.Space Then
            spaceHeld = True
            preview.Cursor = Cursors.Hand
            e.Handled = True
            e.SuppressKeyPress = True
        End If
        If e.Control AndAlso (e.KeyCode = Keys.Add OrElse e.KeyCode = Keys.Oemplus) Then
            SetZoom(Math.Min(800, zoomPercent + 25), New Point(preview.ClientSize.Width \ 2, preview.ClientSize.Height \ 2))
            e.Handled = True
        End If
        If e.Control AndAlso (e.KeyCode = Keys.Subtract OrElse e.KeyCode = Keys.OemMinus) Then
            SetZoom(Math.Max(25, zoomPercent - 25), New Point(preview.ClientSize.Width \ 2, preview.ClientSize.Height \ 2))
            e.Handled = True
        End If
        If e.Control AndAlso e.KeyCode = Keys.D0 Then
            SetZoom(100, New Point(preview.ClientSize.Width \ 2, preview.ClientSize.Height \ 2))
            e.Handled = True
        End If
        If e.Control AndAlso e.KeyCode = Keys.Z Then UndoMask(Nothing, EventArgs.Empty) : e.Handled = True
        If e.Control AndAlso e.KeyCode = Keys.Y Then RedoMask(Nothing, EventArgs.Empty) : e.Handled = True

        If toolBrush.Checked AndAlso (e.KeyCode = Keys.OemOpenBrackets OrElse e.KeyCode = Keys.OemCloseBrackets) Then
            Dim stepSize As Integer = If(brushSize.Value < 20, 1, If(brushSize.Value < 60, 5, 10))
            If e.KeyCode = Keys.OemOpenBrackets Then
                brushSize.Value = Math.Max(brushSize.Minimum, brushSize.Value - stepSize)
            Else
                brushSize.Value = Math.Min(brushSize.Maximum, brushSize.Value + stepSize)
            End If
            preview.Invalidate()
            e.Handled = True
            e.SuppressKeyPress = True
        End If
        MyBase.OnKeyDown(e)
    End Sub

    Protected Overrides Sub OnKeyUp(ByVal e As KeyEventArgs)
        If e.KeyCode = Keys.Space Then
            spaceHeld = False
            If Not isPanning Then preview.Cursor = Cursors.Default
            e.Handled = True
        End If
        MyBase.OnKeyUp(e)
    End Sub

    Private Sub ZoomSliderChanged(ByVal sender As Object, ByVal e As EventArgs)
        SetZoom(zoomSlider.Value, New Point(preview.ClientSize.Width \ 2, preview.ClientSize.Height \ 2), False)
    End Sub

    Private Sub PreviewMouseWheel(ByVal sender As Object, ByVal e As MouseEventArgs)
        Dim stepValue As Integer = If(e.Delta > 0, 25, -25)
        If Math.Abs(e.Delta) >= 240 Then stepValue *= 2
        SetZoom(Math.Max(25, Math.Min(800, zoomPercent + stepValue)), e.Location)
    End Sub

    Private Sub PreviewMouseEnter(ByVal sender As Object, ByVal e As EventArgs)
        preview.Focus()
    End Sub

    Private Sub PreviewResize(ByVal sender As Object, ByVal e As EventArgs)
        ClampPanOffset()
        preview.Invalidate()
    End Sub

    Private Sub SetZoom(ByVal newPercent As Integer, ByVal anchor As Point, Optional ByVal updateSlider As Boolean = True)
        newPercent = Math.Max(25, Math.Min(800, newPercent))
        If newPercent = zoomPercent Then Return

        Dim oldRect As RectangleF = GetImageRectangleF()
        Dim imageX As Double = sourceImage.Width / 2.0
        Dim imageY As Double = sourceImage.Height / 2.0
        If oldRect.Width > 0 AndAlso oldRect.Height > 0 Then
            imageX = (anchor.X - oldRect.X) * sourceImage.Width / oldRect.Width
            imageY = (anchor.Y - oldRect.Y) * sourceImage.Height / oldRect.Height
        End If

        zoomPercent = newPercent
        If updateSlider AndAlso zoomSlider.Value <> zoomPercent Then zoomSlider.Value = zoomPercent

        Dim fitScale As Double = GetFitScale()
        Dim newScale As Double = fitScale * zoomPercent / 100.0
        Dim newWidth As Double = sourceImage.Width * newScale
        Dim newHeight As Double = sourceImage.Height * newScale
        panOffset = New PointF(CSng(anchor.X - preview.ClientSize.Width / 2.0 - (imageX - sourceImage.Width / 2.0) * newScale),
                               CSng(anchor.Y - preview.ClientSize.Height / 2.0 - (imageY - sourceImage.Height / 2.0) * newScale))
        ClampPanOffset()
        preview.Invalidate()
    End Sub

    Private Function GetFitScale() As Double
        If sourceImage.Width <= 0 OrElse sourceImage.Height <= 0 OrElse preview.ClientSize.Width <= 0 OrElse preview.ClientSize.Height <= 0 Then Return 1.0
        Return Math.Min(preview.ClientSize.Width / CDbl(sourceImage.Width), preview.ClientSize.Height / CDbl(sourceImage.Height))
    End Function

    Private Function GetImageRectangleF() As RectangleF
        Dim scale As Double = GetFitScale() * zoomPercent / 100.0
        Dim w As Single = CSng(sourceImage.Width * scale)
        Dim h As Single = CSng(sourceImage.Height * scale)
        Return New RectangleF(CSng((preview.ClientSize.Width - w) / 2.0 + panOffset.X),
                              CSng((preview.ClientSize.Height - h) / 2.0 + panOffset.Y), w, h)
    End Function

    Private Sub ClampPanOffset()
        Dim scale As Double = GetFitScale() * zoomPercent / 100.0
        Dim w As Double = sourceImage.Width * scale
        Dim h As Double = sourceImage.Height * scale
        ' Permit a small overscroll beyond every image edge. Without this margin,
        ' the top edge clamps exactly against the viewport and is hidden while
        ' zoomed, making selections along the upper frame impossible to inspect.
        Dim maxX As Single = If(w > preview.ClientSize.Width, CSng((w - preview.ClientSize.Width) / 2.0 + PanEdgeMargin), 0.0F)
        Dim maxY As Single = If(h > preview.ClientSize.Height, CSng((h - preview.ClientSize.Height) / 2.0 + PanEdgeMargin), 0.0F)
        If maxX = 0 Then
            panOffset.X = 0
        Else
            panOffset.X = Math.Max(-maxX, Math.Min(maxX, panOffset.X))
        End If
        If maxY = 0 Then
            panOffset.Y = 0
        Else
            panOffset.Y = Math.Max(-maxY, Math.Min(maxY, panOffset.Y))
        End If
    End Sub

    Private Sub PushUndo()
        undoMasks.Push(New Bitmap(mask))
        While undoMasks.Count > 30
            Dim temp() As Bitmap = undoMasks.ToArray()
            undoMasks.Clear()
            For i As Integer = Math.Min(29, temp.Length - 1) To 0 Step -1 : undoMasks.Push(temp(i)) : Next
        End While
        For Each b As Bitmap In redoMasks : b.Dispose() : Next
        redoMasks.Clear()
    End Sub

    Private Sub UndoMask(ByVal sender As Object, ByVal e As EventArgs)
        If undoMasks.Count = 0 Then Return
        redoMasks.Push(New Bitmap(mask))
        mask.Dispose() : mask = undoMasks.Pop()
        preview.Invalidate()
    End Sub

    Private Sub RedoMask(ByVal sender As Object, ByVal e As EventArgs)
        If redoMasks.Count = 0 Then Return
        undoMasks.Push(New Bitmap(mask))
        mask.Dispose() : mask = redoMasks.Pop()
        preview.Invalidate()
    End Sub

    Private Sub PaintBrushStroke(ByVal fromPoint As Point, ByVal toPoint As Point, ByVal altErase As Boolean)
        Dim isErase As Boolean = altErase
        Dim alpha As Integer = CInt(255.0 * BrushOpacityPercent / 100.0)
        Using g As Graphics = Graphics.FromImage(mask)
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            If isErase Then g.CompositingMode = Drawing2D.CompositingMode.SourceCopy
            If isErase OrElse brushHardness.Value >= 100 Then
                Using pen As New Pen(If(isErase, Color.FromArgb(0, 255, 255, 255), Color.FromArgb(alpha, 255, 255, 255)), brushSize.Value)
                    pen.StartCap = Drawing2D.LineCap.Round : pen.EndCap = Drawing2D.LineCap.Round
                    g.DrawLine(pen, fromPoint, toPoint)
                End Using
            Else
                ' Feather inward from the selected brush diameter.  The old soft
                ' stroke was 135% of the cursor diameter, so it necessarily
                ' painted outside the visible circle.
                Dim hardnessRatio As Single = brushHardness.Value / 100.0F
                Dim coreWidth As Single = Math.Max(1.0F, brushSize.Value * hardnessRatio)
                Const featherBands As Integer = 8
                For band As Integer = featherBands To 1 Step -1
                    Dim amount As Single = band / CSng(featherBands)
                    Dim bandWidth As Single = coreWidth + (brushSize.Value - coreWidth) * amount
                    Dim bandAlpha As Integer = Math.Max(1, CInt(alpha * (1.0F - amount) / featherBands))
                    Using featherPen As New Pen(Color.FromArgb(bandAlpha, 255, 255, 255), bandWidth)
                        featherPen.StartCap = Drawing2D.LineCap.Round : featherPen.EndCap = Drawing2D.LineCap.Round
                        g.DrawLine(featherPen, fromPoint, toPoint)
                    End Using
                Next
                Using corePen As New Pen(Color.FromArgb(alpha, 255, 255, 255), coreWidth)
                    corePen.StartCap = Drawing2D.LineCap.Round : corePen.EndCap = Drawing2D.LineCap.Round
                    g.DrawLine(corePen, fromPoint, toPoint)
                End Using
            End If
        End Using
    End Sub

    Private Sub FloodFillSelection(ByVal seed As Point)
        Dim w As Integer = sourceImage.Width, h As Integer = sourceImage.Height
        Dim visited(w * h - 1) As Boolean
        Dim q As New Collections.Generic.Queue(Of Integer)()
        Dim limit As Integer = Math.Max(4, CInt(AutoMaskTolerance * 4.42))
        Dim sourceBounds As New Rectangle(0, 0, w, h)
        Dim sourceData As Imaging.BitmapData = sourceImage.LockBits(sourceBounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
        Dim sourceStride As Integer = Math.Abs(sourceData.Stride)
        Dim sourceBytes(sourceStride * h - 1) As Byte
        Marshal.Copy(sourceData.Scan0, sourceBytes, 0, sourceBytes.Length)
        sourceImage.UnlockBits(sourceData)
        Dim targetOffset As Integer = seed.Y * sourceStride + seed.X * 4
        Dim targetB As Integer = sourceBytes(targetOffset)
        Dim targetG As Integer = sourceBytes(targetOffset + 1)
        Dim targetR As Integer = sourceBytes(targetOffset + 2)
        q.Enqueue(seed.Y * w + seed.X)
        Using region As New Bitmap(w, h, PixelFormat.Format32bppArgb)
            Dim regionData As Imaging.BitmapData = region.LockBits(sourceBounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
            Dim regionStride As Integer = Math.Abs(regionData.Stride)
            Dim regionBytes(regionStride * h - 1) As Byte
            While q.Count > 0
                Dim idx As Integer = q.Dequeue()
                If visited(idx) Then Continue While
                visited(idx) = True
                Dim x As Integer = idx Mod w
                Dim y As Integer = idx \ w
                Dim sourceOffset As Integer = y * sourceStride + x * 4
                Dim diff As Integer = Math.Abs(CInt(sourceBytes(sourceOffset + 2)) - targetR) +
                                      Math.Abs(CInt(sourceBytes(sourceOffset + 1)) - targetG) +
                                      Math.Abs(CInt(sourceBytes(sourceOffset)) - targetB)
                If diff > limit Then Continue While
                Dim regionOffset As Integer = y * regionStride + x * 4
                regionBytes(regionOffset) = 255 : regionBytes(regionOffset + 1) = 255
                regionBytes(regionOffset + 2) = 255 : regionBytes(regionOffset + 3) = 255
                If x + 1 < w Then q.Enqueue(idx + 1)
                If x > 0 Then q.Enqueue(idx - 1)
                If y + 1 < h Then q.Enqueue(idx + w)
                If y > 0 Then q.Enqueue(idx - w)
            End While
            Marshal.Copy(regionBytes, 0, regionData.Scan0, regionBytes.Length)
            region.UnlockBits(regionData)
            Using g As Graphics = Graphics.FromImage(mask)
                g.DrawImageUnscaled(region, 0, 0)
            End Using
        End Using
    End Sub

    Private Sub CommitLassoSelection()
        If lassoPoints.Count < 3 Then Return

        Using g As Graphics = Graphics.FromImage(mask)
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            Using brush As New SolidBrush(Color.White)
                g.FillPolygon(brush, lassoPoints.ToArray(), Drawing2D.FillMode.Winding)
            End Using
        End Using
    End Sub

    Private Sub PreviewPaint(ByVal sender As Object, ByVal e As PaintEventArgs)
        Dim imageRect As RectangleF = GetImageRectangleF()
        If imageRect.Width <= 0 OrElse imageRect.Height <= 0 Then Return
        e.Graphics.InterpolationMode = If(zoomPercent >= 200, Drawing2D.InterpolationMode.NearestNeighbor, Drawing2D.InterpolationMode.HighQualityBicubic)
        e.Graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.Half
        e.Graphics.DrawImage(sourceImage, imageRect)
        ' Tint the mask directly into the viewport. The previous path allocated
        ' and painted a full-resolution temporary bitmap on every mouse move.
        Using ia As New ImageAttributes()
            Dim matrix As New ColorMatrix()
            matrix.Matrix00 = 0.0F : matrix.Matrix11 = 0.65F : matrix.Matrix22 = 1.0F : matrix.Matrix33 = 0.38F
            ia.SetColorMatrix(matrix)
            e.Graphics.DrawImage(mask, Rectangle.Round(imageRect), 0, 0, mask.Width, mask.Height, GraphicsUnit.Pixel, ia)
        End Using

        If toolRectangle.Checked AndAlso Not activeEllipse.IsEmpty Then
            Dim p1 As PointF = ImageToPreviewPoint(New Point(activeEllipse.Left, activeEllipse.Top), imageRect)
            Dim p2 As PointF = ImageToPreviewPoint(New Point(activeEllipse.Right, activeEllipse.Bottom), imageRect)
            Dim rect As RectangleF = RectangleF.FromLTRB(p1.X, p1.Y, p2.X, p2.Y)
            e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            Using fillBrush As New SolidBrush(Color.FromArgb(45, Color.DeepSkyBlue))
                e.Graphics.FillEllipse(fillBrush, rect)
            End Using
            Using outlinePen As New Pen(Color.DeepSkyBlue, 2.0F)
                outlinePen.DashStyle = Drawing2D.DashStyle.Dash
                e.Graphics.DrawEllipse(outlinePen, rect)
            End Using
            Dim handleSize As Single = 8.0F
            Using handleBrush As New SolidBrush(Color.White)
                For Each hp As PointF In {New PointF(rect.Left, rect.Top), New PointF(rect.Right, rect.Top), New PointF(rect.Right, rect.Bottom), New PointF(rect.Left, rect.Bottom)}
                    e.Graphics.FillRectangle(handleBrush, hp.X - handleSize / 2.0F, hp.Y - handleSize / 2.0F, handleSize, handleSize)
                Next
            End Using
        End If

        If isDragging AndAlso lassoPoints.Count > 1 Then
            Dim displayPoints(lassoPoints.Count - 1) As PointF
            For i As Integer = 0 To lassoPoints.Count - 1
                displayPoints(i) = New PointF(
                    CSng(imageRect.X + lassoPoints(i).X * imageRect.Width / CDbl(sourceImage.Width)),
                    CSng(imageRect.Y + lassoPoints(i).Y * imageRect.Height / CDbl(sourceImage.Height)))
            Next
            Using outlinePen As New Pen(Color.DeepSkyBlue, 2.0F)
                outlinePen.DashStyle = Drawing2D.DashStyle.Dash
                e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
                e.Graphics.DrawLines(outlinePen, displayPoints)
                If displayPoints.Length > 2 Then e.Graphics.DrawLine(outlinePen, displayPoints(displayPoints.Length - 1), displayPoints(0))
            End Using

            Using anchorBrush As New SolidBrush(Color.White)
                For i As Integer = 0 To displayPoints.Length - 1 Step Math.Max(1, displayPoints.Length \ 24)
                    e.Graphics.FillEllipse(anchorBrush, displayPoints(i).X - 2.0F, displayPoints(i).Y - 2.0F, 4.0F, 4.0F)
                Next
            End Using
        End If

        If brushCursorVisible AndAlso toolBrush.Checked AndAlso imageRect.Contains(brushCursorLocation) Then
            Dim scaleX As Double = imageRect.Width / CDbl(sourceImage.Width)
            Dim scaleY As Double = imageRect.Height / CDbl(sourceImage.Height)
            Dim displayDiameter As Single = CSng(brushSize.Value * Math.Min(scaleX, scaleY))
            displayDiameter = Math.Max(2.0F, displayDiameter)
            Dim cursorRect As New RectangleF(brushCursorLocation.X - displayDiameter / 2.0F,
                                             brushCursorLocation.Y - displayDiameter / 2.0F,
                                             displayDiameter,
                                             displayDiameter)
            e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            Using darkPen As New Pen(Color.FromArgb(220, Color.Black), 3.0F)
                e.Graphics.DrawEllipse(darkPen, cursorRect)
            End Using
            Using lightPen As New Pen(Color.FromArgb(235, Color.White), 1.0F)
                e.Graphics.DrawEllipse(lightPen, cursorRect)
            End Using
        End If
    End Sub

    Private Function ImageToPreviewPoint(ByVal imagePoint As Point, ByVal imageRect As RectangleF) As PointF
        Return New PointF(CSng(imageRect.X + imagePoint.X * imageRect.Width / CDbl(Math.Max(1, sourceImage.Width))),
                          CSng(imageRect.Y + imagePoint.Y * imageRect.Height / CDbl(Math.Max(1, sourceImage.Height))))
    End Function

    Protected Overrides Sub OnFormClosing(ByVal e As FormClosingEventArgs)
        ' Quick Selection edits only the local selection mask.  Never change the
        ' light's global-mask layer choice (Behind Mask / In Front of Mask).
        ' Preserve the value that was active when this editor was opened for
        ' Lasso, Brush, Auto Mask, OK, and Cancel paths.
        bulb.InFrontOfGlobalMask = originalInFrontOfGlobalMask

        If DialogResult = DialogResult.OK Then
            If Not activeEllipse.IsEmpty Then CommitActiveEllipseSelection()
            ' A completely clear mask means "no mask". Saving an all-transparent
            ' PNG leaves a non-empty Base64 value that the renderer correctly
            ' interprets as an active mask with no selected pixels, which makes
            ' the light disappear permanently after Clear Mask.
            bulb.SelectionMaskData = If(SelectedBounds.IsEmpty,
                                        String.Empty,
                                        EncodeMask(mask))
            bulb.IsIlluminatedImageDirty = True
        Else
            bulb.SelectionMaskData = originalMask
        End If
        MyBase.OnFormClosing(e)
    End Sub

    Private Shared Function EncodeMask(ByVal bmp As Bitmap) As String
        Using ms As New MemoryStream()
            bmp.Save(ms, ImageFormat.Png)
            Return Convert.ToBase64String(ms.ToArray())
        End Using
    End Function

    Private Shared Function DecodeMask(ByVal data As String, ByVal size As Size) As Bitmap
        If Not String.IsNullOrEmpty(data) Then
            Try
                Dim bytes() As Byte = Convert.FromBase64String(data)
                Using ms As New MemoryStream(bytes)
                    Using temp As New Bitmap(ms)
                        Return New Bitmap(temp)
                    End Using
                End Using
            Catch
            End Try
        End If
        Dim ret As New Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb)
        Using g As Graphics = Graphics.FromImage(ret)
            g.Clear(Color.Transparent)
        End Using
        Return ret
    End Function

    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        If disposing Then
            If mask IsNot Nothing Then mask.Dispose()
            If sourceImage IsNot Nothing Then sourceImage.Dispose()
            For Each b As Bitmap In undoMasks : b.Dispose() : Next
            For Each b As Bitmap In redoMasks : b.Dispose() : Next
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
