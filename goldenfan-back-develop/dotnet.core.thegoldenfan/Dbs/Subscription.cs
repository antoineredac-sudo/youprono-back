using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class Subscription
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string EndPoint { get; set; } = null!;

    public string P256dh { get; set; } = null!;

    public string Auth { get; set; } = null!;

    public DateTime? ExpirationTime { get; set; }

    public virtual User User { get; set; } = null!;
}
