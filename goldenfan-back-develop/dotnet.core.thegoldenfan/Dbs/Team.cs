using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class Team
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? NormalizedName { get; set; }

    public string? ShortName { get; set; }

    public string? NormalizedShortName { get; set; }

    public string? OfficialName { get; set; }

    public string? NormalizedOfficialName { get; set; }

    public string? Code { get; set; }

    public string? Type { get; set; }

    public string? TeamType { get; set; }

    public string CountryId { get; set; } = null!;

    public string? City { get; set; }

    public string? PostalAddress { get; set; }

    public string? AddressZip { get; set; }

    public string? Founded { get; set; }

    public string? Details { get; set; }

    public bool Status { get; set; }

    public DateTime LastUpdated { get; set; }

    public virtual Country Country { get; set; } = null!;

    public virtual ICollection<Player> Players { get; } = new List<Player>();

    public virtual ICollection<TeamMatch> TeamMatches { get; } = new List<TeamMatch>();
}
