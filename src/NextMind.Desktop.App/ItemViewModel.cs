using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using NextMind.Desktop.Core.Config;

namespace NextMind.Desktop.App;

/// <summary>UI state of one zone member: the label, the Shell icon (loaded in the background) and whether the target exists.</summary>
public sealed class ItemViewModel(ItemConfig config) : INotifyPropertyChanged
{
    private ImageSource? _icon;
    private bool _isMissing;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised on the UI thread the first time the background probe learns whether the target is a folder.</summary>
    public event Action<ItemViewModel, bool>? FolderProbed;

    public ItemConfig Config { get; } = config;

    public string Id => Config.Id;

    public string Path => Config.Path;

    public string Name => Config.Name;

    public bool IsManaged => Config.Kind == ItemKind.Managed;

    public ImageSource? Icon
    {
        get => _icon;
        private set => Set(ref _icon, value, nameof(Icon));
    }

    public bool IsMissing
    {
        get => _isMissing;
        private set
        {
            if (Set(ref _isMissing, value, nameof(IsMissing)))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MissingVisibility)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tooltip)));
            }
        }
    }

    public Visibility MissingVisibility => IsMissing ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Subtle information only (no badges on the tile): where it is, and whether it is a managed Desktop item or a reference.</summary>
    public string Tooltip
    {
        get
        {
            var kind = IsManaged ? "Managed Desktop Item" : "Reference";
            return IsMissing ? $"Missing or unavailable ({kind})\n{Path}" : $"{Path}\n{kind}";
        }
    }

    /// <summary>Called on the UI thread with the result of a background probe.</summary>
    public void Apply(bool exists, ImageSource? icon, bool isDirectory)
    {
        IsMissing = !exists;
        Icon = exists ? icon : null;

        if (exists)
        {
            FolderProbed?.Invoke(this, isDirectory);
        }
    }

    private bool Set<T>(ref T field, T value, string name)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
