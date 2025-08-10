namespace Entities.DTOs.Teams;

public class TeamSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedOn { get; set; }
    public bool IsActive { get; set; }
    public int MemberCount { get; set; }
} 