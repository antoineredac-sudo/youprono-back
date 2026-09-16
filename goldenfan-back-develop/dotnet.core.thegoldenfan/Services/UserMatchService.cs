using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.utils;
using dotnet.core.utils.server.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.Services
{
    public class PredictionValueModel
    {
        public double Possession { get; set; }
        public int Shots { get; set; }
        public int Fouls { get; set; }
        public int Crosses { get; set; }
        public int Score { get; set; }
        //public int YellowCards { get; set; }
        //public int RedCards { get; set; }
        //public int Cards { get; set; }
    }
    public class UserPredictionModel
    {
        public Guid UserId { get; set; }
        public string MatchId { get; set; } = null!;
        public string TeamId { get; set; } = null!;
        public List<string> Players { get; set; } = new();
        public PredictionValueModel Team { get; set; } = new();
        public PredictionValueModel Opponent { get; set; } = new();

        public UserMatch ToDbModel()
        {
            UserMatch newObj = new UserMatch()
            {
                Id = Guid.NewGuid(),
                DateCreated = DateTime.Now,
                MatchId = this.MatchId,
                TeamId = this.TeamId,
                UserId= this.UserId,
                PreTeamPossession = this.Team.Possession,
                PreTeamShots = this.Team.Shots,
                PreTeamFouls = this.Team.Fouls,
                PreTeamCrosses= this.Team.Crosses,
                PreTeamScore= this.Team.Score,
                PreOpponentPossession = this.Opponent.Possession,
                PreOpponentShots = this.Opponent.Shots,
                PreOpponentFouls = this.Opponent.Fouls,
                PreOpponentCrosses = this.Opponent.Crosses,
                PreOpponentScore = this.Opponent.Score
            };
            return newObj;
        }
    }
    public class UserPredictionResult : UserPredictionModel
    {
        public Guid Id { get; set; }
    }

    public class UserMatchService
    {
        private readonly AppDbContext dbContext;

        public UserMatchService(AppDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        private UserPredictionResult CreateResult(UserMatch model)
        {
            UserPredictionResult newObj = new UserPredictionResult()
            {
                Id = model.Id,
                UserId = model.UserId,
                MatchId = model.MatchId,
                TeamId = model.TeamId,
                Players = model.UserPlayerForMatches.Select(s => s.PersonId).ToList(),
                Team = new PredictionValueModel()
                {
                    Possession = model.PreTeamPossession,
                    Shots = model.PreTeamShots,
                    Fouls = model.PreTeamFouls,
                    Crosses = model.PreTeamCrosses,
                    Score = model.PreTeamScore
                },
                Opponent = new PredictionValueModel()
                {
                    Possession = model.PreOpponentPossession,
                    Shots = model.PreOpponentShots,
                    Fouls = model.PreOpponentFouls,
                    Crosses = model.PreOpponentCrosses,
                    Score = model.PreOpponentScore
                }
            };
            return newObj;
        }
        public async Task<UserPredictionResult> GetByUserMatchTeamAsync(Guid userId, string matchId, string teamId)
        {
            string src = "UserMatchService.GetByUserMatchTeamAsync";
            var inDb = await dbContext
                .UserMatches
                .Include(i => i.Match)
                .ThenInclude(i => i.AwayTeam)
                .Include(i => i.Match)
                .ThenInclude(i => i.HomeTeam)
                .Include(i => i.UserPlayerForMatches)
                .ThenInclude(i => i.Person)
                .FirstOrDefaultAsync(w => w.UserId.Equals(userId) &&
                w.MatchId.Equals(matchId) &&
                w.TeamId.Equals(teamId));
            if (inDb == null) { throw BaseException.NotFound(-1, src); }

            var newObj = CreateResult(inDb);
            return newObj;
        }
        public async Task<PaginationModel<UserPredictionResult>> GetByUserAsync(Guid userId, string teamId, int page=1, int limit=10)
        {
            string src = "UserMatchService.GetByUserAsync";
            PaginationModel<UserPredictionResult> res = null;
            IQueryable<UserMatch> inDb = dbContext
                .UserMatches
                .Include(i => i.Match)
                .ThenInclude(i => i.AwayTeam)
                .Include(i => i.Match)
                .ThenInclude(i => i.HomeTeam)
                .Include(i => i.UserPlayerForMatches)
                .ThenInclude(i => i.Person)
                .Where(w => w.UserId.Equals(userId) &&
                w.TeamId.Equals(teamId));
            if (inDb == null) { throw BaseException.NotFound(-1, src); }

            var tmp = await PaginationModel<UserMatch>.CreatePageAsync(inDb, page, limit);
            res = PaginationModel<UserPredictionResult>.ConvertTo(tmp);
            res.Page = new List<UserPredictionResult>();
            foreach (var item in tmp.Page)
            {
                var newObj = CreateResult(item);
                res.Page.Add(newObj);
            }
            return res;
        }
        public async Task<UserPredictionResult> GetByIdAsync(Guid id)
        {
            string src = "UserMatchService.GetByIdAsync";
            var inDb = await dbContext
                .UserMatches
                .Include(i=>i.Match)
                .ThenInclude(i=>i.AwayTeam)
                .Include(i => i.Match)
                .ThenInclude(i => i.HomeTeam)
                .Include(i => i.UserPlayerForMatches)
                .ThenInclude(i => i.Person)
                .FirstOrDefaultAsync(w => w.Id.Equals(id));
            if (inDb == null) { throw BaseException.NotFound(-1, src); }

            var newObj = CreateResult(inDb);
            return newObj;
        }

        // Le verrou de clôture, côté serveur. Jusqu'ici seul le site fermait les
        // pronostics : n'importe qui pouvait passer par Swagger pour créer, modifier
        // ou effacer un pronostic après la clôture. La clôture tombe
        // GroupService.ClotureAvantHeures avant le coup d'envoi, heure de Paris,
        // comme partout ailleurs. Un match déjà noté (« Played ») est fermé quoi
        // qu'il arrive. Code d'erreur -10 : « pronostics fermés ».
        private async Task VerifierClotureAsync(string matchId, string src)
        {
            var match = await dbContext
                .Matches
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id.Equals(matchId));
            if (match == null) { throw BaseException.NotFound(-9, src); }

            if (!string.IsNullOrEmpty(match.Status) &&
                match.Status.Equals("Played", StringComparison.OrdinalIgnoreCase))
            { throw BaseException.InvalidModel(-10, src); }

            DateTime clotureUtc = GroupService.ParisToUtc(
                match.DateTime.AddHours(-GroupService.ClotureAvantHeures));
            if (DateTime.UtcNow >= clotureUtc) { throw BaseException.InvalidModel(-10, src); }
        }

        public async Task<UserPredictionModel> CreateAsync(UserPredictionModel model)
        {
            string src = "UserMatchService.CreateAsync";
            await VerifierClotureAsync(model.MatchId, src);
            var inDb = await dbContext
                .UserMatches
                .FirstOrDefaultAsync(w => w.UserId.Equals(model.UserId) &&
                w.MatchId.Equals(model.MatchId) &&
                w.TeamId.Equals(model.TeamId));
            if (inDb != null) { throw BaseException.AlreadyInDb(-1, src); }

            var newObj = model.ToDbModel();
            await dbContext.UserMatches.AddAsync(newObj);

            var newPlayers = model
                .Players
                .Select(s =>
                new UserPlayerForMatch()
                {
                    Id = Guid.NewGuid(),
                    PersonId = s,
                    UserMatchId = newObj.Id
                })
                .ToList();
            await dbContext.UserPlayerForMatches.AddRangeAsync(newPlayers);
            await dbContext.SaveChangesAsync();

            return model;
        }

        public async Task<UserPredictionModel> UpdateAsync(UserPredictionModel model)
        {
            string src = "UserMatchService.UpdateAsync";
            await VerifierClotureAsync(model.MatchId, src);
            var inDb = await dbContext
                .UserMatches
                .Include(i => i.UserPlayerForMatches)
                .FirstOrDefaultAsync(w => w.UserId.Equals(model.UserId) &&
                w.MatchId.Equals(model.MatchId) &&
                w.TeamId.Equals(model.TeamId));
            if (inDb == null) { throw BaseException.NotFound(-1, src); }

            inDb.PreTeamPossession = model.Team.Possession;
            inDb.PreTeamShots = model.Team.Shots;
            inDb.PreTeamFouls = model.Team.Fouls;
            inDb.PreTeamCrosses = model.Team.Crosses;
            inDb.PreTeamScore = model.Team.Score;

            inDb.PreOpponentPossession = model.Opponent.Possession;
            inDb.PreOpponentShots = model.Opponent.Shots;
            inDb.PreOpponentFouls = model.Opponent.Fouls;
            inDb.PreOpponentCrosses = model.Opponent.Crosses;
            inDb.PreOpponentScore = model.Opponent.Score;

            var newPlayers = model
                .Players
                .Select(s =>
                    new UserPlayerForMatch()
                    {
                        Id = Guid.NewGuid(),
                        PersonId = s,
                        UserMatchId = inDb.Id
                    })
                .ToList();

            dbContext.UserMatches.Update(inDb);
            dbContext.UserPlayerForMatches.RemoveRange(inDb.UserPlayerForMatches);
            await dbContext.UserPlayerForMatches.AddRangeAsync(newPlayers);
            await dbContext.SaveChangesAsync();

            return model;
        }

        public async Task<List<Guid>> DeleteAsync(List<Guid> ids)
        {
            string src = "MatchService.DeleteAsync";
            var lo = await dbContext
                .UserMatches
                .Include(i => i.UserPlayerForMatches)
                .Where(w => ids.Any(a => w.Id.Equals(a)))
                .ToListAsync();
            if (lo == null) { throw BaseException.NotFound(-1, src); }

            // Effacer un pronostic après la clôture est aussi une triche :
            // on ferait disparaître une mauvaise note avant la notation.
            foreach (var item in lo)
            {
                await VerifierClotureAsync(item.MatchId, src);
            }

            if (lo.Count > 0)
            {
                foreach (var item in lo)
                {
                    dbContext.UserPlayerForMatches.RemoveRange(item.UserPlayerForMatches);
                    dbContext.UserMatches.Remove(item);
                }
                await dbContext.SaveChangesAsync();
            }
            return ids;
        }

        public async Task ReplaceMatchAsync(string match0Id, string match1Id)
        {
            var lo = await dbContext
                .UserMatches
                .Where(w=>w.MatchId.Equals(match0Id))
                .ToListAsync();

            if(lo!=null && lo.Count>0)
            {
                lo.ForEach(w => w.MatchId = match1Id);
                dbContext.UpdateRange(lo);
                await dbContext.SaveChangesAsync();
            }
        }

        // ===== LA TENDANCE DES PRONOS (16 septembre 2026) =====
        // Pour le fondateur, qui veut partager sur X la répartition des pronos :
        // combien voient Paris gagner, un nul, l'adversaire gagner, et le score
        // le plus pronostiqué. Données agrégées uniquement, aucun pseudo.
        // Protégée par le même code que la liste des inscrits, parce que le site
        // n'affiche pas encore cette répartition : la montrer aux joueurs avant la
        // clôture reste une décision à prendre.
        // ATTENTION : ce code est écrit en dur ici ET dans UserService.cs. Le jour
        // où il sera déplacé dans une variable de Render, penser aux deux fichiers.
        private const string TendanceAccessCode = "psg2026";

        public sealed class TendanceResult
        {
            public string? MatchId { get; set; }
            public string? Affiche { get; set; }
            public DateTime? CoupDEnvoi { get; set; }
            public bool PronosClos { get; set; }
            public int Pronos { get; set; }
            public int VictoirePsg { get; set; }
            public int Nul { get; set; }
            public int VictoireAdversaire { get; set; }
            public int PourcentVictoirePsg { get; set; }
            public int PourcentNul { get; set; }
            public int PourcentVictoireAdversaire { get; set; }
            public string? ScoreLePlusPronostique { get; set; }
            public int PronosSurCeScore { get; set; }
        }

        private static string NomDuCote(TeamMatch? cote)
        {
            var t = cote?.Team;
            if (t == null) { return ""; }
            if (!string.IsNullOrWhiteSpace(t.OfficialName)) { return t.OfficialName!; }
            if (!string.IsNullOrWhiteSpace(t.Name)) { return t.Name; }
            if (!string.IsNullOrWhiteSpace(t.ShortName)) { return t.ShortName!; }
            return "";
        }

        // Sans matchId : le prochain match de l'équipe. Il le reste jusqu'à trois
        // heures après le coup d'envoi, pour pouvoir relire la répartition le soir
        // même. Avec matchId : n'importe quel match, passé ou à venir.
        public async Task<TendanceResult> TendanceAsync(string accessCode, string teamId, string? matchId)
        {
            string src = "UserMatchService.TendanceAsync";
            if (string.IsNullOrWhiteSpace(accessCode) ||
                !accessCode.Trim().Equals(TendanceAccessCode, StringComparison.OrdinalIgnoreCase))
            { throw BaseException.InvalidModel(-1, src); }

            DateTime maintenantParis = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow, GroupService.ParisTimeZoneInfo);

            var requete = dbContext.Matches
                .Include(i => i.HomeTeam).ThenInclude(t => t.Team)
                .Include(i => i.AwayTeam).ThenInclude(t => t.Team)
                .Where(w => w.HomeTeam.TeamId.Equals(teamId) || w.AwayTeam.TeamId.Equals(teamId));

            Match? match;
            if (string.IsNullOrWhiteSpace(matchId))
            {
                DateTime limite = maintenantParis.AddHours(-3);
                match = await requete
                    .Where(w => w.DateTime > limite)
                    .OrderBy(o => o.DateTime)
                    .FirstOrDefaultAsync();
            }
            else
            {
                match = await requete.FirstOrDefaultAsync(w => w.Id.Equals(matchId));
            }
            if (match == null)
            { throw new BaseException(-2, src, "Aucun match trouvé pour cette équipe."); }

            bool psgRecoit = match.HomeTeam.TeamId.Equals(teamId);
            string domicile = NomDuCote(match.HomeTeam);
            string exterieur = NomDuCote(match.AwayTeam);

            var res = new TendanceResult
            {
                MatchId = match.Id,
                Affiche = domicile + " - " + exterieur,
                CoupDEnvoi = match.DateTime,
                PronosClos = maintenantParis >= match.DateTime.AddHours(-GroupService.ClotureAvantHeures)
            };

            // PreTeamScore est toujours le score prévu pour l'équipe du joueur (le
            // PSG), PreOpponentScore celui de l'adversaire, quel que soit le terrain.
            var scores = await dbContext.UserMatches
                .Where(w => w.MatchId.Equals(match.Id) && w.TeamId.Equals(teamId))
                .Select(s => new { Psg = s.PreTeamScore, Adv = s.PreOpponentScore })
                .ToListAsync();

            res.Pronos = scores.Count;
            if (res.Pronos == 0) { return res; }

            res.VictoirePsg = scores.Count(c => c.Psg > c.Adv);
            res.Nul = scores.Count(c => c.Psg == c.Adv);
            res.VictoireAdversaire = scores.Count(c => c.Psg < c.Adv);

            // Arrondi à l'unité : la somme peut faire 99 ou 101, les effectifs
            // bruts sont là pour trancher.
            res.PourcentVictoirePsg = (int)Math.Round(100.0 * res.VictoirePsg / res.Pronos, MidpointRounding.AwayFromZero);
            res.PourcentNul = (int)Math.Round(100.0 * res.Nul / res.Pronos, MidpointRounding.AwayFromZero);
            res.PourcentVictoireAdversaire = (int)Math.Round(100.0 * res.VictoireAdversaire / res.Pronos, MidpointRounding.AwayFromZero);

            // Le score le plus pronostiqué, écrit dans l'ordre de l'affiche
            // (domicile d'abord), comme on l'écrirait dans un tweet.
            var top = scores
                .GroupBy(g => new { g.Psg, g.Adv })
                .OrderByDescending(o => o.Count())
                .ThenByDescending(o => o.Key.Psg - o.Key.Adv)
                .First();
            int butsDomicile = psgRecoit ? top.Key.Psg : top.Key.Adv;
            int butsExterieur = psgRecoit ? top.Key.Adv : top.Key.Psg;
            res.ScoreLePlusPronostique = domicile + " " + butsDomicile + " - " + butsExterieur + " " + exterieur;
            res.PronosSurCeScore = top.Count();

            return res;
        }
    }
}
