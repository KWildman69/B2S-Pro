Imports Microsoft.Win32

Namespace Illumination
    ' Stores two independent, persistent slider profiles: one for normal lights
    ' and one for artwork flashers.  The profiles are kept in HKCU so they
    ' survive closing and reopening the Designer.
    Public Module LightGlowDefaults
        Private Const RegistryRoot As String = "Software\B2SBackglassDesigner\IlluminationProfiles"

        ' Retained for compatibility with older UI code. Profiles are automatic.
        Public ReadOnly Property Enabled As Boolean
            Get
                Return True
            End Get
        End Property

        Public Function HasProfile(ByVal bulb As BulbInfo) As Boolean
            If bulb Is Nothing Then Return False
            Return ReadInteger(ProfilePath(bulb), "Saved", 0) = 1
        End Function

        Public Sub Save(ByVal spread As Integer,
                        ByVal feather As Integer,
                        ByVal contrast As Integer,
                        ByVal intensity As Integer,
                        Optional ByVal bulb As BulbInfo = Nothing,
                        Optional ByVal lightDiffusion As Integer = 0,
                        Optional ByVal lightTemperature As Integer = 4000)
            If bulb Is Nothing Then Return

            Try
                Using key As RegistryKey = Registry.CurrentUser.CreateSubKey(ProfilePath(bulb))
                    If key Is Nothing Then Return

                    key.SetValue("Saved", 1, RegistryValueKind.DWord)
                    key.SetValue("Spread", Clamp(spread, 0, 1000), RegistryValueKind.DWord)
                    key.SetValue("Feather", Clamp(feather, 0, 200), RegistryValueKind.DWord)
                    key.SetValue("Contrast", Clamp(contrast, 0, 100), RegistryValueKind.DWord)
                    key.SetValue("Intensity", Clamp(intensity, 0, If(IsFlasherProfile(bulb), 1600, 800)), RegistryValueKind.DWord)
                    key.SetValue("LightDiffusion", Clamp(lightDiffusion, 0, 300), RegistryValueKind.DWord)
                    key.SetValue("LightTemperature", Clamp(lightTemperature, 2000, 6500), RegistryValueKind.DWord)

                    If IsFlasherProfile(bulb) Then
                        key.SetValue("TransmissionContrast", Clamp(bulb.ArtworkContrast, 0, 300), RegistryValueKind.DWord)
                        key.SetValue("FlasherStyle", Clamp(bulb.FlasherStyle, 0, 2), RegistryValueKind.DWord)
                        key.SetValue("FlasherSaturation", Clamp(bulb.FlasherSaturation, 0, 200), RegistryValueKind.DWord)
                        key.SetValue("FlasherHighlightProtection", Clamp(bulb.FlasherHighlightProtection, 0, 100), RegistryValueKind.DWord)
                        key.SetValue("FlasherDarkAreaLift", Clamp(bulb.FlasherDarkAreaLift, 0, 100), RegistryValueKind.DWord)
                        key.SetValue("FlasherHotspotX", Clamp(bulb.FlasherHotspotX, 0, 100), RegistryValueKind.DWord)
                        key.SetValue("FlasherHotspotY", Clamp(bulb.FlasherHotspotY, 0, 100), RegistryValueKind.DWord)
                        key.SetValue("MaskRadius", Clamp(bulb.MaskRadius, 0, 100), RegistryValueKind.DWord)
                        key.SetValue("MaskSmartRadius", If(bulb.MaskSmartRadius, 1, 0), RegistryValueKind.DWord)
                        key.SetValue("MaskSmooth", Clamp(bulb.MaskSmooth, 0, 100), RegistryValueKind.DWord)
                        key.SetValue("MaskFeather", Clamp(bulb.MaskFeather, 0, 250), RegistryValueKind.DWord)
                        key.SetValue("MaskContrast", Clamp(bulb.MaskContrast, 0, 100), RegistryValueKind.DWord)
                        key.SetValue("MaskShiftEdge", Clamp(bulb.MaskShiftEdge, -100, 100), RegistryValueKind.DWord)
                        key.SetValue("ArtworkAdjustmentPasses", Clamp(bulb.ArtworkAdjustmentPasses, 1, 12), RegistryValueKind.DWord)
                    End If
                End Using
            Catch
                ' Remembered profiles are optional and must never interrupt editing.
            End Try
        End Sub

        ' Older builds exposed a disable option. Automatic profiles intentionally
        ' remain enabled, so this is now a harmless compatibility method.
        Public Sub Disable()
        End Sub

        Public Sub ApplyTo(ByVal bulb As BulbInfo)
            If bulb Is Nothing Then Return

            Dim path As String = ProfilePath(bulb)
            If ReadInteger(path, "Saved", 0) <> 1 Then Return

            bulb.GlowSpread = Clamp(ReadInteger(path, "Spread", bulb.GlowSpread), 0, 1000)
            bulb.GlowSoftness = Clamp(ReadInteger(path, "Feather", bulb.GlowSoftness), 0, 200)
            bulb.GlowFalloff = Clamp(ReadInteger(path, "Contrast", bulb.GlowFalloff), 0, 100)
            bulb.GlowIntensity = Clamp(ReadInteger(path, "Intensity", bulb.GlowIntensity),
                                       0, If(IsFlasherProfile(bulb), 1600, 800))
            bulb.LightDiffusion = Clamp(ReadInteger(path, "LightDiffusion", bulb.LightDiffusion), 0, 300)
            bulb.LightTemperature = Clamp(ReadInteger(path, "LightTemperature", If(bulb.LightTemperature <= 0, 4000, bulb.LightTemperature)), 2000, 6500)

            If IsFlasherProfile(bulb) Then
                bulb.ArtworkContrast = Clamp(ReadInteger(path, "TransmissionContrast", If(bulb.ArtworkContrast <= 0, 140, bulb.ArtworkContrast)), 0, 300)
                bulb.FlasherStyle = Clamp(ReadInteger(path, "FlasherStyle", bulb.FlasherStyle), 0, 2)
                bulb.FlasherSaturation = Clamp(ReadInteger(path, "FlasherSaturation", bulb.FlasherSaturation), 0, 200)
                bulb.FlasherHighlightProtection = Clamp(ReadInteger(path, "FlasherHighlightProtection", bulb.FlasherHighlightProtection), 0, 100)
                bulb.FlasherDarkAreaLift = Clamp(ReadInteger(path, "FlasherDarkAreaLift", bulb.FlasherDarkAreaLift), 0, 100)
                bulb.FlasherHotspotX = Clamp(ReadInteger(path, "FlasherHotspotX", bulb.FlasherHotspotX), 0, 100)
                bulb.FlasherHotspotY = Clamp(ReadInteger(path, "FlasherHotspotY", bulb.FlasherHotspotY), 0, 100)
                bulb.MaskRadius = Clamp(ReadInteger(path, "MaskRadius", bulb.MaskRadius), 0, 100)
                bulb.MaskSmartRadius = (ReadInteger(path, "MaskSmartRadius", If(bulb.MaskSmartRadius, 1, 0)) = 1)
                bulb.MaskSmooth = Clamp(ReadInteger(path, "MaskSmooth", bulb.MaskSmooth), 0, 100)
                bulb.MaskFeather = Clamp(ReadInteger(path, "MaskFeather", bulb.MaskFeather), 0, 250)
                bulb.MaskContrast = Clamp(ReadInteger(path, "MaskContrast", bulb.MaskContrast), 0, 100)
                bulb.MaskShiftEdge = Clamp(ReadInteger(path, "MaskShiftEdge", bulb.MaskShiftEdge), -100, 100)
                bulb.ArtworkAdjustmentPasses = Clamp(ReadInteger(path, "ArtworkAdjustmentPasses", Math.Max(1, bulb.ArtworkAdjustmentPasses)), 1, 12)
            End If

            bulb.IsIlluminatedImageDirty = True
        End Sub

        Public Function IsFlasherProfile(ByVal bulb As BulbInfo) As Boolean
            If bulb Is Nothing Then Return False
            Return bulb.IlluMode = eIlluMode.Flasher OrElse bulb.LightPurpose = eLightPurpose.Flasher
        End Function

        Private Function ProfilePath(ByVal bulb As BulbInfo) As String
            Return RegistryRoot & If(IsFlasherProfile(bulb), "\Flasher", "\Light")
        End Function

        Private Function Clamp(ByVal value As Integer, ByVal minimum As Integer, ByVal maximum As Integer) As Integer
            Return Math.Max(minimum, Math.Min(maximum, value))
        End Function

        Private Function ReadInteger(ByVal path As String, ByVal name As String, ByVal fallback As Integer) As Integer
            Try
                Using key As RegistryKey = Registry.CurrentUser.OpenSubKey(path, False)
                    If key Is Nothing Then Return fallback
                    Return Convert.ToInt32(key.GetValue(name, fallback))
                End Using
            Catch
                Return fallback
            End Try
        End Function
    End Module
End Namespace
