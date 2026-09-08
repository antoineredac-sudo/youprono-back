using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Calendar> Calendars { get; set; }

    public virtual DbSet<Competition> Competitions { get; set; }

    public virtual DbSet<Country> Countries { get; set; }

    public virtual DbSet<Follower> Followers { get; set; }

    public virtual DbSet<Group> Groups { get; set; }

    public virtual DbSet<GroupMember> GroupMembers { get; set; }

    public virtual DbSet<Friend> Friends { get; set; }

    public virtual DbSet<Match> Matches { get; set; }

    public virtual DbSet<MatchDate> MatchDates { get; set; }

    public virtual DbSet<Nationality> Nationalities { get; set; }

    public virtual DbSet<Person> People { get; set; }

    public virtual DbSet<Place> Places { get; set; }

    public virtual DbSet<Player> Players { get; set; }

    public virtual DbSet<PlayerForMatch> PlayerForMatches { get; set; }

    public virtual DbSet<Subscription> Subscriptions { get; set; }

    public virtual DbSet<Team> Teams { get; set; }

    public virtual DbSet<TeamMatch> TeamMatches { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserMatch> UserMatches { get; set; }

    public virtual DbSet<UserPlayerForMatch> UserPlayerForMatches { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<Calendar>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Calendar_pkey");

            entity.ToTable("Calendar");

            entity.HasIndex(e => e.CompetitionId, "Calendar_Idx_CompetitionId");

            entity.HasIndex(e => e.NormalizedName, "Calendar_Idx_NormalizedName");

            entity.HasIndex(e => e.OcId, "Calendar_Idx_OcId");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.CompetitionId).HasMaxLength(50);
            entity.Property(e => e.EndDate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.LastUpdated).HasColumnType("timestamp without time zone");
            entity.Property(e => e.Name).HasMaxLength(250);
            entity.Property(e => e.NormalizedName).HasMaxLength(250);
            entity.Property(e => e.OcId).HasMaxLength(50);
            entity.Property(e => e.StartDate).HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Competition).WithMany(p => p.Calendars)
                .HasForeignKey(d => d.CompetitionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("foreign_key01");
        });

        modelBuilder.Entity<Competition>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Competition_pkey");

            entity.ToTable("Competition");

            entity.HasIndex(e => e.CountryId, "Competition_Idx_CountryId");

            entity.HasIndex(e => e.NormalizedCompetitionCode, "Competition_Idx_NormalizedCompetitionCode");

            entity.HasIndex(e => e.NormalizedCompetitionFormat, "Competition_Idx_NormalizedCompetitionFormat");

            entity.HasIndex(e => e.NormalizedCompetitionType, "Competition_Idx_NormalizedCompetitionType");

            entity.HasIndex(e => e.NormalizedName, "Competition_Idx_NormalizedName");

            entity.HasIndex(e => e.NormalizedType, "Competition_Idx_NormalizedType");

            entity.HasIndex(e => e.OcId, "Competition_Idx_OcId");

            entity.HasIndex(e => e.OpId, "Competition_Idx_OpId");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.CompetitionCode).HasMaxLength(50);
            entity.Property(e => e.CompetitionFormat).HasMaxLength(50);
            entity.Property(e => e.CompetitionType).HasMaxLength(50);
            entity.Property(e => e.CountryId).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(250);
            entity.Property(e => e.NormalizedCompetitionCode).HasMaxLength(50);
            entity.Property(e => e.NormalizedCompetitionFormat).HasMaxLength(50);
            entity.Property(e => e.NormalizedCompetitionType).HasMaxLength(50);
            entity.Property(e => e.NormalizedName).HasMaxLength(250);
            entity.Property(e => e.NormalizedType).HasMaxLength(50);
            entity.Property(e => e.OcId).HasMaxLength(50);
            entity.Property(e => e.OpId).HasMaxLength(50);
            entity.Property(e => e.Type).HasMaxLength(50);

            entity.HasOne(d => d.Country).WithMany(p => p.Competitions)
                .HasForeignKey(d => d.CountryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("foreign_key01");
        });

        modelBuilder.Entity<Country>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Country_pkey");

            entity.ToTable("Country");

            entity.HasIndex(e => e.Code, "Country_Idx_Code");

            entity.HasIndex(e => e.NormalizedName, "Country_Idx_NormalizedName");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Code).HasMaxLength(10);
            entity.Property(e => e.Name).HasMaxLength(250);
            entity.Property(e => e.NormalizedName).HasMaxLength(250);
        });

        modelBuilder.Entity<Follower>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("follower_pkey");

            entity.ToTable("Follower");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.FollowerNavigation).WithMany(p => p.FollowerFollowerNavigations)
                .HasForeignKey(d => d.FollowerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("foreign_key02");

            entity.HasOne(d => d.User).WithMany(p => p.FollowerUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("foreign_key01");
        });

        modelBuilder.Entity<Friend>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("friend_pkey");

            entity.ToTable("Friend");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.User0).WithMany(p => p.FriendUser0s)
                .HasForeignKey(d => d.User0Id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("foreign_key01");

            entity.HasOne(d => d.User1).WithMany(p => p.FriendUser1s)
                .HasForeignKey(d => d.User1Id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("foreign_key02");
        });

        modelBuilder.Entity<Match>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Match_pkey");

            entity.ToTable("Match");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.CoverageLevel).HasMaxLength(50);
            entity.Property(e => e.DateTime).HasColumnType("timestamp without time zone");
            entity.Property(e => e.PlaceId).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.Winner).HasMaxLength(30);

            entity.HasOne(d => d.AwayTeam).WithMany(p => p.MatchAwayTeams)
                .HasForeignKey(d => d.AwayTeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("foreign_key01");

            entity.HasOne(d => d.HomeTeam).WithMany(p => p.MatchHomeTeams)
                .HasForeignKey(d => d.HomeTeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("foreign_key02");

            entity.HasOne(d => d.MatchDate).WithMany(p => p.Matches)
                .HasForeignKey(d => d.MatchDateId)
                .HasConstraintName("foreign_key03");

            entity.HasOne(d => d.Place).WithMany(p => p.Matches)
                .HasForeignKey(d => d.PlaceId)
                .HasConstraintName("foreign_key04");
        });

        modelBuilder.Entity<MatchDate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("matchdate_pkey");

            entity.ToTable("MatchDate");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CalendarId).HasMaxLength(50);
            entity.Property(e => e.Date).HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Calendar).WithMany(p => p.MatchDates)
                .HasForeignKey(d => d.CalendarId)
                .HasConstraintName("foreign_key01");
        });

        modelBuilder.Entity<Nationality>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("nationality_pkey");

            entity.ToTable("Nationality");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(250);
            entity.Property(e => e.NormalizedName).HasMaxLength(250);
        });

        modelBuilder.Entity<Person>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Person_pkey");

            entity.ToTable("Person");

            entity.HasIndex(e => e.NormalizedFirstName, "Person_Idx_NormalizedFirstName");

            entity.HasIndex(e => e.NormalizedKnownName, "Person_Idx_NormalizedKnownName");

            entity.HasIndex(e => e.NormalizedLastName, "Person_Idx_NormalizedLastName");

            entity.HasIndex(e => e.NormalizedMatchName, "Person_Idx_NormalizedMatchName");

            entity.HasIndex(e => e.NormalizedMiddleName, "Person_Idx_NormalizedMiddleName");

            entity.HasIndex(e => e.NormalizedShortFirstName, "Person_Idx_NormalizedShortFirstName");

            entity.HasIndex(e => e.NormalizedShortLastName, "Person_Idx_NormalizedShortLastName");

            entity.HasIndex(e => e.OcId, "Person_Idx_OcId");

            entity.HasIndex(e => e.OpId, "Person_Idx_OpId");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.CountryOfBirthId).HasMaxLength(50);
            entity.Property(e => e.DateOfBirth).HasColumnType("timestamp without time zone");
            entity.Property(e => e.DateOfDeath).HasColumnType("timestamp without time zone");
            entity.Property(e => e.FirstName).HasMaxLength(250);
            entity.Property(e => e.Foot).HasMaxLength(50);
            entity.Property(e => e.KnownName).HasMaxLength(250);
            entity.Property(e => e.LastName).HasMaxLength(250);
            entity.Property(e => e.MatchName).HasMaxLength(250);
            entity.Property(e => e.MiddleName).HasMaxLength(250);
            entity.Property(e => e.NationalityId).HasMaxLength(50);
            entity.Property(e => e.NormalizedFirstName).HasMaxLength(250);
            entity.Property(e => e.NormalizedKnownName).HasMaxLength(250);
            entity.Property(e => e.NormalizedLastName).HasMaxLength(250);
            entity.Property(e => e.NormalizedMatchName).HasMaxLength(250);
            entity.Property(e => e.NormalizedMiddleName).HasMaxLength(250);
            entity.Property(e => e.NormalizedShortFirstName).HasMaxLength(250);
            entity.Property(e => e.NormalizedShortLastName).HasMaxLength(250);
            entity.Property(e => e.OcId).HasMaxLength(50);
            entity.Property(e => e.OpId).HasMaxLength(50);
            entity.Property(e => e.PlaceOfBirth).HasMaxLength(250);
            entity.Property(e => e.ShortFirstName).HasMaxLength(250);
            entity.Property(e => e.ShortLastName).HasMaxLength(250);

            entity.HasOne(d => d.CountryOfBirth).WithMany(p => p.People)
                .HasForeignKey(d => d.CountryOfBirthId)
                .HasConstraintName("foreign_key02");

            entity.HasOne(d => d.Nationality).WithMany(p => p.People)
                .HasForeignKey(d => d.NationalityId)
                .HasConstraintName("foreign_key01");
        });

        modelBuilder.Entity<Place>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("place_pkey");

            entity.ToTable("Place");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(250);
            entity.Property(e => e.NormalizedName).HasMaxLength(250);
            entity.Property(e => e.NormalizedShortName).HasMaxLength(250);
            entity.Property(e => e.ShortName).HasMaxLength(250);
        });

        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Player_pkey");

            entity.ToTable("Player");

            entity.Property(e => e.PositionOrder).HasDefaultValue(0);

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.PersonId).HasMaxLength(50);
            entity.Property(e => e.Position).HasMaxLength(50);
            entity.Property(e => e.TeamId).HasMaxLength(50);
            entity.Property(e => e.Type).HasMaxLength(50);

            entity.HasOne(d => d.Person).WithMany(p => p.Players)
                .HasForeignKey(d => d.PersonId)
                .HasConstraintName("foreign_key01");

            entity.HasOne(d => d.Team).WithMany(p => p.Players)
                .HasForeignKey(d => d.TeamId)
                .HasConstraintName("foreign_key02");
        });

        modelBuilder.Entity<PlayerForMatch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("playerselectedformatch_pkey");

            entity.ToTable("PlayerForMatch");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("CURRENT_DATE")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.FormationPlace).HasMaxLength(30);
            entity.Property(e => e.PersonId).HasMaxLength(50);
            entity.Property(e => e.Position).HasMaxLength(50);
            entity.Property(e => e.PositionSide).HasMaxLength(50);

            entity.HasOne(d => d.Person).WithMany(p => p.PlayerForMatches)
                .HasForeignKey(d => d.PersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("foreign_key02");

            entity.HasOne(d => d.TeamMatch).WithMany(p => p.PlayerForMatches)
                .HasForeignKey(d => d.TeamMatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("foreign_key01");
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Subscription_pkey");

            entity.ToTable("Subscription");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.Auth).HasMaxLength(255);
            entity.Property(e => e.ExpirationTime)
                .HasPrecision(6)
                .HasDefaultValueSql("now()");
            entity.Property(e => e.P256dh).HasMaxLength(255);

            entity.HasOne(d => d.User).WithMany(p => p.Subscriptions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("UserId");
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Team_pkey");

            entity.ToTable("Team");

            entity.HasIndex(e => e.NormalizedName, "Player_Idx_NormalizedName");

            entity.HasIndex(e => e.NormalizedOfficialName, "Player_Idx_NormalizedOfficialName");

            entity.HasIndex(e => e.NormalizedShortName, "Player_Idx_NormalizedShortName");

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.AddressZip).HasMaxLength(250);
            entity.Property(e => e.City).HasMaxLength(250);
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.CountryId).HasMaxLength(50);
            entity.Property(e => e.Founded).HasMaxLength(50);
            entity.Property(e => e.LastUpdated).HasColumnType("timestamp without time zone");
            entity.Property(e => e.Name).HasMaxLength(250);
            entity.Property(e => e.NormalizedName).HasMaxLength(250);
            entity.Property(e => e.NormalizedOfficialName).HasMaxLength(250);
            entity.Property(e => e.NormalizedShortName).HasMaxLength(250);
            entity.Property(e => e.OfficialName).HasMaxLength(250);
            entity.Property(e => e.PostalAddress).HasMaxLength(250);
            entity.Property(e => e.ShortName).HasMaxLength(250);
            entity.Property(e => e.TeamType).HasMaxLength(50);
            entity.Property(e => e.Type).HasMaxLength(50);

            entity.HasOne(d => d.Country).WithMany(p => p.Teams)
                .HasForeignKey(d => d.CountryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("foreign_key01");
        });

        modelBuilder.Entity<TeamMatch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("TeamMatch_pkey");

            entity.ToTable("TeamMatch");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("CURRENT_DATE")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.FormatType).HasMaxLength(10);
            entity.Property(e => e.TeamId).HasMaxLength(50);

            entity.HasOne(d => d.Team).WithMany(p => p.TeamMatches)
                .HasForeignKey(d => d.TeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("foreign_key01");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_pkey");

            entity.ToTable("User");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.DisplayName).HasMaxLength(250);
            entity.Property(e => e.NormalizedDisplayName).HasMaxLength(250);
            entity.Property(e => e.Email).HasMaxLength(320);
            entity.Property(e => e.EmailOptIn).HasDefaultValue(false);
        });

        modelBuilder.Entity<UserMatch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("usermatch_pkey");

            entity.ToTable("UserMatch");

            entity.HasIndex(e => new { e.MatchId, e.TeamId, e.UserId }, "UserMatch_idx_matchid_teamid_userid01").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.MatchId).HasMaxLength(50);
            entity.Property(e => e.TeamId).HasMaxLength(50);

            entity.HasOne(d => d.Match).WithMany(p => p.UserMatches)
                .HasForeignKey(d => d.MatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("foreign_key01");

            entity.HasOne(d => d.User).WithMany(p => p.UserMatches)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("foreign_key02");
        });

        modelBuilder.Entity<UserPlayerForMatch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("userplayerformatch_pkey");

            entity.ToTable("UserPlayerForMatch");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.PersonId).HasMaxLength(50);

            entity.HasOne(d => d.Person).WithMany(p => p.UserPlayerForMatches)
                .HasForeignKey(d => d.PersonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("foreign_key01");

            entity.HasOne(d => d.UserMatch).WithMany(p => p.UserPlayerForMatches)
                .HasForeignKey(d => d.UserMatchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("foreign_key02");
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("group_pkey");

            entity.ToTable("Group");

            entity.HasIndex(e => e.InviteCode, "Group_idx_invitecode01").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.InviteCode).HasMaxLength(10);
            entity.Property(e => e.Type).HasMaxLength(20);
            entity.Property(e => e.CreatedDate).HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Creator).WithMany()
                .HasForeignKey(d => d.CreatorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("group_foreign_key01");
        });

        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("groupmember_pkey");

            entity.ToTable("GroupMember");

            entity.HasIndex(e => new { e.GroupId, e.UserId }, "GroupMember_idx_groupid_userid01").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.DateJoined).HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Group).WithMany(p => p.Members)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("groupmember_foreign_key01");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("groupmember_foreign_key02");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
