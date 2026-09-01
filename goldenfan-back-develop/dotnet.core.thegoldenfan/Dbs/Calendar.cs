using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class Calendar
{
    public string Id { get; set; } = null!;

    public string? OcId { get; set; }

    public string? Name { get; set; }

    public string? NormalizedName { get; set; }

    public bool IncludesVenues { get; set; }

    public bool Active { get; set; }

    public bool IncludesStandings { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public DateTime LastUpdated { get; set; }

    public string CompetitionId { get; set; } = null!;

    public virtual Competition Competition { get; set; } = null!;

    public virtual ICollection<MatchDate> MatchDates { get; } = new List<MatchDate>();
}
