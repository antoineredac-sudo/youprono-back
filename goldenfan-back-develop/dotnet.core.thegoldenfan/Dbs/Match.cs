using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class Match
{
    public string? CoverageLevel { get; set; }

    public DateTime DateTime { get; set; }

    public Guid AwayTeamId { get; set; }

    public Guid HomeTeamId { get; set; }

    public string Id { get; set; } = null!;

    public Guid? MatchDateId { get; set; }

    public string? PlaceId { get; set; }

    public string? Status { get; set; }

    public string? Winner { get; set; }

    public string? Cards { get; set; }

    public string? Goals { get; set; }

    public string? Substitute { get; set; }

    public virtual TeamMatch AwayTeam { get; set; } = null!;

    public virtual TeamMatch HomeTeam { get; set; } = null!;

    public virtual MatchDate? MatchDate { get; set; }

    public virtual Place? Place { get; set; }

    public virtual ICollection<UserMatch> UserMatches { get; } = new List<UserMatch>();
}
