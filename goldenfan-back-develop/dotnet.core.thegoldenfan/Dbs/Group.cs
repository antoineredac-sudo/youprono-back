using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class Group
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string InviteCode { get; set; } = null!;

    // "amis" = groupe d'amis, plafonne a 10 membres, points et medailles.
    // "kop"  = kop de supporters, sans plafond, ni points ni medailles.
    // Valeur par defaut en base : "amis". Tous les groupes existants le sont.
    public string Type { get; set; } = "amis";

    public Guid CreatorId { get; set; }

    public DateTime CreatedDate { get; set; }

    public virtual User Creator { get; set; } = null!;

    public virtual ICollection<GroupMember> Members { get; } = new List<GroupMember>();
}
