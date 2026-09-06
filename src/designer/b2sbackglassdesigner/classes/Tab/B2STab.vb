Imports System
Imports System.Drawing.Drawing2D

Public Class B2STab

    Inherits ContainerControl

    Public Shadows Event MouseDown(ByVal sender As Object, ByVal e As System.Windows.Forms.MouseEventArgs)
    Public Shadows Event MouseMove(ByVal sender As Object, ByVal e As Mouse.MouseMoveEventArgs)
    Public Event CopyDMDCopyArea(ByVal sender As Object, ByVal e As System.EventArgs)
    Public Event RemoveDMDCopyArea(ByVal sender As Object, ByVal e As System.EventArgs)
    Public Event SelectedIndexChanged(ByVal sender As Object, ByVal e As EventArgs)
    Public Event SelectedItemClicked(ByVal sender As Object, ByVal e As Mouse.MouseMoveEventArgs)
    Public Event SelectedItemMoving(ByVal sender As Object, ByVal e As Mouse.MouseMoveEventArgs)
    Public Event SelectedItemRemoved(ByVal sender As Object, ByVal e As System.EventArgs)
    Public Event SelectedBulbMoved(ByVal sender As Object, ByVal e As System.Windows.Forms.MouseEventArgs)
    Public Event SelectedBulbEdited(ByVal sender As Object, ByVal e As System.EventArgs)
    Public Event LightsReportProgress(ByVal sender As Object, ByVal e As Illumination.Lights.LightsProgressEventArgs)
    Public Event LightColorChanged(ByVal sedner As Object, ByVal e As Illumination.Lights.LightColorChangedEventArgs)
    Public Event NewProjectRequested(ByVal sender As Object, ByVal e As EventArgs)
    Public Event ProjectSavedOnClose(ByVal data As Backglass.Data)
    Public Event ProjectClosedWithoutSaving(ByVal data As Backglass.Data)

    Private Const tabheight As Integer = 32

    Private fontstd As Font = New Font("Segoe UI", 9, FontStyle.Regular)
    Private fontbold As Font = New Font("Segoe UI", 9, FontStyle.Bold)
    Private WithEvents emptyWorkspaceNewButton As WorkspaceNewButton

    ' A real clickable control painted with the exact artwork originally used
    ' by DrawEmptyWorkspace. This preserves the original appearance while
    ' providing dependable WinForms click routing.
    Private Class WorkspaceNewButton
        Inherits Control

        Public Sub New()
            Me.SetStyle(ControlStyles.AllPaintingInWmPaint Or
                        ControlStyles.OptimizedDoubleBuffer Or
                        ControlStyles.SupportsTransparentBackColor Or
                        ControlStyles.UserPaint, True)
            Me.BackColor = Color.Transparent
            Me.Cursor = Cursors.Hand
            Me.Size = New Size(60, 60)
            Me.TabStop = False
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            Dim iconCenter As New Point(Me.ClientSize.Width \ 2, Me.ClientSize.Height \ 2)
            Using iconGlow As New SolidBrush(Color.FromArgb(42, 191, 76, 255))
                e.Graphics.FillEllipse(iconGlow, iconCenter.X - 30, iconCenter.Y - 30, 60, 60)
            End Using
            Using iconFill As New LinearGradientBrush(New Rectangle(iconCenter.X - 22, iconCenter.Y - 22, 44, 44),
                                                       Color.FromArgb(126, 64, 217),
                                                       Color.FromArgb(18, 83, 176),
                                                       LinearGradientMode.ForwardDiagonal)
                e.Graphics.FillEllipse(iconFill, iconCenter.X - 22, iconCenter.Y - 22, 44, 44)
            End Using
            Using plusPen As New Pen(Color.White, 3.0F)
                plusPen.StartCap = LineCap.Round
                plusPen.EndCap = LineCap.Round
                e.Graphics.DrawLine(plusPen, iconCenter.X - 10, iconCenter.Y, iconCenter.X + 10, iconCenter.Y)
                e.Graphics.DrawLine(plusPen, iconCenter.X, iconCenter.Y - 10, iconCenter.X, iconCenter.Y + 10)
            End Using
        End Sub
    End Class

    Protected Overrides Sub OnPaint(e As System.Windows.Forms.PaintEventArgs)
        MyBase.OnPaint(e)
        If AppThemeManager.DarkMode Then
            DrawDarkTabs(e)
            Return
        End If

        ' draw background
        e.Graphics.SmoothingMode = SmoothingMode.HighQuality
        Dim brush As LinearGradientBrush = New LinearGradientBrush(e.ClipRectangle, Color.FromArgb(190, 206, 220), Color.FromArgb(215, 228, 242), LinearGradientMode.Vertical)
        e.Graphics.FillRectangle(brush, e.ClipRectangle)
        brush.Dispose()
        Dim pen As Pen = New Pen(Color.DarkGray)
        e.Graphics.DrawLine(pen, 0, Me.Height - 1, Me.Width - 1, Me.Height - 1)
        pen.Dispose()
        ' draw tabs
        If TabPages.Count > 0 Then
            Dim i As Integer = 0
            Dim x As Integer = 3
            Dim y As Integer = Me.Height - tabheight + 3
            e.Graphics.DrawLine(Pens.Black, 0, y, x, y)
            For Each tabpage As B2STabPage In TabPages
                With tabpage
                    Dim size As Size = TextRenderer.MeasureText(.Text, If(i = SelectedIndex, fontbold, fontstd))
                    Dim thumbnailwidth As Integer = If(.ThumbnailImage IsNot Nothing, .ThumbnailImage.Width + 2, 0)
                    .TabLocation = New Point(x, y)
                    .TabSize = New Size(size.Width + 10 + thumbnailwidth, tabheight - 8 + If(i = SelectedIndex, 2, 0))
                    If thumbnailwidth > 0 Then
                        e.Graphics.DrawImage(.ThumbnailImage, New Point(x + 6, y))
                        If i = SelectedIndex Then
                            e.Graphics.DrawLine(Pens.White, x + 6 + 1, y + 16, x + 6 + 16, y + 16)
                            e.Graphics.DrawLine(Pens.White, x + 6 + 16, y + 1, x + 6 + 16, y + 16)
                        End If
                    End If
                    If i = SelectedIndex Then
                        'e.Graphics.DrawString(.Text, If(i = SelectedIndex, fontbold, fontstd), Brushes.White, New Point(x + 6 + thumbnailwidth + 1, y + 1))
                        TextRenderer.DrawText(e.Graphics, .Text, If(i = SelectedIndex, fontbold, fontstd), New Point(x + 6 + thumbnailwidth + 1, y + 1), Color.White)
                    End If
                    'e.Graphics.DrawString(.Text, If(i = SelectedIndex, fontbold, fontstd), Brushes.Black, New Point(x + 6 + thumbnailwidth, y))
                    TextRenderer.DrawText(e.Graphics, .Text, If(i = SelectedIndex, fontbold, fontstd), New Point(x + 6 + thumbnailwidth, y), Color.Black)
                    If i = SelectedIndex Then
                        e.Graphics.DrawLine(Pens.White, .TabLocation.X + 1, .TabLocation.Y, .TabLocation.X + 1, .TabLocation.Y + .TabSize.Height - 1)
                        e.Graphics.DrawLine(Pens.White, .TabLocation.X + 1, .TabLocation.Y + .TabSize.Height - 1, .TabLocation.X + .TabSize.Width - 1, .TabLocation.Y + .TabSize.Height - 1)
                        e.Graphics.DrawLine(Pens.White, .TabLocation.X + .TabSize.Width - 1, .TabLocation.Y + .TabSize.Height - 1, .TabLocation.X + .TabSize.Width - 1, .TabLocation.Y)
                    End If
                    e.Graphics.DrawLine(Pens.Black, .TabLocation.X, .TabLocation.Y, .TabLocation.X, .TabLocation.Y + .TabSize.Height - 1)
                    e.Graphics.DrawLine(Pens.Black, .TabLocation.X + 1, .TabLocation.Y + .TabSize.Height, .TabLocation.X + .TabSize.Width, .TabLocation.Y + .TabSize.Height)
                    e.Graphics.DrawLine(Pens.Black, .TabLocation.X + .TabSize.Width, .TabLocation.Y + .TabSize.Height, .TabLocation.X + .TabSize.Width, .TabLocation.Y)
                    x += .TabSize.Width
                End With
                i += 1
            Next
        End If
    End Sub

    Private Sub DrawDarkTabs(e As PaintEventArgs)
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        Using background As New LinearGradientBrush(ClientRectangle,
                                                     Color.FromArgb(10, 14, 22),
                                                     Color.FromArgb(0, 1, 4),
                                                     LinearGradientMode.Vertical)
            e.Graphics.FillRectangle(background, ClientRectangle)
        End Using

        Dim y As Integer = Me.Height - tabheight + 3
        Dim workspaceBounds As New Rectangle(8, 8, Math.Max(1, Me.Width - 17), Math.Max(1, y - 17))
        DrawWorkspaceGlass(e.Graphics, workspaceBounds)

        Dim railBounds As New Rectangle(5, Math.Max(0, y - 5), Math.Max(1, Me.Width - 11), Math.Max(1, Me.Height - y + 3))
        Using railPath As GraphicsPath = RoundedTabPath(railBounds, 9)
            Using railFill As New LinearGradientBrush(railBounds,
                                                       Color.FromArgb(16, 20, 30),
                                                       Color.FromArgb(1, 3, 7),
                                                       LinearGradientMode.Vertical)
                e.Graphics.FillPath(railFill, railPath)
            End Using
            Using railGlow As New Pen(Color.FromArgb(48, 185, 55, 255), 4.0F)
                e.Graphics.DrawPath(railGlow, railPath)
            End Using
            Using railBorder As New Pen(Color.FromArgb(175, 190, 62, 255), 1.0F)
                e.Graphics.DrawPath(railBorder, railPath)
            End Using
        End Using

        If TabPages.Count = 0 Then
            DrawEmptyWorkspace(e.Graphics, workspaceBounds)
            Return
        End If

        Dim index As Integer = 0
        Dim x As Integer = 6
        For Each tabpage As B2STabPage In TabPages
            Dim selected As Boolean = index = SelectedIndex
            Dim tabFont As Font = If(selected, fontbold, fontstd)
            Dim textSize As Size = TextRenderer.MeasureText(tabpage.Text, tabFont)
            Dim thumbnailWidth As Integer = If(tabpage.ThumbnailImage IsNot Nothing, tabpage.ThumbnailImage.Width + 4, 0)
            tabpage.TabLocation = New Point(x, y)
            Dim dirtyWidth As Integer = If(tabpage.BackglassData IsNot Nothing AndAlso tabpage.BackglassData.IsDirty, 12, 0)
            tabpage.TabSize = New Size(textSize.Width + 20 + thumbnailWidth + dirtyWidth, tabheight - 6 + If(selected, 2, 0))

            Dim bounds As New Rectangle(tabpage.TabLocation, tabpage.TabSize)
            Dim accent As Color = If(selected, Color.FromArgb(205, 58, 255), Color.FromArgb(45, 171, 255))
            Using path As GraphicsPath = RoundedTabPath(bounds, 8)
                If selected Then
                    Using tabGlow As New Pen(Color.FromArgb(65, accent), 4.0F)
                        e.Graphics.DrawPath(tabGlow, path)
                    End Using
                End If
                Using fill As New LinearGradientBrush(bounds,
                                                       If(selected, Color.FromArgb(120, accent), Color.FromArgb(20, 23, 31)),
                                                       If(selected, Color.FromArgb(30, accent), Color.FromArgb(3, 5, 9)),
                                                       LinearGradientMode.Vertical)
                    e.Graphics.FillPath(fill, path)
                End Using
                Using border As New Pen(Color.FromArgb(If(selected, 235, 105), accent), 1.0F)
                    e.Graphics.DrawPath(border, path)
                End Using
            End Using

            If selected Then
                Using highlight As New Pen(Color.FromArgb(235, 225, 116, 255), 2.0F)
                    e.Graphics.DrawLine(highlight, bounds.Left + 10, bounds.Top + 2, bounds.Right - 10, bounds.Top + 2)
                End Using
            End If

            Dim textLeft As Integer = bounds.Left + 9
            If tabpage.ThumbnailImage IsNot Nothing Then
                Dim imageY As Integer = bounds.Top + Math.Max(1, (bounds.Height - tabpage.ThumbnailImage.Height) \ 2)
                e.Graphics.DrawImage(tabpage.ThumbnailImage, New Point(textLeft, imageY))
                textLeft += thumbnailWidth
            End If

            Dim isDirty As Boolean = tabpage.BackglassData IsNot Nothing AndAlso tabpage.BackglassData.IsDirty
            Dim textRightPadding As Integer = If(isDirty, 18, 7)
            Dim textBounds As New Rectangle(textLeft, bounds.Top, Math.Max(1, bounds.Right - textLeft - textRightPadding), bounds.Height)
            TextRenderer.DrawText(e.Graphics,
                                  tabpage.Text,
                                  tabFont,
                                  textBounds,
                                  If(selected, Color.White, Color.FromArgb(190, 198, 214)),
                                  TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
            If isDirty Then
                Dim dirtyDot As New Rectangle(bounds.Right - 13, bounds.Top + (bounds.Height \ 2) - 3, 6, 6)
                Using dirtyGlow As New SolidBrush(Color.FromArgb(82, 255, 179, 50))
                    e.Graphics.FillEllipse(dirtyGlow, Rectangle.Inflate(dirtyDot, 3, 3))
                End Using
                Using dirtyBrush As New SolidBrush(Color.FromArgb(255, 190, 55))
                    e.Graphics.FillEllipse(dirtyBrush, dirtyDot)
                End Using
            End If
            x += tabpage.TabSize.Width + 4
            index += 1
        Next
    End Sub

    Private Sub DrawWorkspaceGlass(graphics As Graphics, bounds As Rectangle)
        If bounds.Width < 8 OrElse bounds.Height < 8 Then Return

        Using path As GraphicsPath = RoundedTabPath(bounds, 12)
            Using fill As New LinearGradientBrush(bounds,
                                                   Color.FromArgb(8, 13, 22),
                                                   Color.FromArgb(0, 2, 6),
                                                   LinearGradientMode.Vertical)
                graphics.FillPath(fill, path)
            End Using
            Using outerGlow As New Pen(Color.FromArgb(38, 43, 174, 255), 5.0F)
                graphics.DrawPath(outerGlow, path)
            End Using
            Using border As New Pen(Color.FromArgb(120, 55, 186, 255), 1.0F)
                graphics.DrawPath(border, path)
            End Using
        End Using

        Dim oldClip As Region = graphics.Clip
        graphics.SetClip(bounds)
        Using minorGrid As New Pen(Color.FromArgb(13, 49, 193, 255), 1.0F)
            For gridX As Integer = bounds.Left + 24 To bounds.Right Step 48
                graphics.DrawLine(minorGrid, gridX, bounds.Top, gridX, bounds.Bottom)
            Next
            For gridY As Integer = bounds.Top + 24 To bounds.Bottom Step 48
                graphics.DrawLine(minorGrid, bounds.Left, gridY, bounds.Right, gridY)
            Next
        End Using
        Using majorGrid As New Pen(Color.FromArgb(17, 204, 66, 255), 1.0F)
            For gridX As Integer = bounds.Left + 96 To bounds.Right Step 192
                graphics.DrawLine(majorGrid, gridX, bounds.Top, gridX, bounds.Bottom)
            Next
            For gridY As Integer = bounds.Top + 96 To bounds.Bottom Step 192
                graphics.DrawLine(majorGrid, bounds.Left, gridY, bounds.Right, gridY)
            Next
        End Using
        graphics.Clip = oldClip
        oldClip.Dispose()
    End Sub

    Private Sub DrawEmptyWorkspace(graphics As Graphics, workspaceBounds As Rectangle)
        If workspaceBounds.Width < 260 OrElse workspaceBounds.Height < 150 Then Return

        Dim cardWidth As Integer = Math.Min(520, workspaceBounds.Width - 56)
        Dim cardHeight As Integer = Math.Min(190, workspaceBounds.Height - 44)
        Dim cardBounds As New Rectangle(workspaceBounds.Left + ((workspaceBounds.Width - cardWidth) \ 2),
                                        workspaceBounds.Top + ((workspaceBounds.Height - cardHeight) \ 2),
                                        cardWidth,
                                        cardHeight)
        Using cardPath As GraphicsPath = RoundedTabPath(cardBounds, 16)
            Using glow As New Pen(Color.FromArgb(48, 195, 62, 255), 7.0F)
                graphics.DrawPath(glow, cardPath)
            End Using
            Using cardFill As New LinearGradientBrush(cardBounds,
                                                       Color.FromArgb(27, 18, 39),
                                                       Color.FromArgb(2, 6, 13),
                                                       LinearGradientMode.Vertical)
                graphics.FillPath(cardFill, cardPath)
            End Using
            Using border As New Pen(Color.FromArgb(210, 179, 72, 255), 1.2F)
                graphics.DrawPath(border, cardPath)
            End Using
        End Using

        Dim textLeft As Integer = cardBounds.Left + 92
        Dim textWidth As Integer = Math.Max(1, cardBounds.Right - textLeft - 22)
        Using eyebrowFont As New Font("Segoe UI Semibold", 8.0F, FontStyle.Bold)
            TextRenderer.DrawText(graphics,
                                  "B2S PRO WORKSPACE",
                                  eyebrowFont,
                                  New Rectangle(textLeft, cardBounds.Top + 36, textWidth, 20),
                                  Color.FromArgb(66, 205, 255),
                                  TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPrefix)
        End Using
        Using titleFont As New Font("Segoe UI Semibold", 17.0F, FontStyle.Bold)
            TextRenderer.DrawText(graphics,
                                  "Ready to create",
                                  titleFont,
                                  New Rectangle(textLeft, cardBounds.Top + 57, textWidth, 38),
                                  Color.White,
                                  TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPrefix)
        End Using
        Using bodyFont As New Font("Segoe UI", 9.0F, FontStyle.Regular)
            TextRenderer.DrawText(graphics,
                                  "Use File > New or File > Open to begin designing a backglass.",
                                  bodyFont,
                                  New Rectangle(textLeft, cardBounds.Top + 101, textWidth, 44),
                                  Color.FromArgb(185, 194, 211),
                                  TextFormatFlags.Left Or TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix)
        End Using

        Using accentLine As New LinearGradientBrush(New Rectangle(textLeft, cardBounds.Bottom - 30, Math.Max(1, textWidth - 12), 2),
                                                     Color.FromArgb(220, 45, 194, 255),
                                                     Color.FromArgb(220, 207, 73, 255),
                                                     LinearGradientMode.Horizontal)
            graphics.FillRectangle(accentLine, textLeft, cardBounds.Bottom - 30, Math.Max(1, textWidth - 12), 2)
        End Using
    End Sub

    Private Shared Function RoundedTabPath(bounds As Rectangle, radius As Integer) As GraphicsPath
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
    Protected Overrides Sub OnPaintBackground(e As System.Windows.Forms.PaintEventArgs)
        ' nothing to do        
    End Sub

    Private Sub B2STab_PreviewKeyDown(ByVal sender As Object, ByVal e As System.Windows.Forms.PreviewKeyDownEventArgs) Handles Me.PreviewKeyDown
        If e.KeyCode = Keys.Up OrElse e.KeyCode = Keys.Down OrElse e.KeyCode = Keys.Left OrElse e.KeyCode = Keys.Right OrElse
            e.KeyCode = Keys.Delete OrElse
            (e.KeyCode >= Keys.D1 AndAlso e.KeyCode <= Keys.D5) OrElse
            e.KeyCode = Keys.O OrElse e.KeyCode = Keys.R OrElse e.KeyCode = Keys.G OrElse e.KeyCode = Keys.B OrElse e.KeyCode = Keys.Y OrElse e.KeyCode = Keys.M OrElse e.KeyCode = Keys.P OrElse e.KeyCode = Keys.W Then
            If SelectedTabPage IsNot Nothing Then
                SelectedTabPage.Mouse.KeyIsPressed(e.Control, e.KeyCode)
            End If
        End If
    End Sub
    Private Sub B2STab_MouseUp(ByVal sender As Object, ByVal e As System.Windows.Forms.MouseEventArgs) Handles Me.MouseUp
        Dim I As Integer = 0
        For Each tabpage As B2STabPage In TabPages
            With tabpage
                If e.X >= .TabLocation.X AndAlso e.X <= .TabLocation.X + .TabSize.Width AndAlso e.Y >= .TabLocation.Y AndAlso e.Y <= .TabLocation.Y + .TabSize.Height Then
                    SelectedIndex = I
                    Exit For
                End If
            End With
            I += 1
        Next
    End Sub

    Private Sub EmptyWorkspaceNewButton_Click(sender As Object, e As EventArgs) Handles emptyWorkspaceNewButton.Click
        RaiseEvent NewProjectRequested(Me, EventArgs.Empty)
    End Sub

    Private Sub PositionEmptyWorkspaceNewButton()
        If emptyWorkspaceNewButton Is Nothing Then Return

        Dim y As Integer = Me.Height - tabheight + 3
        Dim workspaceBounds As New Rectangle(8, 8, Math.Max(1, Me.Width - 17), Math.Max(1, y - 17))
        Dim canShow As Boolean = AppThemeManager.DarkMode AndAlso TabPages.Count = 0 AndAlso
                                 workspaceBounds.Width >= 260 AndAlso workspaceBounds.Height >= 150
        emptyWorkspaceNewButton.Visible = canShow
        If Not canShow Then Return

        Dim cardWidth As Integer = Math.Min(520, workspaceBounds.Width - 56)
        Dim cardHeight As Integer = Math.Min(190, workspaceBounds.Height - 44)
        Dim cardLeft As Integer = workspaceBounds.Left + ((workspaceBounds.Width - cardWidth) \ 2)
        Dim cardTop As Integer = workspaceBounds.Top + ((workspaceBounds.Height - cardHeight) \ 2)
        emptyWorkspaceNewButton.Location = New Point(cardLeft + 48 - 30,
                                                     cardTop + (cardHeight \ 2) - 30)
        emptyWorkspaceNewButton.BringToFront()
    End Sub

    Private Sub B2STabPage_Scrolled(ByVal sender As Object, ByVal e As B2STabPage.B2STabPageScrollEventArgs)
        _offsetlocation = e.Location
    End Sub
    Private Sub B2STabPage_MouseDown(ByVal sender As Object, ByVal e As System.Windows.Forms.MouseEventArgs)
        RaiseEvent MouseDown(sender, e)
    End Sub
    Private Sub B2STabPage_MouseMove(ByVal sender As Object, ByVal e As Mouse.MouseMoveEventArgs)
        RaiseEvent MouseMove(sender, e)
    End Sub
    Private Sub B2STabPage_CopyDMDCopyArea(ByVal sender As Object, ByVal e As EventArgs)
        RaiseEvent CopyDMDCopyArea(sender, e)
    End Sub
    Private Sub B2STabPage_RemoveDMDCopyArea(ByVal sender As Object, ByVal e As EventArgs)
        RaiseEvent RemoveDMDCopyArea(sender, e)
    End Sub
    Private Sub B2STabPage_SelectedItemMoving(ByVal sender As Object, ByVal e As Mouse.MouseMoveEventArgs)
        RaiseEvent SelectedItemMoving(sender, e)
    End Sub
    Private Sub B2STabPage_SelectedItemRemoved(ByVal sender As Object, ByVal e As System.EventArgs)
        RaiseEvent SelectedItemRemoved(sender, e)
    End Sub
    Private Sub B2STabPage_SelectedBulbMoved(ByVal sender As Object, ByVal e As System.Windows.Forms.MouseEventArgs)
        RaiseEvent SelectedBulbMoved(sender, e)
    End Sub
    Private Sub B2STabPage_SelectedBulbEdited(ByVal sender As Object, ByVal e As System.EventArgs)
        RaiseEvent SelectedBulbEdited(sender, e)
    End Sub
    Private Sub B2STabPage_SelectedItemClicked(ByVal sender As Object, ByVal e As Mouse.MouseMoveEventArgs)
        RaiseEvent SelectedItemClicked(sender, e)
    End Sub

    Private Sub B2STabPage_LightsReportProgress(ByVal sender As Object, ByVal e As Illumination.Lights.LightsProgressEventArgs)
        RaiseEvent LightsReportProgress(sender, e)
    End Sub
    Private Sub B2STabPage_LightColorChanged(ByVal sender As Object, ByVal e As Illumination.Lights.LightColorChangedEventArgs)
        RaiseEvent LightColorChanged(sender, e)
    End Sub

    Private _selectedIndex As Integer = -1
    Public Property SelectedIndex() As Integer
        Get
            Return _selectedIndex
        End Get
        Set(ByVal value As Integer)
            If value >= 0 AndAlso value < TabPages.Count Then
                _selectedIndex = value
                For Each tabpage As B2STabPage In TabPages
                    tabpage.Visible = False
                Next
                Backglass.currentData = TabPages(_selectedIndex).BackglassData
                Backglass.currentTabPage = TabPages(_selectedIndex)
                TabPages(_selectedIndex).Visible = True
                Me.Invalidate()
            Else
                _selectedIndex = -1
                Backglass.currentData = Nothing
                Backglass.currentTabPage = Nothing
            End If
            RaiseEvent SelectedIndexChanged(Me, New EventArgs())
        End Set
    End Property
    Public ReadOnly Property SelectedTabPage() As B2STabPage
        Get
            Return If(SelectedIndex > -1, TabPages(SelectedIndex), Nothing)
        End Get
    End Property

    Private _tabpages As Generic.List(Of B2STabPage) = New Generic.List(Of B2STabPage)
    Public ReadOnly Property TabPages() As Generic.List(Of B2STabPage)
        Get
            Return _tabpages
        End Get
    End Property

    Private _offsetlocation As Point = New Point(0, 0)
    Public ReadOnly Property OffsetLocation() As Point
        Get
            Return _offsetlocation
        End Get
    End Property

    Public Sub New()
        ' set some styles
        Me.SetStyle(ControlStyles.ResizeRedraw Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.UserPaint, True)
        Me.UpdateStyles()
        Me.DoubleBuffered = True
        ' set padding
        Me.Padding = New Padding(0, 0, 0, tabheight)

        ' Use a real control for the startup-card action. The card itself is
        ' owner-drawn, but a child button guarantees normal WinForms click,
        ' keyboard-focus and cursor routing instead of relying on canvas hit tests.
        emptyWorkspaceNewButton = New WorkspaceNewButton() With {
            .Name = "btnEmptyWorkspaceNew",
            .Visible = False
        }
        Me.Controls.Add(emptyWorkspaceNewButton)
        PositionEmptyWorkspaceNewButton()
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        PositionEmptyWorkspaceNewButton()
    End Sub

    Public Sub AddBackglass(ByVal tabpage As B2STabPage)
        tabpage.Location = New Point(0, 0)
        tabpage.Dock = DockStyle.Fill
        tabpage.Visible = False
        AddHandler tabpage.Scrolled, AddressOf B2STabPage_Scrolled
        AddHandler tabpage.MyMouseDown, AddressOf B2STabPage_MouseDown
        AddHandler tabpage.MyMouseMove, AddressOf B2STabPage_MouseMove
        AddHandler tabpage.CopyDMDCopyArea, AddressOf B2STabPage_CopyDMDCopyArea
        AddHandler tabpage.RemoveDMDCopyArea, AddressOf B2STabPage_RemoveDMDCopyArea
        AddHandler tabpage.SelectedItemClicked, AddressOf B2STabPage_SelectedItemClicked
        AddHandler tabpage.SelectedItemMoving, AddressOf B2STabPage_SelectedItemMoving
        AddHandler tabpage.SelectedItemRemoved, AddressOf B2STabPage_SelectedItemRemoved
        AddHandler tabpage.LightsReportProgress, AddressOf B2STabPage_LightsReportProgress
        AddHandler tabpage.LightColorChanged, AddressOf B2STabPage_LightColorChanged
        AddHandler tabpage.SelectedBulbMoved, AddressOf B2STabPage_SelectedBulbMoved
        AddHandler tabpage.SelectedBulbEdited, AddressOf B2STabPage_SelectedBulbEdited
        TabPages.Add(tabpage)
        Me.Controls.Add(tabpage)
        PositionEmptyWorkspaceNewButton()
        OnResize(New EventArgs())
    End Sub
    Public Function RemoveBackglass(ByVal index As Integer) As DialogResult
        Dim ret As DialogResult = DialogResult.No
        If TabPages.Count > 0 AndAlso TabPages.Count > index Then
            Dim tabpage As B2STabPage = TabPages(index)
            If tabpage.BackglassData.IsDirty Then
                ret = B2SMessageBox.Show(My.Resources.MSG_IsDirty, AppTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question)
                If ret = DialogResult.Yes Then
                    Dim helper As Save = New Save()
                    helper.SaveData(tabpage.BackglassData)
                    RaiseEvent ProjectSavedOnClose(tabpage.BackglassData)
                ElseIf ret = DialogResult.No Then
                    RaiseEvent ProjectClosedWithoutSaving(tabpage.BackglassData)
                End If
            End If
            If ret = DialogResult.No Then
                RemoveHandler tabpage.Scrolled, AddressOf B2STabPage_Scrolled
                Me.Controls.Remove(tabpage)
                tabpage.Dispose()
                TabPages.RemoveAt(index)
                If TabPages.Count > 0 Then
                    Do While SelectedIndex >= TabPages.Count
                        SelectedIndex -= 1
                    Loop
                    If SelectedIndex = -1 AndAlso TabPages.Count > 0 Then
                        SelectedIndex = 0
                    End If
                    SelectedIndex = SelectedIndex
                Else
                    SelectedIndex = -1
                End If
                PositionEmptyWorkspaceNewButton()
                OnResize(New EventArgs())
            End If
        End If
        Return ret
    End Function

End Class
