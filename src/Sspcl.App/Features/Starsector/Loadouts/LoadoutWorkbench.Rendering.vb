Imports System.Windows.Media.Imaging
Imports System.Windows.Shapes
Imports Sspcl.Core.Loadouts

Public Partial Class LoadoutWorkbench
    Private ReadOnly _bitmaps As New Dictionary(Of String, BitmapSource)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _zoom As New ScaleTransform(1, 1)
    Private ReadOnly _pan As New TranslateTransform()
    Private _drag As Point?
    Private _manualView As Boolean
    Private _drawingBounds As Rect = New Rect(0, 0, 400, 400)

    Private Function Bitmap(path As String) As BitmapSource
        If String.IsNullOrEmpty(path) Then Return Nothing
        If _bitmaps.ContainsKey(path) Then Return _bitmaps(path)
        Try
            Dim result As New BitmapImage()
            result.BeginInit()
            result.CacheOption = BitmapCacheOption.OnLoad
            result.UriSource = New Uri(path, UriKind.Absolute)
            result.EndInit()
            result.Freeze()
            _bitmaps(path) = result
            Return result
        Catch
            _bitmaps(path) = Nothing
            Return Nothing
        End Try
    End Function
    Private Sub RenderShip()
        CloseWeaponDetails()
        ShipCanvas.Children.Clear()
        If _hull Is Nothing Then Return
        Const padding As Double = 56
        ShipCanvas.Width = _hull.Width + padding * 2
        ShipCanvas.Height = _hull.Height + padding * 2
        _drawingBounds = New Rect(padding, padding, _hull.Width, _hull.Height)
        Dim sprite = Bitmap(_hull.SpritePath)
        LabSprite.Text = If(sprite Is Nothing, "未找到舰船贴图，仍按船体坐标显示槽位。", "")
        If sprite IsNot Nothing Then
            Dim image As New Image With {.Source = sprite, .Width = _hull.Width, .Height = _hull.Height, .Stretch = Stretch.Fill, .IsHitTestVisible = False}
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality)
            Canvas.SetLeft(image, padding) : Canvas.SetTop(image, padding)
            ShipCanvas.Children.Add(image)
        End If
        Dim missing As Integer = 0
        For Each slot In _hull.Slots
            Dim weaponId As String = ""
            If Not _hull.BuiltInWeapons.TryGetValue(slot.Id, weaponId) Then _plan.Weapons.TryGetValue(slot.Id, weaponId)
            If String.IsNullOrEmpty(weaponId) Then Continue For
            Dim weapon = _catalog.Weapons.FirstOrDefault(Function(w) w.Id = weaponId)
            Dim point = ShipGeometry.SlotToSprite(_hull, slot)
            If weapon Is Nothing Then
                missing += 1 : Continue For
            End If
            Dim basePath = If(slot.Mount = "HARDPOINT", weapon.HardpointSprite, weapon.TurretSprite)
            Dim gunPath = If(slot.Mount = "HARDPOINT", weapon.HardpointGunSprite, weapon.TurretGunSprite)
            ' 如攻势级 TPC，固定武器的炮管已画在船体图上，规格有意不提供 hardpointSprite。
            If Bitmap(basePath) Is Nothing AndAlso Bitmap(gunPath) Is Nothing AndAlso Not _hull.BuiltInWeapons.ContainsKey(slot.Id) Then missing += 1
            If weapon.BarrelBelow Then AddWeaponLayer(gunPath, point, slot, padding)
            AddWeaponLayer(basePath, point, slot, padding)
            If Not weapon.BarrelBelow Then AddWeaponLayer(gunPath, point, slot, padding)
        Next
        If missing > 0 Then LabSprite.Text = If(sprite Is Nothing, LabSprite.Text & vbCrLf, "") & missing & " 个武器缺少贴图；导出仍保留原始 ID。"
        LabSprite.VerticalAlignment = If(sprite Is Nothing, VerticalAlignment.Center, VerticalAlignment.Bottom)
        For Each slot In _hull.Slots.Where(Function(s) s.CanEquip OrElse _hull.BuiltInWeapons.ContainsKey(s.Id))
            Dim point = ShipGeometry.SlotToSprite(_hull, slot)
            Dim selected = _slot IsNot Nothing AndAlso _slot.Id = slot.Id
            Dim slotBrush As Brush = If(slot.Locked, Brushes.SlateGray, If(slot.Type = "BALLISTIC", New SolidColorBrush(Color.FromRgb(255, 200, 106)), If(slot.Type = "ENERGY", New SolidColorBrush(Color.FromRgb(114, 199, 255)), If(slot.Type = "MISSILE", New SolidColorBrush(Color.FromRgb(246, 162, 207)), New SolidColorBrush(Color.FromRgb(133, 223, 188))))))
            If selected Then
                Dim arc As New Polyline With {.Stroke = slotBrush, .StrokeThickness = 1.2, .Opacity = 0.75, .IsHitTestVisible = False}
                arc.Points.Add(New Point(point.X + padding, point.Y + padding))
                For index = 0 To 32
                    Dim angle = (slot.Angle - slot.Arc / 2 + slot.Arc * index / 32) * Math.PI / 180
                    arc.Points.Add(New Point(point.X + padding - Math.Sin(angle) * 42, point.Y + padding - Math.Cos(angle) * 42))
                Next
                arc.Points.Add(New Point(point.X + padding, point.Y + padding))
                ShipCanvas.Children.Add(arc)
            End If
            Dim marker As New Ellipse With {.Width = If(selected, 16, 12), .Height = If(selected, 16, 12), .Stroke = If(selected, Brushes.DarkSlateGray, slotBrush), .StrokeThickness = If(selected, 2, 1.5), .Fill = New SolidColorBrush(Color.FromArgb(165, 16, 28, 48)), .Cursor = Cursors.Hand, .Tag = slot.Id, .ToolTip = slot.Display & vbCrLf & If(slot.Locked, "内置武器", "点击选择；右侧双击武器安装")}
            Canvas.SetLeft(marker, point.X + padding - marker.Width / 2)
            Canvas.SetTop(marker, point.Y + padding - marker.Height / 2)
            AddHandler marker.MouseLeftButtonDown, Sub(s, e)
                                                      SelectSlot(slot)
                                                      e.Handled = True
                                                  End Sub
            Dim installed = _catalog.Weapons.FirstOrDefault(Function(w) w.Id = If(_hull.BuiltInWeapons.ContainsKey(slot.Id), _hull.BuiltInWeapons(slot.Id), If(_plan.Weapons.ContainsKey(slot.Id), _plan.Weapons(slot.Id), "")))
            If installed IsNot Nothing Then
                marker.ToolTip = Nothing
                AddHandler marker.MouseEnter, Sub(s, e) BeginWeaponHover(installed, marker)
                AddHandler marker.MouseLeave, AddressOf WeaponLeft
            End If
            ShipCanvas.Children.Add(marker)
            _drawingBounds.Union(New Rect(point.X + padding - 44, point.Y + padding - 44, 88, 88))
        Next
        PreviewBox.Width = ShipCanvas.Width : PreviewBox.Height = ShipCanvas.Height
        If Not _manualView Then ApplyFit()
    End Sub
    Private Sub AddWeaponLayer(path As String, point As SpritePoint, slot As WeaponSlot, padding As Double)
        Dim source = Bitmap(path)
        If source Is Nothing Then Return
        ' 游戏武器按原生像素绘制；numFrames 常指独立帧文件，不能把第一帧再裁成 numFrames 份。
        Dim pivot = ShipGeometry.WeaponPivot(source.PixelWidth, source.PixelHeight, slot.Mount)
        Dim image As New Image With {.Source = source, .Width = source.PixelWidth, .Height = source.PixelHeight, .Stretch = Stretch.Fill, .IsHitTestVisible = False, .RenderTransform = New RotateTransform(ShipGeometry.WeaponRotation(slot), pivot.X, pivot.Y), .Tag = "weapon:" & slot.Id}
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality)
        Canvas.SetLeft(image, point.X + padding - pivot.X) : Canvas.SetTop(image, point.Y + padding - pivot.Y)
        ShipCanvas.Children.Add(image)
        Dim bounds = image.RenderTransform.TransformBounds(New Rect(0, 0, image.Width, image.Height))
        bounds.Offset(Canvas.GetLeft(image), Canvas.GetTop(image))
        _drawingBounds.Union(bounds)
    End Sub
    Private Sub FitPreview()
        Dim transforms As New TransformGroup()
        transforms.Children.Add(_zoom) : transforms.Children.Add(_pan)
        PreviewBox.RenderTransform = transforms
        PreviewBox.RenderTransformOrigin = New Point(0, 0)
        _manualView = False
        ApplyFit()
    End Sub
    Private Sub ApplyFit()
        If PreviewHost.ActualWidth <= 0 OrElse PreviewHost.ActualHeight <= 0 Then Return
        If Not TypeOf PreviewBox.RenderTransform Is TransformGroup Then
            Dim transforms As New TransformGroup()
            transforms.Children.Add(_zoom) : transforms.Children.Add(_pan)
            PreviewBox.RenderTransform = transforms
        End If
        Dim area = VisibleShipArea()
        Dim scale = Math.Min(area.Width / Math.Max(1, _drawingBounds.Width), area.Height / Math.Max(1, _drawingBounds.Height))
        scale = Math.Max(0.05, Math.Min(8, scale))
        _zoom.ScaleX = scale : _zoom.ScaleY = scale
        _pan.X = area.X + (area.Width - _drawingBounds.Width * scale) / 2 - _drawingBounds.X * scale
        _pan.Y = area.Y + (area.Height - _drawingBounds.Height * scale) / 2 - _drawingBounds.Y * scale
    End Sub
    Private Function VisibleShipArea() As Rect
        Dim reserve = OverlayRail.ActualWidth + 24
        Return New Rect(16, 42, Math.Max(80, PreviewHost.ActualWidth - reserve - 32), Math.Max(80, PreviewHost.ActualHeight - 62))
    End Function
    Private Sub PreviewResized(sender As Object, e As SizeChangedEventArgs) Handles PreviewHost.SizeChanged
        UpdateOverlaySizes()
        If Not _manualView Then
            ApplyFit()
        Else
            ' 窗口缩小时保持手工缩放，仅阻止舰船完全离开可见区域。
            Dim area = VisibleShipArea()
            Dim centerX = (_drawingBounds.X + _drawingBounds.Width / 2) * _zoom.ScaleX + _pan.X
            Dim centerY = (_drawingBounds.Y + _drawingBounds.Height / 2) * _zoom.ScaleY + _pan.Y
            _pan.X += Math.Max(area.Left, Math.Min(area.Right, centerX)) - centerX
            _pan.Y += Math.Max(area.Top, Math.Min(area.Bottom, centerY)) - centerY
        End If
    End Sub
    Private Sub OverlayResized(sender As Object, e As SizeChangedEventArgs) Handles OverlayRail.SizeChanged
        If Not _manualView Then ApplyFit()
    End Sub
    Private Sub FitClick(sender As Object, e As RoutedEventArgs) Handles BtnFit.Click
        FitPreview()
    End Sub
    Private Sub ZoomPreview(sender As Object, e As MouseWheelEventArgs) Handles PreviewHost.PreviewMouseWheel
        Dim factor = If(e.Delta > 0, 1.15, 1 / 1.15)
        Dim point = e.GetPosition(PreviewHost)
        Dim oldScale = _zoom.ScaleX
        _zoom.ScaleX = Math.Max(0.05, Math.Min(12, oldScale * factor))
        _zoom.ScaleY = _zoom.ScaleX
        Dim ratio = _zoom.ScaleX / oldScale
        _pan.X = point.X - (point.X - _pan.X) * ratio
        _pan.Y = point.Y - (point.Y - _pan.Y) * ratio
        _manualView = True
        e.Handled = True
    End Sub
    Private Sub StartPan(sender As Object, e As MouseButtonEventArgs) Handles PreviewHost.MouseDown
        If e.ChangedButton <> MouseButton.Middle Then Return
        _drag = e.GetPosition(PreviewHost)
        _manualView = True
        PreviewHost.CaptureMouse()
        e.Handled = True
    End Sub
    Private Sub MovePan(sender As Object, e As MouseEventArgs) Handles PreviewHost.MouseMove
        If Not _drag.HasValue Then Return
        Dim point = e.GetPosition(PreviewHost)
        _pan.X += point.X - _drag.Value.X : _pan.Y += point.Y - _drag.Value.Y
        _drag = point
    End Sub
    Private Sub EndPan(sender As Object, e As MouseButtonEventArgs) Handles PreviewHost.MouseUp
        If e.ChangedButton <> MouseButton.Middle Then Return
        _drag = Nothing
        PreviewHost.ReleaseMouseCapture()
    End Sub
    Private Sub PanCaptureLost(sender As Object, e As MouseEventArgs) Handles PreviewHost.LostMouseCapture
        _drag = Nothing
    End Sub
End Class
