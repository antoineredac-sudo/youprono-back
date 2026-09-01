using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class PlayerForMatch
{
    public Guid Id { get; set; }

    public string PersonId { get; set; } = null!;

    public DateTime CreateDate { get; set; }

    public Guid TeamMatchId { get; set; }

    public int ShirtNumber { get; set; }

    public string? Position { get; set; }

    public string? PositionSide { get; set; }

    public string? FormationPlace { get; set; }

    public string? Stats { get; set; }

    public virtual Person Person { get; set; } = null!;

    public virtual TeamMatch TeamMatch { get; set; } = null!;
}
