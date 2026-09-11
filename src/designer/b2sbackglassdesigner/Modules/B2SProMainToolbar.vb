Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Windows.Forms

''' <summary>
''' Phase 2 renderer and layout for the real main B2S Pro toolbar.
''' It reuses the existing ToolStripItems, commands, and event handlers.
''' </summary>
Public Module B2SProMainToolbar

    Private Const ToolbarHeight As Integer = 70
    Private Const ButtonWidth As Integer = 50
    Private Const ButtonHeight As Integer = 56
    Private Const GroupCornerRadius As Integer = 8

    Public Sub Apply(strip As ToolStrip)
        If strip Is Nothing Then Return

        strip.SuspendLayout()
        Try
            strip.AutoSize = False
            strip.Height = ToolbarHeight
            strip.Padding = New Padding(4, 3, 4, 3)
            strip.GripStyle = ToolStripGripStyle.Hidden
            strip.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow
            strip.ImageScalingSize = New Size(24, 24)
            strip.BackColor = B2SProUIFoundation.Palette.ToolbarBack
            strip.ForeColor = B2SProUIFoundation.Palette.TextPrimary
            strip.Renderer = New MainToolbarRenderer()

            For Each item As ToolStripItem In strip.Items
                ConfigureItem(item)
            Next
        Finally
            strip.ResumeLayout(True)
            strip.Invalidate()
        End Try
    End Sub

    Public Sub ApplyMenu(menu As MenuStrip)
        If menu Is Nothing Then Return

        menu.SuspendLayout()
        Try
            menu.AutoSize = False
            menu.Height = 27
            menu.Padding = New Padding(4, 7, 4, 7)
            menu.BackColor = Color.FromArgb(1, 2, 4)
            menu.ForeColor = Color.FromArgb(245, 246, 250)
            menu.Font = New Font("Segoe UI Semibold", 7.5F, FontStyle.Regular)
            menu.Renderer = New MainMenuRenderer()

            For Each item As ToolStripItem In menu.Items
                item.AutoSize = True
                item.Margin = Padding.Empty
                item.Padding = New Padding(3, 0, 3, 0)
                item.ForeColor = Color.FromArgb(245, 246, 250)
                item.Font = New Font("Segoe UI Semibold", 7.5F, FontStyle.Regular)

                Dim topMenu As ToolStripMenuItem = TryCast(item, ToolStripMenuItem)
                If topMenu IsNot Nothing Then ApplyRegularDropDownSizing(topMenu.DropDownItems)
            Next
        Finally
            menu.ResumeLayout(True)
            menu.Invalidate()
        End Try
    End Sub

    Private Sub ApplyRegularDropDownSizing(items As ToolStripItemCollection)
        For Each item As ToolStripItem In items
            item.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
            item.Padding = New Padding(2, 2, 2, 2)
            item.Margin = New Padding(0, 1, 0, 1)
            Dim childMenu As ToolStripMenuItem = TryCast(item, ToolStripMenuItem)
            If childMenu IsNot Nothing AndAlso childMenu.HasDropDownItems Then ApplyRegularDropDownSizing(childMenu.DropDownItems)
        Next
    End Sub

    Public Sub ApplyStatusBar(status As StatusStrip)
        If status Is Nothing Then Return

        status.SuspendLayout()
        Try
            status.AutoSize = False
            status.Height = 34
            status.Padding = New Padding(9, 4, 9, 4)
            status.SizingGrip = False
            status.BackColor = Color.FromArgb(1, 3, 6)
            status.ForeColor = B2SProUIFoundation.Palette.TextPrimary
            status.Font = New Font("Segoe UI Semibold", 9.0F, FontStyle.Regular)
            status.Renderer = New MainStatusRenderer()

            For Each item As ToolStripItem In status.Items
                item.Margin = New Padding(2, 1, 2, 1)
                If TypeOf item Is ToolStripStatusLabel Then
                    item.BackColor = Color.Transparent
                    item.Padding = New Padding(9, 0, 9, 0)
                ElseIf TypeOf item Is ToolStripProgressBar Then
                    ' ToolStripProgressBar hosts a native ProgressBar, which throws
                    ' at startup when assigned a transparent background color.
                    item.BackColor = Color.FromArgb(1, 3, 6)
                    item.Margin = New Padding(8, 3, 8, 3)
                End If
            Next
        Finally
            status.ResumeLayout(True)
            status.Invalidate()
        End Try
    End Sub

    Private Sub ConfigureItem(item As ToolStripItem)
        If item Is Nothing Then Return

        If TypeOf item Is ToolStripButton Then
            Dim button As ToolStripButton = DirectCast(item, ToolStripButton)
            button.AutoSize = False
            button.Text = FriendlyCaption(button)
            button.Size = New Size(ToolbarButtonWidth(button.Name, button.Text), ButtonHeight)
            button.Margin = New Padding(1, 4, 1, 1)
            button.Padding = New Padding(1)
            button.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            button.TextImageRelation = TextImageRelation.ImageAboveText
            button.ImageAlign = ContentAlignment.TopCenter
            button.TextAlign = ContentAlignment.BottomCenter
            button.ImageScaling = ToolStripItemImageScaling.SizeToFit
            button.Image = CreateToolbarIcon(button.Name, ToolbarAccent(button))
            button.Font = New Font("Segoe UI Condensed", 6.5F, FontStyle.Regular)
            button.ForeColor = B2SProUIFoundation.Palette.TextPrimary
        ElseIf TypeOf item Is ToolStripSeparator Then
            item.AutoSize = False
            item.Size = New Size(6, ButtonHeight)
            item.Margin = New Padding(1, 4, 1, 1)
        ElseIf TypeOf item Is ToolStripComboBox Then
            Dim combo As ToolStripComboBox = DirectCast(item, ToolStripComboBox)
            combo.AutoSize = False
            combo.Width = If(combo.Name.Equals("tscmbZoomInPercent", StringComparison.OrdinalIgnoreCase), 64, Math.Max(combo.Width, 96))
            combo.Margin = New Padding(2, 16, 2, 13)
            combo.Font = New Font("Segoe UI", 8.0F, FontStyle.Regular)
            combo.FlatStyle = FlatStyle.Flat
            combo.BackColor = Color.FromArgb(3, 4, 6)
            combo.ForeColor = Color.White
            combo.ComboBox.FlatStyle = FlatStyle.Flat
            combo.ComboBox.BackColor = Color.FromArgb(3, 4, 6)
            combo.ComboBox.ForeColor = Color.White
        ElseIf TypeOf item Is ToolStripTextBox Then
            Dim textBox As ToolStripTextBox = DirectCast(item, ToolStripTextBox)
            textBox.AutoSize = False
            textBox.Width = Math.Max(textBox.Width, 120)
            textBox.Margin = New Padding(4, 20, 4, 16)
        ElseIf TypeOf item Is ToolStripProgressBar Then
            Dim progress As ToolStripProgressBar = DirectCast(item, ToolStripProgressBar)
            progress.AutoSize = False
            progress.Size = New Size(176, 20)
            progress.Margin = New Padding(8, 20, 8, 16)
            progress.Overflow = ToolStripItemOverflow.Never
            progress.Style = ProgressBarStyle.Continuous
            progress.BackColor = B2SProUIFoundation.Palette.ToolbarBack
        ElseIf TypeOf item Is ToolStripLabel Then
            item.Margin = New Padding(6, 17, 2, 12)
            item.Font = New Font("Segoe UI", 8.25F, FontStyle.Regular)
            item.ForeColor = B2SProUIFoundation.Palette.TextSecondary
        End If
    End Sub

    Private Function FriendlyCaption(button As ToolStripButton) As String
        Dim key As String = button.Name.ToLowerInvariant()
        If key.Contains("choosereeltype") Then Return "CHOOSE" & vbLf & "REEL OR LED" & vbLf & "TYPE"
        If key.Contains("newreel") OrElse key.Contains("addnewreel") Then Return "ADD" & vbLf & "REEL OR LED" & vbLf & "FRAME"
        If key.Contains("newbulb") Then Return "ADD" & vbLf & "LIGHT"
        If key.Contains("flasher") Then Return "ADD" & vbLf & "FLASHER"
        If key.Contains("makesnippet") Then Return "MAKE" & vbLf & "SNIPPET"
        If key.Contains("snippet") Then Return "ADD" & vbLf & "SNIPPET"
        If key.Contains("animation") Then Return "ANIMATION"
        If key.Contains("autosave") Then Return "AUTO" & vbLf & "SAVE"
        If key.Contains("createdirectb2s") Then Return "STEP 1" & vbLf & "CREATE" & vbLf & "B2SPRO FILE"
        If key.Contains("backglasspreview") Then Return "STEP 2" & vbLf & "BACKGLASS" & vbLf & "PREVIEW & TEST"
        If key.Contains("undo") Then Return "UNDO"
        If key.Contains("redo") Then Return "REDO"
        If key.Contains("zoomout") Then Return "ZOOM" & vbLf & "OUT"
        If key.Contains("zoomin") Then Return "ZOOM" & vbLf & "IN"
        If key.Contains("importbackground") Then Return "IMPORT" & vbLf & "BACKGLASS" & vbLf & "IMAGE"
        If key.Contains("backglassbrightness") Then Return "BACKGLASS" & vbLf & "BRIGHTNESS"
        If key.Contains("showilluframes") Then Return "LIGHTS"
        If key.Contains("new") Then Return "NEW"
        If key.Contains("open") Then Return "OPEN"
        If key.Contains("save") Then Return "SAVE"
        If key.Contains("scoreframes") Then Return "SHOW" & vbLf & "ILLUMINATION" & vbLf & "FRAMES"
        If key.Contains("scoring") Then Return "SHOW" & vbLf & "REEL OR LED" & vbLf & "FRAMES"
        If key.Contains("accurate") Then Return "ACCURATE" & vbLf & "LIGHT" & vbLf & "PREVIEW"
        If key.Contains("illumination") Then Return "PREVIEW"
        If key.Contains("help") Then Return "HELP"

        Dim caption As String = button.Text
        If String.IsNullOrWhiteSpace(caption) Then caption = button.ToolTipText
        caption = caption.Replace("&", "").Trim()
        If caption.Length > 10 Then caption = caption.Substring(0, 10)
        Return caption.ToUpperInvariant()
    End Function

    Private Function CreateToolbarIcon(itemName As String, accent As Color) As Image
        Const scale As Integer = 2
        Dim bitmap As New Bitmap(32 * scale, 32 * scale, System.Drawing.Imaging.PixelFormat.Format32bppPArgb)
        bitmap.SetResolution(192.0F, 192.0F)
        Using g As Graphics = Graphics.FromImage(bitmap)
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.CompositingQuality = CompositingQuality.HighQuality
            g.PixelOffsetMode = PixelOffsetMode.HighQuality
            g.ScaleTransform(scale, scale)
            Dim key As String = If(itemName, String.Empty).ToLowerInvariant()
            Using glow As New Pen(Color.FromArgb(70, accent), 5.0F), line As New Pen(accent, 2.25F)
                line.StartCap = LineCap.Round : line.EndCap = LineCap.Round : line.LineJoin = LineJoin.Round
                glow.StartCap = LineCap.Round : glow.EndCap = LineCap.Round : glow.LineJoin = LineJoin.Round
                If key.Contains("createdirectb2s") Then
                    Using pageFill As New LinearGradientBrush(New Rectangle(7, 3, 19, 26), Color.FromArgb(235, 255, 255, 255), Color.FromArgb(130, accent), LinearGradientMode.Vertical)
                        g.FillRectangle(pageFill, 7, 3, 19, 26)
                    End Using
                    g.DrawRectangle(glow, 7, 3, 19, 26) : g.DrawRectangle(line, 7, 3, 19, 26)
                    Using detailPen As New Pen(Color.FromArgb(9, 75, 43), 1.7F)
                        g.DrawLine(detailPen, 11, 9, 22, 9)
                        g.DrawLine(detailPen, 11, 14, 22, 14)
                        g.DrawLine(detailPen, 11, 19, 18, 19)
                    End Using
                    Using badge As New SolidBrush(accent)
                        g.FillEllipse(badge, 18, 18, 11, 11)
                    End Using
                    Using checkPen As New Pen(Color.White, 1.8F)
                        checkPen.StartCap = LineCap.Round : checkPen.EndCap = LineCap.Round
                        g.DrawLines(checkPen, {New PointF(20.5F, 23.5F), New PointF(23, 26), New PointF(27, 21.5F)})
                    End Using
                ElseIf key.Contains("backglasspreview") Then
                    Using screenFill As New LinearGradientBrush(New Rectangle(4, 5, 24, 20), Color.FromArgb(32, 235, 135), Color.FromArgb(7, 70, 42), LinearGradientMode.Vertical)
                        g.FillRectangle(screenFill, 4, 5, 24, 20)
                    End Using
                    g.DrawRectangle(glow, 4, 5, 24, 20) : g.DrawRectangle(line, 4, 5, 24, 20)
                    Using playBrush As New SolidBrush(Color.White)
                        g.FillPolygon(playBrush, {New PointF(12, 10), New PointF(12, 21), New PointF(22, 15.5F)})
                    End Using
                    Using standPen As New Pen(accent, 2.0F)
                        g.DrawLine(standPen, 12, 28, 20, 28)
                        g.DrawLine(standPen, 16, 25, 16, 28)
                    End Using
                ElseIf key.Contains("autosave") Then
                    g.DrawEllipse(glow, 5, 5, 22, 22) : g.DrawEllipse(line, 5, 5, 22, 22)
                    Using whitePen As New Pen(Color.White, 2.0F)
                        whitePen.StartCap = LineCap.Round : whitePen.EndCap = LineCap.Round
                        g.DrawLines(whitePen, {New PointF(10, 16), New PointF(14, 20), New PointF(23, 11)})
                    End Using
                ElseIf key.Contains("undo") OrElse key.Contains("redo") Then
                    Dim flip As Boolean = key.Contains("redo")
                    Dim pts() As PointF = If(flip,
                        {New PointF(9, 9), New PointF(20, 9), New PointF(25, 14), New PointF(25, 22)},
                        {New PointF(23, 9), New PointF(12, 9), New PointF(7, 14), New PointF(7, 22)})
                    g.DrawLines(glow, pts) : g.DrawLines(line, pts)
                    Dim arrow() As PointF = If(flip, {New PointF(9, 9), New PointF(14, 5), New PointF(14, 13)}, {New PointF(23, 9), New PointF(18, 5), New PointF(18, 13)})
                    Using brush As New SolidBrush(Color.White) : g.FillPolygon(brush, arrow) : End Using
                ElseIf key.Contains("zoom") Then
                    g.DrawEllipse(glow, 5, 4, 17, 17) : g.DrawEllipse(line, 5, 4, 17, 17)
                    g.DrawLine(glow, 19, 19, 27, 27) : g.DrawLine(line, 19, 19, 27, 27)
                    Using whitePen As New Pen(Color.White, 1.75F)
                        whitePen.StartCap = LineCap.Round : whitePen.EndCap = LineCap.Round
                        g.DrawLine(whitePen, 9, 12.5F, 18, 12.5F)
                        If key.Contains("in") Then g.DrawLine(whitePen, 13.5F, 8, 13.5F, 17)
                    End Using
                ElseIf key.Contains("backglassbrightness") Then
                    Using sunFill As New SolidBrush(Color.White)
                        g.FillEllipse(sunFill, 10, 10, 12, 12)
                    End Using
                    For ray As Integer = 0 To 7
                        Dim angle As Double = ray * Math.PI / 4.0R
                        Dim innerX As Single = 16.0F + CSng(Math.Cos(angle) * 9.0R)
                        Dim innerY As Single = 16.0F + CSng(Math.Sin(angle) * 9.0R)
                        Dim outerX As Single = 16.0F + CSng(Math.Cos(angle) * 14.0R)
                        Dim outerY As Single = 16.0F + CSng(Math.Sin(angle) * 14.0R)
                        g.DrawLine(glow, innerX, innerY, outerX, outerY)
                        g.DrawLine(line, innerX, innerY, outerX, outerY)
                    Next
                ElseIf key.Contains("importbackground") Then
                    g.DrawRectangle(glow, 4, 5, 24, 22) : g.DrawRectangle(line, 4, 5, 24, 22)
                    Using sun As New SolidBrush(Color.FromArgb(255, 218, 75)) : g.FillEllipse(sun, 20, 8, 4, 4) : End Using
                    Using brush As New SolidBrush(Color.FromArgb(220, 80, 205, 255))
                        g.FillPolygon(brush, {New PointF(6, 24), New PointF(13, 14), New PointF(18, 20), New PointF(21, 17), New PointF(27, 24)})
                    End Using
                ElseIf key.Contains("choosereeltype") Then
                    g.DrawRectangle(glow, 3, 7, 26, 18) : g.DrawRectangle(line, 3, 7, 26, 18)
                    Using slotFill As New LinearGradientBrush(New Rectangle(6, 10, 15, 12), Color.FromArgb(255, 192, 35), Color.FromArgb(255, 78, 12), LinearGradientMode.Vertical)
                        g.FillRectangle(slotFill, 6, 10, 5, 12)
                        g.FillRectangle(slotFill, 13, 10, 5, 12)
                        g.FillRectangle(slotFill, 20, 10, 5, 12)
                    End Using
                    Using arrowPen As New Pen(Color.White, 1.8F)
                        arrowPen.StartCap = LineCap.Round : arrowPen.EndCap = LineCap.ArrowAnchor
                        g.DrawLine(arrowPen, 8, 4, 23, 4)
                    End Using
                ElseIf key.Contains("newreel") OrElse key.Contains("addnewreel") Then
                    g.DrawRectangle(glow, 3, 7, 23, 18) : g.DrawRectangle(line, 3, 7, 23, 18)
                    Using digitPen As New Pen(Color.FromArgb(255, 104, 25), 2.2F)
                        digitPen.StartCap = LineCap.Round : digitPen.EndCap = LineCap.Round
                        g.DrawRectangle(digitPen, 7, 11, 5, 10)
                        g.DrawRectangle(digitPen, 16, 11, 5, 10)
                    End Using
                    DrawPlus(g, 26, 7, Color.White)
                ElseIf key.Contains("scoreframes") OrElse key.Contains("scoring") Then
                    g.DrawRectangle(glow, 4, 5, 24, 21) : g.DrawRectangle(line, 4, 5, 24, 21)
                    Using innerPen As New Pen(Color.White, 1.5F)
                        innerPen.DashStyle = DashStyle.Dot
                        g.DrawRectangle(innerPen, 9, 9, 14, 13)
                    End Using
                    Using rayPen As New Pen(Color.FromArgb(255, 225, 70), 1.8F)
                        rayPen.StartCap = LineCap.Round : rayPen.EndCap = LineCap.Round
                        g.DrawLine(rayPen, 16, 2, 16, 6) : g.DrawLine(rayPen, 2, 16, 6, 16) : g.DrawLine(rayPen, 26, 16, 30, 16)
                    End Using
                ElseIf key.Contains("reel") OrElse key.Contains("score") Then
                    Dim addSymbol As Boolean = key.Contains("add") OrElse key.Contains("new")
                    Using font As New Font("Arial Black", If(key.Contains("scoring"), 11.0F, 14.0F), FontStyle.Regular, GraphicsUnit.Pixel)
                        Dim label As String = If(key.Contains("scoring"), "88", "0")
                        TextRenderer.DrawText(g, label, font, New Rectangle(2, 4, 28, 23), Color.FromArgb(255, 104, 25), TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
                    End Using
                    If addSymbol Then DrawPlus(g, 24, 8, Color.White)
                ElseIf key.Contains("flasher") Then
                    Using boltFill As New SolidBrush(Color.White)
                        g.FillPolygon(boltFill, {New PointF(18, 3), New PointF(8, 18), New PointF(15, 18), New PointF(12, 29), New PointF(25, 13), New PointF(18, 13)})
                    End Using
                    Using flashPen As New Pen(accent, 1.7F)
                        flashPen.StartCap = LineCap.Round : flashPen.EndCap = LineCap.Round
                        g.DrawLine(flashPen, 5, 7, 9, 10)
                        g.DrawLine(flashPen, 27, 7, 23, 10)
                        g.DrawLine(flashPen, 4, 24, 9, 21)
                        g.DrawLine(flashPen, 28, 24, 23, 21)
                    End Using
                ElseIf key.Contains("animation") Then
                    g.DrawEllipse(glow, 5, 5, 22, 22) : g.DrawEllipse(line, 5, 5, 22, 22)
                    Using playBrush As New SolidBrush(Color.White)
                        g.FillPolygon(playBrush, {New PointF(13, 10), New PointF(13, 22), New PointF(23, 16)})
                    End Using
                    Using detailPen As New Pen(accent, 1.6F)
                        detailPen.StartCap = LineCap.Round : detailPen.EndCap = LineCap.Round
                        g.DrawArc(detailPen, 2, 2, 28, 28, 205, 110)
                    End Using
                ElseIf key.Contains("makesnippet") Then
                    Using selectionPen As New Pen(accent, 1.8F)
                        selectionPen.DashStyle = DashStyle.Dash
                        g.DrawRectangle(selectionPen, 5, 5, 22, 22)
                    End Using
                    Using checkPen As New Pen(Color.White, 2.4F)
                        checkPen.StartCap = LineCap.Round : checkPen.EndCap = LineCap.Round
                        g.DrawLine(checkPen, 9, 17, 14, 22)
                        g.DrawLine(checkPen, 14, 22, 24, 10)
                    End Using
                ElseIf key.Contains("snippet") Then
                    g.DrawLine(glow, 5, 11, 5, 5) : g.DrawLine(glow, 5, 5, 11, 5)
                    g.DrawLine(glow, 21, 5, 27, 5) : g.DrawLine(glow, 27, 5, 27, 11)
                    g.DrawLine(glow, 5, 21, 5, 27) : g.DrawLine(glow, 5, 27, 11, 27)
                    g.DrawLine(glow, 21, 27, 27, 27) : g.DrawLine(glow, 27, 21, 27, 27)
                    g.DrawLine(line, 5, 11, 5, 5) : g.DrawLine(line, 5, 5, 11, 5)
                    g.DrawLine(line, 21, 5, 27, 5) : g.DrawLine(line, 27, 5, 27, 11)
                    g.DrawLine(line, 5, 21, 5, 27) : g.DrawLine(line, 5, 27, 11, 27)
                    g.DrawLine(line, 21, 27, 27, 27) : g.DrawLine(line, 27, 21, 27, 27)
                    DrawPlus(g, 16, 16, Color.White)
                ElseIf key.Contains("illu") OrElse key.Contains("bulb") Then
                    Using bulbFill As New LinearGradientBrush(New Rectangle(8, 4, 16, 20), Color.White, Color.FromArgb(255, 198, 36), LinearGradientMode.Vertical)
                        g.FillEllipse(bulbFill, 8, 4, 16, 16)
                    End Using
                    g.DrawArc(line, 8, 4, 16, 16, 190, 160)
                    g.DrawLine(line, 12, 19, 14, 25) : g.DrawLine(line, 20, 19, 18, 25)
                    Using basePen As New Pen(Color.White, 1.7F) : g.DrawLine(basePen, 13, 25, 19, 25) : g.DrawLine(basePen, 14, 28, 18, 28) : End Using
                    If key.Contains("accurate") Then
                        Using checkPen As New Pen(Color.FromArgb(40, 255, 150), 2.3F) : checkPen.StartCap = LineCap.Round : checkPen.EndCap = LineCap.Round : g.DrawLines(checkPen, {New PointF(20, 18), New PointF(23, 21), New PointF(28, 14)}) : End Using
                    ElseIf key.Contains("addnew") Then
                        DrawPlus(g, 24, 9, Color.White)
                    End If
                End If
            End Using
        End Using
        Return bitmap
    End Function

    Private Sub DrawPlus(g As Graphics, x As Single, y As Single, color As Color)
        Using pen As New Pen(color, 2.0F)
            pen.StartCap = LineCap.Round : pen.EndCap = LineCap.Round
            g.DrawLine(pen, x - 4, y, x + 4, y) : g.DrawLine(pen, x, y - 4, x, y + 4)
        End Using
    End Sub

    Private Function ToolbarButtonWidth(itemName As String, caption As String) As Integer
        Dim key As String = If(itemName, String.Empty).ToLowerInvariant()
        Dim width As Integer = ButtonWidth
        If key.Contains("accurate") Then width = 58
        If key.Contains("preview") OrElse key.Contains("showillumination") Then width = Math.Max(width, 54)

        Using measureFont As New Font("Segoe UI Condensed", 6.75F, FontStyle.Regular)
            For Each line As String In If(caption, String.Empty).Split(New String() {vbCrLf, vbLf}, StringSplitOptions.None)
                width = Math.Max(width, TextRenderer.MeasureText(line, measureFont, Size.Empty, TextFormatFlags.NoPadding).Width + 9)
            Next
        End Using
        Return width
    End Function

    Private Function ToolbarAccent(item As ToolStripItem) As Color
        Dim key As String = If(item.Name, String.Empty).ToLowerInvariant()
        If key.Contains("zoom") OrElse key.Contains("importbackground") Then Return Color.FromArgb(21, 197, 255)
        If key.Contains("backglassbrightness") Then Return Color.FromArgb(255, 218, 75)
        If key.Contains("autosave") Then
            Dim toggle As ToolStripButton = TryCast(item, ToolStripButton)
            Return If(toggle IsNot Nothing AndAlso toggle.Checked, Color.FromArgb(42, 225, 105), Color.FromArgb(255, 54, 68))
        End If
        If key.Contains("redo") Then Return Color.FromArgb(183, 190, 201)
        If key.Contains("undo") Then Return Color.FromArgb(46, 177, 255)
        If key.Contains("scoreframes") Then Return Color.FromArgb(21, 197, 255)
        If key.Contains("reel") OrElse key.Contains("score") Then Return Color.FromArgb(255, 183, 0)
        If key.Contains("flasher") Then Return Color.FromArgb(255, 153, 0)
        If key.Contains("snippet") Then Return Color.FromArgb(255, 54, 68)
        If key.Contains("animation") Then Return Color.FromArgb(72, 226, 140)
        If key.Contains("illu") OrElse key.Contains("bulb") Then Return Color.FromArgb(226, 72, 255)
        If key.Contains("new") OrElse key.Contains("open") OrElse key.Contains("save") Then Return Color.FromArgb(40, 190, 255)
        Return Color.FromArgb(199, 54, 255)
    End Function

    Private Function GroupAccent(strip As ToolStrip, firstIndex As Integer, lastIndex As Integer) As Color
        ' Match the three primary design groups by their anchor buttons first.
        ' This keeps the outside panel color independent from the individual
        ' button accents and from any dynamically inserted toolbar controls.
        For index As Integer = firstIndex To lastIndex
            Dim itemName As String = If(strip.Items(index).Name, String.Empty)
            If itemName.Equals("tsbImportBackgroundImage", StringComparison.OrdinalIgnoreCase) Then
                Return Color.FromArgb(42, 225, 105)
            End If
            If itemName.Equals("tsbChooseReelTypeEnhanced", StringComparison.OrdinalIgnoreCase) OrElse
               itemName.Equals("tsbAddNewReelOrLEDFrame", StringComparison.OrdinalIgnoreCase) Then
                Return Color.FromArgb(184, 82, 8)
            End If
            If itemName.Equals("tsbAddNewBulbFrame", StringComparison.OrdinalIgnoreCase) OrElse
               itemName.Equals("tsbAddFlasherEnhanced", StringComparison.OrdinalIgnoreCase) OrElse
               itemName.Equals("tsbAddSnippetEnhanced", StringComparison.OrdinalIgnoreCase) Then
                Return Color.FromArgb(255, 218, 45)
            End If
        Next

        Dim names As New System.Text.StringBuilder()
        For index As Integer = firstIndex To lastIndex
            names.Append(" ").Append(strip.Items(index).Name)
        Next
        Dim key As String = names.ToString().ToLowerInvariant()
        If key.Contains("autosave") Then
            For index As Integer = firstIndex To lastIndex
                Dim toggle As ToolStripButton = TryCast(strip.Items(index), ToolStripButton)
                If toggle IsNot Nothing AndAlso toggle.Name.IndexOf("autosave", StringComparison.OrdinalIgnoreCase) >= 0 Then
                    Return If(toggle.Checked, Color.FromArgb(42, 225, 105), Color.FromArgb(255, 54, 68))
                End If
            Next
        End If
        If key.Contains("zoom") Then
            Return Color.FromArgb(40, 178, 255)
        End If
        If key.Contains("new") OrElse key.Contains("open") OrElse key.Contains("save") Then
            Return Color.FromArgb(40, 178, 255)
        End If
        If key.Contains("importbackground") Then
            Return Color.FromArgb(42, 225, 105)
        End If
        If key.Contains("illu") OrElse key.Contains("bulb") Then
            Return Color.FromArgb(255, 218, 45)
        End If
        If key.Contains("reel") OrElse key.Contains("score") Then
            Return Color.FromArgb(184, 82, 8)
        End If
        Return Color.FromArgb(212, 54, 255)
    End Function

    Private Class MainToolbarRenderer
        Inherits ToolStripProfessionalRenderer

        Public Sub New()
            MyBase.New(New MainToolbarColorTable())
            RoundedEdges = True
        End Sub

        Protected Overrides Sub OnRenderToolStripBackground(e As ToolStripRenderEventArgs)
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            Dim bounds As Rectangle = e.AffectedBounds
            Using brush As New LinearGradientBrush(bounds,
                                                     Color.FromArgb(8, 10, 14),
                                                     Color.FromArgb(0, 0, 1),
                                                     LinearGradientMode.Vertical)
                e.Graphics.FillRectangle(brush, bounds)
            End Using
            Using topGlow As New Pen(Color.FromArgb(72, 0, 185, 255), 1.0F)
                e.Graphics.DrawLine(topGlow, bounds.Left, bounds.Top + 1, bounds.Right, bounds.Top + 1)
            End Using
            Using bottomLine As New Pen(Color.FromArgb(105, 104, 29, 174), 1.0F)
                e.Graphics.DrawLine(bottomLine, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1)
            End Using

            DrawCommandGroups(e.Graphics, e.ToolStrip)
        End Sub

        Private Sub DrawCommandGroups(graphics As Graphics, strip As ToolStrip)
            If strip Is Nothing OrElse strip.Items.Count = 0 Then Return

            Dim firstIndex As Integer = -1
            For index As Integer = 0 To strip.Items.Count
                Dim atEnd As Boolean = index = strip.Items.Count
                Dim isSeparator As Boolean = Not atEnd AndAlso TypeOf strip.Items(index) Is ToolStripSeparator
                Dim isDisplayed As Boolean = Not atEnd AndAlso
                                             strip.Items(index).Available AndAlso
                                             strip.Items(index).Placement = ToolStripItemPlacement.Main AndAlso
                                             strip.Items(index).Bounds.Width > 0

                If firstIndex < 0 AndAlso Not isSeparator AndAlso isDisplayed Then
                    firstIndex = index
                End If

                If firstIndex >= 0 AndAlso (atEnd OrElse isSeparator) Then
                    DrawCommandGroup(graphics, strip, firstIndex, index - 1)
                    firstIndex = -1
                End If
            Next
        End Sub

        Private Sub DrawCommandGroup(graphics As Graphics, strip As ToolStrip, firstIndex As Integer, lastIndex As Integer)
            Dim groupBounds As Rectangle = Rectangle.Empty
            Dim group As B2SProUIFoundation.CommandGroup = B2SProUIFoundation.CommandGroup.Neutral

            For index As Integer = firstIndex To lastIndex
                Dim item As ToolStripItem = strip.Items(index)
                If Not item.Available OrElse item.Placement <> ToolStripItemPlacement.Main OrElse item.Bounds.Width <= 0 Then Continue For
                groupBounds = If(groupBounds.IsEmpty, item.Bounds, Rectangle.Union(groupBounds, item.Bounds))
                If group = B2SProUIFoundation.CommandGroup.Neutral Then
                    group = B2SProUIFoundation.CommandGroupForItem(item)
                End If
            Next

            If groupBounds.IsEmpty Then Return
            groupBounds.Inflate(3, 4)
            groupBounds.Y = Math.Max(7, groupBounds.Y)
            groupBounds.Height = Math.Min(strip.ClientSize.Height - groupBounds.Y - 6, groupBounds.Height)

            Dim accent As Color = GroupAccent(strip, firstIndex, lastIndex)
            Using path As GraphicsPath = RoundedRectangle(groupBounds, GroupCornerRadius)
                Using shadow As New SolidBrush(Color.FromArgb(130, 0, 0, 0))
                    Dim shadowBounds As Rectangle = groupBounds
                    shadowBounds.Offset(0, 2)
                    Using shadowPath As GraphicsPath = RoundedRectangle(shadowBounds, GroupCornerRadius)
                        graphics.FillPath(shadow, shadowPath)
                    End Using
                End Using
                Using glow As New Pen(Color.FromArgb(88, accent), 6.0F)
                    graphics.DrawPath(glow, path)
                End Using
                Using fill As New LinearGradientBrush(groupBounds,
                                                       Color.FromArgb(250, 16, 18, 23),
                                                       Color.FromArgb(253, 1, 1, 3),
                                                       LinearGradientMode.Vertical)
                    graphics.FillPath(fill, path)
                End Using
                Using border As New Pen(Color.FromArgb(255, accent), 1.6F)
                    graphics.DrawPath(border, path)
                End Using
            End Using
        End Sub

        Private Sub DrawCommandGroupTitle(graphics As Graphics,
                                          groupBounds As Rectangle,
                                          title As String,
                                          accent As Color)
            If String.IsNullOrWhiteSpace(title) Then Return

            Using titleFont As New Font("Segoe UI Semibold", 7.0F, FontStyle.Bold)
                Dim titleSize As Size = TextRenderer.MeasureText(title, titleFont)
                Dim titleBounds As New Rectangle(groupBounds.Left + 10,
                                                 Math.Max(1, groupBounds.Top - 7),
                                                 titleSize.Width + 10,
                                                 titleSize.Height)
                Using titleBack As New SolidBrush(Color.FromArgb(1, 3, 6))
                    graphics.FillRectangle(titleBack, titleBounds)
                End Using
                TextRenderer.DrawText(graphics,
                                      title,
                                      titleFont,
                                      titleBounds,
                                      accent,
                                      TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            End Using
        End Sub

        Private Function CommandGroupTitle(strip As ToolStrip,
                                           firstIndex As Integer,
                                           lastIndex As Integer,
                                           group As B2SProUIFoundation.CommandGroup) As String
            Dim key As New System.Text.StringBuilder()
            For index As Integer = firstIndex To lastIndex
                Dim item As ToolStripItem = strip.Items(index)
                key.Append(" ").Append(item.Name).Append(" ").Append(item.Text)
            Next

            Dim names As String = key.ToString().ToLowerInvariant()
            If names.Contains("help") Then Return "TOOLS"
            If names.Contains("illumination") OrElse names.Contains("illu") OrElse names.Contains("bulb") Then Return "LIGHTS"
            If names.Contains("reel") OrElse names.Contains("score") OrElse names.Contains("led") Then Return "REELS & LEDS"
            If names.Contains("importbackground") Then Return "IMAGE"
            If names.Contains("zoom") Then Return "VIEW"
            If names.Contains("undo") OrElse names.Contains("redo") Then Return "EDIT"
            If names.Contains("new") OrElse names.Contains("open") OrElse names.Contains("save") Then Return "FILE"

            Select Case group
                Case B2SProUIFoundation.CommandGroup.File
                    Return "FILE"
                Case B2SProUIFoundation.CommandGroup.Edit
                    Return "EDIT"
                Case B2SProUIFoundation.CommandGroup.View
                    Return "VIEW"
                Case B2SProUIFoundation.CommandGroup.Lighting
                    Return "LIGHTS"
                Case B2SProUIFoundation.CommandGroup.Artwork
                    Return "IMAGE"
                Case B2SProUIFoundation.CommandGroup.PlayExport
                    Return "PLAY / EXPORT"
                Case Else
                    Return "TOOLS"
            End Select
        End Function

        Protected Overrides Sub OnRenderButtonBackground(e As ToolStripItemRenderEventArgs)
            Dim button As ToolStripButton = TryCast(e.Item, ToolStripButton)
            If button Is Nothing Then
                MyBase.OnRenderButtonBackground(e)
                Return
            End If

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            Dim bounds As New Rectangle(1, 1, Math.Max(1, button.Width - 3), Math.Max(1, button.Height - 3))
            Dim accent As Color = ToolbarAccent(button)
            Dim active As Boolean = button.Pressed OrElse button.Selected OrElse button.Checked

            If button.Name.IndexOf("autosave", StringComparison.OrdinalIgnoreCase) >= 0 Then
                Using path As GraphicsPath = RoundedRectangle(bounds, 8)
                    Using glow As New Pen(Color.FromArgb(78, accent), 6.0F)
                        e.Graphics.DrawPath(glow, path)
                    End Using
                    Using fill As New LinearGradientBrush(bounds, Color.FromArgb(205, accent), Color.FromArgb(72, accent), LinearGradientMode.Vertical)
                        e.Graphics.FillPath(fill, path)
                    End Using
                    Using pen As New Pen(Color.FromArgb(250, accent), 1.2F)
                        e.Graphics.DrawPath(pen, path)
                    End Using
                End Using
                Return
            End If

            If active Then
                Using path As GraphicsPath = RoundedRectangle(bounds, 8)
                    Using glow As New Pen(Color.FromArgb(65, accent), 5.0F)
                        e.Graphics.DrawPath(glow, path)
                    End Using
                    Using fill As New LinearGradientBrush(bounds,
                                                           Color.FromArgb(150, accent),
                                                           Color.FromArgb(38, accent),
                                                           LinearGradientMode.Vertical)
                        e.Graphics.FillPath(fill, path)
                    End Using
                    Using pen As New Pen(Color.FromArgb(245, accent), 1.0F)
                        e.Graphics.DrawPath(pen, path)
                    End Using
                End Using
            Else
                Using path As GraphicsPath = RoundedRectangle(bounds, 8)
                    Using fill As New LinearGradientBrush(bounds,
                                                           Color.FromArgb(27, 29, 35),
                                                           Color.FromArgb(2, 2, 4),
                                                           LinearGradientMode.Vertical)
                        e.Graphics.FillPath(fill, path)
                    End Using
                    Using pen As New Pen(Color.FromArgb(36, accent), 1.0F)
                        e.Graphics.DrawPath(pen, path)
                    End Using
                End Using
            End If
        End Sub

        Protected Overrides Sub OnRenderItemImage(e As ToolStripItemImageRenderEventArgs)
            If e.Image Is Nothing Then Return
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality
            If Not e.Item.Enabled Then
                ' ControlPaint.DrawImageDisabled always draws at the source
                ' bitmap's native size. Several toolbar resources are much
                ' larger than ImageScalingSize, so disabled startup commands
                ' used to spill gray blocks across neighboring buttons.
                Using attributes As New ImageAttributes()
                    Dim disabledMatrix As New ColorMatrix(New Single()() {
                        New Single() {0.299F, 0.299F, 0.299F, 0.0F, 0.0F},
                        New Single() {0.587F, 0.587F, 0.587F, 0.0F, 0.0F},
                        New Single() {0.114F, 0.114F, 0.114F, 0.0F, 0.0F},
                        New Single() {0.0F, 0.0F, 0.0F, 0.42F, 0.0F},
                        New Single() {0.0F, 0.0F, 0.0F, 0.0F, 1.0F}})
                    attributes.SetColorMatrix(disabledMatrix)
                    e.Graphics.DrawImage(e.Image,
                                         e.ImageRectangle,
                                         0,
                                         0,
                                         e.Image.Width,
                                         e.Image.Height,
                                         GraphicsUnit.Pixel,
                                         attributes)
                End Using
                Return
            End If

            e.Graphics.DrawImage(e.Image,
                                 e.ImageRectangle,
                                 0,
                                 0,
                                 e.Image.Width,
                                 e.Image.Height,
                                 GraphicsUnit.Pixel)
        End Sub

        Protected Overrides Sub OnRenderItemText(e As ToolStripItemTextRenderEventArgs)
            If TypeOf e.Item Is ToolStripButton Then
                e.TextColor = If(e.Item.Enabled, B2SProUIFoundation.Palette.TextPrimary, Color.FromArgb(105, 115, 135))
                e.TextFont = New Font("Segoe UI Condensed", 6.5F, FontStyle.Regular)
            End If
            MyBase.OnRenderItemText(e)
        End Sub

        Protected Overrides Sub OnRenderSeparator(e As ToolStripSeparatorRenderEventArgs)
            ' Separators create the gaps between the rounded neon panels.
        End Sub

        Private Function GroupAfterSeparator(separator As ToolStripItem) As B2SProUIFoundation.CommandGroup
            If separator Is Nothing OrElse separator.Owner Is Nothing Then Return B2SProUIFoundation.CommandGroup.Neutral
            Dim index As Integer = separator.Owner.Items.IndexOf(separator)
            For i As Integer = index + 1 To separator.Owner.Items.Count - 1
                Dim nextItem As ToolStripItem = separator.Owner.Items(i)
                If TypeOf nextItem Is ToolStripButton Then Return B2SProUIFoundation.CommandGroupForItem(nextItem)
            Next
            Return B2SProUIFoundation.CommandGroup.Neutral
        End Function

        Private Function RoundedRectangle(bounds As Rectangle, radius As Integer) As GraphicsPath
            Dim path As New GraphicsPath()
            Dim diameter As Integer = Math.Max(2, radius * 2)
            Dim arc As New Rectangle(bounds.Location, New Size(diameter, diameter))
            path.AddArc(arc, 180, 90)
            arc.X = bounds.Right - diameter
            path.AddArc(arc, 270, 90)
            arc.Y = bounds.Bottom - diameter
            path.AddArc(arc, 0, 90)
            arc.X = bounds.Left
            path.AddArc(arc, 90, 90)
            path.CloseFigure()
            Return path
        End Function
    End Class

    Private Class MainMenuRenderer
        Inherits ToolStripProfessionalRenderer

        Public Sub New()
            MyBase.New(New MainMenuColorTable())
            RoundedEdges = True
        End Sub

        Protected Overrides Sub OnRenderToolStripBackground(e As ToolStripRenderEventArgs)
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            Dim bounds As Rectangle = e.AffectedBounds
            If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return

            Using fill As New LinearGradientBrush(bounds,
                                                   Color.FromArgb(12, 14, 19),
                                                   Color.FromArgb(0, 1, 3),
                                                   LinearGradientMode.Vertical)
                e.Graphics.FillRectangle(fill, bounds)
            End Using

            If TypeOf e.ToolStrip Is MenuStrip Then
                Using glow As New Pen(Color.FromArgb(48, 178, 49, 255), 4.0F)
                    e.Graphics.DrawLine(glow, bounds.Left, bounds.Bottom - 2, bounds.Right, bounds.Bottom - 2)
                End Using
                Using border As New Pen(Color.FromArgb(175, 181, 57, 255), 1.0F)
                    e.Graphics.DrawLine(border, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1)
                End Using
            End If
        End Sub

        Protected Overrides Sub OnRenderMenuItemBackground(e As ToolStripItemRenderEventArgs)
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            Dim bounds As New Rectangle(2, 2, Math.Max(1, e.Item.Width - 5), Math.Max(1, e.Item.Height - 5))
            Dim active As Boolean = e.Item.Selected OrElse e.Item.Pressed
            Dim accent As Color = If(e.Item.Pressed,
                                     Color.FromArgb(205, 58, 255),
                                     Color.FromArgb(79, 174, 255))
            Using path As GraphicsPath = RoundedMenuRectangle(bounds, 7)
                Using glow As New Pen(Color.FromArgb(If(active, 60, 20), accent), If(active, 4.0F, 2.0F))
                    e.Graphics.DrawPath(glow, path)
                End Using
                Using fill As New LinearGradientBrush(bounds,
                                                       If(active, Color.FromArgb(118, accent), Color.FromArgb(28, 31, 42)),
                                                       If(active, Color.FromArgb(28, accent), Color.FromArgb(3, 5, 9)),
                                                       LinearGradientMode.Vertical)
                    e.Graphics.FillPath(fill, path)
                End Using
                Using border As New Pen(Color.FromArgb(If(active, 230, 62), accent), 1.0F)
                    e.Graphics.DrawPath(border, path)
                End Using
                Using highlight As New Pen(Color.FromArgb(If(active, 115, 48), 235, 240, 255), 1.0F)
                    e.Graphics.DrawLine(highlight, bounds.Left + 7, bounds.Top + 1, bounds.Right - 7, bounds.Top + 1)
                End Using
            End Using
        End Sub

        Protected Overrides Sub OnRenderToolStripBorder(e As ToolStripRenderEventArgs)
            If TypeOf e.ToolStrip Is ToolStripDropDown Then
                Dim bounds As New Rectangle(0, 0, Math.Max(1, e.ToolStrip.Width - 1), Math.Max(1, e.ToolStrip.Height - 1))
                Using glow As New Pen(Color.FromArgb(42, 194, 51, 255), 4.0F)
                    e.Graphics.DrawRectangle(glow, bounds)
                End Using
                Using border As New Pen(Color.FromArgb(205, 190, 62, 255), 1.0F)
                    e.Graphics.DrawRectangle(border, bounds)
                End Using
            End If
        End Sub

        Protected Overrides Sub OnRenderSeparator(e As ToolStripSeparatorRenderEventArgs)
            If e.Vertical Then
                Dim x As Integer = e.Item.Width \ 2
                Using line As New Pen(Color.FromArgb(105, 63, 154, 255), 1.0F)
                    e.Graphics.DrawLine(line, x, 5, x, Math.Max(5, e.Item.Height - 5))
                End Using
            Else
                Dim y As Integer = e.Item.Height \ 2
                Using line As New Pen(Color.FromArgb(105, 190, 63, 255), 1.0F)
                    e.Graphics.DrawLine(line, 28, y, Math.Max(28, e.Item.Width - 7), y)
                End Using
            End If
        End Sub

        Protected Overrides Sub OnRenderItemText(e As ToolStripItemTextRenderEventArgs)
            e.TextColor = If(e.Item.Enabled,
                             Color.FromArgb(246, 247, 251),
                             Color.FromArgb(112, 118, 132))
            MyBase.OnRenderItemText(e)
        End Sub

        Protected Overrides Sub OnRenderArrow(e As ToolStripArrowRenderEventArgs)
            e.ArrowColor = If(e.Item.Enabled,
                              Color.FromArgb(235, 186, 76, 255),
                              Color.FromArgb(100, 105, 118))
            MyBase.OnRenderArrow(e)
        End Sub

        Private Shared Function RoundedMenuRectangle(bounds As Rectangle, radius As Integer) As GraphicsPath
            Dim path As New GraphicsPath()
            Dim diameter As Integer = Math.Max(2, radius * 2)
            Dim arc As New Rectangle(bounds.Location, New Size(diameter, diameter))
            path.AddArc(arc, 180, 90)
            arc.X = bounds.Right - diameter
            path.AddArc(arc, 270, 90)
            arc.Y = bounds.Bottom - diameter
            path.AddArc(arc, 0, 90)
            arc.X = bounds.Left
            path.AddArc(arc, 90, 90)
            path.CloseFigure()
            Return path
        End Function
    End Class

    Private Class MainStatusRenderer
        Inherits ToolStripProfessionalRenderer

        Public Sub New()
            MyBase.New(New MainToolbarColorTable())
            RoundedEdges = False
        End Sub

        Protected Overrides Sub OnRenderToolStripBackground(e As ToolStripRenderEventArgs)
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            Dim bounds As Rectangle = e.AffectedBounds
            If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return

            Using fill As New LinearGradientBrush(bounds,
                                                   Color.FromArgb(10, 13, 19),
                                                   Color.FromArgb(0, 1, 3),
                                                   LinearGradientMode.Vertical)
                e.Graphics.FillRectangle(fill, bounds)
            End Using
            Using glow As New Pen(Color.FromArgb(50, 204, 54, 255), 4.0F)
                e.Graphics.DrawLine(glow, bounds.Left, bounds.Top + 1, bounds.Right, bounds.Top + 1)
            End Using
            Using border As New Pen(Color.FromArgb(190, 196, 59, 255), 1.0F)
                e.Graphics.DrawLine(border, bounds.Left, bounds.Top, bounds.Right, bounds.Top)
            End Using

            For Each item As ToolStripItem In e.ToolStrip.Items
                Dim label As ToolStripStatusLabel = TryCast(item, ToolStripStatusLabel)
                If label Is Nothing OrElse Not label.Available OrElse label.Bounds.Width < 7 Then Continue For

                Dim accent As Color = StatusAccent(label.Name)
                Dim segmentWidth As Integer = label.Bounds.Width - 4
                If label.Spring Then
                    Dim textWidth As Integer = TextRenderer.MeasureText(label.Text, label.Font).Width
                    Dim imageWidth As Integer = If(label.Image IsNot Nothing, label.Image.Width + 8, 0)
                    segmentWidth = Math.Min(segmentWidth, Math.Max(82, textWidth + imageWidth + 24))
                End If

                Dim segment As New Rectangle(label.Bounds.Left + 2,
                                             label.Bounds.Top + 2,
                                             Math.Max(1, segmentWidth),
                                             Math.Max(1, label.Bounds.Height - 5))
                Using path As GraphicsPath = RoundedStatusRectangle(segment, 9)
                    Using segmentGlow As New Pen(Color.FromArgb(48, accent), 4.0F)
                        e.Graphics.DrawPath(segmentGlow, path)
                    End Using
                    Using segmentFill As New LinearGradientBrush(segment,
                                                                  Color.FromArgb(38, accent),
                                                                  Color.FromArgb(2, 5, 8),
                                                                  LinearGradientMode.Vertical)
                        e.Graphics.FillPath(segmentFill, path)
                    End Using
                    Using segmentBorder As New Pen(Color.FromArgb(185, accent), 1.0F)
                        e.Graphics.DrawPath(segmentBorder, path)
                    End Using
                End Using
            Next
        End Sub

        Protected Overrides Sub OnRenderToolStripBorder(e As ToolStripRenderEventArgs)
            ' The neon top edge is drawn with the background so there is no
            ' legacy light border around the status strip.
        End Sub

        Protected Overrides Sub OnRenderItemText(e As ToolStripItemTextRenderEventArgs)
            e.TextColor = If(e.Item.Enabled, e.Item.ForeColor, Color.FromArgb(105, 111, 124))
            MyBase.OnRenderItemText(e)
        End Sub

        Private Shared Function StatusAccent(itemName As String) As Color
            Dim key As String = If(itemName, String.Empty).ToLowerInvariant()
            If key.Contains("statusinfo") Then Return Color.FromArgb(64, 226, 149)
            If key.Contains("fileinfo") Then Return Color.FromArgb(89, 184, 255)
            If key.Contains("filesize") Then Return Color.FromArgb(255, 169, 65)
            If key.Contains("marker") Then Return Color.FromArgb(206, 112, 255)
            Return Color.FromArgb(89, 184, 255)
        End Function

        Private Shared Function RoundedStatusRectangle(bounds As Rectangle, radius As Integer) As GraphicsPath
            Dim path As New GraphicsPath()
            Dim diameter As Integer = Math.Max(2, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)))
            Dim arc As New Rectangle(bounds.Location, New Size(diameter, diameter))
            path.AddArc(arc, 180, 90)
            arc.X = bounds.Right - diameter
            path.AddArc(arc, 270, 90)
            arc.Y = bounds.Bottom - diameter
            path.AddArc(arc, 0, 90)
            arc.X = bounds.Left
            path.AddArc(arc, 90, 90)
            path.CloseFigure()
            Return path
        End Function
    End Class

    Private Class MainMenuColorTable
        Inherits ProfessionalColorTable

        Public Overrides ReadOnly Property ToolStripDropDownBackground As Color
            Get
                Return Color.FromArgb(2, 3, 6)
            End Get
        End Property

        Public Overrides ReadOnly Property ImageMarginGradientBegin As Color
            Get
                Return Color.FromArgb(4, 6, 10)
            End Get
        End Property

        Public Overrides ReadOnly Property ImageMarginGradientMiddle As Color
            Get
                Return Color.FromArgb(7, 9, 14)
            End Get
        End Property

        Public Overrides ReadOnly Property ImageMarginGradientEnd As Color
            Get
                Return Color.FromArgb(4, 6, 10)
            End Get
        End Property

        Public Overrides ReadOnly Property MenuItemBorder As Color
            Get
                Return Color.FromArgb(190, 62, 255)
            End Get
        End Property

        Public Overrides ReadOnly Property MenuItemSelected As Color
            Get
                Return Color.FromArgb(53, 17, 78)
            End Get
        End Property

        Public Overrides ReadOnly Property MenuItemPressedGradientBegin As Color
            Get
                Return Color.FromArgb(91, 28, 129)
            End Get
        End Property

        Public Overrides ReadOnly Property MenuItemPressedGradientEnd As Color
            Get
                Return Color.FromArgb(18, 6, 28)
            End Get
        End Property

        Public Overrides ReadOnly Property SeparatorDark As Color
            Get
                Return Color.FromArgb(126, 52, 194)
            End Get
        End Property

        Public Overrides ReadOnly Property SeparatorLight As Color
            Get
                Return Color.FromArgb(126, 52, 194)
            End Get
        End Property
    End Class

    Private Class MainToolbarColorTable
        Inherits ProfessionalColorTable

        Public Overrides ReadOnly Property ToolStripGradientBegin As Color
            Get
                Return B2SProUIFoundation.Palette.ToolbarBack
            End Get
        End Property

        Public Overrides ReadOnly Property ToolStripGradientMiddle As Color
            Get
                Return B2SProUIFoundation.Palette.ToolbarBack
            End Get
        End Property

        Public Overrides ReadOnly Property ToolStripGradientEnd As Color
            Get
                Return B2SProUIFoundation.Palette.ToolbarBack
            End Get
        End Property

        Public Overrides ReadOnly Property ToolStripBorder As Color
            Get
                Return B2SProUIFoundation.Palette.Border
            End Get
        End Property
    End Class
End Module
