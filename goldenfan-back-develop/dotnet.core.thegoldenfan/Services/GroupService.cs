using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.utils;
using dotnet.core.utils.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.Services
{
    public class GroupService
    {
        private readonly AppDbContext dbContext;

        public GroupService(AppDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        private const int MinMembers = 2;
        private const int MaxMembers = 10;

        // Un mini-championnat dure 5 matchs du PSG, toutes compétitions confondues.
        private const int SeasonLength = 5;

        // Durée d'affichage du vainqueur avant que le cycle suivant démarre.
        // Le point de départ est le coup d'envoi du 5e match (le back ne connaît pas
        // l'heure du coup de sifflet final), donc l'affichage dure en pratique une
        // vingtaine d'heures.
        private const int ChampionDisplayHours = 24;

        public class CreateGroupInput
        {
            public string Name { get; set; } = null!;
        }

        public class GroupResult
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = null!;
            public string InviteCode { get; set; } = null!;
            public DateTime CreatedDate { get; set; }
            public int MemberCount { get; set; }

            // Qui a créé le groupe. Le site s'en sert pour reconnaître le groupe
            // personnel d'un joueur sans avoir à deviner d'après son nom.
            public Guid CreatorId { get; set; }
        }

        public class GroupMemberRankingResult
        {
            public Guid UserId { get; set; }
            public string DisplayName { get; set; } = null!;
            public double TotalScore { get; set; }
            public int MatchesPlayed { get; set; }
            public double ExpertCoef { get; set; }
            public int Rank { get; set; }
        }

        public class LeaveGroupResult
        {
            public bool GroupDeleted { get; set; }
            public int RemainingMembers { get; set; }
        }

        public class GroupDetailsResult
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = null!;
            public string InviteCode { get; set; } = null!;
            public DateTime CreatedDate { get; set; }
            public bool IsSeasonComplete { get; set; }
            public int MatchesCounted { get; set; }
            public int MatchesPlayed { get; set; }
            public int SeasonLength { get; set; }
            public int CycleNumber { get; set; }
            public string? ChampionName { get; set; } = null;
            public List<GroupMemberRankingResult> Ranking { get; set; } = new();
        }

        // Un match est "joué" dès lors que son statut n'est plus "Fixture" (match à venir).
        // Comparaison insensible à la casse pour ne dépendre d'aucune convention d'écriture.
        private static bool IsPlayed(string? status)
        {
            return !string.IsNullOrWhiteSpace(status) &&
                   !status.Trim().Equals("Fixture", StringComparison.OrdinalIgnoreCase);
        }

        // --- Le salon du groupe : modèles de sortie ---

        public class SalonPlayerResult
        {
            public string Id { get; set; } = null!;
            public string? FirstName { get; set; }
            public string? LastName { get; set; }

            // Combien de membres du groupe ont choisi ce joueur, sur ceux qui ont joué.
            public int ChoiceCount { get; set; }

            // Présent dans le onze officiel. Faux tant que la compo n'est pas saisie.
            public bool Found { get; set; }
        }

        public class SalonRealResult
        {
            public int TeamScore { get; set; }
            public int OpponentScore { get; set; }
            public double TeamPossession { get; set; }
            public double OpponentPossession { get; set; }
            public int TeamShots { get; set; }
            public int OpponentShots { get; set; }
            public int TeamFouls { get; set; }
            public int OpponentFouls { get; set; }
            public int TeamCrosses { get; set; }
            public int OpponentCrosses { get; set; }
        }

        public class SalonMemberResult
        {
            public Guid UserId { get; set; }
            public string DisplayName { get; set; } = null!;
            public bool IsMe { get; set; }
            public bool HasPrediction { get; set; }

            // Le pronostic brut. Tout à zéro si le membre n'a pas joué ce match.
            public double TeamPossession { get; set; }
            public double OpponentPossession { get; set; }
            public int TeamShots { get; set; }
            public int OpponentShots { get; set; }
            public int TeamFouls { get; set; }
            public int OpponentFouls { get; set; }
            public int TeamCrosses { get; set; }
            public int OpponentCrosses { get; set; }
            public int TeamScore { get; set; }
            public int OpponentScore { get; set; }

            // Les onze choisis, dans l'ordre d'enregistrement.
            public List<SalonPlayerResult> Picks { get; set; } = new();

            // État 2 : renseignés dès que le onze officiel est saisi.
            public int? CompositionFound { get; set; }
            public double? CompositionNote { get; set; }

            // État 3 : renseignés une fois le match noté.
            public double? NoteComposition { get; set; }
            public double? NoteScore { get; set; }
            public double? NotePossession { get; set; }
            public double? NoteShots { get; set; }
            public double? NoteFouls { get; set; }
            public double? NoteCrosses { get; set; }
            public double? NoteTotal { get; set; }
            public int? MatchRank { get; set; }
            public int? GroupPoints { get; set; }
        }

        public class SalonResult
        {
            public string MatchId { get; set; } = null!;
            public DateTime DateTime { get; set; }
            public string? Status { get; set; }

            // "closed" à la clôture, "composition" dès le onze officiel, "results" une fois noté.
            public string State { get; set; } = "closed";

            public string? TeamName { get; set; }
            public string? OpponentName { get; set; }
            public bool TeamIsHome { get; set; }

            public Guid GroupId { get; set; }
            public string GroupName { get; set; } = null!;

            public int MemberCount { get; set; }
            public int PredictionCount { get; set; }

            public bool HasOfficialComposition { get; set; }
            public List<SalonPlayerResult> OfficialLineup { get; set; } = new();

            public SalonRealResult? Real { get; set; }
            public List<SalonMemberResult> Members { get; set; } = new();
        }

        // Le salon, lui, exige le statut strict : ses notes n'existent que si le moteur
        // est passé, et le moteur n'accepte que "Played".
        private static bool IsScored(string? status)
        {
            return !string.IsNullOrWhiteSpace(status) &&
                   status.Trim().Equals("Played", StringComparison.OrdinalIgnoreCase);
        }

        // Les coups d'envoi sont enregistrés en heure de Paris, sans fuseau, alors que
        // le serveur raisonne en UTC. Sans cette conversion, le verrou de clôture
        // resterait fermé deux heures de trop, en plein dans la fenêtre où les joueurs
        // veulent entrer dans le salon.
        private static readonly TimeZoneInfo ParisTimeZone = ResolveParisTimeZone();

        private static TimeZoneInfo ResolveParisTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"); }
            catch
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time"); }
                catch { return TimeZoneInfo.Utc; }
            }
        }

        private static DateTime ParisToUtc(DateTime parisTime)
        {
            var unspecified = DateTime.SpecifyKind(parisTime, DateTimeKind.Unspecified);
            try { return TimeZoneInfo.ConvertTimeToUtc(unspecified, ParisTimeZone); }
            catch { return unspecified; }
        }

        // Un code court, lisible, sans caractères ambigus (pas de 0/O ni de 1/I)
        private static string GenerateInviteCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = new Random();
            return new string(Enumerable.Range(0, 6).Select(_ => chars[random.Next(chars.Length)]).ToArray());
        }

        public async Task<GroupResult> CreateAsync(Guid creatorId, CreateGroupInput input)
        {
            string src = "GroupService.CreateAsync";
            if (StringHelper.IsNull(input.Name)) { throw BaseException.InvalidModel(-1, src); }

            var creator = await dbContext.Users.FirstOrDefaultAsync(w => w.Id.Equals(creatorId));
            if (creator == null) { throw BaseException.NotFound(-2, src); }

            string inviteCode;
            do { inviteCode = GenerateInviteCode(); }
            while (await dbContext.Groups.AnyAsync(w => w.InviteCode.Equals(inviteCode)));

            var group = new Group
            {
                Id = Guid.NewGuid(),
                Name = input.Name,
                InviteCode = inviteCode,
                CreatorId = creatorId,
                CreatedDate = DateTime.UtcNow
            };
            dbContext.Groups.Add(group);

            dbContext.GroupMembers.Add(new GroupMember
            {
                Id = Guid.NewGuid(),
                GroupId = group.Id,
                UserId = creatorId,
                DateJoined = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();

            return new GroupResult
            {
                Id = group.Id,
                Name = group.Name,
                InviteCode = group.InviteCode,
                CreatedDate = group.CreatedDate,
                MemberCount = 1,
                CreatorId = group.CreatorId
            };
        }

        public async Task<GroupResult> JoinAsync(string inviteCode, Guid userId)
        {
            string src = "GroupService.JoinAsync";
            var group = await dbContext.Groups
                .Include(i => i.Members)
                .FirstOrDefaultAsync(w => w.InviteCode.Equals(inviteCode));
            if (group == null) { throw BaseException.NotFound(-1, src); }

            var user = await dbContext.Users.FirstOrDefaultAsync(w => w.Id.Equals(userId));
            if (user == null) { throw BaseException.NotFound(-2, src); }

            if (group.Members.Any(m => m.UserId.Equals(userId)))
            { throw BaseException.AlreadyInDb(-3, src); }

            if (group.Members.Count >= MaxMembers)
            { throw BaseException.InvalidModel(-4, src); }

            dbContext.GroupMembers.Add(new GroupMember
            {
                Id = Guid.NewGuid(),
                GroupId = group.Id,
                UserId = userId,
                DateJoined = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();

            return new GroupResult
            {
                Id = group.Id,
                Name = group.Name,
                InviteCode = group.InviteCode,
                CreatedDate = group.CreatedDate,
                MemberCount = group.Members.Count + 1,
                CreatorId = group.CreatorId
            };
        }

        // Un membre quitte le groupe de son plein gré. Le créateur n'a aucun statut
        // particulier : s'il part, le groupe continue sans lui (CreatorId reste
        // renseigné, l'utilisateur existe toujours en base, rien ne casse).
        // Le dernier membre à sortir éteint la lumière : un groupe vide est supprimé.
        public async Task<LeaveGroupResult> LeaveAsync(Guid groupId, Guid userId)
        {
            string src = "GroupService.LeaveAsync";

            var group = await dbContext.Groups
                .Include(i => i.Members)
                .FirstOrDefaultAsync(w => w.Id.Equals(groupId));
            if (group == null) { throw BaseException.NotFound(-1, src); }

            var membership = group.Members.FirstOrDefault(m => m.UserId.Equals(userId));
            if (membership == null) { throw BaseException.NotFound(-2, src); }

            // Compté avant toute suppression : après Remove, l'état de la collection
            // de navigation n'est pas garanti tant que SaveChanges n'a pas eu lieu.
            int membersBefore = group.Members.Count;

            dbContext.GroupMembers.Remove(membership);

            bool groupDeleted = false;
            if (membersBefore <= 1)
            {
                dbContext.Groups.Remove(group);
                groupDeleted = true;
            }

            await dbContext.SaveChangesAsync();

            return new LeaveGroupResult
            {
                GroupDeleted = groupDeleted,
                RemainingMembers = groupDeleted ? 0 : membersBefore - 1
            };
        }

        public async Task<GroupDetailsResult> ByIdAsync(Guid groupId)
        {
            string src = "GroupService.ByIdAsync";
            var group = await dbContext.Groups
                .Include(i => i.Members)
                .ThenInclude(i => i.User)
                .FirstOrDefaultAsync(w => w.Id.Equals(groupId));
            if (group == null) { throw BaseException.NotFound(-1, src); }

            // Tous les matchs programmés depuis la création du groupe, dans l'ordre chronologique.
            var allMatches = await dbContext.Matches
                .Where(w => w.DateTime >= group.CreatedDate)
                .OrderBy(o => o.DateTime)
                .Select(s => new { s.Id, s.Status, s.DateTime })
                .ToListAsync();

            DateTime now = DateTime.UtcNow;

            // Les matchs sont découpés en cycles de 5. On avance au cycle suivant
            // seulement quand le cycle courant est complet (5 matchs joués) ET que
            // les 24h d'affichage du vainqueur sont écoulées.
            int cycleIndex = 0;
            while (true)
            {
                var block = allMatches.Skip(cycleIndex * SeasonLength).Take(SeasonLength).ToList();
                if (block.Count < SeasonLength) { break; }
                if (block.Any(a => !IsPlayed(a.Status))) { break; }
                if (now < block.Last().DateTime.AddHours(ChampionDisplayHours)) { break; }
                cycleIndex++;
            }

            var cycleMatches = allMatches.Skip(cycleIndex * SeasonLength).Take(SeasonLength).ToList();
            var cycleMatchIds = cycleMatches.Select(s => s.Id).ToList();
            var playedMatches = cycleMatches.Where(w => IsPlayed(w.Status)).ToList();

            bool isComplete = cycleMatches.Count >= SeasonLength &&
                              playedMatches.Count >= SeasonLength;

            var memberIds = group.Members.Select(m => m.UserId).ToList();

            // On récupère la note DU MATCH (ResultTotal), et non la moyenne de saison
            // (ResultFinalTotal), qui ne sert ici qu'à départager les ex aequo.
            var predictions = await dbContext.UserMatches
                .Where(w => memberIds.Contains(w.UserId) && cycleMatchIds.Contains(w.MatchId))
                .Select(s => new { s.UserId, s.MatchId, s.ResultTotal, s.ResultFinalTotal })
                .ToListAsync();

            var points = memberIds.ToDictionary(k => k, v => 0);
            var played = memberIds.ToDictionary(k => k, v => 0);

            // Barème positionnel ancré sur le DERNIER PRÉSENT, pas sur la taille du groupe :
            // le dernier participant du soir marque 2 points, et chaque place au-dessus
            // en vaut un de plus. L'absent marque 0 et n'occupe aucun rang. Gagner seul
            // dans un groupe de dix rapporte donc 2 points, pas 11.
            foreach (var m in playedMatches)
            {
                var noted = predictions
                    .Where(w => w.MatchId.Equals(m.Id) && w.ResultTotal.HasValue)
                    .OrderByDescending(o => o.ResultTotal.Value)
                    .ToList();

                int rank = 0;
                for (int i = 0; i < noted.Count; i++)
                {
                    // Deux notes identiques donnent le même rang, donc les mêmes points.
                    if (i > 0 && noted[i].ResultTotal.Value != noted[i - 1].ResultTotal.Value)
                    { rank = i; }

                    int score = noted.Count - rank + 1;
                    if (score < 2) { score = 2; }

                    points[noted[i].UserId] += score;
                    played[noted[i].UserId] += 1;
                }
            }

            var ranking = group.Members.Select(m => new GroupMemberRankingResult
            {
                UserId = m.UserId,
                DisplayName = m.User.DisplayName ?? "?",
                TotalScore = points.ContainsKey(m.UserId) ? points[m.UserId] : 0,
                MatchesPlayed = played.ContainsKey(m.UserId) ? played[m.UserId] : 0,
                ExpertCoef = predictions
                    .Where(w => w.UserId.Equals(m.UserId) && w.ResultFinalTotal.HasValue)
                    .Select(s => s.ResultFinalTotal.Value)
                    .DefaultIfEmpty(0)
                    .Max()
            })
            .OrderByDescending(o => o.TotalScore)
            .ThenByDescending(o => o.ExpertCoef)
            .ThenBy(o => o.DisplayName)
            .ToList();

            for (int i = 0; i < ranking.Count; i++) { ranking[i].Rank = i + 1; }

            // Le vainqueur n'est proclamé qu'une fois les 5 matchs joués,
            // et seulement s'il a réellement marqué des points.
            string? championName = null;
            if (isComplete && ranking.Count > 0 && ranking[0].TotalScore > 0)
            { championName = ranking[0].DisplayName; }

            return new GroupDetailsResult
            {
                Id = group.Id,
                Name = group.Name,
                InviteCode = group.InviteCode,
                CreatedDate = group.CreatedDate,
                IsSeasonComplete = isComplete,
                MatchesCounted = cycleMatches.Count,
                MatchesPlayed = playedMatches.Count,
                SeasonLength = SeasonLength,
                CycleNumber = cycleIndex + 1,
                ChampionName = championName,
                Ranking = ranking
            };
        }

        // --- Le salon du groupe ---
        // Pour un groupe et un match, tout ce que la page affiche : le pronostic de
        // chaque membre, sa composition, le nombre de fois où chaque joueur de
        // l'effectif a été choisi, et les notes quand elles existent.
        //
        // Trois états, une seule route. L'état est déduit de ce qui existe en base :
        // pas de onze officiel, on en reste aux pronostics bruts ; onze officiel saisi,
        // les notes de composition apparaissent ; match noté, tout apparaît.
        //
        // Deux règles tenues ici, et nulle part ailleurs :
        // - le serveur refuse de répondre avant la clôture des pronostics. Un verrou
        //   posé seulement côté site laisserait lire les compositions des autres en
        //   appelant l'adresse à la main, 24 h avant tout le monde ;
        // - cette route n'écrit rien. Ni statut, ni note, ni recalcul.
        public async Task<SalonResult> SalonAsync(string teamId, string matchId, Guid userId, Guid groupId)
        {
            string src = "GroupService.SalonAsync";

            if (StringHelper.IsNull(teamId) || StringHelper.IsNull(matchId))
            { throw BaseException.InvalidModel(-1, src); }

            var match = await dbContext.Matches
                .Include(i => i.Place)
                .Include(i => i.MatchDate).ThenInclude(i => i.Calendar).ThenInclude(i => i.Competition)
                .Include(i => i.HomeTeam).ThenInclude(i => i.Team)
                .Include(i => i.HomeTeam).ThenInclude(i => i.PlayerForMatches).ThenInclude(i => i.Person)
                .Include(i => i.AwayTeam).ThenInclude(i => i.Team)
                .Include(i => i.AwayTeam).ThenInclude(i => i.PlayerForMatches).ThenInclude(i => i.Person)
                .FirstOrDefaultAsync(w => w.Id.Equals(matchId));
            if (match == null) { throw BaseException.NotFound(-2, src); }

            // Le verrou. La clôture tombe 24 h avant le coup d'envoi, heure de Paris.
            DateTime clotureUtc = ParisToUtc(match.DateTime.AddHours(-24));
            if (DateTime.UtcNow < clotureUtc) { throw BaseException.InvalidModel(-3, src); }

            var group = await dbContext.Groups
                .Include(i => i.Members).ThenInclude(i => i.User)
                .FirstOrDefaultAsync(w => w.Id.Equals(groupId));
            if (group == null) { throw BaseException.NotFound(-4, src); }

            // Seul un membre entre dans le salon de son groupe.
            if (!group.Members.Any(a => a.UserId.Equals(userId)))
            { throw BaseException.NotFound(-5, src); }

            var detail = BaseMatchResult.FromDb(match);
            bool teamIsHome = detail.HomeTeam != null && detail.HomeTeam.Id != null &&
                              detail.HomeTeam.Id.Equals(teamId, StringComparison.OrdinalIgnoreCase);
            var teamSide = teamIsHome ? detail.HomeTeam : detail.AwayTeam;
            var opponentSide = teamIsHome ? detail.AwayTeam : detail.HomeTeam;
            if (teamSide == null || opponentSide == null) { throw BaseException.NotFound(-6, src); }

            // Le onze officiel. Tant qu'il n'est pas saisi, la liste est vide : rien à comparer.
            var officialPlayers = teamSide.Players
                .Where(w => !(w.Position != null && w.Position.Trim().Equals("SUBSTITUTE", StringComparison.OrdinalIgnoreCase)))
                .ToList();
            bool hasComposition = officialPlayers.Count > 0;
            var officialIds = officialPlayers
                .Where(w => w.Id != null)
                .Select(s => s.Id)
                .ToList();

            bool scored = IsScored(match.Status);

            var memberIds = group.Members.Select(s => s.UserId).ToList();

            // Les pronostics des membres sur ce match, avec les onze choisis par chacun.
            var predictions = await dbContext.UserMatches
                .Where(w => w.MatchId.Equals(matchId) &&
                            w.TeamId.Equals(teamId) &&
                            memberIds.Contains(w.UserId))
                .Include(i => i.UserPlayerForMatches).ThenInclude(i => i.Person)
                .ToListAsync();

            // Le dénominateur de « choisi n fois » : les membres qui ont réellement joué.
            int predictionCount = predictions.Count;

            var choiceCount = new Dictionary<string, int>();
            foreach (var prediction in predictions)
            {
                foreach (var pick in prediction.UserPlayerForMatches)
                {
                    if (pick.PersonId == null) { continue; }
                    if (choiceCount.ContainsKey(pick.PersonId)) { choiceCount[pick.PersonId] += 1; }
                    else { choiceCount[pick.PersonId] = 1; }
                }
            }

            var members = new List<SalonMemberResult>();
            foreach (var member in group.Members)
            {
                var prediction = predictions.FirstOrDefault(w => w.UserId.Equals(member.UserId));

                var row = new SalonMemberResult
                {
                    UserId = member.UserId,
                    DisplayName = member.User.DisplayName ?? "?",
                    IsMe = member.UserId.Equals(userId),
                    HasPrediction = prediction != null
                };

                if (prediction != null)
                {
                    row.TeamPossession = prediction.PreTeamPossession;
                    row.OpponentPossession = prediction.PreOpponentPossession;
                    row.TeamShots = prediction.PreTeamShots;
                    row.OpponentShots = prediction.PreOpponentShots;
                    row.TeamFouls = prediction.PreTeamFouls;
                    row.OpponentFouls = prediction.PreOpponentFouls;
                    row.TeamCrosses = prediction.PreTeamCrosses;
                    row.OpponentCrosses = prediction.PreOpponentCrosses;
                    row.TeamScore = prediction.PreTeamScore;
                    row.OpponentScore = prediction.PreOpponentScore;

                    foreach (var pick in prediction.UserPlayerForMatches)
                    {
                        if (pick.PersonId == null) { continue; }
                        row.Picks.Add(new SalonPlayerResult
                        {
                            Id = pick.PersonId,
                            FirstName = pick.Person != null ? pick.Person.FirstName : null,
                            LastName = pick.Person != null ? pick.Person.LastName : null,
                            ChoiceCount = choiceCount.ContainsKey(pick.PersonId) ? choiceCount[pick.PersonId] : 0,
                            Found = officialIds.Contains(pick.PersonId)
                        });
                    }

                    if (hasComposition)
                    {
                        int found = row.Picks.Count(c => c.Found);
                        row.CompositionFound = found;
                        // Exactement la formule du moteur : titulaires trouvés sur onze, ramenés sur cent.
                        row.CompositionNote = Math.Round((found / 11.0) * 100, 4);
                    }

                    if (scored && prediction.ResultTotal.HasValue)
                    {
                        row.NoteComposition = prediction.ResultTeamCompositionFormula;
                        row.NoteScore = prediction.ResultTeamScoreFormula;
                        row.NotePossession = prediction.ResultTeamPossessionFormula;
                        row.NoteShots = prediction.ResultTeamShotsFormula;
                        row.NoteFouls = prediction.ResultTeamFoulsFormula;
                        row.NoteCrosses = prediction.ResultTeamCrossesFormula;
                        row.NoteTotal = prediction.ResultTotal;
                    }
                }

                members.Add(row);
            }

            // Le classement du match et les points du mini-championnat, une fois le
            // match noté. Même barème que le classement de groupe : ancré sur le dernier
            // présent, deux notes identiques donnant le même rang donc les mêmes points.
            if (scored)
            {
                var noted = members
                    .Where(w => w.NoteTotal.HasValue)
                    .OrderByDescending(o => o.NoteTotal.Value)
                    .ToList();

                int rank = 0;
                for (int i = 0; i < noted.Count; i++)
                {
                    if (i > 0 && noted[i].NoteTotal.Value != noted[i - 1].NoteTotal.Value)
                    { rank = i; }

                    noted[i].MatchRank = rank + 1;

                    int points = noted.Count - rank + 1;
                    if (points < 2) { points = 2; }
                    noted[i].GroupPoints = points;
                }
            }

            // Ceux qui ont joué d'abord, les mieux notés en tête, les absents à la fin.
            // Le site remet le joueur sur la première ligne du tableau : c'est un choix
            // d'affichage, pas un classement.
            members = members
                .OrderByDescending(o => o.HasPrediction)
                .ThenByDescending(o => o.NoteTotal ?? double.MinValue)
                .ThenByDescending(o => o.CompositionNote ?? double.MinValue)
                .ThenBy(o => o.DisplayName)
                .ToList();

            var result = new SalonResult
            {
                MatchId = match.Id,
                DateTime = match.DateTime,
                Status = match.Status,
                State = scored ? "results" : (hasComposition ? "composition" : "closed"),
                TeamName = teamSide.Name,
                OpponentName = opponentSide.Name,
                TeamIsHome = teamIsHome,
                GroupId = group.Id,
                GroupName = group.Name,
                MemberCount = group.Members.Count,
                PredictionCount = predictionCount,
                HasOfficialComposition = hasComposition,
                Members = members
            };

            foreach (var player in officialPlayers)
            {
                if (player.Id == null) { continue; }
                result.OfficialLineup.Add(new SalonPlayerResult
                {
                    Id = player.Id,
                    FirstName = player.FirstName,
                    LastName = player.LastName,
                    ChoiceCount = choiceCount.ContainsKey(player.Id) ? choiceCount[player.Id] : 0,
                    Found = true
                });
            }

            if (scored)
            {
                result.Real = new SalonRealResult
                {
                    TeamScore = teamSide.Score,
                    OpponentScore = opponentSide.Score,
                    TeamPossession = teamSide.Possession,
                    OpponentPossession = opponentSide.Possession,
                    TeamShots = teamSide.Shots,
                    OpponentShots = opponentSide.Shots,
                    TeamFouls = teamSide.Fouls,
                    OpponentFouls = opponentSide.Fouls,
                    TeamCrosses = teamSide.Crosses,
                    OpponentCrosses = opponentSide.Crosses
                };
            }

            return result;
        }

        public async Task<List<GroupResult>> ByUserIdAsync(Guid userId)
        {
            var memberships = await dbContext.GroupMembers
                .Where(w => w.UserId.Equals(userId))
                .Include(i => i.Group)
                .ThenInclude(i => i.Members)
                .ToListAsync();

            return memberships.Select(m => new GroupResult
            {
                Id = m.Group.Id,
                Name = m.Group.Name,
                InviteCode = m.Group.InviteCode,
                CreatedDate = m.Group.CreatedDate,
                MemberCount = m.Group.Members.Count,
                CreatorId = m.Group.CreatorId
            }).ToList();
        }
    }
}
