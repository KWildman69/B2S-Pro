Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Reflection
Imports System.Windows.Forms

Public Class formAbout
    Inherits B2SThemedForm

    Private Shared ReadOnly releaseVersion As String = Diagnostics.FileVersionInfo.GetVersionInfo(GetType(formAbout).Assembly.Location).ProductVersion

    Private aboutContent As AboutSurface
    Private logoImage As Image
    Private avatarImage As Image
    Private avatarStream As MemoryStream

    Public Sub New()
        ConfigureAboutWindow()
    End Sub

    Private Sub ConfigureAboutWindow()
        SuspendLayout()
        Try
            Text = "About B2S Pro"
            BackColor = Color.FromArgb(1, 4, 9)
            ForeColor = Color.FromArgb(246, 248, 255)
            FormBorderStyle = FormBorderStyle.FixedDialog
            ClientSize = New Size(610, 555)
            MinimumSize = Size
            MaximumSize = Size
            StartPosition = FormStartPosition.CenterParent
            MaximizeBox = False
            MinimizeBox = False
            ShowInTaskbar = False
            AutoScaleMode = AutoScaleMode.Dpi

            logoImage = LoadStaticEmbeddedImage("B2SProLogo.png")
            avatarImage = LoadAnimatedEmbeddedImage("B2SAboutAvatar.gif", avatarStream)
            aboutContent = New AboutSurface(logoImage, avatarImage) With {
                .Dock = DockStyle.Fill
            }
            AddHandler aboutContent.CloseButton.Click, AddressOf CloseAboutWindow
            Controls.Add(aboutContent)
            AcceptButton = aboutContent.CloseButton
            CancelButton = aboutContent.CloseButton
        Finally
            ResumeLayout(True)
        End Try
    End Sub

    Private Sub CloseAboutWindow(sender As Object, e As EventArgs)
        DialogResult = DialogResult.Cancel
        Close()
    End Sub

    Private Shared Function LoadStaticEmbeddedImage(resourceName As String) As Image
        Try
            Using stream As Stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                If stream Is Nothing Then Return Nothing
                Using source As Image = Image.FromStream(stream)
                    Return New Bitmap(source)
                End Using
            End Using
        Catch
            Return Nothing
        End Try
    End Function

    Private Shared Function LoadAnimatedEmbeddedImage(resourceName As String, ByRef backingStream As MemoryStream) As Image
        Try
            Using stream As Stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                If stream Is Nothing Then Return Nothing
                backingStream = New MemoryStream()
                stream.CopyTo(backingStream)
                backingStream.Position = 0
                Return Image.FromStream(backingStream)
            End Using
        Catch
            If backingStream IsNot Nothing Then backingStream.Dispose()
            backingStream = Nothing
            Return Nothing
        End Try
    End Function

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        If avatarImage IsNot Nothing Then
            ImageAnimator.StopAnimate(avatarImage, Nothing)
            avatarImage.Dispose()
            avatarImage = Nothing
        End If
        If avatarStream IsNot Nothing Then
            avatarStream.Dispose()
            avatarStream = Nothing
        End If
        If logoImage IsNot Nothing Then
            logoImage.Dispose()
            logoImage = Nothing
        End If
        MyBase.OnFormClosed(e)
    End Sub

    Private NotInheritable Class AboutSurface
        Inherits Panel

        Private ReadOnly bannerLogo As Image
        Private ReadOnly avatarBox As PictureBox
        Private ReadOnly productFont As New Font("Segoe UI Semibold", 18.0F, FontStyle.Bold Or FontStyle.Italic)
        Private ReadOnly versionFont As New Font("Segoe UI", 10.0F, FontStyle.Regular)
        Private ReadOnly copyrightFont As New Font("Segoe UI", 9.5F, FontStyle.Regular)
        Private ReadOnly creditHeadingFont As New Font("Segoe UI Semibold", 8.5F, FontStyle.Bold)
        Private ReadOnly creditFont As New Font("Segoe UI", 9.5F, FontStyle.Regular)
        Private ReadOnly creditBoldFont As New Font("Segoe UI Semibold", 9.5F, FontStyle.Bold)

        Private ReadOnly closeButtonControl As Button

        Public ReadOnly Property CloseButton As Button
            Get
                Return closeButtonControl
            End Get
        End Property

        Public Sub New(logo As Image, avatar As Image)
            bannerLogo = logo
            DoubleBuffered = True
            ResizeRedraw = True
            BackColor = Color.FromArgb(1, 4, 9)

            avatarBox = New PictureBox() With {
                .BackColor = Color.Transparent,
                .Image = avatar,
                .SizeMode = PictureBoxSizeMode.Zoom,
                .TabStop = False
            }
            closeButtonControl = New Button() With {
                .Text = "Close",
                .DialogResult = DialogResult.Cancel,
                .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right,
                .Size = New Size(120, 36),
                .UseVisualStyleBackColor = False
            }
            Controls.Add(avatarBox)
            Controls.Add(closeButtonControl)
            LayoutAboutControls()
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            LayoutAboutControls()
            Invalidate()
        End Sub

        Private Sub LayoutAboutControls()
            avatarBox.SetBounds(38, 239, 75, 95)
            closeButtonControl.Location = New Point(Math.Max(30, ClientSize.Width - closeButtonControl.Width - 30),
                                                    Math.Max(490, ClientSize.Height - closeButtonControl.Height - 15))
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            Dim graphics As Graphics = e.Graphics
            graphics.SmoothingMode = SmoothingMode.AntiAlias
            graphics.CompositingQuality = CompositingQuality.HighQuality
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality
            graphics.Clear(Color.FromArgb(1, 4, 9))

            Dim width As Integer = ClientSize.Width
            If width <= 0 Then Return

            DrawBanner(graphics, width)
            DrawProductLine(graphics, width)
            DrawIdentity(graphics)
            DrawCreatorCredit(graphics, width)
        End Sub

        Private Sub DrawBanner(graphics As Graphics, width As Integer)
            Dim bannerBounds As New Rectangle(0, 0, width, 158)
            Using bannerBrush As New LinearGradientBrush(bannerBounds,
                                                          Color.FromArgb(7, 11, 19),
                                                          Color.FromArgb(1, 4, 9),
                                                          LinearGradientMode.Vertical)
                graphics.FillRectangle(bannerBrush, bannerBounds)
            End Using
            Using separator As New Pen(Color.FromArgb(20, 38, 62))
                graphics.DrawLine(separator, 0, bannerBounds.Bottom - 1, width, bannerBounds.Bottom - 1)
            End Using

            If bannerLogo IsNot Nothing Then
                Dim logoWidth As Integer = Math.Min(300, Math.Max(1, width - 110))
                Dim ratio As Double = CDbl(bannerLogo.Height) / Math.Max(1, bannerLogo.Width)
                Dim logoHeight As Integer = CInt(Math.Round(logoWidth * ratio))
                Dim logoBounds As New Rectangle((width - logoWidth) \ 2,
                                                (bannerBounds.Height - logoHeight) \ 2,
                                                logoWidth,
                                                logoHeight)
                graphics.DrawImage(bannerLogo, logoBounds)
            End If
        End Sub

        Private Sub DrawProductLine(graphics As Graphics, width As Integer)
            Dim productText As String = "B2S Pro"
            Dim productBounds As New RectangleF(30.0F, 174.0F, 130.0F, 38.0F)
            Using glowBrush As New SolidBrush(Color.FromArgb(80, 22, 140, 255))
                graphics.DrawString(productText, productFont, glowBrush, productBounds.X + 2.0F, productBounds.Y + 2.0F)
            End Using
            Using productBrush As New LinearGradientBrush(productBounds,
                                                           Color.White,
                                                           Color.FromArgb(58, 194, 255),
                                                           LinearGradientMode.Vertical)
                graphics.DrawString(productText, productFont, productBrush, productBounds.X, productBounds.Y)
            End Using
            Dim accentBounds As New Rectangle(31, 210, 108, 2)
            Using accentBrush As New LinearGradientBrush(accentBounds,
                                                          Color.FromArgb(39, 190, 255),
                                                          Color.FromArgb(207, 70, 255),
                                                          LinearGradientMode.Horizontal)
                graphics.FillRectangle(accentBrush, accentBounds)
            End Using
            Dim versionText As String = "Version " & releaseVersion
            Dim versionSize As SizeF = graphics.MeasureString(versionText, versionFont)
            Using versionBrush As New SolidBrush(Color.FromArgb(83, 212, 255))
                graphics.DrawString(versionText, versionFont, versionBrush,
                                    width - 30.0F - versionSize.Width, 184.0F)
            End Using
            Using separator As New Pen(Color.FromArgb(27, 43, 64))
                graphics.DrawLine(separator, 30, 224, Math.Max(30, width - 30), 224)
            End Using
        End Sub

        Private Sub DrawIdentity(graphics As Graphics)
            Using copyrightBrush As New SolidBrush(Color.FromArgb(214, 221, 235))
                graphics.DrawString("Copyright © 2026 Ken Wildman. All rights reserved.",
                                    copyrightFont, copyrightBrush, 125.0F, 278.0F)
            End Using
        End Sub

        Private Sub DrawCreatorCredit(graphics As Graphics, width As Integer)
            Dim panelBounds As New Rectangle(30, 350, Math.Max(1, width - 60), 126)
            Using panelPath As GraphicsPath = RoundedRectangle(panelBounds, 4)
                Using panelBrush As New SolidBrush(Color.FromArgb(7, 16, 29))
                    graphics.FillPath(panelBrush, panelPath)
                End Using
            End Using
            Using accentBrush As New SolidBrush(Color.FromArgb(41, 220, 138))
                graphics.FillRectangle(accentBrush, panelBounds.Left, panelBounds.Top, 4, panelBounds.Height)
            End Using
            Using headingBrush As New SolidBrush(Color.FromArgb(64, 233, 155))
                graphics.DrawString("ORIGINAL CREATOR CREDIT", creditHeadingFont, headingBrush,
                                    panelBounds.Left + 24.0F, panelBounds.Top + 20.0F)
            End Using

            Dim x As Single = panelBounds.Left + 24.0F
            Dim y As Single = panelBounds.Top + 44.0F
            Using textBrush As New SolidBrush(Color.FromArgb(238, 243, 252))
                DrawInline(graphics, "All credit and sincere thanks go to", creditFont, textBrush, x, y)
                x += 4.0F
                DrawInline(graphics, "Herweh and the B2S Team", creditBoldFont, textBrush, x, y)
                graphics.DrawString("for creating the original B2S Backglass Designer. Without their work,",
                                    creditFont, textBrush, panelBounds.Left + 24.0F, y + 22.0F)
                graphics.DrawString("dedication and vision, B2S Pro would not have been possible.", creditFont, textBrush,
                                    panelBounds.Left + 24.0F, y + 44.0F)
            End Using
        End Sub

        Private Shared Sub DrawInline(graphics As Graphics,
                                      text As String,
                                      font As Font,
                                      brush As Brush,
                                      ByRef x As Single,
                                      y As Single)
            Using format As StringFormat = CType(StringFormat.GenericTypographic.Clone(), StringFormat)
                graphics.DrawString(text, font, brush, x, y, format)
                x += graphics.MeasureString(text, font, Integer.MaxValue, format).Width
            End Using
        End Sub

        Private Shared Function RoundedRectangle(bounds As Rectangle, radius As Integer) As GraphicsPath
            Dim path As New GraphicsPath()
            Dim diameter As Integer = Math.Max(1, radius * 2)
            Dim arc As New Rectangle(bounds.X, bounds.Y, diameter, diameter)
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

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                productFont.Dispose()
                versionFont.Dispose()
                copyrightFont.Dispose()
                creditHeadingFont.Dispose()
                creditFont.Dispose()
                creditBoldFont.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Class
