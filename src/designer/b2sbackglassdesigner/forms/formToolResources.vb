Imports System

Imports System.Drawing.Drawing2D

Public Class formToolResources

    Public Enum eImagesDataType
        NotDefined = 0
        BackgroundImageRemoved = 1
        IlluminationImageRemoved = 2
        DMDImageRemoved = 3
        BackgroundImageSelectionChanged = 6
        IlluminatedImageSelectionChanged = 7
        DMDImageSelectionChanged = 8
        BackgroundImageTypeChanged = 11
        BackgroundImageRomIDChanged = 12
        BackgroundImageRomIDTypeChanged = 13
    End Enum

    Public Event DataChanged(ByVal sender As Object, ByVal e As ImagesEventArgs)
    Public Class ImagesEventArgs
        Inherits EventArgs

        Public TypeOfData As eImagesDataType = eImagesDataType.NotDefined
        Public Data As Object = Nothing

        Public Sub New(ByVal _typeofdata As eImagesDataType)
            TypeOfData = _typeofdata
        End Sub
        Public Sub New(ByVal _typeofdata As eImagesDataType, ByVal _data As Object)
            TypeOfData = _typeofdata
            Data = _data
        End Sub
    End Class

    Private _imageInfoList As Images.ImageCollection = Nothing
    Public Property ImageInfoList() As Images.ImageCollection
        Get
            Return _imageInfoList
        End Get
        Set(ByVal value As Images.ImageCollection)
            _imageInfoList = value
            Me.Invalidate()
        End Set
    End Property

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.
        MyBase.SaveName = Me.Name
        MyBase.DefaultLocation = eDefaultLocation.NW

        Me.SetStyle(ControlStyles.OptimizedDoubleBuffer, True)
        Me.DoubleBuffered = True

    End Sub

    Public Shadows Sub Invalidate()
        lbImages.DataSource = Nothing
        lbImages.DataSource = ImageInfoList
        MyBase.Invalidate()
    End Sub

    Private Sub formToolResources_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load

        lbImages.ItemHeight = 54
        lbImages.DrawMode = DrawMode.OwnerDrawFixed

        lbImages.DataSource = ImageInfoList

        cmbImageType.SelectionLength = 0
        cmbROMIDType.SelectionLength = 0

    End Sub

    Private Sub Images_Resize(ByVal sender As Object, ByVal e As System.EventArgs) Handles lbImages.Resize
        lbImages.Invalidate()
    End Sub
    Private Sub Images_DrawItem(ByVal sender As Object, ByVal e As System.Windows.Forms.DrawItemEventArgs) Handles lbImages.DrawItem

        If e.Index > -1 Then
            Dim item As Images.ImageInfo = CType(lbImages.Items(e.Index), Images.ImageInfo)
            Dim selected As Boolean = (e.State And DrawItemState.Selected) = DrawItemState.Selected
            Dim titleItem As Boolean = IsTitleItem(item)
            Dim textColor As Color
            If AppThemeManager.DarkMode Then
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
                Using baseBrush As New SolidBrush(Color.FromArgb(1, 3, 6))
                    e.Graphics.FillRectangle(baseBrush, e.Bounds)
                End Using
                Dim accent As Color = If(selected,
                                         Color.FromArgb(205, 82, 255),
                                         If(titleItem, Color.FromArgb(49, 199, 255), Color.FromArgb(38, 151, 214)))
                Dim cardBounds As Rectangle = Rectangle.Inflate(e.Bounds, -3, -2)
                Using path As GraphicsPath = ResourceCardPath(cardBounds, 7)
                    Using rowBrush As New LinearGradientBrush(cardBounds,
                                                              If(selected, Color.FromArgb(71, 22, 103), If(titleItem, Color.FromArgb(11, 42, 57), Color.FromArgb(9, 12, 19))),
                                                              Color.FromArgb(2, 4, 8),
                                                              LinearGradientMode.Vertical)
                        e.Graphics.FillPath(rowBrush, path)
                    End Using
                    Using glow As New Pen(Color.FromArgb(If(selected, 60, 25), accent), 3.0F)
                        e.Graphics.DrawPath(glow, path)
                    End Using
                    Using border As New Pen(Color.FromArgb(If(selected, 225, 105), accent), 1.0F)
                        e.Graphics.DrawPath(border, path)
                    End Using
                End Using
                textColor = If(selected, Color.White, Color.FromArgb(232, 237, 247))
            Else
                e.DrawBackground()
                textColor = Color.Black
            End If

            If titleItem Then
                Using titleFont As New Font(Me.Font.Name, Me.Font.Size + 1, FontStyle.Bold)
                    TextRenderer.DrawText(e.Graphics, item.Text, titleFont, New Rectangle(e.Bounds.X + 138, e.Bounds.Y, e.Bounds.Width - 143, e.Bounds.Height), If(AppThemeManager.DarkMode, Color.FromArgb(49, 199, 255), textColor), TextFormatFlags.WordBreak Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
                End Using
            Else
                ' maybe draw preview image
                If item.Image IsNot Nothing Then
                    If AppThemeManager.DarkMode Then
                        Dim previewBounds As New Rectangle(e.Bounds.X + 6, e.Bounds.Y + 4, 128, Math.Max(1, e.Bounds.Height - 8))
                        Using previewBack As New SolidBrush(Color.FromArgb(0, 1, 3))
                            e.Graphics.FillRectangle(previewBack, previewBounds)
                        End Using
                        Using previewBorder As New Pen(Color.FromArgb(115, 49, 199, 255), 1.0F)
                            e.Graphics.DrawRectangle(previewBorder, previewBounds)
                        End Using
                    End If
                    ' maybe shrink image
                    Dim factor As Single = Math.Max(item.Image.Width / 128, item.Image.Height / 48)
                    Dim shrinkedimage As Image = item.Image.Resized(New Size(item.Image.Width / factor, item.Image.Height / factor))
                    ' draw image
                    e.Graphics.DrawImage(shrinkedimage, New Point(5, e.Bounds.Y + 3))
                End If
                ' text 
                TextRenderer.DrawText(e.Graphics, item.Text.Replace("\", "\ "), Me.Font, New Rectangle(e.Bounds.X + 138, e.Bounds.Y, e.Bounds.Width - 143, e.Bounds.Height), textColor, TextFormatFlags.VerticalCenter Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.WordBreak)
            End If
            If (e.State And DrawItemState.Focus) = DrawItemState.Focus Then e.DrawFocusRectangle()
        End If

    End Sub

    Private Shared Function ResourceCardPath(bounds As Rectangle, radius As Integer) As GraphicsPath
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

    Private Sub Images_Click(ByVal sender As Object, ByVal e As System.EventArgs) Handles lbImages.Click
        SelectItem(sender, False)
    End Sub
    Private Sub Images_DoubleClick(ByVal sender As Object, ByVal e As System.EventArgs) Handles lbImages.DoubleClick
        SelectItem(sender, True)
    End Sub
    Private Sub Images_KeyDown(ByVal sender As Object, ByVal e As System.Windows.Forms.KeyEventArgs) Handles lbImages.KeyDown
        If lbImages.SelectedItem IsNot Nothing Then
            Dim item As Images.ImageInfo = TryCast(sender.SelectedItem, Images.ImageInfo)
            If item IsNot Nothing Then
                If e.KeyCode = Keys.Delete Then
                    If item.Type = Images.eImageInfoType.BackgroundImage OrElse item.Type = Images.eImageInfoType.IlluminationImage OrElse item.Type = Images.eImageInfoType.DMDImage Then
                        ImageInfoList.Remove(item)
                        If item.Type = Images.eImageInfoType.BackgroundImage Then
                            RaiseEvent DataChanged(Me, New ImagesEventArgs(eImagesDataType.BackgroundImageRemoved, Nothing))
                        ElseIf item.Type = Images.eImageInfoType.IlluminationImage Then
                            RaiseEvent DataChanged(Me, New ImagesEventArgs(eImagesDataType.IlluminationImageRemoved, Nothing))
                        ElseIf item.Type = Images.eImageInfoType.DMDImage Then
                            RaiseEvent DataChanged(Me, New ImagesEventArgs(eImagesDataType.DMDImageRemoved, Nothing))
                        End If
                        Me.Invalidate()
                    End If
                ElseIf e.KeyCode = Keys.Space Then
                    SelectItem(sender.SelectedItem, True)
                End If
            End If
        End If
    End Sub
    Private Sub lbImages_MouseUp(sender As Object, e As System.Windows.Forms.MouseEventArgs) Handles lbImages.MouseUp
        If e.Button = Windows.Forms.MouseButtons.Right Then
            lbImages.SelectedIndex = lbImages.IndexFromPoint(e.X, e.Y)
            If lbImages.SelectedItem IsNot Nothing Then
                Dim item As Images.ImageInfo = TryCast(sender.SelectedItem, Images.ImageInfo)
                If item.Image IsNot Nothing Then
                    Using sfd As SaveFileDialog = New SaveFileDialog
                        With sfd
                            .Filter = ImageFileExtensionFilter
                            .FileName = item.Text
                            If .ShowDialog(Me) = Windows.Forms.DialogResult.OK Then
                                Try
                                    item.Image.Save(.FileName)
                                Catch ex As Exception
                                    B2SMessageBox.Show(String.Format(My.Resources.MSG_CannotSavePicFile, ex.Message), AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error)
                                End Try
                            End If
                        End With
                    End Using
                End If
            End If
        End If
    End Sub

    Private Sub ImageType_SelectedIndexChanged(sender As System.Object, e As System.EventArgs) Handles cmbImageType.SelectedIndexChanged
        If DoNoEvents Then Exit Sub
        If lbImages.SelectedItem IsNot Nothing Then
            Dim item As Images.ImageInfo = TryCast(lbImages.SelectedItem, Images.ImageInfo)
            If item IsNot Nothing AndAlso item.Type = Images.eImageInfoType.BackgroundImage Then
                item.BackgroundImageType = cmbImageType.SelectedIndex
            End If
        End If
        RaiseEvent DataChanged(Me, New ImagesEventArgs(eImagesDataType.BackgroundImageTypeChanged, cmbImageType.SelectedIndex))
    End Sub
    Private Sub RomID_TextChanged(sender As System.Object, e As System.EventArgs) Handles txtRomID.TextChanged
        If DoNoEvents Then Exit Sub
        If Not String.IsNullOrEmpty(txtRomID.Text) Then
            If (Not IsNumeric(txtRomID.Text) OrElse txtRomID.Text = "0") Then txtRomID.Text = ""
            If Not String.IsNullOrEmpty(txtRomID.Text) AndAlso cmbROMIDType.SelectedIndex <= 0 Then cmbROMIDType.SelectedIndex = 1
        End If
        If lbImages.SelectedItem IsNot Nothing Then
            Dim item As Images.ImageInfo = TryCast(lbImages.SelectedItem, Images.ImageInfo)
            If item IsNot Nothing AndAlso item.Type = Images.eImageInfoType.BackgroundImage Then
                item.RomID = CInt(If(Not String.IsNullOrEmpty(txtRomID.Text), txtRomID.Text, "0"))
            End If
        End If
        RaiseEvent DataChanged(Me, New ImagesEventArgs(eImagesDataType.BackgroundImageRomIDChanged, txtRomID.Text))
    End Sub
    Private Sub ROMIDType_SelectedIndexChanged(sender As System.Object, e As System.EventArgs) Handles cmbROMIDType.SelectedIndexChanged
        If DoNoEvents Then Exit Sub
        If lbImages.SelectedItem IsNot Nothing Then
            Dim item As Images.ImageInfo = TryCast(lbImages.SelectedItem, Images.ImageInfo)
            If item IsNot Nothing AndAlso item.Type = Images.eImageInfoType.BackgroundImage Then
                item.RomIDType = cmbROMIDType.SelectedIndex
            End If
        End If
        RaiseEvent DataChanged(Me, New ImagesEventArgs(eImagesDataType.BackgroundImageRomIDTypeChanged, cmbROMIDType.SelectedIndex))
    End Sub
    Private Sub ROMIDType_TextChanged(sender As Object, e As System.EventArgs) Handles cmbROMIDType.TextChanged
        If String.IsNullOrEmpty(cmbROMIDType.Text) Then
            cmbROMIDType.SelectedIndex = 0
        End If
    End Sub

    Private DoNoEvents As Boolean = False
    Private Sub SelectItem(sender As Object, ByVal doubleClick As Boolean)
        If lbImages.SelectedItem IsNot Nothing Then
            Dim item As Images.ImageInfo = TryCast(sender.SelectedItem, Images.ImageInfo)
            If item IsNot Nothing AndAlso Not IsTitleItem(item) Then
                Select Case item.Type
                    Case Images.eImageInfoType.BackgroundImage
                        DoNoEvents = True
                        cmbImageType.SelectedIndex = item.BackgroundImageType
                        txtRomID.Text = item.RomID.ToString()
                        If txtRomID.Text = "0" Then txtRomID.Text = String.Empty
                        cmbROMIDType.SelectedIndex = item.RomIDType
                        DoNoEvents = False
                        PanelImages.Visible = True
                        If doubleClick Then
                            RaiseEvent DataChanged(Me, New ImagesEventArgs(eImagesDataType.BackgroundImageSelectionChanged, item))
                        End If
                    Case Images.eImageInfoType.IlluminationImage
                        PanelImages.Visible = False
                        If doubleClick Then
                            RaiseEvent DataChanged(Me, New ImagesEventArgs(eImagesDataType.IlluminatedImageSelectionChanged, item))
                        End If
                    Case Images.eImageInfoType.DMDImage
                        PanelImages.Visible = False
                        If doubleClick Then
                            RaiseEvent DataChanged(Me, New ImagesEventArgs(eImagesDataType.DMDImageSelectionChanged, item))
                        End If
                    Case Else
                        PanelImages.Visible = False
                End Select
            Else
                PanelImages.Visible = False
            End If
        End If
    End Sub

    Private ReadOnly Property IsTitleItem(item As Images.ImageInfo) As Boolean
        Get
            Return (item.Type = Images.eImageInfoType.Title4BackgroundImages OrElse item.Type = Images.eImageInfoType.Title4IlluminationImages OrElse item.Type = Images.eImageInfoType.Title4DMDImages OrElse item.Type = Images.eImageInfoType.Title4IlluminationSnippits)
        End Get
    End Property

End Class
