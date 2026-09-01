using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class Follower
{
    public Guid Id { get; set; }

    public DateTime DateCreated { get; set; }

    public Guid UserId { get; set; }

    public Guid FollowerId { get; set; }

    public virtual User FollowerNavigation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
