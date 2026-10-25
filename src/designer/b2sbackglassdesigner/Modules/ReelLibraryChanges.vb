Imports System.IO
Imports System.Xml

Public Module ReelLibraryChanges
    Public ReadOnly Property DigitImportWorkPath As String
        Get
            Dim overridePath = Environment.GetEnvironmentVariable("B2SPRO_IMPORT_WORK")
            If Not String.IsNullOrEmpty(overridePath) Then Return Path.GetFullPath(overridePath)
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "B2SPro\DigitImport")
        End Get
    End Property
    Private ReadOnly hiddenSets As New Generic.HashSet(Of String)(StringComparer.Ordinal)
    Private ReadOnly setNames As New Generic.Dictionary(Of String, String)(StringComparer.Ordinal)
    Private ReadOnly proportionalSets As New Generic.HashSet(Of String)(StringComparer.Ordinal)
    Private loaded As Boolean
    Private ReadOnly Property HiddenFile As String
        Get
            Return Path.Combine(BackglassProjectsPath, "ImageSetLibrary.xml")
        End Get
    End Property

    Private Sub LoadHidden()
        If loaded Then Return
        If File.Exists(HiddenFile) Then
            Dim xml As New XmlDocument
            xml.Load(HiddenFile)
            For Each node As XmlElement In xml.SelectNodes("ImageSetLibrary/Deleted")
                hiddenSets.Add(node.GetAttribute("Name"))
            Next
            For Each node As XmlElement In xml.SelectNodes("ImageSetLibrary/Name")
                setNames(node.GetAttribute("Key")) = node.GetAttribute("Value")
                If node.GetAttribute("KeepDigitAspect") = "1" Then proportionalSets.Add(node.GetAttribute("Key"))
            Next
        End If
        loaded = True
    End Sub

    Public Function IsBuiltInSetDeleted(name As String) As Boolean
        LoadHidden()
        Return hiddenSets.Contains(name)
    End Function

    Public Sub BackupLibraryFile(path As String)
        If Not File.Exists(path) Then Return
        Dim folder = IO.Path.Combine(DigitImportWorkPath, "Library-Backups")
        Directory.CreateDirectory(folder)
        File.Copy(path, IO.Path.Combine(folder, DateTime.Now.ToString("yyyyMMdd-HHmmss-fffffff") & "-" & Guid.NewGuid().ToString("N").Substring(0, 8) & "-" & IO.Path.GetFileName(path)))
    End Sub

    Private Sub SaveHidden()
        Directory.CreateDirectory(BackglassProjectsPath)
        BackupLibraryFile(HiddenFile)
        Dim xml As New XmlDocument
        Dim root = xml.CreateElement("ImageSetLibrary")
        xml.AppendChild(root)
        For Each name In hiddenSets.OrderBy(Function(value) value)
            Dim node = xml.CreateElement("Deleted")
            node.SetAttribute("Name", name)
            root.AppendChild(node)
        Next
        For Each pair In setNames.OrderBy(Function(value) value.Key)
            Dim node = xml.CreateElement("Name")
            node.SetAttribute("Key", pair.Key)
            node.SetAttribute("Value", pair.Value)
            If proportionalSets.Contains(pair.Key) Then node.SetAttribute("KeepDigitAspect", "1")
            root.AppendChild(node)
        Next
        Dim temporary = HiddenFile & ".new"
        Try
            xml.Save(temporary)
            If File.Exists(HiddenFile) Then
                File.Replace(temporary, HiddenFile, Nothing)
            Else
                File.Move(temporary, HiddenFile)
            End If
        Finally
            If File.Exists(temporary) Then File.Delete(temporary)
        End Try
    End Sub

    Public Function ImageSetDisplayName(key As String, fallback As String) As String
        LoadHidden()
        If setNames.ContainsKey(key) Then Return setNames(key)
        Return fallback
    End Function

    Public Function IsProportionalImageSet(key As String) As Boolean
        LoadHidden()
        Return proportionalSets.Contains(key)
    End Function

    Public Sub MarkProportionalImageSet(key As String)
        LoadHidden()
        Dim added = proportionalSets.Add(key)
        Try
            SaveHidden()
        Catch
            If added Then proportionalSets.Remove(key)
            Throw
        End Try
    End Sub

    Public Function KeepDigitAspect(score As ReelAndLED.ScoreInfo) As Boolean
        Return score.KeepDigitAspect OrElse IsProportionalImageSet(score.ReelType)
    End Function

    Public Function ResizeScoreReel(image As Image, size As Size, score As ReelAndLED.ScoreInfo) As Image
        If KeepDigitAspect(score) Then
            If score.ReelDigitWidth > 0 Then Return ReelImageSizing.StretchToWidth(image, size, score.ReelDigitWidth)
            Return ReelImageSizing.Fit(image, size)
        End If
        Return image.Resized(size)
    End Function

    Public Sub NameImageSet(key As String, name As String)
        ValidateImageSetName(name)
        LoadHidden()
        Dim previous As String = Nothing
        Dim existed = setNames.TryGetValue(key, previous)
        setNames(key) = name.Trim()
        Try
            SaveHidden()
        Catch
            If existed Then
                setNames(key) = previous
            Else
                setNames.Remove(key)
            End If
            Throw
        End Try
    End Sub

    Public Sub ValidateImageSetName(name As String)
        If String.IsNullOrWhiteSpace(name) Then Throw New ArgumentException("An image set needs a name.")
        Dim value = name.Trim()
        If value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 OrElse value.EndsWith(".") Then Throw New ArgumentException("Use a name without Windows filename characters or a trailing period.")
        Dim stem = value.Split("."c)(0).ToUpperInvariant()
        If {"CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"}.Contains(stem) Then Throw New ArgumentException("That name is reserved by Windows. Choose another name.")
    End Sub

    Public Sub ForgetImageSetName(key As String)
        LoadHidden()
        If Not setNames.ContainsKey(key) Then Return
        Dim previous = setNames(key)
        Dim proportional = proportionalSets.Remove(key)
        setNames.Remove(key)
        Try
            SaveHidden()
        Catch
            setNames(key) = previous
            If proportional Then proportionalSets.Add(key)
            Throw
        End Try
    End Sub

    Public Sub DeleteBuiltInSet(name As String)
        LoadHidden()
        Dim changed = hiddenSets.Add(name)
        Try
            SaveHidden()
        Catch
            If changed Then hiddenSets.Remove(name)
            Throw
        End Try
    End Sub

    Public Sub RestoreBuiltInSets(names As String())
        LoadHidden()
        Dim previous = hiddenSets.ToArray()
        For Each name In names
            hiddenSets.Remove(name)
        Next
        Try
            SaveHidden()
        Catch
            hiddenSets.Clear()
            hiddenSets.UnionWith(previous)
            Throw
        End Try
    End Sub
End Module
