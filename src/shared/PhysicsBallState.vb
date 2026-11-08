Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.Drawing

#If PHYSICS_PREVIEW Then
Namespace PhysicsPreview
#End If
Public Partial Class B2SData
    Public Shared Function BuildPhysicsContour(ByVal sourceAlpha As Byte(),
                                               ByVal maskWidth As Integer,
                                               ByVal maskHeight As Integer,
                                               ByVal normalizedPivotX As Single,
                                               ByVal normalizedPivotY As Single) As PointF()
        If sourceAlpha Is Nothing OrElse maskWidth <= 0 OrElse maskHeight <= 0 OrElse
           sourceAlpha.Length < maskWidth * maskHeight Then Return Nothing

        Const angularBinCount As Integer = 720
        Dim outerRadius(angularBinCount - 1) As Single
        Dim pivotX As Single = CSng(Math.Max(0.0R, Math.Min(1.0R, normalizedPivotX))) * (maskWidth - 1)
        Dim pivotY As Single = CSng(Math.Max(0.0R, Math.Min(1.0R, normalizedPivotY))) * (maskHeight - 1)
        Dim radiansToBin As Double = angularBinCount / (Math.PI * 2.0R)

        For y As Integer = 0 To maskHeight - 1
            For x As Integer = 0 To maskWidth - 1
                If sourceAlpha(y * maskWidth + x) = 0 Then Continue For
                Dim deltaX As Single = x - pivotX
                Dim deltaY As Single = y - pivotY
                Dim angle As Double = Math.Atan2(deltaY, deltaX)
                If angle < 0.0R Then angle += Math.PI * 2.0R
                Dim angleBin As Integer = CInt(Math.Floor(angle * radiansToBin))
                If angleBin >= angularBinCount Then angleBin = 0
                Dim radius As Single = CSng(Math.Sqrt(deltaX * deltaX + deltaY * deltaY))
                If radius > outerRadius(angleBin) Then outerRadius(angleBin) = radius
            Next
        Next

        ' Discrete source pixels can leave an occasional empty angular bin.
        ' Interpolate only those bins from the nearest real outline samples.
        For angleBin As Integer = 0 To angularBinCount - 1
            If outerRadius(angleBin) > 0.0F Then Continue For
            Dim previousDistance As Integer = 1
            While previousDistance < angularBinCount AndAlso
                  outerRadius((angleBin - previousDistance + angularBinCount) Mod angularBinCount) <= 0.0F
                previousDistance += 1
            End While
            Dim nextDistance As Integer = 1
            While nextDistance < angularBinCount AndAlso
                  outerRadius((angleBin + nextDistance) Mod angularBinCount) <= 0.0F
                nextDistance += 1
            End While
            If previousDistance < angularBinCount AndAlso nextDistance < angularBinCount Then
                Dim previousRadius As Single = outerRadius((angleBin - previousDistance + angularBinCount) Mod angularBinCount)
                Dim nextRadius As Single = outerRadius((angleBin + nextDistance) Mod angularBinCount)
                outerRadius(angleBin) = (previousRadius * nextDistance + nextRadius * previousDistance) /
                                        (previousDistance + nextDistance)
            End If
        Next

        Dim contour(angularBinCount - 1) As PointF
        For angleBin As Integer = 0 To angularBinCount - 1
            Dim angle As Double = angleBin / radiansToBin
            contour(angleBin) = New PointF(CSng(pivotX + Math.Cos(angle) * outerRadius(angleBin)),
                                           CSng(pivotY + Math.Sin(angle) * outerRadius(angleBin)))
        Next
        Return contour
    End Function

    Public Class PhysicsSwitchZone
        Public Bounds As RectangleF
        Public SwitchID As Integer
        Public Angle As Single
        Public SoundName As String = String.Empty
        Public SoundVolume As Single = 1.0F
    End Class
    Public Class PhysicsLauncher
        Public TriggerType As Integer
        Public TriggerID As Integer
        Public Origin As PointF
        Public Angle As Single
        Public Strength As Single
        Public RandomAngle As Single
        Public RandomStrength As Single
        Public CaptureRadius As Single
        Public FollowPivot As Boolean
    End Class

    Private Class PhysicsBallState
        Implements IDisposable

        Private Structure PivotContourContact
            Public ContactX As Single
            Public ContactY As Single
            Public NormalX As Single
            Public NormalY As Single
            Public Penetration As Single
            Public Distance As Single
            Public SegmentIndex As Integer
        End Structure

        Public Event LauncherCaptured As EventHandler

        Private Const PhysicsSubstepSeconds As Single = 0.001F
        Private Const ShallowSurfaceSlopeLimit As Single = 0.35F
        Private Const ShallowSurfaceRollMultiplier As Single = 2.0F
        Private Const BoundaryScatterDegrees As Single = 10.0F
        Private Const BoundaryScatterMinimumImpactSpeed As Single = 150.0F
        Private ReadOnly ball As B2SPictureBox
        Private ReadOnly flipperName As String
        Private ReadOnly impactPivotNames As New Generic.List(Of String)()
        Private ReadOnly activePivotSilhouetteContacts As New Generic.HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly bounds As RectangleF
        Private ReadOnly gravity As Single
        Private ReadOnly flipperStrength As Single
        Private ReadOnly boundaryBounce As Single
        Private ReadOnly boundaryPaths As New Generic.List(Of Generic.List(Of PointF))()
        Private ReadOnly boundarySegmentBounces As New Generic.List(Of Generic.List(Of Single))()
        Private ReadOnly obstacles As New Generic.List(Of RectangleF)()
        Private ReadOnly obstacleBounces As New Generic.List(Of Single)()
        Private ReadOnly switchZones As New Generic.List(Of PhysicsSwitchZone)()
        Private ReadOnly switchZoneInside As New Generic.List(Of Boolean)()
        Private ReadOnly launcher As PhysicsLauncher
        Private Shared ReadOnly launcherRandom As New Random()
        Private Shared boundaryScatterRandom As New Random()
        Private Shared ReadOnly flipperStrengthRandom As New Random()
        Private launcherArmed As Boolean = True
        Private launcherHolding As Boolean
        Private launcherExitedCapture As Boolean
        Private ReadOnly authoredBallWidth As Single
        Private ReadOnly authoredBallHeight As Single
        Private ReadOnly authoredBallCenter As PointF
        Private ReadOnly timer As New Windows.Forms.Timer() With {.Interval = 16}
        Private ReadOnly clock As New Diagnostics.Stopwatch()
        Private pendingPhysicsSeconds As Double
        Private physicsActive As Boolean = True
        Private Const MaxPhysicsStepsPerTick As Integer = 250
        Private velocity As PointF = PointF.Empty
        ' Simulation positions and velocities stay in the saved backglass space.
        ' Screen scaling is applied only when presenting the ball.
        Private physicsCenter As PointF
        Private lastFlipperAngle As Single
        Private hasLastFlipperAngle As Boolean
        Public Sub New(ByVal pictureBox As B2SPictureBox, ByVal pivotName As String, ByVal playBounds As RectangleF,
                       ByVal gravityValue As Single, ByVal strengthValue As Single, ByVal bounceValue As Single,
                        ByVal returnBoundaries As Generic.List(Of Generic.List(Of PointF)),
                        ByVal returnObstacles As Generic.List(Of RectangleF),
                        ByVal returnSwitchZones As Generic.List(Of PhysicsSwitchZone),
                        ByVal returnLauncher As PhysicsLauncher,
                        ByVal returnBoundarySegmentBounces As Generic.List(Of Generic.List(Of Single)),
                        Optional ByVal returnObstacleBounces As Generic.List(Of Single) = Nothing,
                        Optional ByVal returnImpactPivotNames As Generic.IEnumerable(Of String) = Nothing)
            ball = pictureBox
            flipperName = pivotName
            If returnImpactPivotNames IsNot Nothing Then
                For Each name As String In returnImpactPivotNames
                    Dim normalized As String = If(name, String.Empty).Trim()
                    If normalized.Length > 0 AndAlso Not impactPivotNames.Any(Function(existing) String.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)) Then
                        impactPivotNames.Add(normalized)
                    End If
                Next
            ElseIf Not String.IsNullOrWhiteSpace(flipperName) Then
                ' Legacy files contain only PhysicsFlipperName.
                impactPivotNames.Add(flipperName.Trim())
            End If
            bounds = playBounds
            gravity = gravityValue
            flipperStrength = strengthValue
            boundaryBounce = bounceValue
            authoredBallWidth = If(pictureBox.Width > 0, pictureBox.Width, pictureBox.RectangleF.Width)
            authoredBallHeight = If(pictureBox.Height > 0, pictureBox.Height, pictureBox.RectangleF.Height)
            ' RectangleF is initialized later by screen scaling. The control
            ' bounds already contain the ball's saved editor placement here.
            authoredBallCenter = New PointF(pictureBox.Left + pictureBox.Width / 2.0F,
                                            pictureBox.Top + pictureBox.Height / 2.0F)
            physicsCenter = authoredBallCenter
            If returnBoundaries IsNot Nothing Then
                For pathIndex As Integer = 0 To returnBoundaries.Count - 1
                    Dim path As Generic.List(Of PointF) = returnBoundaries(pathIndex)
                    If path IsNot Nothing AndAlso path.Count >= 2 Then
                        boundaryPaths.Add(New Generic.List(Of PointF)(path))
                        Dim values As New Generic.List(Of Single)()
                        Dim saved As Generic.List(Of Single) = If(returnBoundarySegmentBounces IsNot Nothing AndAlso
                                                                  pathIndex < returnBoundarySegmentBounces.Count,
                                                                  returnBoundarySegmentBounces(pathIndex), Nothing)
                        For segmentIndex As Integer = 0 To path.Count - 2
                            Dim value As Single = If(saved IsNot Nothing AndAlso segmentIndex < saved.Count, saved(segmentIndex), -1.0F)
                            values.Add(Math.Max(-1.0F, Math.Min(3.0F, value)))
                        Next
                        boundarySegmentBounces.Add(values)
                    End If
                Next
            End If
            If returnObstacles IsNot Nothing Then
                For index As Integer = 0 To returnObstacles.Count - 1
                    Dim obstacle As RectangleF = returnObstacles(index)
                    If obstacle.Width > 0.0F AndAlso obstacle.Height > 0.0F Then
                        obstacles.Add(obstacle)
                        Dim value As Single = If(returnObstacleBounces IsNot Nothing AndAlso index < returnObstacleBounces.Count, returnObstacleBounces(index), -1.0F)
                        obstacleBounces.Add(If(Single.IsNaN(value) OrElse Single.IsInfinity(value) OrElse value < 0.0F, boundaryBounce, Math.Min(3.0F, value)))
                    End If
                Next
            End If
            If returnSwitchZones IsNot Nothing Then
                For Each zone As PhysicsSwitchZone In returnSwitchZones
                    If zone IsNot Nothing AndAlso zone.Bounds.Width > 0.0F AndAlso zone.Bounds.Height > 0.0F AndAlso
                       (zone.SwitchID > 0 OrElse Not String.IsNullOrWhiteSpace(zone.SoundName)) Then
                        switchZones.Add(New PhysicsSwitchZone With {.Bounds = zone.Bounds, .SwitchID = Math.Max(0, zone.SwitchID),
                            .Angle = NormalizeSwitchAngle(zone.Angle), .SoundName = If(zone.SoundName, String.Empty).Trim(),
                            .SoundVolume = Math.Max(0.0F, Math.Min(2.0F, zone.SoundVolume))})
                        switchZoneInside.Add(False)
                    End If
                Next
            End If
            If returnLauncher IsNot Nothing Then
                launcher = New PhysicsLauncher With {.TriggerType = returnLauncher.TriggerType, .TriggerID = returnLauncher.TriggerID,
                    .Origin = returnLauncher.Origin, .Angle = returnLauncher.Angle, .Strength = returnLauncher.Strength,
                    .RandomAngle = returnLauncher.RandomAngle, .RandomStrength = returnLauncher.RandomStrength,
                    .CaptureRadius = Math.Max(5.0F, returnLauncher.CaptureRadius), .FollowPivot = returnLauncher.FollowPivot}
                launcherHolding = True
            End If
            AddHandler timer.Tick, AddressOf Tick
        End Sub

        Public Sub Launch()
            If launcher Is Nothing OrElse Not launcherArmed Then Return
            Dim scaleX As Single = If(authoredBallWidth <= 0.0F, 1.0F, ball.RectangleF.Width / authoredBallWidth)
            Dim scaleY As Single = If(authoredBallHeight <= 0.0F, 1.0F, ball.RectangleF.Height / authoredBallHeight)
            Dim randomAngleOffset As Single = CSng((launcherRandom.NextDouble() * 2.0R - 1.0R) * launcher.RandomAngle)
            Dim randomStrengthFactor As Single = 1.0F + CSng((launcherRandom.NextDouble() * 2.0R - 1.0R) * launcher.RandomStrength / 100.0R)
            Dim origin As PointF = PointF.Empty, angle As Single = 0.0F
            GetLauncherPose(origin, angle)
            Dim radians As Double = (angle + randomAngleOffset) * Math.PI / 180.0R
            Dim launchStrength As Single = Math.Max(0.0F, launcher.Strength * randomStrengthFactor)
            physicsCenter = origin
            ball.SetMotionPathPosition(New PointF(origin.X * scaleX, origin.Y * scaleY))
            velocity = New PointF(CSng(Math.Cos(radians)) * launchStrength, CSng(Math.Sin(radians)) * launchStrength)
            launcherArmed = False
            launcherHolding = False
            launcherExitedCapture = False
            activePivotSilhouetteContacts.Clear()
            For index As Integer = 0 To switchZoneInside.Count - 1
                switchZoneInside(index) = False
            Next
        End Sub

        Public Sub Start()
            physicsActive = True
            ball.Visible = True
            pendingPhysicsSeconds = 0.0R
#If PHYSICS_PREVIEW Then
            clock.Restart()
            timer.Start()
#Else
            StartPhysicsClock()
#End If
        End Sub

#If Not PHYSICS_PREVIEW Then
        Public ReadOnly Property PhysicsPivot As B2SPictureBox
            Get
                Return FindPivotPicture(flipperName)
            End Get
        End Property

        Public ReadOnly Property PhysicsPivots As Generic.List(Of B2SPictureBox)
            Get
                Dim result As New Generic.List(Of B2SPictureBox)()
                For Each targetName As String In impactPivotNames
                    Dim pivot As B2SPictureBox = FindPivotPicture(targetName)
                    If pivot IsNot Nothing AndAlso Not result.Contains(pivot) Then result.Add(pivot)
                Next
                Dim launcherPivot As B2SPictureBox = PhysicsPivot
                If launcherPivot IsNot Nothing AndAlso Not result.Contains(launcherPivot) Then result.Add(launcherPivot)
                Return result
            End Get
        End Property

        Public Sub AdvanceServerStep()
            If Not physicsActive Then Return
            If ball.RectangleF.Width <= 0.0F OrElse ball.RectangleF.Height <= 0.0F Then Return
            If Not hasLastFlipperAngle AndAlso PhysicsPivot IsNot Nothing Then
                lastFlipperAngle = PhysicsPivot.PhysicsPreviousAngle
                hasLastFlipperAngle = True
            End If
            AdvancePhysics(0.001R)
        End Sub

        Public Sub PresentServerFrame()
            If Not physicsActive Then Return
            Dim scaleX As Single = If(authoredBallWidth <= 0.0F, 1.0F, ball.RectangleF.Width / authoredBallWidth)
            Dim scaleY As Single = If(authoredBallHeight <= 0.0F, 1.0F, ball.RectangleF.Height / authoredBallHeight)
            ball.SetMotionPathPosition(New PointF(physicsCenter.X * scaleX, physicsCenter.Y * scaleY), True)
        End Sub
#End If

        Private Sub Tick(ByVal sender As Object, ByVal e As EventArgs)
            Dim elapsed As Double = clock.Elapsed.TotalSeconds
            clock.Restart()
            If ball.RectangleF.Width <= 0.0F OrElse ball.RectangleF.Height <= 0.0F Then Return
            AdvancePhysics(elapsed)
        End Sub

        Private Sub AdvancePhysics(ByVal elapsed As Double)
            If Not physicsActive Then Return
            If elapsed <= 0.0R OrElse Double.IsNaN(elapsed) OrElse Double.IsInfinity(elapsed) Then Return
            pendingPhysicsSeconds += elapsed
            Dim flipper As B2SPictureBox = FindPivotPicture(flipperName)
            Dim angularVelocity As Single = 0.0F
            If flipper IsNot Nothing AndAlso impactPivotNames.Any(Function(name) String.Equals(name, flipperName, StringComparison.OrdinalIgnoreCase)) Then
                If hasLastFlipperAngle Then angularVelocity = CSng((flipper.RotationAngle - lastFlipperAngle) * Math.PI / 180.0F / elapsed)
                lastFlipperAngle = flipper.RotationAngle
                hasLastFlipperAngle = True
            End If
            ' Use the same integration step on fast and slow PCs. Retain elapsed
            ' time rather than dropping everything beyond 40 ms. Cap work per
            ' callback so a delayed window cannot monopolize the message loop.
            Dim steps As Integer = CInt(Math.Min(MaxPhysicsStepsPerTick,
                                                Math.Floor((pendingPhysicsSeconds + 0.000000001R) / 0.001R)))
            For index As Integer = 1 To steps
                StepPhysics(PhysicsSubstepSeconds, flipper, angularVelocity)
            Next
            pendingPhysicsSeconds = Math.Max(0.0R, pendingPhysicsSeconds - steps * 0.001R)
        End Sub

        Public Sub ActivateAtCurrentPosition()
            Dim scaleX As Single = If(authoredBallWidth <= 0.0F OrElse ball.RectangleF.Width <= 0.0F, 1.0F, ball.RectangleF.Width / authoredBallWidth)
            Dim scaleY As Single = If(authoredBallHeight <= 0.0F OrElse ball.RectangleF.Height <= 0.0F, 1.0F, ball.RectangleF.Height / authoredBallHeight)
            Dim runtimeCenter As PointF = ball.MotionPathCenter
            physicsCenter = New PointF(runtimeCenter.X / scaleX, runtimeCenter.Y / scaleY)
            velocity = PointF.Empty
            pendingPhysicsSeconds = 0.0R
            launcherHolding = False
            launcherArmed = False
            hasLastFlipperAngle = False
            activePivotSilhouetteContacts.Clear()
            physicsActive = True
            ball.Visible = True
        End Sub

#If Not PHYSICS_PREVIEW Then
        Public Sub DeactivateForMotionPath()
            physicsActive = False
            velocity = PointF.Empty
            pendingPhysicsSeconds = 0.0R
            hasLastFlipperAngle = False
            launcherHolding = False
            launcherArmed = False
            activePivotSilhouetteContacts.Clear()
        End Sub

#End If

#If PHYSICS_PREVIEW Then
        Public Sub AdvancePreview(ByVal elapsed As Double)
            AdvancePhysics(elapsed)
        End Sub

        Public ReadOnly Property PreviewArmed As Boolean
            Get
                Return launcher Is Nothing OrElse launcherArmed
            End Get
        End Property

        Public Sub KeepPreviewMotion(ByVal previous As PhysicsBallState)
            velocity = previous.velocity
            physicsCenter = previous.physicsCenter
            pendingPhysicsSeconds = previous.pendingPhysicsSeconds
            If (launcher Is Nothing) = (previous.launcher Is Nothing) Then
                launcherArmed = previous.launcherArmed
                launcherHolding = previous.launcherHolding
                launcherExitedCapture = previous.launcherExitedCapture
            End If
            lastFlipperAngle = previous.lastFlipperAngle
            hasLastFlipperAngle = previous.hasLastFlipperAngle
            For Each targetName As String In previous.activePivotSilhouetteContacts
                activePivotSilhouetteContacts.Add(targetName)
            Next
            For index As Integer = 0 To Math.Min(switchZoneInside.Count, previous.switchZoneInside.Count) - 1
                If switchZones(index).SwitchID = previous.switchZones(index).SwitchID AndAlso
                   switchZones(index).Bounds = previous.switchZones(index).Bounds AndAlso
                   switchZones(index).Angle = previous.switchZones(index).Angle Then
                    switchZoneInside(index) = previous.switchZoneInside(index)
                End If
            Next
        End Sub
#End If

        Private Sub StepPhysics(ByVal elapsed As Single, ByVal flipper As B2SPictureBox, ByVal angularVelocity As Single)
            Dim center As PointF = physicsCenter
            Dim scaleX As Single = If(authoredBallWidth <= 0.0F, 1.0F, ball.RectangleF.Width / authoredBallWidth)
            Dim scaleY As Single = If(authoredBallHeight <= 0.0F, 1.0F, ball.RectangleF.Height / authoredBallHeight)
            If launcherHolding AndAlso launcher IsNot Nothing Then
                Dim holdCenter As PointF = PointF.Empty, angle As Single = 0.0F
                GetLauncherPose(holdCenter, angle)
                physicsCenter = holdCenter
#If PHYSICS_PREVIEW Then
                ball.SetMotionPathPosition(New PointF(holdCenter.X * scaleX, holdCenter.Y * scaleY))
#End If
                velocity = PointF.Empty
                Return
            End If
            Dim runtimeBounds As RectangleF = bounds
            Dim previousCenter As PointF = center
            velocity.Y += gravity * elapsed
            center.X += velocity.X * elapsed
            center.Y += velocity.Y * elapsed
            Dim radius As Single = Math.Max(2.0F, Math.Min(authoredBallWidth, authoredBallHeight) * 0.43F)
            If center.X - radius < runtimeBounds.Left Then center.X = runtimeBounds.Left + radius : velocity.X = Math.Abs(velocity.X) * 0.55F
            If center.X + radius > runtimeBounds.Right Then center.X = runtimeBounds.Right - radius : velocity.X = -Math.Abs(velocity.X) * 0.55F
            If center.Y - radius < runtimeBounds.Top Then center.Y = runtimeBounds.Top + radius : velocity.Y = Math.Abs(velocity.Y) * 0.55F
            If center.Y + radius > runtimeBounds.Bottom Then center.Y = runtimeBounds.Bottom - radius : velocity.Y = -Math.Abs(velocity.Y) * 0.55F
            If flipper IsNot Nothing Then
                ' A physics-impact pivot is a physical collision object even when
                ' the launcher follows that pivot. The FollowPivot setting only
                ' controls the launcher's pose; it must not remove the object after
                ' the ball is released. Keep the legacy attached-launcher/flipper
                ' exception unchanged for pivots that do not opt into physics.
                If flipper.PivotPhysicsEnabled Then
                    ResolvePivotObjectCollision(center, previousCenter, radius, velocity, flipper, flipperName, boundaryBounce)
                ElseIf launcher Is Nothing OrElse Not launcher.FollowPivot Then
                    ResolveFlipperCollision(center, radius, velocity, flipper, angularVelocity)
                End If
            End If
            For Each targetName As String In impactPivotNames
                If String.Equals(targetName, flipperName, StringComparison.OrdinalIgnoreCase) Then Continue For
                Dim target As B2SPictureBox = FindPivotPicture(targetName)
                If target IsNot Nothing AndAlso target.PivotPhysicsEnabled Then
                    ResolvePivotObjectCollision(center, previousCenter, radius, velocity, target, targetName, boundaryBounce)
                End If
            Next
            For pathIndex As Integer = 0 To boundaryPaths.Count - 1
                Dim path As Generic.List(Of PointF) = boundaryPaths(pathIndex)
                For segmentIndex As Integer = 0 To path.Count - 2
                    Dim fromPoint As PointF = path(segmentIndex)
                    Dim toPoint As PointF = path(segmentIndex + 1)
                    Dim segmentBounce As Single = boundaryBounce
                    If pathIndex < boundarySegmentBounces.Count AndAlso segmentIndex < boundarySegmentBounces(pathIndex).Count AndAlso
                       boundarySegmentBounces(pathIndex)(segmentIndex) >= 0.0F Then
                        segmentBounce = boundarySegmentBounces(pathIndex)(segmentIndex)
                    End If
                    Dim beforeCollisionCenter As PointF = center
                    Dim beforeCollisionVelocity As PointF = velocity
                    ResolveStaticFloorCollision(center, previousCenter, radius, velocity, fromPoint, toPoint, segmentBounce)
                    Dim collisionResolved As Boolean =
                        Math.Abs(center.X - beforeCollisionCenter.X) > 0.0001F OrElse
                        Math.Abs(center.Y - beforeCollisionCenter.Y) > 0.0001F OrElse
                        Math.Abs(velocity.X - beforeCollisionVelocity.X) > 0.0001F OrElse
                        Math.Abs(velocity.Y - beforeCollisionVelocity.Y) > 0.0001F
                    If collisionResolved Then
                        ApplyShallowSurfaceRollingBoost(velocity, fromPoint, toPoint, elapsed, gravity)
                    End If
                Next
            Next
            For index As Integer = 0 To obstacles.Count - 1
                ResolveCircularObstacleCollision(center, radius, velocity, obstacles(index), 1.0F, 1.0F, obstacleBounces(index))
            Next
            CheckLauncherCapture(center, 1.0F, 1.0F)
            CheckSwitchZones(previousCenter, center, 1.0F, 1.0F)
            physicsCenter = center
#If PHYSICS_PREVIEW Then
            ball.SetMotionPathPosition(New PointF(center.X * scaleX, center.Y * scaleY), True)
#End If
        End Sub

        Private Shared Sub ApplyShallowSurfaceRollingBoost(ByRef ballVelocity As PointF,
                                                            ByVal fromPoint As PointF,
                                                            ByVal toPoint As PointF,
                                                            ByVal elapsed As Single,
                                                            ByVal gravityAcceleration As Single)
            Dim segmentX As Single = toPoint.X - fromPoint.X
            Dim segmentY As Single = toPoint.Y - fromPoint.Y
            Dim length As Single = CSng(Math.Sqrt(segmentX * segmentX + segmentY * segmentY))
            If length <= 0.01F Then Return

            Dim tangentX As Single = segmentX / length
            Dim tangentY As Single = segmentY / length
            Dim absoluteSlope As Single = Math.Abs(tangentY)
            If absoluteSlope <= 0.001F OrElse absoluteSlope > ShallowSurfaceSlopeLimit Then Return

            Dim extraProjectedGravity As Single =
                gravityAcceleration * tangentY * (ShallowSurfaceRollMultiplier - 1.0F)
            ballVelocity.X += tangentX * extraProjectedGravity * elapsed
            ballVelocity.Y += tangentY * extraProjectedGravity * elapsed
        End Sub

        Private Sub CheckLauncherCapture(ByRef center As PointF, ByVal scaleX As Single, ByVal scaleY As Single)
            If launcher Is Nothing OrElse launcherArmed Then Return
            Dim origin As PointF = PointF.Empty, angle As Single = 0.0F
            GetLauncherPose(origin, angle)
            Dim radius As Single = launcher.CaptureRadius * (scaleX + scaleY) / 2.0F
            Dim dx As Single = center.X - origin.X
            Dim dy As Single = center.Y - origin.Y
            Dim inside As Boolean = dx * dx + dy * dy <= radius * radius
            If Not launcherExitedCapture Then
                launcherExitedCapture = Not inside
            ElseIf inside Then
                center = origin
                velocity = PointF.Empty
                launcherArmed = True
                launcherHolding = True
                RaiseEvent LauncherCaptured(Me, EventArgs.Empty)
            End If
        End Sub

        Private Function AuthoredPivotBounds(ByVal pivot As B2SPictureBox) As RectangleF
            Dim scaleX As Single = If(authoredBallWidth > 0.0F AndAlso ball.RectangleF.Width > 0.0F, ball.RectangleF.Width / authoredBallWidth, 1.0F)
            Dim scaleY As Single = If(authoredBallHeight > 0.0F AndAlso ball.RectangleF.Height > 0.0F, ball.RectangleF.Height / authoredBallHeight, 1.0F)
            Return New RectangleF(pivot.RectangleF.X / scaleX, pivot.RectangleF.Y / scaleY,
                                  pivot.RectangleF.Width / scaleX, pivot.RectangleF.Height / scaleY)
        End Function

        Private Sub GetLauncherPose(ByRef origin As PointF, ByRef angle As Single)
            origin = launcher.Origin
            angle = launcher.Angle
            If Not launcher.FollowPivot Then Return
            Dim pivot As B2SPictureBox = FindPivotPicture(flipperName)
            If pivot Is Nothing Then Return
            If pivot.PivotAutomaticOscillation Then origin = authoredBallCenter
            Dim pivotBounds As RectangleF = AuthoredPivotBounds(pivot)
            Dim hinge As New PointF(pivotBounds.Left + pivotBounds.Width * pivot.RotationPivotX,
                                    pivotBounds.Top + pivotBounds.Height * pivot.RotationPivotY)
            Dim restAngle As Single = If(pivot.PivotAutomaticOscillation, 0.0F, pivot.PivotDownAngle)
            Dim delta As Single = pivot.RotationAngle - restAngle
            Dim radians As Double = delta * Math.PI / 180.0R
            Dim dx As Single = origin.X - hinge.X, dy As Single = origin.Y - hinge.Y
            origin = New PointF(hinge.X + CSng(dx * Math.Cos(radians) - dy * Math.Sin(radians)),
                                hinge.Y + CSng(dx * Math.Sin(radians) + dy * Math.Cos(radians)))
            angle += delta
        End Sub

        Private Sub CheckSwitchZones(ByVal previousCenter As PointF, ByVal center As PointF,
                                     ByVal scaleX As Single, ByVal scaleY As Single)
            If Math.Abs(scaleX) < 0.000001F OrElse Math.Abs(scaleY) < 0.000001F Then Return
            Dim authoredPrevious As New PointF(previousCenter.X / scaleX, previousCenter.Y / scaleY)
            Dim authoredCurrent As New PointF(center.X / scaleX, center.Y / scaleY)
            For index As Integer = 0 To switchZones.Count - 1
                Dim zone As PhysicsSwitchZone = switchZones(index)
                Dim inside As Boolean = PointInSwitchZone(authoredCurrent, zone)
                Dim crossed As Boolean = Not inside AndAlso SegmentIntersectsSwitchZone(authoredPrevious, authoredCurrent, zone)
                If Not switchZoneInside(index) AndAlso (inside OrElse crossed) Then
                    If zone.SwitchID > 0 Then PulsePhysicsSwitch(zone.SwitchID)
                    If Not String.IsNullOrWhiteSpace(zone.SoundName) Then PlayPhysicsSound(zone.SoundName, zone.SoundVolume)
                End If
                switchZoneInside(index) = inside
            Next
        End Sub

        Private Shared Function PointInSwitchZone(ByVal point As PointF, ByVal zone As PhysicsSwitchZone) As Boolean
            Dim local As PointF = SwitchZoneLocalPoint(point, zone)
            Return zone.Bounds.Contains(local.X, local.Y)
        End Function

        Private Shared Function SegmentIntersectsSwitchZone(ByVal fromPoint As PointF, ByVal toPoint As PointF,
                                                             ByVal zone As PhysicsSwitchZone) As Boolean
            Dim localFrom As PointF = SwitchZoneLocalPoint(fromPoint, zone)
            Dim localTo As PointF = SwitchZoneLocalPoint(toPoint, zone)
            If zone.Bounds.Contains(localFrom.X, localFrom.Y) OrElse zone.Bounds.Contains(localTo.X, localTo.Y) Then Return True
            Dim deltaX As Double = localTo.X - localFrom.X, deltaY As Double = localTo.Y - localFrom.Y
            Dim first As Double = 0.0R, last As Double = 1.0R
            Return ClipSwitchSegment(-deltaX, localFrom.X - zone.Bounds.Left, first, last) AndAlso
                   ClipSwitchSegment(deltaX, zone.Bounds.Right - localFrom.X, first, last) AndAlso
                   ClipSwitchSegment(-deltaY, localFrom.Y - zone.Bounds.Top, first, last) AndAlso
                   ClipSwitchSegment(deltaY, zone.Bounds.Bottom - localFrom.Y, first, last)
        End Function

        Private Shared Function ClipSwitchSegment(ByVal direction As Double, ByVal distance As Double,
                                                   ByRef first As Double, ByRef last As Double) As Boolean
            If Math.Abs(direction) < 0.0000001R Then Return distance >= 0.0R
            Dim ratio As Double = distance / direction
            If direction < 0.0R Then
                If ratio > last Then Return False
                If ratio > first Then first = ratio
            Else
                If ratio < first Then Return False
                If ratio < last Then last = ratio
            End If
            Return True
        End Function

        Private Shared Function SwitchZoneLocalPoint(ByVal point As PointF, ByVal zone As PhysicsSwitchZone) As PointF
            Dim angle As Single = NormalizeSwitchAngle(zone.Angle)
            If Math.Abs(angle) < 0.001F Then Return point
            Dim centerX As Single = zone.Bounds.Left + zone.Bounds.Width / 2.0F
            Dim centerY As Single = zone.Bounds.Top + zone.Bounds.Height / 2.0F
            Dim radians As Double = -angle * Math.PI / 180.0R
            Dim cosine As Double = Math.Cos(radians), sine As Double = Math.Sin(radians)
            Dim x As Double = point.X - centerX, y As Double = point.Y - centerY
            Return New PointF(CSng(centerX + x * cosine - y * sine),
                              CSng(centerY + x * sine + y * cosine))
        End Function

        Private Shared Function NormalizeSwitchAngle(ByVal angle As Single) As Single
            Dim normalized As Single = angle Mod 360.0F
            If normalized > 180.0F Then normalized -= 360.0F
            If normalized <= -180.0F Then normalized += 360.0F
            Return normalized
        End Function

        Private Sub ResolveCircularObstacleCollision(ByRef center As PointF, ByVal ballRadius As Single,
                                                      ByRef ballVelocity As PointF, ByVal obstacle As RectangleF,
                                                      ByVal scaleX As Single, ByVal scaleY As Single, ByVal bounce As Single)
            Dim obstacleCenterX As Single = (obstacle.X + obstacle.Width / 2.0F) * scaleX
            Dim obstacleCenterY As Single = (obstacle.Y + obstacle.Height / 2.0F) * scaleY
            Dim obstacleRadius As Single = Math.Max(1.0F, (obstacle.Width * scaleX + obstacle.Height * scaleY) / 4.0F)
            Dim dx As Single = center.X - obstacleCenterX
            Dim dy As Single = center.Y - obstacleCenterY
            Dim minimumDistance As Single = ballRadius + obstacleRadius
            Dim distanceSquared As Single = dx * dx + dy * dy
            If distanceSquared >= minimumDistance * minimumDistance Then Return
            Dim distance As Single = CSng(Math.Sqrt(distanceSquared))
            Dim normalX As Single
            Dim normalY As Single
            If distance < 0.001F Then
                normalX = 0.0F
                normalY = -1.0F
                distance = 0.0F
            Else
                normalX = dx / distance
                normalY = dy / distance
            End If
            center.X += normalX * (minimumDistance - distance)
            center.Y += normalY * (minimumDistance - distance)
            Dim normalSpeed As Single = ballVelocity.X * normalX + ballVelocity.Y * normalY
            If normalSpeed < 0.0F Then
                ballVelocity.X -= (1.0F + bounce) * normalSpeed * normalX
                ballVelocity.Y -= (1.0F + bounce) * normalSpeed * normalY
            End If
        End Sub

        Private Sub ResolvePivotObjectCollision(ByRef center As PointF, ByVal previousCenter As PointF,
                                                ByVal ballRadius As Single,
                                                ByRef ballVelocity As PointF, ByVal pivot As B2SPictureBox,
                                                ByVal contactKey As String,
                                                ByVal bounce As Single)
            Dim objectBounds As RectangleF = AuthoredPivotBounds(pivot)
            If objectBounds.Width <= 0.0F OrElse objectBounds.Height <= 0.0F Then Return

            ' Knockdown targets intentionally retain their verified full-bounds
            ' contact. A free-spin target uses its rendered alpha silhouette when
            ' the artwork contains a genuine cutout; opaque artwork falls back to
            ' the established circular contact model.
            If pivot.PivotPhysicsResponseMode = 1 OrElse pivot.PivotPhysicsResponseMode = 2 Then
                ResolveKnockdownPivotCollision(center, previousCenter, ballRadius,
                                               ballVelocity, pivot, objectBounds, bounce)
                Return
            End If

            Dim hinge As New PointF(objectBounds.Left + objectBounds.Width * pivot.RotationPivotX,
                                    objectBounds.Top + objectBounds.Height * pivot.RotationPivotY)
            Dim sourceCenter As New PointF(objectBounds.Left + objectBounds.Width / 2.0F,
                                           objectBounds.Top + objectBounds.Height / 2.0F)
            Dim radians As Double = pivot.RotationAngle * Math.PI / 180.0R
            Dim sourceX As Double = sourceCenter.X - hinge.X
            Dim sourceY As Double = sourceCenter.Y - hinge.Y
            Dim objectCenter As New PointF(CSng(hinge.X + sourceX * Math.Cos(radians) - sourceY * Math.Sin(radians)),
                                           CSng(hinge.Y + sourceX * Math.Sin(radians) + sourceY * Math.Cos(radians)))
            Dim objectRadius As Single = Math.Max(3.0F, Math.Min(objectBounds.Width, objectBounds.Height) / 2.0F)
            Dim normalX As Single
            Dim normalY As Single
            Dim contactX As Single
            Dim contactY As Single
            Dim useSilhouette As Boolean = pivot.PhysicsCollisionContour IsNot Nothing AndAlso
                                           pivot.PhysicsCollisionContour.Length >= 3 AndAlso
                                           Not pivot.PhysicsCollisionMaskSize.IsEmpty
            Dim normalizedContactKey As String = If(contactKey, String.Empty).Trim()
            If useSilhouette Then
                Dim isFreshContact As Boolean = normalizedContactKey.Length = 0 OrElse
                                                Not activePivotSilhouetteContacts.Contains(normalizedContactKey)
                If TryResolvePivotSilhouetteManifold(center, previousCenter, ballRadius, ballVelocity,
                                                     pivot, objectBounds, hinge, objectCenter, objectRadius,
                                                     isFreshContact) Then
                    If normalizedContactKey.Length > 0 Then activePivotSilhouetteContacts.Add(normalizedContactKey)
                ElseIf normalizedContactKey.Length > 0 Then
                    activePivotSilhouetteContacts.Remove(normalizedContactKey)
                End If
                Return
            Else
                Dim dx As Single = center.X - objectCenter.X
                Dim dy As Single = center.Y - objectCenter.Y
                Dim minimumDistance As Single = ballRadius + objectRadius
                Dim distanceSquared As Single = dx * dx + dy * dy
                If distanceSquared >= minimumDistance * minimumDistance Then
                    ' A fast ball can cross the complete pivot object between timer
                    ' updates and finish beyond it without overlapping. Sweep the
                    ' ball's full movement against the pivot collision circle before
                    ' treating the step as no contact.
                    Dim motionX As Single = center.X - previousCenter.X
                    Dim motionY As Single = center.Y - previousCenter.Y
                    Dim motionLengthSquared As Single = motionX * motionX + motionY * motionY
                    If motionLengthSquared <= 0.000001F Then Return

                    Dim sweepRelativeX As Single = previousCenter.X - objectCenter.X
                    Dim sweepRelativeY As Single = previousCenter.Y - objectCenter.Y
                    Dim linear As Single = 2.0F * (sweepRelativeX * motionX + sweepRelativeY * motionY)
                    Dim constant As Single = sweepRelativeX * sweepRelativeX + sweepRelativeY * sweepRelativeY -
                                             minimumDistance * minimumDistance
                    Dim discriminant As Double = CDbl(linear) * CDbl(linear) -
                                                 4.0R * CDbl(motionLengthSquared) * CDbl(constant)
                    If discriminant < 0.0R Then Return

                    Dim hitTime As Single = CSng((-CDbl(linear) - Math.Sqrt(discriminant)) /
                                                   (2.0R * CDbl(motionLengthSquared)))
                    If hitTime < 0.0F OrElse hitTime > 1.0F Then Return

                    center.X = previousCenter.X + motionX * hitTime
                    center.Y = previousCenter.Y + motionY * hitTime
                    dx = center.X - objectCenter.X
                    dy = center.Y - objectCenter.Y
                    distanceSquared = dx * dx + dy * dy
                End If

                Dim distance As Single = CSng(Math.Sqrt(distanceSquared))
                If distance < 0.001F Then
                    Dim speed As Single = CSng(Math.Sqrt(ballVelocity.X * ballVelocity.X + ballVelocity.Y * ballVelocity.Y))
                    If speed > 0.001F Then
                        normalX = -ballVelocity.X / speed
                        normalY = -ballVelocity.Y / speed
                    Else
                        normalX = 0.0F
                        normalY = -1.0F
                    End If
                    distance = 0.0F
                Else
                    normalX = dx / distance
                    normalY = dy / distance
                End If
                center.X += normalX * (minimumDistance - distance)
                center.Y += normalY * (minimumDistance - distance)
                contactX = objectCenter.X + normalX * objectRadius
                contactY = objectCenter.Y + normalY * objectRadius
            End If

            Dim armX As Single = contactX - hinge.X
            Dim armY As Single = contactY - hinge.Y
            Dim angularRadians As Single = pivot.PhysicsAngularVelocity * CSng(Math.PI / 180.0R)
            Dim surfaceX As Single = -angularRadians * armY
            Dim surfaceY As Single = angularRadians * armX
            Dim relativeX As Single = ballVelocity.X - surfaceX
            Dim relativeY As Single = ballVelocity.Y - surfaceY
            Dim incomingNormalSpeed As Single = relativeX * normalX + relativeY * normalY
            If incomingNormalSpeed >= 0.0F Then Return

            Dim weight As Single = Math.Max(0.1F, pivot.PivotPhysicsWeight)
            Dim centerOffsetX As Single = objectCenter.X - hinge.X
            Dim centerOffsetY As Single = objectCenter.Y - hinge.Y
            Dim inertia As Single = weight * Math.Max(1.0F, 0.5F * objectRadius * objectRadius +
                                                     centerOffsetX * centerOffsetX + centerOffsetY * centerOffsetY)
            Dim inverseInertia As Single = 1.0F / inertia
            Dim armCrossNormal As Single = armX * normalY - armY * normalX
            Dim normalDenominator As Single = 1.0F + armCrossNormal * armCrossNormal * inverseInertia
            Dim normalImpulse As Single = -(1.0F + Math.Max(0.0F, Math.Min(1.0F, bounce))) * incomingNormalSpeed / normalDenominator
            ballVelocity.X += normalImpulse * normalX
            ballVelocity.Y += normalImpulse * normalY

            Dim tangentX As Single = -normalY
            Dim tangentY As Single = normalX
            Dim relativeTangentSpeed As Single = relativeX * tangentX + relativeY * tangentY
            Dim armCrossTangent As Single = armX * tangentY - armY * tangentX
            Dim tangentDenominator As Single = 1.0F + armCrossTangent * armCrossTangent * inverseInertia
            Dim tangentImpulse As Single = -relativeTangentSpeed / tangentDenominator
            Dim maximumTangentImpulse As Single = Math.Abs(normalImpulse) * Math.Max(0.0F, Math.Min(1.0F, pivot.PivotPhysicsGrip))
            tangentImpulse = Math.Max(-maximumTangentImpulse, Math.Min(maximumTangentImpulse, tangentImpulse))
            ballVelocity.X += tangentImpulse * tangentX
            ballVelocity.Y += tangentImpulse * tangentY

            Dim objectTorque As Single = -normalImpulse * armCrossNormal - tangentImpulse * armCrossTangent
            Dim deltaDegreesPerSecond As Single = objectTorque * inverseInertia * CSng(180.0R / Math.PI) *
                                                  Math.Max(0.0F, flipperStrength)
            pivot.ApplyPivotPhysicsAngularImpulse(deltaDegreesPerSecond)
        End Sub

        Private Function TryResolvePivotSilhouetteManifold(ByRef center As PointF,
                                                           ByVal previousCenter As PointF,
                                                           ByVal ballRadius As Single,
                                                           ByRef ballVelocity As PointF,
                                                           ByVal pivot As B2SPictureBox,
                                                           ByVal objectBounds As RectangleF,
                                                           ByVal hinge As PointF,
                                                           ByVal objectCenter As PointF,
                                                           ByVal objectRadius As Single,
                                                           ByVal isFreshContact As Boolean) As Boolean
            Dim maskWidth As Integer = pivot.PhysicsCollisionMaskSize.Width
            Dim maskHeight As Integer = pivot.PhysicsCollisionMaskSize.Height
            If maskWidth <= 0 OrElse maskHeight <= 0 Then Return False

            Dim radians As Double = pivot.RotationAngle * Math.PI / 180.0R
            Dim cosine As Single = CSng(Math.Cos(radians))
            Dim sine As Single = CSng(Math.Sin(radians))
            Dim previousOffsetX As Single = previousCenter.X - hinge.X
            Dim previousOffsetY As Single = previousCenter.Y - hinge.Y
            Dim currentOffsetX As Single = center.X - hinge.X
            Dim currentOffsetY As Single = center.Y - hinge.Y
            Dim previousLocalX As Single = hinge.X + previousOffsetX * cosine + previousOffsetY * sine
            Dim previousLocalY As Single = hinge.Y - previousOffsetX * sine + previousOffsetY * cosine
            Dim currentLocalX As Single = hinge.X + currentOffsetX * cosine + currentOffsetY * sine
            Dim currentLocalY As Single = hinge.Y - currentOffsetX * sine + currentOffsetY * cosine

            Dim sourceCenterX As Single = objectBounds.Left + objectBounds.Width / 2.0F
            Dim sourceCenterY As Single = objectBounds.Top + objectBounds.Height / 2.0F
            Dim sweepX As Single = currentLocalX - previousLocalX
            Dim sweepY As Single = currentLocalY - previousLocalY
            Dim sweepLengthSquared As Single = sweepX * sweepX + sweepY * sweepY
            Dim closestTime As Single = 0.0F
            If sweepLengthSquared > 0.000001F Then
                closestTime = ((sourceCenterX - previousLocalX) * sweepX +
                               (sourceCenterY - previousLocalY) * sweepY) / sweepLengthSquared
                closestTime = Math.Max(0.0F, Math.Min(1.0F, closestTime))
            End If
            Dim broadX As Single = previousLocalX + sweepX * closestTime - sourceCenterX
            Dim broadY As Single = previousLocalY + sweepY * closestTime - sourceCenterY
            Dim broadRadius As Single = ballRadius +
                CSng(Math.Sqrt(objectBounds.Width * objectBounds.Width + objectBounds.Height * objectBounds.Height)) / 2.0F
            If broadX * broadX + broadY * broadY > broadRadius * broadRadius Then Return False

            Dim sweepLength As Single = CSng(Math.Sqrt(sweepLengthSquared))
            Dim sampleSpacing As Single = Math.Max(0.5F, Math.Min(2.0F, ballRadius * 0.25F))
            Dim sampleCount As Integer = Math.Max(1, CInt(Math.Ceiling(sweepLength / sampleSpacing)))
            Dim contacts(1) As PivotContourContact
            Dim contactCount As Integer = 0
            Dim hitLocalX As Single = 0.0F
            Dim hitLocalY As Single = 0.0F

            ' Resolve a present overlap at the ball's current position. If the
            ' previous position was touching but the current one is clear, the
            ' ball separated during this step; treating fraction zero as a new
            ' sweep hit would rewind it onto the rotating outline and carry it.
            If TryFindPivotContourContacts(currentLocalX, currentLocalY, ballRadius,
                                           pivot.PhysicsCollisionContour, maskWidth, maskHeight,
                                           objectBounds, hinge, contacts, contactCount) Then
                hitLocalX = currentLocalX
                hitLocalY = currentLocalY
            Else
                Dim previousContacts(1) As PivotContourContact
                Dim previousContactCount As Integer = 0
                If TryFindPivotContourContacts(previousLocalX, previousLocalY, ballRadius,
                                               pivot.PhysicsCollisionContour, maskWidth, maskHeight,
                                               objectBounds, hinge, previousContacts, previousContactCount) Then
                    Return False
                End If
                For sampleIndex As Integer = 1 To sampleCount - 1
                    Dim fraction As Single = CSng(sampleIndex) / CSng(sampleCount)
                    Dim sampleX As Single = previousLocalX + sweepX * fraction
                    Dim sampleY As Single = previousLocalY + sweepY * fraction
                    If TryFindPivotContourContacts(sampleX, sampleY, ballRadius,
                                                   pivot.PhysicsCollisionContour, maskWidth, maskHeight,
                                                   objectBounds, hinge, contacts, contactCount) Then
                        hitLocalX = sampleX
                        hitLocalY = sampleY
                        Exit For
                    End If
                Next
            End If
            If contactCount <= 0 Then Return False

            ' Resolve every active contour half-space together. A single-edge snap
            ' can put the ball onto the opposite arm of a concave valley and carry it.
            Dim correctionX As Single = 0.0F
            Dim correctionY As Single = 0.0F
            Dim separationMargin As Single = Math.Max(objectBounds.Width / Math.Max(1, maskWidth - 1),
                                                       objectBounds.Height / Math.Max(1, maskHeight - 1))
            For iteration As Integer = 0 To 5
                For contactIndex As Integer = 0 To contactCount - 1
                    Dim correctionAlongNormal As Single = correctionX * contacts(contactIndex).NormalX +
                                                          correctionY * contacts(contactIndex).NormalY
                    Dim remainingPenetration As Single = contacts(contactIndex).Penetration + separationMargin -
                                                         correctionAlongNormal
                    If remainingPenetration > 0.0F Then
                        correctionX += remainingPenetration * contacts(contactIndex).NormalX
                        correctionY += remainingPenetration * contacts(contactIndex).NormalY
                    End If
                Next
            Next

            Dim separatedLocalX As Single = hitLocalX + correctionX
            Dim separatedLocalY As Single = hitLocalY + correctionY
            Dim separatedOffsetX As Single = separatedLocalX - hinge.X
            Dim separatedOffsetY As Single = separatedLocalY - hinge.Y
            center.X = hinge.X + separatedOffsetX * cosine - separatedOffsetY * sine
            center.Y = hinge.Y + separatedOffsetX * sine + separatedOffsetY * cosine

            Dim hitOffsetX As Single = hitLocalX - hinge.X
            Dim hitOffsetY As Single = hitLocalY - hinge.Y
            Dim hitWorldArmX As Single = hitOffsetX * cosine - hitOffsetY * sine
            Dim hitWorldArmY As Single = hitOffsetX * sine + hitOffsetY * cosine
            Dim incomingOmega As Single = pivot.PhysicsAngularVelocity * CSng(Math.PI / 180.0R)
            Dim incomingSurfaceX As Single = -incomingOmega * hitWorldArmY
            Dim incomingSurfaceY As Single = incomingOmega * hitWorldArmX
            Dim incomingAngularMomentum As Single =
                hitWorldArmX * (ballVelocity.Y - incomingSurfaceY) -
                hitWorldArmY * (ballVelocity.X - incomingSurfaceX)
            Dim desiredSpinSign As Integer = 0
            If isFreshContact AndAlso Math.Abs(incomingAngularMomentum) > 100.0F Then
                desiredSpinSign = Math.Sign(incomingAngularMomentum)
            End If

            Dim worldNormalX(contactCount - 1) As Single
            Dim worldNormalY(contactCount - 1) As Single
            Dim armX(contactCount - 1) As Single
            Dim armY(contactCount - 1) As Single
            For contactIndex As Integer = 0 To contactCount - 1
                worldNormalX(contactIndex) = contacts(contactIndex).NormalX * cosine -
                                             contacts(contactIndex).NormalY * sine
                worldNormalY(contactIndex) = contacts(contactIndex).NormalX * sine +
                                             contacts(contactIndex).NormalY * cosine
                Dim contactOffsetX As Single = contacts(contactIndex).ContactX - hinge.X
                Dim contactOffsetY As Single = contacts(contactIndex).ContactY - hinge.Y
                Dim worldContactX As Single = hinge.X + contactOffsetX * cosine - contactOffsetY * sine
                Dim worldContactY As Single = hinge.Y + contactOffsetX * sine + contactOffsetY * cosine
                armX(contactIndex) = worldContactX - hinge.X
                armY(contactIndex) = worldContactY - hinge.Y
            Next

            Dim weight As Single = Math.Max(0.1F, pivot.PivotPhysicsWeight)
            Dim centerOffsetX As Single = objectCenter.X - hinge.X
            Dim centerOffsetY As Single = objectCenter.Y - hinge.Y
            Dim inertia As Single = weight * Math.Max(1.0F, 0.5F * objectRadius * objectRadius +
                                                     centerOffsetX * centerOffsetX + centerOffsetY * centerOffsetY)
            Dim inverseInertia As Single = 1.0F / inertia
            Dim startingOmega As Single = pivot.PhysicsAngularVelocity * CSng(Math.PI / 180.0R)
            Dim omega As Single = startingOmega
            Dim accumulatedNormalImpulse(contactCount - 1) As Single
            Dim targetNormalVelocity(contactCount - 1) As Single
            Const pivotRestitution As Single = 0.55F

            For contactIndex As Integer = 0 To contactCount - 1
                Dim surfaceX As Single = -omega * armY(contactIndex)
                Dim surfaceY As Single = omega * armX(contactIndex)
                Dim incomingNormalVelocity As Single =
                    (ballVelocity.X - surfaceX) * worldNormalX(contactIndex) +
                    (ballVelocity.Y - surfaceY) * worldNormalY(contactIndex)
                If isFreshContact AndAlso incomingNormalVelocity < 0.0F Then
                    targetNormalVelocity(contactIndex) = -pivotRestitution * incomingNormalVelocity
                Else
                    targetNormalVelocity(contactIndex) = 0.0F
                End If
            Next

            ' Projected Gauss-Seidel gives both valley arms one shared solution.
            ' The ball and pivot receive equal and opposite impulses.
            For iteration As Integer = 0 To 5
                For contactIndex As Integer = 0 To contactCount - 1
                    Dim surfaceX As Single = -omega * armY(contactIndex)
                    Dim surfaceY As Single = omega * armX(contactIndex)
                    Dim currentNormalVelocity As Single =
                        (ballVelocity.X - surfaceX) * worldNormalX(contactIndex) +
                        (ballVelocity.Y - surfaceY) * worldNormalY(contactIndex)
                    Dim armCrossNormal As Single = armX(contactIndex) * worldNormalY(contactIndex) -
                                                   armY(contactIndex) * worldNormalX(contactIndex)
                    Dim effectiveMass As Single = 1.0F + armCrossNormal * armCrossNormal * inverseInertia
                    Dim impulseChange As Single = (targetNormalVelocity(contactIndex) - currentNormalVelocity) /
                                                  effectiveMass
                    Dim newAccumulatedImpulse As Single = Math.Max(0.0F,
                        accumulatedNormalImpulse(contactIndex) + impulseChange)
                    impulseChange = newAccumulatedImpulse - accumulatedNormalImpulse(contactIndex)
                    accumulatedNormalImpulse(contactIndex) = newAccumulatedImpulse
                    If Math.Abs(impulseChange) <= 0.000001F Then Continue For

                    Dim impulseX As Single = impulseChange * worldNormalX(contactIndex)
                    Dim impulseY As Single = impulseChange * worldNormalY(contactIndex)
                    ballVelocity.X += impulseX
                    ballVelocity.Y += impulseY
                    omega += -inverseInertia * (armX(contactIndex) * impulseY - armY(contactIndex) * impulseX)
                Next
            Next

            Dim grip As Single = Math.Max(0.0F, Math.Min(1.0F, pivot.PivotPhysicsGrip))
            If isFreshContact Then
                ' Grip transfers tangential momentum once at impact. Reapplying it
                ' on every persistent millisecond contact makes the rotating outline
                ' drag the ball with it instead of letting the ball separate.
                For contactIndex As Integer = 0 To contactCount - 1
                    If accumulatedNormalImpulse(contactIndex) <= 0.0F Then Continue For
                    Dim tangentX As Single = -worldNormalY(contactIndex)
                    Dim tangentY As Single = worldNormalX(contactIndex)
                    Dim surfaceX As Single = -omega * armY(contactIndex)
                    Dim surfaceY As Single = omega * armX(contactIndex)
                    Dim relativeTangentVelocity As Single =
                        (ballVelocity.X - surfaceX) * tangentX +
                        (ballVelocity.Y - surfaceY) * tangentY
                    Dim armCrossTangent As Single = armX(contactIndex) * tangentY - armY(contactIndex) * tangentX
                    Dim tangentEffectiveMass As Single = 1.0F + armCrossTangent * armCrossTangent * inverseInertia
                    Dim tangentImpulse As Single = -relativeTangentVelocity / tangentEffectiveMass
                    Dim maximumTangentImpulse As Single = grip * accumulatedNormalImpulse(contactIndex)
                    tangentImpulse = Math.Max(-maximumTangentImpulse,
                                              Math.Min(maximumTangentImpulse, tangentImpulse))
                    Dim tangentImpulseX As Single = tangentImpulse * tangentX
                    Dim tangentImpulseY As Single = tangentImpulse * tangentY
                    ballVelocity.X += tangentImpulseX
                    ballVelocity.Y += tangentImpulseY
                    omega += -inverseInertia * (armX(contactIndex) * tangentImpulseY -
                                                 armY(contactIndex) * tangentImpulseX)
                Next
            End If

            Dim solvedAngularChange As Single = omega - startingOmega
            If isFreshContact AndAlso desiredSpinSign <> 0 AndAlso
               solvedAngularChange * desiredSpinSign < 0.0F Then
                ' At a concave valley, two valid edge normals can otherwise choose
                ' the opposite rotational branch. Keep the solid manifold response,
                ' but make the star follow the incoming ball's motion about its pivot.
                omega = startingOmega + Math.Abs(solvedAngularChange) * desiredSpinSign
                For contactIndex As Integer = 0 To contactCount - 1
                    Dim surfaceX As Single = -omega * armY(contactIndex)
                    Dim surfaceY As Single = omega * armX(contactIndex)
                    Dim relativeNormalVelocity As Single =
                        (ballVelocity.X - surfaceX) * worldNormalX(contactIndex) +
                        (ballVelocity.Y - surfaceY) * worldNormalY(contactIndex)
                    If relativeNormalVelocity < 0.0F Then
                        ballVelocity.X -= relativeNormalVelocity * worldNormalX(contactIndex)
                        ballVelocity.Y -= relativeNormalVelocity * worldNormalY(contactIndex)
                    End If
                Next
            End If

            Dim deltaDegreesPerSecond As Single = (omega - startingOmega) * CSng(180.0R / Math.PI)
            If Math.Abs(deltaDegreesPerSecond) > 0.000001F Then
                pivot.ApplyPivotPhysicsAngularImpulse(deltaDegreesPerSecond)
            End If
            Return True
        End Function

        Private Shared Function TryFindPivotContourContacts(ByVal centerX As Single,
                                                            ByVal centerY As Single,
                                                            ByVal ballRadius As Single,
                                                            ByVal contour As PointF(),
                                                            ByVal maskWidth As Integer,
                                                            ByVal maskHeight As Integer,
                                                            ByVal objectBounds As RectangleF,
                                                            ByVal hinge As PointF,
                                                            ByRef contacts() As PivotContourContact,
                                                            ByRef contactCount As Integer) As Boolean
            contactCount = 0
            If contour Is Nothing OrElse contour.Length < 3 OrElse
               maskWidth <= 1 OrElse maskHeight <= 1 OrElse
               contacts Is Nothing OrElse contacts.Length < 2 Then Return False

            Dim scaleX As Single = objectBounds.Width / (maskWidth - 1)
            Dim scaleY As Single = objectBounds.Height / (maskHeight - 1)
            Dim centerInside As Boolean = False
            For index As Integer = 0 To contour.Length - 1
                Dim nextIndex As Integer = (index + 1) Mod contour.Length
                Dim startX As Single = objectBounds.Left + contour(index).X * scaleX
                Dim startY As Single = objectBounds.Top + contour(index).Y * scaleY
                Dim endX As Single = objectBounds.Left + contour(nextIndex).X * scaleX
                Dim endY As Single = objectBounds.Top + contour(nextIndex).Y * scaleY
                If (startY > centerY) <> (endY > centerY) Then
                    Dim crossingX As Single = startX + (centerY - startY) *
                                              (endX - startX) / (endY - startY)
                    If centerX < crossingX Then centerInside = Not centerInside
                End If
            Next

            Dim primaryFound As Boolean = False
            Dim primary As PivotContourContact
            For index As Integer = 0 To contour.Length - 1
                Dim candidate As PivotContourContact
                If TryBuildPivotContourContact(centerX, centerY, ballRadius, contour,
                                               index, scaleX, scaleY, objectBounds,
                                               centerInside, candidate) Then
                    If Not primaryFound OrElse candidate.Distance < primary.Distance Then
                        primary = candidate
                        primaryFound = True
                    End If
                End If
            Next
            If Not primaryFound Then Return False
            contacts(0) = primary
            contactCount = 1

            Dim secondaryFound As Boolean = False
            Dim secondary As PivotContourContact
            For index As Integer = 0 To contour.Length - 1
                Dim candidate As PivotContourContact
                If Not TryBuildPivotContourContact(centerX, centerY, ballRadius, contour,
                                                   index, scaleX, scaleY, objectBounds,
                                                   centerInside, candidate) Then Continue For
                If candidate.SegmentIndex = primary.SegmentIndex Then Continue For
                Dim normalDot As Single = primary.NormalX * candidate.NormalX +
                                          primary.NormalY * candidate.NormalY
                If normalDot >= 0.98F Then Continue For
                Dim contactDeltaX As Single = candidate.ContactX - primary.ContactX
                Dim contactDeltaY As Single = candidate.ContactY - primary.ContactY
                If contactDeltaX * contactDeltaX + contactDeltaY * contactDeltaY <= 0.25F Then Continue For
                If Not secondaryFound OrElse candidate.Distance < secondary.Distance Then
                    secondary = candidate
                    secondaryFound = True
                End If
            Next
            If secondaryFound Then
                contacts(1) = secondary
                contactCount = 2
            End If
            Return True
        End Function

        Private Shared Function TryBuildPivotContourContact(ByVal centerX As Single,
                                                            ByVal centerY As Single,
                                                            ByVal ballRadius As Single,
                                                            ByVal contour As PointF(),
                                                            ByVal segmentIndex As Integer,
                                                            ByVal scaleX As Single,
                                                            ByVal scaleY As Single,
                                                            ByVal objectBounds As RectangleF,
                                                            ByVal centerInside As Boolean,
                                                            ByRef contact As PivotContourContact) As Boolean
            Dim nextIndex As Integer = (segmentIndex + 1) Mod contour.Length
            Dim startX As Single = objectBounds.Left + contour(segmentIndex).X * scaleX
            Dim startY As Single = objectBounds.Top + contour(segmentIndex).Y * scaleY
            Dim endX As Single = objectBounds.Left + contour(nextIndex).X * scaleX
            Dim endY As Single = objectBounds.Top + contour(nextIndex).Y * scaleY
            Dim segmentX As Single = endX - startX
            Dim segmentY As Single = endY - startY
            Dim segmentLengthSquared As Single = segmentX * segmentX + segmentY * segmentY
            If segmentLengthSquared <= 0.000001F Then Return False

            Dim fraction As Single = ((centerX - startX) * segmentX +
                                      (centerY - startY) * segmentY) / segmentLengthSquared
            fraction = Math.Max(0.0F, Math.Min(1.0F, fraction))
            Dim nearestX As Single = startX + segmentX * fraction
            Dim nearestY As Single = startY + segmentY * fraction
            Dim deltaX As Single = centerX - nearestX
            Dim deltaY As Single = centerY - nearestY
            Dim distance As Single = CSng(Math.Sqrt(deltaX * deltaX + deltaY * deltaY))
            If Not centerInside AndAlso distance > ballRadius Then Return False

            Dim normalX As Single
            Dim normalY As Single
            If distance > 0.0001F Then
                If centerInside Then
                    normalX = (nearestX - centerX) / distance
                    normalY = (nearestY - centerY) / distance
                Else
                    normalX = deltaX / distance
                    normalY = deltaY / distance
                End If
            Else
                Dim segmentLength As Single = CSng(Math.Sqrt(segmentLengthSquared))
                normalX = segmentY / segmentLength
                normalY = -segmentX / segmentLength
            End If

            contact.ContactX = nearestX
            contact.ContactY = nearestY
            contact.NormalX = normalX
            contact.NormalY = normalY
            contact.Penetration = If(centerInside, ballRadius + distance, ballRadius - distance)
            contact.Distance = distance
            contact.SegmentIndex = segmentIndex
            Return True
        End Function

        Private Shared Function IsBallWithinPivotSilhouetteContact(ByVal center As PointF,
                                                                   ByVal ballRadius As Single,
                                                                   ByVal pivot As B2SPictureBox,
                                                                   ByVal objectBounds As RectangleF,
                                                                   ByVal hinge As PointF) As Boolean
            Dim maskWidth As Integer = pivot.PhysicsCollisionMaskSize.Width
            Dim maskHeight As Integer = pivot.PhysicsCollisionMaskSize.Height
            If maskWidth <= 0 OrElse maskHeight <= 0 Then Return False

            Dim radians As Double = pivot.RotationAngle * Math.PI / 180.0R
            Dim cosine As Single = CSng(Math.Cos(radians))
            Dim sine As Single = CSng(Math.Sin(radians))
            Dim offsetX As Single = center.X - hinge.X
            Dim offsetY As Single = center.Y - hinge.Y
            Dim localX As Single = hinge.X + offsetX * cosine + offsetY * sine
            Dim localY As Single = hinge.Y - offsetX * sine + offsetY * cosine
            Dim ignoredX As Single = 0.0F
            Dim ignoredY As Single = 0.0F
            Dim ignoredDistance As Single = 0.0F
            Dim ignoredNormalX As Single = 0.0F
            Dim ignoredNormalY As Single = 0.0F
            Dim ignoredInside As Boolean = False
            Return TryFindPivotContourContact(localX, localY, ballRadius,
                                              pivot.PhysicsCollisionContour, maskWidth, maskHeight,
                                              objectBounds, hinge, ignoredX, ignoredY, ignoredDistance,
                                              ignoredNormalX, ignoredNormalY, ignoredInside)
        End Function

        Private Function TryResolvePivotSilhouetteCollision(ByRef center As PointF,
                                                            ByVal previousCenter As PointF,
                                                            ByVal ballRadius As Single,
                                                            ByVal ballVelocity As PointF,
                                                            ByVal pivot As B2SPictureBox,
                                                            ByVal objectBounds As RectangleF,
                                                            ByVal hinge As PointF,
                                                            ByRef normalX As Single,
                                                            ByRef normalY As Single,
                                                            ByRef contactX As Single,
                                                            ByRef contactY As Single) As Boolean
            Dim maskWidth As Integer = pivot.PhysicsCollisionMaskSize.Width
            Dim maskHeight As Integer = pivot.PhysicsCollisionMaskSize.Height
            If maskWidth <= 0 OrElse maskHeight <= 0 Then Return False

            Dim radians As Double = pivot.RotationAngle * Math.PI / 180.0R
            Dim cosine As Single = CSng(Math.Cos(radians))
            Dim sine As Single = CSng(Math.Sin(radians))
            Dim previousOffsetX As Single = previousCenter.X - hinge.X
            Dim previousOffsetY As Single = previousCenter.Y - hinge.Y
            Dim currentOffsetX As Single = center.X - hinge.X
            Dim currentOffsetY As Single = center.Y - hinge.Y
            Dim previousLocalX As Single = hinge.X + previousOffsetX * cosine + previousOffsetY * sine
            Dim previousLocalY As Single = hinge.Y - previousOffsetX * sine + previousOffsetY * cosine
            Dim currentLocalX As Single = hinge.X + currentOffsetX * cosine + currentOffsetY * sine
            Dim currentLocalY As Single = hinge.Y - currentOffsetX * sine + currentOffsetY * cosine

            ' Cheap broad phase: a segment that never reaches the rotated
            ' snippet's enclosing circle cannot reach an opaque mask pixel.
            Dim sourceCenterX As Single = objectBounds.Left + objectBounds.Width / 2.0F
            Dim sourceCenterY As Single = objectBounds.Top + objectBounds.Height / 2.0F
            Dim sweepX As Single = currentLocalX - previousLocalX
            Dim sweepY As Single = currentLocalY - previousLocalY
            Dim sweepLengthSquared As Single = sweepX * sweepX + sweepY * sweepY
            Dim closestTime As Single = 0.0F
            If sweepLengthSquared > 0.000001F Then
                closestTime = ((sourceCenterX - previousLocalX) * sweepX +
                               (sourceCenterY - previousLocalY) * sweepY) / sweepLengthSquared
                closestTime = Math.Max(0.0F, Math.Min(1.0F, closestTime))
            End If
            Dim broadX As Single = previousLocalX + sweepX * closestTime - sourceCenterX
            Dim broadY As Single = previousLocalY + sweepY * closestTime - sourceCenterY
            Dim broadRadius As Single = ballRadius +
                CSng(Math.Sqrt(objectBounds.Width * objectBounds.Width + objectBounds.Height * objectBounds.Height)) / 2.0F
            If broadX * broadX + broadY * broadY > broadRadius * broadRadius Then Return False

            Dim sweepLength As Single = CSng(Math.Sqrt(sweepLengthSquared))
            Dim sampleSpacing As Single = Math.Max(0.5F, Math.Min(2.0F, ballRadius * 0.25F))
            Dim sampleCount As Integer = Math.Max(1, CInt(Math.Ceiling(sweepLength / sampleSpacing)))
            Dim hitLocalX As Single = 0.0F
            Dim hitLocalY As Single = 0.0F
            Dim solidLocalX As Single = 0.0F
            Dim solidLocalY As Single = 0.0F
            Dim solidDistance As Single = 0.0F
            Dim localNormalX As Single = 0.0F
            Dim localNormalY As Single = 0.0F
            Dim centerInside As Boolean = False
            Dim found As Boolean = False

            For sampleIndex As Integer = 0 To sampleCount
                Dim fraction As Single = CSng(sampleIndex) / CSng(sampleCount)
                Dim sampleX As Single = previousLocalX + sweepX * fraction
                Dim sampleY As Single = previousLocalY + sweepY * fraction
                If TryFindPivotContourContact(sampleX, sampleY, ballRadius,
                                              pivot.PhysicsCollisionContour, maskWidth, maskHeight,
                                              objectBounds, hinge,
                                              solidLocalX, solidLocalY, solidDistance,
                                              localNormalX, localNormalY, centerInside) Then
                    hitLocalX = sampleX
                    hitLocalY = sampleY
                    found = True
                    Exit For
                End If
            Next
            If Not found Then Return False

            normalX = localNormalX * cosine - localNormalY * sine
            normalY = localNormalX * sine + localNormalY * cosine
            Dim separatedLocalX As Single = solidLocalX + localNormalX * (ballRadius + 0.01F)
            Dim separatedLocalY As Single = solidLocalY + localNormalY * (ballRadius + 0.01F)
            Dim separatedOffsetX As Single = separatedLocalX - hinge.X
            Dim separatedOffsetY As Single = separatedLocalY - hinge.Y
            center.X = hinge.X + separatedOffsetX * cosine - separatedOffsetY * sine
            center.Y = hinge.Y + separatedOffsetX * sine + separatedOffsetY * cosine
            Dim contactOffsetX As Single = solidLocalX - hinge.X
            Dim contactOffsetY As Single = solidLocalY - hinge.Y
            contactX = hinge.X + contactOffsetX * cosine - contactOffsetY * sine
            contactY = hinge.Y + contactOffsetX * sine + contactOffsetY * cosine
            Return True
        End Function

        Private Shared Function TryFindPivotContourContact(ByVal centerX As Single,
                                                           ByVal centerY As Single,
                                                           ByVal ballRadius As Single,
                                                           ByVal contour As PointF(),
                                                           ByVal maskWidth As Integer,
                                                           ByVal maskHeight As Integer,
                                                           ByVal objectBounds As RectangleF,
                                                           ByVal hinge As PointF,
                                                           ByRef contactX As Single,
                                                           ByRef contactY As Single,
                                                           ByRef contactDistance As Single,
                                                           ByRef normalX As Single,
                                                           ByRef normalY As Single,
                                                           ByRef centerInside As Boolean) As Boolean
            If contour Is Nothing OrElse contour.Length < 3 OrElse
               maskWidth <= 1 OrElse maskHeight <= 1 Then Return False

            Dim scaleX As Single = objectBounds.Width / (maskWidth - 1)
            Dim scaleY As Single = objectBounds.Height / (maskHeight - 1)
            Dim bestDistanceSquared As Single = Single.MaxValue
            Dim bestSegmentX As Single = 0.0F
            Dim bestSegmentY As Single = 0.0F
            centerInside = False

            For index As Integer = 0 To contour.Length - 1
                Dim nextIndex As Integer = (index + 1) Mod contour.Length
                Dim startX As Single = objectBounds.Left + contour(index).X * scaleX
                Dim startY As Single = objectBounds.Top + contour(index).Y * scaleY
                Dim endX As Single = objectBounds.Left + contour(nextIndex).X * scaleX
                Dim endY As Single = objectBounds.Top + contour(nextIndex).Y * scaleY

                If (startY > centerY) <> (endY > centerY) Then
                    Dim crossingX As Single = startX + (centerY - startY) *
                                              (endX - startX) / (endY - startY)
                    If centerX < crossingX Then centerInside = Not centerInside
                End If

                Dim segmentX As Single = endX - startX
                Dim segmentY As Single = endY - startY
                Dim segmentLengthSquared As Single = segmentX * segmentX + segmentY * segmentY
                If segmentLengthSquared <= 0.000001F Then Continue For
                Dim fraction As Single = ((centerX - startX) * segmentX +
                                          (centerY - startY) * segmentY) / segmentLengthSquared
                fraction = Math.Max(0.0F, Math.Min(1.0F, fraction))
                Dim nearestX As Single = startX + segmentX * fraction
                Dim nearestY As Single = startY + segmentY * fraction
                Dim deltaX As Single = centerX - nearestX
                Dim deltaY As Single = centerY - nearestY
                Dim distanceSquared As Single = deltaX * deltaX + deltaY * deltaY
                If distanceSquared < bestDistanceSquared Then
                    bestDistanceSquared = distanceSquared
                    contactX = nearestX
                    contactY = nearestY
                    bestSegmentX = segmentX
                    bestSegmentY = segmentY
                End If
            Next

            If bestDistanceSquared = Single.MaxValue Then Return False
            contactDistance = CSng(Math.Sqrt(bestDistanceSquared))
            If Not centerInside AndAlso contactDistance > ballRadius Then Return False

            ' Orient the contact normal from the actual contour geometry. A
            ' pivot-radial normal is wrong in a concave star valley and can push
            ' the ball into the object. Outside the polygon, the vector from the
            ' edge to the ball is outward. Inside, the shortest exit is from the
            ' ball back to the edge.
            If contactDistance > 0.0001F Then
                If centerInside Then
                    normalX = (contactX - centerX) / contactDistance
                    normalY = (contactY - centerY) / contactDistance
                Else
                    normalX = (centerX - contactX) / contactDistance
                    normalY = (centerY - contactY) / contactDistance
                End If
            Else
                Dim segmentLength As Single = CSng(Math.Sqrt(bestSegmentX * bestSegmentX +
                                                             bestSegmentY * bestSegmentY))
                If segmentLength <= 0.0001F Then Return False
                normalX = bestSegmentY / segmentLength
                normalY = -bestSegmentX / segmentLength
            End If
            Return True
        End Function

        Private Shared Function TryFindPivotMaskOverlap(ByVal centerX As Single,
                                                        ByVal centerY As Single,
                                                        ByVal ballRadius As Single,
                                                        ByVal maskAlpha As Byte(),
                                                        ByVal maskWidth As Integer,
                                                        ByVal maskHeight As Integer,
                                                        ByVal objectBounds As RectangleF,
                                                        ByRef solidX As Single,
                                                        ByRef solidY As Single,
                                                        ByRef solidDistance As Single) As Boolean
            Dim minimumX As Integer = Math.Max(0, CInt(Math.Floor((centerX - ballRadius - objectBounds.Left) /
                                                                  objectBounds.Width * maskWidth)))
            Dim maximumX As Integer = Math.Min(maskWidth - 1, CInt(Math.Ceiling((centerX + ballRadius - objectBounds.Left) /
                                                                               objectBounds.Width * maskWidth)))
            Dim minimumY As Integer = Math.Max(0, CInt(Math.Floor((centerY - ballRadius - objectBounds.Top) /
                                                                  objectBounds.Height * maskHeight)))
            Dim maximumY As Integer = Math.Min(maskHeight - 1, CInt(Math.Ceiling((centerY + ballRadius - objectBounds.Top) /
                                                                                objectBounds.Height * maskHeight)))
            If minimumX > maximumX OrElse minimumY > maximumY Then Return False

            Dim pixelWidth As Single = objectBounds.Width / maskWidth
            Dim pixelHeight As Single = objectBounds.Height / maskHeight
            Dim radiusSquared As Single = ballRadius * ballRadius
            Dim bestDistanceSquared As Single = Single.MaxValue
            For y As Integer = minimumY To maximumY
                Dim pixelY As Single = objectBounds.Top + (y + 0.5F) * pixelHeight
                For x As Integer = minimumX To maximumX
                    If maskAlpha(y * maskWidth + x) = 0 Then Continue For
                    Dim pixelX As Single = objectBounds.Left + (x + 0.5F) * pixelWidth
                    Dim dx As Single = centerX - pixelX
                    Dim dy As Single = centerY - pixelY
                    Dim distanceSquared As Single = dx * dx + dy * dy
                    If distanceSquared <= radiusSquared AndAlso distanceSquared < bestDistanceSquared Then
                        bestDistanceSquared = distanceSquared
                        solidX = pixelX
                        solidY = pixelY
                    End If
                Next
            Next
            If bestDistanceSquared = Single.MaxValue Then Return False
            solidDistance = CSng(Math.Sqrt(bestDistanceSquared))
            Return True
        End Function

        Private Sub ResolveKnockdownPivotCollision(ByRef center As PointF,
                                                   ByVal previousCenter As PointF,
                                                   ByVal ballRadius As Single,
                                                   ByRef ballVelocity As PointF,
                                                   ByVal pivot As B2SPictureBox,
                                                   ByVal objectBounds As RectangleF,
                                                   ByVal bounce As Single)
            Dim hinge As New PointF(objectBounds.Left + objectBounds.Width * pivot.RotationPivotX,
                                    objectBounds.Top + objectBounds.Height * pivot.RotationPivotY)
            Dim radians As Double = pivot.RotationAngle * Math.PI / 180.0R
            Dim cosine As Single = CSng(Math.Cos(radians))
            Dim sine As Single = CSng(Math.Sin(radians))

            Dim previousOffsetX As Single = previousCenter.X - hinge.X
            Dim previousOffsetY As Single = previousCenter.Y - hinge.Y
            Dim currentOffsetX As Single = center.X - hinge.X
            Dim currentOffsetY As Single = center.Y - hinge.Y
            Dim previousLocalX As Single = hinge.X + previousOffsetX * cosine + previousOffsetY * sine
            Dim previousLocalY As Single = hinge.Y - previousOffsetX * sine + previousOffsetY * cosine
            Dim currentLocalX As Single = hinge.X + currentOffsetX * cosine + currentOffsetY * sine
            Dim currentLocalY As Single = hinge.Y - currentOffsetX * sine + currentOffsetY * cosine

            Dim expandedLeft As Single = objectBounds.Left - ballRadius
            Dim expandedTop As Single = objectBounds.Top - ballRadius
            Dim expandedRight As Single = objectBounds.Right + ballRadius
            Dim expandedBottom As Single = objectBounds.Bottom + ballRadius
            Dim localMotionX As Single = currentLocalX - previousLocalX
            Dim localMotionY As Single = currentLocalY - previousLocalY
            Dim hitTime As Single = 0.0F
            Dim exitTime As Single = 1.0F
            Dim localNormalX As Single = 0.0F
            Dim localNormalY As Single = 0.0F

            If Not ClipPivotSweepAxis(previousLocalX, localMotionX,
                                      expandedLeft, expandedRight,
                                      -1.0F, 0.0F,
                                      hitTime, exitTime,
                                      localNormalX, localNormalY) Then Return
            If Not ClipPivotSweepAxis(previousLocalY, localMotionY,
                                      expandedTop, expandedBottom,
                                      0.0F, -1.0F,
                                      hitTime, exitTime,
                                      localNormalX, localNormalY) Then Return
            If hitTime < 0.0F OrElse hitTime > 1.0F Then Return

            Dim impactLocalX As Single = previousLocalX + localMotionX * hitTime
            Dim impactLocalY As Single = previousLocalY + localMotionY * hitTime
            If Math.Abs(localNormalX) < 0.001F AndAlso Math.Abs(localNormalY) < 0.001F Then
                Dim leftDistance As Single = Math.Abs(impactLocalX - expandedLeft)
                Dim rightDistance As Single = Math.Abs(expandedRight - impactLocalX)
                Dim topDistance As Single = Math.Abs(impactLocalY - expandedTop)
                Dim bottomDistance As Single = Math.Abs(expandedBottom - impactLocalY)
                Dim nearest As Single = Math.Min(Math.Min(leftDistance, rightDistance),
                                                 Math.Min(topDistance, bottomDistance))
                If nearest = leftDistance Then
                    localNormalX = -1.0F
                ElseIf nearest = rightDistance Then
                    localNormalX = 1.0F
                ElseIf nearest = topDistance Then
                    localNormalY = -1.0F
                Else
                    localNormalY = 1.0F
                End If
                ' The ball already overlaps the rotated target. Move its center
                ' all the way to the nearest expanded edge instead of leaving it
                ' embedded, so later contacts from either side remain physical.
                If localNormalX < -0.5F Then
                    impactLocalX = expandedLeft
                ElseIf localNormalX > 0.5F Then
                    impactLocalX = expandedRight
                ElseIf localNormalY < -0.5F Then
                    impactLocalY = expandedTop
                ElseIf localNormalY > 0.5F Then
                    impactLocalY = expandedBottom
                End If
            End If

            Dim impactOffsetX As Single = impactLocalX - hinge.X
            Dim impactOffsetY As Single = impactLocalY - hinge.Y
            center.X = hinge.X + impactOffsetX * cosine - impactOffsetY * sine
            center.Y = hinge.Y + impactOffsetX * sine + impactOffsetY * cosine
            Dim normalX As Single = localNormalX * cosine - localNormalY * sine
            Dim normalY As Single = localNormalX * sine + localNormalY * cosine
            center.X += normalX * 0.01F
            center.Y += normalY * 0.01F

            Dim contactX As Single = center.X - normalX * ballRadius
            Dim contactY As Single = center.Y - normalY * ballRadius
            Dim armX As Single = contactX - hinge.X
            Dim armY As Single = contactY - hinge.Y
            Dim angularRadians As Single = pivot.PhysicsAngularVelocity * CSng(Math.PI / 180.0R)
            Dim surfaceX As Single = -angularRadians * armY
            Dim surfaceY As Single = angularRadians * armX
            Dim incomingNormalSpeed As Single = (ballVelocity.X - surfaceX) * normalX +
                                                (ballVelocity.Y - surfaceY) * normalY
            If incomingNormalSpeed >= 0.0F Then Return
            ReflectBoundaryVelocity(ballVelocity, normalX, normalY, bounce, incomingNormalSpeed)
            pivot.ApplyPivotPhysicsAngularImpulse(1.0F)
        End Sub

        Private Shared Function ClipPivotSweepAxis(ByVal start As Single,
                                                   ByVal movement As Single,
                                                   ByVal minimum As Single,
                                                   ByVal maximum As Single,
                                                   ByVal minimumNormalX As Single,
                                                   ByVal minimumNormalY As Single,
                                                   ByRef enterTime As Single,
                                                   ByRef exitTime As Single,
                                                   ByRef normalX As Single,
                                                   ByRef normalY As Single) As Boolean
            If Math.Abs(movement) < 0.000001F Then
                Return start >= minimum AndAlso start <= maximum
            End If

            Dim first As Single = (minimum - start) / movement
            Dim last As Single = (maximum - start) / movement
            Dim firstNormalX As Single = minimumNormalX
            Dim firstNormalY As Single = minimumNormalY
            If first > last Then
                Dim swap As Single = first
                first = last
                last = swap
                firstNormalX = -minimumNormalX
                firstNormalY = -minimumNormalY
            End If

            If first > enterTime Then
                enterTime = first
                normalX = firstNormalX
                normalY = firstNormalY
            End If
            If last < exitTime Then exitTime = last
            Return enterTime <= exitTime AndAlso exitTime >= 0.0F AndAlso enterTime <= 1.0F
        End Function

        Private Sub ResolveStaticFloorCollision(ByRef center As PointF, ByVal previousCenter As PointF,
                                                 ByVal radius As Single, ByRef ballVelocity As PointF,
                                                 ByVal fromPoint As PointF, ByVal toPoint As PointF,
                                                 ByVal bounce As Single)
            Dim segmentX As Single = toPoint.X - fromPoint.X
            Dim segmentY As Single = toPoint.Y - fromPoint.Y
            Dim lengthSquared As Single = segmentX * segmentX + segmentY * segmentY
            If lengthSquared <= 0.01F Then Return
            Dim rawFraction As Single =
                ((center.X - fromPoint.X) * segmentX + (center.Y - fromPoint.Y) * segmentY) / lengthSquared
            Dim fraction As Single = Math.Max(0.0F, Math.Min(1.0F, rawFraction))
            Dim contactX As Single = fromPoint.X + segmentX * fraction
            Dim contactY As Single = fromPoint.Y + segmentY * fraction
            Dim offsetX As Single = center.X - contactX
            Dim offsetY As Single = center.Y - contactY
            Dim distanceSquared As Single = offsetX * offsetX + offsetY * offsetY
            If distanceSquared >= radius * radius Then
                ' A fast ball can cross the entire boundary between timer steps and
                ' finish beyond it without overlapping. Sweep the ball's full path
                ' against the segment capsule before treating this as no collision.
                Dim sweptCenter As PointF
                Dim sweptNormal As PointF
                If Not TryGetSweptStaticFloorCollision(previousCenter, center, radius,
                                                       fromPoint, toPoint,
                                                       sweptCenter, sweptNormal) Then Return

                center.X = sweptCenter.X + sweptNormal.X * 0.01F
                center.Y = sweptCenter.Y + sweptNormal.Y * 0.01F
                Dim sweptNormalSpeed As Single =
                    ballVelocity.X * sweptNormal.X + ballVelocity.Y * sweptNormal.Y
                If sweptNormalSpeed < 0.0F Then
                    ReflectBoundaryVelocity(ballVelocity, sweptNormal.X, sweptNormal.Y, bounce, sweptNormalSpeed)
                End If
                Return
            End If

            Dim distance As Single = CSng(Math.Sqrt(distanceSquared))
            Dim normalX As Single
            Dim normalY As Single
            If rawFraction < 0.0F OrElse rawFraction > 1.0F Then
                Dim previousOffsetX As Single = previousCenter.X - contactX
                Dim previousOffsetY As Single = previousCenter.Y - contactY
                Dim previousDistance As Single = CSng(Math.Sqrt(previousOffsetX * previousOffsetX + previousOffsetY * previousOffsetY))
                ' Resolve against the ball's current radial direction so a slow
                ' contact can roll around a segment endpoint instead of being
                ' anchored to its previous position until enough speed builds.
                If distance >= 0.001F Then
                    normalX = offsetX / distance
                    normalY = offsetY / distance
                ElseIf previousDistance >= 0.001F Then
                    normalX = previousOffsetX / previousDistance
                    normalY = previousOffsetY / previousDistance
                Else
                    Return
                End If
            Else
                Dim segmentLength As Single = CSng(Math.Sqrt(lengthSquared))
                normalX = segmentY / segmentLength
                normalY = -segmentX / segmentLength
                Dim previousSide As Single =
                    (previousCenter.X - contactX) * normalX + (previousCenter.Y - contactY) * normalY
                If Math.Abs(previousSide) < 0.001F Then
                    previousSide = offsetX * normalX + offsetY * normalY
                End If
                If Math.Abs(previousSide) < 0.001F Then
                    If ballVelocity.X * normalX + ballVelocity.Y * normalY > 0.0F Then
                        normalX = -normalX
                        normalY = -normalY
                    End If
                ElseIf previousSide < 0.0F Then
                    normalX = -normalX
                    normalY = -normalY
                End If
            End If
            center.X = contactX + normalX * radius
            center.Y = contactY + normalY * radius
            Dim normalSpeed As Single = ballVelocity.X * normalX + ballVelocity.Y * normalY
            If normalSpeed < 0.0F Then
                ReflectBoundaryVelocity(ballVelocity, normalX, normalY, bounce, normalSpeed)
            End If
        End Sub

        Private Shared Sub ReflectBoundaryVelocity(ByRef ballVelocity As PointF,
                                                    ByVal normalX As Single,
                                                    ByVal normalY As Single,
                                                    ByVal bounce As Single,
                                                    ByVal incomingNormalSpeed As Single)
            ballVelocity.X -= (1.0F + bounce) * incomingNormalSpeed * normalX
            ballVelocity.Y -= (1.0F + bounce) * incomingNormalSpeed * normalY
            If -incomingNormalSpeed < BoundaryScatterMinimumImpactSpeed Then Return

            Dim scatterDegrees As Single
            SyncLock boundaryScatterRandom
                scatterDegrees = CSng((boundaryScatterRandom.NextDouble() * 2.0R - 1.0R) * BoundaryScatterDegrees)
            End SyncLock
            RotateBoundaryVelocity(ballVelocity, normalX, normalY, scatterDegrees)
        End Sub

        Private Shared Sub RotateBoundaryVelocity(ByRef ballVelocity As PointF,
                                                   ByVal normalX As Single,
                                                   ByVal normalY As Single,
                                                   ByVal degrees As Single)
            Dim radians As Double = degrees * Math.PI / 180.0R
            Dim cosine As Single = CSng(Math.Cos(radians))
            Dim sine As Single = CSng(Math.Sin(radians))
            Dim rotatedX As Single = ballVelocity.X * cosine - ballVelocity.Y * sine
            Dim rotatedY As Single = ballVelocity.X * sine + ballVelocity.Y * cosine
            If rotatedX * normalX + rotatedY * normalY <= 0.0F Then Return
            ballVelocity.X = rotatedX
            ballVelocity.Y = rotatedY
        End Sub

        Private Function TryGetSweptStaticFloorCollision(ByVal previousCenter As PointF,
                                                         ByVal currentCenter As PointF,
                                                         ByVal radius As Single,
                                                         ByVal fromPoint As PointF,
                                                         ByVal toPoint As PointF,
                                                         ByRef impactCenter As PointF,
                                                         ByRef impactNormal As PointF) As Boolean
            Dim motionX As Single = currentCenter.X - previousCenter.X
            Dim motionY As Single = currentCenter.Y - previousCenter.Y
            Dim motionLengthSquared As Single = motionX * motionX + motionY * motionY
            If motionLengthSquared <= 0.000001F Then Return False

            Dim segmentX As Single = toPoint.X - fromPoint.X
            Dim segmentY As Single = toPoint.Y - fromPoint.Y
            Dim segmentLengthSquared As Single = segmentX * segmentX + segmentY * segmentY
            If segmentLengthSquared <= 0.01F Then Return False

            Dim segmentLength As Single = CSng(Math.Sqrt(segmentLengthSquared))
            Dim tangentX As Single = segmentX / segmentLength
            Dim tangentY As Single = segmentY / segmentLength
            Dim baseNormalX As Single = tangentY
            Dim baseNormalY As Single = -tangentX
            Dim previousSignedDistance As Single =
                (previousCenter.X - fromPoint.X) * baseNormalX +
                (previousCenter.Y - fromPoint.Y) * baseNormalY
            Dim currentSignedDistance As Single =
                (currentCenter.X - fromPoint.X) * baseNormalX +
                (currentCenter.Y - fromPoint.Y) * baseNormalY
            Dim signedDistanceChange As Single = currentSignedDistance - previousSignedDistance
            Dim bestTime As Single = Single.MaxValue
            Dim found As Boolean = False

            ' A collision with a neighboring corner segment can push the ball
            ' inside this segment after it was already processed for the step.
            ' If the next movement crosses from that embedded position to the
            ' opposite side, restore it to the side it occupied before crossing.
            Dim previousProjection As Single =
                (previousCenter.X - fromPoint.X) * tangentX +
                (previousCenter.Y - fromPoint.Y) * tangentY
            Dim currentProjection As Single =
                (currentCenter.X - fromPoint.X) * tangentX +
                (currentCenter.Y - fromPoint.Y) * tangentY
            If previousProjection >= 0.0F AndAlso previousProjection <= segmentLength AndAlso
               currentProjection >= 0.0F AndAlso currentProjection <= segmentLength AndAlso
               Math.Abs(previousSignedDistance) < radius AndAlso
               previousSignedDistance * currentSignedDistance <= 0.0F AndAlso
               Math.Abs(currentSignedDistance) >= radius Then
                Dim retainedSide As Single
                If Math.Abs(previousSignedDistance) >= 0.000001F Then
                    retainedSide = If(previousSignedDistance > 0.0F, 1.0F, -1.0F)
                Else
                    retainedSide = If(signedDistanceChange < 0.0F, 1.0F, -1.0F)
                End If
                impactCenter = New PointF(fromPoint.X + tangentX * previousProjection + baseNormalX * retainedSide * radius,
                                          fromPoint.Y + tangentY * previousProjection + baseNormalY * retainedSide * radius)
                impactNormal = New PointF(baseNormalX * retainedSide, baseNormalY * retainedSide)
                Return True
            End If

            ' Test the two parallel sides of the segment's radius-expanded capsule.
            For sideIndex As Integer = 0 To 1
                Dim sideSign As Single = If(sideIndex = 0, 1.0F, -1.0F)
                If sideSign * signedDistanceChange < -0.000001F Then
                    Dim hitTime As Single =
                        (sideSign * radius - previousSignedDistance) / signedDistanceChange
                    If hitTime >= 0.0F AndAlso hitTime <= 1.0F AndAlso hitTime < bestTime Then
                        Dim hitX As Single = previousCenter.X + motionX * hitTime
                        Dim hitY As Single = previousCenter.Y + motionY * hitTime
                        Dim segmentProjection As Single =
                            (hitX - fromPoint.X) * tangentX + (hitY - fromPoint.Y) * tangentY
                        If segmentProjection >= 0.0F AndAlso segmentProjection <= segmentLength Then
                            bestTime = hitTime
                            impactCenter = New PointF(hitX, hitY)
                            impactNormal = New PointF(baseNormalX * sideSign, baseNormalY * sideSign)
                            found = True
                        End If
                    End If
                End If
            Next

            ' Test the round caps so high-speed movement cannot skip a boundary end.
            For endpointIndex As Integer = 0 To 1
                Dim endpoint As PointF = If(endpointIndex = 0, fromPoint, toPoint)
                Dim relativeX As Single = previousCenter.X - endpoint.X
                Dim relativeY As Single = previousCenter.Y - endpoint.Y
                Dim circleConstant As Single =
                    relativeX * relativeX + relativeY * relativeY - radius * radius
                If circleConstant >= 0.0F Then
                    Dim circleLinear As Single = 2.0F * (relativeX * motionX + relativeY * motionY)
                    Dim discriminant As Double =
                        CDbl(circleLinear) * CDbl(circleLinear) -
                        4.0R * CDbl(motionLengthSquared) * CDbl(circleConstant)
                    If discriminant >= 0.0R Then
                        Dim hitTime As Single =
                            CSng((-CDbl(circleLinear) - Math.Sqrt(discriminant)) /
                                 (2.0R * CDbl(motionLengthSquared)))
                        If hitTime >= 0.0F AndAlso hitTime <= 1.0F AndAlso hitTime < bestTime Then
                            Dim hitX As Single = previousCenter.X + motionX * hitTime
                            Dim hitY As Single = previousCenter.Y + motionY * hitTime
                            Dim normalX As Single = hitX - endpoint.X
                            Dim normalY As Single = hitY - endpoint.Y
                            Dim normalLength As Single = CSng(Math.Sqrt(normalX * normalX + normalY * normalY))
                            If normalLength >= 0.001F Then
                                bestTime = hitTime
                                impactCenter = New PointF(hitX, hitY)
                                impactNormal = New PointF(normalX / normalLength, normalY / normalLength)
                                found = True
                            End If
                        End If
                    End If
                End If
            Next

            Return found
        End Function

        Private Sub ResolveFlipperCollision(ByRef center As PointF, ByVal ballRadius As Single, ByRef ballVelocity As PointF,
                                            ByVal flipper As B2SPictureBox, ByVal angularVelocity As Single)
            Dim flipperBounds As RectangleF = AuthoredPivotBounds(flipper)
            Dim hinge As New PointF(flipperBounds.Left + flipperBounds.Width * flipper.RotationPivotX,
                                    flipperBounds.Top + flipperBounds.Height * flipper.RotationPivotY)
            Dim baseTipX As Single = flipperBounds.Left + flipperBounds.Width * flipper.RotationTipX
            Dim baseTipY As Single = flipperBounds.Top + flipperBounds.Height * flipper.RotationTipY
            Dim radians As Double = flipper.RotationAngle * Math.PI / 180.0R
            Dim sourceX As Double = baseTipX - hinge.X
            Dim sourceY As Double = baseTipY - hinge.Y
            Dim tip As New PointF(CSng(hinge.X + sourceX * Math.Cos(radians) - sourceY * Math.Sin(radians)),
                                  CSng(hinge.Y + sourceX * Math.Sin(radians) + sourceY * Math.Cos(radians)))
            Dim segmentX As Single = tip.X - hinge.X
            Dim segmentY As Single = tip.Y - hinge.Y
            Dim lengthSquared As Single = segmentX * segmentX + segmentY * segmentY
            If lengthSquared <= 0.01F Then Return
            Dim rawFraction As Single = ((center.X - hinge.X) * segmentX + (center.Y - hinge.Y) * segmentY) / lengthSquared
            Dim fraction As Single = Math.Max(0.0F, Math.Min(1.0F, rawFraction))
            Dim contact As New PointF(hinge.X + segmentX * fraction, hinge.Y + segmentY * fraction)
            Dim segmentLength As Single = CSng(Math.Sqrt(lengthSquared))
            Dim normalX As Single = segmentY / segmentLength
            Dim normalY As Single = -segmentX / segmentLength
            If normalY > 0.0F Then normalX = -normalX : normalY = -normalY
            Dim flipperRadius As Single = Math.Max(3.0F, Math.Min(flipperBounds.Width, flipperBounds.Height) * 0.07F)
            Dim minimumDistance As Single = ballRadius + flipperRadius
            If rawFraction < 0.0F OrElse rawFraction > 1.0F Then
                Dim endX As Single = center.X - contact.X
                Dim endY As Single = center.Y - contact.Y
                Dim endDistance As Single = CSng(Math.Sqrt(endX * endX + endY * endY))
                If endDistance >= minimumDistance OrElse endDistance < 0.001F Then Return
                normalX = endX / endDistance : normalY = endY / endDistance
                If normalY > 0.0F Then Return
                center.X += normalX * (minimumDistance - endDistance)
                center.Y += normalY * (minimumDistance - endDistance)
            Else
                Dim signedDistance As Single = (center.X - contact.X) * normalX + (center.Y - contact.Y) * normalY
                If signedDistance >= minimumDistance Then Return
                center.X += normalX * (minimumDistance - signedDistance)
                center.Y += normalY * (minimumDistance - signedDistance)
            End If
            Dim armX As Single = contact.X - hinge.X
            Dim armY As Single = contact.Y - hinge.Y
            Dim impactStrength As Single = flipperStrength
            Dim variation As Single = flipper.PivotRandomStrength
            If angularVelocity <> 0.0F AndAlso variation > 0.0F AndAlso Not Single.IsInfinity(variation) Then
                variation = Math.Min(100.0F, variation)
                SyncLock flipperStrengthRandom
                    impactStrength *= 1.0F + CSng((flipperStrengthRandom.NextDouble() * 2.0R - 1.0R) * variation / 100.0R)
                End SyncLock
            End If
            Dim surfaceX As Single = -angularVelocity * armY * impactStrength
            Dim surfaceY As Single = angularVelocity * armX * impactStrength
            Dim relativeX As Single = ballVelocity.X - surfaceX
            Dim relativeY As Single = ballVelocity.Y - surfaceY
            Dim towardSurface As Single = relativeX * normalX + relativeY * normalY
            If towardSurface < 0.0F Then
                Const restitution As Single = 0.58F
                relativeX -= (1.0F + restitution) * towardSurface * normalX
                relativeY -= (1.0F + restitution) * towardSurface * normalY
                ballVelocity.X = surfaceX + relativeX
                ballVelocity.Y = surfaceY + relativeY
            End If
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            timer.Stop()
            RemoveHandler timer.Tick, AddressOf Tick
            timer.Dispose()
            clock.Stop()
        End Sub
    End Class

End Class
#If PHYSICS_PREVIEW Then
End Namespace
#End If
