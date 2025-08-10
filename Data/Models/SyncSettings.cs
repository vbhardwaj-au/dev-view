namespace Data.Models
{
    public class SyncSettings
    {
        public string Mode { get; set; } = "Full";
        public int DeltaSyncDays { get; set; } = 5;
        public SyncTargets SyncTargets { get; set; } = new SyncTargets();
        public bool Overwrite { get; set; } = false;
    }

    public class SyncTargets
    {
        public bool Commits { get; set; } = true;
        public bool PullRequests { get; set; } = true;
        public bool Repositories { get; set; } = true;
        public bool Users { get; set; } = true;
    }
}