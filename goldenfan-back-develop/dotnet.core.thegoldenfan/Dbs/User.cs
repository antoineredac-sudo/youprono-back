using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class User
{
    public Guid Id { get; set; }

    public DateTime DateCreated { get; set; }

    public string? DisplayName { get; set; }

    public string? NormalizedDisplayName { get; set; }

    public string? Password { get; set; }

    // Sert d'abord a retrouver son compte : le jeu identifie les gens par pseudo,
    // et un pseudo s'oublie. Servira aussi au rappel avant match, mais seulement
    // pour ceux qui ont coche EmailOptIn.
    public string? Email { get; set; }

    public bool EmailOptIn { get; set; }

    // Reinitialisation du mot de passe : un jeton a usage unique, valable une
    // heure. Les deux champs sont vides en temps normal.
    public string? ResetToken { get; set; }

    public DateTime? ResetTokenExpires { get; set; }

    // Date d'envoi du courriel de bienvenue. Vide tant qu'il n'est pas parti :
    // c'est ce qui empeche un second envoi si la route est appelee deux fois.
    public DateTime? WelcomeSentAt { get; set; }

    public virtual ICollection<Follower> FollowerFollowerNavigations { get; } = new List<Follower>();

    public virtual ICollection<Follower> FollowerUsers { get; } = new List<Follower>();

    public virtual ICollection<Friend> FriendUser0s { get; } = new List<Friend>();

    public virtual ICollection<Friend> FriendUser1s { get; } = new List<Friend>();

    public virtual ICollection<Subscription> Subscriptions { get; } = new List<Subscription>();

    public virtual ICollection<UserMatch> UserMatches { get; } = new List<UserMatch>();
}
