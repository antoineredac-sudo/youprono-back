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
        // ===== LE DEFI CULTURE CLUB DE LA TREVE (21 septembre 2026) =====
        // Trente questions, trois par jour du mercredi 23 septembre au vendredi
        // 2 octobre. Les enonces, les propositions ET les bonnes reponses vivent
        // ici : le site ne recoit jamais la reponse avant que le joueur ait
        // repondu. Le chrono est tenu par le serveur, de la meme facon — c'est
        // l'ecart entre l'affichage de la question et la reponse qui compte, pas
        // ce que raconte le telephone.

        private const int QUIZ_SECONDES = 15;
        // Une seconde et demie de tolerance : le temps que la reponse traverse le
        // reseau. Sans elle, un joueur qui repond a la quatorzieme seconde sur une
        // connexion lente perdrait ses points sans comprendre pourquoi.
        private const int QUIZ_TOLERANCE_MS = 1500;
        private static readonly DateTime QUIZ_CLOTURE_PARIS = new DateTime(2026, 10, 4, 18, 0, 0);

        private sealed class QuizQuestion
        {
            public string Jour { get; set; } = "";      // "2026-09-23"
            public int Rang { get; set; }               // 1, 2 ou 3
            public string Texte { get; set; } = "";
            public string[] Choix { get; set; } = Array.Empty<string>();
            public int Bonne { get; set; }              // index de 0 a 3
            public string Id => "q" + Jour.Replace("-", "") + "-" + Rang;
            public int Points => Rang;                  // 1 point, 2 points, 3 points
        }

        private static readonly QuizQuestion[] QUIZ = new[]
        {
            new QuizQuestion { Jour = "2026-09-23", Rang = 1, Texte = "En quelle année le PSG a-t-il été fondé ?", Choix = new[] { "1960", "1970", "1974", "1977" }, Bonne = 1 },
            new QuizQuestion { Jour = "2026-09-23", Rang = 2, Texte = "Qui est le meilleur buteur de l'histoire du PSG ?", Choix = new[] { "Zlatan Ibrahimović", "Pauleta", "Edinson Cavani", "Kylian Mbappé" }, Bonne = 3 },
            new QuizQuestion { Jour = "2026-09-23", Rang = 3, Texte = "Combien de matchs Marquinhos a-t-il disputés avec le PSG avant cette trêve ?", Choix = new[] { "529", "512", "487", "428" }, Bonne = 0 },

            new QuizQuestion { Jour = "2026-09-24", Rang = 1, Texte = "Quel gardien champion du monde a rejoint le PSG en 2018, à 40 ans ?", Choix = new[] { "Iker Casillas", "Gianluigi Buffon", "Petr Čech", "Manuel Neuer" }, Bonne = 1 },
            new QuizQuestion { Jour = "2026-09-24", Rang = 2, Texte = "En quelle année le PSG a-t-il été champion de France pour la première fois ?", Choix = new[] { "1982", "1986", "1994", "1996" }, Bonne = 1 },
            new QuizQuestion { Jour = "2026-09-24", Rang = 3, Texte = "Qui a marqué le but de la victoire en finale de la Coupe des coupes 1996 ?", Choix = new[] { "Youri Djorkaeff", "Raí", "Bruno N'Gotty", "Patrice Loko" }, Bonne = 2 },

            new QuizQuestion { Jour = "2026-09-25", Rang = 1, Texte = "Quel attaquant uruguayen a marqué 200 buts avec le PSG ?", Choix = new[] { "Luis Suárez", "Edinson Cavani", "Diego Forlán", "Álvaro Recoba" }, Bonne = 1 },
            new QuizQuestion { Jour = "2026-09-25", Rang = 2, Texte = "Quel entraîneur a mené le PSG au titre de champion de France 1994 ?", Choix = new[] { "Luis Fernandez", "Artur Jorge", "Tomislav Ivić", "Gérard Houllier" }, Bonne = 1 },
            new QuizQuestion { Jour = "2026-09-25", Rang = 3, Texte = "Avant Marquinhos, qui détenait le record de matchs joués avec le PSG ?", Choix = new[] { "Safet Sušić", "Jean-Marc Pilorget", "Paul Le Guen", "Mustapha Dahleb" }, Bonne = 1 },

            new QuizQuestion { Jour = "2026-09-26", Rang = 1, Texte = "Qui entraînait le PSG lors de sa victoire en Ligue des champions 2025 ?", Choix = new[] { "Mauricio Pochettino", "Thomas Tuchel", "Luis Enrique", "Christophe Galtier" }, Bonne = 2 },
            new QuizQuestion { Jour = "2026-09-26", Rang = 2, Texte = "Dans quelle ville le PSG a-t-il disputé sa première finale de Ligue des champions, en 2020 ?", Choix = new[] { "Munich", "Madrid", "Lisbonne", "Porto" }, Bonne = 2 },
            new QuizQuestion { Jour = "2026-09-26", Rang = 3, Texte = "Contre quel club le PSG a-t-il remporté sa première Coupe de France, en 1982 ?", Choix = new[] { "AS Saint-Étienne", "FC Nantes", "Girondins de Bordeaux", "AS Monaco" }, Bonne = 0 },

            new QuizQuestion { Jour = "2026-09-27", Rang = 1, Texte = "Quel ancien attaquant du PSG a reçu le Ballon d'or 1995 ?", Choix = new[] { "David Ginola", "Jean-Pierre Papin", "George Weah", "Raí" }, Bonne = 2 },
            new QuizQuestion { Jour = "2026-09-27", Rang = 2, Texte = "Qui a ouvert le score en finale de la Ligue des champions 2025 contre l'Inter ?", Choix = new[] { "Achraf Hakimi", "Désiré Doué", "Khvicha Kvaratskhelia", "Ousmane Dembélé" }, Bonne = 0 },
            new QuizQuestion { Jour = "2026-09-27", Rang = 3, Texte = "En quelle année le PSG est-il remonté en première division, qu'il n'a plus quittée depuis ?", Choix = new[] { "1971", "1972", "1974", "1978" }, Bonne = 2 },

            new QuizQuestion { Jour = "2026-09-28", Rang = 1, Texte = "Contre quel club le PSG a-t-il perdu la finale de la Ligue des champions 2020 ?", Choix = new[] { "Real Madrid", "Bayern Munich", "Liverpool", "Manchester City" }, Bonne = 1 },
            new QuizQuestion { Jour = "2026-09-28", Rang = 2, Texte = "En quelle année Zlatan Ibrahimović est-il arrivé au PSG ?", Choix = new[] { "2011", "2012", "2013", "2014" }, Bonne = 1 },
            new QuizQuestion { Jour = "2026-09-28", Rang = 3, Texte = "Lors du 4-1 contre le Real Madrid en 1993, qui a marqué de la tête le but de la qualification ?", Choix = new[] { "David Ginola", "George Weah", "Antoine Kombouaré", "Alain Roche" }, Bonne = 2 },

            new QuizQuestion { Jour = "2026-09-29", Rang = 1, Texte = "Quel était le score de la finale de la Ligue des champions 2025 contre l'Inter ?", Choix = new[] { "2-0", "3-1", "4-0", "5-0" }, Bonne = 3 },
            new QuizQuestion { Jour = "2026-09-29", Rang = 2, Texte = "De quel club Pauleta est-il arrivé au PSG en 2003 ?", Choix = new[] { "FC Porto", "Girondins de Bordeaux", "Deportivo La Corogne", "Benfica" }, Bonne = 1 },
            new QuizQuestion { Jour = "2026-09-29", Rang = 3, Texte = "Combien de buts Kylian Mbappé a-t-il marqués avec le PSG, toutes compétitions ?", Choix = new[] { "212", "238", "256", "274" }, Bonne = 2 },

            new QuizQuestion { Jour = "2026-09-30", Rang = 1, Texte = "Qui est le capitaine du PSG depuis le départ de Thiago Silva en 2020 ?", Choix = new[] { "Presnel Kimpembe", "Marquinhos", "Achraf Hakimi", "Vitinha" }, Bonne = 1 },
            new QuizQuestion { Jour = "2026-09-30", Rang = 2, Texte = "De quel club brésilien Ronaldinho est-il arrivé au PSG en 2001 ?", Choix = new[] { "Flamengo", "Santos", "Grêmio", "São Paulo" }, Bonne = 2 },
            new QuizQuestion { Jour = "2026-09-30", Rang = 3, Texte = "Qui entraînait le PSG lors de la victoire en Coupe des coupes 1996 ?", Choix = new[] { "Artur Jorge", "Luis Fernandez", "Ricardo", "Philippe Bergeroo" }, Bonne = 1 },

            new QuizQuestion { Jour = "2026-10-01", Rang = 1, Texte = "Quel entraîneur allemand a mené le PSG à sa première finale de Ligue des champions, en 2020 ?", Choix = new[] { "Jürgen Klopp", "Thomas Tuchel", "Julian Nagelsmann", "Hansi Flick" }, Bonne = 1 },
            new QuizQuestion { Jour = "2026-10-01", Rang = 2, Texte = "Quel était le score de la défaite du PSG à Barcelone en mars 2017 ?", Choix = new[] { "4-0", "5-1", "6-1", "6-2" }, Bonne = 2 },
            new QuizQuestion { Jour = "2026-10-01", Rang = 3, Texte = "Combien de titres de champion de France le PSG comptait-il avant son rachat par QSI en 2011 ?", Choix = new[] { "1", "2", "3", "4" }, Bonne = 1 },

            new QuizQuestion { Jour = "2026-10-02", Rang = 1, Texte = "Qui a pris la présidence du PSG après le rachat par QSI en 2011 ?", Choix = new[] { "Michel Denisot", "Nasser al-Khelaïfi", "Robin Leproux", "Sébastien Bazin" }, Bonne = 1 },
            new QuizQuestion { Jour = "2026-10-02", Rang = 2, Texte = "En quelle année le PSG a-t-il remporté sa première Coupe de la Ligue ?", Choix = new[] { "1993", "1995", "1998", "2008" }, Bonne = 1 },
            new QuizQuestion { Jour = "2026-10-02", Rang = 3, Texte = "Combien de buts Zlatan Ibrahimović a-t-il marqués avec le PSG ?", Choix = new[] { "113", "137", "156", "178" }, Bonne = 2 }
        };

        private static string QuizDifficulte(int rang)
        {
            if (rang == 1) { return "Facile"; }
            if (rang == 2) { return "Moyenne"; }
            return "Difficile";
        }

        private static DateTime QuizMaintenantParis()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GroupService.ParisTimeZoneInfo);
        }

        // Le defi est-il encore ouvert ? Passe le dimanche 4 octobre a 18 h, plus
        // aucune reponse n'est acceptee : le classement est fige, le tirage au sort
        // peut avoir lieu.
        private static bool QuizOuvert()
        {
            return QuizMaintenantParis() < QUIZ_CLOTURE_PARIS;
        }

        // Une question n'existe que le jour dit, a partir de minuit, et le reste
        // ensuite : c'est le rattrapage. Rien ne fuit a l'avance.
        private static bool QuizJourOuvert(string jour)
        {
            DateTime date;
            if (!DateTime.TryParse(jour, out date)) { return false; }
            return QuizMaintenantParis().Date >= date.Date;
        }

        public sealed class QuizQuestionResult
        {
            public string Id { get; set; } = "";
            public string Jour { get; set; } = "";
            public int Rang { get; set; }
            public string Difficulte { get; set; } = "";
            public int Points { get; set; }
            public string Texte { get; set; } = "";
            public List<string> Choix { get; set; } = new();
            public bool Jouee { get; set; }
            // Renseignes seulement une fois la question jouee : avant, le site ne
            // sait rien de la bonne reponse.
            public int? MonChoix { get; set; }
            public int? BonneReponse { get; set; }
            public bool? Correct { get; set; }
            public int? PointsObtenus { get; set; }
            public int? TempsMs { get; set; }
        }

        public sealed class QuizJourResult
        {
            public string Date { get; set; } = "";
            public bool Ouvert { get; set; }
            public bool Complet { get; set; }
            public List<QuizQuestionResult> Questions { get; set; } = new();
        }

        public sealed class QuizEtatResult
        {
            public string Aujourdhui { get; set; } = "";
            public bool DefiOuvert { get; set; }
            public string Cloture { get; set; } = "";
            public string PremierJour { get; set; } = "";
            public string DernierJour { get; set; } = "";
            public int Secondes { get; set; }
            public int PointsTotal { get; set; }
            public int TempsTotalMs { get; set; }
            public int QuestionsJouees { get; set; }
            public int QuestionsOuvertes { get; set; }
            public int EnRetard { get; set; }
            public List<QuizJourResult> Jours { get; set; } = new();
        }

        public async Task<QuizEtatResult> QuizEtatAsync(Guid userId)
        {
            var mesReponses = await dbContext.QuizAnswers
                .Where(w => w.UserId.Equals(userId))
                .ToListAsync();

            var res = new QuizEtatResult
            {
                Aujourdhui = QuizMaintenantParis().ToString("yyyy-MM-dd"),
                DefiOuvert = QuizOuvert(),
                Cloture = QUIZ_CLOTURE_PARIS.ToString("yyyy-MM-ddTHH:mm:ss"),
                PremierJour = QUIZ.First().Jour,
                DernierJour = QUIZ.Last().Jour,
                Secondes = QUIZ_SECONDES
            };

            foreach (var jour in QUIZ.Select(s => s.Jour).Distinct().OrderBy(o => o))
            {
                bool ouvert = QuizJourOuvert(jour);
                var ligne = new QuizJourResult { Date = jour, Ouvert = ouvert };

                foreach (var q in QUIZ.Where(w => w.Jour.Equals(jour)).OrderBy(o => o.Rang))
                {
                    var reponse = mesReponses.FirstOrDefault(f => f.QuestionId.Equals(q.Id) && f.AnsweredAt.HasValue);
                    var item = new QuizQuestionResult
                    {
                        Id = q.Id,
                        Jour = q.Jour,
                        Rang = q.Rang,
                        Difficulte = QuizDifficulte(q.Rang),
                        Points = q.Points,
                        Texte = ouvert ? q.Texte : "",
                        Choix = ouvert ? q.Choix.ToList() : new List<string>(),
                        Jouee = reponse != null
                    };

                    if (reponse != null)
                    {
                        item.MonChoix = reponse.Choice;
                        item.BonneReponse = q.Bonne;
                        item.Correct = reponse.Correct;
                        item.PointsObtenus = reponse.Points;
                        item.TempsMs = reponse.TimeMs;
                        res.QuestionsJouees++;
                        res.PointsTotal += reponse.Points;
                        res.TempsTotalMs += reponse.TimeMs;
                    }
                    else if (ouvert)
                    {
                        res.QuestionsOuvertes++;
                        if (!q.Jour.Equals(res.Aujourdhui)) { res.EnRetard++; }
                    }

                    ligne.Questions.Add(item);
                }

                ligne.Complet = ligne.Questions.All(a => a.Jouee);
                res.Jours.Add(ligne);
            }

            return res;
        }

        public sealed class QuizDepartResult
        {
            public string Id { get; set; } = "";
            public string Difficulte { get; set; } = "";
            public int Points { get; set; }
            public string Texte { get; set; } = "";
            public List<string> Choix { get; set; } = new();
            public int Secondes { get; set; }
        }

        // Le joueur affiche une question : le serveur note l'heure. C'est ce
        // moment-la qui fait foi, et lui seul.
        public async Task<QuizDepartResult> QuizDepartAsync(Guid userId, string questionId)
        {
            string src = "UserMatchService.QuizDepartAsync";

            var q = QUIZ.FirstOrDefault(f => f.Id.Equals(questionId));
            if (q == null) { throw BaseException.NotFound(-2, src); }
            if (!QuizJourOuvert(q.Jour)) { throw new BaseException(-3, src, "Cette question n'est pas encore ouverte."); }
            if (!QuizOuvert()) { throw new BaseException(-4, src, "Le défi est terminé."); }

            var ligne = await dbContext.QuizAnswers
                .FirstOrDefaultAsync(w => w.UserId.Equals(userId) && w.QuestionId.Equals(questionId));

            if (ligne != null && ligne.AnsweredAt.HasValue)
            { throw new BaseException(-5, src, "Tu as déjà répondu à cette question."); }

            if (ligne == null)
            {
                ligne = new Dbs.QuizAnswer
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    QuestionId = questionId,
                    StartedAt = DateTime.UtcNow,
                    Choice = -1
                };
                dbContext.QuizAnswers.Add(ligne);
            }
            else
            {
                // Il avait ouvert la question sans repondre : on repart du present.
                // Sans cela, une question affichee puis abandonnee serait perdue.
                ligne.StartedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync();

            return new QuizDepartResult
            {
                Id = q.Id,
                Difficulte = QuizDifficulte(q.Rang),
                Points = q.Points,
                Texte = q.Texte,
                Choix = q.Choix.ToList(),
                Secondes = QUIZ_SECONDES
            };
        }

        public sealed class QuizReponseResult
        {
            public string Id { get; set; } = "";
            public bool Correct { get; set; }
            public int BonneReponse { get; set; }
            public int MonChoix { get; set; }
            public int Points { get; set; }
            public int TempsMs { get; set; }
            public bool HorsDelai { get; set; }
            public int PointsTotal { get; set; }
        }

        public async Task<QuizReponseResult> QuizReponseAsync(Guid userId, string questionId, int choix)
        {
            string src = "UserMatchService.QuizReponseAsync";

            var q = QUIZ.FirstOrDefault(f => f.Id.Equals(questionId));
            if (q == null) { throw BaseException.NotFound(-2, src); }
            if (!QuizOuvert()) { throw new BaseException(-4, src, "Le défi est terminé."); }
            // -1 : le chrono a expire sans que le joueur ait choisi. On enregistre
            // quand meme, sinon la question resterait indefiniment a jouer.
            if (choix < -1 || choix > 3) { throw BaseException.InvalidModel(-6, src); }

            var ligne = await dbContext.QuizAnswers
                .FirstOrDefaultAsync(w => w.UserId.Equals(userId) && w.QuestionId.Equals(questionId));

            if (ligne == null) { throw new BaseException(-7, src, "Cette question n'a pas été ouverte."); }
            if (ligne.AnsweredAt.HasValue)
            { throw new BaseException(-5, src, "Tu as déjà répondu à cette question."); }

            DateTime maintenant = DateTime.UtcNow;
            int ecoule = (int)Math.Max(0, (maintenant - ligne.StartedAt).TotalMilliseconds);
            bool horsDelai = ecoule > (QUIZ_SECONDES * 1000) + QUIZ_TOLERANCE_MS;
            bool correct = choix == q.Bonne;

            ligne.AnsweredAt = maintenant;
            ligne.Choice = choix;
            ligne.Correct = correct;
            ligne.Points = (correct && !horsDelai) ? q.Points : 0;
            // Le temps retenu est plafonne : une question laissee ouverte une nuit
            // ne doit pas peser une nuit entiere dans le departage des ex aequo.
            ligne.TimeMs = Math.Min(ecoule, (QUIZ_SECONDES * 1000) + QUIZ_TOLERANCE_MS);

            await dbContext.SaveChangesAsync();

            int total = await dbContext.QuizAnswers
                .Where(w => w.UserId.Equals(userId) && w.AnsweredAt.HasValue)
                .SumAsync(s => s.Points);

            return new QuizReponseResult
            {
                Id = q.Id,
                Correct = correct,
                BonneReponse = q.Bonne,
                MonChoix = choix,
                Points = ligne.Points,
                TempsMs = ligne.TimeMs,
                HorsDelai = horsDelai,
                PointsTotal = total
            };
        }

        public sealed class QuizLigneResult
        {
            public int Rang { get; set; }
            public Guid UserId { get; set; }
            public string DisplayName { get; set; } = "";
            public int Points { get; set; }
            public int TempsMs { get; set; }
            public int Questions { get; set; }
            public bool Moi { get; set; }
        }

        public sealed class QuizClassementResult
        {
            public int Joueurs { get; set; }
            public List<QuizLigneResult> Lignes { get; set; } = new();
            public QuizLigneResult? Moi { get; set; }
        }

        // Le classement : les points d'abord, le temps total ensuite. A egalite de
        // points, celui qui a repondu le plus vite passe devant.
        public async Task<QuizClassementResult> QuizClassementAsync(Guid userId, int limit = 20)
        {
            var brut = await dbContext.QuizAnswers
                .Where(w => w.AnsweredAt.HasValue)
                .GroupBy(g => g.UserId)
                .Select(s => new
                {
                    UserId = s.Key,
                    Points = s.Sum(x => x.Points),
                    TempsMs = s.Sum(x => x.TimeMs),
                    Questions = s.Count()
                })
                .ToListAsync();

            var ids = brut.Select(s => s.UserId).ToList();
            var pseudos = await dbContext.Users
                .Where(w => ids.Contains(w.Id))
                .Select(s => new { s.Id, s.DisplayName })
                .ToListAsync();

            var ordonne = brut
                .OrderByDescending(o => o.Points)
                .ThenBy(o => o.TempsMs)
                .ToList();

            var res = new QuizClassementResult { Joueurs = ordonne.Count };

            for (int i = 0; i < ordonne.Count; i++)
            {
                var ligne = new QuizLigneResult
                {
                    Rang = i + 1,
                    UserId = ordonne[i].UserId,
                    DisplayName = pseudos.Where(w => w.Id.Equals(ordonne[i].UserId))
                                         .Select(s => s.DisplayName ?? "?").FirstOrDefault() ?? "?",
                    Points = ordonne[i].Points,
                    TempsMs = ordonne[i].TempsMs,
                    Questions = ordonne[i].Questions,
                    Moi = ordonne[i].UserId.Equals(userId)
                };

                if (i < limit) { res.Lignes.Add(ligne); }
                if (ligne.Moi) { res.Moi = ligne; }
            }

            return res;
        }

        // ===== LA PAGE « LA COMPO OFFICIELLE EST TOMBEE » =====
        // Le onze du coach, le onze du joueur, et pour chaque homme le nombre de
        // participants qui l'avaient aligne. La note est celle du serveur, rarete
        // comprise : un titulaire que peu de monde avait vu vaut davantage, et
        // c'est pour cela qu'un meme 8/11 peut donner deux notes differentes.
        //
        // Rien n'est renvoye avant la cloture des pronostics : jusque-la, ces
        // chiffres diraient aux retardataires ce que les autres ont joue.
        public sealed class CompoSoloResult
        {
            public bool HasOfficialComposition { get; set; }
            public int ParticipantCount { get; set; }
            public int Trouves { get; set; }
            public double Note { get; set; }
            public List<string> Officiel { get; set; } = new();
            public List<string> Mien { get; set; } = new();
            public Dictionary<string, int> Choix { get; set; } = new();
        }

        public async Task<CompoSoloResult> CompoAsync(string teamId, string matchId, Guid userId)
        {
            string src = "UserMatchService.CompoAsync";
            var res = new CompoSoloResult();

            var match = await dbContext.Matches
                .Include(i => i.HomeTeam).ThenInclude(t => t.PlayerForMatches)
                .Include(i => i.AwayTeam).ThenInclude(t => t.PlayerForMatches)
                .FirstOrDefaultAsync(w => w.Id.Equals(matchId));
            if (match == null) { throw BaseException.NotFound(-2, src); }

            DateTime clotureUtc = GroupService.ParisToUtc(
                match.DateTime.AddHours(-GroupService.ClotureAvantHeures));
            if (DateTime.UtcNow < clotureUtc) { throw BaseException.InvalidModel(-3, src); }

            var side = (match.HomeTeam != null && match.HomeTeam.TeamId != null
                        && match.HomeTeam.TeamId.Equals(teamId, StringComparison.OrdinalIgnoreCase))
                     ? match.HomeTeam : match.AwayTeam;
            if (side == null) { throw BaseException.NotFound(-4, src); }

            var titulaires = side.PlayerForMatches
                .Where(w => !(w.Position != null
                           && w.Position.Trim().Equals("SUBSTITUTE", StringComparison.OrdinalIgnoreCase)))
                .ToList();
            res.HasOfficialComposition = titulaires.Count > 0;
            res.Officiel = titulaires.Select(s => s.PersonId).ToList();

            var mien = await dbContext.UserMatches
                .Include(i => i.UserPlayerForMatches)
                .FirstOrDefaultAsync(w => w.MatchId.Equals(matchId)
                                       && w.TeamId.Equals(teamId)
                                       && w.UserId.Equals(userId));
            if (mien != null)
            { res.Mien = mien.UserPlayerForMatches.Select(s => s.PersonId).ToList(); }

            var toutes = await dbContext.UserMatches
                .Where(w => w.MatchId.Equals(matchId) && w.TeamId.Equals(teamId))
                .Include(i => i.UserPlayerForMatches)
                .ToListAsync();
            res.ParticipantCount = toutes.Count;

            var decompte = new Dictionary<string, int>();
            foreach (var prediction in toutes)
            {
                foreach (var pick in prediction.UserPlayerForMatches)
                {
                    if (pick.PersonId == null) { continue; }
                    decompte[pick.PersonId] = decompte.TryGetValue(pick.PersonId, out var deja) ? deja + 1 : 1;
                }
            }

            // On ne renvoie que les hommes concernes : les onze du coach et les onze
            // du joueur. Le reste de l'effectif ne regarde pas cette page.
            foreach (var id in res.Officiel.Concat(res.Mien).Distinct())
            {
                if (id == null) { continue; }
                res.Choix[id] = decompte.TryGetValue(id, out var combien) ? combien : 0;
            }

            double somme = 0;
            int trouves = 0;
            foreach (var id in res.Officiel)
            {
                if (id == null || !res.Mien.Contains(id)) { continue; }
                trouves++;
                int nb = decompte.TryGetValue(id, out var c) ? c : 0;
                somme += UserStatsService.BASE_TITULAIRE
                       * UserStatsService.CoefRarete(nb, res.ParticipantCount);
            }
            res.Trouves = trouves;
            res.Note = Math.Round(somme, 4);

            return res;
        }

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
