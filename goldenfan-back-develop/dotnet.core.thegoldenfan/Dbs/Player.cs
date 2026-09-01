using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class Player
{
    public int? ShirtNumber { get; set; }

    public bool? Active { get; set; }

    public string? PersonId { get; set; }

    public Guid Id { get; set; }

    public string? TeamId { get; set; }

    public string? Type { get; set; }

    public string? Position { get; set; }

    public virtual Person? Person { get; set; }

    public virtual Team? Team { get; set; }
}
