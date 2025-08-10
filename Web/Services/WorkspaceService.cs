namespace Web.Services
{
    public class WorkspaceService
    {
        private string? _selectedWorkspace;
        
        public event Action? WorkspaceChanged;

        public string? SelectedWorkspace
        {
            get => _selectedWorkspace;
            set
            {
                _selectedWorkspace = value;
                WorkspaceChanged?.Invoke();
            }
        }

        public bool HasSelectedWorkspace => !string.IsNullOrEmpty(_selectedWorkspace);
    }
} 