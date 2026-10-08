' =====================================================================
' 远行星号（Starsector）功能封装 —— 桥接 sspcl.Core 与 sspcl
' 本模块把 sspcl.Core 的 .NET Standard 2.0 程序集封装成 VB.NET 函数，
' 供 sspcl 的界面直接调用。
' =====================================================================

Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Text.RegularExpressions
Imports Sspcl.Core.Install
Imports Sspcl.Core.Launch
Imports Sspcl.Core.Mods
Imports Sspcl.Core.Saves
Imports Sspcl.Core.Store

Partial Module ModStarsector

    ''' <summary>检测远行星号安装目录（版本/汉化/是否有效）。</summary>
    Function Detect(installPath As String) As Installation
        Return InstallationDetector.Detect(installPath)
    End Function

    ''' <summary>mod 目录路径。</summary>
    Function ModsDir(installPath As String) As String
        Return IO.Path.Combine(installPath, "mods")
    End Function

    ''' <summary>扫描 mod 目录，返回所有 mod。</summary>
    Function ScanMods(modsDir As String) As IReadOnlyList(Of ModSpec)
        Return ModScanner.Scan(modsDir)
    End Function

    ''' <summary>读取已启用 mod id 列表。</summary>
    Function ReadEnabled(installPath As String) As List(Of String)
        Return EnabledModsFile.Read(installPath)
    End Function

    ''' <summary>写入已启用 mod id 列表（原子写 + 自动备份）。</summary>
    Sub WriteEnabled(installPath As String, ids As IEnumerable(Of String))
        EnabledModsFile.Write(installPath, ids)
    End Sub

    ''' <summary>计算启用闭包（自动补齐前置依赖）。</summary>
    Function Resolve(ids As IEnumerable(Of String), mods As IReadOnlyList(Of ModSpec)) As ISet(Of String)
        Return DependencyResolver.Closure(ids, mods)
    End Function

    ''' <summary>读取 vmparams 里的内存设置（MB）。</summary>
    Function ReadMemory(vmparamsText As String) As (XmxMb As Integer, XmsMb As Integer)
        Return Launcher.ReadMemoryMb(vmparamsText)
    End Function

    ''' <summary>直接按安装目录读取内存设置（MB）。</summary>
    Function ReadMemoryOf(installPath As String) As (XmxMb As Integer, XmsMb As Integer)
        Return Launcher.ReadMemoryMb(IO.File.ReadAllText(Launcher.VmparamsPath(installPath)))
    End Function

    ''' <summary>启动游戏（skipLauncher=True 跳过自带启动器直接进主菜单）。</summary>
    Function Launch(installPath As String, xmxMb As Integer, xmsMb As Integer, skipLauncher As Boolean,
                    Optional resolution As String = "1920x1080",
                    Optional priority As ProcessPriorityClass = ProcessPriorityClass.Normal,
                    Optional vmArgs As String = "") As Process
        ' 热启动优化：AppCDS（类数据共享）。Java 17 不支持 AutoCreateSharedArchive，改用两段式：
        ' 首次启动用 ArchiveClassesAtExit 在退出时生成归档；之后用 SharedArchiveFile 加载归档加速类加载。
        If Settings.Get(Of Boolean)("StarsectorWarmStart") Then
            Dim jsa = IO.Path.Combine(installPath, "starsector.jsa")
            If IO.File.Exists(jsa) Then
                vmArgs = "-XX:SharedArchiveFile=""" & jsa & """ " & vmArgs
            Else
                vmArgs = "-XX:ArchiveClassesAtExit=""" & jsa & """ " & vmArgs
            End If
        End If
        Dim extra As IReadOnlyList(Of String) = Nothing
        If Not String.IsNullOrWhiteSpace(vmArgs) Then extra = Launcher.Tokenize(vmArgs)
        Return Launcher.Launch(installPath, xmxMb, xmsMb, skipLauncher, resolution, False, True, priority, extra)
    End Function

    ''' <summary>扫描存档列表。</summary>
    Function ScanSaves(installPath As String) As IReadOnlyList(Of SaveInfo)
        Return SaveScanner.Scan(IO.Path.Combine(installPath, "saves"))
    End Function

    ''' <summary>删除一个 mod 目录（进回收站）。</summary>
    Sub RecycleDir(path As String)
        ModInstaller.RecycleDir(path)
    End Sub

    ''' <summary>写入 vmparams 里的内存设置（MB）。</summary>
    Sub WriteMemory(installPath As String, xmxMb As Integer, xmsMb As Integer)
        Dim p = Launcher.VmparamsPath(installPath)
        Dim text = IO.File.ReadAllText(p)
        text = Regex.Replace(text, "-Xmx\d+[kKmMgG]?", "-Xmx" & xmxMb & "m")
        text = Regex.Replace(text, "-Xms\d+[kKmMgG]?", "-Xms" & xmsMb & "m")
        IO.File.WriteAllText(p, text)
    End Sub

    ''' <summary>拉取 Mod 商店索引。</summary>
    Function FetchStore() As Task(Of List(Of StoreItem))
        Return ModRepoClient.FetchAsync()
    End Function

    ''' <summary>下载并安装一个 mod（直链），返回错误信息（空串 = 成功）。</summary>
    Async Function InstallFromUrl(url As String, modsDir As String) As Task(Of String)
        Try
            Dim tmp = Await ModRepoClient.DownloadToTempAsync(url)
            Dim r = ModInstaller.InstallArchive(tmp, modsDir)
            Return If(r.Success, "", r.Error)
        Catch ex As Exception
            Return ex.Message
        End Try
    End Function

End Module
