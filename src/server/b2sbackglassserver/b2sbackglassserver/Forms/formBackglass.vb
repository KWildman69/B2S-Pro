#Disable Warning BC42016, BC42017, BC42018, BC42019, BC42032
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Windows.Forms
Imports Microsoft.Win32

Public Class formBackglass
    Inherits System.Windows.Forms.Form
    Private Declare Function SetForegroundWindow Lib "user32.dll" (ByVal hwnd As IntPtr) As Integer
    Private Declare Function IsWindow Lib "user32.dll" (ByVal hwnd As IntPtr) As Boolean

    Private Const minSize4Image As Integer = 300000

    Private B2SScreen As B2SScreen = Nothing  '  was New B2SScreen(), delayed to do later  - Westworld, 2016-11-18
    Private B2SLED As B2SLED = New B2SLED()
    Private B2SAnimation As B2SAnimation = New B2SAnimation()

    Private formDMD As formDMD = Nothing
    Private formSettings As formSettings = Nothing
    Private formMode As formMode = Nothing

    Private startupTimer As Timer = Nothing
    Private startupCoreCompleted As Boolean = False

#If B2S = "DLL" Then

    Private snifferTimer As Timer = Nothing
    Private snifferLamps As B2SSnifferPanel = Nothing
    Private snifferSolenoids As B2SSnifferPanel = Nothing
    Private snifferGIStrings As B2SSnifferPanel = Nothing
    Private chkSniffer As CheckBox = Nothing
    Private Const snifferTimerInterval As Integer = 311
#Else
    Private tableTimer As Timer = Nothing
    Private B2STimer As Timer = Nothing
    Private tableHandle As Integer = 0
#End If

    Private rotateTimer As Timer = Nothing
    Private rotateSlowDownSteps As Integer = 0
    Private rotateRunTillEnd As Boolean = False
    Private rotateRunToFirstStep As Boolean = False
    Private rotateSteps As Integer = 0
    Private rotateAngle As Single = 0
    Private rotateTimerInterval As Integer = 0

    Private Class SparseImageTile
        Public Image As Bitmap
        Public Offset As Point
        Public Sub New(ByVal image As Bitmap, ByVal offset As Point)
            Me.Image = image
            Me.Offset = offset
        End Sub
    End Class
    Private sparseImageTiles As New Generic.Dictionary(Of Image, Generic.List(Of SparseImageTile))()
    Private denseImages As New Generic.HashSet(Of Image)()
    Private behindCanvasCacheImage As Bitmap = Nothing
    Private behindCanvasCachePictureBox As B2SPictureBox = Nothing
    Private behindCanvasCacheBackground As Image = Nothing
    Private behindCanvasCacheBounds As Rectangle = Rectangle.Empty
    Private behindCanvasCacheAngle As Single = Single.NaN

    Private Sub ApplyB2SProTransparentCanvasBacking(ByVal imagesNode As Xml.XmlElement)
        ' Legacy directB2S files have no marker and keep the established black
        ' form backing. New B2S Pro exports opt in to the same neutral backing
        ' shown by the Designer preview so transparent canvas openings do not
        ' collapse to black at runtime.
        Me.BackColor = Color.Black
        If imagesNode Is Nothing OrElse imagesNode.Attributes("B2SProTransparentCanvasBacking") Is Nothing Then Return

        Dim parts() As String = imagesNode.Attributes("B2SProTransparentCanvasBacking").InnerText.Split("."c)
        If parts.Length <> 3 Then Return
        Dim red As Byte
        Dim green As Byte
        Dim blue As Byte
        If Byte.TryParse(parts(0), red) AndAlso Byte.TryParse(parts(1), green) AndAlso Byte.TryParse(parts(2), blue) Then
            Me.BackColor = Color.FromArgb(red, green, blue)
        End If
    End Sub

    Private Const MA_NOACTIVATE As System.Int32 = 3
    Private Const WM_MOUSEACTIVATE As Integer = &H21

#Region " Properties "
    Protected Overrides Sub WndProc(ByRef m As Message)
        'Don't allow the window to be activated by swallowing the mouse event.
        If B2SSettings.FormNoFocus And m.Msg = WM_MOUSEACTIVATE Then
            m.Result = New IntPtr(MA_NOACTIVATE)
            Return
        End If
        MyBase.WndProc(m)
    End Sub
#End Region 'Properties

#Region "constructor and closing"


    Public Sub New()

        InitializeComponent()

        ' set some styles
        Me.SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer, True)
        Me.DoubleBuffered = True

        ' set key preview to allow some key action
        Me.KeyPreview = True

#If B2S = "EXE" Then
        If My.Application.CommandLineArgs.Count > 0 Then
            B2SData.TableFileName = My.Application.CommandLineArgs(0).ToString

            If B2SData.TableFileName.EndsWith(".directb2s") Then
                B2SData.TableFileName = Path.GetFileNameWithoutExtension(B2SData.TableFileName)
                B2SSettings.PureEXE = True
                B2SSettings.GameName = String.Empty
                B2SSettings.B2SName = String.Empty
            Else
                Using regkey As RegistryKey = Registry.CurrentUser.OpenSubKey("Software\B2S")
                    B2SSettings.GameName = regkey.GetValue("B2SGameName", String.Empty)
                    B2SSettings.B2SName = regkey.GetValue("B2SB2SName", String.Empty)
                End Using
            End If

            If My.Application.CommandLineArgs.Count > 1 Then
                If My.Application.CommandLineArgs(1).ToString = "1" Then
                    Me.TopMost = True
                End If
            End If
        Else
            MessageBox.Show("Please do not start the EXE this way.", My.Resources.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error)
            End
        End If
        If My.Application.CommandLineArgs.Count > 2 Then
            B2SData.SwitchPulsePipeName = My.Application.CommandLineArgs(2).ToString()
            B2SData.StartPhysicsLauncherBridge(B2SData.SwitchPulsePipeName & "_Launcher")
        End If

        ' get the game name
        'B2SSettings.GameName = "bguns_l8"
        'B2SSettings.GameName = "closeenc"
        'B2SSettings.B2SName = "Baseball"
        'B2SSettings.B2SName = "Spider-Man(Stern 2007) alt full dmdON127"
        'B2SSettings.GameName = "smanve_101"
        'B2SData.TableFileName = "Spider-Man(Stern 2007) alt full dmdON127"

        ' Westworld 2016-18-11 - TableFileName is empty in some cases when launched via PinballX, we use GameName as alternativ
        If String.IsNullOrEmpty(B2SData.TableFileName) Then
            B2SData.TableFileName = B2SSettings.GameName
        End If

        If B2SSettings.CPUAffinityMask > 0 Then
            Dim Proc = Process.GetCurrentProcess
            Proc.ProcessorAffinity = B2SSettings.CPUAffinityMask
        End If
#Else
        Me.TopMost = True
#End If

        B2SScreen = New B2SScreen()
        ' load settings
        B2SSettings.Load()
        ' get B2S xml and start
        Try
            LoadB2SData()
            B2SScreen.ReadB2SSettingsFromFile()
        Catch ex As Exception
            If B2SSettings.ShowStartupError Then
                MessageBox.Show(ex.Message, My.Resources.AppTitle, Windows.Forms.MessageBoxButtons.OK, Windows.Forms.MessageBoxIcon.Error)
            End If
#If B2S = "DLL" Then
            Stop
#Else
            End
#End If
        End Try
        ' initialize screen settings

        InitB2SScreen()

        ' resize images
        ResizeSomeImages()

        ' show snippits
        ShowStartupSnippits()

        ' create 'image on' timer and start it
        startupTimer = New Timer()
        startupTimer.Interval = 2000
        AddHandler startupTimer.Tick, AddressOf StartupTimer_Tick
        startupTimer.Start()

#If B2S = "EXE" Then

        ' create 'table is still running' timer
        tableTimer = New Timer
        tableTimer.Interval = 207
        AddHandler tableTimer.Tick, AddressOf TableTimer_Tick

        ' create B2S data timer
        B2STimer = New Timer
        B2STimer.Interval = 13
        AddHandler B2STimer.Tick, AddressOf B2STimer_Tick
#End If

        ' create rotation timer
        rotateTimer = New Timer
        If rotateTimerInterval > 0 Then
            rotateTimer.Interval = rotateTimerInterval
        End If
        AddHandler rotateTimer.Tick, AddressOf RotateTimer_Tick

    End Sub
#If B2S = "DLL" Then
    Public Sub New(ByVal doNotLoadBackglassData As Boolean)

        InitializeComponent()

        ' set some styles
        Me.SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer, True)
        Me.DoubleBuffered = True

        ' set key peview to allow some key reaction
        Me.KeyPreview = True

        ' initialize screen settings
        InitB2SScreen()

        ' clear some statistics data
        B2SStatistics.ClearAll()

        ' create sniffer control for lamps
        snifferLamps = New B2SSnifferPanel
        snifferLamps.Location = New Point(5, 5)
        snifferLamps.Size = New Size(Me.Width / 2 - 13, Me.Height / 3 * 2)
        snifferLamps.OnOffMode = True
        snifferLamps.Data = B2SStatistics.LampsStats
        snifferLamps.Text = "Lam."
        Me.Controls.Add(snifferLamps)
        ' create sniffer control for solenoids
        snifferSolenoids = New B2SSnifferPanel
        snifferSolenoids.Location = New Point(Me.Width / 2 - 2, 5)
        snifferSolenoids.Size = New Size(Me.Width / 2 - 13, Me.Height / 3 * 2)
        snifferSolenoids.OnOffMode = True
        snifferSolenoids.Data = B2SStatistics.SolenoidsStats
        snifferSolenoids.Text = "Sol."
        Me.Controls.Add(snifferSolenoids)
        ' create sniffer control for gi strings
        snifferGIStrings = New B2SSnifferPanel
        snifferGIStrings.Location = New Point(5, Me.Height / 3 * 2 + 10)
        snifferGIStrings.Size = New Size(Me.Width - 20, Me.Height / 3 - 45)
        snifferGIStrings.OnOffMode = False
        snifferGIStrings.Data = B2SStatistics.GIStringsStats
        snifferGIStrings.Rows = 5
        snifferGIStrings.Text = "GI"
        Me.Controls.Add(snifferGIStrings)
        ' create check box
        chkSniffer = New CheckBox()
        chkSniffer.Location = New Point(20, Me.Height - 30)
        chkSniffer.AutoSize = True
        chkSniffer.Text = "Show statistics backglass after restart"
        chkSniffer.Checked = True
        AddHandler chkSniffer.CheckedChanged, AddressOf chkSniffer_CheckedChanged
        Me.Controls.Add(chkSniffer)

        ' create sniffer timer
        snifferTimer = New Timer()
        snifferTimer.Interval = snifferTimerInterval
        AddHandler snifferTimer.Tick, AddressOf SnifferTimer_Tick
        snifferTimer.Start()

    End Sub
#End If

    Private Sub formBackglass_Shown(sender As Object, e As System.EventArgs) Handles Me.Shown

#If B2S = "EXE" Then
        If Not B2SSettings.FormToFront Then
            Me.SendToBack()
        End If
        SetFocusToVPPlayer()
#Else
        If Not B2SSettings.FormToFront Then
            Me.SendToBack()
        Else
            Dim StoreTopMost As Boolean = Me.TopMost

            Me.TopMost = True
            Me.BringToFront()
            SetFocusToVPPlayer()
            Me.TopMost = StoreTopMost
        End If
#End If

    End Sub

    Private Sub formBackglass_FormClosing(sender As Object, e As System.Windows.Forms.FormClosingEventArgs) Handles Me.FormClosing

        On Error Resume Next
        If rotateTimer IsNot Nothing Then rotateTimer.Stop()
        If startupTimer IsNot Nothing Then startupTimer.Stop()

#If B2S = "DLL" Then
        ' stop all timers as DLL
        If snifferTimer IsNot Nothing Then snifferTimer.Stop()
#Else
        ' stop all timers as EXE
        If tableTimer IsNot Nothing Then tableTimer.Stop()
        If B2STimer IsNot Nothing Then B2STimer.Stop()
#End If
        ' unload DMD form stuff
        If formDMD IsNot Nothing Then
            formDMD.Close()
            formDMD.Dispose()
        End If

        If B2SScreen.formbackground IsNot Nothing Then
            B2SScreen.formbackground.Close()
            B2SScreen.formbackground.Dispose()
        End If

        ' unload mode form
        If formMode IsNot Nothing Then
            formMode.Close()
            formMode.Dispose()
        End If

        ' unload backglass form stuff
        For Each picbox As KeyValuePair(Of String, B2SPictureBox) In B2SData.Illuminations
            If picbox.Value IsNot Nothing Then
                If picbox.Value.BackgroundImage IsNot Nothing Then
                    picbox.Value.BackgroundImage.Dispose()
                    picbox.Value.BackgroundImage = Nothing
                End If
                If picbox.Value.OffImage IsNot Nothing Then
                    picbox.Value.OffImage.Dispose()
                    picbox.Value.OffImage = Nothing
                End If
                picbox.Value.Dispose()
            End If
        Next
        For Each led As KeyValuePair(Of String, Dream7Display) In B2SData.LEDDisplays
            led.Value.Dispose()
        Next
        For Each led As KeyValuePair(Of String, B2SLEDBox) In B2SData.LEDs
            led.Value.Dispose()
        Next
        For Each reel As KeyValuePair(Of String, B2SReelBox) In B2SData.Reels
            reel.Value.Dispose()
        Next
        If TopLightImage4Authentic IsNot Nothing Then
            TopLightImage4Authentic.Dispose()
            TopLightImage4Authentic = Nothing
        End If
        If TopLightImage4Fantasy IsNot Nothing Then
            TopLightImage4Fantasy.Dispose()
            TopLightImage4Fantasy = Nothing
        End If
        If SecondLightImage4Authentic IsNot Nothing Then
            SecondLightImage4Authentic.Dispose()
            SecondLightImage4Authentic = Nothing
        End If
        If SecondLightImage4Fantasy IsNot Nothing Then
            SecondLightImage4Fantasy.Dispose()
            SecondLightImage4Fantasy = Nothing
        End If
        If TopAndSecondLightImage4Authentic IsNot Nothing Then
            TopAndSecondLightImage4Authentic.Dispose()
            TopAndSecondLightImage4Authentic = Nothing
        End If
        If TopAndSecondLightImage4Fantasy IsNot Nothing Then
            TopAndSecondLightImage4Fantasy.Dispose()
            TopAndSecondLightImage4Fantasy = Nothing
        End If
        If DarkImage4Authentic IsNot Nothing Then
            DarkImage4Authentic.Dispose()
            DarkImage4Authentic = Nothing
        End If
        If DarkImage4Fantasy IsNot Nothing Then
            DarkImage4Fantasy.Dispose()
            DarkImage4Fantasy = Nothing
        End If
        If BackgroundImage IsNot Nothing Then
            BackgroundImage.Dispose()
            BackgroundImage = Nothing
        End If
        For Each tiles As Generic.List(Of SparseImageTile) In sparseImageTiles.Values
            For Each tile As SparseImageTile In tiles
                tile.Image.Dispose()
            Next
        Next
        sparseImageTiles.Clear()
        denseImages.Clear()
        If behindCanvasCacheImage IsNot Nothing Then
            behindCanvasCacheImage.Dispose()
            behindCanvasCacheImage = Nothing
        End If

        ' stop all animations
        StopAllAnimations()

        ' clean up some classes
        B2SData.ClearAll()
        B2SSettings.ClearAll()
        B2SStatistics.ClearAll()

    End Sub
    Private Sub formBackglass_Disposed(sender As Object, e As System.EventArgs) Handles Me.Disposed

        On Error Resume Next

        If rotateTimer IsNot Nothing Then RemoveHandler rotateTimer.Tick, AddressOf RotateTimer_Tick

        If startupTimer IsNot Nothing Then RemoveHandler startupTimer.Tick, AddressOf StartupTimer_Tick
#If B2S = "DLL" Then
        If snifferTimer IsNot Nothing Then RemoveHandler snifferTimer.Tick, AddressOf SnifferTimer_Tick
#Else
        If tableTimer IsNot Nothing Then RemoveHandler tableTimer.Tick, AddressOf TableTimer_Tick
        If B2STimer IsNot Nothing Then RemoveHandler B2STimer.Tick, AddressOf B2STimer_Tick
#End If

    End Sub
#End Region

#Region "painting"

    Protected Overrides Sub OnPaint(e As System.Windows.Forms.PaintEventArgs)
        'If B2sSettings.HideBackglass Then hide this form
        If B2SSettings.HideB2SBackglass Then
            Me.Hide()
            Return
        End If
#If B2S = "DLL" Then
        If B2SStatistics.LogStatistics Then
            ' invalidate the statistics controls
            DrawSniffer()

            Return
        End If
#End If
        ' some rendering hints
        e.Graphics.PageUnit = GraphicsUnit.Pixel
        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias

        ' draw background and illumination images
        If Me.BackgroundImage IsNot Nothing Then

            On Error Resume Next

            ' generate new clipping region
            Dim clip As Region = New Region(e.ClipRectangle)
            For Each ledarea As KeyValuePair(Of String, B2SData.LEDAreaInfo) In B2SData.LEDAreas
                If Not ledarea.Value.IsOnDMD Then
                    clip.Exclude(ledarea.Value.Rect)
                End If
            Next
            e.Graphics.SetClip(clip, Drawing2D.CombineMode.Replace)

            ' Draw behind-canvas snippets first. The stationary backglass is then
            ' composited over them, so its alpha masks the already-rotated artwork
            ' instead of a baked mask rotating with the artwork.
            If B2SData.Illuminations.Count > 0 Then
                For Each illu As KeyValuePair(Of String, B2SPictureBox) In B2SData.Illuminations
                    If illu.Value.BehindCanvas Then DrawImage(e, illu.Value, True)
                Next
            End If

            ' draw background image
            e.Graphics.DrawImage(Me.BackgroundImage, 0, 0)

            ' draw all visible and necessary images
            If B2SData.Illuminations.Count > 0 Then

                If Not B2SData.UseZOrder Then

                    ' draw all standard images
                    For Each illu As KeyValuePair(Of String, B2SPictureBox) In B2SData.Illuminations
                        DrawImage(e, illu.Value)
                    Next

                Else

                    ' first of all draw all standard images
                    For Each illu As KeyValuePair(Of String, B2SPictureBox) In B2SData.Illuminations
                        If illu.Value.ZOrder = 0 Then
                            DrawImage(e, illu.Value)
                        End If
                    Next
                    ' now draw zorderd images
                    For Each illus As KeyValuePair(Of Integer, B2SPictureBox()) In B2SData.ZOrderImages
                        'For Each illu As B2SPictureBox In illus.Value
                        '    DrawImage(e, illu)
                        'Next
                        For i As Integer = 0 To illus.Value.Length - 1
                            DrawImage(e, illus.Value(i))
                        Next
                    Next

                End If

            End If

            ' Score displays are child controls over the custom-painted canvas.
            ' Repaint them immediately after a full-screen animation frame so a
            ' pending parent redraw cannot temporarily erase their segments.
            For Each leddisplay As KeyValuePair(Of String, Dream7Display) In B2SData.LEDDisplays
                If leddisplay.Value.Parent Is Me AndAlso leddisplay.Value.Visible AndAlso
                   e.ClipRectangle.IntersectsWith(leddisplay.Value.Bounds) Then
                    leddisplay.Value.BringToFront()
                    leddisplay.Value.Invalidate()
                    leddisplay.Value.Update()
                End If
            Next

        End If
    End Sub

    Private Sub DrawCachedBehindCanvasImage(e As PaintEventArgs, picbox As B2SPictureBox)
        If picbox Is Nothing OrElse picbox.IsDisposed OrElse Not picbox.Visible OrElse
           picbox.BackgroundImage Is Nothing OrElse
           Not e.ClipRectangle.IntersectsWith(Rectangle.Round(picbox.RectangleF)) Then Return

        Dim bounds As Rectangle = Rectangle.Round(picbox.RectangleF)
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return

        ' Compose the rotating artwork with the canvas patch first. Drawing the
        ' result at its real Z-order keeps lower animation layers underneath it,
        ' while the canvas alpha still masks artwork that is behind the glass.
        Dim cacheIsCurrent As Boolean = (behindCanvasCacheImage IsNot Nothing AndAlso
                                         behindCanvasCachePictureBox Is picbox AndAlso
                                         behindCanvasCacheBounds = bounds AndAlso
                                         behindCanvasCacheAngle = picbox.VisualRotationAngle)
        If Not cacheIsCurrent Then
            If behindCanvasCacheImage IsNot Nothing Then behindCanvasCacheImage.Dispose()
            behindCanvasCacheImage = New Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb)
            behindCanvasCachePictureBox = picbox
            behindCanvasCacheBackground = Nothing
            behindCanvasCacheBounds = bounds
            behindCanvasCacheAngle = picbox.VisualRotationAngle

            Using graphics As Graphics = Graphics.FromImage(behindCanvasCacheImage)
                graphics.Clear(Color.Transparent)
                graphics.SmoothingMode = Drawing2D.SmoothingMode.HighSpeed
                graphics.InterpolationMode = Drawing2D.InterpolationMode.Bilinear
                graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighSpeed

                If picbox.NativeRotation OrElse picbox.MotionPathRollEnabled Then
                    Dim localRect As New RectangleF(0, 0, bounds.Width, bounds.Height)
                    Dim destination As PointF() = If(picbox.MotionPathRollEnabled AndAlso Not picbox.NativeRotation,
                                                     BallRollDestinationPoints(localRect, picbox.VisualRotationAngle),
                                                     RotationDestinationPoints(localRect, picbox.VisualRotationAngle))
                    graphics.DrawImage(picbox.BackgroundImage, destination,
                                       New RectangleF(0, 0, picbox.BackgroundImage.Width, picbox.BackgroundImage.Height),
                                       GraphicsUnit.Pixel)
                Else
                    graphics.DrawImage(picbox.BackgroundImage, New Rectangle(0, 0, bounds.Width, bounds.Height))
                End If
            End Using
        End If
        e.Graphics.DrawImageUnscaled(behindCanvasCacheImage, bounds.Location)
    End Sub
    Protected Overrides Sub OnPaintBackground(e As System.Windows.Forms.PaintEventArgs)

        ' maybe no background painting here
        If B2SStatistics.LogStatistics Then
            MyBase.OnPaintBackground(e)
        End If

    End Sub

    Private Sub DrawImage(e As System.Windows.Forms.PaintEventArgs, picbox As B2SPictureBox, Optional ByVal drawingBehindCanvas As Boolean = False)

        If picbox IsNot Nothing Then

            DrawPersistentMotionPathSource(e, picbox, drawingBehindCanvas)

            Dim paintBounds As Rectangle = PicturePaintBounds(picbox)
            Dim drawme As Boolean = (picbox.BehindCanvas = drawingBehindCanvas AndAlso Not B2SSettings.AllOut AndAlso picbox IsNot Nothing AndAlso Not picbox.IsDisposed AndAlso picbox.Visible AndAlso e.ClipRectangle.IntersectsWith(paintBounds))
            If drawme AndAlso B2SScreen.BackglassCutOff <> Nothing Then
                drawme = Not B2SScreen.BackglassCutOff.IntersectsWith(paintBounds)
            End If
            If drawme AndAlso picbox.RomID <> 0 AndAlso Not picbox.SetThruAnimation Then
                drawme = (picbox.RomID <> TopRomID OrElse picbox.RomIDType <> TopRomIDType OrElse picbox.RomInverted <> TopRomInverted) AndAlso (picbox.RomID <> SecondRomID OrElse picbox.RomIDType <> SecondRomIDType OrElse picbox.RomInverted <> SecondRomInverted)
            End If
            If drawme AndAlso B2SData.DualBackglass Then
                drawme = (picbox.DualMode = B2SData.eDualMode.Both OrElse picbox.DualMode = B2SSettings.CurrentDualMode)
            End If

            ' maybe write a drawing log
            If B2SSettings.IsPaintingLogOn AndAlso Not String.IsNullOrEmpty(B2SSettings.LogPath) Then
                ' write to log file
                On Error Resume Next
                Dim log As IO.StreamWriter = New IO.StreamWriter(IO.Path.Combine(B2SSettings.LogPath, "Drawing.txt"), True)
                log.WriteLine(DateTime.Now & ": " & picbox.Name & ", Visible=" & picbox.Visible.ToString() & ", DrawMe=" & drawme.ToString() & ", Rect=" & Rectangle.Round(picbox.RectangleF).ToString())
                log.Flush()
                log.Close()
            End If

            If drawme Then
                Dim drawImage As Image = picbox.BackgroundImage
                If B2SData.OnAndOffImage Then
                    If B2SData.IsOffImageVisible AndAlso picbox.OffImage IsNot Nothing Then
                        drawImage = picbox.OffImage
                    End If
                End If
                If drawImage IsNot Nothing AndAlso (picbox.NativeRotation OrElse picbox.MotionPathRollEnabled) Then
                    Dim directFrameDraw As Boolean = (picbox.NativeRotation AndAlso Not picbox.MotionPathRollEnabled AndAlso picbox.NativeRotationFramePlayback AndAlso
                        drawImage.Width = CInt(picbox.RectangleF.Width) AndAlso
                        drawImage.Height = CInt(picbox.RectangleF.Height))
                    If drawingBehindCanvas Then
                        DrawCachedBehindCanvasImage(e, picbox)
                    ElseIf picbox.NativeRotation AndAlso Not picbox.MotionPathRollEnabled AndAlso Not picbox.PivotRotation AndAlso picbox.NativeRotationFramePlayback AndAlso
                           drawImage.Width = CInt(picbox.RectangleF.Width) AndAlso
                           drawImage.Height = CInt(picbox.RectangleF.Height) Then
                        ' Cached frames are already rotated and sized. Avoid the
                        ' general three-point GDI+ transform path, which is very
                        ' expensive for large alpha images even at zero degrees.
                        e.Graphics.DrawImageUnscaled(drawImage,
                            CInt(picbox.RectangleF.Left),
                            CInt(picbox.RectangleF.Top))
                    Else
                        Dim state As Drawing2D.GraphicsState = e.Graphics.Save()
                        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.HighSpeed
                        e.Graphics.InterpolationMode = Drawing2D.InterpolationMode.Bilinear
                        e.Graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighSpeed
                        Dim pivotX As Single = If(picbox.NativeRotation, picbox.RotationPivotX, 0.5F)
                        Dim pivotY As Single = If(picbox.NativeRotation, picbox.RotationPivotY, 0.5F)
                        Dim destination As PointF() = If(picbox.MotionPathRollEnabled AndAlso Not picbox.NativeRotation,
                                                         BallRollDestinationPoints(picbox.RectangleF, picbox.VisualRotationAngle),
                                                         RotationDestinationPoints(picbox.RectangleF, picbox.VisualRotationAngle, pivotX, pivotY))
                        e.Graphics.DrawImage(drawImage, destination,
                                             New RectangleF(0, 0, drawImage.Width, drawImage.Height),
                                             GraphicsUnit.Pixel)
                        e.Graphics.Restore(state)
                    End If
                ElseIf drawImage IsNot Nothing Then
                    Dim tiles As Generic.List(Of SparseImageTile) = GetSparseImageTiles(drawImage)
                    If tiles Is Nothing Then
                        e.Graphics.DrawImage(drawImage, picbox.RectangleF.Location)
                    Else
                        For Each tile As SparseImageTile In tiles
                            e.Graphics.DrawImageUnscaled(tile.Image,
                                CInt(picbox.RectangleF.Left) + tile.Offset.X,
                                CInt(picbox.RectangleF.Top) + tile.Offset.Y)
                        Next
                    End If
                End If
            End If

        End If

    End Sub

    Private Sub DrawPersistentMotionPathSource(e As PaintEventArgs, picbox As B2SPictureBox, drawingBehindCanvas As Boolean)
        If picbox Is Nothing OrElse Not picbox.IsMotionPathSourceSeparate OrElse
           picbox.BehindCanvas <> drawingBehindCanvas OrElse B2SSettings.AllOut OrElse
           picbox.MotionPathSourceImage Is Nothing Then Return

        Dim sourceBounds As Rectangle = Rectangle.Round(picbox.MotionPathSourceRectangle)
        If sourceBounds.IsEmpty OrElse Not e.ClipRectangle.IntersectsWith(sourceBounds) Then Return
        If B2SScreen.BackglassCutOff <> Nothing AndAlso B2SScreen.BackglassCutOff.IntersectsWith(sourceBounds) Then Return
        If B2SData.DualBackglass AndAlso picbox.DualMode <> B2SData.eDualMode.Both AndAlso picbox.DualMode <> B2SSettings.CurrentDualMode Then Return

        Dim drawImage As Image = picbox.MotionPathSourceImage
        If B2SData.OnAndOffImage AndAlso B2SData.IsOffImageVisible AndAlso picbox.OffImage IsNot Nothing Then drawImage = picbox.OffImage
        If drawImage Is Nothing Then Return

        If picbox.MotionPathRollEnabled AndAlso Math.Abs(picbox.MotionPathSourceRollAngle) > 0.001F Then
            Dim state As Drawing2D.GraphicsState = e.Graphics.Save()
            e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.HighSpeed
            e.Graphics.InterpolationMode = Drawing2D.InterpolationMode.Bilinear
            e.Graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighSpeed
            e.Graphics.DrawImage(drawImage, BallRollDestinationPoints(picbox.MotionPathSourceRectangle, picbox.MotionPathSourceRollAngle),
                                 New RectangleF(0, 0, drawImage.Width, drawImage.Height), GraphicsUnit.Pixel)
            e.Graphics.Restore(state)
            Return
        End If

        Dim tiles As Generic.List(Of SparseImageTile) = GetSparseImageTiles(drawImage)
        If tiles Is Nothing Then
            e.Graphics.DrawImage(drawImage, picbox.MotionPathSourceRectangle.Location)
        Else
            For Each tile As SparseImageTile In tiles
                e.Graphics.DrawImageUnscaled(tile.Image, sourceBounds.Left + tile.Offset.X, sourceBounds.Top + tile.Offset.Y)
            Next
        End If
    End Sub

    Private Function PicturePaintBounds(ByVal picbox As B2SPictureBox) As Rectangle
        If picbox Is Nothing OrElse picbox.RectangleF.IsEmpty Then Return Rectangle.Empty
        If picbox.MotionPathRollEnabled AndAlso Not picbox.NativeRotation Then
            Dim radians As Double = picbox.VisualRotationAngle * Math.PI / 180.0R
            Dim factor As Single = CSng(Math.Abs(Math.Cos(radians)) + Math.Abs(Math.Sin(radians)))
            Dim centerX As Single = picbox.RectangleF.Left + picbox.RectangleF.Width / 2.0F
            Dim centerY As Single = picbox.RectangleF.Top + picbox.RectangleF.Height / 2.0F
            Dim transformed As Rectangle = Rectangle.Ceiling(New RectangleF(
                centerX - picbox.RectangleF.Width * factor / 2.0F,
                centerY - picbox.RectangleF.Height * factor / 2.0F,
                picbox.RectangleF.Width * factor,
                picbox.RectangleF.Height * factor))
            Dim rollingBounds As Rectangle = Rectangle.Union(Rectangle.Ceiling(picbox.RectangleF), transformed)
            rollingBounds.Inflate(4, 4)
            Return rollingBounds
        End If
        If (Not picbox.NativeRotation AndAlso Not picbox.MotionPathRollEnabled) OrElse Math.Abs(picbox.VisualRotationAngle) < 0.001F Then Return Rectangle.Round(picbox.RectangleF)

        Dim pivotX As Single = If(picbox.NativeRotation, picbox.RotationPivotX, 0.5F)
        Dim pivotY As Single = If(picbox.NativeRotation, picbox.RotationPivotY, 0.5F)
        Dim points As PointF() = RotationDestinationPoints(picbox.RectangleF, picbox.VisualRotationAngle,
                                                           pivotX, pivotY)
        If points Is Nothing OrElse points.Length < 3 Then Return Rectangle.Round(picbox.RectangleF)
        Dim fourth As New PointF(points(1).X + points(2).X - points(0).X,
                                 points(1).Y + points(2).Y - points(0).Y)
        Dim left As Single = Math.Min(Math.Min(points(0).X, points(1).X), Math.Min(points(2).X, fourth.X))
        Dim top As Single = Math.Min(Math.Min(points(0).Y, points(1).Y), Math.Min(points(2).Y, fourth.Y))
        Dim right As Single = Math.Max(Math.Max(points(0).X, points(1).X), Math.Max(points(2).X, fourth.X))
        Dim bottom As Single = Math.Max(Math.Max(points(0).Y, points(1).Y), Math.Max(points(2).Y, fourth.Y))
        Dim result As Rectangle = Rectangle.Ceiling(RectangleF.FromLTRB(left, top, right, bottom))
        result.Inflate(2, 2)
        Return result
    End Function

    Private Function GetSparseImageTiles(ByVal image As Image) As Generic.List(Of SparseImageTile)
        Const minimumSparseImagePixels As Integer = 1000000
        Const tileSize As Integer = 128
        If image Is Nothing OrElse image.Width * image.Height < minimumSparseImagePixels Then Return Nothing

        If sparseImageTiles.ContainsKey(image) Then Return sparseImageTiles(image)
        If denseImages.Contains(image) Then Return Nothing
        Dim bitmap As Bitmap = TryCast(image, Bitmap)
        If bitmap Is Nothing Then Return Nothing

        Dim tiles As New Generic.List(Of SparseImageTile)()
        Dim fullRect As New Rectangle(0, 0, bitmap.Width, bitmap.Height)
        Dim converted As Bitmap = Nothing
        If bitmap.PixelFormat <> PixelFormat.Format32bppArgb AndAlso
           bitmap.PixelFormat <> PixelFormat.Format32bppPArgb Then
            converted = New Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb)
            Using g As Graphics = Graphics.FromImage(converted)
                g.DrawImageUnscaled(bitmap, 0, 0)
            End Using
            bitmap = converted
        End If

        Dim visibleTileRects As New Generic.List(Of Rectangle)()
        Dim data As BitmapData = bitmap.LockBits(fullRect, ImageLockMode.ReadOnly, bitmap.PixelFormat)
        Try
            Dim stride As Integer = Math.Abs(data.Stride)
            Dim bytes(stride * bitmap.Height - 1) As Byte
            Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length)
            For top As Integer = 0 To bitmap.Height - 1 Step tileSize
                For left As Integer = 0 To bitmap.Width - 1 Step tileSize
                    Dim width As Integer = Math.Min(tileSize, bitmap.Width - left)
                    Dim height As Integer = Math.Min(tileSize, bitmap.Height - top)
                    Dim hasVisiblePixel As Boolean = False
                    For y As Integer = top To top + height - 1
                        Dim row As Integer = y * stride
                        For x As Integer = left To left + width - 1
                            If bytes(row + x * 4 + 3) <> 0 Then
                                hasVisiblePixel = True
                                Exit For
                            End If
                        Next
                        If hasVisiblePixel Then Exit For
                    Next
                    If hasVisiblePixel Then
                        visibleTileRects.Add(New Rectangle(left, top, width, height))
                    End If
                Next
            Next
        Finally
            bitmap.UnlockBits(data)
        End Try

        ' The source bitmap must be unlocked before GDI+ can copy from it.
        For Each tileRect As Rectangle In visibleTileRects
            Dim tile As New Bitmap(tileRect.Width, tileRect.Height, PixelFormat.Format32bppPArgb)
            Using g As Graphics = Graphics.FromImage(tile)
                g.DrawImage(bitmap, New Rectangle(0, 0, tileRect.Width, tileRect.Height),
                            tileRect, GraphicsUnit.Pixel)
            End Using
            tiles.Add(New SparseImageTile(tile, tileRect.Location))
        Next
        If converted IsNot Nothing Then converted.Dispose()

        ' Dense images are faster as one normal draw. Sparse animation frames
        ' win when fewer than half their tiles contain any visible pixels.
        Dim totalTileCount As Integer = CInt(Math.Ceiling(image.Width / CDbl(tileSize)) *
                                             Math.Ceiling(image.Height / CDbl(tileSize)))
        If tiles.Count * 2 >= totalTileCount Then
            For Each tile As SparseImageTile In tiles
                tile.Image.Dispose()
            Next
            denseImages.Add(image)
            Return Nothing
        End If
        sparseImageTiles.Add(image, tiles)
        Return tiles
    End Function

    Private Function RotationDestinationPoints(ByVal rect As RectangleF, ByVal angle As Single,
                                               Optional ByVal pivotX As Single = 0.5F,
                                               Optional ByVal pivotY As Single = 0.5F) As PointF()
        Dim radians As Double = angle * Math.PI / 180.0R
        Dim cosine As Double = Math.Cos(radians)
        Dim sine As Double = Math.Sin(radians)
        Dim centerX As Single = rect.Left + rect.Width * pivotX
        Dim centerY As Single = rect.Top + rect.Height * pivotY
        Return New PointF() {
            RotationDestinationPoint(centerX, centerY, rect.Width, rect.Height, -pivotX, -pivotY, cosine, sine),
            RotationDestinationPoint(centerX, centerY, rect.Width, rect.Height, 1.0R - pivotX, -pivotY, cosine, sine),
            RotationDestinationPoint(centerX, centerY, rect.Width, rect.Height, -pivotX, 1.0R - pivotY, cosine, sine)
        }
    End Function

    Private Function BallRollDestinationPoints(ByVal rect As RectangleF, ByVal angle As Single) As PointF()
        Dim radians As Double = angle * Math.PI / 180.0R
        Dim cosine As Double = Math.Cos(radians)
        Dim sine As Double = Math.Sin(radians)
        Dim centerX As Single = rect.Left + rect.Width / 2.0F
        Dim centerY As Single = rect.Top + rect.Height / 2.0F
        Return New PointF() {
            BallRollDestinationPoint(centerX, centerY, rect.Width, rect.Height, -0.5R, -0.5R, cosine, sine),
            BallRollDestinationPoint(centerX, centerY, rect.Width, rect.Height, 0.5R, -0.5R, cosine, sine),
            BallRollDestinationPoint(centerX, centerY, rect.Width, rect.Height, -0.5R, 0.5R, cosine, sine)
        }
    End Function

    Private Function BallRollDestinationPoint(ByVal centerX As Single, ByVal centerY As Single,
                                              ByVal width As Single, ByVal height As Single,
                                              ByVal x As Double, ByVal y As Double,
                                              ByVal cosine As Double, ByVal sine As Double) As PointF
        Return New PointF(CSng(centerX + width * (x * cosine - y * sine)),
                          CSng(centerY + height * (x * sine + y * cosine)))
    End Function

    Private Function RotationDestinationPoint(ByVal centerX As Single, ByVal centerY As Single,
                                              ByVal width As Single, ByVal height As Single,
                                              ByVal x As Double, ByVal y As Double,
                                              ByVal cosine As Double, ByVal sine As Double) As PointF
        Dim pixelX As Double = width * x
        Dim pixelY As Double = height * y
        Return New PointF(CSng(centerX + pixelX * cosine - pixelY * sine),
                          CSng(centerY + pixelX * sine + pixelY * cosine))
    End Function

#If B2S = "DLL" Then

    Private Sub DrawSniffer()

        ' invalidate the statistics controls
        snifferLamps.Invalidate()
        snifferSolenoids.Invalidate()
        snifferGIStrings.Invalidate()

    End Sub
#End If
#End Region

#Region "some timer events"

#If B2S = "DLL" Then
    Private Sub SnifferTimer_Tick(ByVal sender As Object, ByVal e As EventArgs)

        If B2SStatistics.LogStatistics Then
            Me.Invalidate()
        End If

    End Sub
#End If
    Private Sub StartupTimer_Tick(ByVal sender As Object, ByVal e As EventArgs)

        startupTimer.Stop()

        ' Cache generation is asynchronous. After normal startup has completed,
        ' use this timer only as a lightweight barrier so every continuous native
        ' rotation begins together after all of its prepared frames are installed.
        If startupCoreCompleted Then
            If Not TryStartContinuousNativeRotations() Then startupTimer.Start()
            Return
        End If

        ' set focus to the VP player
        SetFocusToVPPlayer()

        ' maybe show some 'startup on' images
        ShowStartupImages()

        ' start autostarted animations
        B2SAnimation.AutoStart()

        startupCoreCompleted = True
        startupTimer.Interval = 25
        If Not TryStartContinuousNativeRotations() Then startupTimer.Start()
        If B2SData.RotatingPictureBox.ContainsKey(0) AndAlso B2SData.RotatingPictureBox(0).AutoStartRotation Then StartRotation()

        ' set focus to the VP player
        SetFocusToVPPlayer()

#If B2S = "EXE" Then
        ' start B2S data timer
        B2STimer.Start()

        ' start table check timer
        tableTimer.Start()
#End If
    End Sub

    Private Function TryStartContinuousNativeRotations() As Boolean
        For Each illumination As KeyValuePair(Of String, B2SPictureBox) In B2SData.Illuminations
            If illumination.Value.NativeRotation AndAlso illumination.Value.AutoStartRotation AndAlso
               Not illumination.Value.NativeRotationFramePlayback Then Return False
        Next
        For Each illumination As KeyValuePair(Of String, B2SPictureBox) In B2SData.DMDIlluminations
            If illumination.Value.NativeRotation AndAlso illumination.Value.AutoStartRotation AndAlso
               Not illumination.Value.NativeRotationFramePlayback Then Return False
        Next

        For Each illumination As KeyValuePair(Of String, B2SPictureBox) In B2SData.Illuminations
            If illumination.Value.NativeRotation AndAlso illumination.Value.AutoStartRotation Then
                illumination.Value.StartNativeRotation()
            End If
        Next
        For Each illumination As KeyValuePair(Of String, B2SPictureBox) In B2SData.DMDIlluminations
            If illumination.Value.NativeRotation AndAlso illumination.Value.AutoStartRotation Then
                illumination.Value.StartNativeRotation()
            End If
        Next
        Return True
    End Function

#If B2S = "EXE" Then
    Private Sub TableTimer_Tick(ByVal sender As Object, ByVal e As EventArgs)

        If tableHandle <> 0 AndAlso Not IsWindow(tableHandle) Then
            ' get out here
            tableTimer.Stop()
            Me.Close()
            Me.Dispose()
        End If

    End Sub
    Private Sub B2STimer_Tick(ByVal sender As Object, ByVal e As EventArgs)

        ' poll registry data
        PollingData()

    End Sub

#End If
    Private Sub RotateTimer_Tick(ByVal sender As Object, ByVal e As EventArgs)

        Static currentAngleS As Single = 0
        Static currentAngle As Integer = 0

        ' Native self rotation keeps one bitmap and changes only its paint
        ' transform. Legacy mechanical rotation continues to use indexed images.
        If B2SData.RotatingPictureBox.ContainsKey(0) AndAlso
           B2SData.RotatingPictureBox(0).NativeRotation Then

            B2SData.RotatingPictureBox(0).RotationAngle = If(B2SData.RotatingPictureBox(0).RotationDirection = B2SPictureBox.eSnippitRotationDirection.AntiClockwise, -currentAngle, currentAngle)
            B2SData.RotatingPictureBox(0).Visible = True
            B2SData.RotatingPictureBox(0).Parent.Invalidate(Rectangle.Round(B2SData.RotatingPictureBox(0).RectangleF))

            currentAngleS += rotateAngle
            If currentAngleS >= 360 Then currentAngleS = 0
            currentAngle = CInt(currentAngleS)

        ElseIf B2SData.RotatingImages.ContainsKey(0) AndAlso B2SData.RotatingImages(0).ContainsKey(currentAngle) Then

            B2SData.RotatingPictureBox(0).BackgroundImage = B2SData.RotatingImages(0)(currentAngle)
            B2SData.RotatingPictureBox(0).Visible = True

            currentAngleS += rotateAngle
            If currentAngleS >= 360 Then currentAngleS = 0
            currentAngle = CInt(currentAngleS)

        Else

            rotateTimer.Stop()

        End If

        ' mabye slow down the rotation or stop it at a certain step
        If rotateSlowDownSteps > 0 Then

            Const lastStep As Integer = 25
            Const add2IntervalPerStep As Integer = 3

            If rotateSlowDownSteps >= lastStep Then
                rotateTimer.Stop()
                rotateSlowDownSteps = 0
                rotateTimer.Interval = rotateTimerInterval
            Else
                rotateSlowDownSteps += 1
                rotateTimer.Interval += add2IntervalPerStep
            End If

        ElseIf rotateRunTillEnd Then

            If currentAngleS + rotateAngle >= 360 Then
                rotateTimer.Stop()
            End If

        ElseIf rotateRunToFirstStep Then

            If currentAngleS = 0 Then
                rotateTimer.Stop()
            End If

        End If

    End Sub

#End Region

#Region "polling action B2SBackglassServerEXE"
#If B2S = "EXE" Then
    Private isVisibleStateSet As Boolean = False
    Private lastTopVisible As Boolean = False
    Private lastSecondVisible As Boolean = False

    Private pollingInit As Boolean = False
    Private lamps(400) As Integer
    Private solenoids(400) As Integer
    Private motionPathSolenoids(400) As Integer
    Private motionPathLamps(400) As Integer
    Private motionPathB2SStates(250) As Integer
    Private gistrings(400) As Integer
    Private b2sSets(400) As Integer
    Private mechs(5) As Integer
    Private leds(100) As Integer

    Private animations As Generic.Dictionary(Of String, Integer) = Nothing
    Private lastRandomStartedAnimation As String = String.Empty

    Private rotation As Integer = 0

    Private sounds As Generic.Dictionary(Of String, Integer) = Nothing

    Private Sub PollingData()

        ' initialize the value storage - this storage is to avoid too much update traffic
        InitializePollArrays()

        ' open registry sub key
        Using regkey As RegistryKey = Registry.CurrentUser.OpenSubKey("Software\B2S", True)

            ' get current data
            Dim lampsData As String = GetLampsPollingData(regkey)
            Dim solenoidsData As String = GetSolenoidsPollingData(regkey)
            Dim gistringsData As String = GetGIStringsPollingData(regkey)
            Dim b2sSetsData As String = GetB2SSetsPollingData(regkey)
            Dim mechsData As Integer() = GetMechPollingData(regkey)
            Dim animationsdata As String = GetAnimationsPollingData(regkey)
            Dim rotationsdata As String = GetRotationsPollingData(regkey)
            Dim soundsdata As String = GetSoundsPollingData(regkey)

            ' first of all have a look at the both top images
            Dim topVisible As Boolean = lastTopVisible
            topVisible = GetLampsTopVisible(lampsData, topVisible)
            topVisible = GetSolenoidsTopVisible(solenoidsData, topVisible)
            topVisible = GetGIStringsTopVisible(gistringsData, topVisible)
            topVisible = GetB2SSetsTopVisible(b2sSetsData, topVisible)
            Dim secondVisible As Boolean = lastSecondVisible
            secondVisible = GetLampsSecondVisible(lampsData, secondVisible)
            secondVisible = GetSolenoidsSecondVisible(solenoidsData, secondVisible)
            secondVisible = GetGIStringsSecondVisible(gistringsData, secondVisible)
            secondVisible = GetB2SSetsSecondVisible(b2sSetsData, secondVisible)
            ' maybe show or hide top images
            If lastTopVisible <> topVisible OrElse lastSecondVisible <> secondVisible OrElse Not isVisibleStateSet Then
                B2SData.IsOffImageVisible = False
                isVisibleStateSet = True
                lastTopVisible = topVisible
                lastSecondVisible = secondVisible
                If topVisible AndAlso secondVisible Then
                    BackgroundImage = TopAndSecondLightImage
                ElseIf topVisible Then
                    BackgroundImage = TopLightImage
                ElseIf secondVisible Then
                    BackgroundImage = SecondLightImage
                Else
                    BackgroundImage = DarkImage
                    B2SData.IsOffImageVisible = True
                End If
            End If

            ' get thru all lamps
            GetThruAllLamps(lampsData)

            ' get thru all solenoids
            GetThruAllSolenoids(solenoidsData)

            ' get thru all gistrings
            GetThruAllGIStrings(gistringsData)

            ' get thru all B2SSetData
            GetThruAllB2SData(b2sSetsData)

            ' get thru all mechs
            GetThruAllMechs(mechsData)

            ' get thru all LEDs
            GetThruAllLEDs(regkey)

            ' get thru all animations
            GetThruAllAnimations(regkey, animationsdata)

            ' get thru all rotations
            GetThruAllRotations(regkey, rotationsdata)

            ' get thru all sounds
            GetThruAllSounds(regkey, soundsdata)

            ' maybe hide score display
            GetThruAllScoreDisplays(regkey)

            ' maybe show or hide some illus by groupname
            GetThruAllIlluGroups(regkey)

            ' maybe position something
            GetThruAllPositions(regkey)

        End Using

    End Sub

    Private Sub InitializePollArrays()

        If Not pollingInit Then

            pollingInit = True

            For I As Integer = 0 To 250
                lamps(I) = 0
            Next
            For I As Integer = 0 To 250
                solenoids(I) = 0
            Next
            For I As Integer = 0 To 250
                gistrings(I) = 0
            Next
            For I As Integer = 0 To 250
                b2sSets(I) = 0
            Next
            For I As Integer = 0 To 5
                mechs(I) = -1
            Next
            For I As Integer = 0 To 100
                leds(I) = 0
            Next
            animations = New Generic.Dictionary(Of String, Integer)
            sounds = New Generic.Dictionary(Of String, Integer)

        End If

    End Sub

    Private Function GetLampsPollingData(ByVal regkey As RegistryKey) As String

        Dim lampsData As String = String.Empty

        If B2SData.UseRomLamps OrElse B2SData.UseAnimationLamps OrElse B2SData.UsedMotionPathLampIDs.Count > 0 OrElse B2SData.UsedMotionPathRemoveLampIDs.Count > 0 OrElse B2SData.PivotLampIDs.Count > 0 Then
            lampsData = regkey.GetValue("B2SLamps", New String("0", 401))
            'If B2SSettings.IsROMControlled AndAlso lampsData.Contains("2") Then
            '    regkey.SetValue("B2SLamps", lampsData.Replace("2", "0"))
            'End If
        End If

        Return lampsData

    End Function
    Private Function GetSolenoidsPollingData(ByVal regkey As RegistryKey) As String

        Dim solenoidsData As String = String.Empty

        If B2SData.UseRomSolenoids OrElse B2SData.UseAnimationSolenoids OrElse B2SData.UsedMotionPathSolenoidIDs.Count > 0 OrElse B2SData.UsedMotionPathRemoveSolenoidIDs.Count > 0 OrElse B2SData.PivotSolenoidIDs.Count > 0 Then
            solenoidsData = regkey.GetValue("B2SSolenoids", New String("0", 251))
            If B2SSettings.IsROMControlled AndAlso solenoidsData.Contains("2") Then
                regkey.SetValue("B2SSolenoids", solenoidsData.Replace("2", "0"))
            End If
        End If

        Return solenoidsData

    End Function
    Private Function GetGIStringsPollingData(ByVal regkey As RegistryKey) As String

        Dim gistringsData As String = String.Empty

        If B2SData.UseRomGIStrings OrElse B2SData.UseAnimationGIStrings Then
            gistringsData = regkey.GetValue("B2SGIStrings", New String("0", 251))
        End If

        Return gistringsData

    End Function
    Private Function GetB2SSetsPollingData(ByVal regkey As RegistryKey) As String

        Dim b2sSetsData As String = String.Empty

        If B2SData.UseRomLamps OrElse B2SData.UseAnimationLamps OrElse B2SData.UsedMotionPathB2SIDs.Count > 0 OrElse B2SData.UsedMotionPathStopB2SIDs.Count > 0 OrElse B2SData.UsedMotionPathResumeB2SIDs.Count > 0 OrElse B2SData.UsedMotionPathRemoveB2SIDs.Count > 0 OrElse B2SData.PivotB2SIDs.Count > 0 Then
            b2sSetsData = regkey.GetValue("B2SSetData", New String(Chr(0), 251))
            'If B2SSettings.IsROMControlled AndAlso b2sSetsData.Contains("2") Then
            '    regkey.SetValue("B2SSetData", b2sSetsData.Replace("2", "0"))
            'End If
        End If

        Return b2sSetsData

    End Function
    Private Function GetMechPollingData(ByVal regkey As RegistryKey) As Integer()

        Dim mechsData As Integer() = New Integer() {-1, -1, -1, -1, -1, -1}

        If B2SData.UseRomMechs Then
            For i As Integer = 1 To Math.Min(B2SData.UsedRomMechIDs.Count, 5)
                mechsData(i) = regkey.GetValue("B2SMechs" & i.ToString(), -1)
            Next
        End If

        Return mechsData

    End Function
    Private Function GetAnimationsPollingData(ByVal regkey As RegistryKey) As String

        Dim animationsdata As String = String.Empty

        If B2SAnimation.AreThereAnimations Then
            animationsdata = regkey.GetValue("B2SAnimations", String.Empty)
        End If

        Return animationsdata

    End Function
    Private Function GetRotationsPollingData(ByVal regkey As RegistryKey) As String

        Dim rotationsdata As String = String.Empty

        rotationsdata = regkey.GetValue("B2SRotations", String.Empty)

        Return rotationsdata

    End Function
    Private Function GetSoundsPollingData(ByVal regkey As RegistryKey) As String

        Dim soundsdata As String = String.Empty

        soundsdata = regkey.GetValue("B2SSounds", String.Empty)

        Return soundsdata

    End Function

    Private Function GetLampsTopVisible(ByVal lampsData As String, ByVal currentTopVisible As Boolean) As Boolean

        Dim topVisible As Boolean = currentTopVisible

        If B2SData.UseRomLamps AndAlso TopRomIDType = B2SBaseBox.eRomIDType.Lamp Then
            Dim lampid As Integer = TopRomID
            Dim currentvalue As Integer = CInt(lampsData.Substring(lampid, 1))
            If lamps(lampid) <> currentvalue Then
                If Not B2SData.UsedRomLampIDs.ContainsKey(lampid) AndAlso Not B2SData.UsedAnimationLampIDs.ContainsKey(lampid) AndAlso Not B2SData.UsedRandomAnimationLampIDs.ContainsKey(lampid) Then lamps(lampid) = currentvalue
                topVisible = (currentvalue <> 0)
                If TopRomInverted Then topVisible = Not topVisible
            End If
        End If

        Return topVisible

    End Function
    Private Function GetSolenoidsTopVisible(ByVal solenoidsData As String, ByVal currentTopVisible As Boolean) As Boolean

        Dim topVisible As Boolean = currentTopVisible

        If B2SData.UseRomSolenoids AndAlso TopRomIDType = B2SBaseBox.eRomIDType.Solenoid Then
            Dim solenoidid As Integer = TopRomID
            Dim currentvalue As Integer = CInt(solenoidsData.Substring(solenoidid, 1))
            If solenoids(solenoidid) <> currentvalue Then
                If Not B2SData.UsedRomSolenoidIDs.ContainsKey(solenoidid) AndAlso Not B2SData.UsedAnimationSolenoidIDs.ContainsKey(solenoidid) AndAlso Not B2SData.UsedRandomAnimationSolenoidIDs.ContainsKey(solenoidid) Then solenoids(solenoidid) = currentvalue
                topVisible = (currentvalue <> 0)
                If TopRomInverted Then topVisible = Not topVisible
            End If
        End If

        Return topVisible

    End Function
    Private Function GetGIStringsTopVisible(ByVal gistringsData As String, ByVal currentTopVisible As Boolean) As Boolean

        Dim topVisible As Boolean = currentTopVisible

        If B2SData.UseRomGIStrings AndAlso TopRomIDType = B2SBaseBox.eRomIDType.GIString Then
            Dim gistringid As Integer = TopRomID
            Dim currentvalue As Integer = CInt(gistringsData.Substring(gistringid, 1))
            If gistrings(gistringid) <> currentvalue Then
                If Not B2SData.UsedRomGIStringIDs.ContainsKey(gistringid) AndAlso Not B2SData.UsedAnimationGIStringIDs.ContainsKey(gistringid) AndAlso Not B2SData.UsedRandomAnimationGIStringIDs.ContainsKey(gistringid) Then gistrings(gistringid) = currentvalue
                topVisible = (currentvalue > 4)
                If TopRomInverted Then topVisible = Not topVisible
            End If
        End If

        Return topVisible

    End Function
    Private Function GetB2SSetsTopVisible(ByVal b2sSetsData As String, ByVal currentTopVisible As Boolean) As Boolean

        Dim topVisible As Boolean = currentTopVisible

        If B2SData.UseRomLamps AndAlso TopRomIDType = B2SBaseBox.eRomIDType.Lamp Then
            Dim b2ssetid As Integer = TopRomID
            Dim currentvalue As Integer = Asc(b2sSetsData.Substring(b2ssetid, 1))
            If b2sSets(b2ssetid) <> currentvalue Then
                If Not B2SData.UsedRomLampIDs.ContainsKey(b2ssetid) AndAlso Not B2SData.UsedAnimationLampIDs.ContainsKey(b2ssetid) AndAlso Not B2SData.UsedRandomAnimationLampIDs.ContainsKey(b2ssetid) Then b2sSets(b2ssetid) = currentvalue
                topVisible = (currentvalue <> 0)
                If TopRomInverted Then topVisible = Not topVisible
            End If
        End If

        Return topVisible

    End Function
    Private Function GetLampsSecondVisible(ByVal lampsData As String, ByVal currentSecondVisible As Boolean) As Boolean

        Dim secondVisible As Boolean = currentSecondVisible

        If B2SData.UseRomLamps AndAlso SecondRomIDType = B2SBaseBox.eRomIDType.Lamp Then
            Dim lampid As Integer = SecondRomID
            Dim currentvalue As Integer = CInt(lampsData.Substring(lampid, 1))
            If lamps(lampid) <> currentvalue Then
                If Not B2SData.UsedRomLampIDs.ContainsKey(lampid) AndAlso Not B2SData.UsedAnimationLampIDs.ContainsKey(lampid) AndAlso Not B2SData.UsedRandomAnimationLampIDs.ContainsKey(lampid) Then lamps(lampid) = currentvalue
                secondVisible = (currentvalue <> 0)
                If SecondRomInverted Then secondVisible = Not secondVisible
            End If
        End If

        Return secondVisible

    End Function
    Private Function GetSolenoidsSecondVisible(ByVal solenoidsData As String, ByVal currentSecondVisible As Boolean) As Boolean

        Dim secondVisible As Boolean = currentSecondVisible

        If B2SData.UseRomSolenoids AndAlso SecondRomIDType = B2SBaseBox.eRomIDType.Solenoid Then
            Dim solenoidid As Integer = SecondRomID
            Dim currentvalue As Integer = CInt(solenoidsData.Substring(solenoidid, 1))
            If solenoids(solenoidid) <> currentvalue Then
                If Not B2SData.UsedRomSolenoidIDs.ContainsKey(solenoidid) AndAlso Not B2SData.UsedAnimationSolenoidIDs.ContainsKey(solenoidid) AndAlso Not B2SData.UsedRandomAnimationSolenoidIDs.ContainsKey(solenoidid) Then solenoids(solenoidid) = currentvalue
                secondVisible = (currentvalue <> 0)
                If SecondRomInverted Then secondVisible = Not secondVisible
            End If
        End If

        Return secondVisible

    End Function
    Private Function GetGIStringsSecondVisible(ByVal gistringsData As String, ByVal currentSecondVisible As Boolean) As Boolean

        Dim secondVisible As Boolean = currentSecondVisible

        If B2SData.UseRomGIStrings AndAlso SecondRomIDType = B2SBaseBox.eRomIDType.GIString Then
            Dim gistringid As Integer = SecondRomID
            Dim currentvalue As Integer = CInt(gistringsData.Substring(gistringid, 1))
            If gistrings(gistringid) <> currentvalue Then
                If Not B2SData.UsedRomGIStringIDs.ContainsKey(gistringid) AndAlso Not B2SData.UsedAnimationGIStringIDs.ContainsKey(gistringid) AndAlso Not B2SData.UsedRandomAnimationGIStringIDs.ContainsKey(gistringid) Then gistrings(gistringid) = currentvalue
                secondVisible = (currentvalue > 4)
                If SecondRomInverted Then secondVisible = Not secondVisible
            End If
        End If

        Return secondVisible

    End Function
    Private Function GetB2SSetsSecondVisible(ByVal b2sSetsData As String, ByVal currentSecondVisible As Boolean) As Boolean

        Dim secondVisible As Boolean = currentSecondVisible

        If B2SData.UseRomLamps AndAlso SecondRomIDType = B2SBaseBox.eRomIDType.Lamp Then
            Dim b2ssetid As Integer = SecondRomID
            Dim currentvalue As Integer = Asc(b2sSetsData.Substring(b2ssetid, 1))
            If b2sSets(b2ssetid) <> currentvalue Then
                If Not B2SData.UsedRomLampIDs.ContainsKey(b2ssetid) AndAlso Not B2SData.UsedAnimationLampIDs.ContainsKey(b2ssetid) AndAlso Not B2SData.UsedRandomAnimationLampIDs.ContainsKey(b2ssetid) Then b2sSets(b2ssetid) = currentvalue
                secondVisible = (currentvalue <> 0)
                If SecondRomInverted Then secondVisible = Not secondVisible
            End If
        End If

        Return secondVisible

    End Function

    Private Sub GetThruAllLamps(ByVal lampsData As String)

        Dim pivotLampIDs As New Generic.HashSet(Of Integer)(B2SData.UsedMotionPathLampIDs.Keys)
        pivotLampIDs.UnionWith(B2SData.PivotLampIDs.Keys)
        For Each lampid As Integer In pivotLampIDs
            If lampid < lampsData.Length AndAlso lampid < motionPathLamps.Length Then
                Dim currentvalue As Integer = CInt(lampsData.Substring(lampid, 1))
                If motionPathLamps(lampid) <> currentvalue Then
                    motionPathLamps(lampid) = currentvalue
                    B2SData.SetPivotTriggerState(B2SData.PivotLampIDs, lampid, currentvalue)
                    If currentvalue <> 0 Then
                        If B2SData.UsedMotionPathLampIDs.ContainsKey(lampid) Then B2SData.StartMotionPaths(B2SData.UsedMotionPathLampIDs(lampid))
                        If B2SData.UsedMotionPathRemoveLampIDs.ContainsKey(lampid) Then B2SData.RemoveMotionPaths(B2SData.UsedMotionPathRemoveLampIDs(lampid))
                    End If
                End If
            End If
        Next

        If B2SData.UseRomLamps Then
            For Each lampid As Integer In B2SData.UsedRomLampIDs.Keys
                If lampid < lampsData.Length And lampid < lamps.Length Then
                    Dim currentvalue As Integer = CInt(lampsData.Substring(lampid, 1))
                    If lamps(lampid) <> currentvalue Then
                        If Not B2SData.UsedRomReelLampIDs.ContainsKey(lampid) AndAlso Not B2SData.UsedAnimationLampIDs.ContainsKey(lampid) AndAlso Not B2SData.UsedRandomAnimationLampIDs.ContainsKey(lampid) Then lamps(lampid) = currentvalue
                        If B2SData.UsedRomLampIDs.ContainsKey(lampid) Then
                            For Each picbox As B2SPictureBox In B2SData.UsedRomLampIDs(lampid)
                                If picbox IsNot Nothing AndAlso (Not B2SData.UseIlluminationLocks OrElse String.IsNullOrEmpty(picbox.GroupName) OrElse Not B2SData.IlluminationLocks.ContainsKey(picbox.GroupName)) Then
                                    If picbox.RomIDValue > 0 Then
                                        picbox.Visible = (picbox.RomIDValue = currentvalue)
                                    Else
                                        Dim visible As Boolean = (currentvalue <> 0)
                                        If picbox.RomInverted Then visible = Not visible
                                        If picbox.NativeRotation Then
                                            picbox.SetNativeRotationState(visible)
                                        ElseIf B2SData.UseRotatingImage AndAlso B2SData.RotatingPictureBox(0) IsNot Nothing AndAlso picbox.Equals(B2SData.RotatingPictureBox(0)) Then
                                            If visible Then
                                                StartRotation()
                                            Else
                                                StopRotation()
                                            End If
                                        Else
                                            picbox.Visible = visible
                                        End If
                                    End If
                                End If
                            Next
                        End If
                    End If
                End If
            Next
        End If
        If B2SData.UseRomReelLamps Then
            For Each lampid As Integer In B2SData.UsedRomReelLampIDs.Keys
                If lampid < lampsData.Length And lampid < lamps.Length Then
                    Dim currentvalue As Integer = CInt(lampsData.Substring(lampid, 1))
                    If lamps(lampid) <> currentvalue Then
                        If Not B2SData.UsedAnimationLampIDs.ContainsKey(lampid) AndAlso Not B2SData.UsedRandomAnimationLampIDs.ContainsKey(lampid) Then lamps(lampid) = currentvalue
                        If B2SData.UsedRomReelLampIDs.ContainsKey(lampid) Then
                            For Each reelbox As B2SReelBox In B2SData.UsedRomReelLampIDs(lampid)
                                If reelbox IsNot Nothing AndAlso (Not B2SData.UseIlluminationLocks OrElse String.IsNullOrEmpty(reelbox.GroupName) OrElse Not B2SData.IlluminationLocks.ContainsKey(reelbox.GroupName)) Then
                                    If reelbox.RomIDValue > 0 Then
                                        reelbox.Illuminated = (reelbox.RomIDValue = currentvalue)
                                    Else
                                        Dim illuminated As Boolean = (currentvalue <> 0)
                                        If reelbox.RomInverted Then illuminated = Not illuminated
                                        reelbox.Illuminated = illuminated
                                    End If
                                End If
                            Next
                        End If
                    End If
                End If
            Next
        End If
        If B2SData.UseAnimationLamps Then
            For Each lampid As Integer In B2SData.UsedAnimationLampIDs.Keys
                If lampid < lampsData.Length And lampid < lamps.Length Then
                    Dim currentvalue As Integer = CInt(lampsData.Substring(lampid, 1))
                    If lamps(lampid) <> currentvalue Then
                        If Not B2SData.UsedRandomAnimationLampIDs.ContainsKey(lampid) Then lamps(lampid) = currentvalue
                        If B2SData.UsedAnimationLampIDs.ContainsKey(lampid) Then
                            For Each animation As B2SData.AnimationInfo In B2SData.UsedAnimationLampIDs(lampid)
                                Dim start As Boolean = (currentvalue <> 0)
                                If animation.Inverted Then start = Not start
                                If start Then
                                    StartAnimation(animation.AnimationName)
                                Else
                                    StopAnimation(animation.AnimationName)
                                End If
                            Next
                        End If
                    End If
                End If
            Next
            ' random animation start
            For Each lampid As Integer In B2SData.UsedRandomAnimationLampIDs.Keys
                If lampid < lampsData.Length And lampid < lamps.Length Then
                    Dim currentvalue As Integer = CInt(lampsData.Substring(lampid, 1))
                    If lamps(lampid) <> currentvalue Then
                        lamps(lampid) = currentvalue
                        If B2SData.UsedRandomAnimationLampIDs.ContainsKey(lampid) Then
                            Dim start As Boolean = (currentvalue <> 0)
                            Dim isrunning As Boolean = False
                            If start Then
                                For Each matchinganimation As B2SData.AnimationInfo In B2SData.UsedRandomAnimationLampIDs(lampid)
                                    If IsAnimationRunning(matchinganimation.AnimationName) Then
                                        isrunning = True
                                        Exit For
                                    End If
                                Next
                            End If
                            If start Then
                                If Not isrunning Then
                                    Dim random As Integer = RandomStarter(B2SData.UsedRandomAnimationLampIDs(lampid).Length)
                                    Dim animation As B2SData.AnimationInfo = B2SData.UsedRandomAnimationLampIDs(lampid)(random)
                                    lastRandomStartedAnimation = animation.AnimationName
                                    StartAnimation(lastRandomStartedAnimation)
                                End If
                            Else
                                If Not String.IsNullOrEmpty(lastRandomStartedAnimation) Then
                                    StopAnimation(lastRandomStartedAnimation)
                                    lastRandomStartedAnimation = String.Empty
                                End If
                            End If
                        End If
                    End If
                End If
            Next
        End If

    End Sub

    Private Sub GetThruAllSolenoids(ByVal solenoidsData As String)

        Dim pivotSolenoidIDs As New Generic.HashSet(Of Integer)(B2SData.UsedMotionPathSolenoidIDs.Keys)
        pivotSolenoidIDs.UnionWith(B2SData.PivotSolenoidIDs.Keys)
        pivotSolenoidIDs.UnionWith(B2SData.GetPhysicsLauncherIDs(1))
        For Each solenoidid As Integer In pivotSolenoidIDs
            If solenoidid < solenoidsData.Length Then
                Dim currentvalue As Integer = CInt(solenoidsData.Substring(solenoidid, 1))
                If motionPathSolenoids(solenoidid) <> currentvalue Then
                    motionPathSolenoids(solenoidid) = currentvalue
                    B2SData.FirePhysicsLaunchers(1, solenoidid, currentvalue)
                    B2SData.SetPivotTriggerState(B2SData.PivotSolenoidIDs, solenoidid, currentvalue)
                    If currentvalue <> 0 Then
                        If B2SData.UsedMotionPathSolenoidIDs.ContainsKey(solenoidid) Then B2SData.StartMotionPaths(B2SData.UsedMotionPathSolenoidIDs(solenoidid))
                        If B2SData.UsedMotionPathRemoveSolenoidIDs.ContainsKey(solenoidid) Then B2SData.RemoveMotionPaths(B2SData.UsedMotionPathRemoveSolenoidIDs(solenoidid))
                    End If
                End If
            End If
        Next

        If B2SData.UseRomSolenoids Then
            For Each solenoidid As Integer In B2SData.UsedRomSolenoidIDs.Keys
                If solenoidid < solenoidsData.Length Then
                    Dim currentvalue As Integer = CInt(solenoidsData.Substring(solenoidid, 1))
                    If solenoids(solenoidid) <> currentvalue Then
                        If Not B2SData.UsedAnimationSolenoidIDs.ContainsKey(solenoidid) AndAlso Not B2SData.UsedRandomAnimationSolenoidIDs.ContainsKey(solenoidid) Then solenoids(solenoidid) = currentvalue
                        If B2SData.UsedRomSolenoidIDs.ContainsKey(solenoidid) Then
                            For Each picbox As B2SPictureBox In B2SData.UsedRomSolenoidIDs(solenoidid)
                                'If picbox IsNot Nothing Then
                                If picbox IsNot Nothing AndAlso (Not B2SData.UseIlluminationLocks OrElse String.IsNullOrEmpty(picbox.GroupName) OrElse Not B2SData.IlluminationLocks.ContainsKey(picbox.GroupName)) Then
                                    Dim visible As Boolean = (currentvalue <> 0)
                                    If picbox.RomInverted Then visible = Not visible
                                    If picbox.NativeRotation Then
                                        picbox.SetNativeRotationState(visible)
                                    ElseIf B2SData.UseRotatingImage AndAlso B2SData.RotatingPictureBox(0) IsNot Nothing AndAlso picbox.Equals(B2SData.RotatingPictureBox(0)) Then
                                        If visible Then
                                            StartRotation()
                                        Else
                                            StopRotation()
                                        End If
                                    Else
                                        picbox.Visible = visible
                                    End If
                                End If
                            Next
                        End If
                    End If
                End If
            Next
        End If
        If B2SData.UseAnimationSolenoids Then
            For Each solenoidid As Integer In B2SData.UsedAnimationSolenoidIDs.Keys
                If solenoidid < solenoidsData.Length Then
                    Dim currentvalue As Integer = CInt(solenoidsData.Substring(solenoidid, 1))
                    If solenoids(solenoidid) <> currentvalue Then
                        If Not B2SData.UsedRandomAnimationSolenoidIDs.ContainsKey(solenoidid) Then solenoids(solenoidid) = currentvalue
                        If B2SData.UsedAnimationSolenoidIDs.ContainsKey(solenoidid) Then
                            For Each animation As B2SData.AnimationInfo In B2SData.UsedAnimationSolenoidIDs(solenoidid)
                                Dim start As Boolean = (currentvalue <> 0)
                                If animation.Inverted Then start = Not start
                                If start Then
                                    StartAnimation(animation.AnimationName)
                                Else
                                    StopAnimation(animation.AnimationName)
                                End If
                            Next
                        End If
                    End If
                End If
            Next
            ' random animation start
            For Each solenoidid As Integer In B2SData.UsedRandomAnimationSolenoidIDs.Keys
                If solenoidid < solenoidsData.Length Then
                    Dim currentvalue As Integer = CInt(solenoidsData.Substring(solenoidid, 1))
                    If solenoids(solenoidid) <> currentvalue Then
                        solenoids(solenoidid) = currentvalue
                        If B2SData.UsedRandomAnimationSolenoidIDs.ContainsKey(solenoidid) Then
                            Dim start As Boolean = (currentvalue <> 0)
                            Dim isrunning As Boolean = False
                            If start Then
                                For Each matchinganimation As B2SData.AnimationInfo In B2SData.UsedRandomAnimationSolenoidIDs(solenoidid)
                                    If IsAnimationRunning(matchinganimation.AnimationName) Then
                                        isrunning = True
                                        Exit For
                                    End If
                                Next
                            End If
                            If start Then
                                If Not isrunning Then
                                    Dim random As Integer = RandomStarter(B2SData.UsedRandomAnimationSolenoidIDs(solenoidid).Length)
                                    Dim animation As B2SData.AnimationInfo = B2SData.UsedRandomAnimationSolenoidIDs(solenoidid)(random)
                                    lastRandomStartedAnimation = animation.AnimationName
                                    StartAnimation(lastRandomStartedAnimation)
                                End If
                            Else
                                If Not String.IsNullOrEmpty(lastRandomStartedAnimation) Then
                                    StopAnimation(lastRandomStartedAnimation)
                                    lastRandomStartedAnimation = String.Empty
                                End If
                            End If
                        End If
                    End If
                End If
            Next
        End If

    End Sub

    Private Sub GetThruAllGIStrings(ByVal gistringsData As String)

        If B2SData.UseRomGIStrings Then
            For Each gistringid As Integer In B2SData.UsedRomGIStringIDs.Keys
                If gistringid < gistringsData.Length Then
                    Dim currentvalue As Integer = CInt(gistringsData.Substring(gistringid, 1))
                    If gistrings(gistringid) <> currentvalue Then
                        If Not B2SData.UsedAnimationGIStringIDs.ContainsKey(gistringid) AndAlso Not B2SData.UsedRandomAnimationGIStringIDs.ContainsKey(gistringid) Then gistrings(gistringid) = currentvalue
                        If B2SData.UsedRomGIStringIDs.ContainsKey(gistringid) AndAlso B2SData.UsedRomGIStringIDs(gistringid) IsNot Nothing Then
                            For Each picbox As B2SPictureBox In B2SData.UsedRomGIStringIDs(gistringid)
                                If picbox IsNot Nothing AndAlso (Not B2SData.UseIlluminationLocks OrElse String.IsNullOrEmpty(picbox.GroupName) OrElse Not B2SData.IlluminationLocks.ContainsKey(picbox.GroupName)) Then
                                    Dim visible As Boolean = (currentvalue > 4)
                                    If picbox.RomInverted Then visible = Not visible
                                    If picbox.NativeRotation Then
                                        picbox.SetNativeRotationState(visible)
                                    ElseIf B2SData.UseRotatingImage AndAlso B2SData.RotatingPictureBox(0) IsNot Nothing AndAlso picbox.Equals(B2SData.RotatingPictureBox(0)) Then
                                        If visible Then
                                            StartRotation()
                                        Else
                                            StopRotation()
                                        End If
                                    Else
                                        picbox.Visible = visible
                                    End If
                                End If
                            Next
                        End If
                    End If
                End If
            Next
        End If
        If B2SData.UseAnimationGIStrings Then
            For Each gistringid As Integer In B2SData.UsedAnimationGIStringIDs.Keys
                If gistringid < gistringsData.Length Then
                    Dim currentvalue As Integer = CInt(gistringsData.Substring(gistringid, 1))
                    If gistrings(gistringid) <> currentvalue Then
                        If Not B2SData.UsedRandomAnimationGIStringIDs.ContainsKey(gistringid) Then gistrings(gistringid) = currentvalue
                        If B2SData.UsedAnimationGIStringIDs.ContainsKey(gistringid) Then
                            For Each animation As B2SData.AnimationInfo In B2SData.UsedAnimationGIStringIDs(gistringid)
                                Dim start As Boolean = (currentvalue > 4)
                                If animation.Inverted Then start = Not start
                                If start Then
                                    StartAnimation(animation.AnimationName)
                                Else
                                    StopAnimation(animation.AnimationName)
                                End If
                            Next
                        End If
                    End If
                End If
            Next
            ' random animation start
            For Each gistringid As Integer In B2SData.UsedRandomAnimationGIStringIDs.Keys
                If gistringid < gistringsData.Length Then
                    Dim currentvalue As Integer = CInt(gistringsData.Substring(gistringid, 1))
                    If gistrings(gistringid) <> currentvalue Then
                        gistrings(gistringid) = currentvalue
                        If B2SData.UsedRandomAnimationGIStringIDs.ContainsKey(gistringid) Then
                            Dim start As Boolean = (currentvalue > 4)
                            Dim isrunning As Boolean = False
                            If start Then
                                For Each matchinganimation As B2SData.AnimationInfo In B2SData.UsedRandomAnimationGIStringIDs(gistringid)
                                    If IsAnimationRunning(matchinganimation.AnimationName) Then
                                        isrunning = True
                                        Exit For
                                    End If
                                Next
                            End If
                            If start Then
                                If Not isrunning Then
                                    Dim random As Integer = RandomStarter(B2SData.UsedRandomAnimationGIStringIDs(gistringid).Length)
                                    Dim animation As B2SData.AnimationInfo = B2SData.UsedRandomAnimationGIStringIDs(gistringid)(random)
                                    lastRandomStartedAnimation = animation.AnimationName
                                    StartAnimation(lastRandomStartedAnimation)
                                End If
                            Else
                                If Not String.IsNullOrEmpty(lastRandomStartedAnimation) Then
                                    StopAnimation(lastRandomStartedAnimation)
                                    lastRandomStartedAnimation = String.Empty
                                End If
                            End If
                        End If
                    End If
                End If
            Next
        End If

    End Sub

    Private Sub GetThruAllB2SData(ByVal b2sSetData As String)

        Dim motionTriggerIDs As New Generic.HashSet(Of Integer)(B2SData.UsedMotionPathB2SIDs.Keys)
        motionTriggerIDs.UnionWith(B2SData.UsedMotionPathStopB2SIDs.Keys)
        motionTriggerIDs.UnionWith(B2SData.UsedMotionPathResumeB2SIDs.Keys)
        motionTriggerIDs.UnionWith(B2SData.UsedMotionPathRemoveB2SIDs.Keys)
        motionTriggerIDs.UnionWith(B2SData.PivotB2SIDs.Keys)
        motionTriggerIDs.UnionWith(B2SData.GetPhysicsLauncherIDs(3))
        For Each b2ssetid As Integer In motionTriggerIDs
            If b2ssetid < b2sSetData.Length AndAlso b2ssetid < motionPathB2SStates.Length Then
                Dim currentvalue As Integer = Asc(b2sSetData.Substring(b2ssetid, 1))
                Dim previousValue As Integer = motionPathB2SStates(b2ssetid)
                If previousValue <> currentvalue Then
                    motionPathB2SStates(b2ssetid) = currentvalue
                    B2SData.FirePhysicsLaunchers(3, b2ssetid, currentvalue)
                    B2SData.SetPivotTriggerState(B2SData.PivotB2SIDs, b2ssetid, currentvalue)
                    If previousValue = 0 AndAlso currentvalue <> 0 Then
                        If B2SData.UsedMotionPathB2SIDs.ContainsKey(b2ssetid) Then
                            B2SData.StartMotionPaths(B2SData.UsedMotionPathB2SIDs(b2ssetid))
                        End If
                        If B2SData.UsedMotionPathRemoveB2SIDs.ContainsKey(b2ssetid) Then
                            B2SData.RemoveMotionPaths(B2SData.UsedMotionPathRemoveB2SIDs(b2ssetid))
                        End If
                        If B2SData.UsedMotionPathStopB2SIDs.ContainsKey(b2ssetid) Then
                            For Each picbox As B2SPictureBox In B2SData.UsedMotionPathStopB2SIDs(b2ssetid)
                                If picbox IsNot Nothing Then picbox.StopMotionPath()
                            Next
                        End If
                        If B2SData.UsedMotionPathResumeB2SIDs.ContainsKey(b2ssetid) Then
                            For Each picbox As B2SPictureBox In B2SData.UsedMotionPathResumeB2SIDs(b2ssetid)
                                If picbox IsNot Nothing Then picbox.ResumeMotionPath()
                            Next
                        End If
                    End If
                End If
            End If
        Next

        If B2SData.UseRomLamps Then
            For Each b2ssetid As Integer In B2SData.UsedRomLampIDs.Keys
                If b2ssetid < b2sSetData.Length Then
                    Dim currentvalue As Integer = Asc(b2sSetData.Substring(b2ssetid, 1))
                    If b2sSets(b2ssetid) <> currentvalue Then
                        If Not B2SData.UsedRomReelLampIDs.ContainsKey(b2ssetid) AndAlso Not B2SData.UsedAnimationLampIDs.ContainsKey(b2ssetid) AndAlso Not B2SData.UsedRandomAnimationLampIDs.ContainsKey(b2ssetid) Then b2sSets(b2ssetid) = currentvalue
                        If B2SData.UsedRomLampIDs.ContainsKey(b2ssetid) Then
                            For Each picbox As B2SPictureBox In B2SData.UsedRomLampIDs(b2ssetid)
                                If picbox IsNot Nothing AndAlso (Not B2SData.UseIlluminationLocks OrElse String.IsNullOrEmpty(picbox.GroupName) OrElse Not B2SData.IlluminationLocks.ContainsKey(picbox.GroupName)) Then
                                    If picbox.RomIDValue > 0 Then
                                        picbox.Visible = (picbox.RomIDValue = currentvalue)
                                    Else
                                        Dim visible As Boolean = (currentvalue <> 0)
                                        If picbox.RomInverted Then visible = Not visible
                                        If picbox.NativeRotation Then
                                            picbox.SetNativeRotationState(visible)
                                        ElseIf B2SData.UseRotatingImage AndAlso B2SData.RotatingPictureBox(0) IsNot Nothing AndAlso picbox.Equals(B2SData.RotatingPictureBox(0)) Then
                                            If visible Then
                                                StartRotation()
                                            Else
                                                StopRotation()
                                            End If
                                        Else
                                            picbox.Visible = visible
                                        End If
                                    End If
                                End If
                            Next
                        End If
                    End If
                End If
            Next
        End If
        If B2SData.UseRomReelLamps Then
            For Each b2ssetid As Integer In B2SData.UsedRomReelLampIDs.Keys
                If b2ssetid < b2sSetData.Length Then
                    Dim currentvalue As Integer = Asc(b2sSetData.Substring(b2ssetid, 1))
                    If b2sSets(b2ssetid) <> currentvalue Then
                        If Not B2SData.UsedAnimationLampIDs.ContainsKey(b2ssetid) AndAlso Not B2SData.UsedRandomAnimationLampIDs.ContainsKey(b2ssetid) Then b2sSets(b2ssetid) = currentvalue
                        If B2SData.UsedRomReelLampIDs.ContainsKey(b2ssetid) Then
                            For Each reelbox As B2SReelBox In B2SData.UsedRomReelLampIDs(b2ssetid)
                                If reelbox IsNot Nothing AndAlso (Not B2SData.UseIlluminationLocks OrElse String.IsNullOrEmpty(reelbox.GroupName) OrElse Not B2SData.IlluminationLocks.ContainsKey(reelbox.GroupName)) Then
                                    If reelbox.RomIDValue > 0 Then
                                        reelbox.Illuminated = (reelbox.RomIDValue = currentvalue)
                                    Else
                                        Dim illuminated As Boolean = (currentvalue <> 0)
                                        If reelbox.RomInverted Then illuminated = Not illuminated
                                        reelbox.Illuminated = illuminated
                                    End If
                                End If
                            Next
                        End If
                    End If
                End If
            Next
        End If
        If B2SData.UseAnimationLamps Then
            For Each b2ssetid As Integer In B2SData.UsedAnimationLampIDs.Keys
                If b2ssetid < b2sSetData.Length Then
                    Dim currentvalue As Integer = Asc(b2sSetData.Substring(b2ssetid, 1))
                    If b2sSets(b2ssetid) <> currentvalue Then
                        If Not B2SData.UsedRandomAnimationLampIDs.ContainsKey(b2ssetid) Then b2sSets(b2ssetid) = currentvalue
                        If B2SData.UsedAnimationLampIDs.ContainsKey(b2ssetid) Then
                            For Each animation As B2SData.AnimationInfo In B2SData.UsedAnimationLampIDs(b2ssetid)
                                Dim start As Boolean = (currentvalue <> 0)
                                If animation.Inverted Then start = Not start
                                If start Then
                                    StartAnimation(animation.AnimationName)
                                Else
                                    StopAnimation(animation.AnimationName)
                                End If
                            Next
                        End If
                    End If
                End If
            Next
            ' random animation start
            For Each b2ssetid As Integer In B2SData.UsedRandomAnimationLampIDs.Keys
                If b2ssetid < b2sSetData.Length Then
                    Dim currentvalue As Integer = Asc(b2sSetData.Substring(b2ssetid, 1))
                    If b2sSets(b2ssetid) <> currentvalue Then
                        b2sSets(b2ssetid) = currentvalue
                        If B2SData.UsedRandomAnimationLampIDs.ContainsKey(b2ssetid) Then
                            Dim start As Boolean = (currentvalue <> 0)
                            Dim isrunning As Boolean = False
                            If start Then
                                For Each matchinganimation As B2SData.AnimationInfo In B2SData.UsedRandomAnimationLampIDs(b2ssetid)
                                    If IsAnimationRunning(matchinganimation.AnimationName) Then
                                        isrunning = True
                                        Exit For
                                    End If
                                Next
                            End If
                            If start Then
                                If Not isrunning Then
                                    Dim random As Integer = RandomStarter(B2SData.UsedRandomAnimationLampIDs(b2ssetid).Length)
                                    Dim animation As B2SData.AnimationInfo = B2SData.UsedRandomAnimationLampIDs(b2ssetid)(random)
                                    lastRandomStartedAnimation = animation.AnimationName
                                    StartAnimation(lastRandomStartedAnimation)
                                End If
                            Else
                                If Not String.IsNullOrEmpty(lastRandomStartedAnimation) Then
                                    StopAnimation(lastRandomStartedAnimation)
                                    lastRandomStartedAnimation = String.Empty
                                End If
                            End If
                        End If
                    End If
                End If
            Next
        End If

    End Sub

    Private Sub GetThruAllMechs(ByVal mechsData As Integer())

        If B2SData.UseRomMechs Then
            For Each mechid As Integer In B2SData.UsedRomMechIDs.Keys
                If mechid >= 1 AndAlso mechid <= 5 Then
                    Dim currentvalue As Integer = mechsData(mechid)
                    If mechs(mechid) <> currentvalue Then
                        mechs(mechid) = currentvalue
                        If B2SData.UsedRomMechIDs.ContainsKey(mechid) Then
                            Dim rotatingPicture As B2SPictureBox = Nothing
                            If B2SData.RotatingPictureBox.ContainsKey(mechid) Then rotatingPicture = B2SData.RotatingPictureBox(mechid)
                            If rotatingPicture IsNot Nothing AndAlso rotatingPicture.NativeRotation AndAlso rotatingPicture.PictureBoxType = B2SPictureBox.ePictureBoxType.MechRotatingImage Then
                                rotatingPicture.SetNativeMechPosition(currentvalue)
                            ElseIf rotatingPicture IsNot Nothing AndAlso B2SData.RotatingImages.ContainsKey(mechid) AndAlso B2SData.RotatingImages(mechid) IsNot Nothing AndAlso B2SData.RotatingImages(mechid).Count > 0 AndAlso B2SData.RotatingImages(mechid).ContainsKey(currentvalue) Then
                                rotatingPicture.BackgroundImage = B2SData.RotatingImages(mechid)(currentvalue)
                                rotatingPicture.Visible = True
                            End If
                        End If
                    End If
                End If
            Next
        End If

    End Sub

    Private Sub GetThruAllLEDs(ByVal regkey As RegistryKey)

        If B2SData.UseLEDs OrElse B2SData.UseLEDDisplays OrElse B2SData.UseReels Then
            For digit As Integer = 1 To B2SData.ScoreMaxDigit
                Dim currentvalue As Integer = regkey.GetValue("B2SLED" & digit.ToString(), 0)
                If leds(digit) <> currentvalue Then
                    leds(digit) = currentvalue
                    If B2SData.LEDs.ContainsKey("LEDBox" & digit.ToString()) AndAlso B2SSettings.UsedLEDType = B2SSettings.eLEDTypes.Rendered Then
                        ' rendered LEDs are used
                        Dim ledname As String = "LEDBox" & digit.ToString()
                        B2SData.LEDs(ledname).Value = currentvalue
                    ElseIf B2SData.LEDDisplayDigits.ContainsKey(digit - 1) AndAlso B2SSettings.UsedLEDType = B2SSettings.eLEDTypes.Dream7 Then
                        ' Dream 7 displays are used
                        With B2SData.LEDDisplayDigits(digit - 1)
                            .LEDDisplay.SetValue(.Digit, currentvalue)
                        End With
                    ElseIf B2SData.Reels.ContainsKey("ReelBox" & digit.ToString()) Then
                        ' reels are used
                        Dim reelname As String = "ReelBox" & digit.ToString()
                        Dim reelbox As B2SReelBox = B2SData.Reels(reelname)
                        If B2SSettings.IsROMControlled Then
                            reelbox.Value = currentvalue 'ConvertLEDValue4Reels(currentvalue)
                        Else
                            If reelbox.ScoreType = B2SReelBox.eScoreType.Scores Then
                                reelbox.Text(True) = ConvertLEDValue4Reels(currentvalue)
                            Else
                                reelbox.Text(False) = currentvalue
                            End If
                        End If
                    End If
                End If
            Next
        End If

    End Sub

    Private Sub GetThruAllAnimations(ByVal regkey As RegistryKey, ByVal animationsdata As String)

        If B2SAnimation.AreThereAnimations Then
            If Not String.IsNullOrEmpty(animationsdata) Then
                Dim writeAnimationsData As Boolean = False
                For Each animationinfo As String In animationsdata.Split(Chr(1))
                    If Not String.IsNullOrEmpty(animationinfo) Then
                        Dim animationname As String = animationinfo.Substring(0, animationinfo.Length - 2)
                        Dim animationstate As Integer = CInt(animationinfo.Substring(animationinfo.Length - 1))
                        If animations.ContainsKey(animationname) Then
                            If animations(animationname) <> animationstate Then
                                animations(animationname) = animationstate
                                If animationstate = 1 OrElse animationstate = 2 Then
                                    B2SAnimation.StartAnimation(animationname, (animationstate = 2))
                                ElseIf animationstate = 0 Then
                                    B2SAnimation.StopAnimation(animationname)
                                End If
                                writeAnimationsData = True
                                animationsdata = animationsdata.Replace(animationinfo, animationname & "=9")
                            End If
                        Else
                            animations.Add(animationname, animationstate)
                            If animationstate = 1 OrElse animationstate = 2 Then
                                B2SAnimation.StartAnimation(animationname, (animationstate = 2))
                            ElseIf animationstate = 0 Then
                                B2SAnimation.StopAnimation(animationname)
                            End If
                            writeAnimationsData = True
                            animationsdata = animationsdata.Replace(animationinfo, animationname & "=9")
                        End If
                    End If
                Next
                If writeAnimationsData Then
                    regkey.SetValue("B2SAnimations", animationsdata)
                End If
            End If
        End If

    End Sub

    Private Sub GetThruAllRotations(ByVal regkey As RegistryKey, ByVal rotationsdata As String)

        If Not String.IsNullOrEmpty(rotationsdata) Then
            Dim rotationstate As Integer = CInt(rotationsdata)
            If rotation <> rotationstate Then
                rotation = rotationstate
                If rotationstate = 1 Then
                    StartRotation()
                ElseIf rotationstate = 0 Then
                    StopRotation()
                End If
            End If
        End If

    End Sub

    Private Sub GetThruAllSounds(ByVal regkey As RegistryKey, ByVal soundsdata As String)

        If Not String.IsNullOrEmpty(soundsdata) Then
            Dim writeSoundsData As Boolean = False
            For Each soundinfo As String In soundsdata.Split(Chr(1))
                If Not String.IsNullOrEmpty(soundinfo) Then
                    Dim soundname As String = soundinfo.Substring(0, soundinfo.Length - 2)
                    Dim soundstate As Integer = CInt(soundinfo.Substring(soundinfo.Length - 1))
                    If sounds.ContainsKey(soundname) Then
                        If sounds(soundname) <> soundstate Then
                            sounds(soundname) = soundstate
                            If soundstate = 1 Then
                                PlaySound(soundname)
                            ElseIf soundstate = 0 Then
                                StopSound(soundname)
                            End If
                            writeSoundsData = True
                            soundsdata = soundsdata.Replace(soundinfo, soundname & "=9")
                        End If
                    Else
                        sounds.Add(soundname, soundstate)
                        If soundstate = 1 Then
                            PlaySound(soundname)
                        ElseIf soundstate = 0 Then
                            StopSound(soundname)
                        End If
                        writeSoundsData = True
                        soundsdata = soundsdata.Replace(soundinfo, soundname & "=9")
                    End If
                End If
            Next
            If writeSoundsData Then
                regkey.SetValue("B2SSounds", soundsdata)
            End If
        End If

    End Sub

    Private Sub GetThruAllScoreDisplays(ByVal regkey As RegistryKey)

        Dim hide As Integer = regkey.GetValue("B2SHideScoreDisplays", -1)
        If hide <> -1 Then
            regkey.DeleteValue("B2SHideScoreDisplays", False)
            If hide = 0 Then
                ' show all score displays
                ShowScoreDisplays()
            ElseIf hide = 1 Then
                ' hide all score display
                HideScoreDisplays()
            End If
        End If

    End Sub

    Private Sub GetThruAllIlluGroups(ByVal regkey As RegistryKey)

        Dim illugroups As String = regkey.GetValue("B2SIlluGroupsByName", String.Empty)
        If Not String.IsNullOrEmpty(illugroups) Then
            regkey.DeleteValue("B2SIlluGroupsByName", False)
            ' get thru all illu groups
            For Each illugroupinfo As String In illugroups.Split(Chr(1))
                ' only do the lightning stuff if the group has a name
                If Not String.IsNullOrEmpty(illugroupinfo) And illugroupinfo.Contains("=") Then
                    Dim pos As Integer = illugroupinfo.IndexOf("=")
                    Dim groupname As String = illugroupinfo.Substring(0, pos)
                    Dim value As Integer = CInt(illugroupinfo.Substring(pos + 1))
                    Dim handledByExternalMotion As Boolean = B2SData.TryMoveExternalPositionGrid(groupname, value)
                    If Not handledByExternalMotion Then handledByExternalMotion = B2SData.TryMoveExternalPivot(groupname, value)
                    If Not handledByExternalMotion AndAlso B2SData.IlluminationGroups.ContainsKey(groupname) Then
                        ' get all matching picture boxes
                        For Each picbox As B2SPictureBox In B2SData.IlluminationGroups(groupname)
                            If picbox.RomIDValue > 0 Then
                                picbox.Visible = (picbox.RomIDValue = value)
                            Else
                                picbox.Visible = (value <> 0)
                            End If
                        Next
                    End If
                End If
            Next
        End If

    End Sub

    Private Sub GetThruAllPositions(ByVal regkey As RegistryKey)
        Try

            Dim ledPositions As String = regkey.GetValue("B2SPositions", String.Empty)
            If Not String.IsNullOrEmpty(ledPositions) AndAlso ledPositions.Contains(",") Then
                Dim id As Integer = 0
                Dim xpos As Integer = 0
                Dim ypos As Integer = 0

                regkey.DeleteValue("B2SPositions", False)
                ' get thru all positions
                For Each ledPosition As String In ledPositions.Split(Chr(1))
                    If Not String.IsNullOrEmpty(ledPosition) Then
                        Dim items() As String = ledPosition.Split(","c)
                        id = CInt(items(0))
                        xpos = CInt(items(1))
                        ypos = CInt(items(2))
                        MySetPos(id, xpos, ypos)
                    End If
                Next
            End If
        Catch ex As Exception

        End Try

    End Sub

    Private Sub MySetPos(ByVal id As Integer, ByVal xpos As Integer, ByVal ypos As Integer)

        If B2SData.UsedRomLampIDs.ContainsKey(id) Then
            Dim rescaleBackglass As SizeF
            GetScaleFactor(rescaleBackglass)

            For Each picbox As B2SPictureBox In B2SData.UsedRomLampIDs(id)
                If picbox IsNot Nothing AndAlso (Not B2SData.UseIlluminationLocks OrElse String.IsNullOrEmpty(picbox.GroupName) OrElse Not B2SData.IlluminationLocks.ContainsKey(picbox.GroupName)) Then
                    If picbox.Left <> xpos OrElse picbox.Top <> ypos Then
                        If (picbox.InvokeRequired) Then
                            picbox.BeginInvoke(Sub()
                                                   picbox.Left = xpos
                                                   picbox.Top = ypos
                                                   picbox.RectangleF = New RectangleF(CInt(picbox.Left / rescaleBackglass.Width), CInt(picbox.Top / rescaleBackglass.Height), picbox.RectangleF.Width, picbox.RectangleF.Height)
                                                   If picbox.Parent IsNot Nothing Then picbox.Parent.Invalidate()
                                               End Sub)
                        Else
                            picbox.Left = xpos
                            picbox.Top = ypos
                            picbox.RectangleF = New RectangleF(CInt(picbox.Left / rescaleBackglass.Width), CInt(picbox.Top / rescaleBackglass.Height), picbox.RectangleF.Width, picbox.RectangleF.Height)
                            If picbox.Parent IsNot Nothing Then picbox.Parent.Invalidate()
                        End If
                    End If
                End If
            Next
        End If

    End Sub

    Private Function ConvertLEDValue4Reels(ByVal ledvalue As Integer) As Integer

        Dim ret As Integer = 0
        Select Case ledvalue
            Case 0, 63 : ret = 0
            Case 6 : ret = 1
            Case 91 : ret = 2
            Case 79 : ret = 3
            Case 102 : ret = 4
            Case 109 : ret = 5
            Case 125 : ret = 6
            Case 7 : ret = 7
            Case 127 : ret = 8
            Case 111 : ret = 9
        End Select
        Return ret

    End Function

#End If
#End Region

#Region "settings action"

    Public Sub formBackglass_MouseClick(sender As Object, e As System.Windows.Forms.MouseEventArgs) Handles Me.MouseClick

        If e.Button = Windows.Forms.MouseButtons.Right Then
            formBackglass_KeyUp(Me, New KeyEventArgs(Keys.S))
        End If

    End Sub

    Private Sub formBackglass_KeyUp(sender As Object, e As System.Windows.Forms.KeyEventArgs) Handles Me.KeyUp

        If B2SStatistics.LogStatistics Then Return


        If e.KeyCode = Keys.S Then

            If formSettings IsNot Nothing Then
                Try
                    formSettings.Dispose()
                    formSettings = Nothing
                Catch
                End Try
            End If
            formSettings = New formSettings()
            formSettings.B2SScreen = B2SScreen
            formSettings.B2SAnimation = B2SAnimation
            formSettings.formBackglass = Me
            formSettings.Show(Me)

        ElseIf e.KeyCode = Keys.D1 Then
            StartAnimation("Ball1")
        ElseIf e.KeyCode = Keys.D2 Then
            StartAnimation("Ball2")
        ElseIf e.KeyCode = Keys.D3 Then
            StartAnimation("Ball3")
        ElseIf e.KeyCode = Keys.D4 Then
            StartAnimation("Ball4")
        ElseIf e.KeyCode = Keys.D5 Then
            StartAnimation("Ball5")

        ElseIf e.KeyCode = Keys.M OrElse e.KeyCode = Keys.A OrElse e.KeyCode = Keys.F Then

            If B2SData.DualBackglass Then
                If e.KeyCode = Keys.M Then
                    B2SSettings.CurrentDualMode = If(B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy, B2SSettings.eDualMode.Authentic, B2SSettings.eDualMode.Fantasy)
                ElseIf e.KeyCode = Keys.A Then
                    B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Authentic
                ElseIf e.KeyCode = Keys.F Then
                    B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy
                End If
#If B2S = "DLL" Then
                B2SSettings.Save(, , True)
#Else
                B2SSettings.Save(, True)
#End If
                Me.BackgroundImage = DarkImage
                Me.Invalidate()
                ShowStartupImages()
                B2SAnimation.RestartAnimations()
                If formMode IsNot Nothing Then
                    Try
                        formMode.Dispose()
                        formMode = Nothing
                    Catch
                    End Try
                End If
                formMode = New formMode()
                formMode.Show(Me)
                Me.Focus()
            End If

        ElseIf e.KeyCode = Keys.I OrElse e.KeyCode = Keys.Print OrElse e.KeyCode = Keys.PrintScreen Then

            ' do a screenshot and save it
            If String.IsNullOrEmpty(B2SSettings.ScreenshotPath) Then
                MessageBox.Show("Please enter a valid screenshot path here at the settings.", My.Resources.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            Else
                Dim imageformat As Imaging.ImageFormat = Imaging.ImageFormat.Png
                Dim extension As String = ".png"
                Select Case B2SSettings.ScreenshotFileType
                    Case B2SSettings.eImageFileType.JPG : imageformat = Imaging.ImageFormat.Jpeg : extension = ".jpg"
                    Case B2SSettings.eImageFileType.GIF : imageformat = Imaging.ImageFormat.Gif : extension = ".gif"
                    Case B2SSettings.eImageFileType.BMP : imageformat = Imaging.ImageFormat.Bmp : extension = ".bmp"
                End Select
                Dim screenshotFilename As String = IO.Path.Combine(B2SSettings.ScreenshotPath, IO.Path.GetFileNameWithoutExtension(B2SData.BackglassFileName) & extension)
                B2SScreen.MakeScreenShot(screenshotFilename, imageformat)
                My.Computer.Audio.Play(My.Resources.camera1, AudioPlayMode.Background)
            End If
#If B2S = "EXE" Then
        ElseIf e.KeyCode = Keys.Escape Then

            ' stop the app
            End
#End If
        End If


    End Sub
#If B2S = "DLL" Then
    Private Sub chkSniffer_CheckedChanged(sender As System.Object, e As System.EventArgs)
        B2SSettings.IsStatisticsBackglassOn = chkSniffer.Checked
        B2SSettings.Save(, True)
    End Sub
#End If
#End Region


#Region "public methods"

    Private _backgroundimage As Image
    Public Overrides Property BackgroundImage() As Image
        Get
            Return _backgroundimage
        End Get
        Set(ByVal value As Image)
            If value Is Nothing Then
                _backgroundimage = Nothing
            ElseIf Not value.Equals(_backgroundimage) Then
                _backgroundimage = value
                Me.Invalidate()
            End If
        End Set
    End Property

    Public Sub StartAnimation(ByVal name As String, Optional ByVal playreverse As Boolean = False)

        B2SAnimation.StartAnimation(name, playreverse)

    End Sub
    Public Sub StopAnimation(ByVal name As String)

        B2SAnimation.StopAnimation(name)

    End Sub
    Public Sub StopAllAnimations()

        B2SAnimation.StopAllAnimations()

    End Sub

    Public Function IsAnimationRunning(ByVal name As String) As Boolean

        Return B2SAnimation.IsAnimationRunning(name)

    End Function

    Public Sub StartRotation()

        If B2SData.RotatingPictureBox IsNot Nothing AndAlso B2SData.RotatingPictureBox.ContainsKey(0) AndAlso B2SData.RotatingPictureBox(0) IsNot Nothing Then
            If rotateAngle > 0 AndAlso rotateTimerInterval > 0 Then
                If rotateTimer.Enabled Then rotateTimer.Stop()
                rotateSlowDownSteps = 0
                rotateTimer.Interval = rotateTimerInterval
                rotateTimer.Start()
            End If
        End If

    End Sub
    Public Sub StopRotation()

        If B2SData.RotatingPictureBox IsNot Nothing AndAlso B2SData.RotatingPictureBox.ContainsKey(0) AndAlso B2SData.RotatingPictureBox(0) IsNot Nothing Then
            If rotateTimer.Enabled Then
                If B2SData.RotatingPictureBox(0).SnippitRotationStopBehaviour = B2SPictureBox.eSnippitRotationStopBehaviour.SpinOff Then
                    rotateSlowDownSteps = 1
                ElseIf B2SData.RotatingPictureBox(0).SnippitRotationStopBehaviour = B2SPictureBox.eSnippitRotationStopBehaviour.RunAnimationTillEnd Then
                    rotateRunTillEnd = True
                ElseIf B2SData.RotatingPictureBox(0).SnippitRotationStopBehaviour = B2SPictureBox.eSnippitRotationStopBehaviour.RunAnimationToFirstStep Then
                    rotateRunToFirstStep = True
                Else
                    rotateTimer.Stop()
                End If
            End If
        End If

    End Sub

    Private SelectedLEDType As B2SSettings.eLEDTypes = B2SSettings.eLEDTypes.Undefined
    Public Sub ShowScoreDisplays()

        If SelectedLEDType = B2SSettings.eLEDTypes.Undefined Then SelectedLEDType = GetLEDType()
        If SelectedLEDType = B2SSettings.eLEDTypes.Dream7 Then
            For Each leddisplay As KeyValuePair(Of String, Dream7Display) In B2SData.LEDDisplays
                leddisplay.Value.Visible = True
            Next
        ElseIf SelectedLEDType = B2SSettings.eLEDTypes.Rendered Then
            For Each led As KeyValuePair(Of String, B2SLEDBox) In B2SData.LEDs
                led.Value.Visible = True
            Next
        End If

    End Sub
    Public Sub HideScoreDisplays()

        If SelectedLEDType = B2SSettings.eLEDTypes.Undefined Then SelectedLEDType = GetLEDType()
        If SelectedLEDType = B2SSettings.eLEDTypes.Dream7 Then
            For Each leddisplay As KeyValuePair(Of String, Dream7Display) In B2SData.LEDDisplays
                leddisplay.Value.Visible = False
            Next
        ElseIf SelectedLEDType = B2SSettings.eLEDTypes.Rendered Then
            For Each led As KeyValuePair(Of String, B2SLEDBox) In B2SData.LEDs
                led.Value.Visible = False
            Next
        End If

    End Sub
    Private Function GetLEDType() As B2SSettings.eLEDTypes
        Dim ret As B2SSettings.eLEDTypes = B2SSettings.eLEDTypes.Undefined
        If B2SData.LEDDisplays.Count > 0 Then
            For Each leddisplay As KeyValuePair(Of String, Dream7Display) In B2SData.LEDDisplays
                If leddisplay.Value.Visible Then ret = B2SSettings.eLEDTypes.Dream7
                Exit For
            Next
        ElseIf B2SData.LEDs.Count > 0 Then
            For Each led As KeyValuePair(Of String, B2SLEDBox) In B2SData.LEDs
                If led.Value.Visible Then ret = B2SSettings.eLEDTypes.Rendered
                Exit For
            Next
        End If
        Return ret
    End Function

    Public Sub PlaySound(ByVal soundname As String)

        If B2SData.Sounds.ContainsKey(soundname) Then
            My.Computer.Audio.Play(B2SData.Sounds(soundname), AudioPlayMode.Background)
        End If

    End Sub
    Public Sub StopSound(ByVal soundname As String)

        If B2SData.Sounds.ContainsKey(soundname) Then
            My.Computer.Audio.Stop()
        End If

    End Sub

    Public Sub GetScaleFactor(ByRef scale As SizeF)

        scale = B2SScreen.BackglassRescaleFactor

    End Sub
#End Region


#Region "load B2S data and more B2S stuff"

    Private Sub LoadB2SData()

        Dim filename As String = B2SData.TableFileName & ".directb2s"
        Dim shortFilename As String = B2SData.ShortFileName(filename) & ".directb2s"
        Dim hyperpinFilename As String = filename
        Dim shorthyperpinFilename As String = filename

        B2SSettings.MatchingFileNames = Array.Empty(Of String)()

        ' check whether the table name can be found
        If Not String.IsNullOrEmpty(B2SSettings.GameName) And Not IO.File.Exists(filename) AndAlso Not IO.File.Exists(shortFilename) Then
            'Westworld, check for gamename
            If IO.File.Exists(B2SSettings.GameName & ".directb2s") Then
                filename = B2SSettings.GameName & ".directb2s"
            End If
        End If

        If Not B2SSettings.DisableFuzzyMatching Then
            B2SScreen.debugLog.WriteLogEntry("FuzzyMatching")
            If Not IO.File.Exists(filename) AndAlso Not IO.File.Exists(shortFilename) Then
                If B2SSettings.LocateHyperpinXMLFile() Then
                    hyperpinFilename = B2SSettings.HyperpinName & ".directb2s"
                    shorthyperpinFilename = B2SData.ShortFileName(hyperpinFilename) & ".directb2s"
                End If
                ' check whether the hyperpin description can be found
                If Not IO.File.Exists(hyperpinFilename) AndAlso Not IO.File.Exists(shorthyperpinFilename) Then
                    If filename.Length >= 10 Then
                        ' look for short name
                        B2SScreen.debugLog.WriteLogEntry("FuzzyMatching Search for FileName:" & Directory.GetCurrentDirectory() & "\" & Path.GetFileNameWithoutExtension(shortFilename) & "*.directb2s")
                        B2SSettings.MatchingFileNames = IO.Directory.GetFiles(Directory.GetCurrentDirectory(), Path.GetFileNameWithoutExtension(shortFilename) & "*.directb2s") _
                            .Select(Function(f) IO.Path.GetFileName(f)).ToArray()
                        Dim candidateFilenames As New List(Of String)(B2SSettings.MatchingFileNames)
                        Dim bestMatch As String = B2SData.FuzzyFileName.FindBestMatch(filename, candidateFilenames)

                        If Not String.IsNullOrEmpty(bestMatch) Then
                            shortFilename = bestMatch
                            B2SScreen.debugLog.WriteLogEntry("FuzzyMatching Selected FileName:" & shortFilename)
                        End If
                    End If
                End If
                B2SScreen.debugLog.WriteLogEntry("FuzzyMatching END")
            Else
                B2SScreen.debugLog.WriteLogEntry("FuzzyMatching END - Found matching filename")
            End If
        End If

        If Not IO.File.Exists(filename) AndAlso Not IO.File.Exists(shortFilename) AndAlso Not IO.File.Exists(hyperpinFilename) AndAlso Not IO.File.Exists(shorthyperpinFilename) Then
            Dim text As String = "File '" & IO.Path.Combine(IO.Directory.GetCurrentDirectory(), filename)
            If Not String.IsNullOrEmpty(hyperpinFilename) AndAlso Not filename.Equals(hyperpinFilename, StringComparison.CurrentCultureIgnoreCase) Then
                text &= " and file '" & IO.Path.Combine(IO.Directory.GetCurrentDirectory(), hyperpinFilename) & "'"
            End If
            text &= " not found. Please rename or download the matching directb2s backglass file."
            Throw New Exception(text)
        End If

        Dim XML As Xml.XmlDocument = New Xml.XmlDocument
        If IO.File.Exists(filename) Then
            B2SData.BackglassFileName = filename
        ElseIf Not B2SSettings.DisableFuzzyMatching Then
            If IO.File.Exists(shortFilename) Then
                B2SData.BackglassFileName = shortFilename
            ElseIf IO.File.Exists(hyperpinFilename) Then
                B2SData.BackglassFileName = hyperpinFilename
            ElseIf IO.File.Exists(shorthyperpinFilename) Then
                B2SData.BackglassFileName = shorthyperpinFilename
            End If
        End If

        ' maybe load XML file
        If Not String.IsNullOrEmpty(B2SData.BackglassFileName) Then
            Try
                XML.Load(B2SData.BackglassFileName)
            Catch ex As Exception
                MessageBox.Show("The following error occurred opening the file '" & Path.GetFileName(B2SData.BackglassFileName) & "':" & vbCrLf & vbCrLf & ex.Message, My.Resources.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            End Try
        End If

        ' try to get into the file and read some XML
        If XML Is Nothing OrElse XML.SelectSingleNode("DirectB2SData") Is Nothing Then

            Throw New Exception("File '" & B2SData.BackglassFileName & "' is not a valid directb2s backglass file.")

        Else

            B2SSettings.BackglassFileVersion = XML.SelectSingleNode("DirectB2SData").Attributes("Version").InnerText
#If B2S = "DLL" Then
            ' write backglass file version to registry
            Using regkey As RegistryKey = Registry.CurrentUser.OpenSubKey("Software\B2S", True)
                regkey.SetValue("B2SBackglassFileVersion", B2SSettings.BackglassFileVersion, RegistryValueKind.String)
            End Using
#End If
            ' current backglass version is not allowed to be larger than server version and to be smaller minimum B2S version
            If B2SSettings.BackglassFileVersion > B2SVersionInfo.B2S_VERSION_STRING Then

                Throw New Exception("B2S.Server version (" & B2SVersionInfo.B2S_VERSION_STRING & ") doesn't match 'directb2s' file version (" & B2SSettings.BackglassFileVersion & "). " & vbCrLf & vbCrLf &
                                    "Please update the B2S.Server.")

            ElseIf B2SSettings.BackglassFileVersion < B2SSettings.MinimumDirectB2SVersion Then

                Throw New Exception("'directB2S' file version (" & B2SSettings.BackglassFileVersion & ") doesn't match minimum 'directb2s' version. " & vbCrLf & vbCrLf &
                                    "Please update the 'directB2S' backglass file.")

            Else

                ' get top node
                Dim topnode As Xml.XmlElement = XML.SelectSingleNode("DirectB2SData")

                ' clear all data
                B2SData.ClearAll(True)
                B2SStatistics.ClearAll()

                ' get some basic info
                B2SData.TableName = topnode.SelectSingleNode("Name").Attributes("Value").InnerText
                B2SData.TableType = topnode.SelectSingleNode("TableType").Attributes("Value").InnerText
                B2SData.DMDType = topnode.SelectSingleNode("DMDType").Attributes("Value").InnerText
                If topnode.SelectSingleNode("DMDDefaultLocation") IsNot Nothing Then
                    B2SData.DMDDefaultLocation = New Point(CInt(topnode.SelectSingleNode("DMDDefaultLocation").Attributes("LocX").InnerText), CInt(topnode.SelectSingleNode("DMDDefaultLocation").Attributes("LocY").InnerText))
                End If
                B2SData.GrillHeight = Math.Max(CInt(topnode.SelectSingleNode("GrillHeight").Attributes("Value").InnerText), 0)
                If topnode.SelectSingleNode("GrillHeight").Attributes("Small") IsNot Nothing AndAlso B2SData.GrillHeight > 0 Then
                    B2SData.SmallGrillHeight = Math.Max(CInt(topnode.SelectSingleNode("GrillHeight").Attributes("Small").InnerText), 0)
                End If
                If topnode.SelectSingleNode("DualBackglass") IsNot Nothing Then
                    B2SData.DualBackglass = (topnode.SelectSingleNode("DualBackglass").Attributes("Value").InnerText = "1")
                End If

                ' maybe set current dual mode to get for sure
                If Not B2SData.DualBackglass Then
                    B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Authentic
                End If

                ' get skip defaults
                If topnode.SelectSingleNode("LampsDefaultSkipFrames") IsNot Nothing AndAlso Not B2SSettings.IsGameNameFound Then
                    B2SSettings.LampsSkipFrames = CInt(topnode.SelectSingleNode("LampsDefaultSkipFrames").Attributes("Value").InnerText)
                End If
                If topnode.SelectSingleNode("SolenoidsDefaultSkipFrames") IsNot Nothing AndAlso Not B2SSettings.IsGameNameFound Then
                    B2SSettings.SolenoidsSkipFrames = CInt(topnode.SelectSingleNode("SolenoidsDefaultSkipFrames").Attributes("Value").InnerText)
                End If
                If topnode.SelectSingleNode("GIStringsDefaultSkipFrames") IsNot Nothing AndAlso Not B2SSettings.IsGameNameFound Then
                    B2SSettings.GIStringsSkipFrames = CInt(topnode.SelectSingleNode("GIStringsDefaultSkipFrames").Attributes("Value").InnerText)
                End If
                If topnode.SelectSingleNode("LEDsDefaultSkipFrames") IsNot Nothing AndAlso Not B2SSettings.IsGameNameFound Then
                    B2SSettings.LEDsSkipFrames = CInt(topnode.SelectSingleNode("LEDsDefaultSkipFrames").Attributes("Value").InnerText)
                End If
                If Not B2SSettings.IsGameNameFound Then
                    If B2SSettings.LampsSkipFrames = 0 Then B2SSettings.LampsSkipFrames = 1
                    If B2SSettings.SolenoidsSkipFrames = 0 Then B2SSettings.SolenoidsSkipFrames = 3
                    If B2SSettings.GIStringsSkipFrames = 0 Then B2SSettings.GIStringsSkipFrames = 3
                End If

                ' get all illumination infos
                Dim roms4Authentic As Generic.Dictionary(Of String, Integer) = New Generic.Dictionary(Of String, Integer)
                Dim roms4Fantasy As Generic.Dictionary(Of String, Integer) = New Generic.Dictionary(Of String, Integer)
                If topnode.SelectSingleNode("Illumination") IsNot Nothing AndAlso topnode.SelectNodes("Illumination/Bulb") IsNot Nothing Then
                    For Each innerNode As Xml.XmlElement In topnode.SelectNodes("Illumination/Bulb")
                        Dim parent As String = innerNode.Attributes("Parent").InnerText
                        Dim id As Integer = CInt(innerNode.Attributes("ID").InnerText)
                        Dim romid As Integer = 0
                        Dim romidtype As Integer = 0
                        Dim romidvalue As Integer = 0
                        Dim rominverted As Boolean = False
                        If innerNode.Attributes("B2SID") IsNot Nothing Then
                            romid = CInt(innerNode.Attributes("B2SID").InnerText)
                            If innerNode.Attributes("B2SValue") IsNot Nothing Then
                                romidvalue = CInt(innerNode.Attributes("B2SValue").InnerText)
                            End If
                            romidtype = 1
                        Else
                            romid = CInt(innerNode.Attributes("RomID").InnerText)
                            romidtype = CInt(innerNode.Attributes("RomIDType").InnerText)
                            If innerNode.Attributes("RomInverted") IsNot Nothing Then
                                rominverted = (innerNode.Attributes("RomInverted").InnerText = "1")
                            End If
                        End If
                        Dim intensity As Integer = CInt(innerNode.Attributes("Intensity").InnerText)
                        Dim initialstate As Integer = CInt(innerNode.Attributes("InitialState").InnerText)
                        Dim flasherPulseDuration As Integer = 0
                        If innerNode.Attributes("FlasherPulseDuration") IsNot Nothing Then
                            Integer.TryParse(innerNode.Attributes("FlasherPulseDuration").InnerText, flasherPulseDuration)
                            If flasherPulseDuration > 0 Then flasherPulseDuration = Math.Max(50, Math.Min(5000, flasherPulseDuration))
                        End If
                        Dim dualmode As Integer = 0
                        If innerNode.Attributes("DualMode") IsNot Nothing Then
                            dualmode = CInt(innerNode.Attributes("DualMode").InnerText)
                        End If
                        Dim name As String = innerNode.Attributes("Name").InnerText
                        Dim isimagesnippit As Boolean = False
                        If innerNode.Attributes("IsImageSnippit") IsNot Nothing Then
                            isimagesnippit = (innerNode.Attributes("IsImageSnippit").InnerText = "1")
                        End If
                        Dim zorder As Integer = 0
                        If innerNode.Attributes("ZOrder") IsNot Nothing Then
                            zorder = CInt(innerNode.Attributes("ZOrder").InnerText)
                        End If
                        Dim picboxtype As B2SPictureBox.ePictureBoxType = B2SPictureBox.ePictureBoxType.StandardImage
                        Dim picboxrotatesteps As Integer = 0
                        Dim picboxrotateinterval As Integer = 0
                        Dim picboxrotationdirection As B2SPictureBox.eSnippitRotationDirection = B2SPictureBox.eSnippitRotationDirection.Clockwise
                        Dim picboxrotationstopbehaviour As B2SPictureBox.eSnippitRotationStopBehaviour = B2SPictureBox.eSnippitRotationStopBehaviour.SpinOff
                        Dim automaticrotationcontinuous As Boolean = False
                        Dim nativerotation As Boolean = False
                        Dim snippitbehindcanvas As Boolean = False
                        If innerNode.Attributes("SnippitType") IsNot Nothing Then
                            picboxtype = CInt(innerNode.Attributes("SnippitType").InnerText)
                            If innerNode.Attributes("SnippitRotatingSteps") IsNot Nothing Then
                                picboxrotatesteps = CInt(innerNode.Attributes("SnippitRotatingSteps").InnerText)
                            ElseIf innerNode.Attributes("SnippitRotatingAngle") IsNot Nothing Then
                                Try
                                    picboxrotatesteps = CInt(360 / CInt(innerNode.Attributes("SnippitRotatingAngle").InnerText))
                                Catch
                                End Try
                            End If
                            If innerNode.Attributes("SnippitRotatingInterval") IsNot Nothing Then
                                picboxrotateinterval = CInt(innerNode.Attributes("SnippitRotatingInterval").InnerText)
                            End If
                            If innerNode.Attributes("SnippitRotatingDirection") IsNot Nothing Then
                                picboxrotationdirection = CInt(innerNode.Attributes("SnippitRotatingDirection").InnerText)
                            End If
                            If innerNode.Attributes("SnippitRotatingStopBehaviour") IsNot Nothing Then
                                picboxrotationstopbehaviour = CInt(innerNode.Attributes("SnippitRotatingStopBehaviour").InnerText)
                            End If
                        End If
                        If picboxtype = B2SPictureBox.ePictureBoxType.StandardImage AndAlso
                           innerNode.Attributes("AutomaticRotationEnabled") IsNot Nothing Then
                            nativerotation = (innerNode.Attributes("AutomaticRotationEnabled").InnerText = "1")
                        End If
                        ' Legacy mechanical snippets already contain one source bitmap and
                        ' receive absolute 0-359 degree positions from VPinMAME. Render that
                        ' source directly at the reported angle instead of indexing the old
                        ' step-count bitmap cache, whose 0..steps-1 keys discard most values.
                        ' This is a runtime-only compatibility route; the directB2S remains
                        ' in its original legacy format and is not converted by the editor.
                        If picboxtype = B2SPictureBox.ePictureBoxType.MechRotatingImage AndAlso
                           picboxrotatesteps > 0 Then
                            nativerotation = True
                        End If
                        If nativerotation AndAlso innerNode.Attributes("AutomaticRotationContinuous") IsNot Nothing Then
                            automaticrotationcontinuous = (innerNode.Attributes("AutomaticRotationContinuous").InnerText = "1")
                        End If
                        If nativerotation AndAlso picboxtype <> B2SPictureBox.ePictureBoxType.MechRotatingImage Then
                            If innerNode.Attributes("AutomaticRotationTriggerID") IsNot Nothing Then
                                romid = Math.Max(0, CInt(innerNode.Attributes("AutomaticRotationTriggerID").InnerText))
                            End If
                            If innerNode.Attributes("AutomaticRotationTriggerType") IsNot Nothing Then
                                Dim triggerType As Integer = CInt(innerNode.Attributes("AutomaticRotationTriggerType").InnerText)
                                romidtype = If(triggerType = CInt(B2SBaseBox.eRomIDType.Solenoid),
                                               B2SBaseBox.eRomIDType.Solenoid,
                                               B2SBaseBox.eRomIDType.Lamp)
                            End If
                            If automaticrotationcontinuous Then
                                romid = 0
                                romidtype = B2SBaseBox.eRomIDType.NotDefined
                            End If
                            If innerNode.Attributes("AutomaticRotationSteps") IsNot Nothing Then
                                picboxrotatesteps = Math.Max(4, Math.Min(360, CInt(innerNode.Attributes("AutomaticRotationSteps").InnerText)))
                            End If
                            If innerNode.Attributes("AutomaticRotationInterval") IsNot Nothing Then
                                picboxrotateinterval = Math.Max(10, Math.Min(500, CInt(innerNode.Attributes("AutomaticRotationInterval").InnerText)))
                            End If
                            If innerNode.Attributes("AutomaticRotationDirection") IsNot Nothing Then
                                picboxrotationdirection = CInt(innerNode.Attributes("AutomaticRotationDirection").InnerText)
                            End If
                            If innerNode.Attributes("AutomaticRotationStopBehaviour") IsNot Nothing Then
                                picboxrotationstopbehaviour = CInt(innerNode.Attributes("AutomaticRotationStopBehaviour").InnerText)
                            End If
                        End If
                        If innerNode.Attributes("SnippitBehindCanvas") IsNot Nothing Then
                            snippitbehindcanvas = (innerNode.Attributes("SnippitBehindCanvas").InnerText = "1")
                        End If
                        Dim motionPathDuration As Integer = 3000
                        Dim motionPathLoop As Boolean = False
                        Dim motionPathSolenoidID As Integer = 0
                        Dim motionPathLampID As Integer = 0
                        Dim motionPathB2SID As Integer = 0
                        Dim motionPathStopB2SID As Integer = 0
                        Dim motionPathResumeB2SID As Integer = 0
                        Dim motionPathQueueTriggers As Boolean = False
                        Dim motionPathRollEnabled As Boolean = False
                        Dim motionPathSequenceGroup As String = String.Empty
                        Dim motionPathSequenceOrder As Integer = 0
                        Dim motionPathRespawnEnabled As Boolean = False
                        Dim motionPathRespawnPoint As PointF = PointF.Empty
                        Dim motionPathRespawnDuration As Integer = 350
                        Dim motionPathExitDuration As Integer = 3000
                        Dim motionPathRemoveSolenoidID As Integer = 0
                        Dim motionPathRemoveLampID As Integer = 0
                        Dim motionPathRemoveB2SID As Integer = 0
                        Dim motionPathExitPoints As List(Of PointF) = Nothing
                        Dim motionPathPoints As List(Of PointF) = Nothing
                        Dim externalGridName As String = String.Empty
                        Dim externalGridRepresentative As Boolean = False
                        Dim externalGridDuration As Integer = 45
                        Dim externalPivotGroup As String = String.Empty
                        Dim externalPivotRepresentative As Boolean = False
                        Dim externalPivotAngle As Single = 0.0F
                        Dim externalPivotDuration As Integer = 80
                        Dim externalPivotX As Single = 0.5F
                        Dim externalPivotY As Single = 0.5F
                        Dim externalPivotTipX As Single = 0.9F
                        Dim externalPivotTipY As Single = 0.5F
                        Dim pivotAnimation As Boolean = False
                        Dim pivotDownAngle As Single = 0.0F
                        Dim pivotUpAngle As Single = -30.0F
                        Dim pivotDownTrigger As String = String.Empty
                        Dim pivotUpTrigger As String = String.Empty
                        Dim pivotTriggerType As Integer = 1
                        Dim pivotTriggerID As Integer = 0
                        Dim physicsBall As Boolean = False
                        Dim physicsFlipperName As String = String.Empty
                        Dim physicsBounds As RectangleF = RectangleF.Empty
                        Dim physicsGravity As Single = 650.0F
                        Dim physicsFlipperStrength As Single = 1.0F
                        Dim physicsBoundaryBounce As Single = 0.12F
                        Dim physicsFloorPoints As List(Of PointF) = Nothing
                        Dim physicsBoundaryPaths As List(Of List(Of PointF)) = Nothing
                        Dim physicsObstacles As List(Of RectangleF) = Nothing
                        Dim physicsSwitchZones As List(Of B2SData.PhysicsSwitchZone) = Nothing
                        Dim physicsLauncher As B2SData.PhysicsLauncher = Nothing
                        If innerNode.Attributes("ExternalGridName") IsNot Nothing Then
                            externalGridName = innerNode.Attributes("ExternalGridName").InnerText.Trim()
                        End If
                        If innerNode.Attributes("ExternalGridRepresentative") IsNot Nothing Then
                            externalGridRepresentative = (innerNode.Attributes("ExternalGridRepresentative").InnerText = "1")
                        End If
                        If innerNode.Attributes("ExternalGridDuration") IsNot Nothing Then
                            Integer.TryParse(innerNode.Attributes("ExternalGridDuration").InnerText, externalGridDuration)
                            externalGridDuration = Math.Max(10, Math.Min(1000, externalGridDuration))
                        End If
                        If innerNode.Attributes("ExternalPivotGroup") IsNot Nothing Then externalPivotGroup = innerNode.Attributes("ExternalPivotGroup").InnerText.Trim()
                        If innerNode.Attributes("ExternalPivotRepresentative") IsNot Nothing Then externalPivotRepresentative = (innerNode.Attributes("ExternalPivotRepresentative").InnerText = "1")
                        If innerNode.Attributes("ExternalPivotAngle") IsNot Nothing Then Single.TryParse(innerNode.Attributes("ExternalPivotAngle").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, externalPivotAngle)
                        If innerNode.Attributes("ExternalPivotDuration") IsNot Nothing Then Integer.TryParse(innerNode.Attributes("ExternalPivotDuration").InnerText, externalPivotDuration)
                        If innerNode.Attributes("ExternalPivotX") IsNot Nothing Then Single.TryParse(innerNode.Attributes("ExternalPivotX").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, externalPivotX)
                        If innerNode.Attributes("ExternalPivotY") IsNot Nothing Then Single.TryParse(innerNode.Attributes("ExternalPivotY").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, externalPivotY)
                        If innerNode.Attributes("PivotAnimation") IsNot Nothing Then pivotAnimation = (innerNode.Attributes("PivotAnimation").InnerText = "1")
                        If innerNode.Attributes("PivotX") IsNot Nothing Then Single.TryParse(innerNode.Attributes("PivotX").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, externalPivotX)
                        If innerNode.Attributes("PivotY") IsNot Nothing Then Single.TryParse(innerNode.Attributes("PivotY").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, externalPivotY)
                        If innerNode.Attributes("PivotTipX") IsNot Nothing Then Single.TryParse(innerNode.Attributes("PivotTipX").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, externalPivotTipX)
                        If innerNode.Attributes("PivotTipY") IsNot Nothing Then Single.TryParse(innerNode.Attributes("PivotTipY").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, externalPivotTipY)
                        If innerNode.Attributes("PivotDownAngle") IsNot Nothing Then Single.TryParse(innerNode.Attributes("PivotDownAngle").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, pivotDownAngle)
                        If innerNode.Attributes("PivotUpAngle") IsNot Nothing Then Single.TryParse(innerNode.Attributes("PivotUpAngle").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, pivotUpAngle)
                        If innerNode.Attributes("PivotDuration") IsNot Nothing Then Integer.TryParse(innerNode.Attributes("PivotDuration").InnerText, externalPivotDuration)
                        If innerNode.Attributes("PivotDownTrigger") IsNot Nothing Then pivotDownTrigger = innerNode.Attributes("PivotDownTrigger").InnerText.Trim()
                        If innerNode.Attributes("PivotUpTrigger") IsNot Nothing Then pivotUpTrigger = innerNode.Attributes("PivotUpTrigger").InnerText.Trim()
                        If innerNode.Attributes("PivotTriggerType") IsNot Nothing Then Integer.TryParse(innerNode.Attributes("PivotTriggerType").InnerText, pivotTriggerType)
                        If innerNode.Attributes("PivotTriggerID") IsNot Nothing Then Integer.TryParse(innerNode.Attributes("PivotTriggerID").InnerText, pivotTriggerID)
                        If innerNode.Attributes("PhysicsBall") IsNot Nothing Then physicsBall = (innerNode.Attributes("PhysicsBall").InnerText = "1")
                        If innerNode.Attributes("PhysicsFlipperName") IsNot Nothing Then physicsFlipperName = innerNode.Attributes("PhysicsFlipperName").InnerText.Trim()
                        If innerNode.Attributes("PhysicsBounds") IsNot Nothing Then physicsBounds = ParsePhysicsBounds(innerNode.Attributes("PhysicsBounds").InnerText)
                        If innerNode.Attributes("PhysicsGravity") IsNot Nothing Then Single.TryParse(innerNode.Attributes("PhysicsGravity").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, physicsGravity)
                        If innerNode.Attributes("PhysicsFlipperStrength") IsNot Nothing Then Single.TryParse(innerNode.Attributes("PhysicsFlipperStrength").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, physicsFlipperStrength)
                        If innerNode.Attributes("PhysicsBoundaryBounce") IsNot Nothing Then Single.TryParse(innerNode.Attributes("PhysicsBoundaryBounce").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, physicsBoundaryBounce)
                        If innerNode.Attributes("PhysicsFloorPoints") IsNot Nothing Then physicsFloorPoints = ParseMotionPathPoints(innerNode.Attributes("PhysicsFloorPoints").InnerText)
                        If innerNode.Attributes("PhysicsBoundaries") IsNot Nothing Then physicsBoundaryPaths = ParsePhysicsBoundaries(innerNode.Attributes("PhysicsBoundaries").InnerText)
                        If innerNode.Attributes("PhysicsObstacles") IsNot Nothing Then physicsObstacles = ParsePhysicsObstacles(innerNode.Attributes("PhysicsObstacles").InnerText)
                        If innerNode.Attributes("PhysicsSwitchZones") IsNot Nothing Then physicsSwitchZones = ParsePhysicsSwitchZones(innerNode.Attributes("PhysicsSwitchZones").InnerText)
                        If innerNode.Attributes("PhysicsLauncherEnabled") IsNot Nothing AndAlso innerNode.Attributes("PhysicsLauncherEnabled").InnerText = "1" Then
                            physicsLauncher = New B2SData.PhysicsLauncher With {.TriggerType = 1, .Angle = -90.0F, .Strength = 900.0F}
                            If innerNode.Attributes("PhysicsLauncherTriggerType") IsNot Nothing Then Integer.TryParse(innerNode.Attributes("PhysicsLauncherTriggerType").InnerText, physicsLauncher.TriggerType)
                            If innerNode.Attributes("PhysicsLauncherTriggerID") IsNot Nothing Then Integer.TryParse(innerNode.Attributes("PhysicsLauncherTriggerID").InnerText, physicsLauncher.TriggerID)
                            Dim launcherX As Single = 0.0F, launcherY As Single = 0.0F
                            If innerNode.Attributes("PhysicsLauncherX") IsNot Nothing Then Single.TryParse(innerNode.Attributes("PhysicsLauncherX").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, launcherX)
                            If innerNode.Attributes("PhysicsLauncherY") IsNot Nothing Then Single.TryParse(innerNode.Attributes("PhysicsLauncherY").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, launcherY)
                            physicsLauncher.Origin = New PointF(launcherX, launcherY)
                            If innerNode.Attributes("PhysicsLauncherAngle") IsNot Nothing Then Single.TryParse(innerNode.Attributes("PhysicsLauncherAngle").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, physicsLauncher.Angle)
                            If innerNode.Attributes("PhysicsLauncherStrength") IsNot Nothing Then Single.TryParse(innerNode.Attributes("PhysicsLauncherStrength").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, physicsLauncher.Strength)
                            If innerNode.Attributes("PhysicsLauncherRandomAngle") IsNot Nothing Then Single.TryParse(innerNode.Attributes("PhysicsLauncherRandomAngle").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, physicsLauncher.RandomAngle)
                            If innerNode.Attributes("PhysicsLauncherRandomStrength") IsNot Nothing Then Single.TryParse(innerNode.Attributes("PhysicsLauncherRandomStrength").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, physicsLauncher.RandomStrength)
                            physicsLauncher.CaptureRadius = 45.0F
                            If innerNode.Attributes("PhysicsLauncherCaptureRadius") IsNot Nothing Then Single.TryParse(innerNode.Attributes("PhysicsLauncherCaptureRadius").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, physicsLauncher.CaptureRadius)
                        End If
                        If (physicsBoundaryPaths Is Nothing OrElse physicsBoundaryPaths.Count = 0) AndAlso physicsFloorPoints IsNot Nothing AndAlso physicsFloorPoints.Count >= 2 Then
                            physicsBoundaryPaths = New List(Of List(Of PointF)) From {physicsFloorPoints}
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathDuration") IsNot Nothing Then
                            Integer.TryParse(innerNode.Attributes("MotionPathDuration").InnerText, motionPathDuration)
                            motionPathDuration = Math.Max(250, Math.Min(30000, motionPathDuration))
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathPoints") IsNot Nothing Then
                            motionPathPoints = ParseMotionPathPoints(innerNode.Attributes("MotionPathPoints").InnerText)
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathLoop") IsNot Nothing Then
                            motionPathLoop = (innerNode.Attributes("MotionPathLoop").InnerText = "1")
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathSolenoidID") IsNot Nothing Then
                            Integer.TryParse(innerNode.Attributes("MotionPathSolenoidID").InnerText, motionPathSolenoidID)
                            motionPathSolenoidID = Math.Max(0, Math.Min(255, motionPathSolenoidID))
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathLampID") IsNot Nothing Then
                            Integer.TryParse(innerNode.Attributes("MotionPathLampID").InnerText, motionPathLampID)
                            motionPathLampID = Math.Max(0, Math.Min(255, motionPathLampID))
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathB2SID") IsNot Nothing Then
                            Integer.TryParse(innerNode.Attributes("MotionPathB2SID").InnerText, motionPathB2SID)
                            motionPathB2SID = Math.Max(0, Math.Min(250, motionPathB2SID))
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathStopB2SID") IsNot Nothing Then
                            Integer.TryParse(innerNode.Attributes("MotionPathStopB2SID").InnerText, motionPathStopB2SID)
                            motionPathStopB2SID = Math.Max(0, Math.Min(250, motionPathStopB2SID))
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathResumeB2SID") IsNot Nothing Then
                            Integer.TryParse(innerNode.Attributes("MotionPathResumeB2SID").InnerText, motionPathResumeB2SID)
                            motionPathResumeB2SID = Math.Max(0, Math.Min(250, motionPathResumeB2SID))
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathQueueTriggers") IsNot Nothing Then
                            motionPathQueueTriggers = (innerNode.Attributes("MotionPathQueueTriggers").InnerText = "1")
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathRollEnabled") IsNot Nothing Then
                            motionPathRollEnabled = (innerNode.Attributes("MotionPathRollEnabled").InnerText = "1")
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathSequenceGroup") IsNot Nothing Then
                            motionPathSequenceGroup = innerNode.Attributes("MotionPathSequenceGroup").InnerText.Trim()
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathSequenceOrder") IsNot Nothing Then
                            Integer.TryParse(innerNode.Attributes("MotionPathSequenceOrder").InnerText, motionPathSequenceOrder)
                            motionPathSequenceOrder = Math.Max(0, Math.Min(999, motionPathSequenceOrder))
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathRespawnEnabled") IsNot Nothing Then
                            motionPathRespawnEnabled = (innerNode.Attributes("MotionPathRespawnEnabled").InnerText = "1")
                        End If
                        If isimagesnippit AndAlso motionPathRespawnEnabled Then
                            Dim respawnX As Single = 0.0F, respawnY As Single = 0.0F
                            If innerNode.Attributes("MotionPathRespawnX") IsNot Nothing Then Single.TryParse(innerNode.Attributes("MotionPathRespawnX").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, respawnX)
                            If innerNode.Attributes("MotionPathRespawnY") IsNot Nothing Then Single.TryParse(innerNode.Attributes("MotionPathRespawnY").InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, respawnY)
                            If innerNode.Attributes("MotionPathRespawnDuration") IsNot Nothing Then Integer.TryParse(innerNode.Attributes("MotionPathRespawnDuration").InnerText, motionPathRespawnDuration)
                            motionPathRespawnPoint = New PointF(respawnX, respawnY)
                            motionPathRespawnDuration = Math.Max(50, Math.Min(5000, motionPathRespawnDuration))
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathExitDuration") IsNot Nothing Then
                            Integer.TryParse(innerNode.Attributes("MotionPathExitDuration").InnerText, motionPathExitDuration)
                            motionPathExitDuration = Math.Max(250, Math.Min(30000, motionPathExitDuration))
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathRemoveSolenoidID") IsNot Nothing Then
                            Integer.TryParse(innerNode.Attributes("MotionPathRemoveSolenoidID").InnerText, motionPathRemoveSolenoidID)
                            motionPathRemoveSolenoidID = Math.Max(0, Math.Min(255, motionPathRemoveSolenoidID))
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathRemoveLampID") IsNot Nothing Then
                            Integer.TryParse(innerNode.Attributes("MotionPathRemoveLampID").InnerText, motionPathRemoveLampID)
                            motionPathRemoveLampID = Math.Max(0, Math.Min(255, motionPathRemoveLampID))
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathRemoveB2SID") IsNot Nothing Then
                            Integer.TryParse(innerNode.Attributes("MotionPathRemoveB2SID").InnerText, motionPathRemoveB2SID)
                            motionPathRemoveB2SID = Math.Max(0, Math.Min(250, motionPathRemoveB2SID))
                        End If
                        If isimagesnippit AndAlso innerNode.Attributes("MotionPathExitPoints") IsNot Nothing Then
                            motionPathExitPoints = ParseMotionPathPoints(innerNode.Attributes("MotionPathExitPoints").InnerText)
                        End If
                        Dim visible As Boolean = (CInt(innerNode.Attributes("Visible").InnerText) = 1)
                        Dim loc As Point = New Point(CInt(innerNode.Attributes("LocX").InnerText), CInt(innerNode.Attributes("LocY").InnerText))
                        Dim size As Size = New Size(CInt(innerNode.Attributes("Width").InnerText), CInt(innerNode.Attributes("Height").InnerText))
                        Dim image As Image = Base64ToImage(innerNode.Attributes("Image").InnerText)
                        Dim offimage As Image = Nothing
                        If innerNode.Attributes("OffImage") IsNot Nothing Then
                            offimage = Base64ToImage(innerNode.Attributes("OffImage").InnerText)
                        End If
                        Dim preserveRollingCanvas As Boolean = (isimagesnippit AndAlso motionPathRollEnabled)
                        If picboxtype = B2SPictureBox.ePictureBoxType.StandardImage AndAlso Not nativerotation AndAlso Not preserveRollingCanvas Then
                            ' Events of overlapping pictures get merged #76, crop image transparency
                            image = CropImageToTransparency(image, offimage, loc, size)
                        End If
                        ' create new picturebox control
                        Dim picbox As B2SPictureBox = New B2SPictureBox()
                        Dim IsOnBackglass As Boolean = (parent = "Backglass")
                        picbox.Name = "PictureBox" & id.ToString()
                        picbox.GroupName = name
                        picbox.Location = loc
                        picbox.Size = size
                        ' Pivot triggers own both ON and OFF states; do not also route
                        ' this image through legacy visibility switching.
                        picbox.RomID = If(pivotAnimation, 0, romid)
                        picbox.RomIDType = romidtype
                        picbox.RomIDValue = romidvalue
                        picbox.RomInverted = rominverted
                        picbox.Intensity = intensity
                        picbox.InitialState = initialstate
                        picbox.FlasherPulseDuration = flasherPulseDuration
                        picbox.DualMode = dualmode
                        picbox.BackgroundImage = image
                        picbox.OffImage = offimage
                        picbox.IsImageSnippit = isimagesnippit
                        picbox.SnippitRotationStopBehaviour = picboxrotationstopbehaviour
                        picbox.RotationDirection = picboxrotationdirection
                        picbox.AutoStartRotation = automaticrotationcontinuous
                        picbox.NativeRotation = nativerotation
                        picbox.NativeRotationSteps = Math.Max(2, picboxrotatesteps)
                        picbox.NativeRotationInterval = Math.Max(10, picboxrotateinterval)
                        picbox.RotationPivotX = Math.Max(0.0F, Math.Min(1.0F, externalPivotX))
                        picbox.RotationPivotY = Math.Max(0.0F, Math.Min(1.0F, externalPivotY))
                        picbox.RotationTipX = Math.Max(0.0F, Math.Min(1.0F, externalPivotTipX))
                        picbox.RotationTipY = Math.Max(0.0F, Math.Min(1.0F, externalPivotTipY))
                        picbox.BehindCanvas = snippitbehindcanvas
                        picbox.MotionPathDuration = motionPathDuration
                        picbox.MotionPathLoop = motionPathLoop
                        picbox.MotionPathSolenoidID = motionPathSolenoidID
                        picbox.MotionPathLampID = motionPathLampID
                        picbox.MotionPathB2SID = motionPathB2SID
                        picbox.MotionPathStopB2SID = motionPathStopB2SID
                        picbox.MotionPathResumeB2SID = motionPathResumeB2SID
                        picbox.MotionPathQueueTriggers = motionPathQueueTriggers
                        picbox.MotionPathRollEnabled = motionPathRollEnabled
                        picbox.MotionPathSequenceGroup = motionPathSequenceGroup
                        picbox.MotionPathSequenceOrder = motionPathSequenceOrder
                        picbox.MotionPathRespawnEnabled = motionPathRespawnEnabled
                        picbox.MotionPathRespawnPoint = motionPathRespawnPoint
                        picbox.MotionPathRespawnDuration = motionPathRespawnDuration
                        picbox.MotionPathExitDuration = motionPathExitDuration
                        picbox.MotionPathRemoveSolenoidID = motionPathRemoveSolenoidID
                        picbox.MotionPathRemoveLampID = motionPathRemoveLampID
                        picbox.MotionPathRemoveB2SID = motionPathRemoveB2SID
                        If motionPathPoints IsNot Nothing AndAlso motionPathPoints.Count >= 2 Then
                            picbox.MotionPathPoints.AddRange(motionPathPoints)
                        End If
                        If motionPathExitPoints IsNot Nothing AndAlso motionPathExitPoints.Count >= 2 Then picbox.MotionPathExitPoints.AddRange(motionPathExitPoints)
                        B2SData.RegisterMotionPathSequence(picbox)
                        picbox.ZOrder = zorder
                        picbox.PictureBoxType = picboxtype
                        If IsOnBackglass Then
                            picbox.Type = B2SBaseBox.eType.OnBackglass
                            Me.Controls.Add(picbox)
                            ' add to general collection
                            B2SData.Illuminations.Add(picbox)
                            ' maybe add ZOrder info
                            If zorder > 0 Then
                                B2SData.UseZOrder = True
                                B2SData.ZOrderImages.Add(picbox)
                            End If
                            ' add info to rom collection
                            If Not pivotAnimation AndAlso romid > 0 AndAlso picboxtype = B2SPictureBox.ePictureBoxType.StandardImage AndAlso Not nativerotation AndAlso romidtype <> B2SBaseBox.eRomIDType.Mech Then
                                Dim key As String = If(rominverted, "I", "") & Choose(romidtype, "L", "S", "GI") & romid.ToString() & "|" & romidvalue.ToString()
                                If picbox.DualMode = B2SData.eDualMode.Both OrElse picbox.DualMode = B2SData.eDualMode.Authentic Then
                                    If roms4Authentic.ContainsKey(key) Then roms4Authentic(key) += size.Width * size.Height Else roms4Authentic.Add(key, size.Width * size.Height)
                                End If
                                If picbox.DualMode = B2SData.eDualMode.Both OrElse picbox.DualMode = B2SData.eDualMode.Fantasy Then
                                    If roms4Fantasy.ContainsKey(key) Then roms4Fantasy(key) += size.Width * size.Height Else roms4Fantasy.Add(key, size.Width * size.Height)
                                End If
                            End If
                        Else
                            If Not B2SSettings.HideB2SDMD Then
                                CheckDMDForm()
                                picbox.Type = B2SBaseBox.eType.OnDMD
                                formDMD.Controls.Add(picbox)
                                ' add to general collection
                                B2SData.DMDIlluminations.Add(picbox)
                                ' maybe add ZOrder info
                                If zorder > 0 Then
                                    B2SData.UseDMDZOrder = True
                                    B2SData.ZOrderDMDImages.Add(picbox)
                                End If
                            End If
                        End If
                        picbox.BringToFront()
                        picbox.Visible = False

                        ' add illumination into group
                        B2SData.IlluminationGroups.Add(picbox)
                        If externalGridName.Length > 0 Then
                            B2SData.RegisterExternalPositionGrid(picbox, externalGridName, name, externalGridRepresentative, externalGridDuration)
                        End If
                        If externalPivotGroup.Length > 0 Then
                            B2SData.RegisterExternalPivot(picbox, externalPivotGroup, name, externalPivotRepresentative, externalPivotAngle, externalPivotDuration)
                        End If
                        If pivotAnimation Then
                            picbox.RotationPivotX = Math.Max(0.0F, Math.Min(1.0F, externalPivotX))
                            picbox.RotationPivotY = Math.Max(0.0F, Math.Min(1.0F, externalPivotY))
                            If pivotTriggerType = 0 Then
                                B2SData.RegisterSingleImagePivot(picbox, "Pivot:" & name, pivotDownTrigger, pivotDownAngle, pivotUpTrigger, pivotUpAngle, externalPivotDuration)
                            Else
                                B2SData.RegisterPivotTrigger(picbox, pivotTriggerType, pivotTriggerID, pivotDownAngle, pivotUpAngle, externalPivotDuration)
                            End If
                        End If
                        If physicsBall AndAlso Not physicsBounds.IsEmpty Then
                            B2SData.RegisterPhysicsBall(picbox, physicsFlipperName, physicsBounds, physicsGravity,
                                                        physicsFlipperStrength, physicsBoundaryBounce, physicsBoundaryPaths, physicsObstacles, physicsSwitchZones, physicsLauncher)
                        End If
                        If picbox.MotionPathPoints.Count >= 2 AndAlso motionPathSolenoidID > 0 Then
                            If Not B2SData.UsedMotionPathSolenoidIDs.ContainsKey(motionPathSolenoidID) Then
                                B2SData.UsedMotionPathSolenoidIDs.Add(motionPathSolenoidID, New List(Of B2SPictureBox)())
                            End If
                            B2SData.UsedMotionPathSolenoidIDs(motionPathSolenoidID).Add(picbox)
                        End If
                        If picbox.MotionPathPoints.Count >= 2 AndAlso motionPathLampID > 0 Then
                            If Not B2SData.UsedMotionPathLampIDs.ContainsKey(motionPathLampID) Then
                                B2SData.UsedMotionPathLampIDs.Add(motionPathLampID, New List(Of B2SPictureBox)())
                            End If
                            B2SData.UsedMotionPathLampIDs(motionPathLampID).Add(picbox)
                        End If
                        If picbox.MotionPathPoints.Count >= 2 AndAlso motionPathB2SID > 0 Then
                            If Not B2SData.UsedMotionPathB2SIDs.ContainsKey(motionPathB2SID) Then
                                B2SData.UsedMotionPathB2SIDs.Add(motionPathB2SID, New List(Of B2SPictureBox)())
                            End If
                            B2SData.UsedMotionPathB2SIDs(motionPathB2SID).Add(picbox)
                        End If
                        If picbox.MotionPathPoints.Count >= 2 AndAlso motionPathStopB2SID > 0 Then
                            If Not B2SData.UsedMotionPathStopB2SIDs.ContainsKey(motionPathStopB2SID) Then
                                B2SData.UsedMotionPathStopB2SIDs.Add(motionPathStopB2SID, New List(Of B2SPictureBox)())
                            End If
                            B2SData.UsedMotionPathStopB2SIDs(motionPathStopB2SID).Add(picbox)
                        End If
                        If picbox.MotionPathPoints.Count >= 2 AndAlso motionPathResumeB2SID > 0 Then
                            If Not B2SData.UsedMotionPathResumeB2SIDs.ContainsKey(motionPathResumeB2SID) Then
                                B2SData.UsedMotionPathResumeB2SIDs.Add(motionPathResumeB2SID, New List(Of B2SPictureBox)())
                            End If
                            B2SData.UsedMotionPathResumeB2SIDs(motionPathResumeB2SID).Add(picbox)
                        End If
                        If picbox.MotionPathExitPoints.Count >= 2 AndAlso motionPathRemoveSolenoidID > 0 Then
                            If Not B2SData.UsedMotionPathRemoveSolenoidIDs.ContainsKey(motionPathRemoveSolenoidID) Then B2SData.UsedMotionPathRemoveSolenoidIDs.Add(motionPathRemoveSolenoidID, New List(Of B2SPictureBox)())
                            B2SData.UsedMotionPathRemoveSolenoidIDs(motionPathRemoveSolenoidID).Add(picbox)
                        End If
                        If picbox.MotionPathExitPoints.Count >= 2 AndAlso motionPathRemoveLampID > 0 Then
                            If Not B2SData.UsedMotionPathRemoveLampIDs.ContainsKey(motionPathRemoveLampID) Then B2SData.UsedMotionPathRemoveLampIDs.Add(motionPathRemoveLampID, New List(Of B2SPictureBox)())
                            B2SData.UsedMotionPathRemoveLampIDs(motionPathRemoveLampID).Add(picbox)
                        End If
                        If picbox.MotionPathExitPoints.Count >= 2 AndAlso motionPathRemoveB2SID > 0 Then
                            If Not B2SData.UsedMotionPathRemoveB2SIDs.ContainsKey(motionPathRemoveB2SID) Then B2SData.UsedMotionPathRemoveB2SIDs.Add(motionPathRemoveB2SID, New List(Of B2SPictureBox)())
                            B2SData.UsedMotionPathRemoveB2SIDs(motionPathRemoveB2SID).Add(picbox)
                        End If

                        ' maybe do picture rotating
                        If picboxrotatesteps > 0 AndAlso nativerotation AndAlso picboxtype = B2SPictureBox.ePictureBoxType.MechRotatingImage Then
                            ' Native mechanical rotation keeps one bitmap and maps
                            ' each reported mech position directly to a paint angle.
                            B2SData.UseMechRotatingImage = True
                            If Not B2SData.RotatingPictureBox.ContainsKey(romid) Then
                                B2SData.RotatingPictureBox.Add(romid, picbox)
                            Else
                                B2SData.RotatingPictureBox(romid) = picbox
                            End If
                            picbox.SetNativeMechPosition(0)
                        ElseIf picboxrotatesteps > 0 AndAlso Not nativerotation Then
                            If picboxtype = B2SPictureBox.ePictureBoxType.SelfRotatingImage AndAlso B2SData.RotatingImages.Count = 0 Then
                                Me.rotateSteps = picboxrotatesteps
                                Me.rotateTimerInterval = picboxrotateinterval
                                RotateImage(picbox, picboxrotatesteps, picboxrotationdirection, B2SPictureBox.ePictureBoxType.SelfRotatingImage)
                            ElseIf picboxtype = B2SPictureBox.ePictureBoxType.MechRotatingImage AndAlso B2SData.RotatingImages.Count = 0 Then
                                Me.rotateSteps = picboxrotatesteps
                                RotateImage(picbox, picboxrotatesteps, picboxrotationdirection, B2SPictureBox.ePictureBoxType.MechRotatingImage, romidtype, romid)
                            End If
                        End If
                    Next
                End If

                ' get all score infos
                Dim dream7index As Integer = 1
                Dim renderedandreelindex As Integer = 1
                If topnode.SelectSingleNode("Scores") IsNot Nothing AndAlso topnode.SelectNodes("Scores/Score") IsNot Nothing Then

                    Dim rollinginterval As Integer = 0
                    If topnode.SelectSingleNode("Scores").Attributes("ReelRollingInterval") IsNot Nothing Then
                        rollinginterval = CInt(topnode.SelectSingleNode("Scores").Attributes("ReelRollingInterval").InnerText)
                    End If

                    For Each innerNode As Xml.XmlElement In topnode.SelectNodes("Scores/Score")

                        Dim parent As String = innerNode.Attributes("Parent").InnerText
                        Dim id As Integer = CInt(innerNode.Attributes("ID").InnerText)
                        Dim setid As Integer = 0
                        If innerNode.Attributes("ReelIlluImageSet") IsNot Nothing Then
                            setid = CInt(innerNode.Attributes("ReelIlluImageSet").InnerText)
                        End If
                        Dim reeltype As String = innerNode.Attributes("ReelType").InnerText
                        Dim reellitcolor As Color = String2Color(innerNode.Attributes("ReelLitColor").InnerText)
                        Dim reeldarkcolor As Color = String2Color(innerNode.Attributes("ReelDarkColor").InnerText)
                        Dim d7glow As Single = CSng(innerNode.Attributes("Glow").InnerText) / 100
                        Dim d7thickness As Single = CSng(innerNode.Attributes("Thickness").InnerText) / 100
                        Dim d7shear As Single = CSng(innerNode.Attributes("Shear").InnerText) / 100
                        Dim digits As Integer = CInt(innerNode.Attributes("Digits").InnerText)
                        Dim spacing As Integer = CInt(innerNode.Attributes("Spacing").InnerText)
                        Dim hidden As Boolean = False
                        If innerNode.Attributes("DisplayState") IsNot Nothing Then
                            hidden = (CInt(innerNode.Attributes("DisplayState").InnerText) = 1)
                        End If
                        Dim behindCanvas As Boolean = False
                        If innerNode.Attributes("BehindCanvas") IsNot Nothing Then
                            behindCanvas = (innerNode.Attributes("BehindCanvas").InnerText = "1")
                        End If
                        Dim reel3DEnabled As Boolean = False
                        Dim reel3DBrightness As Integer = 100
                        Dim reel3DTemperature As Integer = 4000
                        Dim reel3DDepth As Integer = 100
                        Dim reel3DGlass As Integer = 55
                        If innerNode.Attributes("Reel3DEnabled") IsNot Nothing Then reel3DEnabled = (innerNode.Attributes("Reel3DEnabled").InnerText = "1")
                        Dim parsedReel3DValue As Integer
                        If innerNode.Attributes("Reel3DBrightness") IsNot Nothing AndAlso Integer.TryParse(innerNode.Attributes("Reel3DBrightness").InnerText, parsedReel3DValue) Then reel3DBrightness = parsedReel3DValue
                        If innerNode.Attributes("Reel3DTemperature") IsNot Nothing AndAlso Integer.TryParse(innerNode.Attributes("Reel3DTemperature").InnerText, parsedReel3DValue) Then reel3DTemperature = parsedReel3DValue
                        If innerNode.Attributes("Reel3DDepth") IsNot Nothing AndAlso Integer.TryParse(innerNode.Attributes("Reel3DDepth").InnerText, parsedReel3DValue) Then reel3DDepth = parsedReel3DValue
                        If innerNode.Attributes("Reel3DGlass") IsNot Nothing AndAlso Integer.TryParse(innerNode.Attributes("Reel3DGlass").InnerText, parsedReel3DValue) Then reel3DGlass = parsedReel3DValue
                        reel3DBrightness = Math.Max(0, Math.Min(200, reel3DBrightness))
                        reel3DTemperature = Math.Max(2000, Math.Min(6500, reel3DTemperature))
                        reel3DDepth = Math.Max(0, Math.Min(200, reel3DDepth))
                        reel3DGlass = Math.Max(0, Math.Min(200, reel3DGlass))
                        Dim loc As Point = New Point(CInt(innerNode.Attributes("LocX").InnerText) - 1, CInt(innerNode.Attributes("LocY").InnerText))
                        Dim size As Size = New Size(CInt(innerNode.Attributes("Width").InnerText), CInt(innerNode.Attributes("Height").InnerText))
                        Dim b2sstartdigit As Integer = 0
                        Dim b2sscoretype As Integer = 0
                        Dim b2splayerno As Integer = 0
                        If innerNode.Attributes("B2SStartDigit") IsNot Nothing Then b2sstartdigit = CInt(innerNode.Attributes("B2SStartDigit").InnerText)
                        If innerNode.Attributes("B2SScoreType") IsNot Nothing Then b2sscoretype = CInt(innerNode.Attributes("B2SScoreType").InnerText)
                        If innerNode.Attributes("B2SPlayerNo") IsNot Nothing Then b2splayerno = CInt(innerNode.Attributes("B2SPlayerNo").InnerText)
                        Dim dream7b2sstartdigit As Integer = b2sstartdigit
                        Dim startdigit As Integer = If(b2sstartdigit > 0, b2sstartdigit, renderedandreelindex)
                        Dim romid As Integer = 0
                        Dim romidtype As Integer = 0
                        Dim romidvalue As Integer = 0
                        If innerNode.Attributes("ReelIlluB2SID") IsNot Nothing Then
                            romid = CInt(innerNode.Attributes("ReelIlluB2SID").InnerText)
                            If innerNode.Attributes("ReelIlluB2SValue") IsNot Nothing Then
                                romidvalue = CInt(innerNode.Attributes("ReelIlluB2SValue").InnerText)
                            End If
                            romidtype = 1
                        End If
                        Dim soundName As String = String.Empty

                        ' set some tmp vars
                        Dim isOnBackglass As Boolean = (parent = "Backglass")
                        Dim isDream7LEDs As Boolean = (reeltype.StartsWith("dream7", StringComparison.CurrentCultureIgnoreCase))
                        Dim isRenderedLEDs As Boolean = (reeltype.StartsWith("rendered", StringComparison.CurrentCultureIgnoreCase))
                        Dim isReels As Boolean = Not (isDream7LEDs OrElse isRenderedLEDs)
                        Dim glowbulb As SizeF = Nothing
                        Dim glow As Integer = d7glow
                        Dim ledtype As String = String.Empty

                        ' set led type
                        If isDream7LEDs Then
                            ledtype = reeltype.Substring(9)
                        ElseIf isRenderedLEDs Then
                            ledtype = reeltype.Substring(11)
                        End If

                        ' maybe get default glow value
                        If B2SSettings.DefaultGlow = -1 Then
                            B2SSettings.DefaultGlow = d7glow
                        End If

                        ' set preferred LED settings
                        If isRenderedLEDs OrElse isDream7LEDs Then
                            If B2SSettings.IsGameNameFound AndAlso B2SSettings.UsedLEDType <> B2SSettings.eLEDTypes.Undefined Then
                                isDream7LEDs = (B2SSettings.UsedLEDType = B2SSettings.eLEDTypes.Dream7)
                                isRenderedLEDs = (B2SSettings.UsedLEDType = B2SSettings.eLEDTypes.Rendered)
                            ElseIf B2SSettings.UsedLEDType = B2SSettings.eLEDTypes.Undefined Then
                                B2SSettings.UsedLEDType = If(isDream7LEDs, B2SSettings.eLEDTypes.Dream7, B2SSettings.eLEDTypes.Rendered)
                            End If
                            If B2SSettings.IsGameNameFound AndAlso B2SSettings.GlowIndex > -1 Then
                                glow = B2SSettings.GlowIndex * 8
                            End If
                            If B2SSettings.IsGameNameFound AndAlso B2SSettings.IsGlowBulbOn Then
                                glowbulb = New SizeF(0.1, 0.4)
                            End If
                        End If

                        ' maybe create Dream 7 LED display controls
                        If isDream7LEDs OrElse isRenderedLEDs Then
                            ' add some self rendered Dream7 LED segments
                            Dim led As Dream7Display = New Dream7Display()
                            led.Name = "LEDDisplay" & id.ToString()
                            led.Location = loc
                            led.Size = size
                            Select Case ledtype
                                Case "7", "8"
                                    led.Type = SegmentNumberType.SevenSegment
                                Case "9", "10"
                                    led.Type = SegmentNumberType.TenSegment
                                Case "14"
                                    led.Type = SegmentNumberType.FourteenSegment
                                    'Case "16"
                                    'led.Type = SegmentNumberType.SixteenSegment
                            End Select
                            led.ScaleMode = ScaleMode.Stretch
                            led.Digits = digits
                            led.Spacing = spacing * 5
                            led.Hidden = hidden
                            ' color settings
                            'led.GlassColor = reellitcolor
                            'led.LightColor = Color.FromArgb(Math.Min(reellitcolor.R + 35, 255), Math.Min(reellitcolor.G + 35, 255), Math.Min(reellitcolor.B + 25, 255))
                            'led.GlassColorCenter = Color.FromArgb(Math.Min(reellitcolor.R + 50, 255), Math.Min(reellitcolor.G + 50, 255), Math.Min(reellitcolor.B + 50, 255))
                            led.LightColor = reellitcolor
                            led.GlassColor = Color.FromArgb(Math.Min(reellitcolor.R + 50, 255), Math.Min(reellitcolor.G + 50, 255), Math.Min(reellitcolor.B + 50, 255))
                            led.GlassColorCenter = Color.FromArgb(Math.Min(reellitcolor.R + 70, 255), Math.Min(reellitcolor.G + 70, 255), Math.Min(reellitcolor.B + 70, 255))
                            led.OffColor = reeldarkcolor
                            'led.BackColor = Color.FromArgb(5, 5, 5)
                            led.BackColor = Color.FromArgb(15, 15, 15)
                            led.GlassAlpha = 140
                            led.GlassAlphaCenter = 255
                            led.Thickness = d7thickness * 1.2
                            led.Shear = d7shear
                            led.Glow = glow
                            If glowbulb <> Nothing Then
                                led.BulbSize = glowbulb
                            End If
                            ' 'TAXI' patch to shear the third LED display
                            If id = 3 AndAlso B2SData.TableName = "Taxi" Then
                                led.Angle = 4
                                led.Shear = led.Shear / 2
                            End If

                            'led.OffsetWidth = -1 * CInt(d7shear * 30)

                            'led.BulbSize = New SizeF(0.1, 0.4)


                            'led.GlassColor = Color.FromArgb(254, 50, 25)
                            'led.LightColor = Color.FromArgb(254, 90, 50)
                            'led.GlassColorCenter = Color.FromArgb(254, 50, 25)
                            'led.GlassAlpha = 140
                            'led.GlassAlphaCenter = 255
                            'led.Glow = 9


                            'led.GlassColor = Color.FromArgb(255, 210, 50)
                            'led.LightColor = Color.FromArgb(255, 0, 0)
                            'led.GlassColorCenter = Color.FromArgb(255, 230, 65)
                            'led.OffColor = Color.FromArgb(20, 20, 20)
                            'led.GlassAlpha = 120
                            'led.GlassAlphaCenter = 255
                            'led.Glow = 15
                            'led.BulbSize = New SizeF(0.1, 0.4)
                            'led.Thickness = 30



                            ' add control to parent
                            If isOnBackglass Then
                                'led.Type = B2SBaseBox.eType.OnBackglass
                                Me.Controls.Add(led)
                                ' add to general collection
                                B2SData.LEDDisplays.Add(led.Name, led)
                            Else
                                If Not B2SSettings.HideB2SDMD Then
                                    CheckDMDForm()
                                    formDMD.Controls.Add(led)
                                    ' add to general collection
                                    B2SData.LEDDisplays.Add(led.Name, led)
                                End If
                            End If
                            led.BringToFront()
                            led.Visible = isDream7LEDs AndAlso Not hidden
                            ' add digit location info
                            For i = 0 To digits - 1
                                If isOnBackglass OrElse Not B2SSettings.HideB2SDMD Then
                                    Dim leddisplayid As Integer = If(dream7b2sstartdigit > 0, dream7b2sstartdigit, dream7index)
                                    B2SData.LEDDisplayDigits.Add(leddisplayid - 1, New B2SData.LEDDisplayDigitLocation(led, i, id))
                                    B2SData.ScoreMaxDigit = leddisplayid
                                End If
                                dream7index += 1
                                If dream7b2sstartdigit > 0 Then dream7b2sstartdigit += 1
                            Next
                            ' add LED area
                            B2SData.LEDAreas.Add("LEDArea" & id.ToString(), New B2SData.LEDAreaInfo(New Rectangle(loc, size), Not isOnBackglass))
                            ' add or update player info collection
                            If b2splayerno > 0 Then
                                B2SData.IsAPlayerAdded = True
                                If Not B2SData.Players.ContainsKey(b2splayerno) Then
                                    B2SData.Players.Add(b2splayerno)
                                End If
                                B2SData.Players(b2splayerno).Add(New B2SPlayer.ControlInfo(startdigit, digits, B2SPlayer.eControlType.Dream7LEDDisplay, led))
                            End If
                        End If

                        ' create reel or led boxes
                        Dim width As Integer = CInt((size.Width - (digits - 1) * spacing / 2) / digits)
                        For i As Integer = 1 To digits
                            Dim x As Integer = loc.X + ((i - 1) * (width + spacing / 2))
                            If isRenderedLEDs OrElse isDream7LEDs Then
                                ' add some self rendered LEDs
                                Dim led As B2SLEDBox = New B2SLEDBox()
                                led.ID = If(b2sstartdigit > 0, b2sstartdigit, renderedandreelindex)
                                led.DisplayID = id
                                led.Name = "LEDBox" & led.ID.ToString()
                                led.StartDigit = startdigit
                                led.Digits = digits
                                led.Hidden = hidden
                                led.Location = New Point(x, loc.Y)
                                led.Size = New Size(width, size.Height)
                                Select Case ledtype
                                    Case "7", "8"
                                        led.LEDType = B2SLED.eLEDType.LED8
                                    Case "9", "10"
                                        led.LEDType = B2SLED.eLEDType.LED10
                                    Case "14"
                                        led.LEDType = B2SLED.eLEDType.LED14
                                        'Case "16"
                                        '    led.LEDType = B2SLED.eLEDType.LED16
                                End Select
                                led.LitLEDSegmentColor = reellitcolor
                                led.DarkLEDSegmentColor = reeldarkcolor
                                led.BackColor = Color.FromArgb(5, 5, 5) ' Color.Transparent 
                                If isOnBackglass Then
                                    led.Type = B2SBaseBox.eType.OnBackglass
                                    Me.Controls.Add(led)
                                    ' add to general collection
                                    B2SData.LEDs.Add(led.Name, led)
                                Else
                                    If Not B2SSettings.HideB2SDMD Then
                                        CheckDMDForm()
                                        led.Type = B2SBaseBox.eType.OnDMD
                                        formDMD.Controls.Add(led)
                                        ' add to general collection
                                        B2SData.LEDs.Add(led.Name, led)
                                    End If
                                End If
                                B2SData.ScoreMaxDigit = led.ID
                                led.BringToFront()
                                led.Visible = isRenderedLEDs AndAlso Not hidden
                                ' add LED area
                                'B2SData.LEDAreas.Add("LEDArea" & id.ToString(), New B2SData.LEDAreaInfo(New Rectangle(loc, size), Not isOnBackglass))
                                ' add or update player info collection
                                ' no need to do this here since it's done at the dream7 LEDs
                                ' write reel info into registry
                                Using regkey As RegistryKey = Registry.CurrentUser.OpenSubKey("Software\B2S", True)
                                    regkey.SetValue("B2SScoreDigit" & led.ID.ToString(), If(isDream7LEDs, "2", "1") & "," & CInt(led.LEDType).ToString() & "," & led.StartDigit.ToString() & "," & led.Digits, RegistryValueKind.String)
                                    regkey.SetValue("B2SScoreDisplay" & id.ToString(), startdigit.ToString(), RegistryValueKind.String)
                                End Using
                            ElseIf isReels Then
                                ' look for matching reel sound
                                soundName = String.Empty
                                If innerNode.Attributes("Sound" & i.ToString()) IsNot Nothing Then
                                    soundName = innerNode.Attributes("Sound" & i.ToString()).InnerText
                                    If String.IsNullOrEmpty(soundName) Then
                                        soundName = "stille"
                                    End If
                                End If
                                ' add reel or LED pictures
                                Dim reel As B2SReelBox = New B2SReelBox()
                                reel.ID = If(b2sstartdigit > 0, b2sstartdigit, renderedandreelindex)
                                reel.DisplayID = id
                                reel.SetID = setid
                                reel.Name = "ReelBox" & reel.ID.ToString()
                                reel.StartDigit = startdigit
                                reel.Digits = digits
                                reel.RomID = romid
                                reel.RomIDType = romidtype
                                reel.RomIDValue = romidvalue
                                reel.BehindCanvas = behindCanvas
                                reel.Reel3DEnabled = reel3DEnabled
                                reel.Reel3DBrightness = reel3DBrightness
                                reel.Reel3DTemperature = reel3DTemperature
                                reel.Reel3DDepth = reel3DDepth
                                reel.Reel3DGlass = reel3DGlass
                                reel.Reel3DDisplayWidth = size.Width
                                reel.Reel3DDisplayOffsetX = x - loc.X
                                reel.Location = New Point(x, loc.Y)
                                reel.Size = New Size(width, size.Height)
                                reel.ReelType = reeltype.Substring(0, reeltype.Length - 2)
                                reel.ScoreType = b2sscoretype
                                reel.SoundName = soundName
                                If rollinginterval >= 10 Then reel.RollingInterval = rollinginterval

                                If isOnBackglass Then
                                    reel.Type = B2SBaseBox.eType.OnBackglass
                                    Me.Controls.Add(reel)
                                    ' add to general collection
                                    B2SData.Reels.Add(reel)
                                Else
                                    If Not B2SSettings.HideB2SDMD Then
                                        CheckDMDForm()
                                        reel.Type = B2SBaseBox.eType.OnDMD
                                        formDMD.Controls.Add(reel)
                                        ' add to general collection
                                        B2SData.Reels.Add(reel)
                                    End If
                                End If
                                B2SData.ScoreMaxDigit = reel.ID
                                reel.BringToFront()
                                reel.Visible = Not hidden
                                ' add or update reel display
                                If Not B2SData.ReelDisplays.ContainsKey(id) Then
                                    Dim reeldisplay As B2SReelDisplay = New B2SReelDisplay
                                    reeldisplay.StartDigit = startdigit
                                    reeldisplay.Digits = digits
                                    reeldisplay.Reels.Add(reel.ID, reel)
                                    B2SData.ReelDisplays.Add(id, reeldisplay)
                                Else
                                    B2SData.ReelDisplays(id).Reels.Add(reel.ID, reel)
                                End If
                                ' add or update player info collection
                                If b2splayerno > 0 Then
                                    B2SData.IsAPlayerAdded = True
                                    If Not B2SData.Players.ContainsKey(b2splayerno) Then
                                        B2SData.Players.Add(b2splayerno)
                                    End If
                                    B2SData.Players(b2splayerno).Add(New B2SPlayer.ControlInfo(startdigit, digits, B2SPlayer.eControlType.ReelDisplay, B2SData.ReelDisplays(id)))
                                End If
                                ' write reel info into registry
                                Using regkey As RegistryKey = Registry.CurrentUser.OpenSubKey("Software\B2S", True)
                                    regkey.SetValue("B2SScoreDigit" & reel.ID.ToString(), "3,0," & reel.StartDigit.ToString() & "," & reel.Digits, RegistryValueKind.String)
                                    regkey.SetValue("B2SScoreDisplay" & id.ToString(), startdigit.ToString(), RegistryValueKind.String)
                                End Using
                            End If

                            renderedandreelindex += 1
                            If b2sstartdigit > 0 Then b2sstartdigit += 1

                        Next

                        dream7index = renderedandreelindex

                    Next
#If B2S = "EXE" Then
                    ' write player info into registry
                    For Each controls As KeyValuePair(Of Integer, B2SPlayer.ControlCollection) In B2SData.Players
                        Dim player As String = String.Empty
                        For Each controlinfo As B2SPlayer.ControlInfo In controls.Value
                            With controlinfo
                                Dim type As String = "0"
                                If .LEDBox IsNot Nothing Then
                                    type = CInt(.LEDBox.LEDType).ToString()
                                ElseIf .LEDDisplay IsNot Nothing Then
                                    type = If(.LEDDisplay.Type = SegmentNumberType.TenSegment, "2", If(.LEDDisplay.Type = SegmentNumberType.FourteenSegment, "3", "1"))
                                End If
                                player &= ";" & If(.Type = B2SPlayer.eControlType.ReelDisplay OrElse .Type = B2SPlayer.eControlType.ReelBox, "3", "1") & "," &
                                          type & "," &
                                          controlinfo.StartDigit & "," &
                                          controlinfo.Digits
                            End With
                        Next
                        If Not String.IsNullOrEmpty(player) Then player = player.Substring(1)
                        Using regkey As RegistryKey = Registry.CurrentUser.OpenSubKey("Software\B2S", True)
                            regkey.SetValue("B2SScorePlayer" & controls.Key.ToString(), player, RegistryValueKind.String)
                        End Using
                    Next
#End If
                End If

                ' maybe get all reel images
                If topnode.SelectSingleNode("Reels") IsNot Nothing Then

                    If topnode.SelectNodes("Reels/Image") IsNot Nothing AndAlso topnode.SelectNodes("Reels/Image").Count > 0 Then

                        For Each innerNode As Xml.XmlElement In topnode.SelectNodes("Reels/Image")
                            Dim name As String = innerNode.Attributes("Name").InnerText
                            Dim image As Image = Base64ToImage(innerNode.Attributes("Image").InnerText)
                            If Not B2SData.ReelImages.ContainsKey(name) Then
                                B2SData.ReelImages.Add(name, image)
                            End If
                        Next

                    ElseIf topnode.SelectNodes("Reels/Images") IsNot Nothing AndAlso topnode.SelectNodes("Reels/Images/Image") IsNot Nothing AndAlso topnode.SelectNodes("Reels/Images/Image").Count > 0 Then

                        For Each innerNode As Xml.XmlElement In topnode.SelectNodes("Reels/Images/Image")
                            Dim name As String = innerNode.Attributes("Name").InnerText
                            Dim image As Image = Base64ToImage(innerNode.Attributes("Image").InnerText)
                            If Not B2SData.ReelImages.ContainsKey(name) Then
                                B2SData.ReelImages.Add(name, image)
                            End If
                            ' maybe get the intermediate reel images
                            If innerNode.Attributes("CountOfIntermediates") IsNot Nothing Then
                                Dim countOfIntermediates As Integer = CInt(innerNode.Attributes("CountOfIntermediates").InnerText)
                                For i As Integer = 1 To countOfIntermediates
                                    Dim intname As String = name & "_" & i.ToString()
                                    Dim intimage As Image = Base64ToImage(innerNode.Attributes("IntermediateImage" & i.ToString()).InnerText)
                                    If Not B2SData.ReelIntermediateImages.ContainsKey(intname) Then
                                        B2SData.ReelIntermediateImages.Add(intname, intimage)
                                    End If
                                Next
                            End If
                        Next

                        If topnode.SelectNodes("Reels/IlluminatedImages") IsNot Nothing Then
                            If topnode.SelectNodes("Reels/IlluminatedImages/IlluminatedImage") IsNot Nothing AndAlso topnode.SelectNodes("Reels/IlluminatedImages/IlluminatedImage").Count > 0 Then
                                For Each innerNode As Xml.XmlElement In topnode.SelectNodes("Reels/IlluminatedImages/IlluminatedImage")
                                    Dim name As String = innerNode.Attributes("Name").InnerText
                                    Dim image As Image = Base64ToImage(innerNode.Attributes("Image").InnerText)
                                    If Not B2SData.ReelIlluImages.ContainsKey(name) Then
                                        B2SData.ReelIlluImages.Add(name, image)
                                    End If
                                    ' maybe get the intermediate reel images
                                    If innerNode.Attributes("CountOfIntermediates") IsNot Nothing Then
                                        Dim countOfIntermediates As Integer = CInt(innerNode.Attributes("CountOfIntermediates").InnerText)
                                        For i As Integer = 1 To countOfIntermediates
                                            Dim intname As String = name & "_" & i.ToString()
                                            Dim intimage As Image = Base64ToImage(innerNode.Attributes("IntermediateImage" & i.ToString()).InnerText)
                                            If Not B2SData.ReelIntermediateIlluImages.ContainsKey(intname) Then
                                                B2SData.ReelIntermediateIlluImages.Add(intname, intimage)
                                            End If
                                        Next
                                    End If
                                Next
                            ElseIf topnode.SelectNodes("Reels/IlluminatedImages/Set") IsNot Nothing AndAlso topnode.SelectNodes("Reels/IlluminatedImages/Set/IlluminatedImage") IsNot Nothing AndAlso topnode.SelectNodes("Reels/IlluminatedImages/Set/IlluminatedImage").Count > 0 Then
                                For Each setnode As Xml.XmlElement In topnode.SelectNodes("Reels/IlluminatedImages/Set")
                                    Dim setid As Integer = CInt(setnode.Attributes("ID").InnerText)
                                    For Each innerNode As Xml.XmlElement In setnode.SelectNodes("IlluminatedImage")
                                        Dim name As String = innerNode.Attributes("Name").InnerText & "_" & setid.ToString()
                                        Dim image As Image = Base64ToImage(innerNode.Attributes("Image").InnerText)
                                        If Not B2SData.ReelIlluImages.ContainsKey(name) Then
                                            B2SData.ReelIlluImages.Add(name, image)
                                        End If
                                        ' maybe get the intermediate reel images
                                        If innerNode.Attributes("CountOfIntermediates") IsNot Nothing Then
                                            Dim countOfIntermediates As Integer = CInt(innerNode.Attributes("CountOfIntermediates").InnerText)
                                            For i As Integer = 1 To countOfIntermediates
                                                Dim intname As String = name & "_" & i.ToString()
                                                Dim intimage As Image = Base64ToImage(innerNode.Attributes("IntermediateImage" & i.ToString()).InnerText)
                                                If Not B2SData.ReelIntermediateIlluImages.ContainsKey(intname) Then
                                                    B2SData.ReelIntermediateIlluImages.Add(intname, intimage)
                                                End If
                                            Next
                                        End If
                                    Next
                                Next
                            End If
                        End If

                    End If

                End If

                ' maybe get all sounds
                If topnode.SelectSingleNode("Sounds") IsNot Nothing Then

                    If topnode.SelectNodes("Sounds/Sound") IsNot Nothing AndAlso topnode.SelectNodes("Sounds/Sound").Count > 0 Then
                        For Each innerNode As Xml.XmlElement In topnode.SelectNodes("Sounds/Sound")
                            Dim name As String = innerNode.Attributes("Name").InnerText
                            Dim stream As IO.MemoryStream = Base64ToWav(innerNode.Attributes("Stream").InnerText)
                            If Not B2SData.Sounds.ContainsKey(name) Then
                                B2SData.Sounds.Add(name, stream.ToArray)
                            End If
                        Next
                    End If
                    For Each reel As KeyValuePair(Of String, B2SReelBox) In B2SData.Reels
                        If B2SData.Sounds.ContainsKey(reel.Value.SoundName) Then
                            reel.Value.Sound = B2SData.Sounds(reel.Value.SoundName)
                        End If
                    Next

                End If

                ' get background and maybe DMD image(s)
                If topnode.SelectSingleNode("Images") IsNot Nothing Then

                    ApplyB2SProTransparentCanvasBacking(DirectCast(topnode.SelectSingleNode("Images"), Xml.XmlElement))

                    ' backglass image
                    Dim offimagenode As Xml.XmlElement = topnode.SelectSingleNode("Images/BackglassOffImage")
                    If offimagenode IsNot Nothing Then
                        B2SData.OnAndOffImage = True
                        ' get on and off image
                        Dim offimage As Image = Base64ToImage(offimagenode.Attributes("Value").InnerText)
                        DarkImage4Authentic = offimage
                        If B2SData.DualBackglass Then
                            DarkImage4Fantasy = offimage
                        End If
                        Dim onimagenode As Xml.XmlElement = topnode.SelectSingleNode("Images/BackglassOnImage")
                        If onimagenode IsNot Nothing Then
                            Dim onimage As Image = Base64ToImage(onimagenode.Attributes("Value").InnerText)
                            TopLightImage4Authentic = onimage
                            If B2SData.DualBackglass Then
                                TopLightImage4Fantasy = onimage
                            End If
                            If onimagenode.Attributes("RomID") IsNot Nothing Then
                                TopRomID4Authentic = CInt(onimagenode.Attributes("RomID").InnerText)
                                TopRomIDType4Authentic = CInt(onimagenode.Attributes("RomIDType").InnerText)
                                TopRomInverted4Authentic = False
                                Select Case TopRomIDType4Authentic
                                    Case B2SBaseBox.eRomIDType.Lamp
                                        B2SData.UsedRomLampIDs4Authentic.Add(TopRomID4Authentic, Nothing)
                                    Case B2SBaseBox.eRomIDType.Solenoid
                                        B2SData.UsedRomSolenoidIDs4Authentic.Add(TopRomID4Authentic, Nothing)
                                    Case B2SBaseBox.eRomIDType.GIString
                                        B2SData.UsedRomGIStringIDs4Authentic.Add(TopRomID4Authentic, Nothing)
                                End Select
                                If B2SData.DualBackglass Then
                                    TopRomID4Fantasy = CInt(onimagenode.Attributes("RomID").InnerText)
                                    TopRomIDType4Fantasy = CInt(onimagenode.Attributes("RomIDType").InnerText)
                                    TopRomInverted4Fantasy = False
                                    Select Case TopRomIDType4Authentic
                                        Case B2SBaseBox.eRomIDType.Lamp
                                            B2SData.UsedRomLampIDs4Fantasy.Add(TopRomID4Fantasy, Nothing)
                                        Case B2SBaseBox.eRomIDType.Solenoid
                                            B2SData.UsedRomSolenoidIDs4Fantasy.Add(TopRomID4Fantasy, Nothing)
                                        Case B2SBaseBox.eRomIDType.GIString
                                            B2SData.UsedRomGIStringIDs4Fantasy.Add(TopRomID4Fantasy, Nothing)
                                    End Select
                                End If
                            End If
                        End If
                    Else
                        Dim imagenode As Xml.XmlElement = topnode.SelectSingleNode("Images/BackglassImage")
                        If imagenode IsNot Nothing Then
                            Dim image As Image = Base64ToImage(imagenode.Attributes("Value").InnerText)
                            DarkImage4Authentic = image
                            If B2SData.DualBackglass Then
                                DarkImage4Fantasy = image
                            End If
                        End If
                    End If
                    ' starting image is the dark image
                    Me.BackgroundImage = DarkImage

                    ' DMD image
                    Dim dmdimagenode As Xml.XmlElement = topnode.SelectSingleNode("Images/DMDImage")
                    If dmdimagenode IsNot Nothing Then
                        Dim image As Image = Base64ToImage(dmdimagenode.Attributes("Value").InnerText)
                        If image IsNot Nothing Then
                            If Not B2SSettings.HideB2SDMD Then
                                CheckDMDForm()
                                formDMD.BackgroundImage = image
                            End If
                        End If
                    End If

#If mergeBulbs Then
                    ' look for the largest bulb amount
                        Dim topSize4Authentic As Integer = 0
                    Dim topkey4Authentic As String = String.Empty
                        Dim secondSize4Authentic As Integer = 0
                    Dim secondkey4Authentic As String = String.Empty
                    For Each romsize As KeyValuePair(Of String, Integer) In roms4Authentic
                            If romsize.Value > secondSize4Authentic Then
                                secondSize4Authentic = romsize.Value
                                secondkey4Authentic = romsize.Key.Split("|")(0)
                        End If
                            If romsize.Value > topSize4Authentic Then
                                secondSize4Authentic = topSize4Authentic
                            secondkey4Authentic = topkey4Authentic
                                topSize4Authentic = romsize.Value
                                topkey4Authentic = romsize.Key.Split("|")(0)
                        End If
                    Next
                    Dim top4Fantasy As Integer = 0
                    Dim topkey4Fantasy As String = String.Empty
                        Dim secondSize4Fantasy As Integer = 0
                    Dim secondkey4Fantasy As String = String.Empty
                    If B2SData.DualBackglass Then
                        For Each romsize As KeyValuePair(Of String, Integer) In roms4Fantasy
                                If romsize.Value > secondSize4Fantasy Then
                                    secondSize4Fantasy = romsize.Value
                                    secondkey4Fantasy = romsize.Key.Split("|")(0)
                            End If
                            If romsize.Value > top4Fantasy Then
                                    secondSize4Fantasy = top4Fantasy
                                secondkey4Fantasy = topkey4Fantasy
                                top4Fantasy = romsize.Value
                                topkey4Fantasy = romsize.Key.Split("|")(0)
                            End If
                        Next
                    End If

                    ' maybe draw some light images for pretty fast image changing
                        If topSize4Authentic >= minSize4Image Then
                        ' create some light images
                        If TopLightImage4Authentic Is Nothing Then
                            TopLightImage4Authentic = CreateLightImage(DarkImage4Authentic, B2SData.eDualMode.Authentic, topkey4Authentic, , TopRomID4Authentic, TopRomIDType4Authentic, TopRomInverted4Authentic)
                                If secondSize4Authentic > minSize4Image Then
                                SecondLightImage4Authentic = CreateLightImage(DarkImage4Authentic, B2SData.eDualMode.Authentic, secondkey4Authentic, , SecondRomID4Authentic, SecondRomIDType4Authentic, SecondRomInverted4Authentic)
                                TopAndSecondLightImage4Authentic = CreateLightImage(DarkImage4Authentic, B2SData.eDualMode.Authentic, topkey4Authentic, secondkey4Authentic)
                            End If
                        Else
                            SecondLightImage4Authentic = CreateLightImage(DarkImage4Authentic, B2SData.eDualMode.Authentic, topkey4Authentic, , SecondRomID4Authentic, SecondRomIDType4Authentic, SecondRomInverted4Authentic)
                            TopAndSecondLightImage4Authentic = CreateLightImage(TopLightImage4Authentic, B2SData.eDualMode.Authentic, topkey4Authentic)
                        End If
                    End If
                    If B2SData.DualBackglass AndAlso top4Fantasy >= minSize4Image Then
                        ' create some light images
                        If TopLightImage4Fantasy Is Nothing Then
                            TopLightImage4Fantasy = CreateLightImage(DarkImage4Fantasy, B2SData.eDualMode.Fantasy, topkey4Fantasy, , TopRomID4Fantasy, TopRomIDType4Fantasy, TopRomInverted4Fantasy)
                                If secondSize4Fantasy > minSize4Image Then
                                SecondLightImage4Fantasy = CreateLightImage(DarkImage4Fantasy, B2SData.eDualMode.Fantasy, secondkey4Fantasy, , SecondRomID4Fantasy, SecondRomIDType4Fantasy, SecondRomInverted4Fantasy)
                                TopAndSecondLightImage4Fantasy = CreateLightImage(DarkImage4Fantasy, B2SData.eDualMode.Fantasy, topkey4Fantasy, secondkey4Fantasy)
                            End If
                        Else
                            SecondLightImage4Fantasy = CreateLightImage(DarkImage4Fantasy, B2SData.eDualMode.Fantasy, topkey4Fantasy, , SecondRomID4Fantasy, SecondRomIDType4Fantasy, SecondRomInverted4Fantasy)
                            TopAndSecondLightImage4Fantasy = CreateLightImage(TopLightImage4Fantasy, B2SData.eDualMode.Fantasy, topkey4Fantasy)
                        End If
                    End If
                    B2SData.UsedTopRomIDType4Authentic = TopRomIDType4Authentic
                    B2SData.UsedSecondRomIDType4Authentic = SecondRomIDType4Authentic
                    If B2SData.DualBackglass Then
                        B2SData.UsedTopRomIDType4Fantasy = TopRomIDType4Fantasy
                        B2SData.UsedSecondRomIDType4Fantasy = SecondRomIDType4Fantasy
                    End If

                    ' remove top and second rom bulbs
                    CheckBulbs(TopRomID4Authentic, TopRomIDType4Authentic, TopRomInverted4Authentic, B2SData.eDualMode.Authentic)
                    CheckBulbs(SecondRomID4Authentic, SecondRomIDType4Authentic, SecondRomInverted4Authentic, B2SData.eDualMode.Authentic)
                    If B2SData.DualBackglass Then
                        CheckBulbs(TopRomID4Fantasy, TopRomIDType4Fantasy, TopRomInverted4Fantasy, B2SData.eDualMode.Fantasy)
                        CheckBulbs(SecondRomID4Fantasy, SecondRomIDType4Fantasy, SecondRomInverted4Fantasy, B2SData.eDualMode.Fantasy)
                    End If
#End If
                End If

                ' get all animation info
                Dim animationpulseswitch As Boolean = False
                If topnode.SelectSingleNode("Animations") IsNot Nothing AndAlso topnode.SelectNodes("Animations/Animation") IsNot Nothing Then
                    For Each innerNode As Xml.XmlElement In topnode.SelectNodes("Animations/Animation")
                        Dim name As String = innerNode.Attributes("Name").InnerText
                        Dim dualmode As B2SData.eDualMode = B2SData.eDualMode.Both
                        If innerNode.Attributes("DualMode") IsNot Nothing Then
                            dualmode = CInt(innerNode.Attributes("DualMode").InnerText)
                        End If
                        Dim interval As Integer = CInt(innerNode.Attributes("Interval").InnerText)
                        Dim loops As Integer = CInt(innerNode.Attributes("Loops").InnerText)
                        Dim idJoins As String = innerNode.Attributes("IDJoin").InnerText
                        Dim startAnimationAtBackglassStartup As Boolean = (innerNode.Attributes("StartAnimationAtBackglassStartup").InnerText = "1")
                        Dim lightsStateAtAnimationStart As B2SAnimation.eLightsStateAtAnimationStart = B2SAnimation.eLightsStateAtAnimationStart.NoChange
                        Dim lightsStateAtAnimationEnd As B2SAnimation.eLightsStateAtAnimationEnd = B2SAnimation.eLightsStateAtAnimationEnd.InvolvedLightsOff
                        Dim animationstopbehaviour As B2SAnimation.eAnimationStopBehaviour = B2S.B2SAnimation.eAnimationStopBehaviour.StopImmediatelly
                        Dim lockInvolvedLamps As Boolean = False
                        Dim hidescoredisplays As Boolean = False
                        Dim bringtofront As Boolean = False
                        Dim randomstart As Boolean = False
                        Dim randomquality As Integer = 1
                        If innerNode.Attributes("LightsStateAtAnimationStart") IsNot Nothing Then
                            lightsStateAtAnimationStart = CInt(innerNode.Attributes("LightsStateAtAnimationStart").InnerText)
                        ElseIf innerNode.Attributes("AllLightsOffAtAnimationStart") IsNot Nothing Then
                            lightsStateAtAnimationStart = If((innerNode.Attributes("AllLightsOffAtAnimationStart").InnerText = "1"), B2SAnimation.eLightsStateAtAnimationStart.LightsOff, B2SAnimation.eLightsStateAtAnimationStart.NoChange)
                        End If
                        If innerNode.Attributes("LightsStateAtAnimationEnd") IsNot Nothing Then
                            lightsStateAtAnimationEnd = CInt(innerNode.Attributes("LightsStateAtAnimationEnd").InnerText)
                        ElseIf innerNode.Attributes("ResetLightsAtAnimationEnd") IsNot Nothing Then
                            lightsStateAtAnimationEnd = If((innerNode.Attributes("ResetLightsAtAnimationEnd").InnerText = "1"), B2SAnimation.eLightsStateAtAnimationEnd.LightsReseted, B2SAnimation.eLightsStateAtAnimationEnd.Undefined)
                        End If
                        If innerNode.Attributes("AnimationStopBehaviour") IsNot Nothing Then
                            animationstopbehaviour = CInt(innerNode.Attributes("AnimationStopBehaviour").InnerText)
                        ElseIf innerNode.Attributes("RunAnimationTilEnd") IsNot Nothing Then
                            animationstopbehaviour = If((innerNode.Attributes("RunAnimationTilEnd").InnerText = "1"), B2SAnimation.eAnimationStopBehaviour.RunAnimationTillEnd, B2SAnimation.eAnimationStopBehaviour.StopImmediatelly)
                        End If
                        lockInvolvedLamps = (innerNode.Attributes("LockInvolvedLamps").InnerText = "1")
                        If innerNode.Attributes("HideScoreDisplays") IsNot Nothing Then
                            hidescoredisplays = (innerNode.Attributes("HideScoreDisplays").InnerText = "1")
                        End If
                        If innerNode.Attributes("BringToFront") IsNot Nothing Then
                            bringtofront = (innerNode.Attributes("BringToFront").InnerText = "1")
                        End If
                        If innerNode.Attributes("RandomStart") IsNot Nothing Then
                            randomstart = (innerNode.Attributes("RandomStart").InnerText = "1")
                        End If
                        If randomstart AndAlso innerNode.Attributes("RandomQuality") IsNot Nothing Then
                            randomquality = CInt(innerNode.Attributes("RandomQuality").InnerText)
                        End If
                        If lightsStateAtAnimationStart = B2SAnimation.eLightsStateAtAnimationStart.Undefined Then lightsStateAtAnimationStart = B2SAnimation.eLightsStateAtAnimationStart.NoChange
                        If lightsStateAtAnimationEnd = B2SAnimation.eLightsStateAtAnimationEnd.Undefined Then lightsStateAtAnimationEnd = B2SAnimation.eLightsStateAtAnimationEnd.InvolvedLightsOff
                        If animationstopbehaviour = B2SAnimation.eAnimationStopBehaviour.Undefined Then animationstopbehaviour = B2SAnimation.eAnimationStopBehaviour.StopImmediatelly
                        Dim entries As B2SAnimation.PictureBoxAnimationEntry() = Nothing
                        For Each stepnode As Xml.XmlElement In innerNode.SelectNodes("AnimationStep")
                            Dim [step] As Integer = CInt(stepnode.Attributes("Step").InnerText)
                            Dim [on] As String = stepnode.Attributes("On").InnerText
                            Dim waitLoopsAfterOn As Integer = CInt(stepnode.Attributes("WaitLoopsAfterOn").InnerText)
                            Dim off As String = stepnode.Attributes("Off").InnerText
                            Dim waitLoopsAfterOff As Integer = CInt(stepnode.Attributes("WaitLoopsAfterOff").InnerText)
                            Dim pulseswitch As Integer = 0
                            If stepnode.Attributes("PulseSwitch") IsNot Nothing Then
                                pulseswitch = CInt(stepnode.Attributes("PulseSwitch").InnerText)
                                If pulseswitch > 0 Then animationpulseswitch = True
                            End If
                            Dim entry As B2SAnimation.PictureBoxAnimationEntry = New B2SAnimation.PictureBoxAnimationEntry([on], waitLoopsAfterOn, off, waitLoopsAfterOff, , , , , pulseswitch)
                            If entries Is Nothing Then
                                ReDim entries(0)
                                entries(0) = entry
                            Else
                                ReDim Preserve entries(entries.Length)
                                entries(entries.Length - 1) = entry
                            End If
                        Next
                        ' maybe add animation
                        If interval > 0 AndAlso entries.Length > 0 Then
                            B2SAnimation.AddAnimation(name, Me, formDMD, dualmode, interval, loops, startAnimationAtBackglassStartup, lightsStateAtAnimationStart, lightsStateAtAnimationEnd,
                                                      animationstopbehaviour, lockInvolvedLamps, hidescoredisplays, bringtofront, randomstart, randomquality,
                                                      entries)
                            ' maybe set slowdown
                            If B2SSettings.AnimationSlowDowns.ContainsKey(name) Then
                                B2SAnimation.AnimationSlowDown(name) = B2SSettings.AnimationSlowDowns(name)
                            End If
                            ' add join to ID
                            If Not String.IsNullOrEmpty(idJoins) Then
                                For Each idJoin As String In idJoins.Split(",")
                                    If Not String.IsNullOrEmpty(idJoin) Then
                                        Dim id0 As Integer = 0
                                        Dim id1 As Integer = 0
                                        Dim id2 As Integer = 0
                                        Dim id3 As Integer = 0
                                        If idJoin.Length >= 1 AndAlso IsNumeric(idJoin) Then id0 = CInt(idJoin)
                                        If idJoin.Length >= 2 AndAlso IsNumeric(idJoin.Substring(1)) Then id1 = CInt(idJoin.Substring(1))
                                        If idJoin.Length >= 3 AndAlso IsNumeric(idJoin.Substring(2)) Then id2 = CInt(idJoin.Substring(2))
                                        If idJoin.Length >= 4 AndAlso IsNumeric(idJoin.Substring(3)) Then id3 = CInt(idJoin.Substring(3))
                                        Select Case idJoin.Substring(0, 1).ToUpper
                                            Case "L"
                                                Dim animations As B2SData.AnimationCollection = If(randomstart, B2SData.UsedRandomAnimationLampIDs, B2SData.UsedAnimationLampIDs)
                                                If id1 > 0 Then For i As Integer = 1 To randomquality : animations.Add(id1, New B2SData.AnimationInfo(name, False)) : Next
                                            Case "S"
                                                Dim animations As B2SData.AnimationCollection = If(randomstart, B2SData.UsedRandomAnimationSolenoidIDs, B2SData.UsedAnimationSolenoidIDs)
                                                If id1 > 0 Then For i As Integer = 1 To randomquality : animations.Add(id1, New B2SData.AnimationInfo(name, False)) : Next
                                            Case "G"
                                                Dim animations As B2SData.AnimationCollection = If(randomstart, B2SData.UsedRandomAnimationGIStringIDs, B2SData.UsedAnimationGIStringIDs)
                                                If idJoin.Substring(1, 1).ToUpper = "I" Then
                                                    If id2 > 0 Then animations.Add(id2, New B2SData.AnimationInfo(name, False))
                                                Else
                                                    If id1 > 0 Then animations.Add(id1, New B2SData.AnimationInfo(name, False))
                                                End If
                                            Case "I"
                                                If idJoin.Length >= 2 Then
                                                    Select Case idJoin.Substring(0, 2).ToUpper
                                                        Case "IL"
                                                            Dim animations As B2SData.AnimationCollection = If(randomstart, B2SData.UsedRandomAnimationLampIDs, B2SData.UsedAnimationLampIDs)
                                                            If id2 > 0 Then animations.Add(id2, New B2SData.AnimationInfo(name, True))
                                                        Case "IS"
                                                            Dim animations As B2SData.AnimationCollection = If(randomstart, B2SData.UsedRandomAnimationSolenoidIDs, B2SData.UsedAnimationSolenoidIDs)
                                                            If id2 > 0 Then animations.Add(id2, New B2SData.AnimationInfo(name, True))
                                                        Case "IG"
                                                            Dim animations As B2SData.AnimationCollection = If(randomstart, B2SData.UsedRandomAnimationGIStringIDs, B2SData.UsedAnimationGIStringIDs)
                                                            If idJoin.Substring(2, 1).ToUpper = "I" Then
                                                                If id3 > 0 Then animations.Add(id3, New B2SData.AnimationInfo(name, True))
                                                            Else
                                                                If id2 > 0 Then animations.Add(id2, New B2SData.AnimationInfo(name, True))
                                                            End If
                                                        Case Else
                                                            Dim animations As B2SData.AnimationCollection = If(randomstart, B2SData.UsedRandomAnimationLampIDs, B2SData.UsedAnimationLampIDs)
                                                            If id1 > 0 Then animations.Add(id1, New B2SData.AnimationInfo(name, True))
                                                    End Select
                                                End If
                                            Case Else
                                                Dim animations As B2SData.AnimationCollection = If(randomstart, B2SData.UsedRandomAnimationLampIDs, B2SData.UsedAnimationLampIDs)
                                                If id0 > 0 Then animations.Add(id0, New B2SData.AnimationInfo(name, False))
                                        End Select
                                    End If
                                Next
                            End If
                        End If
                    Next
                End If

#If B2S = "EXE" Then
                Using regkey As RegistryKey = Registry.CurrentUser.OpenSubKey("Software\B2S", True)
                    regkey.SetValue("B2SSetSwitch", If(animationpulseswitch, 1, 0), RegistryValueKind.DWord)
                End Using
#End If
            End If

            ' set info flags to dirty to load them
#If B2S = "DLL" Then
            B2SData.IsInfoDirty = True
#End If


        End If

    End Sub

    Private Sub InitB2SScreen()

        ' initialize screen settings
        If formDMD IsNot Nothing Then
            If B2SData.DMDType = B2SData.eDMDType.B2SAlwaysOnSecondMonitor Then
                B2SScreen.Start(Me, formDMD, B2SData.DMDDefaultLocation, B2SScreen.eDMDViewMode.ShowDMDOnlyAtDefaultLocation, B2SData.GrillHeight, B2SData.SmallGrillHeight)
            ElseIf B2SData.DMDType = B2SData.eDMDType.B2SAlwaysOnThirdMonitor Then
                B2SScreen.Start(Me, formDMD, B2SData.DMDDefaultLocation, B2SScreen.eDMDViewMode.DoNotShowDMDAtDefaultLocation, B2SData.GrillHeight, B2SData.SmallGrillHeight)
            ElseIf B2SData.DMDType = B2SData.eDMDType.B2SOnSecondOrThirdMonitor Then
                B2SScreen.Start(Me, formDMD, B2SData.DMDDefaultLocation, B2SScreen.eDMDViewMode.ShowDMD, B2SData.GrillHeight, B2SData.SmallGrillHeight)
            Else
                B2SScreen.Start(Me, B2SData.GrillHeight, B2SData.SmallGrillHeight)
            End If
        Else
            B2SScreen.Start(Me, B2SData.GrillHeight, B2SData.SmallGrillHeight)
        End If

    End Sub

    Private Sub ResizeSomeImages()

        ' resize images
        Dim xResizeFactor As Single = 1
        Dim yResizeFactor As Single = 1
        If DarkImage4Authentic IsNot Nothing Then
            Dim width As Integer = DarkImage4Authentic.Width
            Dim height As Integer = DarkImage4Authentic.Height
            Dim image As Image = DarkImage4Authentic.Resized(B2SScreen.BackglassSize)
            DarkImage4Authentic.Dispose()
            DarkImage4Authentic = Nothing
            DarkImage4Authentic = image
            xResizeFactor = width / DarkImage4Authentic.Width
            yResizeFactor = height / DarkImage4Authentic.Height
        End If
        If DarkImage4Fantasy IsNot Nothing Then
            DarkImage4Fantasy = DarkImage4Authentic
        End If
        If TopLightImage4Authentic IsNot Nothing Then
            Dim image As Image = TopLightImage4Authentic.Resized(B2SScreen.BackglassSize)
            TopLightImage4Authentic.Dispose()
            TopLightImage4Authentic = Nothing
            TopLightImage4Authentic = image
        End If
        If TopLightImage4Fantasy IsNot Nothing Then
            Dim image As Image = TopLightImage4Fantasy.Resized(B2SScreen.BackglassSize)
            TopLightImage4Fantasy.Dispose()
            TopLightImage4Fantasy = Nothing
            TopLightImage4Fantasy = image
        End If
        If SecondLightImage4Authentic IsNot Nothing Then
            Dim image As Image = SecondLightImage4Authentic.Resized(B2SScreen.BackglassSize)
            SecondLightImage4Authentic.Dispose()
            SecondLightImage4Authentic = Nothing
            SecondLightImage4Authentic = image
        End If
        If SecondLightImage4Fantasy IsNot Nothing Then
            Dim image As Image = SecondLightImage4Fantasy.Resized(B2SScreen.BackglassSize)
            SecondLightImage4Fantasy.Dispose()
            SecondLightImage4Fantasy = Nothing
            SecondLightImage4Fantasy = image
        End If
        If TopAndSecondLightImage4Authentic IsNot Nothing Then
            Dim image As Image = TopAndSecondLightImage4Authentic.Resized(B2SScreen.BackglassSize)
            TopAndSecondLightImage4Authentic.Dispose()
            TopAndSecondLightImage4Authentic = Nothing
            TopAndSecondLightImage4Authentic = image
        End If
        If TopAndSecondLightImage4Fantasy IsNot Nothing Then
            Dim image As Image = TopAndSecondLightImage4Fantasy.Resized(B2SScreen.BackglassSize)
            TopAndSecondLightImage4Fantasy.Dispose()
            TopAndSecondLightImage4Fantasy = Nothing
            TopAndSecondLightImage4Fantasy = image
        End If
        Me.BackgroundImage = DarkImage

        ' now resize the detail images
        If xResizeFactor <> 1 OrElse yResizeFactor <> 1 Then
            For Each illu As KeyValuePair(Of String, B2SPictureBox) In B2SData.Illuminations
                If illu.Value.PictureBoxType = B2SPictureBox.ePictureBoxType.StandardImage AndAlso Not illu.Value.NativeRotation Then
                    If illu.Value.BackgroundImage IsNot Nothing Then
                        Dim newsize As SizeF = New SizeF(illu.Value.BackgroundImage.Size.Width / xResizeFactor, illu.Value.BackgroundImage.Size.Height / yResizeFactor)
                        'Dim image As Image = illu.Value.BackgroundImage.ResizedF(newsize, True)
                        'Dim offimage As Image = illu.Value.OffImage.ResizedF(newsize, True)
                        Dim image As Image = illu.Value.BackgroundImage.Resized(Size.Round(newsize), True)
                        Dim offimage As Image = illu.Value.OffImage.Resized(Size.Round(newsize), True)
                        illu.Value.BackgroundImage = image
                        illu.Value.OffImage = offimage
                    End If
                End If
            Next
        End If

        ' and now rotate and resize the rotation images
        For Each rotatingImageColl As KeyValuePair(Of Integer, Generic.Dictionary(Of Integer, Image)) In B2SData.RotatingImages
            For I As Integer = 0 To 359
                If rotatingImageColl.Value.ContainsKey(I) Then
                    If xResizeFactor <> 1 OrElse yResizeFactor <> 1 Then
                        Dim image As Image = rotatingImageColl.Value(I)
                        image = image.Resized(New Size(image.Width / xResizeFactor, image.Height / yResizeFactor), True)
                        rotatingImageColl.Value.Remove(I)
                        rotatingImageColl.Value.Add(I, image)
                    End If
                End If
            Next
            ' maybe draw the starting rotate image
            If B2SData.UseRotatingImage OrElse B2SData.UseMechRotatingImage Then
                B2SData.RotatingPictureBox(rotatingImageColl.Key).BackgroundImage = rotatingImageColl.Value(0)
                B2SData.RotatingPictureBox(rotatingImageColl.Key).Visible = True
            End If
        Next

        ' Screen scaling is complete here. Build native caches at their actual
        ' runtime rectangle so playback can use a direct, unscaled bitmap blit.
        For Each illumination As KeyValuePair(Of String, B2SPictureBox) In B2SData.Illuminations
            illumination.Value.PrepareNativeRotationFrames()
            If illumination.Value.NativeRotation AndAlso illumination.Value.AutoStartRotation Then
                ' Display the stationary source immediately while the synchronized
                ' continuous-rotation caches finish preparing in the background.
                illumination.Value.Visible = True
            End If
            If illumination.Value.MotionPathPoints.Count >= 2 AndAlso illumination.Value.MotionPathSolenoidID = 0 AndAlso illumination.Value.MotionPathLampID = 0 AndAlso illumination.Value.MotionPathB2SID = 0 Then illumination.Value.StartMotionPath()
        Next
        For Each illumination As KeyValuePair(Of String, B2SPictureBox) In B2SData.DMDIlluminations
            illumination.Value.PrepareNativeRotationFrames()
            If illumination.Value.NativeRotation AndAlso illumination.Value.AutoStartRotation Then
                illumination.Value.Visible = True
            End If
            If illumination.Value.MotionPathPoints.Count >= 2 AndAlso illumination.Value.MotionPathSolenoidID = 0 AndAlso illumination.Value.MotionPathLampID = 0 AndAlso illumination.Value.MotionPathB2SID = 0 Then illumination.Value.StartMotionPath()
        Next

    End Sub

    Private Function ParseMotionPathPoints(ByVal value As String) As List(Of PointF)
        Dim points As New List(Of PointF)()
        If String.IsNullOrWhiteSpace(value) Then Return points
        For Each pair As String In value.Split(";"c)
            Dim parts() As String = pair.Split(","c)
            If parts.Length <> 2 Then Continue For
            Dim x As Single
            Dim y As Single
            If Single.TryParse(parts(0), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, x) AndAlso
               Single.TryParse(parts(1), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, y) Then
                points.Add(New PointF(x, y))
            End If
        Next
        Return points
    End Function

    Private Function ParsePhysicsBoundaries(ByVal value As String) As List(Of List(Of PointF))
        Dim paths As New List(Of List(Of PointF))()
        If String.IsNullOrWhiteSpace(value) Then Return paths
        For Each encoded As String In value.Split("|"c)
            Dim path As List(Of PointF) = ParseMotionPathPoints(encoded)
            If path.Count >= 2 Then paths.Add(path)
        Next
        Return paths
    End Function

    Private Function ParsePhysicsObstacles(ByVal value As String) As List(Of RectangleF)
        Dim result As New List(Of RectangleF)()
        If String.IsNullOrWhiteSpace(value) Then Return result
        For Each encoded As String In value.Split("|"c)
            Dim parts As String() = encoded.Split(","c)
            Dim x, y, width, height As Single
            If parts.Length = 4 AndAlso Single.TryParse(parts(0), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, x) AndAlso
               Single.TryParse(parts(1), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, y) AndAlso
               Single.TryParse(parts(2), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, width) AndAlso
               Single.TryParse(parts(3), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, height) AndAlso width > 0 AndAlso height > 0 Then
                result.Add(New RectangleF(x, y, width, height))
            End If
        Next
        Return result
    End Function

    Private Function ParsePhysicsSwitchZones(ByVal value As String) As List(Of B2SData.PhysicsSwitchZone)
        Dim result As New List(Of B2SData.PhysicsSwitchZone)()
        If String.IsNullOrWhiteSpace(value) Then Return result
        For Each encoded As String In value.Split("|"c)
            Dim parts As String() = encoded.Split(","c)
            Dim x, y, width, height As Single
            Dim switchID As Integer
            If parts.Length = 5 AndAlso Single.TryParse(parts(0), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, x) AndAlso Single.TryParse(parts(1), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, y) AndAlso Single.TryParse(parts(2), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, width) AndAlso Single.TryParse(parts(3), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, height) AndAlso Integer.TryParse(parts(4), switchID) AndAlso width > 0 AndAlso height > 0 AndAlso switchID > 0 Then
                result.Add(New B2SData.PhysicsSwitchZone With {.Bounds = New RectangleF(x, y, width, height), .SwitchID = Math.Min(255, switchID)})
            End If
        Next
        Return result
    End Function

    Private Function ParsePhysicsBounds(ByVal value As String) As RectangleF
        If String.IsNullOrWhiteSpace(value) Then Return RectangleF.Empty
        Dim parts() As String = value.Split(","c)
        If parts.Length <> 4 Then Return RectangleF.Empty
        Dim left As Single
        Dim top As Single
        Dim right As Single
        Dim bottom As Single
        If Not Single.TryParse(parts(0), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, left) OrElse
           Not Single.TryParse(parts(1), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, top) OrElse
           Not Single.TryParse(parts(2), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, right) OrElse
           Not Single.TryParse(parts(3), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, bottom) OrElse
           right <= left OrElse bottom <= top Then Return RectangleF.Empty
        Return RectangleF.FromLTRB(left, top, right, bottom)
    End Function

    Private Sub ShowStartupSnippits()
        Static isdone As Boolean = False
        If Not isdone Then
            isdone = True
            ' maybe show some 'startup on' snippits
            Dim topIsOn As Boolean = False
            For Each picbox As KeyValuePair(Of String, B2SPictureBox) In B2SData.Illuminations
                If picbox.Value.InitialState = 1 AndAlso picbox.Value.IsImageSnippit Then
                    picbox.Value.Visible = True
                End If
            Next
            ' maybe show some 'startup on' DMD snippits
            For Each picbox As KeyValuePair(Of String, B2SPictureBox) In B2SData.DMDIlluminations
                If picbox.Value.InitialState = 1 AndAlso picbox.Value.IsImageSnippit Then
                    picbox.Value.Visible = True
                End If
            Next
        End If
    End Sub
    Private Sub ShowStartupImages()

        Static isdone As Boolean = False
        If Not isdone Then
            isdone = True
            ' maybe show some 'startup on' images
            Dim topIsOn As Boolean = False
            For Each picbox As KeyValuePair(Of String, B2SPictureBox) In B2SData.Illuminations
                If picbox.Value.InitialState = 1 AndAlso Not picbox.Value.IsImageSnippit Then
                    If TopRomID > 0 AndAlso picbox.Value.RomID = TopRomID AndAlso picbox.Value.RomIDType = TopRomIDType AndAlso picbox.Value.RomInverted = TopRomInverted Then
                        topIsOn = True
                        If TopLightImage IsNot Nothing AndAlso Not TopLightImage.Equals(Me.BackgroundImage) Then
                            Me.BackgroundImage = TopLightImage
                        End If
                    ElseIf Not topIsOn AndAlso SecondRomID > 0 AndAlso picbox.Value.RomID = SecondRomID AndAlso picbox.Value.RomIDType = SecondRomIDType AndAlso picbox.Value.RomInverted = SecondRomInverted Then
                        If SecondLightImage IsNot Nothing AndAlso Not SecondLightImage.Equals(Me.BackgroundImage) Then
                            Me.BackgroundImage = SecondLightImage
                        End If
                    Else
                        picbox.Value.Visible = True
                    End If
                End If
            Next
        End If

    End Sub

    Private Sub RotateImage(ByVal picbox As B2SPictureBox,
                            ByVal rotationsteps As Integer,
                            ByVal rotationdirection As B2SPictureBox.eSnippitRotationDirection,
                            ByVal type As B2SPictureBox.ePictureBoxType,
                            Optional ByVal romidtype As Integer = 0,
                            Optional ByVal romid As Integer = 0)

        If picbox IsNot Nothing AndAlso rotationsteps > 0 Then

            ' store some data
            If romid = 0 Then
                B2SData.UseRotatingImage = True
            Else
                B2SData.UseMechRotatingImage = True
            End If
            If Not B2SData.RotatingPictureBox.ContainsKey(romid) Then
                B2SData.RotatingPictureBox.Add(romid, picbox)
            Else
                B2SData.RotatingPictureBox(romid) = picbox
            End If

            ' calc rotation angle
            Me.rotateAngle = 360 / rotationsteps

            ' Self-rotating pictures are painted dynamically from their single
            ' source bitmap. Mechanical rotation retains its indexed image cache.
            If type = B2SPictureBox.ePictureBoxType.SelfRotatingImage AndAlso picbox.NativeRotation Then Return

            ' rotate the image the whole circle
            Dim rotatingangleS As Single = 0
            Dim index As Integer = 0
            Do While rotatingangleS < 360
                Dim rotatingAngle As Integer = CInt(rotatingangleS)
                Dim image As Image = B2SData.RotatingPictureBox(romid).BackgroundImage.Rotated(If(rotationdirection = B2SPictureBox.eSnippitRotationDirection.AntiClockwise, rotatingAngle, 360 - rotatingAngle))
                If Not B2SData.RotatingImages.ContainsKey(romid) Then B2SData.RotatingImages.Add(romid, New Generic.Dictionary(Of Integer, Image))
                B2SData.RotatingImages(romid).Add(If(picbox.PictureBoxType = B2SPictureBox.ePictureBoxType.MechRotatingImage, index, rotatingAngle), image)
                rotatingangleS += Me.rotateAngle
                index += 1
            Loop

            ' set start image
            'B2SData.RotatingPictureBox(romid).BackgroundImage = B2SData.RotatingImages(romid)(0)
            'B2SData.RotatingPictureBox(romid).Visible = True

        End If

    End Sub

#End Region


#Region "image cache stuff"

    Public ReadOnly Property DarkImage() As Image
        Get
            If B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy Then
                Return DarkImage4Fantasy
            Else
                Return DarkImage4Authentic
            End If
        End Get
    End Property
    Public ReadOnly Property TopLightImage() As Image
        Get
            If B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy Then
                Return TopLightImage4Fantasy
            Else
                Return TopLightImage4Authentic
            End If
        End Get
    End Property
    Public ReadOnly Property SecondLightImage() As Image
        Get
            If B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy Then
                Return SecondLightImage4Fantasy
            Else
                Return SecondLightImage4Authentic
            End If
        End Get
    End Property
    Public ReadOnly Property TopAndSecondLightImage() As Image
        Get
            If B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy Then
                Return TopAndSecondLightImage4Fantasy
            Else
                Return TopAndSecondLightImage4Authentic
            End If
        End Get
    End Property
    Public Property DarkImage4Authentic() As Image = Nothing
    Public Property TopLightImage4Authentic() As Image = Nothing
    Public Property SecondLightImage4Authentic() As Image = Nothing
    Public Property TopAndSecondLightImage4Authentic() As Image = Nothing
    Public Property DarkImage4Fantasy() As Image = Nothing
    Public Property TopLightImage4Fantasy() As Image = Nothing
    Public Property SecondLightImage4Fantasy() As Image = Nothing
    Public Property TopAndSecondLightImage4Fantasy() As Image = Nothing

    Public ReadOnly Property TopRomID() As Integer
        Get
            If B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy Then
                Return TopRomID4Fantasy
            Else
                Return TopRomID4Authentic
            End If
        End Get
    End Property
    Public ReadOnly Property TopRomIDType() As B2SBaseBox.eRomIDType
        Get
            If B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy Then
                Return TopRomIDType4Fantasy
            Else
                Return TopRomIDType4Authentic
            End If
        End Get
    End Property
    Public ReadOnly Property TopRomInverted() As Boolean
        Get
            If B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy Then
                Return TopRomInverted4Fantasy
            Else
                Return TopRomInverted4Authentic
            End If
        End Get
    End Property
    Public ReadOnly Property SecondRomID() As Integer
        Get
            If B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy Then
                Return SecondRomID4Fantasy
            Else
                Return SecondRomID4Authentic
            End If
        End Get
    End Property
    Public ReadOnly Property SecondRomIDType() As B2SBaseBox.eRomIDType
        Get
            If B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy Then
                Return SecondRomIDType4Fantasy
            Else
                Return SecondRomIDType4Authentic
            End If
        End Get
    End Property
    Public ReadOnly Property SecondRomInverted() As Boolean
        Get
            If B2SSettings.CurrentDualMode = B2SSettings.eDualMode.Fantasy Then
                Return SecondRomInverted4Fantasy
            Else
                Return SecondRomInverted4Authentic
            End If
        End Get
    End Property
    Public Property TopRomID4Authentic() As Integer = 0
    Public Property TopRomIDType4Authentic() As B2SBaseBox.eRomIDType = B2SBaseBox.eRomIDType.NotDefined
    Public Property TopRomInverted4Authentic() As Boolean = False
    Public Property SecondRomID4Authentic() As Integer = 0
    Public Property SecondRomIDType4Authentic() As B2SBaseBox.eRomIDType = B2SBaseBox.eRomIDType.NotDefined
    Public Property SecondRomInverted4Authentic() As Boolean = False
    Public Property TopRomID4Fantasy() As Integer = 0
    Public Property TopRomIDType4Fantasy() As B2SBaseBox.eRomIDType = B2SBaseBox.eRomIDType.NotDefined
    Public Property TopRomInverted4Fantasy() As Boolean = False
    Public Property SecondRomID4Fantasy() As Integer = 0
    Public Property SecondRomIDType4Fantasy() As B2SBaseBox.eRomIDType = B2SBaseBox.eRomIDType.NotDefined
    Public Property SecondRomInverted4Fantasy() As Boolean = False

#End Region


#Region "more private methods"

    Private Sub CheckDMDForm()
        If formDMD Is Nothing AndAlso Not B2SSettings.HideB2SDMD Then
            formDMD = New formDMD()
        End If
    End Sub

    Private Function CreateLightImage(ByRef image As Image,
                                      ByVal dualmode As B2SData.eDualMode,
                                      Optional ByVal firstromkey As String = "",
                                      Optional ByVal secondromkey As String = "",
                                      Optional ByRef romid As Integer = 0,
                                      Optional ByRef romidtype As B2SBaseBox.eRomIDType = B2SBaseBox.eRomIDType.NotDefined,
                                      Optional ByRef rominverted As Boolean = False) As Image
        Dim firstromValue As Integer = 0

        Dim secondromid As Integer = 0
        Dim secondromValue As Integer = 0
        Dim secondromidtype As B2SBaseBox.eRomIDType = B2SBaseBox.eRomIDType.NotDefined
        Dim secondrominverted As Boolean = False
        If firstromkey.Substring(0, 1) = "I" Then
            rominverted = True
            firstromkey = firstromkey.Substring(1)
        End If
        firstromValue = CInt(secondromkey.Split("|")(1))
        firstromkey = firstromkey.Split("|")(0)

        romidtype = If((firstromkey.Substring(0, 1) = "S"), B2SBaseBox.eRomIDType.Solenoid, If((firstromkey.Substring(0, 2) = "GI"), B2SBaseBox.eRomIDType.GIString, B2SBaseBox.eRomIDType.Lamp))
        romid = CInt(If((romidtype = B2SBaseBox.eRomIDType.GIString), firstromkey.Substring(2), firstromkey.Substring(1)))
        If Not String.IsNullOrEmpty(secondromkey) Then
            secondromValue = CInt(secondromkey.Split("|")(1))
            secondromkey = secondromkey.Split("|")(0)
            If secondromkey.Substring(0, 1) = "I" Then
                secondrominverted = True
                secondromkey = secondromkey.Substring(1)
            End If
            secondromidtype = If((secondromkey.Substring(0, 1) = "S"), B2SBaseBox.eRomIDType.Solenoid, If((secondromkey.Substring(0, 2) = "GI"), B2SBaseBox.eRomIDType.GIString, B2SBaseBox.eRomIDType.Lamp))
            secondromid = CInt(If((secondromidtype = B2SBaseBox.eRomIDType.GIString), secondromkey.Substring(2), secondromkey.Substring(1)))
        End If
        ' create image copy
        Dim ret As Image = New Bitmap(image.Width, image.Height)
        Using gr As Graphics = Graphics.FromImage(ret)
            gr.InterpolationMode = Drawing2D.InterpolationMode.High
            gr.SmoothingMode = Drawing2D.SmoothingMode.HighQuality
            gr.DrawImage(image, New Rectangle(0, 0, ret.Width, ret.Height))
        End Using
        ' draw matching bulbs into image
        For Each picbox As B2SPictureBox In Me.Controls.OfType(Of B2SPictureBox)()
            If picbox.RomID = romid AndAlso picbox.RomIDValue = firstromValue AndAlso picbox.RomIDType = romidtype AndAlso picbox.RomInverted = rominverted AndAlso (picbox.DualMode = B2SData.eDualMode.Both OrElse picbox.DualMode = dualmode) Then
                Using gr As Graphics = Graphics.FromImage(ret)
                    gr.InterpolationMode = Drawing2D.InterpolationMode.High
                    gr.SmoothingMode = Drawing2D.SmoothingMode.HighQuality
                    gr.DrawImage(picbox.BackgroundImage, New Rectangle(picbox.Location.X, picbox.Location.Y, picbox.Size.Width, picbox.Size.Height))
                End Using
            End If
        Next
        ' maybe draw second matching bulbs into image
        If Not String.IsNullOrEmpty(secondromkey) Then
            For Each picbox As B2SPictureBox In Me.Controls.OfType(Of B2SPictureBox)()
                If picbox.RomID = secondromid AndAlso picbox.RomIDValue = secondromValue AndAlso picbox.RomIDType = secondromidtype AndAlso picbox.RomInverted = secondrominverted AndAlso (picbox.DualMode = B2SData.eDualMode.Both OrElse picbox.DualMode = dualmode) Then
                    Using gr As Graphics = Graphics.FromImage(ret)
                        gr.InterpolationMode = Drawing2D.InterpolationMode.High
                        gr.DrawImage(picbox.BackgroundImage, New Rectangle(picbox.Location.X, picbox.Location.Y, picbox.Size.Width, picbox.Size.Height))
                    End Using
                End If
            Next
        End If
        ' that's it
        Return ret
    End Function
    Private Sub CheckBulbs(ByVal romid As Integer,
                           ByVal romidtype As B2SBaseBox.eRomIDType,
                           ByVal rominverted As Boolean,
                           ByVal dualmode As B2SData.eDualMode)
        If romid > 0 AndAlso romidtype <> B2SBaseBox.eRomIDType.NotDefined Then
            Dim UsedRomIDs As Generic.SortedList(Of Integer, B2SBaseBox()) = Nothing
            If romidtype = B2SBaseBox.eRomIDType.Lamp Then
                UsedRomIDs = If(dualmode = B2SData.eDualMode.Fantasy, B2SData.UsedRomLampIDs4Fantasy, B2SData.UsedRomLampIDs4Authentic)
            ElseIf romidtype = B2SBaseBox.eRomIDType.Solenoid Then
                UsedRomIDs = If(dualmode = B2SData.eDualMode.Fantasy, B2SData.UsedRomSolenoidIDs4Fantasy, B2SData.UsedRomSolenoidIDs4Authentic)
            ElseIf romidtype = B2SBaseBox.eRomIDType.GIString Then
                UsedRomIDs = If(dualmode = B2SData.eDualMode.Fantasy, B2SData.UsedRomGIStringIDs4Fantasy, B2SData.UsedRomGIStringIDs4Authentic)
            End If
            If UsedRomIDs.ContainsKey(romid) Then
                UsedRomIDs.Remove(romid)
                For Each picbox As KeyValuePair(Of String, B2SPictureBox) In B2SData.Illuminations
                    With picbox.Value
                        If .RomID = romid AndAlso .RomIDType = romidtype AndAlso .RomInverted <> rominverted AndAlso (.DualMode = B2SData.eDualMode.Both OrElse .DualMode = dualmode) Then
                            B2SData.Illuminations.Add(picbox.Value, dualmode)
                        End If
                    End With
                Next
            End If
        End If
    End Sub
    Public Function GetBoundingRectangle(image As Bitmap) As Rectangle
        Dim rect As New Rectangle(0, 0, image.Width, image.Height)
        Dim bmpData As Imaging.BitmapData = image.LockBits(rect, Imaging.ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppArgb)

        Dim stride As Integer = bmpData.Stride
        Dim scan0 As IntPtr = bmpData.Scan0

        Dim minX As Integer = image.Width
        Dim minY As Integer = image.Height
        Dim maxX As Integer = 0
        Dim maxY As Integer = 0

        Dim foundNonTransparent As Boolean = False

        Dim pixels(image.Height * stride - 1) As Byte
        System.Runtime.InteropServices.Marshal.Copy(scan0, pixels, 0, pixels.Length)

        For y As Integer = 0 To image.Height - 1
            For x As Integer = 0 To image.Width - 1
                Dim index As Integer = (y * stride) + (x * 4) ' 4 bytes per pixel (32bpp)
                Dim alpha As Byte = pixels(index + 3)

                If alpha <> 0 Then
                    foundNonTransparent = True
                    If x < minX Then minX = x
                    If y < minY Then minY = y
                    If x > maxX Then maxX = x
                    If y > maxY Then maxY = y
                End If
            Next
        Next

        image.UnlockBits(bmpData)

        If Not foundNonTransparent Then
            ' No non-transparent pixels found
            Return Rectangle.Empty
        End If

        Return New Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1)
    End Function

    Public Function CropImageToTransparency(image As Image, offimage As Image, ByRef loc As Point, ByRef size As Size) As Image
        Dim bitmap As Bitmap = CType(image, Bitmap)
        Dim boundingRect As Rectangle = GetBoundingRectangle(bitmap)

        If boundingRect = Rectangle.Empty Then
            ' Return an empty image or the original image as needed
            Return image
        End If

        Dim croppedImage As New Bitmap(boundingRect.Width, boundingRect.Height)

        If offimage IsNot Nothing Then
            Dim offbitmap As Bitmap = CType(offimage, Bitmap)
            Dim offboundingRect As Rectangle = GetBoundingRectangle(offbitmap)
            If offboundingRect = Rectangle.Empty Then
                ' Return an empty image or the original image as needed
                Return image
            End If
            ' Crop the image
            boundingRect = Rectangle.Union(boundingRect, offboundingRect)
            Using g As Graphics = Graphics.FromImage(croppedImage)
                g.DrawImage(offbitmap, New Rectangle(0, 0, boundingRect.Width, boundingRect.Height), boundingRect, GraphicsUnit.Pixel)
            End Using
            offimage = offbitmap
        End If

        Using g As Graphics = Graphics.FromImage(croppedImage)
            g.DrawImage(bitmap, New Rectangle(0, 0, boundingRect.Width, boundingRect.Height), boundingRect, GraphicsUnit.Pixel)
        End Using

        ' Update loc and size based on the new dimensions
        Dim sizeOrg As Size = size
        size = New Size(CInt(size.Width * (boundingRect.Width / image.Width)), CInt(size.Height * (boundingRect.Height / image.Height)))
        loc = New Point(loc.X + boundingRect.X, loc.Y + boundingRect.Y)

        Return croppedImage
    End Function
    Private Function ImageToBase64(image As Image) As String
        If image IsNot Nothing Then
            With New System.Drawing.ImageConverter
                Dim bytes() As Byte = CType(.ConvertTo(image, GetType(Byte())), Byte())
                Return Convert.ToBase64String(bytes, Base64FormattingOptions.InsertLineBreaks)
            End With
        Else
            Return String.Empty
        End If
    End Function
    Private Function Base64ToImage(data As String) As Image
        Dim image As Image = Nothing
        If data.Length > 0 Then
            Dim bytes() As Byte = Convert.FromBase64String(data)
            If bytes IsNot Nothing AndAlso bytes.Length > 0 Then
                With New System.Drawing.ImageConverter
                    image = CType(.ConvertFrom(bytes), Image)
                End With
            End If
        End If
        Return image
    End Function

    Public Function WavToBase64(stream As IO.Stream) As String
        If stream IsNot Nothing Then
            Dim bytes() As Byte
            ReDim bytes(stream.Length - 1)
            Using reader As IO.BinaryReader = New IO.BinaryReader(stream)
                Dim length As Integer = reader.Read(bytes, 0, stream.Length)
            End Using
            Return Convert.ToBase64String(bytes, Base64FormattingOptions.InsertLineBreaks)
        Else
            Return String.Empty
        End If
    End Function
    Public Function Base64ToWav(data As String) As IO.Stream
        If data.Length > 0 Then
            Dim bytes() As Byte = Convert.FromBase64String(data)
            Return New IO.MemoryStream(bytes)
        Else
            Return Nothing
        End If
    End Function

    Private Function Color2String(ByVal color As Color) As String
        Return color.R.ToString() & "." & color.G.ToString() & "." & color.B.ToString()
    End Function
    Private Function String2Color(ByVal color As String) As Color
        Dim colorvalues As String() = color.Split(".")
        Return Drawing.Color.FromArgb(CInt(colorvalues(0)), CInt(colorvalues(1)), CInt(colorvalues(2)))
    End Function

    Private Sub SetFocusToVPPlayer()

        ' set focus to the VP player
        Dim proc As Processes = New Processes()
        SetForegroundWindow(proc.TableHandle)
#If B2S = "EXE" Then
        tableHandle = proc.TableHandle
#End If
    End Sub

    Private Function RandomStarter(ByVal top As Integer) As Integer

        Static lastone As Integer = -1
        Dim ret As Integer = -1
        Do Until ret >= 0 AndAlso ret < top AndAlso ret <> lastone
            Dim random As Random = New Random(Date.Now.Millisecond)
            ret = CInt(Math.Truncate(random.NextDouble() * top))
        Loop
        lastone = ret
        Return ret

    End Function
#End Region


End Class
