using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class Friend
{
    public Guid Id { get; set; }

    public Guid User0Id { get; set; }

    public Guid User1Id { get; set; }

    public DateTime DateCreated { get; set; }

    public virtual User User0 { get; set; } = null!;

    public virtual User User1 { get; set; } = null!;
}
