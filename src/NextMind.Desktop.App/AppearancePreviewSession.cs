using NextMind.Desktop.Core.Config;

namespace NextMind.Desktop.App;

/// <summary>
/// Keeps live appearance edits separate from the persisted configuration.
/// The last committed snapshot is restored when an editing session is abandoned.
/// </summary>
public sealed class AppearancePreviewSession
{
    private readonly Action<AppearancePreferencesConfig> _preview;
    private readonly Action<AppearancePreferencesConfig> _persist;
    private AppearancePreferencesConfig _committed;
    private bool _hasUncommittedPreview;

    public AppearancePreviewSession(
        AppearancePreferencesConfig committed,
        Action<AppearancePreferencesConfig> preview,
        Action<AppearancePreferencesConfig> persist)
    {
        ArgumentNullException.ThrowIfNull(committed);
        _preview = preview ?? throw new ArgumentNullException(nameof(preview));
        _persist = persist ?? throw new ArgumentNullException(nameof(persist));
        _committed = AppearanceEditor.Copy(committed);
    }

    public void Preview(AppearancePreferencesConfig appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        _hasUncommittedPreview = true;
        _preview(AppearanceEditor.Copy(appearance));
    }

    public void Commit(AppearancePreferencesConfig appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        var snapshot = AppearanceEditor.Copy(appearance);
        _preview(AppearanceEditor.Copy(snapshot));
        _persist(AppearanceEditor.Copy(snapshot));
        _committed = snapshot;
        _hasUncommittedPreview = false;
    }

    public void Rollback()
    {
        if (!_hasUncommittedPreview)
        {
            return;
        }

        _preview(AppearanceEditor.Copy(_committed));
        _hasUncommittedPreview = false;
    }
}
