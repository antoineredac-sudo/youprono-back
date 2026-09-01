using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class UserPlayerForMatch
{
    public Guid Id { get; set; }

    public Guid UserMatchId { get; set; }

    public string PersonId { get; set; } = null!;

    public virtual Person Person { get; set; } = null!;

    public virtual UserMatch UserMatch { get; set; } = null!;
}
