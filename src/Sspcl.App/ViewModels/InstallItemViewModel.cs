using System.Windows;
using System.Windows.Media;
using Sspcl.Core.Install;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;

namespace Sspcl.App.ViewModels;

/// <summary>版本卡片里的一行。</summary>
public sealed class InstallItemViewModel
{
    public Installation Installation { get; }
    public bool IsCurrent { get; }

    public string DisplayName => Installation.DisplayName;
    public string Version => Installation.Version;
    public string Path => Installation.Path;
    public string Initial => string.IsNullOrEmpty(DisplayName) ? "?" : DisplayName[..1].ToUpperInvariant();
    public string Subtitle =>
        $"{Installation.ModCount} 个 mod · 启用 {Installation.EnabledModCount} · {Installation.SaveCount} 个存档 · 汉化 {Installation.LocalizationPackageVersion}";
    public bool HasProblems => Installation.Problems.Count > 0;
    public string ProblemsText => HasProblems ? "问题：" + string.Join("；", Installation.Problems) : "";

    public Brush CurrentBrush => IsCurrent
        ? (Brush)Application.Current.Resources["Accent.Primary"]
        : (Brush)Application.Current.Resources["State.Idle"];

    public InstallItemViewModel(Installation installation, bool isCurrent)
    {
        Installation = installation;
        IsCurrent = isCurrent;
    }
}
