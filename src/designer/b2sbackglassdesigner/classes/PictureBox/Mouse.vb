Imports System

Public Class Mouse

    Private parent As B2SPictureBox = Nothing
    Private contextMenuItems As ContextMenuStrip = Nothing

    Public Event MouseDown(ByVal sender As Object, ByVal e As System.Windows.Forms.MouseEventArgs)
    Public Event MouseUp(ByVal sender As Object, ByVal e As System.Windows.Forms.MouseEventArgs)
    Public Event MouseMove(ByVal sender As Object, ByVal e As MouseMoveEventArgs)
    Public Event CopyDMDCopyArea(ByVal sender As Object, ByVal e As System.EventArgs)
    Public Event RemoveDMDCopyArea(ByVal sender As Object, ByVal e As System.EventArgs)
    Public Event SelectedItemClicked(ByVal sender As Object, ByVal e As MouseMoveEventArgs)
    Public Event SelectedItemMoving(ByVal sender As Object, ByVal e As MouseMoveEventArgs)
    Public Event SelectedItemRemoved(ByVal sender As Object, ByVal e As EventArgs)
    Public Event SelectedBulbMoved(ByVal sender As Object, ByVal e As System.Windows.Forms.MouseEventArgs)
    Public Event SelectedBulbEdited(ByVal sender As Object, ByVal e As System.EventArgs)
    Public Class MouseMoveEventArgs
        Inherits EventArgs

        Public Enum eItemType
            Undefined = 0
            Score = 1
            Bulb = 2
        End Enum

        Public ItemType As eItemType = eItemType.Undefined
        Public Location As Point = Nothing
        Public Size As Size = Nothing

        Public Sub New(ByVal _location As Point)
            Location = _location
        End Sub
        Public Sub New(ByVal _itemtype As eItemType, ByVal _location As Point, ByVal _size As Size)
            ItemType = _itemtype
            Location = _location
            Size = _size
        End Sub
    End Class

    Private Property currentBulb() As Illumination.BulbInfo = Nothing
    Private Property currentScore() As ReelAndLED.ScoreInfo = Nothing
    Private Property currentDMDCopyArea() As InfoBase = Nothing

    ' Multi-selection is reference based so bulbs and score displays can be
    ' selected together without changing their collection order.
    Private NotInheritable Class ReferenceComparer
        Implements Generic.IEqualityComparer(Of InfoBase)

        Public Overloads Function Equals(ByVal left As InfoBase, ByVal right As InfoBase) As Boolean Implements Generic.IEqualityComparer(Of InfoBase).Equals
            Return Object.ReferenceEquals(left, right)
        End Function

        Public Overloads Function GetHashCode(ByVal item As InfoBase) As Integer Implements Generic.IEqualityComparer(Of InfoBase).GetHashCode
            Return If(item Is Nothing, 0, Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item))
        End Function
    End Class

    Private Shared ReadOnly selectionComparer As New ReferenceComparer()
    Private ReadOnly _selectedItems As New Generic.List(Of InfoBase)()
    Private ReadOnly _selectedLookup As New Generic.HashSet(Of InfoBase)(selectionComparer)
    Private ReadOnly _selectedItemsView As Generic.IList(Of InfoBase) = _selectedItems.AsReadOnly()
    Private ReadOnly groupStartLocations As New Generic.Dictionary(Of InfoBase, Point)(selectionComparer)

    Public ReadOnly Property SelectedItems As Generic.IList(Of InfoBase)
        Get
            Return _selectedItemsView
        End Get
    End Property

    Public Function IsSelected(ByVal item As InfoBase) As Boolean
        Return item IsNot Nothing AndAlso _selectedLookup.Contains(item)
    End Function

    Private Function PictureAnimationPrefix(ByVal bulb As Illumination.BulbInfo) As String
        If bulb Is Nothing OrElse String.IsNullOrEmpty(bulb.Name) OrElse
           Not bulb.Name.StartsWith("PA_", StringComparison.OrdinalIgnoreCase) Then Return String.Empty
        Dim separator As Integer = bulb.Name.LastIndexOf("_"c)
        If separator <= 2 OrElse separator = bulb.Name.Length - 1 Then Return String.Empty
        Dim frameNumber As Integer
        If Not Integer.TryParse(bulb.Name.Substring(separator + 1), frameNumber) Then Return String.Empty
        Return bulb.Name.Substring(0, separator + 1)
    End Function

    Private Function PictureAnimationGroup(ByVal primary As Illumination.BulbInfo) As Generic.List(Of InfoBase)
        Dim result As New Generic.List(Of InfoBase)()
        Dim prefix As String = PictureAnimationPrefix(primary)
        If prefix.Length = 0 OrElse bulbs Is Nothing Then Return result
        For Each candidate As Illumination.BulbInfo In bulbs
            If candidate.IsImageSnippit AndAlso candidate.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
                result.Add(candidate)
            End If
        Next
        Return result
    End Function

    Private Function IsSelectedPictureAnimationGroup(ByVal primary As Illumination.BulbInfo) As Boolean
        Dim prefix As String = PictureAnimationPrefix(primary)
        If prefix.Length = 0 OrElse _selectedItems.Count < 2 Then Return False
        For Each item As InfoBase In _selectedItems
            Dim bulb As Illumination.BulbInfo = TryCast(item, Illumination.BulbInfo)
            If bulb Is Nothing OrElse Not bulb.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then Return False
        Next
        Return True
    End Function

    Private Function IsSelectedSnippetTransform(ByVal primary As Illumination.BulbInfo) As Boolean
        If primary Is Nothing OrElse Not primary.IsImageSnippit Then Return False
        Return IsSelectedPictureAnimationGroup(primary) OrElse (_selectedItems.Count = 1 AndAlso IsSelected(primary))
    End Function

    Private Sub ResampleSelectedPictureAnimationImages()
        If currentBulb Is Nothing OrElse Not IsSelectedSnippetTransform(currentBulb) Then Return
        Dim resizeItems As Generic.IEnumerable(Of InfoBase) = If(IsSelectedPictureAnimationGroup(currentBulb), _selectedItems, New InfoBase() {currentBulb})
        For Each item As InfoBase In resizeItems
            Dim bulb As Illumination.BulbInfo = TryCast(item, Illumination.BulbInfo)
            If bulb Is Nothing OrElse bulb.Image Is Nothing OrElse bulb.Size.Width <= 0 OrElse bulb.Size.Height <= 0 Then Continue For
            If bulb.Image.Size.Equals(bulb.Size) Then Continue For
            Dim oldImage As Image = bulb.Image
            Dim optimized As Image = oldImage.Resized(bulb.Size)
            bulb.Image = optimized
            bulb.IsIlluminatedImageDirty = True
            If Backglass.currentImages IsNot Nothing Then
                Backglass.currentImages.SetNewImage(Images.eImageInfoType.IlluminationSnippits, bulb.Name, optimized)
            End If
            oldImage.Dispose()
        Next
    End Sub

    ' Resize a rotated light in its own local coordinate system. This keeps the
    ' frame, pointer and illumination field together instead of resizing the
    ' hidden unrotated X/Y edges.
    Private Sub ResizeRotatedLight(ByVal e As MouseEventArgs)
        If currentBulb Is Nothing Then Return
        Const minimumSize As Integer = 10

        Dim angle As Double = currentBulb.LightRotationAngle * Math.PI / 180.0R
        Dim cosine As Double = Math.Cos(angle)
        Dim sine As Double = Math.Sin(angle)
        Dim deltaX As Double = (e.X - currentMouseLocation.X) / factor
        Dim deltaY As Double = (e.Y - currentMouseLocation.Y) / factor
        Dim localDeltaX As Double = deltaX * cosine + deltaY * sine
        Dim localDeltaY As Double = -deltaX * sine + deltaY * cosine

        Dim left As Double = -currentStartSize.Width / 2.0R
        Dim right As Double = currentStartSize.Width / 2.0R
        Dim top As Double = -currentStartSize.Height / 2.0R
        Dim bottom As Double = currentStartSize.Height / 2.0R
        If IsMatchingLeft Then left += localDeltaX
        If IsMatchingRight Then right += localDeltaX
        If IsMatchingTop Then top += localDeltaY
        If IsMatchingBottom Then bottom += localDeltaY

        If right - left < minimumSize Then
            If IsMatchingLeft Then left = right - minimumSize Else right = left + minimumSize
        End If
        If bottom - top < minimumSize Then
            If IsMatchingTop Then top = bottom - minimumSize Else bottom = top + minimumSize
        End If

        Dim localCenterX As Double = (left + right) / 2.0R
        Dim localCenterY As Double = (top + bottom) / 2.0R
        Dim globalCenterOffsetX As Double = localCenterX * cosine - localCenterY * sine
        Dim globalCenterOffsetY As Double = localCenterX * sine + localCenterY * cosine
        Dim startCenterX As Double = currentStartLocation.X + currentStartSize.Width / 2.0R
        Dim startCenterY As Double = currentStartLocation.Y + currentStartSize.Height / 2.0R
        Dim newWidth As Integer = Math.Max(minimumSize, CInt(Math.Round(right - left)))
        Dim newHeight As Integer = Math.Max(minimumSize, CInt(Math.Round(bottom - top)))
        Dim newCenterX As Double = startCenterX + globalCenterOffsetX
        Dim newCenterY As Double = startCenterY + globalCenterOffsetY

        currentBulb.Size = New Size(newWidth, newHeight)
        currentBulb.Location = New Point(CInt(Math.Round(newCenterX - newWidth / 2.0R)),
                                         CInt(Math.Round(newCenterY - newHeight / 2.0R)))
    End Sub

    Private Sub ApplySelectedPictureAnimationRotation(ByVal angle As Single)
        If currentBulb Is Nothing OrElse Not IsSelectedSnippetTransform(currentBulb) OrElse Math.Abs(angle) < 0.05F Then Return
        Dim groupedAnimation As Boolean = IsSelectedPictureAnimationGroup(currentBulb)
        Dim center As New PointF(currentBulb.Location.X + currentBulb.Size.Width / 2.0F,
                                 currentBulb.Location.Y + currentBulb.Size.Height / 2.0F)
        If groupedAnimation Then
            ApplyGroupedPictureAnimationRotation(angle, center)
            Return
        End If

        Dim oldImage As Image = currentBulb.Image
        If oldImage Is Nothing Then Return
        Dim rotated As Bitmap = RotatedSingleSnippetFrame(oldImage, angle)
        currentBulb.Image = rotated
        currentBulb.Size = rotated.Size
        currentBulb.Location = New Point(CInt(Math.Round(center.X - rotated.Width / 2.0F)),
                                         CInt(Math.Round(center.Y - rotated.Height / 2.0F)))
        currentBulb.IsIlluminatedImageDirty = True
        If Backglass.currentImages IsNot Nothing Then
            Backglass.currentImages.SetNewImage(Images.eImageInfoType.IlluminationSnippits, currentBulb.Name, rotated)
        End If
        oldImage.Dispose()
    End Sub

    Private Sub ApplyGroupedPictureAnimationRotation(ByVal angle As Single, ByVal center As PointF)
        Dim rotatedFrames As New Generic.List(Of Generic.KeyValuePair(Of Illumination.BulbInfo, Bitmap))()
        Dim croppedFrames As New Generic.List(Of Generic.KeyValuePair(Of Illumination.BulbInfo, Bitmap))()
        Dim sharedBounds As Rectangle = Rectangle.Empty
        Dim committed As Boolean = False
        Try
            For Each item As InfoBase In _selectedItems
                Dim bulb As Illumination.BulbInfo = TryCast(item, Illumination.BulbInfo)
                If bulb Is Nothing OrElse bulb.Image Is Nothing Then Continue For
                Dim rotated As Bitmap = RotatedPictureAnimationFrame(bulb.Image, angle)
                rotatedFrames.Add(New Generic.KeyValuePair(Of Illumination.BulbInfo, Bitmap)(bulb, rotated))
                Dim occupied As Rectangle = AlphaBounds(rotated)
                If Not occupied.IsEmpty Then
                    sharedBounds = If(sharedBounds.IsEmpty, occupied, Rectangle.Union(sharedBounds, occupied))
                End If
            Next
            If rotatedFrames.Count = 0 Then Return

            Dim expandedSize As Size = rotatedFrames(0).Value.Size
            If sharedBounds.IsEmpty Then sharedBounds = New Rectangle(Point.Empty, expandedSize)
            sharedBounds.Intersect(New Rectangle(Point.Empty, expandedSize))
            If sharedBounds.Width <= 0 OrElse sharedBounds.Height <= 0 Then Return

            For Each frame As Generic.KeyValuePair(Of Illumination.BulbInfo, Bitmap) In rotatedFrames
                Dim cropped As New Bitmap(sharedBounds.Width, sharedBounds.Height, Imaging.PixelFormat.Format32bppArgb)
                Using graphics As Graphics = Graphics.FromImage(cropped)
                    graphics.Clear(Color.Transparent)
                    graphics.CompositingMode = Drawing2D.CompositingMode.SourceCopy
                    graphics.DrawImage(frame.Value,
                                       New Rectangle(0, 0, cropped.Width, cropped.Height),
                                       sharedBounds,
                                       GraphicsUnit.Pixel)
                End Using
                croppedFrames.Add(New Generic.KeyValuePair(Of Illumination.BulbInfo, Bitmap)(frame.Key, cropped))
            Next

            Dim newLocation As New Point(CInt(Math.Round(center.X - expandedSize.Width / 2.0F + sharedBounds.X)),
                                         CInt(Math.Round(center.Y - expandedSize.Height / 2.0F + sharedBounds.Y)))
            For Each frame As Generic.KeyValuePair(Of Illumination.BulbInfo, Bitmap) In croppedFrames
                Dim bulb As Illumination.BulbInfo = frame.Key
                Dim oldImage As Image = bulb.Image
                bulb.Image = frame.Value
                bulb.Size = frame.Value.Size
                bulb.Location = newLocation
                bulb.IsIlluminatedImageDirty = True
                If Backglass.currentImages IsNot Nothing Then
                    Backglass.currentImages.SetNewImage(Images.eImageInfoType.IlluminationSnippits, bulb.Name, frame.Value)
                End If
                oldImage.Dispose()
            Next
            committed = True
        Finally
            For Each frame As Generic.KeyValuePair(Of Illumination.BulbInfo, Bitmap) In rotatedFrames
                frame.Value.Dispose()
            Next
            If Not committed Then
                For Each frame As Generic.KeyValuePair(Of Illumination.BulbInfo, Bitmap) In croppedFrames
                    frame.Value.Dispose()
                Next
            End If
        End Try
    End Sub

    Private Function RotatedSingleSnippetFrame(ByVal source As Image, ByVal angle As Single) As Bitmap
        Dim expanded As Bitmap = RotatedPictureAnimationFrame(source, angle)
        Dim occupied As Rectangle = AlphaBounds(expanded)
        If occupied.Width <= 0 OrElse occupied.Height <= 0 OrElse
           (occupied.X = 0 AndAlso occupied.Y = 0 AndAlso occupied.Width = expanded.Width AndAlso occupied.Height = expanded.Height) Then Return expanded
        Dim result As New Bitmap(occupied.Width, occupied.Height, Imaging.PixelFormat.Format32bppArgb)
        Using graphics As Graphics = Graphics.FromImage(result)
            graphics.Clear(Color.Transparent)
            graphics.CompositingMode = Drawing2D.CompositingMode.SourceCopy
            graphics.DrawImage(expanded, New Rectangle(0, 0, result.Width, result.Height), occupied, GraphicsUnit.Pixel)
        End Using
        expanded.Dispose()
        Return result
    End Function

    Private Function AlphaBounds(ByVal image As Bitmap) As Rectangle
        Dim left As Integer = image.Width
        Dim top As Integer = image.Height
        Dim right As Integer = -1
        Dim bottom As Integer = -1
        Dim data As Imaging.BitmapData = image.LockBits(New Rectangle(0, 0, image.Width, image.Height), Imaging.ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppArgb)
        Try
            Dim row(Math.Abs(data.Stride) - 1) As Byte
            For y As Integer = 0 To image.Height - 1
                Runtime.InteropServices.Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length)
                For x As Integer = 0 To image.Width - 1
                    If row(x * 4 + 3) <> 0 Then
                        left = Math.Min(left, x) : right = Math.Max(right, x)
                        top = Math.Min(top, y) : bottom = Math.Max(bottom, y)
                    End If
                Next
            Next
        Finally
            image.UnlockBits(data)
        End Try
        If right < left OrElse bottom < top Then Return Rectangle.Empty
        ' Keep a small transparent safety border for bicubic edge pixels.
        left = Math.Max(0, left - 2) : top = Math.Max(0, top - 2)
        right = Math.Min(image.Width - 1, right + 2) : bottom = Math.Min(image.Height - 1, bottom + 2)
        Return Rectangle.FromLTRB(left, top, right + 1, bottom + 1)
    End Function

    Private Function RotatedPictureAnimationFrame(ByVal source As Image, ByVal angle As Single) As Bitmap
        Dim radians As Double = angle * Math.PI / 180.0R
        Dim width As Integer = Math.Max(1, CInt(Math.Ceiling(Math.Abs(source.Width * Math.Cos(radians)) + Math.Abs(source.Height * Math.Sin(radians)))))
        Dim height As Integer = Math.Max(1, CInt(Math.Ceiling(Math.Abs(source.Width * Math.Sin(radians)) + Math.Abs(source.Height * Math.Cos(radians)))))
        Dim result As New Bitmap(width, height, Imaging.PixelFormat.Format32bppArgb)
        Using graphics As Graphics = Graphics.FromImage(result)
            graphics.Clear(Color.Transparent)
            graphics.CompositingMode = Drawing2D.CompositingMode.SourceCopy
            graphics.CompositingQuality = Drawing2D.CompositingQuality.HighQuality
            graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
            graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
            graphics.SmoothingMode = Drawing2D.SmoothingMode.HighQuality
            graphics.TranslateTransform(width / 2.0F, height / 2.0F)
            graphics.RotateTransform(angle)
            graphics.DrawImage(source, -source.Width / 2.0F, -source.Height / 2.0F, source.Width, source.Height)
        End Using
        Return result
    End Function

    Public Sub SetSelection(ByVal items As Generic.IEnumerable(Of InfoBase), Optional ByVal primary As InfoBase = Nothing, Optional ByVal notify As Boolean = False)
        _selectedItems.Clear()
        _selectedLookup.Clear()
        If items IsNot Nothing Then
            For Each item As InfoBase In items
                If item IsNot Nothing AndAlso _selectedLookup.Add(item) Then _selectedItems.Add(item)
            Next
        End If
        If primary Is Nothing OrElse Not _selectedLookup.Contains(primary) Then
            primary = If(_selectedItems.Count = 0, Nothing, _selectedItems(_selectedItems.Count - 1))
        End If
        _selectedBulb = TryCast(primary, Illumination.BulbInfo)
        _selectedScore = TryCast(primary, ReelAndLED.ScoreInfo)
        If notify AndAlso primary IsNot Nothing Then RaiseSelectionChanged(primary)
    End Sub

    Private Sub ClearSelection()
        _selectedItems.Clear()
        _selectedLookup.Clear()
        _selectedBulb = Nothing
        _selectedScore = Nothing
    End Sub

    Private Sub SelectSingle(ByVal item As InfoBase)
        _selectedItems.Clear()
        _selectedLookup.Clear()
        If item IsNot Nothing Then
            _selectedItems.Add(item)
            _selectedLookup.Add(item)
        End If
        _selectedBulb = TryCast(item, Illumination.BulbInfo)
        _selectedScore = TryCast(item, ReelAndLED.ScoreInfo)
        RaiseSelectionChanged(item)
    End Sub

    Private Function ToggleSelected(ByVal item As InfoBase) As Boolean
        If item Is Nothing Then Return False
        For i As Integer = _selectedItems.Count - 1 To 0 Step -1
            If Object.ReferenceEquals(_selectedItems(i), item) Then
                _selectedItems.RemoveAt(i)
                _selectedLookup.Remove(item)
                If Object.ReferenceEquals(_selectedBulb, item) Then _selectedBulb = Nothing
                If Object.ReferenceEquals(_selectedScore, item) Then _selectedScore = Nothing
                If _selectedItems.Count > 0 Then
                    Dim primary As InfoBase = _selectedItems(_selectedItems.Count - 1)
                    _selectedBulb = TryCast(primary, Illumination.BulbInfo)
                    _selectedScore = TryCast(primary, ReelAndLED.ScoreInfo)
                    RaiseSelectionChanged(primary)
                End If
                Return False
            End If
        Next
        _selectedItems.Add(item)
        _selectedLookup.Add(item)
        _selectedBulb = TryCast(item, Illumination.BulbInfo)
        _selectedScore = TryCast(item, ReelAndLED.ScoreInfo)
        RaiseSelectionChanged(item)
        Return True
    End Function

    Private Sub RaiseSelectionChanged(ByVal item As InfoBase)
        If TypeOf item Is Illumination.BulbInfo Then
            RaiseEvent SelectedItemClicked(Me, New MouseMoveEventArgs(MouseMoveEventArgs.eItemType.Bulb, item.Location, item.Size))
        ElseIf TypeOf item Is ReelAndLED.ScoreInfo Then
            RaiseEvent SelectedItemClicked(Me, New MouseMoveEventArgs(MouseMoveEventArgs.eItemType.Score, item.Location, item.Size))
        End If
    End Sub

    Public ReadOnly Property SelectedItem() As InfoBase
        Get
            Return If(SelectedBulb IsNot Nothing, SelectedBulb, If(SelectedScore IsNot Nothing, SelectedScore, Nothing))
        End Get
    End Property
    Private _selectedScore As ReelAndLED.ScoreInfo
    Public Property SelectedScore() As ReelAndLED.ScoreInfo
        Get
            Return _selectedScore
        End Get
        Set(ByVal value As ReelAndLED.ScoreInfo)
            If value Is Nothing Then
                If _selectedScore IsNot Nothing Then
                    For i As Integer = _selectedItems.Count - 1 To 0 Step -1
                        If Object.ReferenceEquals(_selectedItems(i), _selectedScore) Then
                            _selectedLookup.Remove(_selectedScore)
                            _selectedItems.RemoveAt(i)
                        End If
                    Next
                End If
                _selectedScore = Nothing
            ElseIf _selectedScore IsNot value OrElse _selectedItems.Count <> 1 OrElse Not IsSelected(value) Then
                SelectSingle(value)
            End If
        End Set
    End Property
    Private _selectedBulb As Illumination.BulbInfo
    Public Property IsDMDCopyAreaSelected As Boolean = False

    Public Property SelectedBulb() As Illumination.BulbInfo
        Get
            Return _selectedBulb
        End Get
        Set(ByVal value As Illumination.BulbInfo)
            If value Is Nothing Then
                If _selectedBulb IsNot Nothing Then
                    For i As Integer = _selectedItems.Count - 1 To 0 Step -1
                        If Object.ReferenceEquals(_selectedItems(i), _selectedBulb) Then
                            _selectedLookup.Remove(_selectedBulb)
                            _selectedItems.RemoveAt(i)
                        End If
                    Next
                End If
                _selectedBulb = Nothing
            ElseIf _selectedBulb IsNot value OrElse _selectedItems.Count <> 1 OrElse Not IsSelected(value) Then
                SelectSingle(value)
            End If
        End Set
    End Property

    Public Property PreviewedScore() As ReelAndLED.ScoreInfo
    Public Property PreviewedBulb() As Illumination.BulbInfo

    Public Property ShowScoreFrames() As Boolean = False
    Public Property ShowIlluFrames() As Boolean = False

    Public Property SetGrillHeight() As Boolean = False
    Public Property SetSmallGrillHeight() As Boolean = False
    Public Property CopyDMDImageFromBackglass() As Boolean = False
    Public Property SetDMDLocation() As Boolean = False

    Public Property RomInfoFilter() As String

    Public Property LastScoreSize() As Size = Nothing
    Public Property LastScoreDigits() As Integer = Nothing
    Public Property LastScoreSpacing() As Integer = Nothing
    Public Property LastScoreReelType() As String = Nothing
    Public Property LastScoreReelColor() As Color = Nothing
    Public Property LastBulbSize() As Size = Nothing
    Public Property LastBulbIntensity() As Integer = Nothing
    Public Property LastBulbLightColor() As Color = Nothing
    Public Property LastBulbDodgeColor() As Color = Nothing
    Public Property LastBulbInitialState() As Integer = Nothing
    Public Property LastBulbFont() As Font = Nothing

    Private IsMatchingLeft As Boolean = False
    Private IsMatchingRight As Boolean = False
    Private IsMatchingTop As Boolean = False
    Private IsMatchingBottom As Boolean = False
    Private IsMatchingRotation As Boolean = False
    Private IsMatchingPictureAnimationRotation As Boolean = False
    Private IsMatchingLightRotation As Boolean = False
    Private pictureAnimationPendingAngle As Single = 0.0F
    Private lightRotationPendingAngle As Single = 0.0F
    Public ReadOnly Property PendingPictureAnimationAngle As Single
        Get
            Return pictureAnimationPendingAngle
        End Get
    End Property
    Public ReadOnly Property IsPictureAnimationRotationActive As Boolean
        Get
            Return IsMatchingPictureAnimationRotation AndAlso dragStarted
        End Get
    End Property
    Public ReadOnly Property PendingLightRotationAngle As Single
        Get
            Return lightRotationPendingAngle
        End Get
    End Property
    Public ReadOnly Property IsLightRotationActive As Boolean
        Get
            Return IsMatchingLightRotation AndAlso dragStarted
        End Get
    End Property
    Private IsMatchingPerspectiveDepth As Boolean = False
    Private IsMatchingPerspectiveLeft As Boolean = False
    Private IsMatchingPerspectiveRight As Boolean = False
    Private rotationStartAngle As Single = 0.0F
    Private IsMatchingX As Boolean = False
    Private IsMatchingGrillHeightX As Boolean = False
    Private IsMatchingSmallGrillHeightX As Boolean = False
    Private IsMatchingDMDLocationX As Boolean = False

    Private currentStartLocation As Point = Nothing
    Private currentStartSize As Size = Nothing
    Private currentMouseLocation As Point = Nothing
    Private dragStarted As Boolean = False

    Private moveStartLocation As Point = Nothing
    Private moveParentLocation As Point = Nothing

    Public Property factor() As Double = 1

    Public ReadOnly Property IsMouseOverGrillRemover() As Boolean
        Get
            Return IsMatchingGrillHeightX
        End Get
    End Property
    Public ReadOnly Property IsMouseOverSmallGrillRemover() As Boolean
        Get
            Return IsMatchingSmallGrillHeightX
        End Get
    End Property
    Public ReadOnly Property IsMouseOverDMDLocationRemover() As Boolean
        Get
            Return IsMatchingDMDLocationX
        End Get
    End Property

    Private ReadOnly Property scores() As ReelAndLED.ScoreCollection
        Get
            Return If(Backglass.currentData IsNot Nothing, If(Backglass.currentData.IsDMDImageShown, Backglass.currentData.DMDScores, Backglass.currentData.Scores), Nothing)
        End Get
    End Property
    Private ReadOnly Property bulbs() As Illumination.BulbCollection
        Get
            Return If(Backglass.currentData IsNot Nothing, If(Backglass.currentData.IsDMDImageShown, Backglass.currentData.DMDBulbs, Backglass.currentData.Bulbs), Nothing)
        End Get
    End Property
    Private ReadOnly Property dmdcopyimage() As InfoBase
        Get
            Return If(Backglass.currentData IsNot Nothing, If(Backglass.currentData.IsDMDImageShown, Nothing, Backglass.currentData.DMDCopyArea), Nothing)
        End Get
    End Property

    Public Sub New(ByRef _parent As B2SPictureBox)
        parent = _parent
        AddHandler parent.MouseMove, AddressOf Parent_MouseMove
        AddHandler parent.MouseDown, AddressOf Parent_MouseDown
        AddHandler parent.MouseUp, AddressOf Parent_MouseUp
        AddHandler parent.MouseDoubleClick, AddressOf Parent_MouseDoubleClick
    End Sub

    Public Sub KeyIsPressed(ByVal ctrl As Boolean, ByVal keycode As Keys)
        If keycode = Keys.Delete AndAlso IsDMDCopyAreaSelected Then
            RaiseEvent RemoveDMDCopyArea(Me, New EventArgs())
            Return
        End If
        If keycode = Keys.Delete Then
            DeleteItems(_selectedItems)
            Return
        End If
        If SelectedItem IsNot Nothing AndAlso Not LayerManager.IsLocked(SelectedItem) Then
            With SelectedItem
                Dim currentLocation As Point = .Location
                Dim currentSize As Size = .Size
                Select Case keycode
                    Case Keys.Up
                        If .Location.Y > .Size.Height / 2 * (-1) Then
                            If ctrl Then
                                .Size.Height -= 1
                            Else
                                .Location.Y -= 1
                            End If
                        End If
                    Case Keys.Down
                        If .Location.Y < Backglass.currentData.Image.Size.Height - .Size.Height / 2 Then
                            If ctrl Then
                                .Size.Height += 1
                            Else
                                .Location.Y += 1
                            End If
                        End If
                    Case Keys.Left
                        If .Location.X > .Size.Width / 2 * (-1) Then
                            If ctrl Then
                                .Size.Width -= 1
                            Else
                                .Location.X -= 1
                            End If
                        End If
                    Case Keys.Right
                        If .Location.X < Backglass.currentData.Image.Size.Width - .Size.Width / 2 Then
                            If ctrl Then
                                .Size.Width += 1
                            Else
                                .Location.X += 1
                            End If
                        End If
                    Case Else
                        If SelectedBulb IsNot Nothing Then
                            If keycode >= Keys.D1 AndAlso keycode <= Keys.D5 Then
                                SelectedBulb.Intensity = keycode - Keys.D0
                                RaiseEvent SelectedBulbEdited(Me, New EventArgs())
                            Else
                                Dim index As Integer = -1
                                Select Case keycode
                                    Case Keys.O
                                        index = 0
                                    Case Keys.R
                                        index = 1
                                    Case Keys.G
                                        index = 2
                                    Case Keys.B
                                        index = 3
                                    Case Keys.Y
                                        index = 4
                                    Case Keys.M
                                        index = 5
                                    Case Keys.P
                                        index = 6
                                    Case Keys.W
                                        index = 7
                                End Select
                                If index >= 0 Then
                                    If TranslateDodgeColor2Index(SelectedBulb.DodgeColor) = index Then SelectedBulb.DodgeColor = Nothing Else SelectedBulb.DodgeColor = TranslateIndex2DodgeColor(index)
                                    RaiseEvent SelectedBulbEdited(Me, New EventArgs())
                                End If
                            End If
                        End If
                End Select
                If keycode = Keys.Up OrElse keycode = Keys.Down OrElse keycode = Keys.Left OrElse keycode = Keys.Right Then
                    ' maybe store bulb or score settings
                    StoreCurrentSettings(SelectedItem)
                    ' undo entry
                    Undo.AddEntry(New Undo.UndoEntry(Undo.Type.BulbOrScoreMoved, SelectedItem, currentLocation, currentSize, .Location, .Size))
                    RaiseEvent SelectedItemMoving(Me, New MouseMoveEventArgs(If(SelectedBulb IsNot Nothing, MouseMoveEventArgs.eItemType.Bulb, MouseMoveEventArgs.eItemType.Score), .Location, .Size))
                End If
            End With
            parent.Refresh()
        End If
    End Sub

    Public Sub DeleteItems(ByVal items As Generic.IEnumerable(Of InfoBase))
        ' Delete one immutable snapshot. The Layers window used to change the
        ' active item and refresh itself between individual removals, collapsing
        ' the canvas multi-selection and leaving the two views out of sync.
        Dim selected As New Generic.List(Of InfoBase)()
        If items IsNot Nothing Then
            For Each item As InfoBase In items
                If item IsNot Nothing AndAlso Not selected.Any(Function(existing) Object.ReferenceEquals(existing, item)) Then
                    selected.Add(item)
                End If
            Next
        End If
        If selected.Count = 0 AndAlso SelectedItem IsNot Nothing Then selected.Add(SelectedItem)
        If selected.Count = 0 Then Return

        Dim remaining As New Generic.List(Of InfoBase)()
        Dim removedAny As Boolean = False
        Dim removedSnippet As Boolean = False
        For Each item As InfoBase In selected
            If LayerManager.IsLocked(item) Then
                remaining.Add(item)
                Continue For
            End If

            Dim bulb As Illumination.BulbInfo = TryCast(item, Illumination.BulbInfo)
            If bulb IsNot Nothing Then
                If bulb.IsImageSnippit Then
                    Backglass.currentImages.RemoveByTypeAndName(Images.eImageInfoType.IlluminationSnippits, bulb.Name)
                    removedSnippet = True
                End If
                Undo.AddEntry(New Undo.UndoEntry(Undo.Type.BulbRemoved, bulb))
                RemoveBulbByReference(bulb)
            Else
                Dim score As ReelAndLED.ScoreInfo = TryCast(item, ReelAndLED.ScoreInfo)
                If score Is Nothing Then Continue For
                Undo.AddEntry(New Undo.UndoEntry(Undo.Type.ScoreRemoved, score))
                RemoveScoreByReference(score)
            End If
            LayerManager.Forget(item)
            removedAny = True
        Next

        SetSelection(remaining)
        If removedSnippet Then B2SBackglassDesigner.formDesigner.RefreshImageInfoList()
        If removedAny Then
            RaiseEvent SelectedItemRemoved(Me, New EventArgs())
        End If
    End Sub

    Private Sub RemoveBulbByReference(ByVal target As Illumination.BulbInfo)
        If bulbs Is Nothing OrElse target Is Nothing Then Return
        For index As Integer = bulbs.Count - 1 To 0 Step -1
            If Object.ReferenceEquals(bulbs(index), target) Then
                bulbs.RemoveAt(index)
                Return
            End If
        Next
    End Sub

    Private Sub RemoveScoreByReference(ByVal target As ReelAndLED.ScoreInfo)
        If scores Is Nothing OrElse target Is Nothing Then Return
        For index As Integer = scores.Count - 1 To 0 Step -1
            If Object.ReferenceEquals(scores(index), target) Then
                scores.RemoveAt(index)
                Return
            End If
        Next
    End Sub

    Private Function SelectedScreenBounds() As Rectangle
        Dim bounds As Rectangle = Rectangle.Empty
        Dim hasBounds As Boolean = False
        Dim items As Generic.IEnumerable(Of InfoBase) = _selectedItems
        If _selectedItems.Count = 0 Then
            Dim singleItem As InfoBase = If(currentBulb IsNot Nothing, DirectCast(currentBulb, InfoBase), If(currentScore IsNot Nothing, DirectCast(currentScore, InfoBase), currentDMDCopyArea))
            If singleItem Is Nothing Then Return Rectangle.Empty
            items = New InfoBase() {singleItem}
        End If
        For Each item As InfoBase In items
            If item Is Nothing Then Continue For
            Dim itemBounds As Rectangle
            If TypeOf item Is ReelAndLED.ScoreInfo Then
                itemBounds = parent.ScoreEditorBounds(DirectCast(item, ReelAndLED.ScoreInfo), CSng(factor))
            ElseIf TypeOf item Is Illumination.BulbInfo AndAlso
                   IsSelectedSnippetTransform(DirectCast(item, Illumination.BulbInfo)) Then
                itemBounds = parent.PictureAnimationEditorBounds(DirectCast(item, Illumination.BulbInfo), CSng(factor), pictureAnimationPendingAngle)
            ElseIf TypeOf item Is Illumination.BulbInfo AndAlso IsMatchingLightRotation Then
                itemBounds = parent.LightRotationEditorBounds(DirectCast(item, Illumination.BulbInfo), CSng(factor), lightRotationPendingAngle)
            ElseIf TypeOf item Is Illumination.BulbInfo Then
                itemBounds = parent.BulbEditorBounds(DirectCast(item, Illumination.BulbInfo), CSng(factor))
            Else
                itemBounds = New Rectangle(CInt(item.Location.X * factor),
                                           CInt(item.Location.Y * factor),
                                           Math.Max(1, CInt(item.Size.Width * factor)),
                                           Math.Max(1, CInt(item.Size.Height * factor)))
            End If
            If hasBounds Then
                bounds = Rectangle.Union(bounds, itemBounds)
            Else
                bounds = itemBounds
                hasBounds = True
            End If
        Next
        Return bounds
    End Function

    Private Sub Parent_MouseMove(sender As Object, e As System.Windows.Forms.MouseEventArgs)
        Const minsize As Integer = 10
        If currentBulb Is Nothing AndAlso currentScore Is Nothing AndAlso currentDMDCopyArea Is Nothing Then
            If ShowIlluFrames OrElse ShowScoreFrames OrElse SetGrillHeight OrElse SetSmallGrillHeight OrElse CopyDMDImageFromBackglass OrElse SetDMDLocation Then
                parent.Cursor = CalcMouseLocation(e.X, e.Y)
                'ElseIf moveStartLocation <> Nothing Then
                '    Dim loc As Point = moveParentLocation - moveStartLocation + New Point(e.X, e.Y)
                '    If Not loc.Equals(parent.Location) Then
                '        parent.Location = loc
                '        'parent.Refresh()
                '    End If
            End If
        Else
            ' Never keep an object attached to the pointer after a normal click.
            ' Movement is legal only while the physical left mouse button remains down.
            If (Control.MouseButtons And MouseButtons.Left) <> MouseButtons.Left Then
                currentBulb = Nothing
                currentScore = Nothing
                currentDMDCopyArea = Nothing
                dragStarted = False
                parent.EndDragPreview()
                parent.Cursor = Cursors.Default
                RaiseEvent MouseMove(Me, New MouseMoveEventArgs(New Point(CInt(e.X / factor), CInt(e.Y / factor))))
                Return
            End If

            ' A mouse-down selects the item. Do not move or resize it until the
            ' pointer has travelled beyond the normal Windows drag threshold.
            If Not dragStarted Then
                Dim dragSize As Size = SystemInformation.DragSize
                Dim dragBounds As New Rectangle(currentMouseLocation.X - dragSize.Width \ 2,
                                                currentMouseLocation.Y - dragSize.Height \ 2,
                                                dragSize.Width,
                                                dragSize.Height)
                If dragBounds.Contains(e.Location) Then
                    RaiseEvent MouseMove(Me, New MouseMoveEventArgs(New Point(CInt(e.X / factor), CInt(e.Y / factor))))
                    Return
                End If
                dragStarted = True
                If Not IsMatchingRotation AndAlso Not IsMatchingPerspectiveDepth AndAlso
                   Not IsMatchingPerspectiveLeft AndAlso Not IsMatchingPerspectiveRight Then parent.BeginDragPreview(_selectedItems)
            End If

            Backglass.currentData.IsDirty = True
            Dim previousDragBounds As Rectangle = SelectedScreenBounds()
            Dim current As InfoBase = If(currentBulb IsNot Nothing, currentBulb, If(currentScore IsNot Nothing, currentScore, currentDMDCopyArea))
            If (IsMatchingPerspectiveLeft OrElse IsMatchingPerspectiveRight) AndAlso currentScore IsNot Nothing Then
                Dim centerX As Double = (currentScore.Location.X + currentScore.Size.Width / 2.0) * factor
                Dim centerY As Double = (currentScore.Location.Y + currentScore.Size.Height / 2.0) * factor
                Dim radians As Double = -currentScore.RotationAngle * Math.PI / 180.0
                Dim dx As Double = e.X - centerX
                Dim dy As Double = e.Y - centerY
                Dim localVertical As Double = dx * Math.Sin(radians) + dy * Math.Cos(radians)
                Dim depth As Single = Math.Max(-1.0F, Math.Min(1.0F, currentScore.PerspectiveDepth))
                Dim amount As Single = Math.Abs(depth)
                Dim nearScale As Single = 1.0F + 0.06F * amount
                Dim farScale As Single = 1.0F - 0.58F * amount
                Dim baseScale As Single = If(IsMatchingPerspectiveLeft,
                                             If(depth >= 0.0F, nearScale, farScale),
                                             If(depth >= 0.0F, farScale, nearScale))
                Dim halfHeight As Double = Math.Max(1.0, currentScore.Size.Height * factor / 2.0)
                Dim endScale As Double = Math.Max(0.2, Math.Min(2.5, -localVertical / (halfHeight * Math.Max(0.1F, baseScale))))
                If My.Computer.Keyboard.ShiftKeyDown Then endScale = Math.Round(endScale * 10.0) / 10.0
                If IsMatchingPerspectiveLeft Then
                    currentScore.PerspectiveLeftScale = CSng(endScale)
                Else
                    currentScore.PerspectiveRightScale = CSng(endScale)
                End If
                parent.Refresh()
                RaiseEvent SelectedItemMoving(sender, New MouseMoveEventArgs(MouseMoveEventArgs.eItemType.Score, currentScore.Location, currentScore.Size))
            ElseIf IsMatchingPerspectiveDepth AndAlso currentScore IsNot Nothing Then
                Dim centerX As Double = (currentScore.Location.X + currentScore.Size.Width / 2.0) * factor
                Dim centerY As Double = (currentScore.Location.Y + currentScore.Size.Height / 2.0) * factor
                Dim radians As Double = -currentScore.RotationAngle * Math.PI / 180.0
                Dim dx As Double = e.X - centerX
                Dim dy As Double = e.Y - centerY
                ' Convert pointer movement back into the unrotated score-window coordinate system.
                Dim localHorizontal As Double = dx * Math.Cos(radians) - dy * Math.Sin(radians)
                Dim travel As Double = Math.Max(30.0, currentScore.Size.Width * factor * 0.45)
                Dim depth As Double = Math.Max(-1.0, Math.Min(1.0, localHorizontal / travel))
                If My.Computer.Keyboard.ShiftKeyDown Then depth = Math.Round(depth * 10.0) / 10.0
                currentScore.PerspectiveDepth = CSng(depth)
                parent.Refresh()
                RaiseEvent SelectedItemMoving(sender, New MouseMoveEventArgs(MouseMoveEventArgs.eItemType.Score, currentScore.Location, currentScore.Size))
            ElseIf IsMatchingLightRotation AndAlso currentBulb IsNot Nothing Then
                Dim centerX As Double = (currentBulb.Location.X + currentBulb.Size.Width / 2.0) * factor
                Dim centerY As Double = (currentBulb.Location.Y + currentBulb.Size.Height / 2.0) * factor
                Dim angle As Double = Math.Atan2(e.Y - centerY, e.X - centerX) * 180.0 / Math.PI + 90.0
                If My.Computer.Keyboard.ShiftKeyDown Then angle = Math.Round(angle / 15.0) * 15.0
                Do While angle < -180.0 : angle += 360.0 : Loop
                Do While angle > 180.0 : angle -= 360.0 : Loop
                lightRotationPendingAngle = CSng(angle)
                parent.Refresh()
                RaiseEvent SelectedItemMoving(sender, New MouseMoveEventArgs(MouseMoveEventArgs.eItemType.Bulb, currentBulb.Location, currentBulb.Size))
            ElseIf IsMatchingPictureAnimationRotation AndAlso currentBulb IsNot Nothing Then
                Dim centerX As Double = (currentBulb.Location.X + currentBulb.Size.Width / 2.0) * factor
                Dim centerY As Double = (currentBulb.Location.Y + currentBulb.Size.Height / 2.0) * factor
                Dim angle As Double = Math.Atan2(e.Y - centerY, e.X - centerX) * 180.0 / Math.PI + 90.0
                If My.Computer.Keyboard.ShiftKeyDown Then angle = Math.Round(angle / 15.0) * 15.0
                Do While angle < -180.0 : angle += 360.0 : Loop
                Do While angle > 180.0 : angle -= 360.0 : Loop
                pictureAnimationPendingAngle = CSng(angle)
                RaiseEvent SelectedItemMoving(sender, New MouseMoveEventArgs(MouseMoveEventArgs.eItemType.Bulb, currentBulb.Location, currentBulb.Size))
            ElseIf IsMatchingRotation AndAlso currentScore IsNot Nothing Then
                Dim centerX As Double = (currentScore.Location.X + currentScore.Size.Width / 2.0) * factor
                Dim centerY As Double = (currentScore.Location.Y + currentScore.Size.Height / 2.0) * factor
                Dim angle As Double = Math.Atan2(e.Y - centerY, e.X - centerX) * 180.0 / Math.PI + 90.0
                If My.Computer.Keyboard.ShiftKeyDown Then angle = Math.Round(angle / 15.0) * 15.0
                Do While angle < 0 : angle += 360.0 : Loop
                Do While angle >= 360.0 : angle -= 360.0 : Loop
                currentScore.RotationAngle = CSng(angle)
                parent.Refresh()
                RaiseEvent SelectedItemMoving(sender, New MouseMoveEventArgs(MouseMoveEventArgs.eItemType.Score, currentScore.Location, currentScore.Size))
            ElseIf IsMatchingLeft OrElse IsMatchingRight OrElse IsMatchingTop OrElse IsMatchingBottom Then
                If currentBulb IsNot Nothing AndAlso Not currentBulb.IsImageSnippit AndAlso
                   Math.Abs(currentBulb.LightRotationAngle) >= 0.001F Then
                    ResizeRotatedLight(e)
                ElseIf IsMatchingLeft AndAlso IsMatchingTop Then
                    current.Location = New Point(currentStartLocation.X - (currentMouseLocation.X - e.X) / factor, currentStartLocation.Y - (currentMouseLocation.Y - e.Y) / factor)
                    current.Size = New Size(currentStartSize.Width + (currentMouseLocation.X - e.X) / factor, currentStartSize.Height + (currentMouseLocation.Y - e.Y) / factor)
                ElseIf IsMatchingLeft AndAlso IsMatchingBottom Then
                    current.Location = New Point(currentStartLocation.X - (currentMouseLocation.X - e.X) / factor, currentStartLocation.Y)
                    current.Size = New Size(currentStartSize.Width + (currentMouseLocation.X - e.X) / factor, currentStartSize.Height - (currentMouseLocation.Y - e.Y) / factor)
                ElseIf IsMatchingRight AndAlso IsMatchingTop Then
                    current.Location = New Point(currentStartLocation.X, currentStartLocation.Y - (currentMouseLocation.Y - e.Y) / factor)
                    current.Size = New Size(currentStartSize.Width - (currentMouseLocation.X - e.X) / factor, currentStartSize.Height + (currentMouseLocation.Y - e.Y) / factor)
                ElseIf IsMatchingRight AndAlso IsMatchingBottom Then
                    current.Size = New Size(currentStartSize.Width - (currentMouseLocation.X - e.X) / factor, currentStartSize.Height - (currentMouseLocation.Y - e.Y) / factor)
                ElseIf IsMatchingLeft Then
                    current.Location = New Point(currentStartLocation.X - (currentMouseLocation.X - e.X) / factor, currentStartLocation.Y)
                    current.Size = New Size(currentStartSize.Width + (currentMouseLocation.X - e.X) / factor, currentStartSize.Height)
                ElseIf IsMatchingRight Then
                    current.Size = New Size(currentStartSize.Width - (currentMouseLocation.X - e.X) / factor, currentStartSize.Height)
                ElseIf IsMatchingTop Then
                    current.Location = New Point(currentStartLocation.X, currentStartLocation.Y - (currentMouseLocation.Y - e.Y) / factor)
                    current.Size = New Size(currentStartSize.Width, currentStartSize.Height + (currentMouseLocation.Y - e.Y) / factor)
                ElseIf IsMatchingBottom Then
                    current.Size = New Size(currentStartSize.Width, currentStartSize.Height - (currentMouseLocation.Y - e.Y) / factor)
                End If
                If current.Location.X > currentStartLocation.X + currentStartSize.Width - minsize Then
                    current.Location.X = currentStartLocation.X + currentStartSize.Width - minsize
                End If
                If current.Location.Y > currentStartLocation.Y + currentStartSize.Height - minsize Then
                    current.Location.Y = currentStartLocation.Y + currentStartSize.Height - minsize
                End If
                If current.Size.Width < minsize OrElse current.Size.Height < minsize Then
                    current.Size = New Size(Math.Max(current.Size.Width, minsize), Math.Max(current.Size.Height, minsize))
                End If
                ' Imported picture-sequence frames occupy one shared rectangle.
                ' Resizing any frame therefore resizes and repositions the whole
                ' automatically selected PA_ group as one editor object.
                If currentBulb IsNot Nothing AndAlso IsSelectedPictureAnimationGroup(currentBulb) Then
                    For Each selected As InfoBase In _selectedItems
                        selected.Location = current.Location
                        selected.Size = current.Size
                    Next
                End If
                ' maybe store bulb or score size
                StoreCurrentSettings(current)
            Else
                Dim deltaX As Integer = CInt((e.X - currentMouseLocation.X) / factor)
                Dim deltaY As Integer = CInt((e.Y - currentMouseLocation.Y) / factor)
                Dim image As Image = If(Backglass.currentData.IsDMDImageShown, Backglass.currentData.DMDImage, Backglass.currentData.Image)
                If IsSelected(current) AndAlso _selectedItems.Count > 1 Then
                    For Each selected As InfoBase In _selectedItems
                        If LayerManager.IsLocked(selected) OrElse Not groupStartLocations.ContainsKey(selected) Then Continue For
                        Dim startLocation As Point = groupStartLocations(selected)
                        Dim newLocation As New Point(startLocation.X + deltaX, startLocation.Y + deltaY)
                        If newLocation.X < selected.Size.Width / 2 * (-1) Then newLocation.X = selected.Size.Width / 2 * (-1)
                        If newLocation.Y < selected.Size.Height / 2 * (-1) Then newLocation.Y = selected.Size.Height / 2 * (-1)
                        If image IsNot Nothing Then
                            If newLocation.X > image.Size.Width - selected.Size.Width / 2 Then newLocation.X = image.Size.Width - selected.Size.Width / 2
                            If newLocation.Y > image.Size.Height - selected.Size.Height / 2 Then newLocation.Y = image.Size.Height - selected.Size.Height / 2
                        End If
                        selected.Location = newLocation
                    Next
                Else
                    current.Location = New Point(currentStartLocation.X + deltaX, currentStartLocation.Y + deltaY)
                    If current.Location.X < current.Size.Width / 2 * (-1) Then current.Location.X = current.Size.Width / 2 * (-1)
                    If current.Location.Y < current.Size.Height / 2 * (-1) Then current.Location.Y = current.Size.Height / 2 * (-1)
                    If image IsNot Nothing Then
                        If current.Location.X > image.Size.Width - current.Size.Width / 2 Then current.Location.X = image.Size.Width - current.Size.Width / 2
                        If current.Location.Y > image.Size.Height - current.Size.Height / 2 Then current.Location.Y = image.Size.Height - current.Size.Height / 2
                    End If
                End If
            End If
            ' The illuminated bitmap is cached separately from the selection frame.
            ' Moving or resizing a bulb must invalidate that cache immediately so the
            ' expanded glow follows the light box during the drag.
            If currentBulb IsNot Nothing Then currentBulb.IsIlluminatedImageDirty = True
            For Each selected As InfoBase In _selectedItems
                If TypeOf selected Is Illumination.BulbInfo Then
                    DirectCast(selected, Illumination.BulbInfo).IsIlluminatedImageDirty = True
                End If
            Next

            parent.RefreshDragRegion(previousDragBounds, SelectedScreenBounds())
            RaiseEvent SelectedItemMoving(sender, New MouseMoveEventArgs(
                                          If(currentBulb Is Nothing, MouseMoveEventArgs.eItemType.Score, MouseMoveEventArgs.eItemType.Bulb),
                                          current.Location,
                                          current.Size))
        End If
        ' raise event
        RaiseEvent MouseMove(Me, New MouseMoveEventArgs(New Point(CInt(e.X / factor), CInt(e.Y / factor))))
        ' reset remove click when moving is done
        IsMatchingX = False
        IsMatchingGrillHeightX = False
        IsMatchingSmallGrillHeightX = False
        IsMatchingDMDLocationX = False
    End Sub
    Private Sub Parent_MouseDown(sender As Object, e As System.Windows.Forms.MouseEventArgs)
        If e.Button = MouseButtons.Left Then
            parent.Cursor = CalcMouseLocation(e.X, e.Y, currentBulb, currentScore, currentDMDCopyArea)
            If currentBulb IsNot Nothing AndAlso LayerManager.IsLocked(currentBulb) Then currentBulb = Nothing
            If currentScore IsNot Nothing AndAlso LayerManager.IsLocked(currentScore) Then currentScore = Nothing
            ' remember whether the DMD copy rectangle was clicked
            IsDMDCopyAreaSelected = (currentDMDCopyArea IsNot Nothing)
            ' A normal click selects one object. Ctrl+Click toggles an object in
            ' the current selection without changing layer order.
            Dim clicked As InfoBase = If(currentBulb IsNot Nothing, DirectCast(currentBulb, InfoBase), If(currentScore IsNot Nothing, DirectCast(currentScore, InfoBase), currentDMDCopyArea))
            If clicked IsNot Nothing AndAlso clicked IsNot currentDMDCopyArea Then
                If My.Computer.Keyboard.CtrlKeyDown Then
                    If Not ToggleSelected(clicked) Then
                        currentBulb = Nothing
                        currentScore = Nothing
                        clicked = Nothing
                    End If
                ElseIf Not IsSelected(clicked) Then
                    SelectSingle(clicked)
                End If
            ElseIf Not My.Computer.Keyboard.CtrlKeyDown Then
                ClearSelection()
            End If
            ' A frame created by the picture-sequence importer represents one
            ' animation object. Select all of its overlapping frames together so
            ' the existing multi-object drag path moves the full sequence.
            If currentBulb IsNot Nothing AndAlso Not My.Computer.Keyboard.CtrlKeyDown Then
                Dim animationGroup As Generic.List(Of InfoBase) = PictureAnimationGroup(currentBulb)
                If animationGroup.Count > 1 Then SetSelection(animationGroup, currentBulb, True)
            End If
            dragStarted = False
            groupStartLocations.Clear()
            For Each selected As InfoBase In _selectedItems
                groupStartLocations(selected) = selected.Location
            Next
            ' store location
            Dim current As InfoBase = If(currentBulb IsNot Nothing, currentBulb, If(currentScore IsNot Nothing, currentScore, currentDMDCopyArea))
            If current IsNot Nothing Then
                currentStartLocation = current.Location
                currentStartSize = current.Size
                If currentScore IsNot Nothing Then rotationStartAngle = currentScore.RotationAngle
                If currentBulb IsNot Nothing AndAlso Not currentBulb.IsImageSnippit Then lightRotationPendingAngle = currentBulb.LightRotationAngle
                currentMouseLocation = New Point(e.X, e.Y)
                ' SelectedBulb/SelectedScore already raises SelectedItemClicked when
                ' the selection actually changes. Do not raise it a second time here.
            Else
                moveStartLocation = New Point(e.X, e.Y)
                moveParentLocation = parent.Location
            End If
        ElseIf e.Button = MouseButtons.Right Then
            Dim itemList As Generic.List(Of InfoBase) = New Generic.List(Of InfoBase)
            Dim sorteditemList As Generic.SortedList(Of String, InfoBase) = New Generic.SortedList(Of String, InfoBase)
            parent.Cursor = CalcMouseLocation(e.X, e.Y, , , , , itemList)
            For Each item As InfoBase In itemList
                If TypeOf item Is Illumination.BulbInfo Then
                    With DirectCast(item, Illumination.BulbInfo)
                        If String.IsNullOrEmpty(.Name) Then
                            sorteditemList.Add("ZZZZZZZZ" & .ID, item)
                        Else
                            sorteditemList.Add(.Name & .ID.ToString(), item)
                        End If
                    End With
                ElseIf TypeOf item Is ReelAndLED.ScoreInfo Then
                    With DirectCast(item, ReelAndLED.ScoreInfo)
                        sorteditemList.Add(" " & .ID.ToString(), item)
                    End With
                End If
            Next
            itemList.Clear()
            If sorteditemList.Count >= 1 Then
                If contextMenuItems Is Nothing Then
                    contextMenuItems = New ContextMenuStrip()
                    AddHandler contextMenuItems.ItemClicked, AddressOf ContextMenuItems_ItemClicked
                    AddHandler contextMenuItems.MouseLeave, AddressOf ContextMenuItems_ItemsLeft
                End If
                For i As Integer = contextMenuItems.Items.Count - 1 To 0 Step -1
                    If contextMenuItems.Items(i).Image IsNot Nothing Then
                        contextMenuItems.Items(i).Image.Dispose()
                        contextMenuItems.Items(i).Image = Nothing
                    End If
                    RemoveHandler contextMenuItems.Items(i).MouseEnter, AddressOf ContextMenuItem_ItemEntered
                    RemoveHandler contextMenuItems.Items(i).MouseLeave, AddressOf ContextMenuItem_ItemLeft
                    contextMenuItems.Items(i).Dispose()
                Next
                contextMenuItems.Items.Clear()
                For Each item As KeyValuePair(Of String, InfoBase) In sorteditemList
                    If TypeOf item.Value Is Illumination.BulbInfo Then
                        With DirectCast(item.Value, Illumination.BulbInfo)
                            With contextMenuItems.Items.Add("Name='" & .Name & "', ID='" & If(Not String.IsNullOrEmpty(.RomInfo2String), .RomInfo2String, .B2SInfo2String) & If(.B2SValue > 0, "/" & .B2SValue, "") & "', text='" & .Text & "', location='" & .Location.X & ", " & .Location.Y & "', size='" & .Size.Width & "x" & .Size.Height & "'")
                                .Tag = item.Value
                                .Image = My.Resources.illumination2
                                AddHandler .MouseEnter, AddressOf ContextMenuItem_ItemEntered
                                AddHandler .MouseLeave, AddressOf ContextMenuItem_ItemLeft
                            End With
                        End With
                    ElseIf TypeOf item.Value Is ReelAndLED.ScoreInfo Then
                        With DirectCast(item.Value, ReelAndLED.ScoreInfo)
                            With contextMenuItems.Items.Add("Index='" & .ID & "', startdigit='" & .B2SStartDigit & "', location='" & .Location.X & ", " & .Location.Y & "', size='" & .Size.Width & "x" & .Size.Height & "'")
                                .Tag = item.Value
                                .Image = My.Resources.led_small
                                AddHandler .MouseEnter, AddressOf ContextMenuItem_ItemEntered
                                AddHandler .MouseLeave, AddressOf ContextMenuItem_ItemLeft
                            End With
                        End With
                    End If
                Next
                contextMenuItems.Show(Cursor.Position)
            End If
        End If
        RaiseEvent MouseDown(Me, e)
    End Sub
    Private Sub Parent_MouseUp(sender As Object, e As System.Windows.Forms.MouseEventArgs)
        ' Only commit movement, settings and undo when a real drag occurred.
        If dragStarted Then
            If currentBulb IsNot Nothing AndAlso IsSelectedPictureAnimationGroup(currentBulb) AndAlso
               (IsMatchingLeft OrElse IsMatchingRight OrElse IsMatchingTop OrElse IsMatchingBottom) Then
                For Each selected As InfoBase In _selectedItems
                    Dim oldLocation As Point = If(groupStartLocations.ContainsKey(selected), groupStartLocations(selected), currentStartLocation)
                    If Not oldLocation.Equals(selected.Location) OrElse Not currentStartSize.Equals(selected.Size) Then
                        Undo.AddEntry(New Undo.UndoEntry(Undo.Type.BulbOrScoreMoved, selected, oldLocation, currentStartSize, selected.Location, selected.Size))
                    End If
                Next
            ElseIf _selectedItems.Count > 1 AndAlso Not (IsMatchingLeft OrElse IsMatchingRight OrElse IsMatchingTop OrElse IsMatchingBottom) Then
                For Each selected As InfoBase In _selectedItems
                    If groupStartLocations.ContainsKey(selected) AndAlso Not groupStartLocations(selected).Equals(selected.Location) Then
                        Undo.AddEntry(New Undo.UndoEntry(Undo.Type.BulbOrScoreMoved, selected, groupStartLocations(selected), selected.Size, selected.Location, selected.Size))
                    End If
                Next
            ElseIf SelectedItem IsNot Nothing AndAlso
                   (Not currentStartLocation.Equals(SelectedItem.Location) OrElse Not currentStartSize.Equals(SelectedItem.Size)) Then
                Undo.AddEntry(New Undo.UndoEntry(Undo.Type.BulbOrScoreMoved, SelectedItem, currentStartLocation, currentStartSize, SelectedItem.Location, SelectedItem.Size))
            End If
        End If
        If dragStarted Then
            If currentBulb IsNot Nothing Then
                If IsMatchingLightRotation Then
                    currentBulb.LightRotationAngle = lightRotationPendingAngle
                    currentBulb.IsIlluminatedImageDirty = True
                ElseIf IsMatchingPictureAnimationRotation Then
                    ApplySelectedPictureAnimationRotation(pictureAnimationPendingAngle)
                ElseIf IsMatchingLeft OrElse IsMatchingRight OrElse IsMatchingTop OrElse IsMatchingBottom Then
                    ResampleSelectedPictureAnimationImages()
                End If
                StoreCurrentSettings(currentBulb)
                RaiseEvent SelectedBulbMoved(Me, e)
            ElseIf currentScore IsNot Nothing Then
                StoreCurrentSettings(currentScore)
            End If
        End If
        currentBulb = Nothing
        currentScore = Nothing
        currentDMDCopyArea = Nothing
        IsMatchingPerspectiveDepth = False
        IsMatchingPerspectiveLeft = False
        IsMatchingPerspectiveRight = False
        IsMatchingPictureAnimationRotation = False
        IsMatchingLightRotation = False
        pictureAnimationPendingAngle = 0.0F
        lightRotationPendingAngle = 0.0F
        dragStarted = False
        parent.Cursor = Cursors.Default
        ' Only report an edit after an actual drag/resize; selection alone is not an edit.
        ' maybe remove selected item
        parent.Cursor = CalcMouseLocation(e.X, e.Y, , , , False)
        If IsMatchingX AndAlso (SelectedItem IsNot Nothing OrElse CopyDMDImageFromBackglass) Then
            If TypeOf SelectedItem Is Illumination.BulbInfo Then
                Dim bulb As Illumination.BulbInfo = SelectedItem
                If bulb.IsImageSnippit Then
                    Backglass.currentImages.RemoveByTypeAndName(Images.eImageInfoType.IlluminationSnippits, bulb.Name)
                    B2SBackglassDesigner.formDesigner.RefreshImageInfoList()
                End If
                Undo.AddEntry(New Undo.UndoEntry(Undo.Type.BulbRemoved, SelectedItem))
                bulbs.Remove(SelectedItem)
                SelectedBulb = Nothing
                RaiseEvent SelectedItemRemoved(Me, New EventArgs())
            ElseIf TypeOf SelectedItem Is ReelAndLED.ScoreInfo Then
                Undo.AddEntry(New Undo.UndoEntry(Undo.Type.ScoreRemoved, SelectedItem))
                scores.Remove(SelectedItem)
                SelectedScore = Nothing
                RaiseEvent SelectedItemRemoved(Me, New EventArgs())
            ElseIf CopyDMDImageFromBackglass Then
                If e.Button = MouseButtons.Left Then
                    RaiseEvent CopyDMDCopyArea(Me, New EventArgs())
                End If
            End If
        End If
        moveStartLocation = Nothing
        groupStartLocations.Clear()
        dragStarted = False
        RaiseEvent MouseUp(Me, e)
        ' Dispose the frozen drag image and invalidate once so the final
        ' full-quality light/flasher replaces the lightweight drag ellipse.
        parent.EndDragPreview(True)
        parent.FlushDragPainting()
    End Sub

    Private Sub Parent_MouseDoubleClick(sender As Object, e As System.Windows.Forms.MouseEventArgs)
        If e.Button <> MouseButtons.Left Then Return

        Dim clickedBulb As Illumination.BulbInfo = Nothing
        Dim clickedScore As ReelAndLED.ScoreInfo = Nothing
        Dim clickedDMD As InfoBase = Nothing
        CalcMouseLocation(e.X, e.Y, clickedBulb, clickedScore, clickedDMD, False)

        ' A double-click on the blue center handle restores the complete score
        ' transform to a normal rectangular display. A single drag on the same
        ' handle retains its existing left/right depth behavior.
        If clickedScore IsNot Nothing AndAlso IsMatchingPerspectiveDepth AndAlso
           Not LayerManager.IsLocked(clickedScore) Then
            SelectSingle(clickedScore)
            ResetScoreTransform(clickedScore)
            Return
        End If

        If clickedBulb Is Nothing OrElse LayerManager.IsLocked(clickedBulb) Then Return

        ' Keep the normal canvas behavior intact, but make a double-click a
        ' shortcut to the matching settings panel for the exact object.
        SelectSingle(clickedBulb)
        parent.Invalidate()
        parent.BeginInvoke(New MethodInvoker(Sub() OpenBulbSettingsShortcut(clickedBulb)))
    End Sub

    Private Sub ResetScoreTransform(ByVal score As ReelAndLED.ScoreInfo)
        If score Is Nothing Then Return
        score.RotationAngle = 0.0F
        score.PerspectiveDepth = 0.0F
        score.PerspectiveLeftScale = 1.0F
        score.PerspectiveRightScale = 1.0F
        If Backglass.currentData IsNot Nothing Then Backglass.currentData.IsDirty = True
        parent.Refresh()
        RaiseEvent SelectedItemMoving(Me, New MouseMoveEventArgs(MouseMoveEventArgs.eItemType.Score, score.Location, score.Size))
    End Sub

    Private Sub OpenBulbSettingsShortcut(ByVal bulb As Illumination.BulbInfo)
        If bulb Is Nothing OrElse B2SBackglassDesigner.formDesigner Is Nothing Then Return

        ' The object has already been selected by Parent_MouseDoubleClick.
        ' Call the Designer's real settings path directly instead of searching
        ' for buttons or tool-window captions.
        B2SBackglassDesigner.formDesigner.OpenSelectedObjectSettings()
    End Sub

    Private Function ClickMatchingControl(ByVal root As Control,
                                          ByVal preferredTexts() As String,
                                          ByVal preferredNames() As String) As Boolean
        For Each wanted As String In preferredTexts
            For Each child As Control In AllChildControls(root)
                If String.Equals(child.Text.Trim(), wanted, StringComparison.OrdinalIgnoreCase) Then
                    If PerformControlClick(child) Then Return True
                End If
            Next
        Next
        For Each wanted As String In preferredNames
            For Each child As Control In AllChildControls(root)
                If child.Name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0 Then
                    If PerformControlClick(child) Then Return True
                End If
            Next
        Next
        Return False
    End Function

    Private Function AllChildControls(ByVal root As Control) As Generic.List(Of Control)
        Dim result As New Generic.List(Of Control)()
        For Each child As Control In root.Controls
            result.Add(child)
            result.AddRange(AllChildControls(child))
        Next
        Return result
    End Function

    Private Function PerformControlClick(ByVal control As Control) As Boolean
        If Not control.Enabled Then Return False
        Dim clickable As IButtonControl = TryCast(control, IButtonControl)
        If clickable IsNot Nothing Then
            clickable.PerformClick()
            Return True
        End If
        Dim clickMethod = control.GetType().GetMethod("PerformClick", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.Public)
        If clickMethod IsNot Nothing Then
            clickMethod.Invoke(control, Nothing)
            Return True
        End If
        Return False
    End Function

    Private Function ClickMatchingToolStripItem(ByVal root As Control,
                                                ByVal preferredTexts() As String,
                                                ByVal preferredNames() As String) As Boolean
        For Each child As Control In AllChildControls(root)
            If Not TypeOf child Is ToolStrip Then Continue For
            Dim strip As ToolStrip = DirectCast(child, ToolStrip)
            If ClickMatchingToolStripItems(strip.Items, preferredTexts, preferredNames) Then Return True
        Next
        Return False
    End Function

    Private Function ClickMatchingToolStripItems(ByVal items As ToolStripItemCollection,
                                                 ByVal preferredTexts() As String,
                                                 ByVal preferredNames() As String) As Boolean
        For Each wanted As String In preferredTexts
            For Each item As ToolStripItem In items
                If String.Equals(item.Text.Replace("&", "").Trim(), wanted, StringComparison.OrdinalIgnoreCase) AndAlso item.Enabled Then
                    item.PerformClick()
                    Return True
                End If
            Next
        Next
        For Each wanted As String In preferredNames
            For Each item As ToolStripItem In items
                If item.Name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0 AndAlso item.Enabled Then
                    item.PerformClick()
                    Return True
                End If
                Dim dropDown As ToolStripDropDownItem = TryCast(item, ToolStripDropDownItem)
                If dropDown IsNot Nothing AndAlso ClickMatchingToolStripItems(dropDown.DropDownItems, preferredTexts, preferredNames) Then Return True
            Next
        Next
        Return False
    End Function


    Private Sub ContextMenuItems_ItemClicked(ByVal sender As Object, ByVal e As System.Windows.Forms.ToolStripItemClickedEventArgs)
        If e.ClickedItem.Tag IsNot Nothing Then
            If TypeOf e.ClickedItem.Tag Is Illumination.BulbInfo Then
                SelectedBulb = e.ClickedItem.Tag
                bulbs.Remove(SelectedBulb)
                bulbs.Insert(0, SelectedBulb)
            ElseIf TypeOf e.ClickedItem.Tag Is ReelAndLED.ScoreInfo Then
                SelectedScore = e.ClickedItem.Tag
                scores.Remove(SelectedScore)
                scores.Insert(0, SelectedScore)
            End If
        End If
        parent.Invalidate()
    End Sub
    Private Sub ContextMenuItems_ItemsLeft(ByVal sender As Object, ByVal e As EventArgs)
        PreviewedBulb = Nothing
        PreviewedScore = Nothing
        parent.Invalidate()
    End Sub
    Private Sub ContextMenuItem_ItemEntered(ByVal sender As Object, ByVal e As EventArgs)
        PreviewedBulb = Nothing
        PreviewedScore = Nothing
        If TypeOf sender.tag Is Illumination.BulbInfo Then
            PreviewedBulb = sender.tag
        ElseIf TypeOf sender.tag Is ReelAndLED.ScoreInfo Then
            PreviewedScore = sender.tag
        End If
        parent.Invalidate()
    End Sub
    Private Sub ContextMenuItem_ItemLeft(ByVal sender As Object, ByVal e As EventArgs)
        PreviewedBulb = Nothing
        PreviewedScore = Nothing
        parent.Invalidate()
    End Sub

    Private Function CalcMouseLocation(ByVal x As Integer,
                                       ByVal y As Integer,
                                       Optional ByRef currentBulbWithMouseOver As Illumination.BulbInfo = Nothing,
                                       Optional ByRef currentScoreWithMouseOver As ReelAndLED.ScoreInfo = Nothing,
                                       Optional ByRef currentDMDCopyAreaWithMouseOver As InfoBase = Nothing,
                                       Optional ByVal lookforMatchingX As Boolean = True,
                                       Optional ByRef allBulbsAndScores As Generic.List(Of InfoBase) = Nothing) As Cursor
        Dim look4secondone As Integer = If(My.Computer.Keyboard.CtrlKeyDown AndAlso My.Computer.Keyboard.ShiftKeyDown, 2, 1)
        Dim startwithbulbs As Boolean = (SelectedScore Is Nothing)
        Dim ret As Cursor = Cursors.Default
        IsMatchingLeft = False
        IsMatchingRight = False
        IsMatchingTop = False
        IsMatchingBottom = False
        IsMatchingRotation = False
        IsMatchingPictureAnimationRotation = False
        IsMatchingLightRotation = False
        IsMatchingPerspectiveDepth = False
        IsMatchingPerspectiveLeft = False
        IsMatchingPerspectiveRight = False
        If lookforMatchingX Then
            IsMatchingX = False
            IsMatchingGrillHeightX = False
            IsMatchingSmallGrillHeightX = False
            IsMatchingDMDLocationX = False
        End If
        currentBulbWithMouseOver = Nothing
        currentScoreWithMouseOver = Nothing
        currentDMDCopyAreaWithMouseOver = Nothing
        ' Enhanced 3.0.5 Stage 4: convert screen coordinates once per event.
        ' The original code repeated these divisions for every bulb and score.
        Dim designX As Single = CSng(x / factor)
        Dim designY As Single = CSng(y / factor)
        If SelectedScore IsNot Nothing AndAlso ShowScoreFrames AndAlso LayerManager.IsVisible(SelectedScore) Then
            Dim perspectivePoints As PointF() = parent.ScorePerspectivePoints(SelectedScore, CSng(factor))
            Dim perspectiveRadius As Double = 9.0
            If (x - perspectivePoints(0).X) * (x - perspectivePoints(0).X) +
               (y - perspectivePoints(0).Y) * (y - perspectivePoints(0).Y) <= perspectiveRadius * perspectiveRadius Then
                IsMatchingPerspectiveLeft = True
                currentScoreWithMouseOver = SelectedScore
                Return Cursors.SizeNS
            End If
            If (x - perspectivePoints(1).X) * (x - perspectivePoints(1).X) +
               (y - perspectivePoints(1).Y) * (y - perspectivePoints(1).Y) <= perspectiveRadius * perspectiveRadius Then
                IsMatchingPerspectiveRight = True
                currentScoreWithMouseOver = SelectedScore
                Return Cursors.SizeNS
            End If
            Dim centerX As Double = SelectedScore.Location.X + SelectedScore.Size.Width / 2.0
            Dim centerY As Double = SelectedScore.Location.Y + SelectedScore.Size.Height / 2.0
            ' The center handle controls left/right depth turning.
            Dim depthRadius As Double = 10.0 / factor
            If (designX - centerX) * (designX - centerX) + (designY - centerY) * (designY - centerY) <= depthRadius * depthRadius Then
                IsMatchingPerspectiveDepth = True
                currentScoreWithMouseOver = SelectedScore
                Return Cursors.SizeWE
            End If
            centerX = SelectedScore.Location.X + SelectedScore.Size.Width / 2.0
            centerY = SelectedScore.Location.Y + SelectedScore.Size.Height / 2.0
            Dim radians As Double = SelectedScore.RotationAngle * Math.PI / 180.0
            Dim localY As Double = -(SelectedScore.Size.Height / 2.0 + 24.0 / factor)
            Dim handleX As Double = centerX - localY * Math.Sin(radians)
            Dim handleY As Double = centerY + localY * Math.Cos(radians)
            Dim radius As Double = 9.0 / factor
            If (designX - handleX) * (designX - handleX) + (designY - handleY) * (designY - handleY) <= radius * radius Then
                IsMatchingRotation = True
                currentScoreWithMouseOver = SelectedScore
                Return Cursors.Hand
            End If
        End If
        If SelectedBulb IsNot Nothing AndAlso ShowIlluFrames AndAlso LayerManager.IsVisible(SelectedBulb) AndAlso
           IsSelectedSnippetTransform(SelectedBulb) Then
            Dim centerX As Double = (SelectedBulb.Location.X + SelectedBulb.Size.Width / 2.0) * factor
            Dim handleX As Double = centerX
            Dim handleY As Double = SelectedBulb.Location.Y * factor - 24.0
            Const radius As Double = 9.0
            If (x - handleX) * (x - handleX) + (y - handleY) * (y - handleY) <= radius * radius Then
                IsMatchingPictureAnimationRotation = True
                currentBulbWithMouseOver = SelectedBulb
                Return Cursors.Hand
            End If
        End If
        If SelectedBulb IsNot Nothing AndAlso ShowIlluFrames AndAlso LayerManager.IsVisible(SelectedBulb) AndAlso
           Not SelectedBulb.IsImageSnippit AndAlso _selectedItems.Count = 1 Then
            Dim centerX As Double = (SelectedBulb.Location.X + SelectedBulb.Size.Width / 2.0) * factor
            Dim centerY As Double = (SelectedBulb.Location.Y + SelectedBulb.Size.Height / 2.0) * factor
            Dim handleRadius As Double = SelectedBulb.Size.Height * factor / 2.0 + 24.0
            Dim radians As Double = SelectedBulb.LightRotationAngle * Math.PI / 180.0
            Dim handleX As Double = centerX + Math.Sin(radians) * handleRadius
            Dim handleY As Double = centerY - Math.Cos(radians) * handleRadius
            Const radius As Double = 9.0
            If (x - handleX) * (x - handleX) + (y - handleY) * (y - handleY) <= radius * radius Then
                IsMatchingLightRotation = True
                currentBulbWithMouseOver = SelectedBulb
                Return Cursors.Hand
            End If
        End If
        For i As Integer = 0 To 1
            If look4secondone > 0 Then
                currentScoreWithMouseOver = Nothing
            End If
            If (i = 0 AndAlso startwithbulbs) OrElse (i = 1 AndAlso Not startwithbulbs) Then
                If ShowIlluFrames AndAlso (currentScoreWithMouseOver Is Nothing OrElse allBulbsAndScores IsNot Nothing) Then
                    For Each bulb As Illumination.BulbInfo In bulbs
                        ret = CalcItem(bulb, designX, designY, currentBulbWithMouseOver, lookforMatchingX)
                        If ret <> Cursors.Default Then
                            If allBulbsAndScores IsNot Nothing Then
                                allBulbsAndScores.Add(bulb)
                            End If
                            look4secondone -= 1
                            If look4secondone <= 0 AndAlso allBulbsAndScores Is Nothing Then Exit For
                        End If
                    Next
                End If
            End If
            If look4secondone > 0 Then
                currentBulbWithMouseOver = Nothing
            End If
            If (i = 0 AndAlso Not startwithbulbs) OrElse (i = 1 AndAlso startwithbulbs) Then
                If ShowScoreFrames AndAlso (currentBulbWithMouseOver Is Nothing OrElse allBulbsAndScores IsNot Nothing) Then
                    For Each score As ReelAndLED.ScoreInfo In scores
                        ret = CalcItem(score, designX, designY, currentScoreWithMouseOver)
                        If ret <> Cursors.Default Then
                            If allBulbsAndScores IsNot Nothing Then
                                allBulbsAndScores.Add(score)
                            End If
                            look4secondone -= 1
                            If look4secondone <= 0 AndAlso allBulbsAndScores Is Nothing Then Exit For
                        End If
                    Next
                End If
            End If
        Next
        ' look for DMD image copy frame
        If ret = Cursors.Default AndAlso CopyDMDImageFromBackglass Then
            ret = CalcItem(dmdcopyimage, designX, designY, currentDMDCopyAreaWithMouseOver, lookforMatchingX)
        End If
        ' look for grills and DMD
        If ret = Cursors.Default Then
            ret = CalcItem(Nothing, designX, designY, Nothing)
        End If
        ' get out
        Return ret
    End Function
    Private Function CalcItem(ByVal item As InfoBase,
                              ByVal x As Single,
                              ByVal y As Single,
                              ByRef currentWithMouseOver As InfoBase,
                              Optional ByVal lookforMatchingX As Boolean = True) As Cursor
        Const thick As Integer = 6
        Dim ret As Cursor = Cursors.Default
        If item IsNot Nothing Then
            ' Objects hidden from the Layers panel must also be absent from
            ' canvas hit-testing. Otherwise an invisible snippet can sit above
            ' a visible light, capture the click, and open Snippet Properties.
            If Not LayerManager.IsVisible(item) Then Return Cursors.Default
            With item
                If TypeOf item Is ReelAndLED.ScoreInfo Then
                    Dim score As ReelAndLED.ScoreInfo = DirectCast(item, ReelAndLED.ScoreInfo)
                    If Math.Abs(score.RotationAngle) > 0.001F Then
                        Dim cx As Double = .Location.X + .Size.Width / 2.0
                        Dim cy As Double = .Location.Y + .Size.Height / 2.0
                        Dim radians As Double = -score.RotationAngle * Math.PI / 180.0
                        Dim dx As Double = x - cx
                        Dim dy As Double = y - cy
                        x = CSng(cx + dx * Math.Cos(radians) - dy * Math.Sin(radians))
                        y = CSng(cy + dx * Math.Sin(radians) + dy * Math.Cos(radians))
                    End If
                ElseIf TypeOf item Is Illumination.BulbInfo Then
                    Dim bulb As Illumination.BulbInfo = DirectCast(item, Illumination.BulbInfo)
                    If Not bulb.IsImageSnippit AndAlso Math.Abs(bulb.LightRotationAngle) > 0.001F Then
                        Dim cx As Double = .Location.X + .Size.Width / 2.0R
                        Dim cy As Double = .Location.Y + .Size.Height / 2.0R
                        Dim radians As Double = -bulb.LightRotationAngle * Math.PI / 180.0R
                        Dim dx As Double = x - cx
                        Dim dy As Double = y - cy
                        x = CSng(cx + dx * Math.Cos(radians) - dy * Math.Sin(radians))
                        y = CSng(cy + dx * Math.Sin(radians) + dy * Math.Cos(radians))
                    End If
                End If
                ' Enhanced 3.0.5 Stage 4: most objects are nowhere near the
                ' pointer. Reject them before ROM filtering and resize tests.
                If x < .Location.X OrElse x > .Location.X + .Size.Width OrElse
                   y < .Location.Y OrElse y > .Location.Y + .Size.Height Then
                    Return Cursors.Default
                End If
                Dim getin As Boolean = True
                If TypeOf item Is Illumination.BulbInfo Then
                    getin = (String.IsNullOrEmpty(RomInfoFilter) OrElse
                             RomInfoFilter.Equals(.B2SInfo2String) OrElse
                             RomInfoFilter.Equals(.RomInfo2String) OrElse
                             (RomInfoFilter.Equals("withoutid") AndAlso ((Backglass.currentData.CommType = eCommType.B2S AndAlso String.IsNullOrEmpty(.B2SInfo2String)) OrElse (Backglass.currentData.CommType = eCommType.Rom AndAlso String.IsNullOrEmpty(.RomInfo2String)))))
                    If Not getin Then
                        With DirectCast(item, Illumination.BulbInfo)
                            getin = (RomInfoFilter.Equals("off") AndAlso .InitialState = 0) OrElse
                                    (RomInfoFilter.Equals("on") AndAlso .InitialState = 1) OrElse
                                    (RomInfoFilter.Equals("alwayson") AndAlso .InitialState = 2) OrElse
                                    (RomInfoFilter.Equals("authentic") AndAlso .DualMode <> eDualMode.Fantasy) OrElse
                                    (RomInfoFilter.Equals("fantasy") AndAlso .DualMode <> eDualMode.Authentic) OrElse
                                    (RomInfoFilter.Equals("withname") AndAlso Not String.IsNullOrEmpty(.Name))
                        End With
                    End If
                End If
                If getin Then
                    If lookforMatchingX Then
                        Dim isSmallRect As Boolean = (item.Size.Width * factor < 25 OrElse item.Size.Height * factor < 25)
                        If x >= .Location.X + .Size.Width - If(isSmallRect, 11, 15) / factor AndAlso x <= .Location.X + .Size.Width - 5 / factor AndAlso y >= .Location.Y + 5 / factor AndAlso y <= .Location.Y + If(isSmallRect, 11, 15) / factor Then
                            If (SelectedItem IsNot Nothing AndAlso .Equals(SelectedItem)) OrElse CopyDMDImageFromBackglass Then
                                IsMatchingX = True
                                If isSmallRect Then IsMatchingX = False
                            End If
                        End If
                    End If
                    Dim allowResize As Boolean = Not TypeOf item Is Illumination.BulbInfo
                    If TypeOf item Is Illumination.BulbInfo Then
                        Dim resizeBulb As Illumination.BulbInfo = DirectCast(item, Illumination.BulbInfo)
                        allowResize = True
                    End If
                    If allowResize Then
                        IsMatchingLeft = False
                        IsMatchingRight = False
                        IsMatchingTop = False
                        IsMatchingBottom = False
                        If x >= .Location.X AndAlso x <= .Location.X + .Size.Width Then
                            IsMatchingTop = (y >= .Location.Y AndAlso y <= .Location.Y + thick)
                            IsMatchingBottom = (y >= .Location.Y + .Size.Height - thick AndAlso y <= .Location.Y + .Size.Height)
                        End If
                        If y >= .Location.Y AndAlso y <= .Location.Y + .Size.Height Then
                            IsMatchingLeft = (x >= .Location.X AndAlso x <= .Location.X + thick)
                            IsMatchingRight = (x >= .Location.X + .Size.Width - thick AndAlso x <= .Location.X + .Size.Width)
                        End If
                        If IsMatchingLeft AndAlso IsMatchingTop Then
                            ret = Cursors.SizeNWSE
                        ElseIf IsMatchingLeft AndAlso IsMatchingBottom Then
                            ret = Cursors.SizeNESW
                        ElseIf IsMatchingRight AndAlso IsMatchingTop Then
                            ret = Cursors.SizeNESW
                        ElseIf IsMatchingRight AndAlso IsMatchingBottom Then
                            ret = Cursors.SizeNWSE
                        ElseIf IsMatchingLeft OrElse IsMatchingRight Then
                            ret = Cursors.SizeWE
                        ElseIf IsMatchingTop OrElse IsMatchingBottom Then
                            ret = Cursors.SizeNS
                        End If
                    End If
                    If x >= .Location.X AndAlso x <= .Location.X + .Size.Width AndAlso y >= .Location.Y AndAlso y <= .Location.Y + .Size.Height Then
                        currentWithMouseOver = item
                        If ret = Cursors.Default Then ret = Cursors.Hand
                    End If
                End If
            End With
        Else
            If parent IsNot Nothing Then
                Dim screenX As Single = x * factor
                Dim screenY As Single = y * factor
                Dim grillX As Integer = parent.Width - 15
                Dim grillY As Integer = (Backglass.currentData.Image.Height - Backglass.currentData.GrillHeight) * factor - 15
                Dim smallgrillY As Integer = (Backglass.currentData.Image.Height - Backglass.currentData.SmallGrillHeight) * factor - 15
                If (SetGrillHeight OrElse SetSmallGrillHeight) Then
                    ' grills
                    If screenX >= grillX AndAlso screenX <= grillX + 12 AndAlso screenY >= grillY AndAlso screenY <= grillY + 12 Then
                        IsMatchingGrillHeightX = True
                        ret = Cursors.Hand
                    ElseIf screenX >= grillX AndAlso screenX <= grillX + 12 AndAlso screenY >= smallgrillY AndAlso screenY <= smallgrillY + 12 Then
                        IsMatchingSmallGrillHeightX = True
                        ret = Cursors.Hand
                    End If
                ElseIf SetDMDLocation Then
                    ' DMD
                    If Backglass.currentData.DMDImage IsNot Nothing Then
                        Dim dmdsize As Size = New Size(Backglass.currentData.DMDImage.Width * factor, Backglass.currentData.DMDImage.Height * factor)
                        If Backglass.currentData.DMDDefaultLocation <> Nothing Then
                            Dim dmdloc As Point = New Point(Backglass.currentData.DMDDefaultLocation.X * factor, Backglass.currentData.DMDDefaultLocation.Y * factor)
                            grillX = dmdloc.X + dmdsize.Width + 5
                            grillY = dmdloc.Y
                            If screenX >= grillX AndAlso screenX <= grillX + 12 AndAlso screenY >= grillY AndAlso screenY <= grillY + 12 Then
                                IsMatchingDMDLocationX = True
                                ret = Cursors.Hand
                            End If
                        End If
                    End If
                End If
            End If
        End If
        Return ret
    End Function

    'Private Function CreateMouseMoveEventArgs(current As InfoBase) As MouseMoveEventArgs
    '    Return New MouseMoveEventArgs(If(currentBulb Is Nothing, MouseMoveEventArgs.eItemType.Score, MouseMoveEventArgs.eItemType.Bulb),
    '                                  current.Location,
    '                                  current.Size)
    'End Function

    Private Sub StoreCurrentSettings(ByVal current As InfoBase)
        ' maybe store bulb or score settings
        If TypeOf current Is Illumination.BulbInfo Then
            With DirectCast(current, Illumination.BulbInfo)
                LastBulbSize = .Size
                LastBulbIntensity = .Intensity
                LastBulbInitialState = .InitialState
                LastBulbLightColor = .LightColor
                LastBulbDodgeColor = .DodgeColor
                LastBulbFont = If(Not String.IsNullOrEmpty(.FontName), New Font(.FontName, .FontSize, .FontStyle), Nothing)
            End With
        ElseIf TypeOf current Is ReelAndLED.ScoreInfo Then
            With DirectCast(current, ReelAndLED.ScoreInfo)
                LastScoreSize = .Size
                LastScoreDigits = .Digits
                LastScoreSpacing = .Spacing
                LastScoreReelType = .ReelType
                LastScoreReelColor = .ReelColor
                ' maybe set dirty flag for a reel recalculation
                .IsSingleReelSizeDirty = True
            End With
        End If
    End Sub

End Class
