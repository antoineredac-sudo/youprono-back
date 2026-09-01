using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

public partial class UserMatch
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string MatchId { get; set; } = null!;

    public DateTime DateCreated { get; set; }

    public string TeamId { get; set; } = null!;

    public double PreTeamPossession { get; set; }

    public int PreTeamShots { get; set; }

    public int PreTeamFouls { get; set; }

    public int PreTeamCrosses { get; set; }

    public int PreTeamScore { get; set; }

    public double PreOpponentPossession { get; set; }

    public int PreOpponentShots { get; set; }

    public int PreOpponentFouls { get; set; }

    public int PreOpponentCrosses { get; set; }

    public int PreOpponentScore { get; set; }

    public double? ResultTeamPossessionValue { get; set; }

    public double? ResultTeamPossessionFormula { get; set; }

    public double? ResultTeamPossessionTotal { get; set; }

    public double? ResultTeamShotsValue { get; set; }

    public double? ResultTeamShotsFormula { get; set; }

    public double? ResultTeamShotsTotal { get; set; }

    public double? ResultTeamFoulsValue { get; set; }

    public double? ResultTeamFoulsFormula { get; set; }

    public double? ResultTeamFoulsTotal { get; set; }

    public double? ResultTeamCrossesValue { get; set; }

    public double? ResultTeamCrossesFormula { get; set; }

    public double? ResultTeamCrossesTotal { get; set; }

    public double? ResultTeamScoreValue { get; set; }

    public double? ResultTeamScoreFormula { get; set; }

    public double? ResultTeamScoreTotal { get; set; }

    public double? ResultOpponentCrossesValue { get; set; }

    public double? ResultOpponentCrossesFormula { get; set; }

    public double? ResultOpponentCrossesTotal { get; set; }

    public double? ResultOpponentScoreValue { get; set; }

    public double? ResultOpponentScoreFormula { get; set; }

    public double? ResultOpponentScoreTotal { get; set; }

    public double? ResultOpponentPossessionValue { get; set; }

    public double? ResultOpponentPossessionFormula { get; set; }

    public double? ResultOpponentPossessionTotal { get; set; }

    public double? ResultOpponentShotsValue { get; set; }

    public double? ResultOpponentShotsFormula { get; set; }

    public double? ResultOpponentShotsTotal { get; set; }

    public double? ResultOpponentFoulsValue { get; set; }

    public double? ResultOpponentFoulsFormula { get; set; }

    public double? ResultOpponentFoulsTotal { get; set; }

    public double? ResultCumulationPossession { get; set; }

    public double? ResultCumulationShots { get; set; }

    public double? ResultCumulationFouls { get; set; }

    public double? ResultCumulationCrosses { get; set; }

    public double? ResultCumulationScore { get; set; }

    public double? ResultTeamCompositionValue { get; set; }

    public double? ResultTeamCompositionFormula { get; set; }

    public double? ResultTeamCompositionTotal { get; set; }

    public double? ResultTotal { get; set; }

    public double? ResultFinalTotal { get; set; }

    public double? ResultBonus { get; set; }

    public virtual Match Match { get; set; } = null!;

    public virtual User User { get; set; } = null!;

    public virtual ICollection<UserPlayerForMatch> UserPlayerForMatches { get; } = new List<UserPlayerForMatch>();
}
