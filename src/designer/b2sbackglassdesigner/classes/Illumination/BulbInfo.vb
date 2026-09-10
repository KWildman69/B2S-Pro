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

        Public Visible As Boolean = True

        Public InitialState As Integer = 0
        Public DualMode As eDualMode = eDualMode.Both

        Public LightColor As Color = DefaultLightColor
        Public DodgeColor As Color = Nothing
        Public Intensity As Integer = 1
        Public IlluMode As eIlluMode = eIlluMode.Standard
        Public LightPurpose As eLightPurpose = eLightPurpose.Lamp

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
        ' Render the light physically behind the backglass artwork, visible only
        ' through transparent or partially transparent canvas pixels.
        Public LightBehindCanvas As Boolean = False

        ' Optional quick-selection mask stored as a PNG encoded in Base64.
        Public SelectionMaskData As String = String.Empty
        Public SelectionTolerance As Integer = 20
        Public SelectionFeather As Integer = 0

        Public ZOrder As Integer = 0


        Public IsImageSnippit As Boolean = False
        Public Image As Image = Nothing

        Public SnippitInfo As SnippitInfo = New SnippitInfo()

        Public IsIlluminatedImageDirty As Boolean = True

    End Class

    Public Class SnippitInfo

        ' Independent image brightness for snippets. 100 preserves the source.
        Public Brightness As Integer = 100
        ' Draw/export the snippet through transparent areas of the backglass
        ' artwork so it behaves as an image physically behind the glass.
        Public BehindCanvas As Boolean = False
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
        Public MotionPathRespawnEnabled As Boolean = False
        Public MotionPathRespawnPoint As PointF = PointF.Empty
        Public MotionPathRespawnDuration As Integer = 350
        Public MotionPathExitPoints As New List(Of PointF)()
        Public MotionPathExitDuration As Integer = 3000
        Public MotionPathRemoveSolenoidID As Integer = 0
        Public MotionPathRemoveLampID As Integer = 0
        Public MotionPathRemoveB2SID As Integer = 0

        ' Single-image pivot animation. One snippet owns both named positions.
        Public PivotAnimationEnabled As Boolean = False
        Public PivotX As Single = 0.5F
        Public PivotY As Single = 0.5F
        Public PivotTipX As Single = 0.9F
        Public PivotTipY As Single = 0.5F
        Public PivotDownAngle As Single = 0.0F
        Public PivotUpAngle As Single = -30.0F
        Public PivotDuration As Integer = 80
        ' 0=named commands, 1=solenoid, 2=lamp, 3=B2S ID.
        Public PivotTriggerType As Integer = 1
        Public PivotTriggerID As Integer = 0
        Public PivotDownTrigger As String = String.Empty
        Public PivotUpTrigger As String = String.Empty

        ' B2S Pro visual physics metadata. An empty boundary list preserves
        ' every legacy backglass and disables the physics runtime path.
        Public PhysicsBall As Boolean = False
        Public PhysicsFlipperName As String = String.Empty
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
        Public PhysicsSwitchZones As New List(Of RectangleF)()
        Public PhysicsSwitchIDs As New List(Of Integer)()
        Public PhysicsLauncherEnabled As Boolean = False
        Public PhysicsLauncherTriggerType As Integer = 1
        Public PhysicsLauncherTriggerID As Integer = 0
        Public PhysicsLauncherX As Single = 0.0F
        Public PhysicsLauncherY As Single = 0.0F
        Public PhysicsLauncherAngle As Single = -90.0F
        Public PhysicsLauncherStrength As Single = 900.0F
        Public PhysicsLauncherRandomAngle As Single = 0.0F
        Public PhysicsLauncherRandomStrength As Single = 0.0F
        Public PhysicsLauncherCaptureRadius As Single = 45.0F

        Public Function UpgradeLegacySelfRotation(ByVal triggerID As Integer,
                                                  ByVal triggerType As eRomIDType) As Boolean
            If SnippitType <> eSnippitType.SelfRotatingImage OrElse AutomaticRotationEnabled Then Return False

            AutomaticRotationEnabled = True
            AutomaticRotationContinuous = (triggerID <= 0)
            AutomaticRotationTriggerID = Math.Max(0, triggerID)
            AutomaticRotationTriggerType = If(triggerType = eRomIDType.Solenoid,
                                               eRomIDType.Solenoid,
                                               eRomIDType.Lamp)
            AutomaticRotationSteps = Math.Max(4, Math.Min(90, SnippitRotatingSteps))
            AutomaticRotationInterval = Math.Max(10, Math.Min(500, SnippitRotatingInterval))
            AutomaticRotationDirection = SnippitRotatingDirection
            AutomaticRotationStopBehaviour = SnippitRotatingStopBehaviour
            SnippitType = eSnippitType.StandardImage
            Return True
        End Function

        Public Function UpgradeLegacyMechRotation() As Boolean
            If SnippitType <> eSnippitType.MechRotatingImage OrElse AutomaticRotationEnabled Then Return False

            ' Mechanical snippets are position controlled, not started and
            ' stopped by an on/off command. Keep their type and Mech ID while
            ' moving the shared rotation values into the native one-image data.
            AutomaticRotationEnabled = True
            AutomaticRotationContinuous = False
            AutomaticRotationTriggerID = 0
            AutomaticRotationSteps = Math.Max(2, Math.Min(360, SnippitRotatingSteps))
            AutomaticRotationInterval = Math.Max(10, Math.Min(500, SnippitRotatingInterval))
            AutomaticRotationDirection = SnippitRotatingDirection
            AutomaticRotationStopBehaviour = SnippitRotatingStopBehaviour
            Return True
        End Function

    End Class

End Namespace
