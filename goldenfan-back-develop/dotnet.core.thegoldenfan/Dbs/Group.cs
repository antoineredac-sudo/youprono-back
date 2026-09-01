using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class Group
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string InviteCode { get; set; } = null!;

    public Guid CreatorId { get; set; }

    public DateTime CreatedDate { get; set; }

    public virtual User Creator { get; set; } = null!;

    public virtual ICollection<GroupMember> Members { get; } = new List<GroupMember>();
}
