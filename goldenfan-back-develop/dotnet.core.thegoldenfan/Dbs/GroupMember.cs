using System;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class GroupMember
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }

    public Guid UserId { get; set; }

    public DateTime DateJoined { get; set; }

    public virtual Group Group { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
