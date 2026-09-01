using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class Competition
{
    public string Id { get; set; } = null!;

    public string? OcId { get; set; }

    public string? OpId { get; set; }

    public string? CompetitionCode { get; set; }

    public string? NormalizedCompetitionCode { get; set; }

    public string CountryId { get; set; } = null!;

    public string? Name { get; set; }

    public string? NormalizedName { get; set; }

    public string? CompetitionFormat { get; set; }

    public string? NormalizedCompetitionFormat { get; set; }

    public string? Type { get; set; }

    public string? NormalizedType { get; set; }

    public string? CompetitionType { get; set; }

    public string? NormalizedCompetitionType { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsFriendly { get; set; }

    public virtual ICollection<Calendar> Calendars { get; } = new List<Calendar>();

    public virtual Country Country { get; set; } = null!;
}
