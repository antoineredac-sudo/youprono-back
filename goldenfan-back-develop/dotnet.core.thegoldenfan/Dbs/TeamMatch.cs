using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class TeamMatch
{
    public Guid Id { get; set; }

    public string TeamId { get; set; } = null!;

    public DateTime? CreateDate { get; set; }

    public string? FormatType { get; set; }

    public int Score { get; set; }

    public string? Stats { get; set; }

    public virtual ICollection<Match> MatchAwayTeams { get; } = new List<Match>();

    public virtual ICollection<Match> MatchHomeTeams { get; } = new List<Match>();

    public virtual ICollection<PlayerForMatch> PlayerForMatches { get; } = new List<PlayerForMatch>();

    public virtual Team Team { get; set; } = null!;
}
