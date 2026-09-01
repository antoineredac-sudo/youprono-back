using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class MatchDate
{
    public Guid Id { get; set; }

    public string? CalendarId { get; set; }

    public DateTime? Date { get; set; }

    public virtual Calendar? Calendar { get; set; }

    public virtual ICollection<Match> Matches { get; } = new List<Match>();
}
