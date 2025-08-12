using System.Net.Http.Json;

namespace Web.Services
{
    public class WorkspaceService
    {
        private string? _selectedWorkspace;
        private readonly IHttpClientFactory _httpClientFactory;
        
        public event Action? WorkspaceChanged;

        public WorkspaceService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

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

        /// <summary>
        /// Automatically selects a workspace if there's only one available
        /// </summary>
        public async Task<bool> TryAutoSelectWorkspaceAsync()
        {
            if (HasSelectedWorkspace)
                return true;

            try
            {
                var httpClient = _httpClientFactory.CreateClient("api");
                var workspaces = await httpClient.GetFromJsonAsync<IEnumerable<string>>("api/analytics/workspaces");
                
                if (workspaces != null && workspaces.Any())
                {
                    // If there's only one workspace, auto-select it
                    if (workspaces.Count() == 1)
                    {
                        SelectedWorkspace = workspaces.First();
                        return true;
                    }
                    // If there are multiple workspaces but none selected, select the first one
                    else if (workspaces.Any())
                    {
                        SelectedWorkspace = workspaces.First();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw - this is just for auto-selection
                Console.WriteLine($"Error auto-selecting workspace: {ex.Message}");
            }

            return false;
        }
    }
} 