namespace VolturaAiWatcher;

public sealed class ChatFilterOption : System.ComponentModel.INotifyPropertyChanged
{
    private string _displayName;
    private string _projectName;
    private bool _isSelected;

    public ChatFilterOption(string? threadId, string displayName, string projectName, bool isSelected = false)
    {
        ThreadId = threadId;
        _displayName = displayName;
        _projectName = projectName;
        _isSelected = isSelected;
    }

    public string? ThreadId { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value);
    }

    public string ProjectName
    {
        get => _projectName;
        set => SetField(ref _projectName, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}
