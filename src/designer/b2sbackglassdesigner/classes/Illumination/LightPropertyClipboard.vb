Public NotInheritable Class LightPropertyClipboard
    Private Shared hasValue As Boolean
    Private Shared spread, softness, intensity, falloff, diffusion, temperature, blend, quality, tolerance, feather As Integer
    Private Shared maskRadius, maskSmooth, maskFeather, maskContrast, maskShiftEdge, flasherRadialSpikes As Integer
    Private Shared maskSmartRadius As Boolean
    Private Shared flasherStyle, flasherSaturation, flasherHighlightProtection, flasherDarkAreaLift, flasherHotspotX, flasherHotspotY, flasherPulseDuration As Integer
    Private Shared inFrontOfGlobalMask, lightBehindCanvas, artworkPixelLighting As Boolean
    Private Sub New()
    End Sub
    Public Shared ReadOnly Property CanPaste As Boolean
        Get
            Return hasValue
        End Get
    End Property
    Public Shared Sub CopyFrom(ByVal bulb As Illumination.BulbInfo)
        If bulb Is Nothing Then Return
        spread=bulb.GlowSpread : softness=bulb.GlowSoftness : intensity=bulb.GlowIntensity
        falloff=bulb.GlowFalloff : diffusion=bulb.LightDiffusion : temperature=bulb.LightTemperature : blend=bulb.GlowBlendMode : quality=bulb.GlowPreviewQuality
        tolerance=bulb.SelectionTolerance : feather=bulb.SelectionFeather
        maskRadius=bulb.MaskRadius : maskSmartRadius=bulb.MaskSmartRadius : maskSmooth=bulb.MaskSmooth
        maskFeather=bulb.MaskFeather : maskContrast=bulb.MaskContrast : maskShiftEdge=bulb.MaskShiftEdge
        flasherRadialSpikes=bulb.FlasherRadialSpikes
        flasherStyle=bulb.FlasherStyle : flasherSaturation=bulb.FlasherSaturation : flasherHighlightProtection=bulb.FlasherHighlightProtection : flasherDarkAreaLift=bulb.FlasherDarkAreaLift : flasherHotspotX=bulb.FlasherHotspotX : flasherHotspotY=bulb.FlasherHotspotY : flasherPulseDuration=bulb.FlasherPulseDuration
        inFrontOfGlobalMask=bulb.InFrontOfGlobalMask
        lightBehindCanvas=bulb.LightBehindCanvas
        artworkPixelLighting=bulb.ArtworkPixelLighting
        hasValue=True
    End Sub
    Public Shared Sub PasteTo(ByVal bulb As Illumination.BulbInfo)
        If bulb Is Nothing OrElse Not hasValue Then Return
        bulb.GlowSpread=spread : bulb.GlowSoftness=softness : bulb.GlowIntensity=intensity
        bulb.GlowFalloff=falloff : bulb.LightDiffusion=diffusion : bulb.LightTemperature=temperature : bulb.GlowBlendMode=blend : bulb.GlowPreviewQuality=quality
        bulb.SelectionTolerance=tolerance : bulb.SelectionFeather=feather
        ' SelectionMaskData is full-backglass geometry tied to the source
        ' flasher's exact coordinates. Copying it to another flasher leaves the
        ' destination object pointing at a mask elsewhere on the artwork and
        ' makes both the live preview and generated image disappear. Reuse the
        ' refinement settings, but keep the destination's own authored mask.
        bulb.MaskRadius=maskRadius : bulb.MaskSmartRadius=maskSmartRadius : bulb.MaskSmooth=maskSmooth
        bulb.MaskFeather=maskFeather : bulb.MaskContrast=maskContrast : bulb.MaskShiftEdge=maskShiftEdge
        bulb.FlasherRadialSpikes=flasherRadialSpikes
        bulb.FlasherStyle=flasherStyle : bulb.FlasherSaturation=flasherSaturation : bulb.FlasherHighlightProtection=flasherHighlightProtection : bulb.FlasherDarkAreaLift=flasherDarkAreaLift : bulb.FlasherHotspotX=flasherHotspotX : bulb.FlasherHotspotY=flasherHotspotY : bulb.FlasherPulseDuration=flasherPulseDuration
        bulb.InFrontOfGlobalMask=inFrontOfGlobalMask
        bulb.LightBehindCanvas=lightBehindCanvas
        bulb.ArtworkPixelLighting=artworkPixelLighting
        bulb.IsIlluminatedImageDirty=True
    End Sub
End Class
