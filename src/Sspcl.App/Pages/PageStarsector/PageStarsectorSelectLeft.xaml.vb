Imports System.Windows.Media

Public Class PageStarsectorSelectLeft

    Public Event AddFolderRequested()
    Public Event FolderSelected(path As String)

    Private Const AddIcon As String = "M512.277 954.412c-118.89 0-230.659-46.078-314.73-129.73S67.12 629.666 67.12 511.222s46.327-229.744 130.398-313.427 195.82-129.73 314.73-129.73 230.659 46.078 314.72 129.73S957.397 392.81 957.397 511.183 911.078 740.96 826.97 824.642s-195.8 129.77-314.692 129.77z m0-822.784c-101.972 0-197.809 39.494-269.865 111.222s-111.7 166.997-111.7 268.373 39.653 196.695 111.67 268.335S410.246 890.78 512.248 890.78s197.809-39.484 269.865-111.222 111.7-166.998 111.67-268.374c-0.03-101.375-39.654-196.665-111.67-268.303S614.22 131.628 512.277 131.628z m222.585 347.8H544.073V288.64c-0.76-17.561-15.613-31.18-33.173-30.419-16.495 0.714-29.704 13.924-30.419 30.419v190.787H289.703c-17.56 0.761-31.179 15.614-30.419 33.174 0.715 16.494 13.924 29.703 30.42 30.418H480.48v190.788c0.761 17.56 15.614 31.179 33.174 30.419 16.494-0.715 29.703-13.925 30.418-30.42V543.02h190.788c17.56 0.762 32.413-12.857 33.173-30.418 0.762-17.561-12.858-32.414-30.419-33.174a31.683 31.683 0 0 0-2.753 0z"
    Private Const ImportIcon As String = "M512 40.96C249.344 40.96 35.84 252.416 35.84 512s213.504 471.04 476.16 471.04c103.424 0 202.752-33.28 286.72-96.256l1.536-1.536c5.12-5.632 7.68-12.8 7.68-19.968 0-16.896-13.824-30.208-30.72-30.208-7.68 0-15.36 2.56-20.992 7.68h-0.512c-71.68 52.224-155.648 79.36-243.712 79.36-227.328 0-412.16-182.784-412.16-407.552 0-224.768 184.832-407.552 412.16-407.552s412.16 182.784 412.16 407.552c0 68.608-15.872 132.608-46.592 190.464-0.512 1.024-1.024 2.048-1.024 3.072-0.512 2.048-1.536 4.608-1.536 8.192 0 16.896 13.824 30.208 30.72 30.208 12.288 0 23.04-7.168 28.16-18.432 35.84-68.608 53.76-141.312 53.76-216.064 0.512-259.584-212.992-471.04-475.648-471.04z M812.032 483.328c-31.744-20.992-71.68 1.536-78.848 6.144-1.024 0.512-104.448 61.44-128 74.752-8.192 4.608-22.528-0.512-27.136-4.096-31.232-36.352-54.272-70.656-68.608-102.4-13.312-29.184 0.512-41.472 3.072-43.52 7.168-4.608 114.688-68.608 143.36-83.456 24.064-12.288 40.96-25.088 46.08-45.056 3.072-13.312 0-27.136-9.216-39.936-22.016-31.744-172.544-84.992-311.296-3.584-157.184 91.648-152.064 242.688-150.528 292.352v9.216c0 18.944-12.8 37.376-14.848 40.448l-20.992 21.504c-6.144 6.144-9.216 13.824-9.216 22.528 0 8.704 3.584 16.384 9.728 22.528 12.8 12.288 32.768 11.776 45.056-0.512l22.528-23.552 0.512-0.512c3.072-3.584 30.208-38.4 30.208-81.92l-0.512-11.264c-1.536-44.544-5.632-162.816 119.296-235.52 88.064-51.2 173.056-32.256 208.896-19.968-36.864 19.456-143.36 83.456-144.896 84.48-22.016 14.336-55.808 58.88-26.112 122.88 17.408 37.376 43.52 76.8 80.896 120.32 14.336 17.408 62.976 37.376 103.424 15.36 24.576-13.312 125.44-73.216 130.048-75.776 2.048-1.024 4.608-2.56 7.68-3.584 0 2.56-0.512 6.144-1.024 10.752-5.632 35.84-35.328 155.136-191.488 181.76-49.664 8.704-89.6 3.584-121.856-0.512h-0.512c-37.888-4.608-73.216-9.216-101.888 14.336-31.232 26.112-40.96 34.304-35.84 54.272 3.584 14.336 16.384 24.064 30.72 24.064 2.56 0 5.12-0.512 7.68-1.024 6.656-1.536 12.8-5.632 16.896-10.752 2.048-2.048 7.68-6.656 20.992-18.432 6.656-5.632 25.088-3.584 52.736 0 34.816 4.608 81.92 10.24 141.312 0.512 157.184-26.624 228.864-138.752 243.2-234.496 7.68-38.912 0-64.512-21.504-78.336z"

    Private Sub Init() Handles Me.Loaded
        RefreshList()
    End Sub

    Public Sub RefreshList()
        PanList.Children.Clear()
        '文件夹列表 标题
        PanList.Children.Add(New TextBlock With {.Text = "文件夹列表", .Margin = New Thickness(13, 18, 5, 4), .Opacity = 0.6, .FontSize = 12})
        For Each p In ModMain.StarsectorPaths.ToList()
            Dim name = IO.Path.GetFileName(p.TrimEnd("\"c))
            If String.IsNullOrEmpty(name) Then name = p
            Dim item As New MyListItem With {
                .IsScaleAnimationEnabled = False,
                .Type = MyListItem.CheckType.RadioBox,
                .MinPaddingRight = 30,
                .Title = name,
                .Info = p,
                .Height = 40,
                .Tag = p
            }
            AddHandler item.Changed, Sub(s, e2)
                                          Dim li = TryCast(s, MyListItem)
                                          If li IsNot Nothing AndAlso li.Checked Then SelectFolder(p)
                                      End Sub
            Dim gear As New MyIconButton With {.Logo = Logo.IconButtonSetup, .LogoScale = 1.1, .ToolTip = "移除该文件夹"}
            AddHandler gear.Click, Sub(s, e2) RemoveFolder(p)
            item.Buttons = {gear}
            PanList.Children.Add(item)
        Next
        '添加或导入 标题
        PanList.Children.Add(New TextBlock With {.Text = "添加或导入", .Margin = New Thickness(13, 18, 5, 4), .Opacity = 0.6, .FontSize = 12})
        Dim addItem As New MyListItem With {.IsScaleAnimationEnabled = False, .Type = MyListItem.CheckType.Clickable, .Title = "添加已有文件夹", .Height = 34, .ToolTip = "将一个已有的远行星号文件夹添加到列表", .Logo = AddIcon}
        AddHandler addItem.Click, Sub(s, e2) RaiseEvent AddFolderRequested()
        PanList.Children.Add(addItem)
        '边距
        PanList.Children.Add(New FrameworkElement With {.Height = 10, .IsHitTestVisible = False})
        '确认勾选状态
        Dim sel = If(String.IsNullOrWhiteSpace(ModMain.SelectedFolderPath), ModMain.StarsectorPath, ModMain.SelectedFolderPath)
        For Each child As UIElement In PanList.Children
            Dim it = TryCast(child, MyListItem)
            If it IsNot Nothing AndAlso it.Tag IsNot Nothing AndAlso String.Equals(it.Tag.ToString(), sel, StringComparison.OrdinalIgnoreCase) Then
                it.Checked = True
            End If
        Next
    End Sub

    Private Sub SelectFolder(path As String)
        Logger.Info("SelectFolder=" & path)
        ModMain.SelectedFolderPath = path
        RaiseEvent FolderSelected(path)
    End Sub

    Private Sub RemoveFolder(path As String)
        ModMain.StarsectorPaths.Remove(path)
        ModMain.SaveStarsectorPaths()
        RefreshList()
        ModMain.FrmStarsectorSelectRight.RefreshCards()
    End Sub

End Class
