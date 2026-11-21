namespace MockRtsp;

/// <summary>
/// Picks the videos a camera plays, in order: from the video folder, or any other file.
/// The layout lives in PlaylistDialog.Designer.cs.
/// </summary>
public sealed partial class PlaylistDialog : Form
{
    /// <summary>A file shown by its name.</summary>
    private sealed record FileItem(string Path)
    {
        public override string ToString() => System.IO.Path.GetFileName(Path);
    }

    private readonly string _filter;

    /// <summary>Parameterless constructor for the designer.</summary>
    public PlaylistDialog() : this(Array.Empty<string>(), Array.Empty<string>(), "camera", Array.Empty<string>())
    {
    }

    /// <param name="available">The playable videos of the folder.</param>
    /// <param name="playlist">The camera's playlist now.</param>
    /// <param name="camera">The camera's stream path, for the title.</param>
    /// <param name="extensions">Video file extensions, for "Other file...".</param>
    public PlaylistDialog(IEnumerable<string> available, IEnumerable<string> playlist, string camera, IEnumerable<string> extensions)
    {
        InitializeComponent();

        Text = $"Playlist for {camera}";
        _available.Items.AddRange(available.Select(f => (object)new FileItem(f)).ToArray());
        _items.Items.AddRange(playlist.Select(f => (object)new FileItem(f)).ToArray());

        string patterns = string.Join(";", extensions.Select(e => "*" + e));
        _filter = $"Videos ({patterns})|{patterns}|All files (*.*)|*.*";

        if (_items.Items.Count > 0)
            _items.SelectedIndex = 0;

        UpdateButtons();
    }

    /// <summary>The playlist chosen, in order. Read after OK.</summary>
    public IReadOnlyList<string> Files => _items.Items.Cast<FileItem>().Select(i => i.Path).ToList();

    private void OnAddClick(object? sender, EventArgs e)
    {
        foreach (var item in _available.SelectedItems.Cast<FileItem>().ToList())
            Add(item.Path);

        UpdateButtons();
    }

    private void OnAddFileClick(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Add videos to the playlist",
            Filter = _filter,
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        foreach (var file in dialog.FileNames)
            Add(file);

        UpdateButtons();
    }

    /// <summary>Adds a video at the end, or selects it if it is in the playlist already.</summary>
    private void Add(string file)
    {
        string full = Path.GetFullPath(file);
        int existing = IndexOf(full);
        if (existing < 0)
            existing = _items.Items.Add(new FileItem(full));

        _items.SelectedIndex = existing;
    }

    private int IndexOf(string file)
    {
        for (int i = 0; i < _items.Items.Count; i++)
        {
            if (string.Equals(((FileItem)_items.Items[i]).Path, file, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private void OnRemoveClick(object? sender, EventArgs e)
    {
        int index = _items.SelectedIndex;
        if (index < 0)
            return;

        _items.Items.RemoveAt(index);
        if (_items.Items.Count > 0)
            _items.SelectedIndex = Math.Min(index, _items.Items.Count - 1);

        UpdateButtons();
    }

    private void OnUpClick(object? sender, EventArgs e) => MoveSelected(-1);

    private void OnDownClick(object? sender, EventArgs e) => MoveSelected(1);

    private void MoveSelected(int by)
    {
        int index = _items.SelectedIndex;
        int target = index + by;
        if (index < 0 || target < 0 || target >= _items.Items.Count)
            return;

        var item = _items.Items[index];
        _items.Items.RemoveAt(index);
        _items.Items.Insert(target, item);
        _items.SelectedIndex = target;
        UpdateButtons();
    }

    private void OnSelectionChanged(object? sender, EventArgs e) => UpdateButtons();

    private void UpdateButtons()
    {
        int index = _items.SelectedIndex;
        _add.Enabled = _available.SelectedItems.Count > 0;
        _remove.Enabled = index >= 0;
        _up.Enabled = index > 0;
        _down.Enabled = index >= 0 && index < _items.Items.Count - 1;
        _ok.Enabled = _items.Items.Count > 0;
    }

    private void OnOkClick(object? sender, EventArgs e)
    {
        if (_items.Items.Count == 0)
        {
            MessageBox.Show(this, "Add at least one video.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DialogResult = DialogResult.OK;
    }
}
