using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.thegoldenfan.Services.Opta;
using dotnet.core.utils;
using dotnet.core.utils.server.Helpers;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.FileSystemGlobbing;
using Npgsql.Internal.TypeHandlers.NetworkHandlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.Services
{
    public class CalculResult
    {
        public double Value { get; set; }
        public double Formula { get; set; }
        public double Total { get; set; }
    }

    public class StatsResult
    {
        public CalculResult Possession { get; set; } = new();
        public CalculResult Shots { get; set; } = new();
        public CalculResult Fouls { get; set; } = new();
        public CalculResult Crosses { get; set; } = new();
        public CalculResult Score { get; set; } = new();
        public CalculResult Composition { get; set; } = new();
        public double Total { get; set; }
        public double Final { get; set; }
    }
    public class CumulationResult
    {
        public double Possession { get; set; }
        public double Shots { get; set; }
        public double Fouls { get; set; }
        public double Crosses { get; set; }
        public double Score { get; set; }
    }
    public class UserMatchResult
    {
        public int Rank { get; set; }
        public StatsResult Team { get; set; } = new();
        public StatsResult Opponent { get; set; } = new();
        public CumulationResult Cumulation { get; set; } = new();
    }
    public class UserStatsResult
    {
        public Guid UserId { get; set; }
        public int Nblayer { get; set; }
        public BaseMatchResult MatchResult { get; set; } = new();
        public UserMatchResult Result { get; set; } = new();
    }

    public class UserStatsService
    {
        public static Dictionary<string, double[]> _COEF_ { get; set; } = new Dictionary<string, double[]>()
        {
            { "POSSESSION", new [ ]{ 5.3, 1, 1.42 } },
            { "SHOTS", new [ ]{ 5.1, 5, 1.16 } },
            { "FOULS", new [ ]{ 4.9, 5, 1.20 } },
            { "CROSSES", new [ ]{ 4.7, 5, 1.19 } },
            { "SCORE", new [ ]{ 1, 1, 2.23 } },
            { "COMPOSITION", new [ ]{ 1, 1, 4.05 } }
        };
        public static List<List<double>> _ScoreDifference_ { get; set; } = new List<List<double>>()
        {
            new List<double>(){ 0, 0, 30, 25, 20, 15, 10, 5, 0 },
            new List<double>(){ 0, 55, 50, 45, 40 },
            new List<double>(){ 85, 80, 75, 70, 65 }
        };


        private readonly AppDbContext dbContext;
        private readonly MatchService matchService;
        private readonly UserMatchService userMatchService;
        private readonly UserService userService;
        private readonly OptaMatchStatsService optaMatchStatsService;


        public UserStatsService(AppDbContext dbContext, MatchService matchService, UserMatchService userMatchService, UserService userService, OptaMatchStatsService optaMatchStatsService)
        {
            this.dbContext = dbContext;
            this.matchService = matchService;
            this.userMatchService = userMatchService;
            this.userService = userService;
            this.optaMatchStatsService = optaMatchStatsService;
        }


        private CalculResult CalculateValue(double val0, double val1, double[] coef)
        {
            var res = new CalculResult()
            {
                Value = Math.Round(100 - (Math.Abs(val0 - val1) * coef[0]), 4),
            };
            //res.Total = res.Value * coef[1];
            return res;
        }
        private List<CalculResult> CalculatePossession(TeamResult selectedTeam, TeamResult opponentTeam, UserPredictionModel userPrediction)
        {
            List<CalculResult> res = new();
            var coef = _COEF_["POSSESSION"];
            var team = CalculateValue(selectedTeam.Possession, userPrediction.Team.Possession, coef);
            var opponent = CalculateValue(opponentTeam.Possession, userPrediction.Opponent.Possession, coef);
            team.Formula = Math.Round(team.Value, 4);
            team.Total = Math.Round(team.Formula * coef[2], 4);
            opponent.Formula = Math.Round(opponent.Value, 4);
            opponent.Total = Math.Round(opponent.Formula * coef[2], 4);
            res.Add(team);
            res.Add(opponent);
            return res;
        }
        private List<CalculResult> CalculateShots(TeamResult selectedTeam, TeamResult opponentTeam, UserPredictionModel userPrediction)
        {
            List<CalculResult> res = new();
            var coef = _COEF_["SHOTS"];
            var team = CalculateValue(selectedTeam.Shots, userPrediction.Team.Shots, coef);
            var opponent = CalculateValue(opponentTeam.Shots, userPrediction.Opponent.Shots, coef);
            var cumulation = CalculateValue(selectedTeam.Shots + opponentTeam.Shots, userPrediction.Team.Shots + userPrediction.Opponent.Shots, coef);
            team.Formula = Math.Round(((team.Value * 2) + (opponent.Value * 2) + cumulation.Value) / 5, 4);
            team.Total = Math.Round(team.Formula * coef[2], 4);
            opponent.Formula = Math.Round(((team.Value * 2) + (opponent.Value * 2) + cumulation.Value) / 5, 4);
            opponent.Total = Math.Round(opponent.Formula * coef[2], 4);
            res.Add(team);
            res.Add(opponent);
            res.Add(cumulation);
            return res;
        }
        private List<CalculResult> CalculateFouls(TeamResult selectedTeam, TeamResult opponentTeam, UserPredictionModel userPrediction)
        {
            List<CalculResult> res = new();
            var coef = _COEF_["FOULS"];
            var team = CalculateValue(selectedTeam.Fouls, userPrediction.Team.Fouls, coef);
            var opponent = CalculateValue(opponentTeam.Fouls, userPrediction.Opponent.Fouls, coef);
            var cumulation = CalculateValue(selectedTeam.Fouls + opponentTeam.Fouls, userPrediction.Team.Fouls + userPrediction.Opponent.Fouls, coef);
            team.Formula = Math.Round(((team.Value * 2) + (opponent.Value * 2) + cumulation.Value) / 5, 4);
            team.Total = Math.Round(team.Formula * coef[2], 4);
            opponent.Formula = Math.Round(((team.Value * 2) + (opponent.Value * 2) + cumulation.Value) / 5, 4);
            opponent.Total = Math.Round(opponent.Formula * coef[2], 4);
            res.Add(team);
            res.Add(opponent);
            res.Add(cumulation);
            return res;
        }
        private List<CalculResult> CalculateCrosses(TeamResult selectedTeam, TeamResult opponentTeam, UserPredictionModel userPrediction)
        {
            List<CalculResult> res = new();
            var coef = _COEF_["CROSSES"];
            var team = CalculateValue(selectedTeam.Crosses, userPrediction.Team.Crosses, coef);
            var opponent = CalculateValue(opponentTeam.Crosses, userPrediction.Opponent.Crosses, coef);
            var cumulation = CalculateValue(selectedTeam.Crosses + opponentTeam.Crosses, userPrediction.Team.Crosses + userPrediction.Opponent.Crosses, coef);
            team.Formula = Math.Round(((team.Value * 2) + (opponent.Value * 2) + cumulation.Value) / 5, 4);
            team.Total = Math.Round(team.Formula * coef[2], 4);
            opponent.Formula = Math.Round(((team.Value * 2) + (opponent.Value * 2) + cumulation.Value) / 5, 4);
            opponent.Total = Math.Round(opponent.Formula * coef[2], 4);
            res.Add(team);
            res.Add(opponent);
            res.Add(cumulation);
            return res;
        }

        private List<CalculResult> CalculateScore(TeamResult selectedTeam, TeamResult opponentTeam, UserPredictionModel userPrediction)
        {
            List<CalculResult> res = new();

            //0: null, +0: win, -0: lose
            var preDelta = userPrediction.Team.Score - userPrediction.Opponent.Score;
            var realDelta = selectedTeam.Score - opponentTeam.Score;
            double score = 0;

            int i = 0;
            if (preDelta == realDelta || (preDelta > 0 && realDelta > 0) || preDelta < 0 && realDelta < 0)
            {
                if (userPrediction.Team.Score.Equals(selectedTeam.Score) && userPrediction.Opponent.Score.Equals(opponentTeam.Score))
                {
                    score = 100;
                }
                else
                {
                    if (preDelta == 0 && realDelta == 0)
                    {
                        i = userPrediction.Team.Score - selectedTeam.Score;
                    }
                    else
                    {
                        i = Math.Abs((userPrediction.Team.Score - selectedTeam.Score) - (userPrediction.Opponent.Score - opponentTeam.Score));
                    }
                    if (i < 0) { i = 0; }
                    else
                    {
                        if (i > 4) { i = 4; }
                    }
                    score = _ScoreDifference_[2][i];
                }
            }
            else
            {
                if (preDelta == 0 || realDelta == 0)
                {
                    i = Math.Abs((userPrediction.Team.Score - selectedTeam.Score) - (userPrediction.Opponent.Score - opponentTeam.Score));
                    if (i < 1) { i = 1; }
                    else
                    {
                        if (i > 4) { i = 4; }
                    }
                    score = _ScoreDifference_[1][i];
                }
                else
                {
                    i = Math.Abs((userPrediction.Team.Score - selectedTeam.Score) - (userPrediction.Opponent.Score - opponentTeam.Score));
                    if (i < 2) { i = 2; }
                    else
                    {
                        if (i > 7) { i = 7; }
                    }
                    score = _ScoreDifference_[0][i];
                }
            }

            var coef = _COEF_["SCORE"];
            var team = CalculateValue(selectedTeam.Score, userPrediction.Team.Score, coef);
            var opponent = CalculateValue(opponentTeam.Score, userPrediction.Opponent.Score, coef);
            var cumulation = new CalculResult();
            team.Value = 0;
            team.Formula = score;
            team.Total = Math.Round(team.Formula * coef[2], 4);
            cumulation.Value = score;
            res.Add(team);
            res.Add(opponent);
            res.Add(cumulation);
            return res;
        }
        private List<CalculResult> CalculateComposition(TeamResult selectedTeam, TeamResult opponentTeam, UserPredictionModel userPrediction)
        {
            List<CalculResult> res = new();
            var coef = _COEF_["COMPOSITION"];
            int nbSelected = 0;
            var selectedPlayers = selectedTeam.Players;
            var predictedPlayers = userPrediction.Players;

            selectedPlayers.RemoveAll(r => r.Position!=null && r.Position.ToUpper().Equals("SUBSTITUTE"));
            foreach(var player in selectedPlayers)
            {
                var tmp = predictedPlayers.FirstOrDefault(w => w.Equals(player.Id));
                if(tmp != null) { nbSelected++; }
            }
            var team = new CalculResult();
            team.Value = Math.Round(((double)nbSelected / (double)11) * 100, 4);
            team.Formula = Math.Round(team.Value, 4);
            team.Total = Math.Round(team.Formula * coef[2], 4);
            res.Add(team);
            return res;
        }

        private async Task<UserStatsResult> CalculateStatsAsync(Guid userId, string matchId, string teamId)
        {
            var inDb = await userMatchService.GetByUserMatchTeamAsync(userId, matchId, teamId);
            UserPredictionModel model = new UserPredictionModel()
            {
                MatchId = inDb.MatchId,
                UserId = inDb.UserId,
                TeamId = teamId,
                Players = inDb.Players,
                Team = inDb.Team,
                Opponent = inDb.Opponent
            };

            BaseMatchResult matchOfficialStats = await matchService.ByIdAsync(matchId);
            TeamResult selectedTeam = null;
            TeamResult opponentTeam = null;
            if (matchOfficialStats.AwayTeam.Id.Equals(teamId)) { selectedTeam = matchOfficialStats.AwayTeam; opponentTeam = matchOfficialStats.HomeTeam; }
            else { selectedTeam = matchOfficialStats.HomeTeam; opponentTeam = matchOfficialStats.AwayTeam; }

            model.Players = model.Players.OrderBy(ob => ob).ToList();
            var possession = CalculatePossession(selectedTeam, opponentTeam, model);
            var shots = CalculateShots(selectedTeam, opponentTeam, model);
            var fouls = CalculateFouls(selectedTeam, opponentTeam, model);
            var crosses = CalculateCrosses(selectedTeam, opponentTeam, model);
            var score = CalculateScore(selectedTeam, opponentTeam, model);
            var composition = CalculateComposition(selectedTeam, opponentTeam, model);
            UserStatsResult res = new()
            {
                UserId = userId,
                Result = new UserMatchResult()
                {
                    Team = new StatsResult()
                    {
                        Possession = possession.First(),
                        Shots = shots.First(),
                        Fouls = fouls.First(),
                        Crosses = crosses.First(),
                        Score = score.First(),
                        Composition = composition.First()
                    },
                    Opponent = new StatsResult()
                    {
                        Possession = possession[1],
                        Shots = shots[1],
                        Fouls = fouls[1],
                        Crosses = crosses[1],
                        Score = score[1]
                    },
                    Cumulation = new CumulationResult()
                    {
                        Shots = shots.Last().Value,
                        Fouls = fouls.Last().Value,
                        Crosses = crosses.Last().Value,
                        Score = score.Last().Value
                    }
                },
                MatchResult = matchOfficialStats,
                //Prediction = model
            };
            return res;
        }
        public async Task UpdateAsync(Guid userId, string matchId, string teamId)
        {
            string src = "UserStatsService.UpdateAsync";

            var match = await dbContext.Matches.FirstOrDefaultAsync(w => w.Id.Equals(matchId));
            if (match == null) { throw BaseException.NotFound(-1, src); }
            if (match.Status != "Played") {
                var staleEntry = await dbContext.UserMatches.FirstOrDefaultAsync(w => w.UserId.Equals(userId) && w.MatchId.Equals(matchId) && w.TeamId.Equals(teamId));
                if (staleEntry != null && staleEntry.ResultTotal != null) {
                    staleEntry.ResultTotal = null;
                    staleEntry.ResultFinalTotal = null;
                    await dbContext.SaveChangesAsync();
                }
                throw new Exception("Les résultats officiels de ce match n'ont pas encore été saisis.");
            }

            var stats = await CalculateStatsAsync(userId, matchId, teamId);
            var inDb = await dbContext
                .UserMatches
                .FirstOrDefaultAsync(w => w.UserId.Equals(userId) &&
                w.MatchId.Equals(matchId) &&
                w.TeamId.Equals(teamId));
            if (inDb == null) { throw BaseException.NotFound(-1, src); }

            /**********************************/
            var cumulation = stats.Result.Cumulation;
            inDb.ResultCumulationPossession = cumulation.Possession;
            inDb.ResultCumulationShots = cumulation.Shots;
            inDb.ResultCumulationFouls = cumulation.Fouls;
            inDb.ResultCumulationCrosses = cumulation.Crosses;
            inDb.ResultCumulationScore = cumulation.Score;

            /**********************************/
            var opponent = stats.Result.Opponent;
            inDb.ResultOpponentPossessionFormula = opponent.Possession.Formula;
            inDb.ResultOpponentPossessionTotal = opponent.Possession.Total;
            inDb.ResultOpponentPossessionValue = opponent.Possession.Value;
            inDb.ResultOpponentShotsFormula = opponent.Shots.Formula;
            inDb.ResultOpponentShotsTotal = opponent.Shots.Total;
            inDb.ResultOpponentShotsValue = opponent.Shots.Value;
            inDb.ResultOpponentFoulsFormula = opponent.Fouls.Formula;
            inDb.ResultOpponentFoulsTotal = opponent.Fouls.Total;
            inDb.ResultOpponentFoulsValue = opponent.Fouls.Value;
            inDb.ResultOpponentCrossesFormula = opponent.Crosses.Formula;
            inDb.ResultOpponentCrossesTotal = opponent.Crosses.Total;
            inDb.ResultOpponentCrossesValue = opponent.Crosses.Value;
            inDb.ResultOpponentScoreFormula = opponent.Score.Formula;
            inDb.ResultOpponentScoreTotal = opponent.Score.Total;
            inDb.ResultOpponentScoreValue = opponent.Score.Value;

            /**********************************/
            var team = stats.Result.Team;
            inDb.ResultTeamPossessionFormula = team.Possession.Formula;
            inDb.ResultTeamPossessionTotal = team.Possession.Total;
            inDb.ResultTeamPossessionValue = team.Possession.Value;
            inDb.ResultTeamShotsFormula = team.Shots.Formula;
            inDb.ResultTeamShotsTotal = team.Shots.Total;
            inDb.ResultTeamShotsValue = team.Shots.Value;
            inDb.ResultTeamFoulsFormula = team.Fouls.Formula;
            inDb.ResultTeamFoulsTotal = team.Fouls.Total;
            inDb.ResultTeamFoulsValue = team.Fouls.Value;
            inDb.ResultTeamCrossesFormula = team.Crosses.Formula;
            inDb.ResultTeamCrossesTotal = team.Crosses.Total;
            inDb.ResultTeamCrossesValue = team.Crosses.Value;
            inDb.ResultTeamScoreFormula = team.Score.Formula;
            inDb.ResultTeamScoreTotal = team.Score.Total;
            inDb.ResultTeamScoreValue = team.Score.Value;
            inDb.ResultTeamCompositionFormula = team.Composition.Formula;
            inDb.ResultTeamCompositionTotal = team.Composition.Total;
            inDb.ResultTeamCompositionValue = team.Composition.Value;

            var coef = _COEF_.Select(s => s.Value.LastOrDefault()).Sum();
            var val = Math.Round((team.Possession.Total + team.Shots.Total + team.Fouls.Total + team.Crosses.Total + team.Score.Total + team.Composition.Total) / coef, 4);
            inDb.ResultTotal = val;
            inDb.ResultBonus = await userService.AttendanceBonusAsync(userId, teamId);

            var lo = await dbContext.UserMatches.Where(w => w.TeamId.Equals(teamId) && w.UserId.Equals(userId)).ToListAsync();
            lo.RemoveAll(w => w.ResultTotal == null);
            var count = lo.Count;
            var sum = lo.Sum(s => s.ResultTotal);
            var one = lo.FirstOrDefault(w => w.Id.Equals(inDb.Id));
            if (one !=null && one.ResultTotal != val) { sum = sum + val; }
            inDb.ResultFinalTotal = (sum/count) + inDb.ResultBonus;

            dbContext.UserMatches.Update(inDb);
            await dbContext.SaveChangesAsync();
        }

        private async Task<List<UserStatsResult>> CalculateUsersStatsAsync(List<Guid> usersId, string matchId, string teamId)
        {
            List<UserStatsResult> lo = new List<UserStatsResult>();
            BaseMatchResult matchOfficialStats = await matchService.ByIdAsync(matchId);
            foreach (var userId in usersId)
            { 
                var inDb = await userMatchService.GetByUserMatchTeamAsync(userId, matchId, teamId);
                UserPredictionModel model = new UserPredictionModel()
                {
                    MatchId = inDb.MatchId,
                    UserId = inDb.UserId,
                    TeamId = teamId,
                    Players = inDb.Players,
                    Team = inDb.Team,
                    Opponent = inDb.Opponent
                };
                TeamResult selectedTeam = null;
                TeamResult opponentTeam = null;
                if (matchOfficialStats.AwayTeam.Id.Equals(teamId)) { selectedTeam = matchOfficialStats.AwayTeam; opponentTeam = matchOfficialStats.HomeTeam; }
                else { selectedTeam = matchOfficialStats.HomeTeam; opponentTeam = matchOfficialStats.AwayTeam; }

                model.Players = model.Players.OrderBy(ob => ob).ToList();
                var possession = CalculatePossession(selectedTeam, opponentTeam, model);
                var shots = CalculateShots(selectedTeam, opponentTeam, model);
                var fouls = CalculateFouls(selectedTeam, opponentTeam, model);
                var crosses = CalculateCrosses(selectedTeam, opponentTeam, model);
                var score = CalculateScore(selectedTeam, opponentTeam, model);
                var composition = CalculateComposition(selectedTeam, opponentTeam, model);
                UserStatsResult res = new()
                {
                    UserId = userId,
                    Result = new UserMatchResult()
                    {
                        Team = new StatsResult()
                        {
                            Possession = possession.First(),
                            Shots = shots.First(),
                            Fouls = fouls.First(),
                            Crosses = crosses.First(),
                            Score = score.First(),
                            Composition = composition.First()
                        },
                        Opponent = new StatsResult()
                        {
                            Possession = possession[1],
                            Shots = shots[1],
                            Fouls = fouls[1],
                            Crosses = crosses[1],
                            Score = score[1]
                        },
                        Cumulation = new CumulationResult()
                        {
                            Shots = shots.Last().Value,
                            Fouls = fouls.Last().Value,
                            Crosses = crosses.Last().Value,
                            Score = score.Last().Value
                        }
                    },
                    MatchResult = matchOfficialStats,
                    //Prediction = model
                };
                lo.Add(res);
            }
            return lo;
        }
        public async Task UpdateUsersAsync(List<Guid> usersId, string matchId, string teamId)
        {
            string src = "UserStatsService.UpdateUsersAsync";
            var lostats = await CalculateUsersStatsAsync(usersId, matchId, teamId);
            var lo = await dbContext
                .UserMatches
                .Where(w => w.MatchId.Equals(matchId) &&
                w.TeamId.Equals(teamId))
                .ToListAsync();

            List<UserMatch> toUpdate = new List<UserMatch>();
            int i = 0;
            foreach (var stats in lostats)
            {
                var userId = stats.UserId;
                var inDb = lo.FirstOrDefault(w => w.UserId.Equals(userId));
                if (inDb == null) { throw BaseException.NotFound(-1, src); }

                /**********************************/
                var cumulation = stats.Result.Cumulation;
                inDb.ResultCumulationPossession = cumulation.Possession;
                inDb.ResultCumulationShots = cumulation.Shots;
                inDb.ResultCumulationFouls = cumulation.Fouls;
                inDb.ResultCumulationCrosses = cumulation.Crosses;
                inDb.ResultCumulationScore = cumulation.Score;

                /**********************************/
                var opponent = stats.Result.Opponent;
                inDb.ResultOpponentPossessionFormula = opponent.Possession.Formula;
                inDb.ResultOpponentPossessionTotal = opponent.Possession.Total;
                inDb.ResultOpponentPossessionValue = opponent.Possession.Value;
                inDb.ResultOpponentShotsFormula = opponent.Shots.Formula;
                inDb.ResultOpponentShotsTotal = opponent.Shots.Total;
                inDb.ResultOpponentShotsValue = opponent.Shots.Value;
                inDb.ResultOpponentFoulsFormula = opponent.Fouls.Formula;
                inDb.ResultOpponentFoulsTotal = opponent.Fouls.Total;
                inDb.ResultOpponentFoulsValue = opponent.Fouls.Value;
                inDb.ResultOpponentCrossesFormula = opponent.Crosses.Formula;
                inDb.ResultOpponentCrossesTotal = opponent.Crosses.Total;
                inDb.ResultOpponentCrossesValue = opponent.Crosses.Value;
                inDb.ResultOpponentScoreFormula = opponent.Score.Formula;
                inDb.ResultOpponentScoreTotal = opponent.Score.Total;
                inDb.ResultOpponentScoreValue = opponent.Score.Value;

                /**********************************/
                var team = stats.Result.Team;
                inDb.ResultTeamPossessionFormula = team.Possession.Formula;
                inDb.ResultTeamPossessionTotal = team.Possession.Total;
                inDb.ResultTeamPossessionValue = team.Possession.Value;
                inDb.ResultTeamShotsFormula = team.Shots.Formula;
                inDb.ResultTeamShotsTotal = team.Shots.Total;
                inDb.ResultTeamShotsValue = team.Shots.Value;
                inDb.ResultTeamFoulsFormula = team.Fouls.Formula;
                inDb.ResultTeamFoulsTotal = team.Fouls.Total;
                inDb.ResultTeamFoulsValue = team.Fouls.Value;
                inDb.ResultTeamCrossesFormula = team.Crosses.Formula;
                inDb.ResultTeamCrossesTotal = team.Crosses.Total;
                inDb.ResultTeamCrossesValue = team.Crosses.Value;
                inDb.ResultTeamScoreFormula = team.Score.Formula;
                inDb.ResultTeamScoreTotal = team.Score.Total;
                inDb.ResultTeamScoreValue = team.Score.Value;
                inDb.ResultTeamCompositionFormula = team.Composition.Formula;
                inDb.ResultTeamCompositionTotal = team.Composition.Total;
                inDb.ResultTeamCompositionValue = team.Composition.Value;

                var coef = _COEF_.Select(s => s.Value.LastOrDefault()).Sum();
                var val = Math.Round((team.Possession.Total + team.Shots.Total + team.Fouls.Total + team.Crosses.Total + team.Score.Total + team.Composition.Total) / coef, 4);
                inDb.ResultTotal = val;
                inDb.ResultBonus = await userService.AttendanceBonusAsync(userId, teamId);

                //inDb.ResultFinalTotal = val + inDb.ResultBonus;
                var l = await dbContext.UserMatches.Where(w => w.TeamId.Equals(teamId) && w.UserId.Equals(userId)).ToListAsync();
                l.RemoveAll(r => r.ResultTotal == null);
                var count = l.Count;
                var sum = l.Sum(s => s.ResultTotal);
                var one = lo.FirstOrDefault(w => w.Id.Equals(inDb.Id));
                if (one != null && one.ResultTotal != val) { sum = sum + val; }

                inDb.ResultFinalTotal = (sum / count) + inDb.ResultBonus;

                toUpdate.Add(inDb);
            }
            if(toUpdate!=null && toUpdate.Count>0)
            { 
                dbContext.UserMatches.UpdateRange(toUpdate);
                await dbContext.SaveChangesAsync();
            }
        }
        public async Task UpdateAllAsync(string teamId)
        {
            var matchesId = await optaMatchStatsService.UpdatePreviousDbAsync(teamId);
            foreach(var matchId in matchesId)
            {
                var lo = await dbContext
                    .UserMatches
                    .Where(w => w.MatchId.Equals(matchId))
                    .Select(s => s.UserId)
                    .ToListAsync();
                await UpdateUsersAsync(lo, matchId, teamId);
            }
        }
        public async Task UpdateTeamMatchAsync(string teamId, string matchId)
        {
            var lo = await dbContext
                .UserMatches
                .Where(w => w.MatchId.Equals(matchId))
                .Select(s => s.UserId)
                .ToListAsync();
            await UpdateUsersAsync(lo, matchId, teamId);
        }

        private UserStatsResult CreateResultModel(UserMatch model)
        {
            UserStatsResult res = new()
            {
                Result = new UserMatchResult()
                {
                    Team = new StatsResult()
                    {
                        Possession = new CalculResult()
                        {
                            Value = model.ResultTeamPossessionValue.HasValue ? model.ResultTeamPossessionValue.Value : 0,
                            Formula = model.ResultTeamPossessionFormula.HasValue ? model.ResultTeamPossessionFormula.Value : 0,
                            Total = model.ResultTeamPossessionTotal.HasValue ? model.ResultTeamPossessionTotal.Value : 0
                        },
                        Shots = new CalculResult()
                        {
                            Value = model.ResultTeamShotsValue.HasValue ? model.ResultTeamShotsValue.Value : 0,
                            Formula = model.ResultTeamShotsFormula.HasValue ? model.ResultTeamShotsFormula.Value : 0,
                            Total = model.ResultTeamShotsTotal.HasValue ? model.ResultTeamShotsTotal.Value : 0
                        },
                        Fouls = new CalculResult()
                        {
                            Value = model.ResultTeamFoulsValue.HasValue ? model.ResultTeamFoulsValue.Value : 0,
                            Formula = model.ResultTeamFoulsFormula.HasValue ? model.ResultTeamFoulsFormula.Value : 0,
                            Total = model.ResultTeamFoulsTotal.HasValue ? model.ResultTeamFoulsTotal.Value : 0
                        },
                        Crosses = new CalculResult()
                        {
                            Value = model.ResultTeamCrossesValue.HasValue ? model.ResultTeamCrossesValue.Value : 0,
                            Formula = model.ResultTeamCrossesFormula.HasValue ? model.ResultTeamCrossesFormula.Value : 0,
                            Total = model.ResultTeamCrossesTotal.HasValue ? model.ResultTeamCrossesTotal.Value : 0
                        },
                        Score = new CalculResult()
                        {
                            Value = model.ResultTeamScoreValue.HasValue ? model.ResultTeamScoreValue.Value : 0,
                            Formula = model.ResultTeamScoreFormula.HasValue ? model.ResultTeamScoreFormula.Value : 0,
                            Total = model.ResultTeamScoreTotal.HasValue ? model.ResultTeamScoreTotal.Value : 0
                        },
                        Composition = new CalculResult()
                        {
                            Value = model.ResultTeamCompositionValue.HasValue ? model.ResultTeamCompositionValue.Value : 0,
                            Formula = model.ResultTeamCompositionFormula.HasValue ? model.ResultTeamCompositionFormula.Value : 0,
                            Total = model.ResultTeamCompositionTotal.HasValue ? model.ResultTeamCompositionTotal.Value : 0
                        },
                        Total = model.ResultTotal.HasValue ? model.ResultTotal.Value:0,
                        Final = model.ResultFinalTotal.HasValue ? model.ResultFinalTotal.Value : 0,
                    },
                    Opponent = new StatsResult()
                    {
                        Possession = new CalculResult()
                        {
                            Value = model.ResultOpponentPossessionValue.HasValue ? model.ResultOpponentPossessionValue.Value : 0,
                            Formula = model.ResultOpponentPossessionFormula.HasValue ? model.ResultOpponentPossessionFormula.Value : 0,
                            Total = model.ResultOpponentPossessionTotal.HasValue ? model.ResultOpponentPossessionTotal.Value : 0
                        },
                        Shots = new CalculResult()
                        {
                            Value = model.ResultOpponentShotsValue.HasValue ? model.ResultOpponentShotsValue.Value : 0,
                            Formula = model.ResultOpponentShotsValue.HasValue ? model.ResultOpponentShotsValue.Value : 0,
                            Total = model.ResultOpponentShotsValue.HasValue ? model.ResultOpponentShotsValue.Value : 0
                        },
                        Fouls = new CalculResult()
                        {
                            Value = model.ResultOpponentFoulsValue.HasValue ? model.ResultOpponentFoulsValue.Value : 0,
                            Formula = model.ResultOpponentFoulsFormula.HasValue ? model.ResultOpponentFoulsFormula.Value : 0,
                            Total = model.ResultOpponentFoulsTotal.HasValue ? model.ResultOpponentFoulsTotal.Value : 0
                        },
                        Crosses = new CalculResult()
                        {
                            Value = model.ResultOpponentCrossesValue.HasValue ? model.ResultOpponentCrossesValue.Value : 0,
                            Formula = model.ResultOpponentCrossesFormula.HasValue ? model.ResultOpponentCrossesFormula.Value : 0,
                            Total = model.ResultOpponentCrossesTotal.HasValue ? model.ResultOpponentCrossesTotal.Value : 0
                        },
                        Score = new CalculResult()
                        {
                            Value = model.ResultOpponentScoreValue.HasValue ? model.ResultOpponentScoreValue.Value : 0,
                            Formula = model.ResultOpponentScoreFormula.HasValue ? model.ResultOpponentScoreFormula.Value : 0,
                            Total = model.ResultOpponentScoreTotal.HasValue ? model.ResultOpponentScoreTotal.Value : 0
                        }
                    },
                    Cumulation = new CumulationResult()
                    {
                        Shots = model.ResultCumulationShots.HasValue ? model.ResultCumulationShots.Value : 0,
                        Fouls = model.ResultCumulationFouls.HasValue ? model.ResultCumulationFouls.Value : 0,
                        Crosses = model.ResultCumulationCrosses.HasValue ? model.ResultCumulationCrosses.Value : 0,
                        Score = model.ResultCumulationScore.HasValue ? model.ResultCumulationScore.Value : 0
                    }
                },
            };
            return res;
        }
        public async Task<UserStatsResult> ByUserMatchTeamAsync(Guid userId, string matchId, string teamId)
        {
            string src = "UserStatsService.ByUserMatchTeamAsync";
            BaseMatchResult matchOfficialStats = await matchService.ByIdAsync(matchId);
            UserMatch? inDb = await dbContext
                .UserMatches
                .Include(i => i.Match)
                .ThenInclude(i => i.MatchDate)
                .ThenInclude(i => i.Calendar)
                .ThenInclude(i => i.Competition)
                .Include(i => i.Match)
                .ThenInclude(i => i.AwayTeam)
                .ThenInclude(i => i.Team)
                .Include(i => i.Match)
                .ThenInclude(i => i.HomeTeam)
                .ThenInclude(i => i.Team)
                .Include(i => i.UserPlayerForMatches)
                .ThenInclude(i => i.Person)
                .FirstOrDefaultAsync(w => w.UserId.Equals(userId) &&
                    w.MatchId.Equals(matchId) &&
                    w.TeamId.Equals(teamId));
            if (inDb == null) { throw BaseException.NotFound(-1, src); }

            var res = CreateResultModel(inDb);
            var lo = await dbContext
                .UserMatches
                .Where(w => w.MatchId.Equals(matchId))
                .OrderByDescending(ob => ob.ResultTotal)
                .Select(w => w.UserId)
                .ToListAsync();
            res.Nblayer = lo.Count;
            if(lo.Contains(userId)) { res.Result.Rank = lo.FindIndex(w => w.Equals(userId)) + 1; }
            res.MatchResult = matchOfficialStats;
            return res;
        }
        public async Task<UserStatsResult> ByIdAsync(Guid id)
        {
            string src = "UserStatsService.ByIdAsync";
            
            UserMatch? inDb = await dbContext
                .UserMatches
                .Include(i => i.Match)
                .ThenInclude(i => i.MatchDate)
                .ThenInclude(i => i.Calendar)
                .ThenInclude(i => i.Competition)
                .Include(i => i.Match)
                .ThenInclude(i => i.AwayTeam)
                .ThenInclude(i => i.Team)
                .Include(i => i.Match)
                .ThenInclude(i => i.HomeTeam)
                .ThenInclude(i => i.Team)
                .Include(i => i.UserPlayerForMatches)
                .ThenInclude(i => i.Person)
                .FirstOrDefaultAsync(w => w.Id.Equals(id));
            if (inDb == null) { throw BaseException.NotFound(-1, src); }
            
            BaseMatchResult matchOfficialStats = await matchService.ByIdAsync(inDb.MatchId);
            var res = CreateResultModel(inDb);
            var lo = await dbContext
            .UserMatches
                .Where(w => w.MatchId.Equals(inDb.MatchId))
                .OrderBy(ob => ob.ResultTotal)
                .Select(w => w.UserId)
                .ToListAsync();
            res.Nblayer = lo.Count;
            if (lo.Contains(inDb.UserId)) { res.Result.Rank = lo.FindIndex(w => w.Equals(inDb.UserId)); }
            res.MatchResult = matchOfficialStats;
            return res;
        }

        public sealed class UserListStatsResult
        {
            public Guid Id { get; set; }
            public string? MatchId { get; set; }
            public int Rank { get; set; }
            public DateTime Date { get; set; }
            public string Opponent { get; set; }
            public double Score { get; set; }
        }
        public async Task<PaginationModel<UserListStatsResult>> ByUserTeamAsync(Guid userId, string teamId, int page=1, int limit=10)
        {
            PaginationModel<UserListStatsResult> res = null;
            var lo = dbContext
                .UserMatches
                .Include(i => i.Match)
                .ThenInclude(i => i.AwayTeam)
                .ThenInclude(i => i.Team)
                .Include(i => i.Match)
                .ThenInclude(i => i.HomeTeam)
                .ThenInclude(i => i.Team)
                .Where(w => w.UserId.Equals(userId) && w.TeamId.Equals(teamId))
                .OrderByDescending(ob => ob.Match.DateTime);
            PaginationModel<UserMatch> pagination = await PaginationModel<UserMatch>.CreatePageAsync(lo, page, limit);
            res = PaginationModel<UserListStatsResult>.ConvertTo(pagination);
            res.Page = new List<UserListStatsResult>();
            foreach(var item in pagination.Page)
            {
                var newObj = new UserListStatsResult();
                newObj.Id = item.Id;
                newObj.MatchId = item.MatchId;
                newObj.Date = item.Match.DateTime;
                newObj.Opponent = item.Match.AwayTeam.TeamId.Equals(teamId) ? item.Match.HomeTeam.Team.OfficialName : item.Match.AwayTeam.Team.OfficialName;
                newObj.Score = item.ResultTotal.HasValue ? item.ResultTotal.Value : 0;

                var matches = await dbContext
                    .UserMatches
                    .Where(w => w.MatchId.Equals(item.MatchId))
                    .OrderByDescending(ob => ob.ResultTotal)
                    //.GroupBy(ob => ob.UserId)
                    //.Select(s => s.FirstOrDefault())
                    .ToListAsync();
                newObj.Rank = matches.FindIndex(f => f.UserId.Equals(userId));
                newObj.Rank = newObj.Rank + 1;
                res.Page.Add(newObj);
            }


            return res;
        }

        public sealed class GlobalStatsResult
        {
            public int Rank { get; set; } //rank ordered by resultfinaltotal => expert coef
            public double Record { get; set; } //best score (resulttotal)
            public double Bonus { get; set; } //bonus at the moment
            public int MatchesPlayed { get; set; } //pronostics notés du joueur
            public int MatchesTotal { get; set; } //matchs du PSG depuis son premier pronostic
            public double ExpertCoef { get; set; } //best expert coef (resultfinaltotal)
        }

        public async Task<GlobalStatsResult> GlobalStatsAsync(Guid userId, string teamId)
        {
            GlobalStatsResult res = new GlobalStatsResult();
            var matches = await dbContext
                .UserMatches
                .Include(i => i.Match)
                .Where(w => w.TeamId.Equals(teamId) && w.ResultFinalTotal != null && w.ResultTotal != null && w.ResultTotal != 0 && w.ResultFinalTotal != 0)
                .OrderByDescending(ob => ob.ResultFinalTotal)
                .ToListAsync();
            res.Bonus = await userService.AttendanceBonusAsync(userId, teamId);
            var assiduite = await userService.AttendanceAsync(userId, teamId);
            res.MatchesPlayed = assiduite.Played;
            res.MatchesTotal = assiduite.Total;
            if(matches!=null)
            {
                var gbusers = matches.GroupBy(gb => gb.UserId);
                var user = gbusers.FirstOrDefault(w => w.Key.Equals(userId));
                if(user!=null)
                { 
                    var max = user.OrderByDescending(ob => ob.ResultTotal).FirstOrDefault();
                    res.Record = max.ResultTotal.HasValue ? max.ResultTotal.Value : 0;
                }
                Dictionary<Guid, double> avcoef = await userService.ExpertCoefAllAsync(teamId);
                avcoef = avcoef.OrderByDescending(ob => ob.Value).ToDictionary(o=>o.Key, o => o.Value);
                res.ExpertCoef = avcoef.FirstOrDefault(w => w.Key.Equals(userId)).Value;
                res.Rank = avcoef.Keys.ToList().IndexOf(userId) + 1;
            }
            return res;
        }


        public sealed class UserRanking
        {
            public Guid Id { get; set; }
            public string? UserName { get; set; }
            public int Rank { get; set; }
            public double Score { get; set; }
            public int Games { get; set; }
        }
        public async Task<List<UserRanking>> RankingByResultFinalTotalAsync(string teamId)
        {
            List<UserRanking> res = new List<UserRanking>();
            var coefs = await userService.ExpertCoefAllAsync(teamId);
            var gb = await dbContext
                .UserMatches
                .Include(i => i.User)
                .Include(i => i.Match)
                .GroupBy(gb => gb.UserId)
                .ToListAsync();

            foreach (var item in gb)
            {
                if(coefs.ContainsKey(item.Key))
                { 
                    UserRanking obj = new UserRanking()
                    {
                        Id = item.Key,
                        UserName = item.First().User.DisplayName,
                        Score = coefs[item.Key],
                        Games = item.Count()
                    };
                    res.Add(obj);
                }
            }
            res = res.OrderByDescending(ob => ob.Score).ToList();
            int i = 0;
            foreach(var item in res) { item.Rank = i + 1; i++; }
            return res;
        }

        public async Task<List<UserRanking>> RankingByResultTotalAsync(string teamId)
        {
            List<UserRanking> res = new List<UserRanking>();
            var gb = await dbContext
                .UserMatches
                .Include(i => i.User)
                .Include(i => i.Match)
                .GroupBy(gb => gb.UserId)
                .ToListAsync();

            foreach (var item in gb)
            {
                var lo = item.Where(w => w.ResultTotal != null).OrderByDescending(ob => ob.ResultTotal).ToList();
                if(lo!=null && lo.Count>0)
                { 
                    var u = item.FirstOrDefault()?.User;
                    var rt = lo.FirstOrDefault()?.ResultTotal;
                    UserRanking obj = new UserRanking()
                    {
                        Id = item.Key,
                        UserName = u == null ? "" : u.DisplayName,
                        Score = rt.HasValue ? rt.Value : 0,
                        Games = item.Count()
                    };
                    res.Add(obj);
                }
            }
            res = res.OrderByDescending(ob => ob.Score).ToList();

            int i = 0;
            foreach (var item in res) { item.Rank = i + 1; i++; }
            return res;
        }
    }
}
