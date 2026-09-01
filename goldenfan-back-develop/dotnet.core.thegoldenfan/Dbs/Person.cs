using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class Person
{
    public string Id { get; set; } = null!;

    public string? OcId { get; set; }

    public string? OpId { get; set; }

    public string? FirstName { get; set; }

    public string? NormalizedFirstName { get; set; }

    public string? MiddleName { get; set; }

    public string? NormalizedMiddleName { get; set; }

    public string? LastName { get; set; }

    public string? NormalizedLastName { get; set; }

    public string? ShortFirstName { get; set; }

    public string? NormalizedShortFirstName { get; set; }

    public string? ShortLastName { get; set; }

    public string? NormalizedShortLastName { get; set; }

    public string? KnownName { get; set; }

    public string? NormalizedKnownName { get; set; }

    public string? MatchName { get; set; }

    public string? NormalizedMatchName { get; set; }

    public string? NationalityId { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public DateTime? DateOfDeath { get; set; }

    public string? PlaceOfBirth { get; set; }

    public string? CountryOfBirthId { get; set; }

    public bool Active { get; set; }

    public int Height { get; set; }

    public int? Weight { get; set; }

    public string? Foot { get; set; }

    public bool? Status { get; set; }

    public virtual Country? CountryOfBirth { get; set; }

    public virtual Nationality? Nationality { get; set; }

    public virtual ICollection<PlayerForMatch> PlayerForMatches { get; } = new List<PlayerForMatch>();

    public virtual ICollection<Player> Players { get; } = new List<Player>();

    public virtual ICollection<UserPlayerForMatch> UserPlayerForMatches { get; } = new List<UserPlayerForMatch>();
}
