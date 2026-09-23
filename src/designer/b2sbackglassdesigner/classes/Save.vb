Imports System
Imports System.Text
Imports System.IO
Imports System.Linq

Public Class Save

    Inherits HelperBase

    Private Const SaveVersion As String = "1.27"

    Public Function LoadData(ByRef _backglassData As Backglass.Data,
                             ByVal XML As Xml.XmlDocument,
                             Optional ByVal defaultArtworkPixelLighting As Boolean = False) As Boolean
        If XML IsNot Nothing AndAlso XML.SelectSingleNode("B2SBackglassData") IsNot Nothing Then
            Dim version As String = XML.SelectSingleNode("B2SBackglassData").Attributes("Version").InnerText
            Dim topnode As Xml.XmlElement = XML.SelectNodes("B2SBackglassData")(0)
            _backglassData = New Backglass.Data()
            Dim myanimations As Animation.AnimationHeaderCollection = New Animation.AnimationHeaderCollection()
            _backglassData.Animations = myanimations
            Dim myscores As ReelAndLED.ScoreCollection = New ReelAndLED.ScoreCollection()
            _backglassData.Scores = myscores
            Dim myscorestackorders As New Generic.Dictionary(Of ReelAndLED.ScoreInfo, Integer)()
            Dim mydmdscores As ReelAndLED.ScoreCollection = New ReelAndLED.ScoreCollection()
            _backglassData.DMDScores = mydmdscores
            Dim mydmdscorestackorders As New Generic.Dictionary(Of ReelAndLED.ScoreInfo, Integer)()
            Dim mybulbs As Illumination.BulbCollection = New Illumination.BulbCollection()
            _backglassData.Bulbs = mybulbs
            Dim mybulbstackorders As New Generic.Dictionary(Of Illumination.BulbInfo, Integer)()
            Dim mydmdbulbs As Illumination.BulbCollection = New Illumination.BulbCollection()
            _backglassData.DMDBulbs = mydmdbulbs
            Dim mydmdbulbstackorders As New Generic.Dictionary(Of Illumination.BulbInfo, Integer)()
            Dim myimages As Images.ImageCollection = New Images.ImageCollection()
            _backglassData.Images = myimages
            With _backglassData
                If topnode.SelectSingleNode("BackupName") IsNot Nothing Then
                    .BackupName = topnode.SelectSingleNode("BackupName").Attributes("Value").InnerText
                End If
                .ProjectGUID = topnode.SelectSingleNode("ProjectGUID").Attributes("Value").InnerText
                .ProjectGUID2 = topnode.SelectSingleNode("ProjectGUID2").Attributes("Value").InnerText
                Dim globalMaskNode As Xml.XmlNode = topnode.SelectSingleNode("GlobalIlluminationMask")
                If globalMaskNode IsNot Nothing Then
                    If globalMaskNode.Attributes("Data") IsNot Nothing Then .GlobalIlluminationMaskData = globalMaskNode.Attributes("Data").InnerText
                    If globalMaskNode.Attributes("SourceData") IsNot Nothing Then .GlobalIlluminationMaskSourceData = globalMaskNode.Attributes("SourceData").InnerText
                    If globalMaskNode.Attributes("Enabled") IsNot Nothing Then .GlobalIlluminationMaskEnabled = (globalMaskNode.Attributes("Enabled").InnerText <> "0")
                    If globalMaskNode.Attributes("Inverted") IsNot Nothing Then .GlobalIlluminationMaskInverted = (globalMaskNode.Attributes("Inverted").InnerText = "1")
                    If globalMaskNode.Attributes("Threshold") IsNot Nothing Then
                        Dim thresholdValue As Integer
                        If Integer.TryParse(globalMaskNode.Attributes("Threshold").InnerText, thresholdValue) Then .GlobalIlluminationMaskThreshold = Math.Max(0, Math.Min(255, thresholdValue))
                    End If
                End If
                .AssemblyGUID = topnode.SelectSingleNode("AssemblyGUID").Attributes("Value").InnerText
                .Name = topnode.SelectSingleNode("Name").Attributes("Value").InnerText
                .LoadedName = .Name
                If topnode.SelectSingleNode("VSName") IsNot Nothing Then
                    .VSName = topnode.SelectSingleNode("VSName").Attributes("Value").InnerText
                End If
                If topnode.SelectSingleNode("DualBackglass") IsNot Nothing Then
                    .DualBackglass = (topnode.SelectSingleNode("DualBackglass").Attributes("Value").InnerText = "1")
                End If
                If topnode.SelectSingleNode("Author") IsNot Nothing Then
                    .Author = topnode.SelectSingleNode("Author").Attributes("Value").InnerText
                End If
                If topnode.SelectSingleNode("Artwork") IsNot Nothing Then
                    .Artwork = topnode.SelectSingleNode("Artwork").Attributes("Value").InnerText
                End If
                If topnode.SelectSingleNode("GameName") IsNot Nothing Then
                    .GameName = topnode.SelectSingleNode("GameName").Attributes("Value").InnerText
                End If
                .TableType = CInt(topnode.SelectSingleNode("TableType").Attributes("Value").InnerText)
                If topnode.SelectSingleNode("AddEMDefaults") IsNot Nothing Then
                    .AddEMDefaults = (topnode.SelectSingleNode("AddEMDefaults").Attributes("Value").InnerText = "1")
                End If
                If topnode.SelectSingleNode("DMDType") IsNot Nothing Then
                    .DMDType = CInt(topnode.SelectSingleNode("DMDType").Attributes("Value").InnerText)
                End If
                If topnode.SelectSingleNode("CommType") IsNot Nothing Then
                    .CommType = CInt(topnode.SelectSingleNode("CommType").Attributes("Value").InnerText)
                End If
                If topnode.SelectSingleNode("DestType") IsNot Nothing Then
                    .DestType = CInt(topnode.SelectSingleNode("DestType").Attributes("Value").InnerText)
                ElseIf .CommType = eCommType.Rom Then
                    .DestType = eDestType.DirectB2S
                End If
                .NumberOfPlayers = CInt(topnode.SelectSingleNode("NumberOfPlayers").Attributes("Value").InnerText)
                If topnode.SelectSingleNode("B2SDataCount") IsNot Nothing Then
                    .B2SDataCount = CInt(topnode.SelectSingleNode("B2SDataCount").Attributes("Value").InnerText)
                End If
                If topnode.SelectSingleNode("ReelType") IsNot Nothing Then
                    .ReelType = topnode.SelectSingleNode("ReelType").Attributes("Value").InnerText
                End If
                If topnode.SelectSingleNode("UseDream7LEDs") IsNot Nothing Then
                    .UseDream7LEDs = (topnode.SelectSingleNode("UseDream7LEDs").Attributes("Value").InnerText = "1")
                End If
                If topnode.SelectSingleNode("D7Glow") IsNot Nothing Then
                    .D7Glow = CSng(topnode.SelectSingleNode("D7Glow").Attributes("Value").InnerText) / 100
                    .D7Thickness = CSng(topnode.SelectSingleNode("D7Thickness").Attributes("Value").InnerText) / 100
                    .D7Shear = CSng(topnode.SelectSingleNode("D7Shear").Attributes("Value").InnerText) / 100
                End If
                If topnode.SelectSingleNode("ReelColor") IsNot Nothing Then
                    Try
                        .ReelColor = String2Color(topnode.SelectSingleNode("ReelColor").Attributes("Value").InnerText.Replace(";", "."))
                    Catch
                        .ReelColor = Color.OrangeRed
                    End Try
                End If
                If topnode.SelectSingleNode("ReelRollingDirection") IsNot Nothing Then
                    .ReelRollingDirection = CInt(topnode.SelectSingleNode("ReelRollingDirection").Attributes("Value").InnerText)
                End If
                If topnode.SelectSingleNode("ReelRollingInterval") IsNot Nothing Then
                    .ReelRollingInterval = CInt(topnode.SelectSingleNode("ReelRollingInterval").Attributes("Value").InnerText)
                End If
                If topnode.SelectSingleNode("ReelIntermediateImageCount") IsNot Nothing Then
                    .ReelIntermediateImageCount = CInt(topnode.SelectSingleNode("ReelIntermediateImageCount").Attributes("Value").InnerText)
                End If
                .GrillHeight = CInt(topnode.SelectSingleNode("GrillHeight").Attributes("Value").InnerText)
                If topnode.SelectSingleNode("GrillHeight").Attributes("Small") IsNot Nothing Then
                    .SmallGrillHeight = CInt(topnode.SelectSingleNode("GrillHeight").Attributes("Small").InnerText)
                End If
                .DMDDefaultLocation = New Point(CInt(topnode.SelectSingleNode("DMDDefaultLocationX").Attributes("Value").InnerText), CInt(topnode.SelectSingleNode("DMDDefaultLocationY").Attributes("Value").InnerText))
                If topnode.SelectSingleNode("DMDCopyAreaX") IsNot Nothing Then
                    .DMDCopyArea.Location = New Point(CInt(topnode.SelectSingleNode("DMDCopyAreaX").Attributes("Value").InnerText), CInt(topnode.SelectSingleNode("DMDCopyAreaY").Attributes("Value").InnerText))
                    .DMDCopyArea.Size = New Size(CInt(topnode.SelectSingleNode("DMDCopyAreaWidth").Attributes("Value").InnerText), CInt(topnode.SelectSingleNode("DMDCopyAreaHeight").Attributes("Value").InnerText))
                End If

                ' get all animations
                If topnode.SelectSingleNode("Animations") IsNot Nothing AndAlso topnode.SelectNodes("Animations/Animation") IsNot Nothing Then
                    For Each innerNode As Xml.XmlElement In topnode.SelectNodes("Animations/Animation")
                        Dim ani As Animation.AnimationHeader = New Animation.AnimationHeader()
                        ani.Name = innerNode.Attributes("Name").InnerText
                        If innerNode.Attributes("DualMode") IsNot Nothing Then
                            ani.DualMode = CInt(innerNode.Attributes("DualMode").InnerText)
                        End If
                        ani.Interval = CInt(innerNode.Attributes("Interval").InnerText)
                        ani.Loops = CInt(innerNode.Attributes("Loops").InnerText)
                        If innerNode.Attributes("B2SJoin") IsNot Nothing Then
                            ani.IDJoin = innerNode.Attributes("B2SJoin").InnerText
                        Else
                            ani.IDJoin = innerNode.Attributes("IDJoin").InnerText
                        End If
                        If ani.IDJoin = "0" Then ani.IDJoin = String.Empty
                        ani.StartAnimationAtBackglassStartup = (innerNode.Attributes("StartAnimationAtBackglassStartup").InnerText = "1")
                        If innerNode.Attributes("LightsStateAtAnimationStart") IsNot Nothing Then
                            ani.LightsStateAtAnimationStart = CInt(innerNode.Attributes("LightsStateAtAnimationStart").InnerText)
                        Else
                            ani.LightsStateAtAnimationStart = If((innerNode.Attributes("AllLightsOffAtAnimationStart").InnerText = "1"), Animation.AnimationHeader.eLightsStateAtAnimationStart.LightsOff, Animation.AnimationHeader.eLightsStateAtAnimationStart.NoChange)
                        End If
                        If innerNode.Attributes("LightsStateAtAnimationEnd") IsNot Nothing Then
                            ani.LightsStateAtAnimationEnd = CInt(innerNode.Attributes("LightsStateAtAnimationEnd").InnerText)
                        ElseIf innerNode.Attributes("ResetLightsAtAnimationEnd") IsNot Nothing Then
                            ani.LightsStateAtAnimationEnd = If((innerNode.Attributes("ResetLightsAtAnimationEnd").InnerText = "1"), Animation.AnimationHeader.eLightsStateAtAnimationEnd.LightsReseted, Animation.AnimationHeader.eLightsStateAtAnimationEnd.Undefined)
                        End If
                        If innerNode.Attributes("RunAnimationTilEnd") IsNot Nothing Then
                            ani.AnimationStopBehaviour = If((innerNode.Attributes("RunAnimationTilEnd").InnerText = "1"), Animation.AnimationHeader.eAnimationStopBehaviour.RunAnimationTillEnd, Animation.AnimationHeader.eAnimationStopBehaviour.StopImmediatelly)
                        ElseIf innerNode.Attributes("AnimationStopBehaviour") IsNot Nothing Then
                            ani.AnimationStopBehaviour = CInt(innerNode.Attributes("AnimationStopBehaviour").InnerText)
                        End If
                        ani.LockInvolvedLamps = (innerNode.Attributes("LockInvolvedLamps").InnerText = "1")
                        If innerNode.Attributes("HideScoreDisplays") IsNot Nothing Then
                            ani.HideScoreDisplays = (innerNode.Attributes("HideScoreDisplays").InnerText = "1")
                        End If
                        If innerNode.Attributes("BringToFront") IsNot Nothing Then
                            ani.BringToFront = (innerNode.Attributes("BringToFront").InnerText = "1")
                        End If
                        If innerNode.Attributes("RandomStart") IsNot Nothing Then
                            ani.RandomStart = (innerNode.Attributes("RandomStart").InnerText = "1")
                            ani.RandomQuality = CInt(innerNode.Attributes("RandomQuality").InnerText)
                        End If
                        For Each stepnode As Xml.XmlElement In innerNode.SelectNodes("AnimationStep")
                            Dim animationstep As Animation.AnimationStep = New Animation.AnimationStep()
                            animationstep.Step = CInt(stepnode.Attributes("Step").InnerText)
                            animationstep.On = stepnode.Attributes("On").InnerText
                            animationstep.WaitLoopsAfterOn = CInt(stepnode.Attributes("WaitLoopsAfterOn").InnerText)
                            animationstep.Off = stepnode.Attributes("Off").InnerText
                            animationstep.WaitLoopsAfterOff = CInt(stepnode.Attributes("WaitLoopsAfterOff").InnerText)
                            If stepnode.Attributes("PulseSwitch") IsNot Nothing Then
                                animationstep.PulseSwitch = CInt(stepnode.Attributes("PulseSwitch").InnerText)
                            End If
                            ani.AnimationSteps.Add(animationstep)
                        Next
                        myanimations.Add(ani)
                    Next
                End If

                ' get all score info
                If topnode.SelectSingleNode("Scores") IsNot Nothing AndAlso topnode.SelectNodes("Scores/Score") IsNot Nothing Then
                    For Each innerNode As Xml.XmlElement In topnode.SelectNodes("Scores/Score")
                        Dim score As ReelAndLED.ScoreInfo = New ReelAndLED.ScoreInfo()
                        score.ID = CInt(innerNode.Attributes("ID").InnerText)
                        score.ReelType = innerNode.Attributes("ReelType").InnerText
                        score.ReelColor = _backglassData.ReelColor
                        If innerNode.Attributes("ReelColor") IsNot Nothing Then
                            score.ReelColor = String2Color(innerNode.Attributes("ReelColor").InnerText.Replace(";", "."))
                        End If
                        If innerNode.Attributes("B2SStartDigit") IsNot Nothing Then
                            score.B2SStartDigit = CInt(innerNode.Attributes("B2SStartDigit").InnerText)
                        End If
                        If innerNode.Attributes("B2SScoreType") IsNot Nothing Then
                            score.B2SScoreType = CInt(innerNode.Attributes("B2SScoreType").InnerText)
                        End If
                        If innerNode.Attributes("B2SPlayerNo") IsNot Nothing Then
                            score.B2SPlayerNo = CInt(innerNode.Attributes("B2SPlayerNo").InnerText)
                        End If
                        If innerNode.Attributes("NumberOfReels") IsNot Nothing Then
                            score.Digits = CInt(innerNode.Attributes("NumberOfReels").InnerText)
                        End If
                        If innerNode.Attributes("Digits") IsNot Nothing Then
                            score.Digits = CInt(innerNode.Attributes("Digits").InnerText)
                        End If
                        If innerNode.Attributes("SpaceBetweenReels") IsNot Nothing Then
                            score.Spacing = CInt(innerNode.Attributes("SpaceBetweenReels").InnerText)
                        End If
                        If innerNode.Attributes("Spacing") IsNot Nothing Then
                            score.Spacing = CInt(innerNode.Attributes("Spacing").InnerText)
                        End If
                        If innerNode.Attributes("DisplayState") IsNot Nothing Then
                            score.DisplayState = CInt(innerNode.Attributes("DisplayState").InnerText)
                        End If
                        If innerNode.Attributes("ZOrder") IsNot Nothing Then
                            score.ZOrder = CInt(innerNode.Attributes("ZOrder").InnerText)
                        End If
                        score.BehindCanvas = (ReadIntAttribute(innerNode, "BehindCanvas", 0, 0, 1) = 1)
                        If innerNode.Attributes("RotationAngle") IsNot Nothing Then
                            score.RotationAngle = Single.Parse(innerNode.Attributes("RotationAngle").InnerText, Globalization.CultureInfo.InvariantCulture)
                        End If
                        If innerNode.Attributes("PerspectiveDepth") IsNot Nothing Then score.PerspectiveDepth = Single.Parse(innerNode.Attributes("PerspectiveDepth").InnerText, Globalization.CultureInfo.InvariantCulture)
                        If innerNode.Attributes("PerspectiveLeftScale") IsNot Nothing Then score.PerspectiveLeftScale = Single.Parse(innerNode.Attributes("PerspectiveLeftScale").InnerText, Globalization.CultureInfo.InvariantCulture)
                        If innerNode.Attributes("PerspectiveRightScale") IsNot Nothing Then score.PerspectiveRightScale = Single.Parse(innerNode.Attributes("PerspectiveRightScale").InnerText, Globalization.CultureInfo.InvariantCulture)
                        If innerNode.Attributes("ReelIlluLocation") IsNot Nothing Then
                            score.ReelIlluLocation = CInt(innerNode.Attributes("ReelIlluLocation").InnerText)
                        End If
                        If innerNode.Attributes("ReelIlluB2SID") IsNot Nothing Then
                            score.ReelIlluB2SID = CInt(innerNode.Attributes("ReelIlluB2SID").InnerText)
                        End If
                        If innerNode.Attributes("ReelIlluB2SIDType") IsNot Nothing Then
                            score.ReelIlluB2SIDType = CInt(innerNode.Attributes("ReelIlluB2SIDType").InnerText)
                        End If
                        If innerNode.Attributes("ReelIlluB2SValue") IsNot Nothing Then
                            score.ReelIlluB2SValue = CInt(innerNode.Attributes("ReelIlluB2SValue").InnerText)
                        End If
                        If innerNode.Attributes("ReelIlluIntensity") IsNot Nothing Then
                            score.ReelIlluIntensity = CInt(innerNode.Attributes("ReelIlluIntensity").InnerText)
                        End If
                        score.Reel3DEnabled = (ReadIntAttribute(innerNode, "Reel3DEnabled", 0, 0, 1) = 1)
                        score.Reel3DBrightness = ReadIntAttribute(innerNode, "Reel3DBrightness", 100, 0, 400)
                        score.Reel3DTemperature = ReadIntAttribute(innerNode, "Reel3DTemperature", 4000, 2000, 6500)
                        score.Reel3DDepth = ReadIntAttribute(innerNode, "Reel3DDepth", 100, 0, 200)
                        score.Reel3DGlass = ReadIntAttribute(innerNode, "Reel3DGlass", 55, 0, 200)
                        score.Location = New Point(CInt(innerNode.Attributes("LocX").InnerText), CInt(innerNode.Attributes("LocY").InnerText))
                        score.Size = New Size(CInt(innerNode.Attributes("Width").InnerText), CInt(innerNode.Attributes("Height").InnerText))
                        If score.Size.Width < 10 Then score.Size.Width = 10
                        If score.Size.Height < 10 Then score.Size.Height = 10
                        If innerNode.Attributes("Parent") IsNot Nothing AndAlso innerNode.Attributes("Parent").InnerText.Equals("DMD") Then
                            score.ParentForm = eParentForm.DMD
                            mydmdscores.Add(score.ID, score)
                            AddSavedDesignerStackOrder(innerNode, score, mydmdscorestackorders)
                        Else
                            score.ParentForm = eParentForm.Backglass
                            myscores.Add(score.ID, score)
                            AddSavedDesignerStackOrder(innerNode, score, myscorestackorders)
                        End If
                    Next
                    RestoreDesignerStackOrder(myscores, myscorestackorders)
                    RestoreDesignerStackOrder(mydmdscores, mydmdscorestackorders)
                End If

                ' get all illumination info
                If topnode.SelectSingleNode("Illumination") IsNot Nothing AndAlso topnode.SelectNodes("Illumination/Bulb") IsNot Nothing Then
                    For Each innerNode As Xml.XmlElement In topnode.SelectNodes("Illumination/Bulb")
                        Dim bulb As Illumination.BulbInfo = New Illumination.BulbInfo()
                        bulb.ID = CInt(innerNode.Attributes("ID").InnerText)
                        If innerNode.Attributes("B2SID") IsNot Nothing Then
                            bulb.B2SID = CInt(innerNode.Attributes("B2SID").InnerText)
                        End If
                        If innerNode.Attributes("B2SIDType") IsNot Nothing Then
                            bulb.B2SIDType = CInt(innerNode.Attributes("B2SIDType").InnerText)
                        End If
                        If innerNode.Attributes("B2SValue") IsNot Nothing Then
                            bulb.B2SValue = CInt(innerNode.Attributes("B2SValue").InnerText)
                        End If
                        If innerNode.Attributes("RomID") IsNot Nothing Then
                            bulb.RomID = CInt(innerNode.Attributes("RomID").InnerText)
                        End If
                        If innerNode.Attributes("RomIDType") IsNot Nothing Then
                            bulb.RomIDType = CInt(innerNode.Attributes("RomIDType").InnerText)
                        End If
                        If innerNode.Attributes("RomInverted") IsNot Nothing Then
                            bulb.RomInverted = (innerNode.Attributes("RomInverted").InnerText = "1")
                        End If
                        bulb.Name = innerNode.Attributes("Name").InnerText
                        bulb.Text = innerNode.Attributes("Text").InnerText
                        If innerNode.Attributes("TextAlignment") IsNot Nothing Then
                            bulb.TextAlignment = CInt(innerNode.Attributes("TextAlignment").InnerText)
                        End If
                        bulb.FontName = innerNode.Attributes("FontName").InnerText
                        bulb.FontSize = CSng(innerNode.Attributes("FontSize").InnerText)
                        If bulb.FontSize >= 100 Then bulb.FontSize = bulb.FontSize / 100
                        bulb.FontStyle = CInt(innerNode.Attributes("FontStyle").InnerText)
                        bulb.Visible = (CInt(innerNode.Attributes("Visible").InnerText) = 1)
                        bulb.Location = New Point(CInt(innerNode.Attributes("LocX").InnerText), CInt(innerNode.Attributes("LocY").InnerText))
                        bulb.Size = New Size(CInt(innerNode.Attributes("Width").InnerText), CInt(innerNode.Attributes("Height").InnerText))
                        If bulb.Size.Width < 10 Then bulb.Size.Width = 10
                        If bulb.Size.Height < 10 Then bulb.Size.Height = 10
                        If innerNode.Attributes("InitialState") IsNot Nothing Then
                            bulb.InitialState = CInt(innerNode.Attributes("InitialState").InnerText)
                            If bulb.InitialState = 3 Then bulb.InitialState = 1
                        End If
                        If innerNode.Attributes("DualMode") IsNot Nothing Then
                            bulb.DualMode = CInt(innerNode.Attributes("DualMode").InnerText)
                        End If
                        bulb.Intensity = CInt(innerNode.Attributes("Intensity").InnerText)
                        bulb.LightPurpose = CType(ReadIntAttribute(innerNode, "LightPurpose", CInt(Illumination.eLightPurpose.Lamp), 0, 1), Illumination.eLightPurpose)
                        bulb.ArtworkPixelLighting = (ReadIntAttribute(innerNode,
                                                                     "ArtworkPixelLighting",
                                                                     If(defaultArtworkPixelLighting, 1, 0),
                                                                     0, 1) = 1)
                        bulb.BlinkEnabled = (ReadIntAttribute(innerNode, "BlinkEnabled", 0, 0, 1) = 1)
                        bulb.BlinkInterval = ReadIntAttribute(innerNode, "BlinkInterval", 500, 1, 60000)
                        ' Read enhanced/flasher values defensively.  Older projects and interrupted
                        ' saves can contain attributes whose value is present but blank.  VB's CInt("")
                        ' raises InvalidCastException, so every optional numeric value uses a safe
                        ' parser and a sensible backwards-compatible default.
                        bulb.GlowSpread = ReadIntAttribute(innerNode, "GlowSpread", bulb.GlowSpread, 0, 1000)
                        If innerNode.Attributes("InFrontOfGlobalMask") IsNot Nothing Then bulb.InFrontOfGlobalMask = (innerNode.Attributes("InFrontOfGlobalMask").InnerText = "1")
                        If innerNode.Attributes("GlobalMaskLayerExplicit") IsNot Nothing Then bulb.GlobalMaskLayerExplicit = (innerNode.Attributes("GlobalMaskLayerExplicit").InnerText = "1")
                        bulb.LightBehindCanvas = (ReadIntAttribute(innerNode, "LightBehindCanvas", 0, 0, 1) = 1)
                        bulb.GlowSoftness = ReadIntAttribute(innerNode, "GlowSoftness", bulb.GlowSoftness, 0, 300)
                        bulb.GlowFalloff = ReadIntAttribute(innerNode, "GlowFalloff", bulb.GlowFalloff, 0, 300)
                        bulb.LightDiffusion = ReadIntAttribute(innerNode, "LightDiffusion", bulb.LightDiffusion, 0, 300)
                        bulb.LightTemperature = ReadIntAttribute(innerNode, "LightTemperature", 4000, 2000, 6500)
                        bulb.LightRotationAngle = ReadSingleAttribute(innerNode, "LightRotationAngle", 0.0F, -360.0F, 360.0F)
                        bulb.IlluMode = CType(ReadIntAttribute(innerNode, "IlluMode", CInt(bulb.IlluMode), 0, 1), Illumination.eIlluMode)
                        Dim maximumGlowIntensity As Integer = If(bulb.UsesArtworkPixelRenderer, 1600, 800)
                        bulb.GlowIntensity = ReadIntAttribute(innerNode, "GlowIntensity", bulb.GlowIntensity, 0, maximumGlowIntensity)
                        bulb.GlowBlendMode = ReadIntAttribute(innerNode, "GlowBlendMode", bulb.GlowBlendMode, 0, 2)
                        bulb.GlowPreviewQuality = ReadIntAttribute(innerNode, "GlowPreviewQuality", bulb.GlowPreviewQuality, 0, 1)
                        '=== Phase 2 Artwork Flasher Persistence ===
                        bulb.FlasherStyle = ReadIntAttribute(innerNode, "FlasherStyle", 0, 0, 2)
                        bulb.FlasherSaturation = ReadIntAttribute(innerNode, "FlasherSaturation", 105, 0, 200)
                        bulb.FlasherHighlightProtection = ReadIntAttribute(innerNode, "FlasherHighlightProtection", 70, 0, 100)
                        bulb.FlasherDarkAreaLift = ReadIntAttribute(innerNode, "FlasherDarkAreaLift", 20, 0, 100)
                        bulb.FlasherHotspotX = ReadIntAttribute(innerNode, "FlasherHotspotX", 50, 0, 100)
                        bulb.FlasherHotspotY = ReadIntAttribute(innerNode, "FlasherHotspotY", 50, 0, 100)
                        bulb.FlasherPulseDuration = ReadIntAttribute(innerNode, "FlasherPulseDuration", 0, 0, 5000)
                        If innerNode.Attributes("SelectionMaskData") IsNot Nothing Then bulb.SelectionMaskData = innerNode.Attributes("SelectionMaskData").InnerText
                        bulb.SelectionTolerance = ReadIntAttribute(innerNode, "SelectionTolerance", 20, 0, 255)
                        bulb.SelectionFeather = ReadIntAttribute(innerNode, "SelectionFeather", 0, 0, 100)
                        bulb.MaskRadius = ReadIntAttribute(innerNode, "MaskRadius", 18, 0, 100)
                        bulb.MaskSmartRadius = (ReadIntAttribute(innerNode, "MaskSmartRadius", 0, 0, 1) = 1)
                        bulb.MaskSmooth = ReadIntAttribute(innerNode, "MaskSmooth", 50, 0, 100)
                        bulb.MaskFeather = ReadIntAttribute(innerNode, "MaskFeather", 52, 0, 250)
                        bulb.MaskContrast = ReadIntAttribute(innerNode, "MaskContrast", 0, 0, 100)
                        bulb.MaskShiftEdge = ReadIntAttribute(innerNode, "MaskShiftEdge", 10, -100, 100)
                        bulb.FlasherRadialSpikes = ReadIntAttribute(innerNode, "FlasherRadialSpikes", 0, 0, 100)
                        bulb.ArtworkBrightness = ReadIntAttribute(innerNode, "ArtworkBrightness", 0, -150, 150)
                        bulb.ArtworkContrast = ReadIntAttribute(innerNode, "ArtworkContrast", 0, -100, 300)
                        bulb.ArtworkAdjustmentPasses = ReadIntAttribute(innerNode, "ArtworkAdjustmentPasses", 1, 1, 12)
                        If bulb.Intensity < 1 Then
                            bulb.Intensity = 1
                        ElseIf bulb.Intensity > MaxBulbIntensity Then
                            bulb.Intensity = MaxBulbIntensity
                        End If
                        If innerNode.Attributes("LightColor") IsNot Nothing Then
                            bulb.LightColor = String2Color(innerNode.Attributes("LightColor").InnerText)
                        End If
                        If innerNode.Attributes("DodgeColor") IsNot Nothing Then
                            bulb.DodgeColor = String2Color(innerNode.Attributes("DodgeColor").InnerText)
                        End If
                        If innerNode.Attributes("ZOrder") IsNot Nothing Then
                            bulb.ZOrder = CInt(innerNode.Attributes("ZOrder").InnerText)
                        End If
                        If innerNode.Attributes("IsImageSnippit") IsNot Nothing Then
                            bulb.IsImageSnippit = (innerNode.Attributes("IsImageSnippit").InnerText = "1")
                            If bulb.IsImageSnippit Then
                                bulb.Image = Base64ToImage(innerNode.Attributes("Image").InnerText)
                            End If
                        End If
                        If innerNode.Attributes("SnippitType") IsNot Nothing Then
                            bulb.SnippitInfo.SnippitType = CInt(innerNode.Attributes("SnippitType").InnerText)
                            If bulb.SnippitInfo.SnippitType <> eSnippitType.StandardImage Then
                                If innerNode.Attributes("SnippitMechID") IsNot Nothing Then
                                    bulb.SnippitInfo.SnippitMechID = CInt(innerNode.Attributes("SnippitMechID").InnerText)
                                End If
                                If innerNode.Attributes("SnippitRotatingSteps") IsNot Nothing Then
                                    bulb.SnippitInfo.SnippitRotatingSteps = CInt(innerNode.Attributes("SnippitRotatingSteps").InnerText)
                                ElseIf innerNode.Attributes("SnippitRotatingAngle") IsNot Nothing Then
                                    bulb.SnippitInfo.SnippitRotatingSteps = CInt(360 / CInt(innerNode.Attributes("SnippitRotatingAngle").InnerText))
                                End If
                                If innerNode.Attributes("SnippitRotatingDirection") IsNot Nothing Then
                                    bulb.SnippitInfo.SnippitRotatingDirection = CInt(innerNode.Attributes("SnippitRotatingDirection").InnerText)
                                End If
                                If innerNode.Attributes("SnippitRotatingStopBehaviour") IsNot Nothing Then
                                    bulb.SnippitInfo.SnippitRotatingStopBehaviour = CInt(innerNode.Attributes("SnippitRotatingStopBehaviour").InnerText)
                                End If
                                If innerNode.Attributes("SnippitRotatingInterval") IsNot Nothing Then
                                    bulb.SnippitInfo.SnippitRotatingInterval = CInt(innerNode.Attributes("SnippitRotatingInterval").InnerText)
                                End If
                            End If
                        End If
                        bulb.SnippitInfo.Brightness = ReadIntAttribute(innerNode, "SnippitBrightness", 100, 0, 200)
                        bulb.SnippitInfo.BehindCanvas = (ReadIntAttribute(innerNode, "SnippitBehindCanvas", 0, 0, 1) = 1)
                        bulb.SnippitInfo.MotionPathDuration = ReadIntAttribute(innerNode, "MotionPathDuration", 3000, 250, 30000)
                        bulb.SnippitInfo.MotionPathLoop = (ReadIntAttribute(innerNode, "MotionPathLoop", 0, 0, 1) = 1)
                        bulb.SnippitInfo.MotionPathSolenoidID = ReadIntAttribute(innerNode, "MotionPathSolenoidID", 0, 0, 255)
                        bulb.SnippitInfo.MotionPathLampID = ReadIntAttribute(innerNode, "MotionPathLampID", 0, 0, 255)
                        bulb.SnippitInfo.MotionPathB2SID = ReadIntAttribute(innerNode, "MotionPathB2SID", 0, 0, 250)
                        bulb.SnippitInfo.MotionPathStopB2SID = ReadIntAttribute(innerNode, "MotionPathStopB2SID", 0, 0, 250)
                        bulb.SnippitInfo.MotionPathResumeB2SID = ReadIntAttribute(innerNode, "MotionPathResumeB2SID", 0, 0, 250)
                        bulb.SnippitInfo.MotionPathQueueTriggers = (ReadIntAttribute(innerNode, "MotionPathQueueTriggers", 0, 0, 1) = 1)
                        bulb.SnippitInfo.MotionPathRollEnabled = (ReadIntAttribute(innerNode, "MotionPathRollEnabled", 0, 0, 1) = 1)
                        bulb.SnippitInfo.MotionPathSequenceGroup = If(innerNode.Attributes("MotionPathSequenceGroup") Is Nothing, String.Empty, innerNode.Attributes("MotionPathSequenceGroup").InnerText.Trim())
                        bulb.SnippitInfo.MotionPathSequenceOrder = ReadIntAttribute(innerNode, "MotionPathSequenceOrder", 0, 0, 999)
                        bulb.SnippitInfo.MotionPathRespawnEnabled = (ReadIntAttribute(innerNode, "MotionPathRespawnEnabled", 0, 0, 1) = 1)
                        bulb.SnippitInfo.MotionPathRespawnPoint = New PointF(
                            ReadSingleAttribute(innerNode, "MotionPathRespawnX", 0.0F, -100000.0F, 100000.0F),
                            ReadSingleAttribute(innerNode, "MotionPathRespawnY", 0.0F, -100000.0F, 100000.0F))
                        bulb.SnippitInfo.MotionPathRespawnDuration = ReadIntAttribute(innerNode, "MotionPathRespawnDuration", 350, 50, 5000)
                        bulb.SnippitInfo.MotionPathExitDuration = ReadIntAttribute(innerNode, "MotionPathExitDuration", 3000, 250, 30000)
                        bulb.SnippitInfo.MotionPathRemoveSolenoidID = ReadIntAttribute(innerNode, "MotionPathRemoveSolenoidID", 0, 0, 255)
                        bulb.SnippitInfo.MotionPathRemoveLampID = ReadIntAttribute(innerNode, "MotionPathRemoveLampID", 0, 0, 255)
                        bulb.SnippitInfo.MotionPathRemoveB2SID = ReadIntAttribute(innerNode, "MotionPathRemoveB2SID", 0, 0, 250)
                        bulb.SnippitInfo.PivotAnimationEnabled = (ReadIntAttribute(innerNode, "PivotAnimation", 0, 0, 1) = 1)
                        bulb.SnippitInfo.PivotX = ReadSingleAttribute(innerNode, "PivotX", 0.5F, -100000.0F, 100000.0F)
                        bulb.SnippitInfo.PivotY = ReadSingleAttribute(innerNode, "PivotY", 0.5F, -100000.0F, 100000.0F)
                        bulb.SnippitInfo.PivotTipX = ReadSingleAttribute(innerNode, "PivotTipX", 0.9F, 0.0F, 1.0F)
                        bulb.SnippitInfo.PivotTipY = ReadSingleAttribute(innerNode, "PivotTipY", 0.5F, 0.0F, 1.0F)
                        bulb.SnippitInfo.PivotDownAngle = ReadSingleAttribute(innerNode, "PivotDownAngle", 0.0F, -360.0F, 360.0F)
                        bulb.SnippitInfo.PivotUpAngle = ReadSingleAttribute(innerNode, "PivotUpAngle", -30.0F, -360.0F, 360.0F)
                        bulb.SnippitInfo.PivotDuration = ReadIntAttribute(innerNode, "PivotDuration", 80, 10, 5000)
                        bulb.SnippitInfo.PivotAutomaticOscillation = ReadIntAttribute(innerNode, "PivotAutomaticOscillation", 0, 0, 1) = 1
                        bulb.SnippitInfo.PivotTriggerType = ReadIntAttribute(innerNode, "PivotTriggerType", 1, 0, 4)
                        bulb.SnippitInfo.PivotTriggerID = ReadIntAttribute(innerNode, "PivotTriggerID", 0, 0, 255)
                        bulb.SnippitInfo.PivotDownTrigger = If(innerNode.Attributes("PivotDownTrigger") Is Nothing, String.Empty, innerNode.Attributes("PivotDownTrigger").InnerText.Trim())
                        bulb.SnippitInfo.PivotUpTrigger = If(innerNode.Attributes("PivotUpTrigger") Is Nothing, String.Empty, innerNode.Attributes("PivotUpTrigger").InnerText.Trim())
                        bulb.SnippitInfo.PhysicsBall = (ReadIntAttribute(innerNode, "PhysicsBall", 0, 0, 1) = 1)
                        bulb.SnippitInfo.PhysicsFlipperName = If(innerNode.Attributes("PhysicsFlipperName") Is Nothing, String.Empty, innerNode.Attributes("PhysicsFlipperName").InnerText.Trim())
                        bulb.SnippitInfo.PhysicsBounds = If(innerNode.Attributes("PhysicsBounds") Is Nothing, String.Empty, innerNode.Attributes("PhysicsBounds").InnerText.Trim())
                        bulb.SnippitInfo.PhysicsGravity = ReadSingleAttribute(innerNode, "PhysicsGravity", 800.0F, 0.0F, 10000.0F)
                        bulb.SnippitInfo.PhysicsFlipperStrength = ReadSingleAttribute(innerNode, "PhysicsFlipperStrength", 1.0F, 0.0F, 5.0F)
                        bulb.SnippitInfo.PhysicsBoundaryBounce = ReadSingleAttribute(innerNode, "PhysicsBoundaryBounce", 0.12F, 0.0F, 1.0F)
                        If innerNode.Attributes("PhysicsFloorPoints") IsNot Nothing Then
                            bulb.SnippitInfo.PhysicsFloorPoints.AddRange(ParseMotionPathPoints(innerNode.Attributes("PhysicsFloorPoints").InnerText))
                        End If
                        If innerNode.Attributes("PhysicsBoundaries") IsNot Nothing Then
                            bulb.SnippitInfo.PhysicsBoundaryPaths.AddRange(ParsePhysicsBoundaries(innerNode.Attributes("PhysicsBoundaries").InnerText))
                        ElseIf bulb.SnippitInfo.PhysicsFloorPoints.Count >= 2 Then
                            bulb.SnippitInfo.PhysicsBoundaryPaths.Add(New List(Of PointF)(bulb.SnippitInfo.PhysicsFloorPoints))
                        End If
                        If innerNode.Attributes("PhysicsBoundaryNames") IsNot Nothing Then bulb.SnippitInfo.PhysicsBoundaryNames.AddRange(innerNode.Attributes("PhysicsBoundaryNames").InnerText.Split("|"c))
                        If innerNode.Attributes("PhysicsBoundaryLocks") IsNot Nothing Then
                            For Each token As String In innerNode.Attributes("PhysicsBoundaryLocks").InnerText.Split("|"c)
                                bulb.SnippitInfo.PhysicsBoundaryLocks.Add(token.Trim() = "1" OrElse token.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
                            Next
                        End If
                        If innerNode.Attributes("PhysicsBoundarySegmentBounces") IsNot Nothing Then
                            bulb.SnippitInfo.PhysicsBoundarySegmentBounces.AddRange(ParsePhysicsBoundarySegmentBounces(innerNode.Attributes("PhysicsBoundarySegmentBounces").InnerText))
                        End If
                        If innerNode.Attributes("PhysicsObstacles") IsNot Nothing Then bulb.SnippitInfo.PhysicsObstacles.AddRange(ParsePhysicsObstacles(innerNode.Attributes("PhysicsObstacles").InnerText))
                            If innerNode.Attributes("PhysicsObstacleBounces") IsNot Nothing Then bulb.SnippitInfo.PhysicsObstacleBounces.AddRange(ParsePhysicsObstacleBounces(innerNode.Attributes("PhysicsObstacleBounces").InnerText))
                        If innerNode.Attributes("PhysicsSwitchZones") IsNot Nothing Then ParsePhysicsSwitchZones(innerNode.Attributes("PhysicsSwitchZones").InnerText, bulb.SnippitInfo.PhysicsSwitchZones, bulb.SnippitInfo.PhysicsSwitchIDs)
                        If innerNode.Attributes("PhysicsSwitchAngles") IsNot Nothing Then ParsePhysicsSwitchAngles(innerNode.Attributes("PhysicsSwitchAngles").InnerText, bulb.SnippitInfo.PhysicsSwitchAngles)
                        While bulb.SnippitInfo.PhysicsSwitchAngles.Count < bulb.SnippitInfo.PhysicsSwitchZones.Count
                            bulb.SnippitInfo.PhysicsSwitchAngles.Add(0.0F)
                        End While
                        While bulb.SnippitInfo.PhysicsSwitchAngles.Count > bulb.SnippitInfo.PhysicsSwitchZones.Count
                            bulb.SnippitInfo.PhysicsSwitchAngles.RemoveAt(bulb.SnippitInfo.PhysicsSwitchAngles.Count - 1)
                        End While
                        bulb.SnippitInfo.PhysicsLauncherEnabled = ReadIntegerAttribute(innerNode, "PhysicsLauncherEnabled", 0, 0, 1) = 1
                        bulb.SnippitInfo.PhysicsLauncherFollowPivot = ReadIntegerAttribute(innerNode, "PhysicsLauncherFollowPivot", 0, 0, 1) = 1
                        bulb.SnippitInfo.PhysicsLauncherTriggerType = ReadIntegerAttribute(innerNode, "PhysicsLauncherTriggerType", 1, 1, 3)
                        bulb.SnippitInfo.PhysicsLauncherTriggerID = ReadIntegerAttribute(innerNode, "PhysicsLauncherTriggerID", 0, 0, 255)
                        bulb.SnippitInfo.PhysicsLauncherX = ReadSingleAttribute(innerNode, "PhysicsLauncherX", CSng(bulb.Location.X + bulb.Size.Width / 2.0F), -100000.0F, 100000.0F)
                        bulb.SnippitInfo.PhysicsLauncherY = ReadSingleAttribute(innerNode, "PhysicsLauncherY", CSng(bulb.Location.Y + bulb.Size.Height / 2.0F), -100000.0F, 100000.0F)
                        bulb.SnippitInfo.PhysicsLauncherAngle = ReadSingleAttribute(innerNode, "PhysicsLauncherAngle", -90.0F, -360.0F, 360.0F)
                        bulb.SnippitInfo.PhysicsLauncherStrength = ReadSingleAttribute(innerNode, "PhysicsLauncherStrength", 900.0F, 0.0F, 10000.0F)
                        bulb.SnippitInfo.PhysicsLauncherRandomAngle = ReadSingleAttribute(innerNode, "PhysicsLauncherRandomAngle", 0.0F, 0.0F, 180.0F)
                        bulb.SnippitInfo.PhysicsLauncherRandomStrength = ReadSingleAttribute(innerNode, "PhysicsLauncherRandomStrength", 0.0F, 0.0F, 100.0F)
                        bulb.SnippitInfo.PhysicsLauncherCaptureRadius = ReadSingleAttribute(innerNode, "PhysicsLauncherCaptureRadius", 45.0F, 5.0F, 500.0F)
                        If innerNode.Attributes("MotionPathPoints") IsNot Nothing Then
                            bulb.SnippitInfo.MotionPathPoints.AddRange(ParseMotionPathPoints(innerNode.Attributes("MotionPathPoints").InnerText))
                        End If
                        If innerNode.Attributes("MotionPathExitPoints") IsNot Nothing Then
                            bulb.SnippitInfo.MotionPathExitPoints.AddRange(ParseMotionPathPoints(innerNode.Attributes("MotionPathExitPoints").InnerText))
                        End If
                        If innerNode.Attributes("AutomaticRotationEnabled") IsNot Nothing Then
                            bulb.SnippitInfo.AutomaticRotationEnabled = (innerNode.Attributes("AutomaticRotationEnabled").InnerText = "1")
                        End If
                        If innerNode.Attributes("AutomaticRotationContinuous") IsNot Nothing Then
                            bulb.SnippitInfo.AutomaticRotationContinuous = (innerNode.Attributes("AutomaticRotationContinuous").InnerText = "1")
                        End If
                        If innerNode.Attributes("AutomaticRotationTriggerID") IsNot Nothing Then
                            bulb.SnippitInfo.AutomaticRotationTriggerID = Math.Max(0, CInt(innerNode.Attributes("AutomaticRotationTriggerID").InnerText))
                        End If
                        If innerNode.Attributes("AutomaticRotationTriggerType") IsNot Nothing Then
                            Dim triggerType As Integer = CInt(innerNode.Attributes("AutomaticRotationTriggerType").InnerText)
                            bulb.SnippitInfo.AutomaticRotationTriggerType = If(triggerType = CInt(eRomIDType.Solenoid), eRomIDType.Solenoid, eRomIDType.Lamp)
                        End If
                        If innerNode.Attributes("AutomaticRotationSteps") IsNot Nothing Then
                            bulb.SnippitInfo.AutomaticRotationSteps = Math.Max(2, Math.Min(360, CInt(innerNode.Attributes("AutomaticRotationSteps").InnerText)))
                        End If
                        If innerNode.Attributes("AutomaticRotationInterval") IsNot Nothing Then
                            bulb.SnippitInfo.AutomaticRotationInterval = Math.Max(10, Math.Min(500, CInt(innerNode.Attributes("AutomaticRotationInterval").InnerText)))
                        End If
                        If innerNode.Attributes("AutomaticRotationDirection") IsNot Nothing Then
                            bulb.SnippitInfo.AutomaticRotationDirection = CInt(innerNode.Attributes("AutomaticRotationDirection").InnerText)
                        End If
                        If innerNode.Attributes("AutomaticRotationStopBehaviour") IsNot Nothing Then
                            bulb.SnippitInfo.AutomaticRotationStopBehaviour = CInt(innerNode.Attributes("AutomaticRotationStopBehaviour").InnerText)
                        End If
                        ' Missing native-rotation metadata identifies a legacy
                        ' project. Preserve that format until the user explicitly
                        ' selects conversion in the snippet properties dialog.
                        If innerNode.Attributes("Parent") IsNot Nothing AndAlso innerNode.Attributes("Parent").InnerText.Equals("DMD") Then
                            bulb.ParentForm = eParentForm.DMD
                            mydmdbulbs.Insert(mydmdbulbs.Count, bulb)
                            AddSavedDesignerStackOrder(innerNode, bulb, mydmdbulbstackorders)
                        Else
                            bulb.ParentForm = eParentForm.Backglass
                            mybulbs.Insert(mybulbs.Count, bulb)
                            AddSavedDesignerStackOrder(innerNode, bulb, mybulbstackorders)
                        End If
                    Next
                    RestoreDesignerStackOrder(mybulbs, mybulbstackorders)
                    RestoreDesignerStackOrder(mydmdbulbs, mydmdbulbstackorders)
                End If

                ' get standard thumbnail image
                If topnode.SelectSingleNode("Images/ThumbnailImages/MainImage") IsNot Nothing Then
                    .ThumbnailImage = Base64ToImage(topnode.SelectSingleNode("Images/ThumbnailImages/MainImage").Attributes("Image").InnerText)
                End If

                ' get main images
                .ImageFileName = CheckImageFileName(.Name, topnode.SelectSingleNode("Images/BackgroundImages/MainImage").Attributes("FileName").InnerText)
                .Image = Base64ToImage(topnode.SelectSingleNode("Images/BackgroundImages/MainImage").Attributes("Image").InnerText)
                If topnode.SelectSingleNode("Images/DMDImages/MainImage").Attributes("Image") IsNot Nothing Then
                    .DMDImageFileName = CheckImageFileName(.Name, topnode.SelectSingleNode("Images/DMDImages/MainImage").Attributes("FileName").InnerText)
                    .DMDImage = Base64ToImage(topnode.SelectSingleNode("Images/DMDImages/MainImage").Attributes("Image").InnerText)
                End If
                .IsSavedImageDirty = False
                .IsSavedDMDImageDirty = False
                ' get more background images
                myimages.Init()
                If .Image IsNot Nothing Then
                    Dim mainimageinfo As Images.ImageInfo = New Images.ImageInfo(Images.eImageInfoType.BackgroundImage, .ImageFileName, .Image)
                    mainimageinfo.BackgroundImageType = CInt(topnode.SelectSingleNode("Images/BackgroundImages/MainImage").Attributes("Type").InnerText)
                    mainimageinfo.RomID = CInt(topnode.SelectSingleNode("Images/BackgroundImages/MainImage").Attributes("RomID").InnerText)
                    mainimageinfo.RomIDType = CInt(topnode.SelectSingleNode("Images/BackgroundImages/MainImage").Attributes("RomIDType").InnerText)
                    myimages.Insert(Images.eImageInfoType.Title4BackgroundImages, mainimageinfo)
                End If
                For Each innerNode As Xml.XmlElement In topnode.SelectNodes("Images/BackgroundImages/Image")
                    Dim imageinfo As Images.ImageInfo = New Images.ImageInfo(Images.eImageInfoType.BackgroundImage)
                    imageinfo.Text = CheckImageFileName(.Name, innerNode.Attributes("FileName").InnerText)
                    imageinfo.Image = Base64ToImage(innerNode.Attributes("Image").InnerText)
                    If innerNode.Attributes("Type") IsNot Nothing Then
                        imageinfo.BackgroundImageType = CInt(innerNode.Attributes("Type").InnerText)
                        imageinfo.RomID = CInt(innerNode.Attributes("RomID").InnerText)
                        imageinfo.RomIDType = CInt(innerNode.Attributes("RomIDType").InnerText)
                    End If
                    myimages.Insert(Images.eImageInfoType.Title4BackgroundImages, imageinfo)
                Next
                ' get more illuminated images
                For Each innerNode As Xml.XmlElement In topnode.SelectNodes("Images/IlluminatedImages/Image")
                    Dim imageinfo As Images.ImageInfo = New Images.ImageInfo(Images.eImageInfoType.IlluminationImage)
                    imageinfo.Text = CheckImageFileName(.Name, innerNode.Attributes("FileName").InnerText)
                    imageinfo.Image = Base64ToImage(innerNode.Attributes("Image").InnerText)
                    myimages.Insert(Images.eImageInfoType.Title4IlluminationImages, imageinfo)
                Next
                ' get more DMD images
                If .DMDImage IsNot Nothing Then
                    myimages.Insert(Images.eImageInfoType.Title4DMDImages, New Images.ImageInfo(Images.eImageInfoType.DMDImage, .DMDImageFileName, .DMDImage))
                End If
                For Each innerNode As Xml.XmlElement In topnode.SelectNodes("Images/DMDImages/Image")
                    Dim imageinfo As Images.ImageInfo = New Images.ImageInfo(Images.eImageInfoType.DMDImage)
                    imageinfo.Text = CheckImageFileName(.Name, innerNode.Attributes("FileName").InnerText)
                    imageinfo.Image = Base64ToImage(innerNode.Attributes("Image").InnerText)
                    myimages.Insert(Images.eImageInfoType.Title4DMDImages, imageinfo)
                Next
                ' get illumination snippits
                For i As Integer = 1 To 2
                    For Each bulb As Illumination.BulbInfo In If((i = 2), mydmdbulbs, mybulbs)
                        With bulb
                            If .IsImageSnippit AndAlso .Image IsNot Nothing Then
                                Dim imageinfo As Images.ImageInfo = New Images.ImageInfo(Images.eImageInfoType.IlluminationSnippits)
                                imageinfo.Text = .Name
                                imageinfo.Image = .Image
                                myimages.Insert(Images.eImageInfoType.Title4IlluminationSnippits, imageinfo)
                            End If
                        End With
                    Next
                Next
            End With
        End If

        Return True

    End Function
    Public Sub SaveData(ByRef _backglassData As Backglass.Data,
                        ByRef serializedXml As Xml.XmlDocument)

            ' Serialize the editable project for embedding inside the one .B2SPro file.
            Dim XML As Xml.XmlDocument = New Xml.XmlDocument
            Dim nodeHeader As Xml.XmlElement = XML.CreateElement("B2SBackglassData")
            Dim nodeScores As Xml.XmlElement = XML.CreateElement("Scores")
            Dim nodeIllumination As Xml.XmlElement = XML.CreateElement("Illumination")
            Dim nodeAnimations As Xml.XmlElement = XML.CreateElement("Animations")
            Dim nodeImages As Xml.XmlElement = XML.CreateElement("Images")
            XML.AppendChild(nodeHeader)
            nodeHeader.SetAttribute("Version", SaveVersion)
            With _backglassData
                AddXMLAttribute(XML, nodeHeader, "ProjectGUID", "Value", .ProjectGUID)
                AddXMLAttribute(XML, nodeHeader, "ProjectGUID2", "Value", .ProjectGUID2)
                If Not String.IsNullOrEmpty(.GlobalIlluminationMaskData) Then
                    Dim nodeGlobalMask As Xml.XmlElement = XML.CreateElement("GlobalIlluminationMask")
                    nodeGlobalMask.SetAttribute("Data", .GlobalIlluminationMaskData)
                    If Not String.IsNullOrEmpty(.GlobalIlluminationMaskSourceData) Then nodeGlobalMask.SetAttribute("SourceData", .GlobalIlluminationMaskSourceData)
                    nodeGlobalMask.SetAttribute("Enabled", If(.GlobalIlluminationMaskEnabled, "1", "0"))
                    nodeGlobalMask.SetAttribute("Inverted", If(.GlobalIlluminationMaskInverted, "1", "0"))
                    nodeGlobalMask.SetAttribute("Threshold", Math.Max(0, Math.Min(255, .GlobalIlluminationMaskThreshold)).ToString())
                    nodeHeader.AppendChild(nodeGlobalMask)
                End If
                AddXMLAttribute(XML, nodeHeader, "AssemblyGUID", "Value", .AssemblyGUID)
                AddXMLAttribute(XML, nodeHeader, "Name", "Value", .Name)
                AddXMLAttribute(XML, nodeHeader, "VSName", "Value", .VSName)
                AddXMLAttribute(XML, nodeHeader, "DualBackglass", "Value", If(.DualBackglass, "1", "0"))
                AddXMLAttribute(XML, nodeHeader, "Author", "Value", .Author)
                AddXMLAttribute(XML, nodeHeader, "Artwork", "Value", .Artwork)
                AddXMLAttribute(XML, nodeHeader, "GameName", "Value", .GameName)
                AddXMLAttribute(XML, nodeHeader, "TableType", "Value", CInt(.TableType).ToString())
                AddXMLAttribute(XML, nodeHeader, "AddEMDefaults", "Value", If(.AddEMDefaults, "1", "0"))
                AddXMLAttribute(XML, nodeHeader, "DMDType", "Value", CInt(.DMDType).ToString())
                AddXMLAttribute(XML, nodeHeader, "CommType", "Value", CInt(.CommType).ToString())
                AddXMLAttribute(XML, nodeHeader, "DestType", "Value", CInt(.DestType).ToString())
                AddXMLAttribute(XML, nodeHeader, "NumberOfPlayers", "Value", .NumberOfPlayers.ToString())
                AddXMLAttribute(XML, nodeHeader, "B2SDataCount", "Value", .B2SDataCount.ToString())
                AddXMLAttribute(XML, nodeHeader, "ReelType", "Value", .ReelType.ToString())
                AddXMLAttribute(XML, nodeHeader, "UseDream7LEDs", "Value", If(.UseDream7LEDs, "1", "0"))
                AddXMLAttribute(XML, nodeHeader, "D7Glow", "Value", (.D7Glow * 100).ToString())
                AddXMLAttribute(XML, nodeHeader, "D7Thickness", "Value", (.D7Thickness * 100).ToString())
                AddXMLAttribute(XML, nodeHeader, "D7Shear", "Value", (.D7Shear * 100).ToString())
                If .ReelColor <> Nothing Then
                    AddXMLAttribute(XML, nodeHeader, "ReelColor", "Value", Color2String(.ReelColor))
                End If
                AddXMLAttribute(XML, nodeHeader, "ReelRollingDirection", "Value", CInt(.ReelRollingDirection).ToString())
                AddXMLAttribute(XML, nodeHeader, "ReelRollingInterval", "Value", .ReelRollingInterval.ToString())
                AddXMLAttribute(XML, nodeHeader, "ReelIntermediateImageCount", "Value", .ReelIntermediateImageCount.ToString())
                AddXMLAttribute(XML, nodeHeader, "GrillHeight", "Value", CInt(.GrillHeight).ToString())
                If .SmallGrillHeight > 0 Then
                    AddXMLAttribute(XML, nodeHeader, "GrillHeight", "Small", CInt(.SmallGrillHeight).ToString())
                End If
                AddXMLAttribute(XML, nodeHeader, "DMDDefaultLocationX", "Value", CInt(.DMDDefaultLocation.X).ToString())
                AddXMLAttribute(XML, nodeHeader, "DMDDefaultLocationY", "Value", CInt(.DMDDefaultLocation.Y).ToString())
                If .DMDCopyArea IsNot Nothing AndAlso .DMDCopyArea.Location <> Nothing AndAlso .DMDCopyArea.Size <> Nothing Then
                    AddXMLAttribute(XML, nodeHeader, "DMDCopyAreaX", "Value", CInt(.DMDCopyArea.Location.X).ToString())
                    AddXMLAttribute(XML, nodeHeader, "DMDCopyAreaY", "Value", CInt(.DMDCopyArea.Location.Y).ToString())
                    AddXMLAttribute(XML, nodeHeader, "DMDCopyAreaWidth", "Value", CInt(.DMDCopyArea.Size.Width).ToString())
                    AddXMLAttribute(XML, nodeHeader, "DMDCopyAreaHeight", "Value", CInt(.DMDCopyArea.Size.Height).ToString())
                End If

                ' add animations
                nodeHeader.AppendChild(nodeAnimations)
                For Each item As Animation.AnimationHeader In .Animations
                    If Not String.IsNullOrEmpty(item.Name) Then
                        Dim nodeAnimation As Xml.XmlElement = XML.CreateElement("Animation")
                        nodeAnimations.AppendChild(nodeAnimation)
                        With item
                            nodeAnimation.SetAttribute("Name", .Name)
                            nodeAnimation.SetAttribute("Parent", "Backglass")
                            nodeAnimation.SetAttribute("DualMode", CInt(.DualMode).ToString())
                            nodeAnimation.SetAttribute("Interval", .Interval.ToString())
                            nodeAnimation.SetAttribute("Loops", .Loops.ToString())
                            nodeAnimation.SetAttribute("IDJoin", .IDJoin)
                            nodeAnimation.SetAttribute("StartAnimationAtBackglassStartup", If(.StartAnimationAtBackglassStartup, "1", "0"))
                            nodeAnimation.SetAttribute("LightsStateAtAnimationStart", CInt(.LightsStateAtAnimationStart).ToString())
                            nodeAnimation.SetAttribute("LightsStateAtAnimationEnd", CInt(.LightsStateAtAnimationEnd).ToString())
                            nodeAnimation.SetAttribute("AnimationStopBehaviour", CInt(.AnimationStopBehaviour).ToString())
                            nodeAnimation.SetAttribute("LockInvolvedLamps", If(.LockInvolvedLamps, "1", "0"))
                            nodeAnimation.SetAttribute("HideScoreDisplays", If(.HideScoreDisplays, "1", "0"))
                            nodeAnimation.SetAttribute("BringToFront", If(.BringToFront, "1", "0"))
                            If .RandomStart Then
                                nodeAnimation.SetAttribute("RandomStart", If(.RandomStart, "1", "0"))
                                nodeAnimation.SetAttribute("RandomQuality", .RandomQuality.ToString())
                            End If
                            ' add all steps to animation
                            For Each stepitem As Animation.AnimationStep In item.AnimationSteps
                                Dim nodeStep As Xml.XmlElement = XML.CreateElement("AnimationStep")
                                nodeAnimation.AppendChild(nodeStep)
                                With stepitem
                                    nodeStep.SetAttribute("Step", .Step.ToString())
                                    nodeStep.SetAttribute("On", .On)
                                    nodeStep.SetAttribute("WaitLoopsAfterOn", .WaitLoopsAfterOn.ToString())
                                    nodeStep.SetAttribute("Off", .Off)
                                    nodeStep.SetAttribute("WaitLoopsAfterOff", .WaitLoopsAfterOff.ToString())
                                    If .PulseSwitch > 0 Then
                                        nodeStep.SetAttribute("PulseSwitch", .PulseSwitch.ToString())
                                    End If
                                End With
                            Next
                        End With
                    End If
                Next

                ' add score details
                nodeHeader.AppendChild(nodeScores)
                Dim scorestackorders As Generic.Dictionary(Of ReelAndLED.ScoreInfo, Integer) = BuildDesignerStackOrders(.Scores)
                Dim dmdscorestackorders As Generic.Dictionary(Of ReelAndLED.ScoreInfo, Integer) = BuildDesignerStackOrders(.DMDScores)
                Dim savescores As Generic.SortedList(Of Integer, ReelAndLED.ScoreInfo) = New Generic.SortedList(Of Integer, ReelAndLED.ScoreInfo)
                For Each score As ReelAndLED.ScoreInfo In .Scores
                    savescores.Add(score.ID, score)
                Next
                For Each score As ReelAndLED.ScoreInfo In .DMDScores
                    savescores.Add(score.ID + 1000000, score)
                Next
                For Each score As KeyValuePair(Of Integer, ReelAndLED.ScoreInfo) In savescores
                    If score.Value.SizeX.Width > 0 AndAlso score.Value.SizeX.Height > 0 Then
                        Dim nodeScore As Xml.XmlElement = XML.CreateElement("Score")
                        nodeScores.AppendChild(nodeScore)
                        With score.Value
                            nodeScore.SetAttribute("ID", .ID)
                            nodeScore.SetAttribute("Parent", If((score.Key >= 1000000), "DMD", "Backglass"))
                            nodeScore.SetAttribute("ReelType", .ReelType.ToString())
                            If .ReelColor <> Nothing Then
                                nodeScore.SetAttribute("ReelColor", Color2String(.ReelColor))
                            End If
                            If .B2SStartDigit > 0 Then
                                nodeScore.SetAttribute("B2SStartDigit", .B2SStartDigit.ToString())
                            End If
                            If .B2SScoreType <> eB2SScoreType.NotUsed Then
                                nodeScore.SetAttribute("B2SScoreType", CInt(.B2SScoreType).ToString())
                            End If
                            If .B2SPlayerNo <> eB2SPlayerNo.NotUsed Then
                                nodeScore.SetAttribute("B2SPlayerNo", CInt(.B2SPlayerNo).ToString())
                            End If
                            nodeScore.SetAttribute("Digits", .Digits.ToString())
                            nodeScore.SetAttribute("Spacing", .Spacing.ToString())
                            nodeScore.SetAttribute("DisplayState", CInt(.DisplayState).ToString())
                            nodeScore.SetAttribute("ZOrder", .ZOrder.ToString())
                            Dim designerStackOrder As Integer
                            Dim stackOrders As Generic.Dictionary(Of ReelAndLED.ScoreInfo, Integer) = If(.ParentForm = eParentForm.DMD, dmdscorestackorders, scorestackorders)
                            If stackOrders.TryGetValue(score.Value, designerStackOrder) Then nodeScore.SetAttribute("DesignerStackOrder", designerStackOrder.ToString())
                            nodeScore.SetAttribute("BehindCanvas", If(.BehindCanvas, "1", "0"))
                            If Math.Abs(.RotationAngle) > 0.001F Then
                                nodeScore.SetAttribute("RotationAngle", .RotationAngle.ToString(Globalization.CultureInfo.InvariantCulture))
                            End If
                            If Math.Abs(.PerspectiveDepth) > 0.001F Then nodeScore.SetAttribute("PerspectiveDepth", .PerspectiveDepth.ToString(Globalization.CultureInfo.InvariantCulture))
                            If Math.Abs(.PerspectiveLeftScale - 1.0F) > 0.001F Then nodeScore.SetAttribute("PerspectiveLeftScale", .PerspectiveLeftScale.ToString(Globalization.CultureInfo.InvariantCulture))
                            If Math.Abs(.PerspectiveRightScale - 1.0F) > 0.001F Then nodeScore.SetAttribute("PerspectiveRightScale", .PerspectiveRightScale.ToString(Globalization.CultureInfo.InvariantCulture))
                            nodeScore.SetAttribute("ReelIlluLocation", CInt(.ReelIlluLocation).ToString())
                            nodeScore.SetAttribute("ReelIlluIntensity", .ReelIlluIntensity.ToString())
                            nodeScore.SetAttribute("ReelIlluB2SID", .ReelIlluB2SID.ToString())
                            nodeScore.SetAttribute("ReelIlluB2SIDType", CInt(.ReelIlluB2SIDType).ToString())
                            nodeScore.SetAttribute("ReelIlluB2SValue", .ReelIlluB2SValue.ToString())
                            If .Reel3DEnabled Then
                                nodeScore.SetAttribute("Reel3DEnabled", "1")
                                nodeScore.SetAttribute("Reel3DBrightness", Math.Max(0, Math.Min(400, .Reel3DBrightness)).ToString())
                                nodeScore.SetAttribute("Reel3DTemperature", Math.Max(2000, Math.Min(6500, .Reel3DTemperature)).ToString())
                                nodeScore.SetAttribute("Reel3DDepth", Math.Max(0, Math.Min(200, .Reel3DDepth)).ToString())
                                nodeScore.SetAttribute("Reel3DGlass", Math.Max(0, Math.Min(200, .Reel3DGlass)).ToString())
                            End If
                            nodeScore.SetAttribute("LocX", .Location.X)
                            nodeScore.SetAttribute("LocY", .Location.Y)
                            nodeScore.SetAttribute("Width", .Size.Width)
                            nodeScore.SetAttribute("Height", .Size.Height)
                        End With
                    End If
                Next

                ' add illumination
                nodeHeader.AppendChild(nodeIllumination)
                Dim bulbstackorders As Generic.Dictionary(Of Illumination.BulbInfo, Integer) = BuildDesignerStackOrders(.Bulbs)
                Dim dmdbulbstackorders As Generic.Dictionary(Of Illumination.BulbInfo, Integer) = BuildDesignerStackOrders(.DMDBulbs)
                Dim savebulbs As Generic.SortedList(Of Integer, Illumination.BulbInfo) = New Generic.SortedList(Of Integer, Illumination.BulbInfo)
                For Each bulb As Illumination.BulbInfo In .Bulbs
                    savebulbs.Add(bulb.ID, bulb)
                Next
                For Each bulb As Illumination.BulbInfo In .DMDBulbs
                    savebulbs.Add(bulb.ID + 1000000, bulb)
                Next
                For Each bulb As KeyValuePair(Of Integer, Illumination.BulbInfo) In savebulbs
                    If bulb.Value.SizeX.Width > 0 AndAlso bulb.Value.SizeX.Height > 0 Then
                        Dim nodeBulb As Xml.XmlElement = XML.CreateElement("Bulb")
                        nodeIllumination.AppendChild(nodeBulb)
                        With bulb.Value
                            nodeBulb.SetAttribute("ID", .ID)
                            nodeBulb.SetAttribute("Parent", If((bulb.Value.ParentForm = eParentForm.DMD), "DMD", "Backglass"))
                            nodeBulb.SetAttribute("B2SID", .B2SID.ToString())
                            nodeBulb.SetAttribute("B2SIDType", CInt(.B2SIDType).ToString())
                            nodeBulb.SetAttribute("B2SValue", .B2SValue.ToString())
                            nodeBulb.SetAttribute("RomID", .RomID.ToString())
                            nodeBulb.SetAttribute("RomIDType", CInt(.RomIDType).ToString())
                            nodeBulb.SetAttribute("RomInverted", If(.RomInverted, "1", "0"))
                            nodeBulb.SetAttribute("Name", .Name)
                            nodeBulb.SetAttribute("Text", .Text)
                            nodeBulb.SetAttribute("TextAlignment", CInt(.TextAlignment).ToString())
                            nodeBulb.SetAttribute("FontName", .FontName)
                            nodeBulb.SetAttribute("FontSize", CInt(.FontSize * 100).ToString())
                            nodeBulb.SetAttribute("FontStyle", CInt(.FontStyle).ToString())
                            nodeBulb.SetAttribute("Visible", If(.Visible, "1", "0"))
                            nodeBulb.SetAttribute("LocX", .Location.X)
                            nodeBulb.SetAttribute("LocY", .Location.Y)
                            nodeBulb.SetAttribute("Width", .Size.Width)
                            nodeBulb.SetAttribute("Height", .Size.Height)
                            nodeBulb.SetAttribute("InitialState", .InitialState.ToString())
                            nodeBulb.SetAttribute("DualMode", CInt(.DualMode).ToString())
                            If .Intensity <= 0 Then .Intensity = 1
                            nodeBulb.SetAttribute("Intensity", .Intensity.ToString())
                            nodeBulb.SetAttribute("LightPurpose", CInt(.LightPurpose).ToString())
                            nodeBulb.SetAttribute("ArtworkPixelLighting", If(.ArtworkPixelLighting, "1", "0"))
                            nodeBulb.SetAttribute("BlinkEnabled", If(.BlinkEnabled, "1", "0"))
                            nodeBulb.SetAttribute("BlinkInterval", Math.Max(1, Math.Min(60000, .BlinkInterval)).ToString())
                            nodeBulb.SetAttribute("GlowSpread", .GlowSpread.ToString())
                            nodeBulb.SetAttribute("InFrontOfGlobalMask", If(.InFrontOfGlobalMask, "1", "0"))
                            nodeBulb.SetAttribute("GlobalMaskLayerExplicit", If(.GlobalMaskLayerExplicit, "1", "0"))
                            nodeBulb.SetAttribute("LightBehindCanvas", If(.LightBehindCanvas, "1", "0"))
                            nodeBulb.SetAttribute("GlowSoftness", .GlowSoftness.ToString())
                            nodeBulb.SetAttribute("GlowFalloff", .GlowFalloff.ToString())
                            nodeBulb.SetAttribute("LightDiffusion", .LightDiffusion.ToString())
                            nodeBulb.SetAttribute("LightTemperature", Math.Max(2000, Math.Min(6500, .LightTemperature)).ToString())
                            nodeBulb.SetAttribute("LightRotationAngle", .LightRotationAngle.ToString("R", Globalization.CultureInfo.InvariantCulture))
                            nodeBulb.SetAttribute("GlowIntensity", .GlowIntensity.ToString())
                            nodeBulb.SetAttribute("GlowBlendMode", .GlowBlendMode.ToString())
                            nodeBulb.SetAttribute("GlowPreviewQuality", .GlowPreviewQuality.ToString())
                            '=== Phase 2 Artwork Flasher Persistence ===
                            nodeBulb.SetAttribute("FlasherStyle", .FlasherStyle.ToString())
                            nodeBulb.SetAttribute("FlasherSaturation", .FlasherSaturation.ToString())
                            nodeBulb.SetAttribute("FlasherHighlightProtection", .FlasherHighlightProtection.ToString())
                            nodeBulb.SetAttribute("FlasherDarkAreaLift", .FlasherDarkAreaLift.ToString())
                            nodeBulb.SetAttribute("FlasherHotspotX", .FlasherHotspotX.ToString())
                            nodeBulb.SetAttribute("FlasherHotspotY", .FlasherHotspotY.ToString())
                            If .FlasherPulseDuration > 0 Then nodeBulb.SetAttribute("FlasherPulseDuration", Math.Max(50, Math.Min(5000, .FlasherPulseDuration)).ToString())
                            If Not String.IsNullOrEmpty(.SelectionMaskData) Then nodeBulb.SetAttribute("SelectionMaskData", .SelectionMaskData)
                            nodeBulb.SetAttribute("SelectionTolerance", .SelectionTolerance.ToString())
                            nodeBulb.SetAttribute("SelectionFeather", .SelectionFeather.ToString())
                            nodeBulb.SetAttribute("MaskRadius", .MaskRadius.ToString())
                            nodeBulb.SetAttribute("MaskSmartRadius", If(.MaskSmartRadius, "1", "0"))
                            nodeBulb.SetAttribute("MaskSmooth", .MaskSmooth.ToString())
                            nodeBulb.SetAttribute("MaskFeather", .MaskFeather.ToString())
                            nodeBulb.SetAttribute("MaskContrast", .MaskContrast.ToString())
                            nodeBulb.SetAttribute("MaskShiftEdge", .MaskShiftEdge.ToString())
                            nodeBulb.SetAttribute("FlasherRadialSpikes", Math.Max(0, Math.Min(100, .FlasherRadialSpikes)).ToString())
                            nodeBulb.SetAttribute("ArtworkBrightness", .ArtworkBrightness.ToString())
                            nodeBulb.SetAttribute("ArtworkContrast", .ArtworkContrast.ToString())
                            nodeBulb.SetAttribute("ArtworkAdjustmentPasses", .ArtworkAdjustmentPasses.ToString())
                            If .LightColor <> Nothing Then
                                nodeBulb.SetAttribute("LightColor", Color2String(.LightColor))
                            End If
                            If .DodgeColor <> Nothing Then
                                nodeBulb.SetAttribute("DodgeColor", Color2String(.DodgeColor))
                            End If
                            nodeBulb.SetAttribute("IlluMode", CInt(.IlluMode).ToString())
                            nodeBulb.SetAttribute("ZOrder", .ZOrder.ToString())
                            Dim designerStackOrder As Integer
                            Dim stackOrders As Generic.Dictionary(Of Illumination.BulbInfo, Integer) = If(.ParentForm = eParentForm.DMD, dmdbulbstackorders, bulbstackorders)
                            If stackOrders.TryGetValue(bulb.Value, designerStackOrder) Then nodeBulb.SetAttribute("DesignerStackOrder", designerStackOrder.ToString())
                            nodeBulb.SetAttribute("IsImageSnippit", If(.IsImageSnippit, "1", "0"))
                            nodeBulb.SetAttribute("SnippitType", CInt(.SnippitInfo.SnippitType).ToString())
                            nodeBulb.SetAttribute("SnippitBrightness", Math.Max(0, Math.Min(200, .SnippitInfo.Brightness)).ToString())
                            nodeBulb.SetAttribute("SnippitBehindCanvas", If(.SnippitInfo.BehindCanvas, "1", "0"))
                            If .IsImageSnippit Then
                                nodeBulb.SetAttribute("PivotAnimation", If(.SnippitInfo.PivotAnimationEnabled, "1", "0"))
                                nodeBulb.SetAttribute("PivotX", .SnippitInfo.PivotX.ToString(Globalization.CultureInfo.InvariantCulture))
                                nodeBulb.SetAttribute("PivotY", .SnippitInfo.PivotY.ToString(Globalization.CultureInfo.InvariantCulture))
                                nodeBulb.SetAttribute("PivotTipX", .SnippitInfo.PivotTipX.ToString(Globalization.CultureInfo.InvariantCulture))
                                nodeBulb.SetAttribute("PivotTipY", .SnippitInfo.PivotTipY.ToString(Globalization.CultureInfo.InvariantCulture))
                                nodeBulb.SetAttribute("PivotDownAngle", .SnippitInfo.PivotDownAngle.ToString(Globalization.CultureInfo.InvariantCulture))
                                nodeBulb.SetAttribute("PivotUpAngle", .SnippitInfo.PivotUpAngle.ToString(Globalization.CultureInfo.InvariantCulture))
                                nodeBulb.SetAttribute("PivotDuration", Math.Max(10, Math.Min(5000, .SnippitInfo.PivotDuration)).ToString())
                                If .SnippitInfo.PivotAutomaticOscillation Then nodeBulb.SetAttribute("PivotAutomaticOscillation", "1")
                                nodeBulb.SetAttribute("PivotTriggerType", Math.Max(0, Math.Min(4, .SnippitInfo.PivotTriggerType)).ToString())
                                nodeBulb.SetAttribute("PivotTriggerID", Math.Max(0, Math.Min(255, .SnippitInfo.PivotTriggerID)).ToString())
                                nodeBulb.SetAttribute("PivotDownTrigger", .SnippitInfo.PivotDownTrigger.Trim())
                                nodeBulb.SetAttribute("PivotUpTrigger", .SnippitInfo.PivotUpTrigger.Trim())
                                If .SnippitInfo.PhysicsBall OrElse .SnippitInfo.PhysicsFloorPoints.Count >= 2 OrElse .SnippitInfo.PhysicsBoundaryPaths.Count > 0 Then
                                    nodeBulb.SetAttribute("PhysicsBall", If(.SnippitInfo.PhysicsBall, "1", "0"))
                                    nodeBulb.SetAttribute("PhysicsFlipperName", .SnippitInfo.PhysicsFlipperName.Trim())
                                    If Not String.IsNullOrWhiteSpace(.SnippitInfo.PhysicsBounds) Then nodeBulb.SetAttribute("PhysicsBounds", .SnippitInfo.PhysicsBounds.Trim())
                                    nodeBulb.SetAttribute("PhysicsGravity", Math.Max(0.0F, Math.Min(10000.0F, .SnippitInfo.PhysicsGravity)).ToString("R", Globalization.CultureInfo.InvariantCulture))
                                    nodeBulb.SetAttribute("PhysicsFlipperStrength", Math.Max(0.0F, Math.Min(5.0F, .SnippitInfo.PhysicsFlipperStrength)).ToString("R", Globalization.CultureInfo.InvariantCulture))
                                    nodeBulb.SetAttribute("PhysicsBoundaryBounce", Math.Max(0.0F, Math.Min(1.0F, .SnippitInfo.PhysicsBoundaryBounce)).ToString("R", Globalization.CultureInfo.InvariantCulture))
                                    If .SnippitInfo.PhysicsFloorPoints.Count >= 2 Then nodeBulb.SetAttribute("PhysicsFloorPoints", SerializeMotionPathPoints(.SnippitInfo.PhysicsFloorPoints))
                                    If .SnippitInfo.PhysicsBoundaryPaths.Any(Function(path) path IsNot Nothing AndAlso path.Count >= 2) Then nodeBulb.SetAttribute("PhysicsBoundaries", SerializePhysicsBoundaries(.SnippitInfo.PhysicsBoundaryPaths))
                                    If .SnippitInfo.PhysicsBoundaryNames.Count > 0 Then nodeBulb.SetAttribute("PhysicsBoundaryNames", String.Join("|", .SnippitInfo.PhysicsBoundaryNames.Select(Function(name) name.Replace("|", " ").Trim()).ToArray()))
                                    If .SnippitInfo.PhysicsBoundaryLocks.Count > 0 Then nodeBulb.SetAttribute("PhysicsBoundaryLocks", String.Join("|", .SnippitInfo.PhysicsBoundaryLocks.Select(Function(locked) If(locked, "1", "0")).ToArray()))
                                    If .SnippitInfo.PhysicsBoundarySegmentBounces.Any(Function(values) values IsNot Nothing AndAlso values.Any(Function(value) value >= 0.0F)) Then
                                        nodeBulb.SetAttribute("PhysicsBoundarySegmentBounces", SerializePhysicsBoundarySegmentBounces(.SnippitInfo.PhysicsBoundaryPaths, .SnippitInfo.PhysicsBoundarySegmentBounces))
                                    End If
                                    If .SnippitInfo.PhysicsObstacles.Count > 0 Then nodeBulb.SetAttribute("PhysicsObstacles", SerializePhysicsObstacles(.SnippitInfo.PhysicsObstacles))
                                    If .SnippitInfo.PhysicsObstacleBounces.Count > 0 Then nodeBulb.SetAttribute("PhysicsObstacleBounces", String.Join(",", .SnippitInfo.PhysicsObstacleBounces.Select(Function(value) value.ToString("R", Globalization.CultureInfo.InvariantCulture)).ToArray()))
                                If .SnippitInfo.PhysicsSwitchZones.Count > 0 Then
                                    nodeBulb.SetAttribute("PhysicsSwitchZones", SerializePhysicsSwitchZones(.SnippitInfo.PhysicsSwitchZones, .SnippitInfo.PhysicsSwitchIDs))
                                    If .SnippitInfo.PhysicsSwitchAngles.Any(Function(angle) Math.Abs(angle) >= 0.001F) Then
                                        nodeBulb.SetAttribute("PhysicsSwitchAngles", SerializePhysicsSwitchAngles(.SnippitInfo.PhysicsSwitchZones.Count, .SnippitInfo.PhysicsSwitchAngles))
                                    End If
                                End If
                                If .SnippitInfo.PhysicsLauncherEnabled Then
                                    nodeBulb.SetAttribute("PhysicsLauncherEnabled", "1")
                                    If .SnippitInfo.PhysicsLauncherFollowPivot Then nodeBulb.SetAttribute("PhysicsLauncherFollowPivot", "1")
                                    nodeBulb.SetAttribute("PhysicsLauncherTriggerType", .SnippitInfo.PhysicsLauncherTriggerType.ToString())
                                    nodeBulb.SetAttribute("PhysicsLauncherTriggerID", .SnippitInfo.PhysicsLauncherTriggerID.ToString())
                                    nodeBulb.SetAttribute("PhysicsLauncherX", .SnippitInfo.PhysicsLauncherX.ToString("R", Globalization.CultureInfo.InvariantCulture))
                                    nodeBulb.SetAttribute("PhysicsLauncherY", .SnippitInfo.PhysicsLauncherY.ToString("R", Globalization.CultureInfo.InvariantCulture))
                                    nodeBulb.SetAttribute("PhysicsLauncherAngle", .SnippitInfo.PhysicsLauncherAngle.ToString("R", Globalization.CultureInfo.InvariantCulture))
                                    nodeBulb.SetAttribute("PhysicsLauncherStrength", .SnippitInfo.PhysicsLauncherStrength.ToString("R", Globalization.CultureInfo.InvariantCulture))
                                    nodeBulb.SetAttribute("PhysicsLauncherRandomAngle", .SnippitInfo.PhysicsLauncherRandomAngle.ToString("R", Globalization.CultureInfo.InvariantCulture))
                                    nodeBulb.SetAttribute("PhysicsLauncherRandomStrength", .SnippitInfo.PhysicsLauncherRandomStrength.ToString("R", Globalization.CultureInfo.InvariantCulture))
                                    nodeBulb.SetAttribute("PhysicsLauncherCaptureRadius", .SnippitInfo.PhysicsLauncherCaptureRadius.ToString("R", Globalization.CultureInfo.InvariantCulture))
                                End If
                                End If
                            End If
                            If .SnippitInfo.MotionPathRollEnabled Then nodeBulb.SetAttribute("MotionPathRollEnabled", "1")
                            If .SnippitInfo.MotionPathPoints.Count >= 2 Then
                                nodeBulb.SetAttribute("MotionPathPoints", SerializeMotionPathPoints(.SnippitInfo.MotionPathPoints))
                                nodeBulb.SetAttribute("MotionPathDuration", Math.Max(250, Math.Min(30000, .SnippitInfo.MotionPathDuration)).ToString())
                                nodeBulb.SetAttribute("MotionPathLoop", If(.SnippitInfo.MotionPathLoop, "1", "0"))
                                nodeBulb.SetAttribute("MotionPathSolenoidID", Math.Max(0, Math.Min(255, .SnippitInfo.MotionPathSolenoidID)).ToString())
                                nodeBulb.SetAttribute("MotionPathLampID", Math.Max(0, Math.Min(255, .SnippitInfo.MotionPathLampID)).ToString())
                                nodeBulb.SetAttribute("MotionPathB2SID", Math.Max(0, Math.Min(250, .SnippitInfo.MotionPathB2SID)).ToString())
                                nodeBulb.SetAttribute("MotionPathStopB2SID", Math.Max(0, Math.Min(250, .SnippitInfo.MotionPathStopB2SID)).ToString())
                                nodeBulb.SetAttribute("MotionPathResumeB2SID", Math.Max(0, Math.Min(250, .SnippitInfo.MotionPathResumeB2SID)).ToString())
                                nodeBulb.SetAttribute("MotionPathQueueTriggers", If(.SnippitInfo.MotionPathQueueTriggers, "1", "0"))
                                If Not String.IsNullOrWhiteSpace(.SnippitInfo.MotionPathSequenceGroup) Then
                                    nodeBulb.SetAttribute("MotionPathSequenceGroup", .SnippitInfo.MotionPathSequenceGroup.Trim())
                                    nodeBulb.SetAttribute("MotionPathSequenceOrder", Math.Max(1, Math.Min(999, .SnippitInfo.MotionPathSequenceOrder)).ToString())
                                    If .SnippitInfo.MotionPathRespawnEnabled Then
                                        nodeBulb.SetAttribute("MotionPathRespawnEnabled", "1")
                                        nodeBulb.SetAttribute("MotionPathRespawnX", .SnippitInfo.MotionPathRespawnPoint.X.ToString("R", Globalization.CultureInfo.InvariantCulture))
                                        nodeBulb.SetAttribute("MotionPathRespawnY", .SnippitInfo.MotionPathRespawnPoint.Y.ToString("R", Globalization.CultureInfo.InvariantCulture))
                                        nodeBulb.SetAttribute("MotionPathRespawnDuration", Math.Max(50, Math.Min(5000, .SnippitInfo.MotionPathRespawnDuration)).ToString())
                                    End If
                                End If
                                If .SnippitInfo.MotionPathExitPoints.Count >= 2 Then
                                    nodeBulb.SetAttribute("MotionPathExitPoints", SerializeMotionPathPoints(.SnippitInfo.MotionPathExitPoints))
                                    nodeBulb.SetAttribute("MotionPathExitDuration", Math.Max(250, Math.Min(30000, .SnippitInfo.MotionPathExitDuration)).ToString())
                                    nodeBulb.SetAttribute("MotionPathRemoveSolenoidID", Math.Max(0, Math.Min(255, .SnippitInfo.MotionPathRemoveSolenoidID)).ToString())
                                    nodeBulb.SetAttribute("MotionPathRemoveLampID", Math.Max(0, Math.Min(255, .SnippitInfo.MotionPathRemoveLampID)).ToString())
                                    nodeBulb.SetAttribute("MotionPathRemoveB2SID", Math.Max(0, Math.Min(250, .SnippitInfo.MotionPathRemoveB2SID)).ToString())
                                End If
                            End If
                            If .SnippitInfo.SnippitType <> eSnippitType.StandardImage Then
                                If .SnippitInfo.SnippitType = eSnippitType.MechRotatingImage Then
                                    nodeBulb.SetAttribute("SnippitMechID", .SnippitInfo.SnippitMechID.ToString())
                                End If
                                nodeBulb.SetAttribute("SnippitRotatingSteps", .SnippitInfo.SnippitRotatingSteps.ToString())
                                nodeBulb.SetAttribute("SnippitRotatingDirection", CInt(.SnippitInfo.SnippitRotatingDirection).ToString())
                                nodeBulb.SetAttribute("SnippitRotatingStopBehaviour", CInt(.SnippitInfo.SnippitRotatingStopBehaviour).ToString())
                                If .SnippitInfo.SnippitType = eSnippitType.SelfRotatingImage Then
                                    nodeBulb.SetAttribute("SnippitRotatingInterval", .SnippitInfo.SnippitRotatingInterval.ToString())
                                End If
                            End If
                            nodeBulb.SetAttribute("AutomaticRotationEnabled", If(.SnippitInfo.AutomaticRotationEnabled, "1", "0"))
                            nodeBulb.SetAttribute("AutomaticRotationContinuous", If(.SnippitInfo.AutomaticRotationContinuous, "1", "0"))
                            nodeBulb.SetAttribute("AutomaticRotationTriggerID", Math.Max(0, .SnippitInfo.AutomaticRotationTriggerID).ToString())
                            nodeBulb.SetAttribute("AutomaticRotationTriggerType", CInt(If(.SnippitInfo.AutomaticRotationTriggerType = eRomIDType.Solenoid, eRomIDType.Solenoid, eRomIDType.Lamp)).ToString())
                            nodeBulb.SetAttribute("AutomaticRotationSteps", .SnippitInfo.AutomaticRotationSteps.ToString())
                            nodeBulb.SetAttribute("AutomaticRotationInterval", .SnippitInfo.AutomaticRotationInterval.ToString())
                            nodeBulb.SetAttribute("AutomaticRotationDirection", CInt(.SnippitInfo.AutomaticRotationDirection).ToString())
                            nodeBulb.SetAttribute("AutomaticRotationStopBehaviour", CInt(.SnippitInfo.AutomaticRotationStopBehaviour).ToString())
                            nodeBulb.SetAttribute("Image", If(.Image IsNot Nothing, ImageToBase64(.Image), ""))
                        End With
                    End If
                Next

                ' add images
                nodeHeader.AppendChild(nodeImages)
                Dim nodeTI As Xml.XmlElement = XML.CreateElement("ThumbnailImages")
                Dim nodeBI As Xml.XmlElement = XML.CreateElement("BackgroundImages")
                Dim nodeII As Xml.XmlElement = XML.CreateElement("IlluminatedImages")
                Dim nodeDI As Xml.XmlElement = XML.CreateElement("DMDImages")
                nodeImages.AppendChild(nodeTI)
                nodeImages.AppendChild(nodeBI)
                nodeImages.AppendChild(nodeII)
                nodeImages.AppendChild(nodeDI)
                ' standard thumbnail image
                If .ThumbnailImage IsNot Nothing Then
                    Dim nodeTIMain As Xml.XmlElement = XML.CreateElement("MainImage")
                    nodeTI.AppendChild(nodeTIMain)
                    nodeTIMain.SetAttribute("Image", ImageToBase64(.ThumbnailImage))
                End If
                ' main background image with some data
                Dim nodeBIMain As Xml.XmlElement = XML.CreateElement("MainImage")
                nodeBI.AppendChild(nodeBIMain)
                nodeBIMain.SetAttribute("Type", "0")
                nodeBIMain.SetAttribute("RomID", "0")
                nodeBIMain.SetAttribute("RomIDType", "0")
                nodeBIMain.SetAttribute("FileName", .ImageFileName)
                nodeBIMain.SetAttribute("Image", ImageToBase64(.Image))
                .IsSavedImageDirty = False
                ' main DMD image with some data
                Dim nodeDIMain As Xml.XmlElement = XML.CreateElement("MainImage")
                nodeDI.AppendChild(nodeDIMain)
                If .DMDImage IsNot Nothing Then
                    nodeDIMain.SetAttribute("FileName", .DMDImageFileName)
                    nodeDIMain.SetAttribute("Image", ImageToBase64(.DMDImage))
                    .IsSavedDMDImageDirty = False
                End If
                ' get thru all images
                For Each image As Images.ImageInfo In .Images
                    If image.Image IsNot Nothing AndAlso Not image.Text.Equals(.ImageFileName, StringComparison.CurrentCultureIgnoreCase) AndAlso Not image.Text.Equals(.DMDImageFileName, StringComparison.CurrentCultureIgnoreCase) Then
                        Dim node As Xml.XmlElement = Nothing
                        Select Case image.Type
                            Case Images.eImageInfoType.BackgroundImage
                                node = nodeBI
                            Case Images.eImageInfoType.IlluminationImage
                                node = nodeII
                            Case Images.eImageInfoType.DMDImage
                                node = nodeDI
                        End Select
                        If node IsNot Nothing Then
                            Dim nodeImageDetails As Xml.XmlElement = XML.CreateElement("Image")
                            If node.ParentNode Is Nothing Then nodeImages.AppendChild(node)
                            node.AppendChild(nodeImageDetails)
                            With image
                                If .Type = Images.eImageInfoType.BackgroundImage Then
                                    nodeImageDetails.SetAttribute("Type", CInt(.BackgroundImageType).ToString())
                                    nodeImageDetails.SetAttribute("RomID", .RomID.ToString())
                                    nodeImageDetails.SetAttribute("RomIDType", CInt(.RomIDType).ToString())
                                End If
                                nodeImageDetails.SetAttribute("FileName", .Text)
                                nodeImageDetails.SetAttribute("Image", ImageToBase64(.Image))
                            End With
                        End If
                    ElseIf image.Image IsNot Nothing AndAlso image.Text.Equals(.ImageFileName, StringComparison.CurrentCultureIgnoreCase) AndAlso image.Type = Images.eImageInfoType.BackgroundImage Then
                        nodeBIMain.Attributes("Type").InnerText = image.BackgroundImageType
                        nodeBIMain.Attributes("RomID").InnerText = image.RomID.ToString()
                        nodeBIMain.Attributes("RomIDType").InnerText = CInt(image.RomIDType).ToString()
                    End If
                Next
            End With

            serializedXml = XML

    End Sub


    Private Shared Function ReadSingleAttribute(ByVal node As Xml.XmlNode, ByVal name As String, ByVal defaultValue As Single, ByVal minimum As Single, ByVal maximum As Single) As Single
        If node.Attributes(name) Is Nothing Then Return defaultValue
        Dim value As Single
        If Not Single.TryParse(node.Attributes(name).InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, value) Then Return defaultValue
        Return Math.Max(minimum, Math.Min(maximum, value))
    End Function

    Private Shared Function ReadIntegerAttribute(ByVal node As Xml.XmlNode, ByVal name As String, ByVal defaultValue As Integer, ByVal minimum As Integer, ByVal maximum As Integer) As Integer
        If node.Attributes(name) Is Nothing Then Return defaultValue
        Dim value As Integer
        If Not Integer.TryParse(node.Attributes(name).InnerText, value) Then Return defaultValue
        Return Math.Max(minimum, Math.Min(maximum, value))
    End Function

    Private Shared Function SerializeMotionPathPoints(ByVal points As IEnumerable(Of PointF)) As String
        Dim values As New List(Of String)()
        For Each point As PointF In points
            values.Add(point.X.ToString("R", Globalization.CultureInfo.InvariantCulture) & "," &
                       point.Y.ToString("R", Globalization.CultureInfo.InvariantCulture))
        Next
        Return String.Join(";", values.ToArray())
    End Function

    Private Shared Function ParseMotionPathPoints(ByVal value As String) As List(Of PointF)
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

    Private Shared Function SerializePhysicsBoundaries(ByVal paths As IEnumerable(Of List(Of PointF))) As String
        Dim values As New List(Of String)()
        For Each path As List(Of PointF) In paths
            If path IsNot Nothing AndAlso path.Count >= 2 Then values.Add(SerializeMotionPathPoints(path))
        Next
        Return String.Join("|", values.ToArray())
    End Function

    Private Shared Function ParsePhysicsBoundaries(ByVal value As String) As List(Of List(Of PointF))
        Dim paths As New List(Of List(Of PointF))()
        If String.IsNullOrWhiteSpace(value) Then Return paths
        For Each encoded As String In value.Split("|"c)
            Dim path As List(Of PointF) = ParseMotionPathPoints(encoded)
            If path.Count >= 2 Then paths.Add(path)
        Next
        Return paths
    End Function

    Private Shared Function SerializePhysicsBoundarySegmentBounces(ByVal paths As IList(Of List(Of PointF)),
                                                                    ByVal segmentBounces As IList(Of List(Of Single))) As String
        Dim encodedPaths As New List(Of String)()
        For pathIndex As Integer = 0 To paths.Count - 1
            Dim path As List(Of PointF) = paths(pathIndex)
            If path Is Nothing OrElse path.Count < 2 Then Continue For
            Dim encodedSegments As New List(Of String)()
            Dim saved As List(Of Single) = If(pathIndex < segmentBounces.Count, segmentBounces(pathIndex), Nothing)
            For segmentIndex As Integer = 0 To path.Count - 2
                Dim value As Single = If(saved IsNot Nothing AndAlso segmentIndex < saved.Count, saved(segmentIndex), -1.0F)
                encodedSegments.Add(Math.Max(-1.0F, Math.Min(3.0F, value)).ToString("R", Globalization.CultureInfo.InvariantCulture))
            Next
            encodedPaths.Add(String.Join(",", encodedSegments.ToArray()))
        Next
        Return String.Join("|", encodedPaths.ToArray())
    End Function

    Private Shared Function ParsePhysicsBoundarySegmentBounces(ByVal value As String) As List(Of List(Of Single))
        Dim paths As New List(Of List(Of Single))()
        If String.IsNullOrWhiteSpace(value) Then Return paths
        For Each encodedPath As String In value.Split("|"c)
            Dim segments As New List(Of Single)()
            For Each encodedSegment As String In encodedPath.Split(","c)
                Dim parsed As Single
                If Single.TryParse(encodedSegment, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, parsed) Then
                    segments.Add(Math.Max(-1.0F, Math.Min(3.0F, parsed)))
                Else
                    segments.Add(-1.0F)
                End If
            Next
            paths.Add(segments)
        Next
        Return paths
    End Function

    Private Shared Function ParsePhysicsObstacleBounces(ByVal value As String) As List(Of Single)
        Dim result As New List(Of Single)()
        If String.IsNullOrWhiteSpace(value) Then Return result
        For Each encoded As String In value.Split(","c)
            Dim parsed As Single
            If Not Single.TryParse(encoded, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, parsed) OrElse
                Single.IsNaN(parsed) OrElse Single.IsInfinity(parsed) OrElse parsed < 0.0F Then
                result.Add(-1.0F)
            Else
                result.Add(Math.Min(3.0F, parsed))
            End If
        Next
        Return result
    End Function

    Private Shared Function SerializePhysicsObstacles(ByVal obstacles As IEnumerable(Of RectangleF)) As String
        Return String.Join("|", obstacles.Where(Function(item) item.Width > 0 AndAlso item.Height > 0).
                           Select(Function(item) String.Join(",", {item.X.ToString("R", Globalization.CultureInfo.InvariantCulture),
                                                                  item.Y.ToString("R", Globalization.CultureInfo.InvariantCulture),
                                                                  item.Width.ToString("R", Globalization.CultureInfo.InvariantCulture),
                                                                  item.Height.ToString("R", Globalization.CultureInfo.InvariantCulture)})).ToArray())
    End Function

    Private Shared Function ParsePhysicsObstacles(ByVal value As String) As List(Of RectangleF)
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

    Private Shared Function SerializePhysicsSwitchZones(ByVal zones As IList(Of RectangleF), ByVal switchIDs As IList(Of Integer)) As String
        Dim encoded As New List(Of String)()
        For index As Integer = 0 To zones.Count - 1
            Dim item As RectangleF = zones(index)
            If item.Width <= 0 OrElse item.Height <= 0 Then Continue For
            Dim switchID As Integer = If(index < switchIDs.Count, switchIDs(index), 1)
            encoded.Add(String.Join(",", {item.X.ToString("R", Globalization.CultureInfo.InvariantCulture), item.Y.ToString("R", Globalization.CultureInfo.InvariantCulture), item.Width.ToString("R", Globalization.CultureInfo.InvariantCulture), item.Height.ToString("R", Globalization.CultureInfo.InvariantCulture), Math.Max(1, Math.Min(255, switchID)).ToString(Globalization.CultureInfo.InvariantCulture)}))
        Next
        Return String.Join("|", encoded.ToArray())
    End Function

    Private Shared Sub ParsePhysicsSwitchZones(ByVal value As String, ByVal zones As IList(Of RectangleF), ByVal switchIDs As IList(Of Integer))
        If String.IsNullOrWhiteSpace(value) Then Return
        For Each item As String In value.Split("|"c)
            Dim parts As String() = item.Split(","c)
            Dim x, y, width, height As Single
            Dim switchID As Integer
            If parts.Length = 5 AndAlso Single.TryParse(parts(0), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, x) AndAlso Single.TryParse(parts(1), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, y) AndAlso Single.TryParse(parts(2), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, width) AndAlso Single.TryParse(parts(3), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, height) AndAlso Integer.TryParse(parts(4), switchID) AndAlso width > 0 AndAlso height > 0 Then
                zones.Add(New RectangleF(x, y, width, height))
                switchIDs.Add(Math.Max(1, Math.Min(255, switchID)))
            End If
        Next
    End Sub

    Private Shared Function SerializePhysicsSwitchAngles(ByVal zoneCount As Integer, ByVal angles As IList(Of Single)) As String
        Dim encoded As New List(Of String)()
        For index As Integer = 0 To zoneCount - 1
            Dim angle As Single = If(index < angles.Count, NormalizeSwitchAngle(angles(index)), 0.0F)
            encoded.Add(angle.ToString("R", Globalization.CultureInfo.InvariantCulture))
        Next
        Return String.Join("|", encoded.ToArray())
    End Function

    Private Shared Sub ParsePhysicsSwitchAngles(ByVal value As String, ByVal angles As IList(Of Single))
        If String.IsNullOrWhiteSpace(value) Then Return
        For Each item As String In value.Split("|"c)
            Dim angle As Single
            angles.Add(If(Single.TryParse(item, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, angle),
                          NormalizeSwitchAngle(angle), 0.0F))
        Next
    End Sub

    Private Shared Function NormalizeSwitchAngle(ByVal angle As Single) As Single
        Dim normalized As Single = angle Mod 360.0F
        If normalized > 180.0F Then normalized -= 360.0F
        If normalized <= -180.0F Then normalized += 360.0F
        Return normalized
    End Function

    Private Shared Function BuildDesignerStackOrders(Of T As Class)(ByVal items As IEnumerable(Of T)) As Generic.Dictionary(Of T, Integer)
        Dim result As New Generic.Dictionary(Of T, Integer)()
        If items Is Nothing Then Return result

        Dim index As Integer = 0
        For Each item As T In items
            If item IsNot Nothing Then result(item) = index
            index += 1
        Next
        Return result
    End Function

    Private Shared Sub AddSavedDesignerStackOrder(Of T As Class)(ByVal node As Xml.XmlElement,
                                                                  ByVal item As T,
                                                                  ByVal savedOrders As Generic.Dictionary(Of T, Integer))
        If node Is Nothing OrElse item Is Nothing OrElse savedOrders Is Nothing OrElse
           node.Attributes("DesignerStackOrder") Is Nothing Then Return

        Dim value As Integer
        If Integer.TryParse(node.Attributes("DesignerStackOrder").InnerText, value) AndAlso value >= 0 Then
            savedOrders(item) = value
        End If
    End Sub

    Private Shared Sub RestoreDesignerStackOrder(Of T As Class)(ByVal items As Generic.List(Of T),
                                                                 ByVal savedOrders As Generic.Dictionary(Of T, Integer))
        If items Is Nothing OrElse items.Count < 2 OrElse savedOrders Is Nothing OrElse
           savedOrders.Count <> items.Count Then Return

        ' Only a complete, unambiguous saved order is allowed to rearrange a
        ' collection. A missing, duplicate, or damaged value leaves legacy data
        ' exactly as it loaded instead of inventing an order.
        Dim uniqueOrders As New Generic.HashSet(Of Integer)()
        For Each item As T In items
            Dim value As Integer
            If item Is Nothing OrElse Not savedOrders.TryGetValue(item, value) OrElse
               value < 0 OrElse Not uniqueOrders.Add(value) Then Return
        Next

        items.Sort(Function(left As T, right As T) savedOrders(left).CompareTo(savedOrders(right)))
    End Sub

    Private Shared Function ReadIntAttribute(ByVal node As Xml.XmlElement,
                                             ByVal attributeName As String,
                                             ByVal fallback As Integer,
                                             ByVal minimum As Integer,
                                             ByVal maximum As Integer) As Integer
        If node Is Nothing OrElse node.Attributes(attributeName) Is Nothing Then Return Math.Max(minimum, Math.Min(maximum, fallback))

        Dim text As String = node.Attributes(attributeName).InnerText
        Dim parsed As Integer
        If String.IsNullOrWhiteSpace(text) OrElse Not Integer.TryParse(text.Trim(), parsed) Then
            parsed = fallback
        End If
        Return Math.Max(minimum, Math.Min(maximum, parsed))
    End Function

    Private Function CheckImageFileName(ByVal name As String, ByVal filename As String) As String
        Dim ret As String = filename
        If Not String.IsNullOrEmpty(filename) AndAlso Not IO.File.Exists(filename) Then
            ret = IO.Path.Combine(".\Projects", name, "My Resources", FileIO.FileSystem.GetFileInfo(filename).Name)
            'ret = IO.Path.Combine(BackglassProjectsPath, name, "My Resources", FileIO.FileSystem.GetFileInfo(filename).Name)
        End If
        Return ret
    End Function

End Class
