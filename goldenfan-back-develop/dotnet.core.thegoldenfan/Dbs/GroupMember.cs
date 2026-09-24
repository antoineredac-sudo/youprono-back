using System;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class GroupMember
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }

    public Guid UserId { get; set; }

    public DateTime DateJoined { get; set; }

    // Qui l'a inscrit, quand ce n'est pas lui-meme : le createur du tournoi qui
    // est alle le chercher dans le classement general (23 septembre 2026).
    public Guid? AddedByUserId { get; set; }

    // Vrai des qu'on le lui a dit. Le bandeau d'annonce ne se montre qu'une fois.
    public bool NoticeSeen { get; set; }

    public virtual Group Group { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
