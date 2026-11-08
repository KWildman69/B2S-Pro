Imports System.Drawing

' The shared server simulation runs against these in-memory preview bodies.
' No COM server, registry, ROM connection or switch bridge is started.
Namespace PhysicsPreview
    Public Class B2SPictureBox
        Public RectangleF As RectangleF
        Public Left As Integer
        Public Top As Integer
        Public Width As Integer
        Public Height As Integer
        Public Visible As Boolean
        Public RotationAngle As Single
        Public RotationPivotX As Single
        Public RotationPivotY As Single
        Public RotationTipX As Single
        Public RotationTipY As Single
        Public PivotRandomStrength As Single
        Public PivotDownAngle As Single
        Public PivotAutomaticOscillation As Boolean
        Public PivotPhysicsEnabled As Boolean
        Public PivotPhysicsWeight As Single = 1.0F
        Public PivotPhysicsFriction As Single = 1.25F
        Public PivotPhysicsGrip As Single = 0.5F
        Public PivotPhysicsResponseMode As Integer
        Public PivotPhysicsKnockdownAngle As Single = 90.0F
        Public PivotPhysicsKnockdownDuration As Integer = 180
        Public PivotPhysicsResetDelay As Integer = 1000
        Public PhysicsCollisionMaskAlpha As Byte()
        Public PhysicsCollisionMaskSize As Size = Size.Empty
        Public PhysicsCollisionContour As PointF()
        Public PhysicsAngularVelocity As Single
        Public PhysicsPreviousAngle As Single
        Private pivotPhysicsResponseState As Integer
        Private pivotPhysicsResponseElapsed As Double
        Private pivotPhysicsResponseStartAngle As Single
        Private pivotPhysicsResponseRestAngle As Single
        Public PreservePhysicsArtworkAspect As Boolean
        Public RollEnabled As Boolean
        Public RollAngle As Single
        Public ReadOnly Property MotionPathCenter As PointF
            Get
                Return New PointF(RectangleF.X + RectangleF.Width / 2.0F, RectangleF.Y + RectangleF.Height / 2.0F)
            End Get
        End Property
        Public Sub SetMotionPathPosition(ByVal center As PointF, Optional ByVal advanceRoll As Boolean = False)
            If advanceRoll AndAlso RollEnabled Then
                Dim previous As PointF = MotionPathCenter
                Dim dx As Single = center.X - previous.X, dy As Single = center.Y - previous.Y
                Dim distance As Double = Math.Sqrt(dx * dx + dy * dy)
                Dim direction As Single = If(Math.Abs(dx) >= Math.Abs(dy), Math.Sign(dx), Math.Sign(dy))
                Dim radius As Single = Math.Max(1.0F, Math.Min(RectangleF.Width, RectangleF.Height) / 2.0F)
                If distance > 0.0001R AndAlso direction <> 0.0F Then
                    RollAngle = CSng((RollAngle + direction * distance / radius * 180.0R / Math.PI) Mod 360.0R)
                End If
            End If
            RectangleF = New RectangleF(center.X - RectangleF.Width / 2.0F, center.Y - RectangleF.Height / 2.0F,
                                        RectangleF.Width, RectangleF.Height)
        End Sub
        Public Sub ApplyPivotPhysicsAngularImpulse(ByVal deltaDegreesPerSecond As Single)
            If Not PivotPhysicsEnabled OrElse Single.IsNaN(deltaDegreesPerSecond) OrElse Single.IsInfinity(deltaDegreesPerSecond) Then Return
            If PivotPhysicsResponseMode > 0 Then
                If pivotPhysicsResponseState <> 0 Then Return
                PhysicsAngularVelocity = 0.0F
                pivotPhysicsResponseRestAngle = RotationAngle
                pivotPhysicsResponseStartAngle = RotationAngle
                pivotPhysicsResponseElapsed = 0.0R
                pivotPhysicsResponseState = 1
                Return
            End If
            PhysicsAngularVelocity = Math.Max(-2160.0F, Math.Min(2160.0F, PhysicsAngularVelocity + deltaDegreesPerSecond))
        End Sub
        Public Sub AdvancePhysicsPivot(ByVal elapsed As Double)
            If Not PivotPhysicsEnabled OrElse elapsed <= 0.0R Then Return
            PhysicsPreviousAngle = RotationAngle
            If PivotPhysicsResponseMode > 0 Then
                AdvancePivotPhysicsResponse(elapsed)
                PhysicsAngularVelocity = CSng((RotationAngle - PhysicsPreviousAngle) / elapsed)
                Return
            End If
            RotationAngle = CSng((RotationAngle + PhysicsAngularVelocity * elapsed) Mod 360.0R)
            Dim damping As Double = Math.Exp(-Math.Max(0.0F, PivotPhysicsFriction) * elapsed)
            PhysicsAngularVelocity = CSng(PhysicsAngularVelocity * damping)
            If Math.Abs(PhysicsAngularVelocity) < 0.01F Then PhysicsAngularVelocity = 0.0F
        End Sub
        Private Sub AdvancePivotPhysicsResponse(ByVal elapsed As Double)
            If pivotPhysicsResponseState = 0 OrElse pivotPhysicsResponseState = 2 Then Return
            pivotPhysicsResponseElapsed += elapsed * 1000.0R
            If pivotPhysicsResponseState = 1 Then
                Dim duration As Double = Math.Max(10, Math.Min(5000, PivotPhysicsKnockdownDuration))
                Dim progress As Single = CSng(Math.Min(1.0R, pivotPhysicsResponseElapsed / duration))
                Dim eased As Single = progress * progress * (3.0F - 2.0F * progress)
                RotationAngle = pivotPhysicsResponseStartAngle + (PivotPhysicsKnockdownAngle - pivotPhysicsResponseStartAngle) * eased
                If progress >= 1.0F Then
                    RotationAngle = PivotPhysicsKnockdownAngle
                    pivotPhysicsResponseElapsed = 0.0R
                    pivotPhysicsResponseState = If(PivotPhysicsResponseMode = 2, 3, 2)
                End If
                Return
            End If
            If pivotPhysicsResponseState = 3 Then
                If pivotPhysicsResponseElapsed < Math.Max(0, Math.Min(30000, PivotPhysicsResetDelay)) Then Return
                pivotPhysicsResponseStartAngle = RotationAngle
                pivotPhysicsResponseElapsed = 0.0R
                pivotPhysicsResponseState = 4
            End If
            If pivotPhysicsResponseState = 4 Then
                Dim duration As Double = Math.Max(10, Math.Min(5000, PivotPhysicsKnockdownDuration))
                Dim progress As Single = CSng(Math.Min(1.0R, pivotPhysicsResponseElapsed / duration))
                Dim eased As Single = progress * progress * (3.0F - 2.0F * progress)
                RotationAngle = pivotPhysicsResponseStartAngle + (pivotPhysicsResponseRestAngle - pivotPhysicsResponseStartAngle) * eased
                If progress >= 1.0F Then
                    RotationAngle = pivotPhysicsResponseRestAngle
                    pivotPhysicsResponseElapsed = 0.0R
                    pivotPhysicsResponseState = 0
                End If
            End If
        End Sub
    End Class

    Public Partial Class B2SData
        Implements IDisposable
        Private state As PhysicsBallState
        Private Shared activePreview As B2SData
        Public ReadOnly Body As New B2SPictureBox()
        Public Pivot As B2SPictureBox
        Public ReadOnly Pivots As New Dictionary(Of String, B2SPictureBox)(StringComparer.OrdinalIgnoreCase)
        Public Event SwitchHit(ByVal switchID As Integer)
        Public Event SoundHit(ByVal soundName As String, ByVal soundVolume As Single)

        Private Shared Function FindPivotPicture(ByVal name As String) As B2SPictureBox
            If activePreview Is Nothing OrElse String.IsNullOrEmpty(name) Then Return Nothing
            Dim result As B2SPictureBox = Nothing
            If activePreview.Pivots.TryGetValue(name, result) Then Return result
            Return activePreview.Pivot
        End Function
        Private Shared Sub PulsePhysicsSwitch(ByVal id As Integer)
            If activePreview IsNot Nothing Then activePreview.ReportSwitch(id)
        End Sub
        Private Shared Sub PlayPhysicsSound(ByVal soundName As String, ByVal soundVolume As Single)
            If activePreview IsNot Nothing Then activePreview.ReportSound(soundName, soundVolume)
        End Sub
        Private Sub ReportSwitch(ByVal id As Integer)
            RaiseEvent SwitchHit(id)
        End Sub
        Private Sub ReportSound(ByVal soundName As String, ByVal soundVolume As Single)
            RaiseEvent SoundHit(soundName, soundVolume)
        End Sub

        Public Sub Configure(ByVal ballBounds As Rectangle, ByVal playBounds As RectangleF,
                             ByVal gravity As Single, ByVal strength As Single, ByVal bounce As Single,
                             ByVal paths As List(Of List(Of PointF)), ByVal obstacles As List(Of RectangleF),
                             ByVal zones As List(Of PhysicsSwitchZone), ByVal launcher As PhysicsLauncher,
                             ByVal segmentBounces As List(Of List(Of Single)), ByVal reset As Boolean,
                             Optional ByVal obstacleBounces As List(Of Single) = Nothing,
                             Optional ByVal pivotTargetNames As List(Of String) = Nothing,
                             Optional ByVal primaryPivotName As String = Nothing)
            Body.Left = ballBounds.Left : Body.Top = ballBounds.Top
            Body.Width = ballBounds.Width : Body.Height = ballBounds.Height
            If reset OrElse state Is Nothing Then
                Body.RectangleF = ballBounds
                Body.RollAngle = 0.0F
            End If
            Dim replacement As New PhysicsBallState(Body, If(primaryPivotName, String.Empty), playBounds,
                                                    gravity, strength, bounce, paths, obstacles, zones, launcher, segmentBounces, obstacleBounces, pivotTargetNames)
            If state IsNot Nothing Then
                If Not reset Then replacement.KeepPreviewMotion(state)
                state.Dispose()
            End If
            state = replacement
        End Sub
        Public ReadOnly Property CanFire As Boolean
            Get
                Return state IsNot Nothing AndAlso state.PreviewArmed
            End Get
        End Property
        Public Sub Advance(ByVal elapsed As Double)
            Dim previous As B2SData = activePreview
            Try
                activePreview = Me
                If state IsNot Nothing Then state.AdvancePreview(elapsed)
            Finally
                activePreview = previous
            End Try
        End Sub
        Public Sub Fire()
            Dim previous As B2SData = activePreview
            Try
                activePreview = Me
                If state IsNot Nothing Then state.Launch()
            Finally
                activePreview = previous
            End Try
        End Sub
        Public Sub ReleaseAtCurrentPosition()
            If state IsNot Nothing Then state.ActivateAtCurrentPosition()
        End Sub
        Public Sub Dispose() Implements IDisposable.Dispose
            If state IsNot Nothing Then state.Dispose() : state = Nothing
        End Sub
    End Class
End Namespace
