using System.Text.Json.Nodes;

namespace Sspcl.App.ViewModels;

/// <summary>settings.json 结构化编辑器的单个字段。</summary>
public sealed class SettingsFieldVM : ObservableObject
{
    public string Name { get; set; } = "";
    public string Translation { get; set; } = "";
    public string Format { get; set; } = "";
    public string Note { get; set; } = "";
    public string Kind { get; set; } = "string"; // bool / number / array / string
    public bool Deprecated { get; set; }

    /// <summary>settings.json 中的原值（用于保留类型）。</summary>
    public JsonNode? Original { get; set; }

    public bool IsChecked { get; set; }
    public string Text { get; set; } = "";

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (Set(ref _isExpanded, value)) OnPropertyChanged(nameof(ExpandGlyph));
        }
    }

    public string ExpandGlyph => IsExpanded ? "▾" : "▸";

    public bool IsBool => Kind == "bool";
    public bool ShowText => !IsBool;
    public string Label => Deprecated ? $"[废置] {Translation}" : Translation;
    public string SubLabel => $"{Name} · {Format}";
    public bool HasNote => Note.Length > 0;
}
