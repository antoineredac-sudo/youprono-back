using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class Country
{
    public string Id { get; set; } = null!;

    public string? Name { get; set; }

    public string? NormalizedName { get; set; }

    public string? Code { get; set; }

    public virtual ICollection<Competition> Competitions { get; } = new List<Competition>();

    public virtual ICollection<Person> People { get; } = new List<Person>();

    public virtual ICollection<Team> Teams { get; } = new List<Team>();
}
