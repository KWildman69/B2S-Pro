Imports System.Text
Imports System.Linq

Public Class Backglass

    Public Shared Property currentData() As Data = Nothing
    Public Shared Property currentTabPage() As B2STabPage = Nothing

    Public Shared ReadOnly Property currentBulbs() As Illumination.BulbCollection
        Get
            Return If(currentData IsNot Nothing, If(currentData.IsDMDImageShown, currentData.DMDBulbs, currentData.Bulbs), Nothing)
        End Get
    End Property
    Public Shared ReadOnly Property currentScores() As ReelAndLED.ScoreCollection
        Get
            Return If(currentData IsNot Nothing, If(currentData.IsDMDImageShown, currentData.DMDScores, currentData.Scores), Nothing)
        End Get
    End Property
    Public Shared ReadOnly Property currentAnimations() As Animation.AnimationHeaderCollection
        Get
            Return If(currentData IsNot Nothing, currentData.Animations, Nothing)
            'Return If(currentData IsNot Nothing, If(currentData.IsDMDImageShown, currentData.DMDAnimations, currentData.Animations), Nothing)
        End Get
    End Property
    Public Shared ReadOnly Property currentImages() As Images.ImageCollection
        Get
            Return If(currentData IsNot Nothing, If(currentData.IsDMDImageShown, currentData.Images, currentData.Images), Nothing)
        End Get
    End Property

    Public Shared ReadOnly Property currentUsedIDs() As Generic.SortedList(Of String, Integer)
        Get
            Dim ret As Generic.SortedList(Of String, Integer) = New Generic.SortedList(Of String, Integer)
            If currentData IsNot Nothing Then
                If currentData.CommType = eCommType.Rom Then
                    For Each bulb As Illumination.BulbInfo In currentBulbs
                        If bulb.RomID > 0 AndAlso bulb.RomIDType > eRomIDType.NotUsed Then
                            If Not ret.ContainsKey(bulb.RomInfo2String) Then
                                ret.Add(bulb.RomInfo2String, 1)
                            Else
                                ret(bulb.RomInfo2String) += 1
                            End If
                        End If
                    Next
                ElseIf currentData.CommType = eCommType.B2S Then
                    For Each bulb As Illumination.BulbInfo In currentBulbs
                        If bulb.B2SID > 0 Then
                            If Not ret.ContainsKey(bulb.B2SID.ToString()) Then
                                ret.Add(bulb.B2SID.ToString(), 1)
                            Else
                                ret(bulb.B2SID.ToString()) += 1
                            End If
                        End If
                    Next
                End If
            End If
            Return ret
        End Get
    End Property

    Public Class Data

        Public Sub New()
            ProjectGUID = Guid.NewGuid.ToString().ToUpper()
            ProjectGUID2 = Guid.NewGuid.ToString().ToUpper()
            AssemblyGUID = Guid.NewGuid.ToString().ToUpper()
        End Sub

        Public Property ProjectGUID() As String = String.Empty
        Public Property ProjectGUID2() As String = String.Empty
        Public Property AssemblyGUID() As String = String.Empty

        ' Enhanced 2.6.4: one full-backglass transmission mask shared by every light.
        ' PNG data is stored as Base64 in the project. White passes light, black blocks it.
        Public Property GlobalIlluminationMaskData() As String = String.Empty
        Public Property GlobalIlluminationMaskSourceData() As String = String.Empty
        Public Property GlobalIlluminationMaskEnabled() As Boolean = False
        Public Property GlobalIlluminationMaskInverted() As Boolean = False
        Public Property GlobalIlluminationMaskThreshold() As Integer = 128

        Private _name As String = String.Empty
        Private _vsname As String = String.Empty
        Public Property Name() As String
            Get
                Return _name
            End Get
            Set(ByVal value As String)
                If Not _name.Equals(value.Trim) Then
                    _name = value.Trim
                    IsDirty = True
                End If
            End Set
        End Property
        Public Property LoadedName() As String = String.Empty
        ' Runtime-only identity of the file this tab was loaded from. This is not
        ' written into B2S/directB2S XML; it only lets the editor distinguish two
        ' same-named backglasses that came from different folders.
        Public Property SourceFilePath() As String = String.Empty
        ' Runtime-only path of the exact automatic-recovery file that produced
        ' this document. Its filename may contain a recovery identity different
        ' from the new Data instance created while loading it.
        Public Property RecoverySourceFilePath() As String = String.Empty
        Public Property VSName() As String
            Get
                Return _vsname
            End Get
            Set(ByVal value As String)
                If Not _vsname.Equals(value) Then
                    _vsname = value
                    IsDirty = True
                End If
            End Set
        End Property
        Public ReadOnly Property SecuredName() As String
            Get
                Return Secured(_name)
            End Get
        End Property

        Private _dualbackglass As Boolean = False
        Public Property DualBackglass() As Boolean
            Get
                Return _dualbackglass
            End Get
            Set(ByVal value As Boolean)
                If Not _dualbackglass.Equals(value) Then
                    _dualbackglass = value
                    IsDirty = True
                End If
            End Set
        End Property

        Private _author As String = String.Empty
        Public Property Author() As String
            Get
                Return _author
            End Get
            Set(ByVal value As String)
                If Not _author.Equals(value) Then
                    _author = value
                    IsDirty = True
                    If value IsNot Nothing Then
                        LatestAuthor = value
                        B2SBackglassDesigner.formDesigner.SaveSettings()
                    End If
                End If
            End Set
        End Property
        Private _artwork As String = String.Empty
        Public Property Artwork() As String
            Get
                Return _artwork
            End Get
            Set(ByVal value As String)
                If Not _artwork.Equals(value) Then
                    _artwork = value
                    IsDirty = True
                End If
            End Set
        End Property

        Private _gamename As String = String.Empty
        Public Property GameName() As String
            Get
                Return _gamename
            End Get
            Set(ByVal value As String)
                If Not _gamename.Equals(value) Then
                    _gamename = value
                    IsDirty = True
                End If
            End Set
        End Property

        Public Property BackupName() As String = String.Empty
        Public ReadOnly Property IsBackup() As Boolean
            Get
                Return Not String.IsNullOrEmpty(BackupName)
            End Get
        End Property

        Private _tabletype As eTableType = eTableType.NotDefined
        Public Property TableType() As eTableType
            Get
                Return _tabletype
            End Get
            Set(ByVal value As eTableType)
                If Not _tabletype.Equals(value) Then
                    _tabletype = value
                    IsDirty = True
                End If
            End Set
        End Property

        Private _addemdefaults As Boolean
        Public Property AddEMDefaults() As Boolean
            Get
                Return _addemdefaults
            End Get
            Set(ByVal value As Boolean)
                If Not _addemdefaults.Equals(value) Then
                    _addemdefaults = value
                    IsDirty = True
                End If
            End Set
        End Property

        Private _dmdtype As eDMDType = eDMDType.NotDefined
        Public Property DMDType() As eDMDType
            Get
                Return _dmdtype
            End Get
            Set(ByVal value As eDMDType)
                If Not _dmdtype.Equals(value) Then
                    _dmdtype = value
                    IsDirty = True
                End If
            End Set
        End Property

        Private _commtype As eCommType = eCommType.NotDefined
        Public Property CommType() As eCommType
            Get
                Return _commtype
            End Get
            Set(ByVal value As eCommType)
                If Not _commtype.Equals(value) Then
                    _commtype = value
                    IsDirty = True
                End If
            End Set
        End Property

        Private _desttype As eDestType = eDestType.NotDefined
        Public Property DestType() As eDestType
            Get
                Return _desttype
            End Get
            Set(ByVal value As eDestType)
                If Not _desttype.Equals(value) Then
                    _desttype = value
                    IsDirty = True
                End If
            End Set
        End Property

        Private _reeltype As String = String.Empty
        Public Property ReelType() As String
            Get
                Return _reeltype
            End Get
            Set(ByVal value As String)
                If Not _reeltype.Equals(value) Then
                    _reeltype = value
                    IsDirty = True
                End If
            End Set
        End Property
        Private _reelcolor As Color = Nothing
        Public Property ReelColor() As Color
            Get
                Return _reelcolor
            End Get
            Set(ByVal value As Color)
                If Not _reelcolor.Equals(value) Then
                    _reelcolor = value
                    IsDirty = True
                End If
            End Set
        End Property

        Private _image As Image = Nothing
        Private _imagefilename As String = String.Empty
        Public Property IsSavedImageDirty() As Boolean = False
        Public Property Image() As Image
            Get
                Return _image
            End Get
            Set(ByVal value As Image)
                If _image IsNot value Then
                    _image = value
                    IsDirty = True
                End If
            End Set
        End Property
        Public Property ImageFileName() As String
            Get
                Return _imagefilename
            End Get
            Set(ByVal value As String)
                If Not _imagefilename.Equals(value) Then
                    _imagefilename = value
                    IsDirty = True
                End If
            End Set
        End Property

        Private _dmdimage As Image = Nothing
        Private _dmdimagefilename As String = String.Empty
        Public Property IsSavedDMDImageDirty() As Boolean = False
        Public Property DMDImage() As Image
            Get
                Return _dmdimage
            End Get
            Set(ByVal value As Image)
                If _dmdimage IsNot value Then
                    _dmdimage = value
                    IsDirty = True
                End If
            End Set
        End Property
        Public Property DMDImageFileName() As String
            Get
                Return _dmdimagefilename
            End Get
            Set(ByVal value As String)
                If Not _dmdimagefilename.Equals(value) Then
                    _dmdimagefilename = value
                    IsDirty = True
                End If
            End Set
        End Property

        Public Property ThumbnailImage() As Image

        Private _importedImages As Image()

        Public Property AddedGrillOriginalHeight As Integer = 0
        Private _GrillHeight As Integer = 0
        Public Property GrillHeight() As Integer
            Get
                Return _GrillHeight
            End Get
            Set(ByVal value As Integer)
                If _GrillHeight <> value Then
                    _GrillHeight = value
                    IsDirty = True
                End If
            End Set
        End Property
        Private _SmallGrillHeight As Integer = 0
        Public Property SmallGrillHeight() As Integer
            Get
                Return _SmallGrillHeight
            End Get
            Set(ByVal value As Integer)
                If _SmallGrillHeight <> value Then
                    _SmallGrillHeight = value
                    IsDirty = True
                End If
            End Set
        End Property

        Public DMDCopyArea As InfoBase = New InfoBase
        Private _DMDDefaultLocation As Point = Nothing
        Public Property DMDDefaultLocation() As Point
            Get
                Return _DMDDefaultLocation
            End Get
            Set(ByVal value As Point)
                If _DMDDefaultLocation <> value Then
                    _DMDDefaultLocation = value
                    IsDirty = True
                End If
            End Set
        End Property

        ' B2S Pro: the <Displays> element of an imported backglass file (DMD positions saved
        ' by the VPX B2S plugin), kept unchanged so the .B2SPro export carries it over.
        Public Property DisplaysXml As String = String.Empty

        Private _NumberOfPlayers As Integer = 0
        Public Property NumberOfPlayers() As Integer
            Get
                Return _NumberOfPlayers
            End Get
            Set(ByVal value As Integer)
                If _NumberOfPlayers <> value Then
                    _NumberOfPlayers = value
                    IsDirty = True
                End If
            End Set
        End Property

        Private _B2SDataCount As Integer = 0
        Public Property B2SDataCount() As Integer
            Get
                Return _B2SDataCount
            End Get
            Set(ByVal value As Integer)
                If _B2SDataCount <> value Then
                    _B2SDataCount = value
                    IsDirty = True
                End If
            End Set
        End Property

        Private _UseDream7LEDs As Boolean = True
        Public Property UseDream7LEDs() As Boolean
            Get
                Return _UseDream7LEDs
            End Get
            Set(ByVal value As Boolean)
                If _UseDream7LEDs <> value Then
                    _UseDream7LEDs = value
                    IsDirty = True
                End If
            End Set
        End Property
        Private _D7Glow As Single = 0
        Public Property D7Glow() As Single
            Get
                Return _D7Glow
            End Get
            Set(ByVal value As Single)
                If _D7Glow <> value Then
                    _D7Glow = value
                    IsDirty = True
                End If
            End Set
        End Property
        Private _D7Thickness As Single = 0
        Public Property D7Thickness() As Single
            Get
                Return _D7Thickness
            End Get
            Set(ByVal value As Single)
                If _D7Thickness <> value Then
                    _D7Thickness = value
                    IsDirty = True
                End If
            End Set
        End Property
        Private _D7Shear As Single = 0
        Public Property D7Shear() As Single
            Get
                Return _D7Shear
            End Get
            Set(ByVal value As Single)
                If _D7Shear <> value Then
                    _D7Shear = value
                    IsDirty = True
                End If
            End Set
        End Property

        Private _ReelRollingDirection As eReelRollingDirection = eReelRollingDirection.Up
        Public Property ReelRollingDirection() As eReelRollingDirection
            Get
                Return _ReelRollingDirection
            End Get
            Set(ByVal value As eReelRollingDirection)
                If _ReelRollingDirection <> value Then
                    _ReelRollingDirection = value
                    IsDirty = True
                End If
            End Set
        End Property

        Private _ReelRollingInterval As Integer = 0
        Public Property ReelRollingInterval() As Integer
            Get
                Return _ReelRollingInterval
            End Get
            Set(ByVal value As Integer)
                If _ReelRollingInterval <> value Then
                    _ReelRollingInterval = value
                    IsDirty = True
                End If
            End Set
        End Property
        Private _ReelIntermediateImageCount As Integer = 0
        Public Property ReelIntermediateImageCount() As Integer
            Get
                Return _ReelIntermediateImageCount
            End Get
            Set(ByVal value As Integer)
                If _ReelIntermediateImageCount <> value Then
                    _ReelIntermediateImageCount = value
                    IsDirty = True
                End If
            End Set
        End Property

        Public Property Scores() As ReelAndLED.ScoreCollection = New ReelAndLED.ScoreCollection()
        Public Property DMDScores() As ReelAndLED.ScoreCollection = New ReelAndLED.ScoreCollection()

        Public Property Bulbs() As Illumination.BulbCollection = New Illumination.BulbCollection()
        Public Property DMDBulbs() As Illumination.BulbCollection = New Illumination.BulbCollection()

        ' Number of backglass visual layers below the fixed canvas card.  Every
        ' light, flasher, snippet, reel and LED participates in the same exact
        ' front-to-back deck; the canvas is only a fixed divider in that deck.
        Public Property CanvasLayerPosition() As Integer = 0
        Public Property UnifiedLayerDeck() As Boolean = True

        ' The DMD has its own deck and its own canvas divider. Zero (the default)
        ' keeps every DMD item in front of the DMD image.
        Public Property DMDCanvasLayerPosition() As Integer = 0

        Public Sub NormalizeCanvasLayerPosition()
            Dim visualCount As Integer = If(Bulbs Is Nothing, 0, Bulbs.Count) + If(Scores Is Nothing, 0, Scores.Count)
            CanvasLayerPosition = Math.Max(0, Math.Min(visualCount, CanvasLayerPosition))
            Dim dmdVisualCount As Integer = If(DMDBulbs Is Nothing, 0, DMDBulbs.Count) + If(DMDScores Is Nothing, 0, DMDScores.Count)
            DMDCanvasLayerPosition = Math.Max(0, Math.Min(dmdVisualCount, DMDCanvasLayerPosition))
        End Sub

        ' Canvas divider of the backglass deck or of the DMD deck.
        Public Function CanvasLayerPositionFor(ByVal isDMD As Boolean) As Integer
            Return If(isDMD, DMDCanvasLayerPosition, CanvasLayerPosition)
        End Function

        Public Sub SetCanvasLayerPositionFor(ByVal isDMD As Boolean, ByVal value As Integer)
            If isDMD Then
                DMDCanvasLayerPosition = Math.Max(0, value)
            Else
                CanvasLayerPosition = Math.Max(0, value)
            End If
            NormalizeCanvasLayerPosition()
        End Sub

        Public Sub EnsureUnifiedLayerDeck()
            EnsureUnifiedLayerDeck(Bulbs, Scores, True)
            EnsureUnifiedLayerDeck(DMDBulbs, DMDScores, False)
            UnifiedLayerDeck = True
            NormalizeCanvasLayerPosition()
        End Sub

        Private Sub EnsureUnifiedLayerDeck(ByVal bulbs As Illumination.BulbCollection,
                                           ByVal scores As ReelAndLED.ScoreCollection,
                                           ByVal hasCanvas As Boolean)
            Dim items As New List(Of Object)()
            If bulbs IsNot Nothing Then
                For Each bulb As Illumination.BulbInfo In bulbs
                    items.Add(bulb)
                Next
            End If
            If scores IsNot Nothing Then
                For Each score As ReelAndLED.ScoreInfo In scores
                    items.Add(score)
                Next
            End If
            If items.Count = 0 Then Return

            Dim validOrders As New Generic.HashSet(Of Integer)()
            Dim complete As Boolean = True
            For Each item As Object In items
                Dim order As Integer = VisualLayerOrder(item)
                If order < 0 OrElse Not validOrders.Add(order) Then complete = False
            Next

            Dim ordered As New List(Of Object)()
            If complete Then
                ordered.AddRange(items.OrderBy(Function(item) VisualLayerOrder(item)))
            ElseIf items.All(Function(item) VisualLayerOrder(item) < 0) Then
                ' One-time migration of a pre-unified project.  Preserve the
                ' exact visible arrangement: scores were always in front and
                ' CanvasLayerPosition counted only the final bulb entries.
                items.Sort(Function(left As Object, right As Object)
                               Dim result As Integer = VisualZ(right).CompareTo(VisualZ(left))
                               If result <> 0 Then Return result
                               Return LegacyStableVisualIndex(left, bulbs, scores).CompareTo(LegacyStableVisualIndex(right, bulbs, scores))
                           End Function)
                If hasCanvas AndAlso bulbs IsNot Nothing Then
                    Dim below As New Generic.HashSet(Of Illumination.BulbInfo)()
                    Dim legacyBelowCount As Integer = Math.Max(0, Math.Min(bulbs.Count, CanvasLayerPosition))
                    For index As Integer = bulbs.Count - legacyBelowCount To bulbs.Count - 1
                        If index >= 0 Then below.Add(bulbs(index))
                    Next
                    ordered.AddRange(items.Where(Function(item)
                                                     Dim bulb As Illumination.BulbInfo = TryCast(item, Illumination.BulbInfo)
                                                     Return bulb Is Nothing OrElse Not below.Contains(bulb)
                                                 End Function))
                    ordered.AddRange(items.Where(Function(item)
                                                     Dim bulb As Illumination.BulbInfo = TryCast(item, Illumination.BulbInfo)
                                                     Return bulb IsNot Nothing AndAlso below.Contains(bulb)
                                                 End Function))
                Else
                    ordered.AddRange(items)
                End If
            Else
                ' New objects have no order yet.  Put only those new objects at
                ' the front and retain every established deck relationship.
                ordered.AddRange(items.Where(Function(item) VisualLayerOrder(item) < 0).
                                       OrderByDescending(Function(item) VisualZ(item)).
                                       ThenBy(Function(item) LegacyStableVisualIndex(item, bulbs, scores)))
                ordered.AddRange(items.Where(Function(item) VisualLayerOrder(item) >= 0).
                                       OrderBy(Function(item) VisualLayerOrder(item)).
                                       ThenBy(Function(item) LegacyStableVisualIndex(item, bulbs, scores)))
            End If

            For index As Integer = 0 To ordered.Count - 1
                SetVisualLayerOrder(ordered(index), index)
            Next
        End Sub

        Private Shared Function VisualLayerOrder(ByVal item As Object) As Integer
            If TypeOf item Is Illumination.BulbInfo Then Return DirectCast(item, Illumination.BulbInfo).LayerOrder
            Return DirectCast(item, ReelAndLED.ScoreInfo).LayerOrder
        End Function

        Private Shared Sub SetVisualLayerOrder(ByVal item As Object, ByVal value As Integer)
            If TypeOf item Is Illumination.BulbInfo Then
                DirectCast(item, Illumination.BulbInfo).LayerOrder = value
            Else
                DirectCast(item, ReelAndLED.ScoreInfo).LayerOrder = value
            End If
        End Sub

        Private Shared Function VisualZ(ByVal item As Object) As Integer
            If TypeOf item Is Illumination.BulbInfo Then Return DirectCast(item, Illumination.BulbInfo).ZOrder
            Return DirectCast(item, ReelAndLED.ScoreInfo).ZOrder
        End Function

        Private Shared Function LegacyStableVisualIndex(ByVal item As Object,
                                                        ByVal bulbs As Illumination.BulbCollection,
                                                        ByVal scores As ReelAndLED.ScoreCollection) As Integer
            If TypeOf item Is Illumination.BulbInfo Then Return If(bulbs Is Nothing, Integer.MaxValue, bulbs.IndexOf(DirectCast(item, Illumination.BulbInfo)))
            Return 1000000 + If(scores Is Nothing, Integer.MaxValue, scores.IndexOf(DirectCast(item, ReelAndLED.ScoreInfo)))
        End Function

        Public Property Animations() As Animation.AnimationHeaderCollection = New Animation.AnimationHeaderCollection()
        'Public Property DMDAnimations() As Animation.AnimationHeaderCollection = New Animation.AnimationHeaderCollection()

        Public Property Images() As Images.ImageCollection = New Images.ImageCollection()

        Private _isDirty As Boolean = False
        Private _changeVersion As Long = 0
        ' Session-only revision of the last verified recovery snapshot.
        Public Property RecoveryChangeVersion As Long = -1

        Public Property IsDirty() As Boolean
            Get
                Return _isDirty
            End Get
            Set(ByVal value As Boolean)
                _isDirty = value
                If value Then _changeVersion += 1
            End Set
        End Property

        ' Monotonic design revision used to tell whether a generated DirectB2S
        ' file still represents the live editor data. Unlike IsDirty, this also
        ' distinguishes edits made after an export while the project remains
        ' unsaved (and therefore remains dirty).
        Public ReadOnly Property ChangeVersion() As Long
            Get
                Return _changeVersion
            End Get
        End Property


        ' no need to save this stuff

        Public Property IsDMDImageShown As Boolean = False

        Public Property Zoom() As Integer = 100
        Public Property ShowScoreFrames As Boolean = False
        Public Property ShowScoring As Boolean = False
        Public Property ShowIlluFrames As Boolean = False
        Public Property ShowIllumination As Boolean = False

        Public ReadOnly Property IsExternalIlluminationImageSelected() As Boolean
            Get
                Return (FirstSelectedExternalIlluminationImage() IsNot Nothing)
            End Get
        End Property
        Public ReadOnly Property FirstSelectedExternalIlluminationImage() As Image
            Get
                Dim ret As Image = Nothing
                For Each item As Images.ImageInfo In Images
                    If item.Type = B2SBackglassDesigner.Images.eImageInfoType.IlluminationImage Then 'AndAlso item.Selected Then
                        ret = item.Image
                        Exit For
                    End If
                Next
                Return ret
            End Get
        End Property

        Public ReadOnly Property IsDMDImageAvailable() As Boolean
            Get
                Return (FirstDMDImage() IsNot Nothing)
            End Get
        End Property
        Public ReadOnly Property FirstDMDImage() As Image
            Get
                Dim ret As Image = Nothing
                For Each item As Images.ImageInfo In Images
                    If item.Type = B2SBackglassDesigner.Images.eImageInfoType.DMDImage Then
                        ret = item.Image
                        Exit For
                    End If
                Next
                Return ret
            End Get
        End Property

        Public ReadOnly Property IsResourceFileNeeded() As Boolean
            Get
                Dim ret As Boolean = False
                For Each rtype As String In ReelType.Split(",")
                    If Not String.IsNullOrEmpty(rtype) AndAlso Not IsReelImageRendered(rtype) Then
                        ret = True
                        Exit For
                    End If
                Next
                Return ret
            End Get
        End Property
        Public ReadOnly Property ResourceImages() As Generic.Dictionary(Of String, Image)
            Get
                Dim ret As Generic.Dictionary(Of String, Image) = New Generic.Dictionary(Of String, Image)
                For Each rtype As String In ReelType.Split(",")
                    If Not String.IsNullOrEmpty(rtype) AndAlso rtype.Length > 2 Then
                        Dim twodigits As Boolean = IsNumeric(rtype.Substring(rtype.Length - 2, 1))
                        Dim name As String = rtype.Substring(0, rtype.Length - 1 - If(twodigits, 1, 0))
                        For i As Integer = 0 To 9 + If(twodigits, 90, 0)
                            Dim number As String = i.ToString
                            If twodigits AndAlso number.Length = 1 Then number = "0" & number
                            Dim image As Image = My.Resources.ResourceManager.GetObject(name & number)
                            If image IsNot Nothing Then
                                ret.Add(name & number, image)
                            Else
                                Exit For
                            End If
                        Next
                        Dim emptyimage As Image = My.Resources.ResourceManager.GetObject(name & "Empty")
                        If emptyimage IsNot Nothing Then
                            ret.Add(name & "Empty", emptyimage)
                        End If
                    End If
                Next
                Return ret
            End Get
        End Property

    End Class

End Class
