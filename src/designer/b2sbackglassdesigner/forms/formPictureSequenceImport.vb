Imports System
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Text.RegularExpressions

Public Class formPictureSequenceImport
    Inherits B2SThemedForm

    Private ReadOnly lstFrames As New ListBox()
    Private ReadOnly txtName As New TextBox()
    Private ReadOnly cmbDestination As New ComboBox()
    Private ReadOnly numX As New NumericUpDown()
    Private ReadOnly numY As New NumericUpDown()
    Private ReadOnly numWidth As New NumericUpDown()
    Private ReadOnly numHeight As New NumericUpDown()
    Private ReadOnly numInterval As New NumericUpDown()
    Private ReadOnly chkContinuous As New CheckBox()
    Private ReadOnly chkStartup As New CheckBox()
    Private ReadOnly btnImport As New Button()
    Private ReadOnly btnCancel As New Button()
    Private ReadOnly btnChoose As New Button()
    Private temporaryGifFolder As String = String.Empty

    Public ReadOnly Property FrameFiles As List(Of String) = New List(Of String)()
    Public ReadOnly Property FrameWaitLoops As List(Of Integer) = New List(Of Integer)()
    Public ReadOnly Property AnimationName As String
        Get
            Return txtName.Text
        End Get
    End Property
    Public ReadOnly Property UseDMD As Boolean
        Get
            Return cmbDestination.SelectedIndex = 1
        End Get
    End Property
    Public ReadOnly Property OutputLocation As Point
        Get
            Return New Point(CInt(numX.Value), CInt(numY.Value))
        End Get
    End Property
    Public ReadOnly Property OutputSize As Size
        Get
            Return New Size(CInt(numWidth.Value), CInt(numHeight.Value))
        End Get
    End Property
    Public ReadOnly Property FrameInterval As Integer
        Get
            Return CInt(numInterval.Value)
        End Get
    End Property
    Public ReadOnly Property ContinuousLoop As Boolean
        Get
            Return chkContinuous.Checked
        End Get
    End Property
    Public ReadOnly Property StartAtStartup As Boolean
        Get
            Return chkStartup.Checked
        End Get
    End Property

    Public Sub New()
        Me.Text = "Import Picture Sequence (TEST)"
        Me.ClientSize = New Size(720, 520)
        Me.MinimumSize = New Size(680, 500)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.FormBorderStyle = FormBorderStyle.Sizable

        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .Padding = New Padding(12),
                                                 .ColumnCount = 1, .RowCount = 7}
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 44.0F))

        Dim fileRow As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2}
        fileRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        fileRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 185.0F))
        fileRow.Controls.Add(FormLabel("Ordered PNG frames or one animated GIF:"), 0, 0)
        btnChoose.Text = "Choose PNG / GIF..."
        btnChoose.Dock = DockStyle.Fill
        AddHandler btnChoose.Click, AddressOf ChooseFrames
        fileRow.Controls.Add(btnChoose, 1, 0)
        layout.Controls.Add(fileRow, 0, 0)

        lstFrames.Dock = DockStyle.Fill
        layout.Controls.Add(lstFrames, 0, 1)

        Dim details As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 4}
        details.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110.0F))
        details.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 55.0F))
        details.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 90.0F))
        details.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 45.0F))
        details.Controls.Add(FormLabel("Animation name:"), 0, 0)
        txtName.Dock = DockStyle.Fill : details.Controls.Add(txtName, 1, 0)
        details.Controls.Add(FormLabel("Destination:"), 2, 0)
        cmbDestination.Dock = DockStyle.Fill
        cmbDestination.DropDownStyle = ComboBoxStyle.DropDownList
        cmbDestination.Items.AddRange(New Object() {"Backglass", "DMD screen"})
        cmbDestination.SelectedIndex = 0
        details.Controls.Add(cmbDestination, 3, 0)
        layout.Controls.Add(details, 0, 2)

        Dim boundsRow As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 8}
        For index As Integer = 0 To 3
            boundsRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, If(index < 2, 28.0F, 52.0F)))
            boundsRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F))
        Next
        ConfigureNumber(numX, 0, 20000, 0) : ConfigureNumber(numY, 0, 20000, 0)
        ConfigureNumber(numWidth, 1, 20000, 320) : ConfigureNumber(numHeight, 1, 20000, 240)
        boundsRow.Controls.Add(FormLabel("X:"), 0, 0) : boundsRow.Controls.Add(numX, 1, 0)
        boundsRow.Controls.Add(FormLabel("Y:"), 2, 0) : boundsRow.Controls.Add(numY, 3, 0)
        boundsRow.Controls.Add(FormLabel("Width:"), 4, 0) : boundsRow.Controls.Add(numWidth, 5, 0)
        boundsRow.Controls.Add(FormLabel("Height:"), 6, 0) : boundsRow.Controls.Add(numHeight, 7, 0)
        layout.Controls.Add(boundsRow, 0, 3)

        Dim options As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight,
                                                 .WrapContents = False, .Padding = New Padding(0, 4, 0, 0)}
        options.Controls.Add(FormLabel("Frame interval (ms):", 135))
        ConfigureNumber(numInterval, 10, 5000, 50) : numInterval.Width = 90 : options.Controls.Add(numInterval)
        chkContinuous.Text = "Loop continuously"
        chkContinuous.Width = 145
        chkContinuous.Checked = True
        options.Controls.Add(chkContinuous)
        chkStartup.Text = "Start at backglass startup"
        chkStartup.Width = 185
        options.Controls.Add(chkStartup)
        layout.Controls.Add(options, 0, 4)

        Dim note As New Label() With {
            .Text = "GIF frames and timing are converted into the existing optimized picture-animation format. Shared transparent padding is cropped without changing alignment.",
            .AutoSize = False
        }
        note.Dock = DockStyle.Fill : note.TextAlign = ContentAlignment.MiddleLeft
        layout.Controls.Add(note, 0, 5)

        btnImport.Text = "Import Sequence"
        btnImport.Width = 135 : btnImport.Height = 30
        AddHandler btnImport.Click, AddressOf AcceptImport
        btnCancel.Text = "Cancel"
        btnCancel.Width = 120 : btnCancel.Height = 30
        btnCancel.DialogResult = DialogResult.Cancel
        Dim actions As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft,
                                                 .WrapContents = False, .Padding = New Padding(0, 5, 0, 0)}
        actions.Controls.Add(btnCancel) : actions.Controls.Add(btnImport)
        layout.Controls.Add(actions, 0, 6)
        Me.Controls.Add(layout)
        Me.CancelButton = btnCancel
    End Sub

    Private Shared Function FormLabel(ByVal text As String, Optional ByVal width As Integer = 0) As Label
        Dim label As New Label With {.Text = text, .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft,
                                    .AutoEllipsis = True}
        If width > 0 Then label.Dock = DockStyle.None : label.Width = width : label.Height = 28
        Return label
    End Function

    Private Shared Sub ConfigureNumber(ByVal control As NumericUpDown, ByVal minimum As Decimal,
                                       ByVal maximum As Decimal, ByVal value As Decimal)
        control.Dock = DockStyle.Fill
        control.Minimum = minimum
        control.Maximum = maximum
        control.Value = value
    End Sub

    Private Sub ChooseFrames(ByVal sender As Object, ByVal e As EventArgs)
        Using dialog As New OpenFileDialog()
            dialog.Title = "Choose ordered PNG frames or one animated GIF"
            dialog.Filter = "Picture animation (*.png;*.gif)|*.png;*.gif|PNG images (*.png)|*.png|Animated GIF (*.gif)|*.gif"
            dialog.Multiselect = True
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return

            Dim gifFiles As String() = dialog.FileNames.Where(Function(path) String.Equals(IO.Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase)).ToArray()
            If gifFiles.Length > 0 AndAlso (gifFiles.Length <> 1 OrElse dialog.FileNames.Length <> 1) Then
                B2SMessageBox.Show("Choose either one animated GIF or two or more PNG frames.", "Picture Animation Import", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            ClearTemporaryGifFrames()
            FrameFiles.Clear()
            FrameWaitLoops.Clear()
            If gifFiles.Length = 1 Then
                Try
                    ExtractGifFrames(gifFiles(0))
                Catch ex As Exception
                    B2SMessageBox.Show("The GIF could not be decoded: " & ex.Message, "Picture Animation Import", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    Return
                End Try
                If txtName.Text.Trim().Length = 0 Then txtName.Text = IO.Path.GetFileNameWithoutExtension(gifFiles(0))
            Else
                FrameFiles.AddRange(dialog.FileNames.OrderBy(Function(path) path, New NaturalPathComparer()))
                For index As Integer = 0 To FrameFiles.Count - 1
                    FrameWaitLoops.Add(1)
                Next
                If FrameFiles.Count > 0 AndAlso txtName.Text.Trim().Length = 0 Then txtName.Text = IO.Path.GetFileName(IO.Path.GetDirectoryName(FrameFiles(0)))
            End If
            lstFrames.Items.Clear()
            For Each filename As String In FrameFiles
                lstFrames.Items.Add(IO.Path.GetFileName(filename))
            Next
            If FrameFiles.Count > 0 Then
                Using first As Image = Image.FromFile(FrameFiles(0))
                    numWidth.Value = Math.Min(numWidth.Maximum, Math.Max(numWidth.Minimum, first.Width))
                    numHeight.Value = Math.Min(numHeight.Maximum, Math.Max(numHeight.Minimum, first.Height))
                End Using
            End If
        End Using
    End Sub

    Private Sub ExtractGifFrames(ByVal filename As String)
        temporaryGifFolder = IO.Path.Combine(IO.Path.GetTempPath(), "B2SPro-GifImport-" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(temporaryGifFolder)
        Try
            Using gif As Image = Image.FromFile(filename)
                Dim dimension As New FrameDimension(gif.FrameDimensionsList(0))
                Dim frameCount As Integer = gif.GetFrameCount(dimension)
                If frameCount < 2 Then Throw New InvalidOperationException("The selected GIF does not contain multiple animation frames.")

                Dim delays As Integer() = ReadGifDelays(gif, frameCount)
                Dim baseDelay As Integer = delays(0)
                For index As Integer = 1 To delays.Length - 1
                    baseDelay = GreatestCommonDivisor(baseDelay, delays(index))
                Next
                baseDelay = Math.Max(10, baseDelay)
                numInterval.Value = Math.Min(numInterval.Maximum, Math.Max(numInterval.Minimum, baseDelay))

                For index As Integer = 0 To frameCount - 1
                    gif.SelectActiveFrame(dimension, index)
                    Dim output As String = IO.Path.Combine(temporaryGifFolder, "frame_" & (index + 1).ToString("0000") & ".png")
                    Using frame As New Bitmap(gif.Width, gif.Height, PixelFormat.Format32bppArgb)
                        Using graphics As Graphics = Graphics.FromImage(frame)
                            graphics.Clear(Color.Transparent)
                            graphics.CompositingMode = Drawing2D.CompositingMode.SourceCopy
                            graphics.DrawImageUnscaled(gif, 0, 0)
                        End Using
                        frame.Save(output, ImageFormat.Png)
                    End Using
                    FrameFiles.Add(output)
                    FrameWaitLoops.Add(Math.Max(1, CInt(Math.Round(delays(index) / CDbl(baseDelay)))))
                Next
            End Using
        Catch
            FrameFiles.Clear()
            FrameWaitLoops.Clear()
            ClearTemporaryGifFrames()
            Throw
        End Try
    End Sub

    Private Function ReadGifDelays(ByVal gif As Image, ByVal frameCount As Integer) As Integer()
        Dim delays(frameCount - 1) As Integer
        For index As Integer = 0 To delays.Length - 1
            delays(index) = 100
        Next
        Try
            Dim delayProperty As PropertyItem = gif.GetPropertyItem(&H5100)
            For index As Integer = 0 To frameCount - 1
                Dim offset As Integer = index * 4
                If offset + 3 < delayProperty.Value.Length Then
                    Dim hundredths As Integer = BitConverter.ToInt32(delayProperty.Value, offset)
                    delays(index) = Math.Max(10, hundredths * 10)
                End If
            Next
        Catch
            ' GIF delay metadata is optional; 100 ms is the established fallback.
        End Try
        Return delays
    End Function

    Private Function GreatestCommonDivisor(ByVal left As Integer, ByVal right As Integer) As Integer
        left = Math.Abs(left)
        right = Math.Abs(right)
        While right <> 0
            Dim remainder As Integer = left Mod right
            left = right
            right = remainder
        End While
        Return Math.Max(1, left)
    End Function

    Private Sub ClearTemporaryGifFrames()
        If String.IsNullOrEmpty(temporaryGifFolder) Then Return
        Try
            If Directory.Exists(temporaryGifFolder) Then Directory.Delete(temporaryGifFolder, True)
        Catch
            ' Temporary cleanup must not damage an otherwise completed import.
        End Try
        temporaryGifFolder = String.Empty
    End Sub

    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        If disposing Then ClearTemporaryGifFrames()
        MyBase.Dispose(disposing)
    End Sub

    Private Sub AcceptImport(ByVal sender As Object, ByVal e As EventArgs)
        If FrameFiles.Count < 2 Then
            B2SMessageBox.Show("Choose one animated GIF or at least two PNG frames.", "Picture Animation Import", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        If txtName.Text.Trim().Length = 0 Then
            B2SMessageBox.Show("Enter an animation name.", "Picture Animation Import", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        If UseDMD AndAlso (Backglass.currentData Is Nothing OrElse Backglass.currentData.DMDImage Is Nothing) Then
            B2SMessageBox.Show("This backglass does not currently have a DMD image/canvas.", "Picture Animation Import", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    Private Class NaturalPathComparer
        Implements IComparer(Of String)

        Public Function Compare(x As String, y As String) As Integer Implements IComparer(Of String).Compare
            Dim left = Regex.Split(IO.Path.GetFileName(x), "(\d+)")
            Dim right = Regex.Split(IO.Path.GetFileName(y), "(\d+)")
            For index As Integer = 0 To Math.Min(left.Length, right.Length) - 1
                Dim result As Integer
                Dim leftNumber As Long
                Dim rightNumber As Long
                If Long.TryParse(left(index), leftNumber) AndAlso Long.TryParse(right(index), rightNumber) Then
                    result = leftNumber.CompareTo(rightNumber)
                Else
                    result = StringComparer.OrdinalIgnoreCase.Compare(left(index), right(index))
                End If
                If result <> 0 Then Return result
            Next
            Return left.Length.CompareTo(right.Length)
        End Function
    End Class
End Class
