using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class Place
{
    public string Id { get; set; } = null!;

    public string? Name { get; set; }

    public string? NormalizedName { get; set; }

    public string? ShortName { get; set; }

    public string? NormalizedShortName { get; set; }

    public virtual ICollection<Match> Matches { get; } = new List<Match>();
}
