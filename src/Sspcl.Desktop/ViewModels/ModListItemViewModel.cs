using System.Windows;
using System.Windows.Media;
using Sspcl.Core.Mods;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;

namespace Sspcl.Desktop.ViewModels;

/// <summary>Mod 列表里的一行。IsEnabled 由 CheckBox 双向绑定，变更时回调 ToggleHandler。</summary>
public sealed class ModListItemViewModel : ObservableObject
{
    private bool _isEnabled;
    private bool _isAutoEnabled;
    private bool _favorite;
    private string _statusText = "";
    private string? _problemText;
    private string _note = "";
    private string _tags = "";
    private Brush _statusBrush = Brushes.Gray;

    public ModSpec Spec { get; }
    public Action<ModListItemViewModel, bool>? ToggleHandler { get; set; }

    public ModListItemViewModel(ModSpec spec) => Spec = spec;

    public string Id => Spec.Id;
    public string Name => string.IsNullOrWhiteSpace(Spec.Name) ? Spec.Folder : Spec.Name;
    public string Version => string.IsNullOrEmpty(Spec.VersionRaw) ? "—" : Spec.VersionRaw;
    public string GameVersion => string.IsNullOrEmpty(Spec.GameVersionRaw) ? "—" : Spec.GameVersionRaw;
    public string Authors => string.Join(" · ", Spec.Authors);
    public string Description => Spec.Description;
    public string Folder => Spec.Folder;
    public string DepsText => Spec.Dependencies.Count == 0
        ? ""
        : "前置：" + string.Join("  ", Spec.Dependencies.Select(d => d.Id));

    public string? ProblemText { get => _problemText; private set => Set(ref _problemText, value); }
    public bool HasProblem => !string.IsNullOrEmpty(ProblemText);

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (Set(ref _isEnabled, value))
                ToggleHandler?.Invoke(this, value);
        }
    }

    public bool IsAutoEnabled { get => _isAutoEnabled; private set => Set(ref _isAutoEnabled, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public Brush StatusBrush { get => _statusBrush; private set => Set(ref _statusBrush, value); }

    public bool Favorite
    {
        get => _favorite;
        set
        {
            if (Set(ref _favorite, value))
            {
                OnPropertyChanged(nameof(FavoriteGlyph));
                OnPropertyChanged(nameof(FavoriteBrush));
            }
        }
    }
    public string FavoriteGlyph => Favorite ? "★" : "☆";
    public Brush FavoriteBrush => Favorite
        ? (Brush)Application.Current.Resources["Accent.Warm"]
        : (Brush)Application.Current.Resources["Text.Tertiary"];

    public string Note { get => _note; set { if (Set(ref _note, value)) { OnPropertyChanged(nameof(NoteText)); OnPropertyChanged(nameof(HasNote)); } } }
    public string Tags { get => _tags; set => Set(ref _tags, value); }
    public string NoteText => Note;
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    /// <summary>直接落状态，不触发 ToggleHandler（供页面在 _applying 期间统一刷新）。</summary>
    public void ApplyState(bool enabled, bool auto, string status, string? problem, Brush brush)
    {
        IsAutoEnabled = auto;
        StatusText = status;
        ProblemText = problem;
        StatusBrush = brush;
        _isEnabled = enabled;
        OnPropertyChanged(nameof(IsEnabled));
    }
}
