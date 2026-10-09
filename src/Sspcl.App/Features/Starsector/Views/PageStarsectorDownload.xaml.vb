Imports Sspcl.Core.Store.Forum
Imports Sspcl.Core.Mods
Imports System.Collections.ObjectModel
Imports System.ComponentModel

Public Class PageStarsectorDownload
    Friend Shared Function GetForumCatalogAsync() As Task(Of ForumCatalogResult)
        Return CatalogRepository.RefreshAsync()
    End Function

    Friend Shared Function DownloadUpdateAsync(modInfo As ForumMod, release As ForumModRelease, progress As IProgress(Of ModDownloadProgress), token As CancellationToken) As Task(Of DownloadedModArchive)
        Return DownloadQueue.DownloadAsync(modInfo, release, progress, cancellationToken:=token)
    End Function

    Friend Shared Function WaitForInstallAsync(token As CancellationToken) As Task
        Return InstallGate.WaitAsync(token)
    End Function

    Friend Shared Sub ReleaseInstall()
        InstallGate.Release()
    End Sub
    Private Shared ReadOnly IndexHttp As HttpClient = CreateHttp(TimeSpan.FromSeconds(45))
    Private Shared ReadOnly DownloadHttp As HttpClient = CreateHttp(TimeSpan.FromSeconds(45))
    Private Shared ReadOnly _api As New ForumApiClient(IndexHttp, New Uri("https://api.fossic.org/"))
    Private Shared ReadOnly CatalogRepository As New ForumCatalogRepository(_api, IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SSPCL", "Cache", "Forum"))
    Private _mods As New List(Of ForumMod)()
    Private _categories As New Dictionary(Of String, String)()
    Private _languages As New Dictionary(Of String, String)()
    Private Shared ReadOnly DownloadQueue As New ForumDownloadQueue(New ForumModDownloader(DownloadHttp, _api), 3)
    Private Shared ReadOnly InstallGate As New SemaphoreSlim(1, 1)
    Private ReadOnly _downloads As New ObservableCollection(Of DownloadTaskItem)()
    Private _loadOperation As CancellationTokenSource
    Private _refreshing As Boolean
    Private _applyingCatalog As Boolean

    Private Shared Function CreateHttp(timeout As TimeSpan) As HttpClient
        Dim client As New HttpClient With {.Timeout = timeout}
        Return client
    End Function

    Private Async Sub Init() Handles Me.Loaded
        If _refreshing Then Return
        TaskList.ItemsSource = _downloads
        Await LoadIndexAsync()
    End Sub

    Private Sub LeavePage() Handles Me.Unloaded
        If _loadOperation IsNot Nothing Then _loadOperation.Cancel()
    End Sub

    Private Async Sub Refresh_Click(sender As Object, e As RoutedEventArgs)
        If Not _refreshing Then Await LoadIndexAsync(True)
    End Sub

    Private Async Function LoadIndexAsync(Optional force As Boolean = False) As Task
        Using operation As New CancellationTokenSource()
            _loadOperation = operation
            _refreshing = True
            BtnRefresh.IsEnabled = False
            UpdateDownloadButton()
            Try
                Dim cached = Await CatalogRepository.GetCachedAsync(operation.Token)
                If cached IsNot Nothing Then ApplyCatalog(cached)
                LabCache.Text = If(cached Is Nothing, "首次打开，正在获取论坛目录…", "已显示本地目录，正在检查更新…")
                Dim result = Await CatalogRepository.RefreshAsync(force, operation.Token)
                ApplyCatalog(result.Catalog)
                LabCache.Text = "目录更新于 " & result.Catalog.Mods.FetchedUtc.ToLocalTime().ToString("MM-dd HH:mm") & If(result.IsStale, " · 使用旧缓存", " · 缓存有效") & If(result.Warning Is Nothing, "", vbCrLf & result.Warning)
                If _downloads.Count = 0 Then LabStatus.Text = "请选择 MOD 和对应游戏版本的发布包。"
            Catch ex As OperationCanceledException
                LabCache.Text = "目录更新已取消，已有目录仍可浏览。"
            Catch ex As Exception
                LabCache.Text = "目录更新失败：" & ex.Message
            Finally
                _loadOperation = Nothing
                _refreshing = False
                BtnRefresh.IsEnabled = True
                UpdateDownloadButton()
            End Try
        End Using
    End Function

    Private Sub ApplyCatalog(catalog As ForumCatalog)
        _applyingCatalog = True
        Try
            _mods = If(catalog.Mods.Data, New List(Of ForumMod)())
            _categories = If(catalog.Categories.Data, New Dictionary(Of String, String)())
            _languages = If(catalog.Languages.Data, New Dictionary(Of String, String)())
            Dim versions = If(catalog.Versions.Data, _mods.SelectMany(Function(m) m.GameVersions).Distinct().ToList())
            SetOptions(BoxVersion, "所有游戏版本", versions.Select(Function(v) New KeyValuePair(Of String, String)(v, v)))
            SetOptions(BoxCategory, "所有分类", If(_categories.Count > 0, _categories.AsEnumerable(), _mods.Select(Function(m) m.Category).Distinct().Select(Function(k) New KeyValuePair(Of String, String)(k, k))))
            SetOptions(BoxLanguage, "所有语言", If(_languages.Count > 0, _languages.AsEnumerable(), _mods.Select(Function(m) m.Language).Distinct().Select(Function(k) New KeyValuePair(Of String, String)(k, k))))
        Finally
            _applyingCatalog = False
        End Try
        RefreshList()
    End Sub

    Private Sub SetOptions(box As ComboBox, allLabel As String, options As IEnumerable(Of KeyValuePair(Of String, String)))
        Dim selected = SelectedKey(box)
        box.Items.Clear()
        box.Items.Add(New ComboBoxItem With {.Content = allLabel, .Tag = ""})
        For Each pair In options
            box.Items.Add(New ComboBoxItem With {.Content = pair.Value, .Tag = pair.Key})
        Next
        box.SelectedIndex = 0
        For Each item As ComboBoxItem In box.Items
            If CStr(item.Tag) = selected Then box.SelectedItem = item : Exit For
        Next
    End Sub

    Private Function SelectedKey(box As ComboBox) As String
        Dim item = TryCast(box.SelectedItem, ComboBoxItem)
        Return If(item Is Nothing, "", CStr(item.Tag))
    End Function

    Private Sub Filter_Changed(sender As Object, e As RoutedEventArgs)
        If ModList IsNot Nothing AndAlso Not _applyingCatalog Then RefreshList()
    End Sub

    Private Sub RefreshList()
        Dim search = BoxSearch.Text.Trim()
        Dim version = SelectedKey(BoxVersion)
        Dim category = SelectedKey(BoxCategory)
        Dim language = SelectedKey(BoxLanguage)
        Dim selected = TryCast(ModList.SelectedItem, ForumMod)
        Dim selectedRelease = TryCast(BoxRelease.SelectedItem, ReleaseOption)
        Dim visible = _mods.Where(Function(m) (version = "" OrElse m.GameVersions.Contains(version)) AndAlso
            (category = "" OrElse m.Category = category) AndAlso (language = "" OrElse m.Language = language) AndAlso
            (CheckDirect.IsChecked <> True OrElse (m.Releases IsNot Nothing AndAlso m.Releases.Any(Function(r) _api.GetDownloadUri(m, r) IsNot Nothing))) AndAlso
            (search = "" OrElse (m.Id & " " & m.ChineseName & " " & m.EnglishName & " " & String.Join(" ", m.Authors) & " " & WebUtility.HtmlDecode(m.ShortDescription)).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList()
        If BoxSort.SelectedIndex = 1 Then
            visible = visible.OrderBy(Function(m) m.ChineseName, StringComparer.OrdinalIgnoreCase).ToList()
        Else
            visible = visible.OrderByDescending(Function(m) m.UpdateDate).ToList()
        End If
        ModList.ItemsSource = visible
        If selected IsNot Nothing Then ModList.SelectedItem = visible.FirstOrDefault(Function(m) m.Id = selected.Id AndAlso m.InfoType = selected.InfoType)
        If selectedRelease IsNot Nothing AndAlso ModList.SelectedItem IsNot Nothing Then
            BoxRelease.SelectedItem = BoxRelease.Items.Cast(Of ReleaseOption)().FirstOrDefault(Function(r) r.Release.AttachmentId = selectedRelease.Release.AttachmentId)
        End If
        LabCount.Text = "显示 " & visible.Count & " / " & _mods.Count & " 个 MOD"
        LabEmpty.Visibility = If(visible.Count = 0, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Sub Mod_Selected(sender As Object, e As SelectionChangedEventArgs)
        If BoxRelease Is Nothing Then Return
        Dim modInfo = TryCast(ModList.SelectedItem, ForumMod)
        BoxRelease.ItemsSource = Nothing
        BtnDownload.IsEnabled = False
        BtnSaveArchive.IsEnabled = False
        BtnForum.IsEnabled = False
        LabName.Text = If(modInfo Is Nothing, "选择一个 MOD 查看详情", modInfo.ChineseName)
        LabInfo.Text = ""
        LabDescription.Text = ""
        LabDependencies.Text = ""
        LabNotes.Text = ""
        LabDownloadHint.Text = ""
        If modInfo Is Nothing Then Return
        LabInfo.Text = "作者：" & String.Join("、", modInfo.Authors) & " · " & LabelFor(_categories, modInfo.Category) & " · " & LabelFor(_languages, modInfo.Language) & " · " & SourceLabel(modInfo.InfoType)
        If modInfo.Translators.Count > 0 Then LabInfo.Text &= vbCrLf & "汉化：" & String.Join("、", modInfo.Translators)
        LabDescription.Text = WebUtility.HtmlDecode(modInfo.ShortDescription)
        LabDependencies.Text = "前置：" & If(modInfo.DependencyNames.Count = 0, "未标注", String.Join("、", modInfo.DependencyNames)) & vbCrLf &
            "冲突：" & If(modInfo.ConflictNames.Count = 0, "未标注", String.Join("、", modInfo.ConflictNames))
        LabNotes.Text = WebUtility.HtmlDecode(String.Join(vbCrLf, New String() {modInfo.AdminNotes.IndexComment, modInfo.AdminNotes.ThreadComment}.Where(Function(s) Not String.IsNullOrWhiteSpace(s))))
        Dim releases = If(modInfo.Releases, New List(Of ForumModRelease)())
        BoxRelease.ItemsSource = releases.OrderByDescending(Function(r) r.UploadedAt).Select(Function(r) New ReleaseOption With {.Release = r, .Display = r.GameVersion & " · " & r.ModVersion & " · " & If(r.FileName, If(r.DisplayName, "附件 " & r.AttachmentId)) & If(r.FileSize.HasValue, " · " & (r.FileSize.GetValueOrDefault() / 1048576.0).ToString("0.##") & " MB", "") & If(_api.GetDownloadUri(modInfo, r) Is Nothing, " · 仅发布帖下载", " · 可直接下载")}).ToList()
        BoxRelease.SelectedIndex = -1
        BtnForum.IsEnabled = ForumUri(modInfo) IsNot Nothing
        LabDownloadHint.Text = If(Not modInfo.AllowDirectDownload, "论坛未开放此 MOD 的启动器直下载。版本与附件信息可查看，请打开发布帖下载。", If(releases.Count = 0, "暂无发布包，请打开发布帖获取。", "请选择游戏版本对应的发布包。"))
    End Sub

    Private Sub Release_Selected(sender As Object, e As SelectionChangedEventArgs)
        UpdateDownloadButton()
    End Sub

    Private Sub UpdateDownloadButton()
        If BtnDownload Is Nothing Then Return
        Dim modInfo = TryCast(ModList.SelectedItem, ForumMod)
        Dim optionItem = TryCast(BoxRelease.SelectedItem, ReleaseOption)
        Dim duplicate = modInfo IsNot Nothing AndAlso optionItem IsNot Nothing AndAlso _downloads.Any(Function(job) job.Active AndAlso job.ModInfo IsNot Nothing AndAlso job.Release IsNot Nothing AndAlso job.ModInfo.Id = modInfo.Id AndAlso job.ModInfo.InfoType = modInfo.InfoType AndAlso job.Release.AttachmentId = optionItem.Release.AttachmentId)
        BtnDownload.IsEnabled = Not duplicate AndAlso modInfo IsNot Nothing AndAlso optionItem IsNot Nothing AndAlso _api.GetDownloadUri(modInfo, optionItem.Release) IsNot Nothing
        BtnSaveArchive.IsEnabled = BtnDownload.IsEnabled
        If optionItem IsNot Nothing AndAlso modInfo IsNot Nothing Then
            If Not modInfo.AllowDirectDownload Then
                LabDownloadHint.Text = "论坛未开放此 MOD 的启动器直下载。版本与附件信息可查看，请打开发布帖下载。"
            ElseIf _api.GetDownloadUri(modInfo, optionItem.Release) Is Nothing Then
                LabDownloadHint.Text = "论坛未提供此发布包的可用直下载地址，请打开发布帖下载。"
            ElseIf duplicate Then
                LabDownloadHint.Text = "此发布包正在处理，请在下载任务中查看进度或取消。"
            Else
                LabDownloadHint.Text = "请核对版本和文件名。下载到 MOD 池会更新池内同 ID 的旧版；补丁或附加文件请保存压缩包。"
            End If
        End If
    End Sub

    Private Async Sub ImportArchive_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog As New Microsoft.Win32.OpenFileDialog With {
            .Title = "导入已下载 MOD 压缩包", .Filter = "MOD 压缩包|*.zip;*.rar;*.7z", .Multiselect = True, .CheckFileExists = True
        }
        If dialog.ShowDialog() <> True Then Return
        Await ImportArchivesAsync(dialog.FileNames, ModMain.ModPoolDir())
    End Sub

    Private Async Function ImportArchivesAsync(paths As String(), target As String) As Task
        BtnImportArchive.IsEnabled = False
        Try
            For Each path In paths
                Dim job As New DownloadTaskItem With {.Title = "本地导入 · " & IO.Path.GetFileName(path), .Status = "等待安装到 MOD 池…"}
                _downloads.Add(job)
                TaskList.ItemsSource = _downloads
                RefreshTaskSummary()
                Dim entered As Boolean = False
                Try
                    Await InstallGate.WaitAsync(job.Cancellation.Token)
                    entered = True
                    job.Cancellation.Token.ThrowIfCancellationRequested()
                    job.CanCancel = False
                    job.Status = "正在导入到 MOD 池…"
                    job.Notify()
                    Dim result = Await Task.Run(Function() ModInstaller.InstallArchive(path, target))
                    If result.Success Then
                        job.Percent = 100
                        job.Status = "已导入：" & result.Name & If(result.WasUpdate, "（已更新）", "") & "。请到版本设置启用。"
                    ElseIf result.Error.Contains("mod_info.json") Then
                        job.Status = "此压缩包不是完整 MOD，无法自动导入。请按发布帖说明安装补丁或附加文件；原压缩包已保留。"
                    Else
                        job.Status = "导入失败：" & result.Error
                    End If
                Catch ex As OperationCanceledException When job.Cancellation.IsCancellationRequested
                    job.Status = "已取消导入，原压缩包已保留。"
                Catch ex As Exception
                    job.Status = "导入失败：" & ex.Message
                Finally
                    If entered Then InstallGate.Release()
                    job.Active = False
                    job.CanCancel = False
                    job.Indeterminate = False
                    job.Notify()
                    job.Cancellation.Dispose()
                    RefreshTaskSummary()
                End Try
            Next
        Finally
            BtnImportArchive.IsEnabled = True
        End Try
    End Function

    Private Async Sub Download_Click(sender As Object, e As RoutedEventArgs)
        Await DownloadSelectedAsync(True)
    End Sub

    Private Async Sub SaveArchive_Click(sender As Object, e As RoutedEventArgs)
        Await DownloadSelectedAsync(False)
    End Sub

    Private Async Function DownloadSelectedAsync(installToPool As Boolean) As Task
        Dim modInfo = TryCast(ModList.SelectedItem, ForumMod)
        Dim selected = TryCast(BoxRelease.SelectedItem, ReleaseOption)
        If modInfo Is Nothing OrElse selected Is Nothing OrElse Not BtnDownload.IsEnabled Then Return
        Dim savePath As String = Nothing
        If Not installToPool Then
            savePath = ChooseArchivePath(selected.Release)
            If savePath Is Nothing Then Return
        End If
        Dim job As New DownloadTaskItem With {.ModInfo = modInfo, .Release = selected.Release, .Title = modInfo.ChineseName & " · " & If(selected.Release.FileName, selected.Display)}
        _downloads.Add(job)
        TaskList.ItemsSource = _downloads
        RefreshTaskSummary()
        UpdateDownloadButton()
        Dim archive As DownloadedModArchive = Nothing
        Dim receiving As Boolean = True
        Try
            Dim progress As New Progress(Of ModDownloadProgress)(Sub(p)
                If Not receiving OrElse Not job.Active Then Return
                job.Percent = p.Percent.GetValueOrDefault()
                job.Indeterminate = Not p.Percent.HasValue
                job.Status = "下载中：" & (p.ReceivedBytes / 1048576.0).ToString("F1") & " MB" & If(p.Percent.HasValue, "（" & p.Percent.GetValueOrDefault().ToString("F0") & "%）", "")
                job.Notify()
            End Sub)
            '网络、取消与解压均不占用 UI 线程；进度回到当前页面 Dispatcher。
            archive = Await Task.Run(Function() DownloadQueue.DownloadAsync(modInfo, job.Release, progress,
                Sub() Dispatcher.BeginInvoke(New Action(Sub()
                    If job.Active AndAlso receiving Then job.Status = "正在连接服务器…" : job.Notify()
                End Sub)), job.Cancellation.Token))
            receiving = False
            job.Cancellation.Token.ThrowIfCancellationRequested()
            job.Indeterminate = False
            job.Percent = 100
            If Not installToPool Then
                job.Status = "正在保存文件…"
                job.CanCancel = False
                job.Notify()
                Dim destination = IO.Path.ChangeExtension(savePath, IO.Path.GetExtension(archive.Path))
                Await Task.Run(Sub() IO.File.Copy(archive.Path, destination, True))
                job.Status = "已保存：" & destination
            Else
                job.Status = "等待安装到 MOD 池…"
                job.Notify()
                Await InstallGate.WaitAsync(job.Cancellation.Token)
                Try
                    job.Cancellation.Token.ThrowIfCancellationRequested()
                    job.CanCancel = False
                    job.Status = "正在安装到 MOD 池…"
                    job.Notify()
                    Dim target = ModMain.ModPoolDir()
                    Dim result = Await Task.Run(Function() ModInstaller.InstallArchive(archive.Path, target))
                    If Not result.Success Then
                        If result.Error.Contains("mod_info.json") Then
                            job.Archive = archive
                            archive = Nothing
                            job.CanSave = True
                            job.Status = "此附件不是完整 MOD，请点击保存，再按发布帖说明使用。"
                        Else
                            Throw New InvalidOperationException(result.Error)
                        End If
                    Else
                        job.Status = "已安装：" & result.Name & If(result.WasUpdate, "（已更新）", "") & "。请到版本设置启用。"
                    End If
                Finally
                    InstallGate.Release()
                End Try
            End If
        Catch ex As OperationCanceledException When job.Cancellation.IsCancellationRequested
            job.Status = "已取消，可以重新加入下载。"
        Catch ex As Exception
            job.Status = "下载或安装失败：" & If(TypeOf ex Is OperationCanceledException, "服务器连接超时，请重试。", ex.Message)
        Finally
            receiving = False
            If archive IsNot Nothing Then archive.Dispose()
            job.Active = False
            job.CanCancel = False
            job.Indeterminate = False
            job.Notify()
            job.Cancellation.Dispose()
            RefreshTaskSummary()
            UpdateDownloadButton()
        End Try
    End Function

    Private Function ChooseArchivePath(release As ForumModRelease, Optional extension As String = "") As String
        Dim fileName = IO.Path.GetFileName(If(release.FileName, "mod.zip"))
        For Each invalid In IO.Path.GetInvalidFileNameChars()
            fileName = fileName.Replace(invalid, "_"c)
        Next
        If extension <> "" Then fileName = IO.Path.ChangeExtension(fileName, extension)
        Dim dialog As New Microsoft.Win32.SaveFileDialog With {
            .FileName = fileName, .Filter = "MOD 压缩包|*.zip;*.rar;*.7z|所有文件|*.*", .OverwritePrompt = True, .Title = "保存 MOD 压缩包"
        }
        Return If(dialog.ShowDialog() = True, dialog.FileName, Nothing)
    End Function

    Private Sub RefreshTaskSummary()
        LabTasks.Text = "下载与导入任务：" & _downloads.Where(Function(job) job.Active).Count() & " 项处理中 · 最多同时下载 3 项；切换页面不会中断任务。"
    End Sub

    Private Sub CancelTask_Click(sender As Object, e As RoutedEventArgs)
        Dim job = TryCast(DirectCast(sender, Button).DataContext, DownloadTaskItem)
        If job Is Nothing OrElse Not job.Active OrElse Not job.CanCancel Then Return
        job.CanCancel = False
        job.Status = "正在取消…"
        job.Notify()
        'Abort 网络连接可能同步执行，不让取消按钮阻塞 UI。
        Task.Run(Sub()
            Try
                job.Cancellation.Cancel()
            Catch ex As ObjectDisposedException
            End Try
        End Sub)
    End Sub

    Private Async Sub SaveTask_Click(sender As Object, e As RoutedEventArgs)
        Dim job = TryCast(DirectCast(sender, Button).DataContext, DownloadTaskItem)
        If job Is Nothing OrElse job.Archive Is Nothing Then Return
        Dim archive = job.Archive
        Dim destination = ChooseArchivePath(job.Release, IO.Path.GetExtension(archive.Path))
        If destination Is Nothing Then Return
        job.CanSave = False
        job.Active = True
        job.Notify()
        Try
            Await Task.Run(Sub() IO.File.Copy(archive.Path, destination, True))
            job.Status = "已保存：" & destination
            archive.Dispose()
            job.Archive = Nothing
        Catch ex As Exception
            job.Status = "保存失败：" & ex.Message
            job.CanSave = True
        Finally
            job.Active = False
            job.Notify()
            RefreshTaskSummary()
        End Try
    End Sub

    Private Sub ClearTasks_Click(sender As Object, e As RoutedEventArgs)
        For Each job In _downloads.Where(Function(item) Not item.Active).ToList()
            If job.Archive IsNot Nothing Then job.Archive.Dispose()
            _downloads.Remove(job)
        Next
        RefreshTaskSummary()
    End Sub

    Public Class DownloadTaskItem
        Implements INotifyPropertyChanged
        Public Property ModInfo As ForumMod
        Public Property Release As ForumModRelease
        Public Property Title As String
        Public Property Status As String = "等待下载…"
        Public Property Percent As Double
        Public Property Indeterminate As Boolean = True
        Public Property Active As Boolean = True
        Public Property CanCancel As Boolean = True
        Public Property CanSave As Boolean
        Public Property Archive As DownloadedModArchive
        Public ReadOnly Cancellation As New CancellationTokenSource()
        Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged
        Public Sub Notify()
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(""))
        End Sub
    End Class

    Private Sub Forum_Click(sender As Object, e As RoutedEventArgs)
        Dim modInfo = TryCast(ModList.SelectedItem, ForumMod)
        Dim uri = If(modInfo Is Nothing, Nothing, ForumUri(modInfo))
        If uri Is Nothing Then Return
        Try
            Process.Start(New ProcessStartInfo(uri.AbsoluteUri) With {.UseShellExecute = True})
        Catch ex As Exception
            LabStatus.Text = "无法打开发布帖：" & ex.Message
        End Try
    End Sub

    Private Function ForumUri(modInfo As ForumMod) As Uri
        For Each url In modInfo.PublishUrls
            Dim uri As Uri = Nothing
            If Uri.TryCreate(url, UriKind.Absolute, uri) AndAlso (uri.Scheme = "https" OrElse uri.Scheme = "http") Then Return uri
        Next
        Return Nothing
    End Function

    Private Function LabelFor(labels As Dictionary(Of String, String), key As String) As String
        Dim label As String = Nothing
        Return If(labels.TryGetValue(key, label), label, key)
    End Function

    Private Function SourceLabel(kind As String) As String
        Select Case kind
            Case "original" : Return "原创"
            Case "translated" : Return "汉化"
            Case "reposted" : Return "转载"
            Case Else : Return kind
        End Select
    End Function

    Private Class ReleaseOption
        Public Property Release As ForumModRelease
        Public Property Display As String
    End Class
End Class
