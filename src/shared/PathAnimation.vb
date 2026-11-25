Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization

' B2S Pro Path Animation.
' Shared by the Designer (editor, test panel, project save, export) and the
' Server (load and runtime) so an image moves the same way in both.
' Coordinates are image-snippet CENTERS in backglass-image pixels, the same
' convention as the existing Motion Path feature. Nothing here is used unless
' a snippet has PathAnim="1", so existing and legacy files are unaffected.

Public Enum PathAnimTriggerType
    None = 0
    Solenoid = 1
    Lamp = 2
    B2SID = 3
    Switch = 4
End Enum

Public Enum PathAnimDriveMode
    GoToStop = 0
    StepStops = 1
    RunStop = 2
    PositionValue = 3
    ' Pop up / drop: moves to the end of the path while the trigger is on and
    ' back to the start when it turns off (drop targets, pop-up cards, ...).
    PopDrop = 4
End Enum

Public Structure PathAnimTrigger
    Public Type As PathAnimTriggerType
    Public ID As Integer

    Public Sub New(ByVal type As PathAnimTriggerType, ByVal id As Integer)
        Me.Type = type
        Me.ID = id
    End Sub

    Public ReadOnly Property IsSet As Boolean
        Get
            Return Type <> PathAnimTriggerType.None AndAlso ID > 0
        End Get
    End Property

    Public Function ToText() As String
        If Not IsSet Then Return "0:0"
        Return CInt(Type).ToString(CultureInfo.InvariantCulture) & ":" & ID.ToString(CultureInfo.InvariantCulture)
    End Function

    Public Shared Function Parse(ByVal text As String) As PathAnimTrigger
        Dim result As New PathAnimTrigger(PathAnimTriggerType.None, 0)
        If String.IsNullOrWhiteSpace(text) Then Return result
        Dim parts As String() = text.Trim().Split(":"c)
        If parts.Length <> 2 Then Return result
        Dim t As Integer, id As Integer
        If Not Integer.TryParse(parts(0), NumberStyles.Integer, CultureInfo.InvariantCulture, t) Then Return result
        If Not Integer.TryParse(parts(1), NumberStyles.Integer, CultureInfo.InvariantCulture, id) Then Return result
        If t < 1 OrElse t > 4 OrElse id < 1 OrElse id > 999 Then Return result
        Return New PathAnimTrigger(CType(t, PathAnimTriggerType), id)
    End Function

    Public Overrides Function ToString() As String
        If Not IsSet Then Return "none"
        Select Case Type
            Case PathAnimTriggerType.Solenoid : Return "Solenoid " & ID
            Case PathAnimTriggerType.Lamp : Return "Lamp " & ID
            Case PathAnimTriggerType.B2SID : Return "B2S ID " & ID
            Case Else : Return "SW " & ID
        End Select
    End Function
End Structure

' A table switch placed on the path: SwitchID is pulsed each time the snippet
' crosses Position (fraction 0..1 of the path length).
Public Structure PathAnimSwitch
    Public SwitchID As Integer
    Public Position As Single

    Public Sub New(ByVal switchID As Integer, ByVal position As Single)
        Me.SwitchID = switchID
        Me.Position = position
    End Sub
End Structure

Public Class PathAnimSettings
    Public Enabled As Boolean = False
    Public Points As New List(Of PointF)()
    Public Closed As Boolean = False
    Public Smooth As Boolean = False
    ' Stop positions as fractions (0..1) of the path length, ascending.
    Public Stops As New List(Of Single)()
    Public Mode As PathAnimDriveMode = PathAnimDriveMode.GoToStop
    Public StopTriggers As New List(Of PathAnimTrigger)()
    ' Step mode: each rising edge goes to the next stop.
    Public ForwardTrigger As PathAnimTrigger
    ' Motor mode: moves forward while this is on, stops when it goes off.
    Public RunTrigger As PathAnimTrigger
    Public ValueTrigger As PathAnimTrigger
    Public HomeTrigger As PathAnimTrigger
    ' Image pixels per second; 0 jumps straight to the target.
    Public Speed As Single = 600.0F
    Public Rotate As Boolean = False
    ' Direction the artwork faces, in degrees (0 = right, 90 = down).
    Public RotateOffset As Single = 0.0F
    ' Editor choice: keep one stop on each drawn point (Designer only).
    Public StopsAtPoints As Boolean = False
    ' Switches on the track: the table switch is pulsed each time the snippet
    ' crosses that spot, like a ball rolling over a rollover switch.
    Public Switches As New List(Of PathAnimSwitch)()
    ' Magnet: ID of the snippet this one picks up by touch and carries
    ' (0 = none). DropTrigger lets it go; ReturnAfterDrop sends this snippet
    ' back to the start of its path after the drop (otherwise it stays).
    Public CarryID As Integer = 0
    Public DropTrigger As PathAnimTrigger
    Public ReturnAfterDrop As Boolean = False
    ' Magnet: also let go automatically at the end of the path (open path:
    ' arriving at its last point; closed loop: coming back around to the start).
    Public DropAtEnd As Boolean = False
    ' Pop up / drop: a small bounce where the image lands (0 = none, 100 =
    ' the most, a rebound of 6 % of the move). Only used by that drive.
    Public Bounce As Integer = 0
    ' Pop up / drop: speed back to the start when the trigger turns off
    ' (-1 = the same as Speed). Only used by that drive.
    Public ReturnSpeed As Single = -1.0F

    Public Const MaxStops As Integer = 200
    Public Const MaxSwitches As Integer = 32
    Public Const MaxSwitchID As Integer = 255

    Public Function Clone() As PathAnimSettings
        Dim copy As PathAnimSettings = DirectCast(Me.MemberwiseClone(), PathAnimSettings)
        copy.Points = New List(Of PointF)(Points)
        copy.Stops = New List(Of Single)(Stops)
        copy.StopTriggers = New List(Of PathAnimTrigger)(StopTriggers)
        copy.Switches = New List(Of PathAnimSwitch)(Switches)
        Return copy
    End Function

    Public ReadOnly Property IsUsable As Boolean
        Get
            Return Enabled AndAlso Points.Count >= 2 AndAlso Stops.Count >= 1
        End Get
    End Property

    Public Function StopTrigger(ByVal index As Integer) As PathAnimTrigger
        If index < 0 OrElse index >= StopTriggers.Count Then Return New PathAnimTrigger(PathAnimTriggerType.None, 0)
        Return StopTriggers(index)
    End Function

    ' Keeps stops sorted, in range and matched one-to-one with their triggers.
    Public Sub Normalize()
        Dim pairs As New List(Of KeyValuePair(Of Single, PathAnimTrigger))()
        For index As Integer = 0 To Stops.Count - 1
            Dim f As Single = Math.Max(0.0F, Math.Min(1.0F, Stops(index)))
            If Closed AndAlso f >= 1.0F Then f = 0.0F
            pairs.Add(New KeyValuePair(Of Single, PathAnimTrigger)(f, StopTrigger(index)))
        Next
        pairs.Sort(Function(a, b) a.Key.CompareTo(b.Key))
        Stops.Clear()
        StopTriggers.Clear()
        For Each pair In pairs
            If Stops.Count >= MaxStops Then Exit For
            If Stops.Count > 0 AndAlso Math.Abs(Stops(Stops.Count - 1) - pair.Key) < 0.0001F Then Continue For
            Stops.Add(pair.Key)
            StopTriggers.Add(pair.Value)
        Next
        Speed =Math.Max(0.0F, Math.Min(20000.0F, Speed))
        Bounce = Math.Max(0, Math.Min(100, Bounce))
        If ReturnSpeed < 0 Then ReturnSpeed = -1.0F Else ReturnSpeed = Math.Min(20000.0F, ReturnSpeed)
        Dim cleanSwitches As New List(Of PathAnimSwitch)()
        For Each sw As PathAnimSwitch In Switches
            If sw.SwitchID < 1 OrElse sw.SwitchID > MaxSwitchID Then Continue For
            Dim f As Single = Math.Max(0.0F, Math.Min(1.0F, sw.Position))
            If Closed AndAlso f >= 1.0F Then f = 0.0F
            cleanSwitches.Add(New PathAnimSwitch(sw.SwitchID, f))
            If cleanSwitches.Count >= MaxSwitches Then Exit For
        Next
        Switches = cleanSwitches
    End Sub

    ' Drops every setting the chosen drive does not use, so nothing hidden is
    ' saved or exported. A motor uses no stops (one stop at the path start is
    ' kept as the resting reference).
    Public Sub ClearUnusedForMode()
        Dim none As New PathAnimTrigger()
        If Mode <> PathAnimDriveMode.StepStops Then ForwardTrigger = none
        If Mode <> PathAnimDriveMode.RunStop AndAlso Mode <> PathAnimDriveMode.PopDrop Then RunTrigger = none
        If Mode <> PathAnimDriveMode.PopDrop Then
            Bounce = 0
            ReturnSpeed = -1.0F
        End If
        If Mode <> PathAnimDriveMode.PositionValue Then ValueTrigger = none
        If Mode <> PathAnimDriveMode.GoToStop Then
            For index As Integer = 0 To StopTriggers.Count - 1
                StopTriggers(index) = none
            Next
        End If
        If CarryID <= 0 Then
            CarryID = 0
            DropTrigger = none
            ReturnAfterDrop = False
            DropAtEnd = False
        End If
        If Mode = PathAnimDriveMode.RunStop Then
            Stops.Clear()
            Stops.Add(0.0F)
            StopTriggers.Clear()
            StopTriggers.Add(none)
            StopsAtPoints = False
        End If
        If Mode = PathAnimDriveMode.PopDrop Then
            ' Two places only: the start (trigger off) and the end (trigger on).
            Closed = False
            Stops.Clear()
            Stops.AddRange(EvenStops(2, False))
            StopTriggers.Clear()
            StopTriggers.Add(none)
            StopTriggers.Add(none)
            StopsAtPoints = False
        End If
    End Sub

    ' Evenly spaced stops. An open path gets both ends; a closed loop does not
    ' repeat its start at the end.
    Public Shared Function EvenStops(ByVal count As Integer, ByVal closed As Boolean) As List(Of Single)
        Dim result As New List(Of Single)()
        count = Math.Max(1, Math.Min(MaxStops, count))
        If count = 1 Then
            result.Add(0.0F)
        ElseIf closed Then
            For index As Integer = 0 To count - 1
                result.Add(CSng(index / count))
            Next
        Else
            For index As Integer = 0 To count - 1
                result.Add(CSng(index / (count - 1)))
            Next
        End If
        Return result
    End Function

    ' ---- XML (project file and exported backglass use the same attributes) ----

    Public Sub WriteTo(ByVal node As Xml.XmlElement)
        If Not Enabled OrElse Points.Count < 2 Then Return
        ClearUnusedForMode()
        Normalize()
        Dim inv As CultureInfo = CultureInfo.InvariantCulture
        node.SetAttribute("PathAnim", "1")
        node.SetAttribute("PathAnimPoints", SerializePoints(Points))
        If Closed Then node.SetAttribute("PathAnimClosed", "1")
        If Smooth Then node.SetAttribute("PathAnimSmooth", "1")
        Dim stopText As New List(Of String)()
        For Each f As Single In Stops
            stopText.Add(f.ToString("R", inv))
        Next
        node.SetAttribute("PathAnimStops", String.Join(";", stopText))
        node.SetAttribute("PathAnimMode", CInt(Mode).ToString(inv))
        If Mode = PathAnimDriveMode.GoToStop Then
            Dim triggerText As New List(Of String)()
            For index As Integer = 0 To Stops.Count - 1
                triggerText.Add(StopTrigger(index).ToText())
            Next
            node.SetAttribute("PathAnimStopTriggers", String.Join(";", triggerText))
        End If
        If ForwardTrigger.IsSet Then node.SetAttribute("PathAnimForward", ForwardTrigger.ToText())
        If RunTrigger.IsSet Then node.SetAttribute("PathAnimRun", RunTrigger.ToText())
        If ValueTrigger.IsSet Then node.SetAttribute("PathAnimValue", ValueTrigger.ToText())
        If HomeTrigger.IsSet Then node.SetAttribute("PathAnimHome", HomeTrigger.ToText())
        node.SetAttribute("PathAnimSpeed", Speed.ToString("R", inv))
        If Rotate Then
            node.SetAttribute("PathAnimRotate", "1")
            node.SetAttribute("PathAnimRotateOffset", RotateOffset.ToString("R", inv))
        End If
        If StopsAtPoints Then node.SetAttribute("PathAnimStopsAtPoints", "1")
        If Switches.Count > 0 Then
            Dim switchText As New List(Of String)()
            For Each sw As PathAnimSwitch In Switches
                switchText.Add(sw.SwitchID.ToString(inv) & "@" & sw.Position.ToString("R", inv))
            Next
            node.SetAttribute("PathAnimSwitches", String.Join(";", switchText))
        End If
        If CarryID > 0 Then
            node.SetAttribute("PathAnimCarry", CarryID.ToString(inv))
            If DropTrigger.IsSet Then node.SetAttribute("PathAnimDrop", DropTrigger.ToText())
            If ReturnAfterDrop Then node.SetAttribute("PathAnimDropReturn", "1")
            If DropAtEnd Then node.SetAttribute("PathAnimDropAtEnd", "1")
        End If
        If Bounce > 0 Then node.SetAttribute("PathAnimBounce", Bounce.ToString(inv))
        If ReturnSpeed >= 0 Then node.SetAttribute("PathAnimReturnSpeed", ReturnSpeed.ToString("R", inv))
    End Sub

    ' Returns Nothing when the node has no Path Animation.
    Public Shared Function ReadFrom(ByVal node As Xml.XmlNode) As PathAnimSettings
        If node Is Nothing OrElse node.Attributes Is Nothing Then Return Nothing
        If Attr(node, "PathAnim") <> "1" Then Return Nothing
        Dim s As New PathAnimSettings() With {.Enabled = True}
        s.Points.AddRange(ParsePoints(Attr(node, "PathAnimPoints")))
        If s.Points.Count < 2 Then Return Nothing
        s.Closed = (Attr(node, "PathAnimClosed") = "1")
        s.Smooth = (Attr(node, "PathAnimSmooth") = "1")
        For Each part As String In Attr(node, "PathAnimStops").Split(";"c)
            Dim f As Single
            If Single.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, f) Then s.Stops.Add(f)
        Next
        If s.Stops.Count = 0 Then s.Stops.AddRange(EvenStops(2, s.Closed))
        s.Mode = CType(Math.Max(0, Math.Min(4, ReadInt(node, "PathAnimMode", 0))), PathAnimDriveMode)
        Dim triggers As String() = Attr(node, "PathAnimStopTriggers").Split(";"c)
        For index As Integer = 0 To s.Stops.Count - 1
            s.StopTriggers.Add(If(index < triggers.Length, PathAnimTrigger.Parse(triggers(index)), New PathAnimTrigger()))
        Next
        s.ForwardTrigger = PathAnimTrigger.Parse(Attr(node, "PathAnimForward"))
        s.RunTrigger = PathAnimTrigger.Parse(Attr(node, "PathAnimRun"))
        s.ValueTrigger = PathAnimTrigger.Parse(Attr(node, "PathAnimValue"))
        s.HomeTrigger = PathAnimTrigger.Parse(Attr(node, "PathAnimHome"))
        s.Speed = ReadSingle(node, "PathAnimSpeed", 600.0F)
        s.Rotate = (Attr(node, "PathAnimRotate") = "1")
        s.RotateOffset = ReadSingle(node, "PathAnimRotateOffset", 0.0F)
        s.StopsAtPoints = (Attr(node, "PathAnimStopsAtPoints") = "1")
        For Each part As String In Attr(node, "PathAnimSwitches").Split(";"c)
            Dim pieces As String() = part.Split("@"c)
            If pieces.Length <> 2 Then Continue For
            Dim id As Integer, f As Single
            If Integer.TryParse(pieces(0), NumberStyles.Integer, CultureInfo.InvariantCulture, id) AndAlso
               Single.TryParse(pieces(1), NumberStyles.Float, CultureInfo.InvariantCulture, f) Then
                s.Switches.Add(New PathAnimSwitch(id, f))
            End If
        Next
        s.CarryID = Math.Max(0, ReadInt(node, "PathAnimCarry", 0))
        s.DropTrigger = PathAnimTrigger.Parse(Attr(node, "PathAnimDrop"))
        s.ReturnAfterDrop = (Attr(node, "PathAnimDropReturn") = "1")
        s.DropAtEnd = (Attr(node, "PathAnimDropAtEnd") = "1")
        s.Bounce = ReadInt(node, "PathAnimBounce", 0)
        s.ReturnSpeed = ReadSingle(node, "PathAnimReturnSpeed", -1.0F)
        s.Normalize()
        Return s
    End Function

    Private Shared Function Attr(ByVal node As Xml.XmlNode, ByVal name As String) As String
        Dim a As Xml.XmlAttribute = node.Attributes(name)
        Return If(a Is Nothing, String.Empty, a.InnerText)
    End Function

    Private Shared Function ReadInt(ByVal node As Xml.XmlNode, ByVal name As String, ByVal fallback As Integer) As Integer
        Dim v As Integer
        Return If(Integer.TryParse(Attr(node, name), NumberStyles.Integer, CultureInfo.InvariantCulture, v), v, fallback)
    End Function

    Private Shared Function ReadSingle(ByVal node As Xml.XmlNode, ByVal name As String, ByVal fallback As Single) As Single
        Dim v As Single
        Return If(Single.TryParse(Attr(node, name), NumberStyles.Float, CultureInfo.InvariantCulture, v), v, fallback)
    End Function

    Public Shared Function SerializePoints(ByVal points As IEnumerable(Of PointF)) As String
        Dim parts As New List(Of String)()
        For Each p As PointF In points
            parts.Add(p.X.ToString("R", CultureInfo.InvariantCulture) & "," & p.Y.ToString("R", CultureInfo.InvariantCulture))
        Next
        Return String.Join(";", parts)
    End Function

    Public Shared Function ParsePoints(ByVal text As String) As List(Of PointF)
        Dim result As New List(Of PointF)()
        If String.IsNullOrWhiteSpace(text) Then Return result
        For Each part As String In text.Split(";"c)
            Dim xy As String() = part.Split(","c)
            If xy.Length <> 2 Then Continue For
            Dim x As Single, y As Single
            If Single.TryParse(xy(0), NumberStyles.Float, CultureInfo.InvariantCulture, x) AndAlso
               Single.TryParse(xy(1), NumberStyles.Float, CultureInfo.InvariantCulture, y) Then
                result.Add(New PointF(x, y))
            End If
        Next
        Return result
    End Function
End Class

' Magnet: a path snippet picks up another snippet by touch, carries it at the
' same spot relative to itself (turning with it), and lets go on a trigger.
' All values are backglass-image pixels; the Designer test and the Server use
' this same class so both behave alike. After a drop it will not pick the
' snippet up again until the two have separated and touch again.
Public Class PathAnimMagnet
    Private carryingValue As Boolean
    Private mustSeparate As Boolean
    Private offsetLocal As PointF
    Private returningValue As Boolean

    Public ReadOnly Property Carrying As Boolean
        Get
            Return carryingValue
        End Get
    End Property

    ' True while the carrier drives back to its start after letting go. The
    ' magnet is off for that trip, so it cannot grab what it just dropped.
    Public ReadOnly Property Returning As Boolean
        Get
            Return returningValue
        End Get
    End Property

    Public Sub StartReturn()
        returningValue = True
    End Sub

    ' Call when the carrier has stopped: the magnet is on again.
    Public Sub ReturnFinished()
        returningValue = False
    End Sub

    Public Sub Reset()
        carryingValue = False
        mustSeparate = False
        offsetLocal = PointF.Empty
        returningValue = False
    End Sub

    ' carrierAngle is the carrier image's turn in degrees. riderBounds is the
    ' carried snippet's current rectangle. Returns True when it was picked up
    ' by this update.
    Public Function Update(ByVal carrierCenter As PointF, ByVal carrierSize As SizeF, ByVal carrierAngle As Single,
                           ByVal riderBounds As RectangleF) As Boolean
        If carryingValue OrElse returningValue Then Return False
        Dim touching As Boolean = Touches(carrierCenter, carrierSize, carrierAngle, riderBounds)
        If mustSeparate Then
            If Not touching Then mustSeparate = False
            Return False
        End If
        If Not touching Then Return False
        Dim riderCenter As New PointF(riderBounds.X + riderBounds.Width / 2.0F, riderBounds.Y + riderBounds.Height / 2.0F)
        offsetLocal = Rotate(New PointF(riderCenter.X - carrierCenter.X, riderCenter.Y - carrierCenter.Y), -carrierAngle)
        carryingValue = True
        Return True
    End Function

    ' Where the carried snippet's center goes for the carrier's current pose.
    Public Function RiderCenter(ByVal carrierCenter As PointF, ByVal carrierAngle As Single) As PointF
        Dim offset As PointF = Rotate(offsetLocal, carrierAngle)
        Return New PointF(carrierCenter.X + offset.X, carrierCenter.Y + offset.Y)
    End Function

    Public Sub Drop()
        If Not carryingValue Then Return
        carryingValue = False
        mustSeparate = True
    End Sub

    Private Shared Function Rotate(ByVal p As PointF, ByVal degrees As Single) As PointF
        Dim r As Double = degrees * Math.PI / 180.0
        Dim c As Double = Math.Cos(r), s As Double = Math.Sin(r)
        Return New PointF(CSng(p.X * c - p.Y * s), CSng(p.X * s + p.Y * c))
    End Function

    ' True when the carrier rectangle (turned by carrierAngle about its center)
    ' and the rider rectangle overlap or touch (separating-axis test).
    Public Shared Function Touches(ByVal carrierCenter As PointF, ByVal carrierSize As SizeF, ByVal carrierAngle As Single,
                                   ByVal riderBounds As RectangleF) As Boolean
        If carrierSize.Width <= 0 OrElse carrierSize.Height <= 0 OrElse riderBounds.Width <= 0 OrElse riderBounds.Height <= 0 Then Return False
        Dim hw As Single = carrierSize.Width / 2.0F, hh As Single = carrierSize.Height / 2.0F
        Dim carrier As PointF() = {
            Offset(carrierCenter, Rotate(New PointF(-hw, -hh), carrierAngle)), Offset(carrierCenter, Rotate(New PointF(hw, -hh), carrierAngle)),
            Offset(carrierCenter, Rotate(New PointF(hw, hh), carrierAngle)), Offset(carrierCenter, Rotate(New PointF(-hw, hh), carrierAngle))}
        Dim rider As PointF() = {
            New PointF(riderBounds.Left, riderBounds.Top), New PointF(riderBounds.Right, riderBounds.Top),
            New PointF(riderBounds.Right, riderBounds.Bottom), New PointF(riderBounds.Left, riderBounds.Bottom)}
        Dim axes As PointF() = {New PointF(1, 0), New PointF(0, 1), Rotate(New PointF(1, 0), carrierAngle), Rotate(New PointF(0, 1), carrierAngle)}
        For Each axis As PointF In axes
            Dim minA As Double = Double.MaxValue, maxA As Double = Double.MinValue
            Dim minB As Double = Double.MaxValue, maxB As Double = Double.MinValue
            For Each p As PointF In carrier
                Dim v As Double = p.X * axis.X + p.Y * axis.Y
                minA = Math.Min(minA, v) : maxA = Math.Max(maxA, v)
            Next
            For Each p As PointF In rider
                Dim v As Double = p.X * axis.X + p.Y * axis.Y
                minB = Math.Min(minB, v) : maxB = Math.Max(maxB, v)
            Next
            If maxA < minB OrElse maxB < minA Then Return False
        Next
        Return True
    End Function

    Private Shared Function Offset(ByVal a As PointF, ByVal b As PointF) As PointF
        Return New PointF(a.X + b.X, a.Y + b.Y)
    End Function
End Class

' The path as a dense polyline with arc-length lookup.
Public Class PathAnimGeometry
    Private ReadOnly samples As New List(Of PointF)()
    Private ReadOnly cumulative As New List(Of Double)()
    Public ReadOnly Closed As Boolean
    Public ReadOnly Length As Double

    Private Const SamplesPerSegment As Integer = 24

    Public Sub New(ByVal points As IList(Of PointF), ByVal closed As Boolean, ByVal smooth As Boolean)
        Me.Closed = closed AndAlso points.Count >= 3
        If points.Count = 0 Then
            samples.Add(PointF.Empty)
        ElseIf points.Count = 1 Then
            samples.Add(points(0))
        ElseIf Not smooth Then
            samples.AddRange(points)
            If Me.Closed Then samples.Add(points(0))
        Else
            Dim n As Integer = points.Count
            Dim segmentCount As Integer = If(Me.Closed, n, n - 1)
            For segment As Integer = 0 To segmentCount - 1
                Dim p0 As PointF = PointAt(points, segment - 1, Me.Closed)
                Dim p1 As PointF = PointAt(points, segment, Me.Closed)
                Dim p2 As PointF = PointAt(points, segment + 1, Me.Closed)
                Dim p3 As PointF = PointAt(points, segment + 2, Me.Closed)
                For stepIndex As Integer = 0 To SamplesPerSegment - 1
                    samples.Add(CatmullRom(p0, p1, p2, p3, CSng(stepIndex / SamplesPerSegment)))
                Next
            Next
            samples.Add(If(Me.Closed, points(0), points(n - 1)))
        End If
        Dim total As Double = 0
        cumulative.Add(0)
        For index As Integer = 1 To samples.Count - 1
            Dim dx As Double = samples(index).X - samples(index - 1).X
            Dim dy As Double = samples(index).Y - samples(index - 1).Y
            total += Math.Sqrt(dx * dx + dy * dy)
            cumulative.Add(total)
        Next
        Length = total
    End Sub

    Private Shared Function PointAt(ByVal points As IList(Of PointF), ByVal index As Integer, ByVal closed As Boolean) As PointF
        Dim n As Integer = points.Count
        If closed Then Return points(((index Mod n) + n) Mod n)
        Return points(Math.Max(0, Math.Min(n - 1, index)))
    End Function

    Private Shared Function CatmullRom(ByVal p0 As PointF, ByVal p1 As PointF, ByVal p2 As PointF, ByVal p3 As PointF, ByVal t As Single) As PointF
        Dim t2 As Single = t * t
        Dim t3 As Single = t2 * t
        Return New PointF(
            0.5F * (2.0F * p1.X + (-p0.X + p2.X) * t + (2.0F * p0.X - 5.0F * p1.X + 4.0F * p2.X - p3.X) * t2 + (-p0.X + 3.0F * p1.X - 3.0F * p2.X + p3.X) * t3),
            0.5F * (2.0F * p1.Y + (-p0.Y + p2.Y) * t + (2.0F * p0.Y - 5.0F * p1.Y + 4.0F * p2.Y - p3.Y) * t2 + (-p0.Y + 3.0F * p1.Y - 3.0F * p2.Y + p3.Y) * t3))
    End Function

    Public ReadOnly Property SamplePoints As IList(Of PointF)
        Get
            Return samples
        End Get
    End Property

    ' Wraps on a closed path, clamps on an open one.
    Public Function NormalizeDistance(ByVal distance As Double) As Double
        If Length <= 0 Then Return 0
        If Closed Then
            Dim d As Double = distance Mod Length
            If d < 0 Then d += Length
            Return d
        End If
        Return Math.Max(0, Math.Min(Length, distance))
    End Function

    Private Function SegmentAt(ByVal distance As Double) As Integer
        Dim lo As Integer = 0
        Dim hi As Integer = cumulative.Count - 1
        While hi - lo > 1
            Dim mid As Integer = (lo + hi) \ 2
            If cumulative(mid) <= distance Then lo = mid Else hi = mid
        End While
        Return lo
    End Function

    Public Function PositionAt(ByVal distance As Double) As PointF
        If samples.Count = 1 OrElse Length <= 0 Then Return samples(0)
        Dim d As Double = NormalizeDistance(distance)
        Dim i As Integer = SegmentAt(d)
        If i >= samples.Count - 1 Then Return samples(samples.Count - 1)
        Dim segLength As Double = cumulative(i + 1) - cumulative(i)
        Dim t As Single = If(segLength <= 0, 0.0F, CSng((d - cumulative(i)) / segLength))
        Return New PointF(samples(i).X + (samples(i + 1).X - samples(i).X) * t,
                          samples(i).Y + (samples(i + 1).Y - samples(i).Y) * t)
    End Function

    ' Direction of travel (forward along the path) in degrees, measured after
    ' an optional X/Y scale so a stretched backglass still looks right.
    Public Function AngleAt(ByVal distance As Double, Optional ByVal scaleX As Single = 1.0F, Optional ByVal scaleY As Single = 1.0F) As Single
        If samples.Count < 2 OrElse Length <= 0 Then Return 0.0F
        Dim d As Double = NormalizeDistance(distance)
        Dim span As Double = Math.Max(1.0, Length * 0.004)
        Dim a As PointF, b As PointF
        If Closed Then
            a = PositionAt(d - span) : b = PositionAt(d + span)
        Else
            a = PositionAt(Math.Max(0, d - span)) : b = PositionAt(Math.Min(Length, d + span))
        End If
        Dim dx As Double = (b.X - a.X) * scaleX
        Dim dy As Double = (b.Y - a.Y) * scaleY
        If Math.Abs(dx) < 0.000001 AndAlso Math.Abs(dy) < 0.000001 Then Return 0.0F
        Return CSng(Math.Atan2(dy, dx) * 180.0 / Math.PI)
    End Function

    ' Projects a point onto the path; returns the distance along it.
    Public Function NearestDistance(ByVal point As PointF, ByRef squaredGap As Double) As Double
        squaredGap = Double.MaxValue
        Dim best As Double = 0
        For i As Integer = 0 To samples.Count - 2
            Dim ax As Double = samples(i).X, ay As Double = samples(i).Y
            Dim bx As Double = samples(i + 1).X, by As Double = samples(i + 1).Y
            Dim vx As Double = bx - ax, vy As Double = by - ay
            Dim len2 As Double = vx * vx + vy * vy
            Dim t As Double = If(len2 <= 0, 0, Math.Max(0, Math.Min(1, ((point.X - ax) * vx + (point.Y - ay) * vy) / len2)))
            Dim px As Double = ax + vx * t, py As Double = ay + vy * t
            Dim gap As Double = (point.X - px) * (point.X - px) + (point.Y - py) * (point.Y - py)
            If gap < squaredGap Then
                squaredGap = gap
                best = cumulative(i) + (cumulative(i + 1) - cumulative(i)) * t
            End If
        Next
        Return best
    End Function
End Class

' Runtime motion state. Time-driven; the caller advances it with elapsed
' milliseconds and reads Position/Angle. Distances are kept unwrapped so a
' closed loop can keep going forward lap after lap.
Public Class PathAnimMover
    Public ReadOnly Settings As PathAnimSettings
    Public ReadOnly Geometry As PathAnimGeometry
    Private current As Double
    Private target As Double
    Private targetStop As Integer
    Private running As Boolean
    ' Unwrapped distance of the snippet's FRONT already checked for track switches.
    Private switchCheckedD As Double
    ' Unwrapped center distance already checked for reaching the path end.
    Private endCheckedD As Double
    Private snippetSizeValue As SizeF = SizeF.Empty
    ' Pop up / drop bounce after landing: time into the bounce (-1 = none),
    ' where the move started and its size.
    Private bounceMs As Double = -1
    Private moveFrom As Double
    Private bounceAmount As Double
    Private Const BounceTime As Double = 240.0

    Public Sub New(ByVal settings As PathAnimSettings, ByVal geometry As PathAnimGeometry)
        Me.Settings = settings
        Me.Geometry = geometry
        Reset()
    End Sub

    ' Size of the moving image in backglass-image pixels. Track switches are
    ' tripped by the image's front, half its length ahead of its center in the
    ' direction of travel (empty size = the center trips them).
    Public Property SnippetSize As SizeF
        Get
            Return snippetSizeValue
        End Get
        Set(ByVal value As SizeF)
            snippetSizeValue = value
            ' Where the snippet already is does not count as crossing a switch.
            switchCheckedD = FrontDistance
        End Set
    End Property

    ' Distance from the image's center to its front along the path: half the
    ' image's extent along its direction of travel. A turning image travels
    ' the way its artwork faces (RotateOffset); a non-turning image travels
    ' along the path's direction at this point.
    Public ReadOnly Property FrontOffset As Double
        Get
            If snippetSizeValue.Width <= 0 OrElse snippetSizeValue.Height <= 0 Then Return 0
            Dim degrees As Double = If(Settings.Rotate, Settings.RotateOffset, Geometry.AngleAt(current))
            Dim radians As Double = degrees * Math.PI / 180.0
            Return snippetSizeValue.Width / 2.0 * Math.Abs(Math.Cos(radians)) + snippetSizeValue.Height / 2.0 * Math.Abs(Math.Sin(radians))
        End Get
    End Property

    ' Unwrapped distance of the image's front.
    Public ReadOnly Property FrontDistance As Double
        Get
            Return current + FrontOffset
        End Get
    End Property

    Public ReadOnly Property StopCount As Integer
        Get
            Return Settings.Stops.Count
        End Get
    End Property

    Public Function StopDistance(ByVal index As Integer) As Double
        If StopCount = 0 Then Return 0
        index = Math.Max(0, Math.Min(StopCount - 1, index))
        Return Settings.Stops(index) * Geometry.Length
    End Function

    Public Sub Reset()
        ' The snippet starts where the path starts (stop 1).
        running = False
        targetStop = 0
        current = StopDistance(targetStop)
        target = current
        switchCheckedD = FrontDistance
        endCheckedD = current
        bounceMs = -1
        moveFrom = current
    End Sub

    ' True once when the snippet has just reached the end of its path since
    ' the last call: an open path's last point, or for a closed loop coming
    ' around to the start point (moving forward). Starting at the end does not
    ' count.
    Public Function TakeEndReached() As Boolean
        Dim fromD As Double = endCheckedD
        Dim toD As Double = current
        endCheckedD = current
        Dim length As Double = Geometry.Length
        If length <= 0 OrElse toD <= fromD + 0.000001 Then Return False
        If Geometry.Closed Then
            Return Math.Floor((toD + 0.000001) / length) > Math.Floor((fromD + 0.000001) / length)
        End If
        Return toD >= length - 0.01 AndAlso fromD < length - 0.01
    End Function

    ' Track switches the image's front crossed since the last call, in
    ' crossing order. A switch is crossed when its spot lies after the last
    ' checked front distance and at or before the current one (or the reverse
    ' when moving backward); a closed loop repeats every spot once per lap.
    ' Starting with the front on a spot does not press it.
    Public Function TakeSwitchHits() As List(Of Integer)
        Dim hits As New List(Of Integer)()
        Dim fromD As Double = switchCheckedD
        Dim toD As Double = FrontDistance
        switchCheckedD = toD
        If Settings.Switches.Count = 0 OrElse Math.Abs(toD - fromD) < 0.000001 Then Return hits
        Dim length As Double = Geometry.Length
        If length <= 0 Then Return hits
        Dim forward As Boolean = toD > fromD
        Dim low As Double = Math.Min(fromD, toD)
        Dim high As Double = Math.Max(fromD, toD)
        Dim found As New List(Of KeyValuePair(Of Double, Integer))()
        For Each sw As PathAnimSwitch In Settings.Switches
            Dim spot As Double = sw.Position * length
            If Geometry.Closed Then
                Dim k As Long = CLng(Math.Floor((low - spot) / length))
                Dim guard As Integer = 0
                Do
                    Dim d As Double = spot + k * length
                    If d > high Then Exit Do
                    Dim inside As Boolean = If(forward, d > low AndAlso d <= high, d >= low AndAlso d < high)
                    If inside Then found.Add(New KeyValuePair(Of Double, Integer)(d, sw.SwitchID))
                    k += 1
                    guard += 1
                Loop While guard < 1000
            Else
                Dim inside As Boolean = If(forward, spot > low AndAlso spot <= high, spot >= low AndAlso spot < high)
                If inside Then found.Add(New KeyValuePair(Of Double, Integer)(spot, sw.SwitchID))
            End If
        Next
        found.Sort(Function(a, b) If(forward, a.Key.CompareTo(b.Key), b.Key.CompareTo(a.Key)))
        For Each item In found
            hits.Add(item.Value)
        Next
        Return hits
    End Function

    Public ReadOnly Property IsMoving As Boolean
        Get
            Return running OrElse Math.Abs(target - current) > 0.0001 OrElse bounceMs >= 0
        End Get
    End Property

    ' 1-based stop the object is heading to (or resting at).
    Public ReadOnly Property CurrentStop As Integer
        Get
            Return targetStop + 1
        End Get
    End Property

    ' Unwrapped target for a stop. On a closed loop the snippet always goes
    ' forward around the loop; on an open path it goes straight to the stop.
    Private Function TargetFor(ByVal index As Integer) As Double
        Dim stopD As Double = StopDistance(index)
        If Not Geometry.Closed Then Return stopD
        Dim length As Double = Geometry.Length
        Dim baseD As Double = Geometry.NormalizeDistance(target)
        Dim forward As Double = stopD - baseD
        If forward < 0 Then forward += length
        If forward < 0.0001 Then Return target
        Return target + forward
    End Function

    Public Sub GoToStop(ByVal stopNumber As Integer)
        If StopCount = 0 Then Return
        Dim index As Integer = Math.Max(0, Math.Min(StopCount - 1, stopNumber - 1))
        running = False
        target = TargetFor(index)
        targetStop = index
        BeginMove()
    End Sub

    ' Next stop; a closed loop goes from the last stop back to stop 1.
    Public Sub StepBy(ByVal delta As Integer)
        If StopCount = 0 OrElse delta = 0 Then Return
        running = False
        Dim index As Integer = targetStop + delta
        If Geometry.Closed Then
            index = ((index Mod StopCount) + StopCount) Mod StopCount
        Else
            index = Math.Max(0, Math.Min(StopCount - 1, index))
        End If
        If index = targetStop Then Return
        target = TargetFor(index)
        targetStop = index
        BeginMove()
    End Sub

    ' Motor on: moves forward along the path until Halt.
    Public Sub Run()
        running = True
        bounceMs = -1
    End Sub

    Public Sub Halt()
        If Not running Then Return
        running = False
        target = current
        ' Remember the nearest stop so Step continues from here.
        Dim here As Double = Geometry.NormalizeDistance(current)
        Dim best As Integer = 0
        Dim bestGap As Double = Double.MaxValue
        For index As Integer = 0 To StopCount - 1
            Dim gap As Double = Math.Abs(StopDistance(index) - here)
            If gap < bestGap Then bestGap = gap : best = index
        Next
        targetStop = best
    End Sub

    Public Sub Home()
        running = False
        Dim index As Integer = 0
        If Geometry.Closed Then
            ' Shortest way back along the loop.
            Dim length As Double = Geometry.Length
            Dim baseD As Double = Geometry.NormalizeDistance(target)
            Dim forward As Double = StopDistance(index) - baseD
            If forward < 0 Then forward += length
            target = If(forward <= length / 2, target + forward, target + forward - length)
        Else
            target = StopDistance(index)
        End If
        targetStop = index
        BeginMove()
    End Sub

    ' Speed of the move to the current target: Pop up / drop goes back to the
    ' start at its own speed when one is set; everything else uses Speed.
    Private ReadOnly Property MoveSpeed As Single
        Get
            If Settings.Mode = PathAnimDriveMode.PopDrop AndAlso targetStop = 0 AndAlso Settings.ReturnSpeed >= 0 Then Return Settings.ReturnSpeed
            Return Settings.Speed
        End Get
    End Property

    Private Sub BeginMove()
        bounceMs = -1
        moveFrom = current
        If MoveSpeed <= 0 AndAlso Math.Abs(target - current) > 0.0001 Then
            current = target
            Landed()
        End If
    End Sub

    ' Pop up / drop: start the small bounce where the image lands.
    Private Sub Landed()
        bounceMs = -1
        If Settings.Mode <> PathAnimDriveMode.PopDrop OrElse Settings.Bounce <= 0 Then Return
        Dim travel As Double = current - moveFrom
        If Math.Abs(travel) < 0.5 Then Return
        ' Rebounds back the way it came, at most 6 % of the move.
        bounceAmount = -travel * 0.06 * Math.Max(0, Math.Min(100, Settings.Bounce)) / 100.0
        bounceMs = 0
    End Sub

    ' Offset along the path during the bounce: a few quick hops that die out.
    Private ReadOnly Property BounceOffset As Double
        Get
            If bounceMs < 0 Then Return 0
            Return bounceAmount * Math.Exp(-bounceMs / 60.0) * Math.Abs(Math.Sin(Math.PI * bounceMs / 80.0))
        End Get
    End Property

    ' Returns True when the position changed.
    Public Function Advance(ByVal elapsedMs As Double) As Boolean
        If elapsedMs <= 0 Then Return False
        Dim stepD As Double = Settings.Speed * elapsedMs / 1000.0
        If running Then
            If Settings.Speed <= 0 Then stepD = Geometry.Length / 2
            Dim nextD As Double = current + stepD
            If Not Geometry.Closed Then
                If nextD <= 0 OrElse nextD >= Geometry.Length Then
                    nextD = Math.Max(0, Math.Min(Geometry.Length, nextD))
                    current = nextD
                    Halt()
                    Return True
                End If
            End If
            current = nextD
            target = current
            Return True
        End If
        If Math.Abs(target - current) <= 0.0001 Then
            If bounceMs < 0 Then Return False
            bounceMs += elapsedMs
            If bounceMs >= BounceTime Then bounceMs = -1
            Return True
        End If
        stepD = MoveSpeed * elapsedMs / 1000.0
        If MoveSpeed <= 0 OrElse Math.Abs(target - current) <= stepD Then
            current = target
            Landed()
        Else
            current += Math.Sign(target - current) * stepD
        End If
        Return True
    End Function

    Public ReadOnly Property Distance As Double
        Get
            Return Geometry.NormalizeDistance(current)
        End Get
    End Property

    Public ReadOnly Property Position As PointF
        Get
            Return Geometry.PositionAt(current + BounceOffset)
        End Get
    End Property

    Public Function Angle(Optional ByVal scaleX As Single = 1.0F, Optional ByVal scaleY As Single = 1.0F) As Single
        If Not Settings.Rotate Then Return 0.0F
        Return Geometry.AngleAt(current, scaleX, scaleY) - Settings.RotateOffset
    End Function
End Class
