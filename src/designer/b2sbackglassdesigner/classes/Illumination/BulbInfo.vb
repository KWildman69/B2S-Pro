Namespace Illumination

    Public Enum eTextAlignment
        Center = 0
        Left = 1
        Right = 2
    End Enum
    Public Enum eIlluMode
        Standard = 0
        Flasher = 1
    End Enum

    ' Describes how a standard Light object is used in the designer.
    ' This is classification metadata only; both choices use the same Light renderer.
    Public Enum eLightPurpose
        Lamp = 0
        Flasher = 1
    End Enum

    Public Class BulbInfo

        Inherits InfoBase

        Public Name As String = String.Empty

        Public Text As String = String.Empty
        Public TextAlignment As eTextAlignment = eTextAlignment.Center
        Public FontName As String = String.Empty
        Public FontSize As Single = 10
        Public FontStyle As FontStyle = FontStyle.Regular
        ' Optional text shape, set with the text box handles. Bend arches the
        ' text (-1..1); the perspective values match the score window handles.
        ' The defaults draw the text exactly as before and are not saved.
        Public TextBend As Single = 0.0F
        Public TextPerspectiveDepth As Single = 0.0F
        Public TextPerspectiveLeftScale As Single = 1.0F
        Public TextPerspectiveRightScale As Single = 1.0F

        Public Function TextShape() As TextShapeInfo
            Return New TextShapeInfo(TextBend, TextPerspectiveDepth, TextPerspectiveLeftScale, TextPerspectiveRightScale)
        End Function

        Public Visible As Boolean = True

        Public InitialState As Integer = 0
        Public DualMode As eDualMode = eDualMode.Both

        Public LightColor As Color = DefaultLightColor
        Public DodgeColor As Color = Nothing
        Public Intensity As Integer = 1
        Public IlluMode As eIlluMode = eIlluMode.Standard
        Public LightPurpose As eLightPurpose = eLightPurpose.Lamp
        ' Regular B2S Pro lights can use the same artwork-pixel renderer as
        ' flashers without changing their lamp identity or trigger behavior.
        ' False preserves imported legacy directB2S illumination exactly.
        Public ArtworkPixelLighting As Boolean = False
        ' Draw only the authored light shape as a transparent layer at the
        ' light's normal Z-order. No stationary artwork pixels are baked into it.
        Public SeparateLightLayer As Boolean = False
        ' Opt-in runtime lighting for moving artwork. The saved light field is
        ' applied to the snippet's current frame instead of exporting one frozen
        ' illuminated snapshot. False preserves every existing backglass path.
        Public DynamicFrontLighting As Boolean = False
        ' Optionally keep the authored light visible through its normal Quick
        ' Selection mask while dynamic lighting redraws moving snippets above it.
        Public DynamicFrontShowLight As Boolean = False
        ' Optional explicit snippet target for dynamic front lighting. Zero keeps
        ' the legacy behavior of lighting every eligible snippet below the light.
        Public DynamicFrontTargetSnippetID As Integer = 0
        ' New files may explicitly select more than one snippet. The singular ID
        ' remains the fallback for older Designer, Server, and plugin versions.
        ' False preserves the legacy zero-means-all behavior when the plural XML
        ' attribute is absent; True makes this list authoritative.
        Public DynamicFrontTargetSnippetIDs As New List(Of Integer)()
        Public DynamicFrontTargetSelectionDefined As Boolean = False

        Public ReadOnly Property UsesArtworkPixelRenderer As Boolean
            Get
                Return ArtworkPixelLighting OrElse
                       IlluMode = eIlluMode.Flasher OrElse
                       LightPurpose = eLightPurpose.Flasher
            End Get
        End Property

        ' Optional per-light blinker. Interval is the duration of each on/off phase.
        Public BlinkEnabled As Boolean = False
        Public BlinkInterval As Integer = 500

        ' Enhanced light rendering. Spread is pixels beyond the selection box.
        Public GlowSpread As Integer = 0
        Public GlowSoftness As Integer = 60
        Public GlowIntensity As Integer = 100
        Public GlowFalloff As Integer = 60
        ' Optional outward diffusion for standard lights. 0 preserves the legacy light renderer.
        Public LightDiffusion As Integer = 0
        ' Correlated color temperature in Kelvin. 4000K preserves the legacy light color.
        Public LightTemperature As Integer = 4000
        ' Static editor rotation for the illumination field. Zero preserves every
        ' existing light and exported backglass exactly.
        Public LightRotationAngle As Single = 0.0F
        Public GlowBlendMode As Integer = 0 ' 0=Normal, 1=Additive, 2=Screen
        Public GlowPreviewQuality As Integer = 1 ' 0=Draft, 1=High

        ' Photoshop-style artwork flasher controls.
        Public FlasherStyle As Integer = 1 ' kept for project compatibility; artwork-only is the new default
        Public FlasherSaturation As Integer = 100
        Public FlasherHighlightProtection As Integer = 0
        Public FlasherDarkAreaLift As Integer = 0
        Public FlasherHotspotX As Integer = 50
        Public FlasherHotspotY As Integer = 50
        ' Optional one-shot runtime pulse. Zero preserves legacy trigger-following behavior.
        Public FlasherPulseDuration As Integer = 0

        ' Select and Mask settings. These are stored per flasher and can also be
        ' remembered as defaults by LightGlowDefaults.
        Public MaskRadius As Integer = 18
        Public MaskSmartRadius As Boolean = False
        Public MaskSmooth As Integer = 50
        Public MaskFeather As Integer = 52
        Public MaskContrast As Integer = 0
        Public MaskShiftEdge As Integer = 10
        ' Optional starburst rays outside a box-mode flasher. Zero preserves
        ' every legacy light and flasher exactly.
        Public FlasherRadialSpikes As Integer = 0

        ' Image > Adjustments > Brightness/Contrast settings.
        Public ArtworkBrightness As Integer = 0
        Public ArtworkContrast As Integer = 0
        Public ArtworkAdjustmentPasses As Integer = 1

        ' Enhanced 2.7.6: False = behind global mask, True = in front/bypass mask.
        Public InFrontOfGlobalMask As Boolean = False
        Public GlobalMaskLayerExplicit As Boolean = False
        ' Optional quick-selection mask stored as a PNG encoded in Base64.
        Public SelectionMaskData As String = String.Empty
        Public SelectionTolerance As Integer = 20
        Public SelectionFeather As Integer = 0

        Public ZOrder As Integer = 0
        ' Exact front-to-back position in the unified visual deck.  This is
        ' independent of the editable Z value so lights, flashers, snippets,
        ' reels and LEDs can be interleaved without per-type placement flags.
        Public LayerOrder As Integer = -1


        Public IsImageSnippit As Boolean = False
        Public Image As Image = Nothing

        Public SnippitInfo As SnippitInfo = New SnippitInfo()

        Public IsIlluminatedImageDirty As Boolean = True

    End Class

    ' Bend and perspective for a text box. IsFlat means the text is drawn with
    ' the original renderer, unchanged.
    Public Class TextShapeInfo
        Public ReadOnly Bend As Single
        Public ReadOnly Depth As Single
        Public ReadOnly LeftScale As Single
        Public ReadOnly RightScale As Single

        Public Sub New(ByVal bend As Single, ByVal depth As Single, ByVal leftScale As Single, ByVal rightScale As Single)
            Me.Bend = Math.Max(-1.0F, Math.Min(1.0F, bend))
            Me.Depth = Math.Max(-1.0F, Math.Min(1.0F, depth))
            Me.LeftScale = Math.Max(0.2F, Math.Min(2.5F, leftScale))
            Me.RightScale = Math.Max(0.2F, Math.Min(2.5F, rightScale))
        End Sub

        Public ReadOnly Property IsFlat As Boolean
            Get
                Return Math.Abs(Bend) < 0.001F AndAlso Math.Abs(Depth) < 0.001F AndAlso
                       Math.Abs(LeftScale - 1.0F) < 0.001F AndAlso Math.Abs(RightScale - 1.0F) < 0.001F
            End Get
        End Property

        Public ReadOnly Property Key As String
            Get
                If IsFlat Then Return ""
                Dim inv As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture
                Return Bend.ToString("R", inv) & "/" & Depth.ToString("R", inv) & "/" & LeftScale.ToString("R", inv) & "/" & RightScale.ToString("R", inv)
            End Get
        End Property

        ' The score-style perspective outline of a box: top-left, top-right,
        ' bottom-right, bottom-left. Same formula as the score window handles.
        Public Function Corners(ByVal box As RectangleF) As PointF()
            Dim centerX As Single = box.X + box.Width / 2.0F
            Dim centerY As Single = box.Y + box.Height / 2.0F
            Dim halfWidth As Single = box.Width / 2.0F
            Dim halfHeight As Single = box.Height / 2.0F
            Dim amount As Single = Math.Abs(Depth)
            Dim projectedHalfWidth As Single = halfWidth * (1.0F - 0.34F * amount)
            Dim nearScale As Single = 1.0F + 0.06F * amount
            Dim farScale As Single = 1.0F - 0.58F * amount
            Dim leftEnd As Single = If(Depth >= 0.0F, nearScale, farScale) * LeftScale
            Dim rightEnd As Single = If(Depth >= 0.0F, farScale, nearScale) * RightScale
            Return New PointF() {
                New PointF(centerX - projectedHalfWidth, centerY - halfHeight * leftEnd),
                New PointF(centerX + projectedHalfWidth, centerY - halfHeight * rightEnd),
                New PointF(centerX + projectedHalfWidth, centerY + halfHeight * rightEnd),
                New PointF(centerX - projectedHalfWidth, centerY + halfHeight * leftEnd)}
        End Function

        ' Moves one point of flat text inside box to its bent/perspective place.
        Public Function Warp(ByVal p As PointF, ByVal box As RectangleF) As PointF
            Dim width As Single = Math.Max(1.0F, box.Width)
            Dim height As Single = Math.Max(1.0F, box.Height)
            Dim centerX As Single = box.X + width / 2.0F
            Dim centerY As Single = box.Y + height / 2.0F
            Dim x As Double = p.X
            Dim y As Double = p.Y
            ' Bend: the middle line of the box becomes an arc of the same length.
            ' Bend 1 is a half circle arching up; -1 curves down like a smile.
            If Math.Abs(Bend) >= 0.001F Then
                Dim arc As Double = Math.Abs(Bend) * Math.PI
                Dim radius As Double = width / arc
                Dim angle As Double = (x - centerX) / radius
                Dim offset As Double = y - centerY
                If Bend > 0.0F Then
                    Dim r As Double = radius - offset
                    x = centerX + r * Math.Sin(angle)
                    y = centerY + radius - r * Math.Cos(angle)
                Else
                    Dim r As Double = radius + offset
                    x = centerX + r * Math.Sin(angle)
                    y = centerY - radius + r * Math.Cos(angle)
                End If
            End If
            ' Perspective: the box becomes the score-style outline.
            If Math.Abs(Depth) >= 0.001F OrElse Math.Abs(LeftScale - 1.0F) >= 0.001F OrElse Math.Abs(RightScale - 1.0F) >= 0.001F Then
                Dim amount As Double = Math.Abs(Depth)
                Dim nearScale As Double = 1.0 + 0.06 * amount
                Dim farScale As Double = 1.0 - 0.58 * amount
                Dim leftEnd As Double = If(Depth >= 0.0F, nearScale, farScale) * LeftScale
                Dim rightEnd As Double = If(Depth >= 0.0F, farScale, nearScale) * RightScale
                Dim u As Double = (x - box.X) / width
                Dim endScale As Double = leftEnd + (rightEnd - leftEnd) * u
                x = centerX + (x - centerX) * (1.0 - 0.34 * amount)
                y = centerY + (y - centerY) * endScale
            End If
            Return New PointF(CSng(x), CSng(y))
        End Function
    End Class

    ' Saves the text shape only when it is used, so flat text boxes and every
    ' existing file keep exactly the same attributes.
    Public NotInheritable Class TextShapeXml
        Public Shared Sub Write(ByVal node As Xml.XmlElement, ByVal shape As TextShapeInfo)
            If node Is Nothing OrElse shape Is Nothing OrElse shape.IsFlat Then Return
            Dim inv As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture
            If Math.Abs(shape.Bend) >= 0.001F Then node.SetAttribute("TextBend", shape.Bend.ToString("R", inv))
            If Math.Abs(shape.Depth) >= 0.001F Then node.SetAttribute("TextPerspectiveDepth", shape.Depth.ToString("R", inv))
            If Math.Abs(shape.LeftScale - 1.0F) >= 0.001F Then node.SetAttribute("TextPerspectiveLeftScale", shape.LeftScale.ToString("R", inv))
            If Math.Abs(shape.RightScale - 1.0F) >= 0.001F Then node.SetAttribute("TextPerspectiveRightScale", shape.RightScale.ToString("R", inv))
        End Sub

        Public Shared Sub Read(ByVal node As Xml.XmlNode, ByVal bulb As BulbInfo)
            If node Is Nothing OrElse bulb Is Nothing Then Return
            bulb.TextBend = ReadValue(node, "TextBend", 0.0F, -1.0F, 1.0F)
            bulb.TextPerspectiveDepth = ReadValue(node, "TextPerspectiveDepth", 0.0F, -1.0F, 1.0F)
            bulb.TextPerspectiveLeftScale = ReadValue(node, "TextPerspectiveLeftScale", 1.0F, 0.2F, 2.5F)
            bulb.TextPerspectiveRightScale = ReadValue(node, "TextPerspectiveRightScale", 1.0F, 0.2F, 2.5F)
        End Sub

        Private Shared Function ReadValue(ByVal node As Xml.XmlNode, ByVal name As String, ByVal fallback As Single, ByVal minimum As Single, ByVal maximum As Single) As Single
            Dim attribute As Xml.XmlAttribute = If(node.Attributes Is Nothing, Nothing, node.Attributes(name))
            If attribute Is Nothing Then Return fallback
            Dim value As Single
            If Not Single.TryParse(attribute.InnerText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, value) Then Return fallback
            If Single.IsNaN(value) OrElse Single.IsInfinity(value) Then Return fallback
            Return Math.Max(minimum, Math.Min(maximum, value))
        End Function
    End Class

    Public Class SnippitInfo

        ' Independent image brightness for snippets. 100 preserves the source.
        Public Brightness As Integer = 100
        ' Optional snippet-only refinement of a Quick Selection alpha mask.
        ' Zero values are an exact no-op so existing snippets retain their
        ' authored appearance until an author deliberately adjusts a slider.
        Public MaskEdgeSoftness As Integer = 0
        Public MaskOutwardDiffusion As Integer = 0
        Public MaskShiftEdge As Integer = 0
        Public SnippitType As eSnippitType = eSnippitType.StandardImage
        Public SnippitMechID As Integer = 0
        Public SnippitRotatingSteps As Integer = 0
        Public SnippitRotatingInterval As Integer = 0
        Public SnippitRotatingDirection As eSnippitRotationDirection = eSnippitRotationDirection.Clockwise
        Public SnippitRotatingStopBehaviour As eSnippitRotationStopBehaviour = eSnippitRotationStopBehaviour.SpinOff

        ' Independent B2S Pro automatic rotation.  These settings deliberately
        ' do not use the legacy self/mechanical rotating snippet types.
        Public AutomaticRotationEnabled As Boolean = False
        Public AutomaticRotationContinuous As Boolean = True
        Public AutomaticRotationTriggerID As Integer = 0
        Public AutomaticRotationTriggerType As eRomIDType = eRomIDType.Lamp
        Public AutomaticRotationSteps As Integer = 24
        Public AutomaticRotationInterval As Integer = 50
        Public AutomaticRotationDirection As eSnippitRotationDirection = eSnippitRotationDirection.Clockwise
        Public AutomaticRotationStopBehaviour As eSnippitRotationStopBehaviour = eSnippitRotationStopBehaviour.RunAnimationTillEnd

        ' B2S Pro motion-path test metadata. Empty points preserve all legacy behavior.
        Public MotionPathPoints As New List(Of PointF)()
        Public MotionPathDuration As Integer = 3000
        Public MotionPathLoop As Boolean = False
        Public MotionPathSwitchID As Integer = 0
        Public MotionPathStopSwitchID As Integer = 0
        Public MotionPathStopLampID As Integer = 0
        Public MotionPathStopSolenoidID As Integer = 0
        Public MotionPathResumeSwitchID As Integer = 0
        Public MotionPathResumeLampID As Integer = 0
        Public MotionPathResumeSolenoidID As Integer = 0
        Public MotionPathRemoveSwitchID As Integer = 0
        Public MotionPathSolenoidID As Integer = 0
        Public MotionPathLampID As Integer = 0
        Public MotionPathB2SID As Integer = 0
        Public MotionPathStopB2SID As Integer = 0
        Public MotionPathResumeB2SID As Integer = 0
        Public MotionPathQueueTriggers As Boolean = False
        ' Optional distance-driven visual rolling for any ball snippet that moves.
        ' False preserves all existing motion-path and physics rendering.
        Public MotionPathRollEnabled As Boolean = False
        Public MotionPathSequenceGroup As String = String.Empty
        Public MotionPathSequenceOrder As Integer = 0
        Public MotionPathDrainAll As Boolean = False
        Public MotionPathGravityDrop As Boolean = False
        Public MotionPathRespawnEnabled As Boolean = False
        Public MotionPathRespawnPoint As PointF = PointF.Empty
        Public MotionPathRespawnDuration As Integer = 350
        Public MotionPathExitPoints As New List(Of PointF)()
        Public MotionPathExitDuration As Integer = 3000
        Public MotionPathRemoveSolenoidID As Integer = 0
        Public MotionPathRemoveLampID As Integer = 0
        Public MotionPathRemoveB2SID As Integer = 0

        ' B2S Pro Path Animation (shared\PathAnimation.vb). Disabled by default;
        ' nothing is saved or exported unless it is enabled on this snippet.
        Public PathAnim As New PathAnimSettings()

        ' Single-image pivot animation. One snippet owns both named positions.
        Public PivotAnimationEnabled As Boolean = False
        Public PivotX As Single = 0.5F
        Public PivotY As Single = 0.5F
        Public PivotTipX As Single = 0.9F
        Public PivotTipY As Single = 0.5F
        Public PivotDownAngle As Single = 0.0F
        Public PivotUpAngle As Single = -30.0F
        Public PivotDuration As Integer = 80
        Public PivotRandomStrength As Single = 0.0F
        Public PivotAutomaticOscillation As Boolean = False
        ' Optional collision-driven motion for a pivot snippet. Disabled by
        ' default so every existing pivot and legacy backglass is unchanged.
        Public PivotPhysicsEnabled As Boolean = False
        Public PivotPhysicsWeight As Single = 1.0F
        Public PivotPhysicsFriction As Single = 1.25F
        Public PivotPhysicsGrip As Single = 0.5F
        ' 0=free spin, 1=knock down and stay, 2=knock down then reset.
        Public PivotPhysicsResponseMode As Integer = 0
        Public PivotPhysicsKnockdownAngle As Single = 90.0F
        Public PivotPhysicsKnockdownDuration As Integer = 180
        Public PivotPhysicsResetDelay As Integer = 1000
        ' Optional counterweight for a free-spin physics pivot. Gravity pulls the
        ' weight point (normalized to the image, like PivotX/Y) straight down.
        Public PivotWeightEnabled As Boolean = False
        Public PivotWeightX As Single = 0.5F
        Public PivotWeightY As Single = 1.0F
        Public PivotWeightStrength As Single = 50.0F
        ' 0=named commands, 1=solenoid, 2=lamp, 3=B2S ID.
        Public PivotTriggerType As Integer = 1
        Public PivotTriggerID As Integer = 0
        Public PivotDownTrigger As String = String.Empty
        Public PivotUpTrigger As String = String.Empty
        ' Reverse trigger: rest while the solenoid/lamp/B2S ID/switch is on, move up while it is off.
        Public PivotTriggerReverse As Boolean = False
        ' Optional pivot-to-pivot strike: this pivot's moving silhouette hits the
        ' named physics pivot and spins it. Empty keeps every file unchanged.
        Public PivotStrikeTarget As String = String.Empty
        Public PivotStrikeStrength As Single = 100.0F
        ' True: the shapes must touch. False: a no-contact (linked) kick.
        Public PivotStrikeContact As Boolean = True
        ' No-contact kick spin direction: 1 = clockwise, -1 = counter-clockwise.
        Public PivotStrikeDirection As Integer = 1

        ' B2S Pro visual physics metadata. An empty boundary list preserves
        ' every legacy backglass and disables the physics runtime path.
        Public PhysicsBall As Boolean = False
        Public PhysicsFlipperName As String = String.Empty
        ' PhysicsFlipperName remains the single legacy/launcher pivot. New
        ' files can additionally name every pivot that receives ball impacts.
        Public PhysicsPivotTargetNames As New List(Of String)()
        Public PhysicsBounds As String = String.Empty
        Public PhysicsGravity As Single = 800.0F
        Public PhysicsFlipperStrength As Single = 1.0F
        Public PhysicsBoundaryBounce As Single = 0.12F
        Public PhysicsFloorPoints As New List(Of PointF)()
        Public PhysicsBoundaryPaths As New List(Of List(Of PointF))()
        Public PhysicsBoundaryNames As New List(Of String)()
        Public PhysicsBoundaryLocks As New List(Of Boolean)()
        ' One list per boundary path and one value per segment. A negative
        ' value means that segment inherits PhysicsBoundaryBounce.
        Public PhysicsBoundarySegmentBounces As New List(Of List(Of Single))()
        Public PhysicsObstacles As New List(Of RectangleF)()
        Public PhysicsObstacleBounces As New List(Of Single)()
        Public PhysicsSwitchZones As New List(Of RectangleF)()
        Public PhysicsSwitchIDs As New List(Of Integer)()
        Public PhysicsSwitchAngles As New List(Of Single)()
        ' Optional embedded WAV per sensor zone. Empty entries preserve the
        ' existing ROM-switch-only behavior. Data is Base64 so project saves
        ' remain self-contained and do not depend on the original WAV path.
        Public PhysicsSwitchSoundNames As New List(Of String)()
        Public PhysicsSwitchSoundData As New List(Of String)()
        Public PhysicsSwitchSoundVolumes As New List(Of Integer)()
        Public PhysicsLauncherEnabled As Boolean = False
        Public PhysicsLauncherFollowPivot As Boolean = False
        Public PhysicsLauncherTriggerType As Integer = 1
        Public PhysicsLauncherTriggerID As Integer = 0
        Public PhysicsLauncherX As Single = 0.0F
        Public PhysicsLauncherY As Single = 0.0F
        Public PhysicsLauncherAngle As Single = -90.0F
        Public PhysicsLauncherStrength As Single = 900.0F
        Public PhysicsLauncherRandomAngle As Single = 0.0F
        Public PhysicsLauncherRandomStrength As Single = 0.0F
        Public PhysicsLauncherCaptureRadius As Single = 45.0F

        ' Present only on frames created by the Infinity Lights generator.
        ' Empty values preserve every normal and legacy snippet path.
        Public InfinityLightsGroup As String = String.Empty
        Public InfinityLightsSettings As String = String.Empty

    End Class

End Namespace
