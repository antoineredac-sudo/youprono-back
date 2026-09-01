using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class Goal
{
    public Guid Id { get; set; }

    public Guid? TeamMatchId { get; set; }

    public int? Period { get; set; }

    public DateTime? Timestamp { get; set; }

    public string? Type { get; set; }

    public int? HomeScore { get; set; }

    public int? AwayScore { get; set; }

    public Guid? ScorerId { get; set; }

    public Guid? AssistId { get; set; }

    public virtual PlayerForMatch? Assist { get; set; }

    public virtual PlayerForMatch? Scorer { get; set; }

    public virtual TeamMatch? TeamMatch { get; set; }
}
