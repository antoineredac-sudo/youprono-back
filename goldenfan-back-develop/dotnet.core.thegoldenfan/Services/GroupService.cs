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

        // Le coefficient expert n'est calcule qu'a un seul endroit du jeu :
        // UserService.ExpertCoefAllAsync. On l'appelle plutot que de le recalculer ici,
        // sinon le kop afficherait des nombres que le classement general contredirait.
        private readonly UserService userService;

        public GroupService(AppDbContext dbContext, UserService userService)
        {
            this.dbContext = dbContext;
            this.userService = userService;
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

        // ===== LA MEMOIRE COURTE DU DECOMPTE DES CHOIX =====
        // Pour afficher « choisi 12 fois sur 38 », le salon lit tous les pronostics
        // du match, tous joueurs confondus, avec les onze noms choisis par chacun.
        // A trois cents participants cela fait plus de trois mille lignes, relues
        // a chaque ouverture du salon, par chaque membre de chaque groupe — alors
        // que le resultat est le meme pour tout le monde.
        //
        // On le garde donc quelques minutes. C'est sans danger : le salon ne
        // s'ouvre qu'apres la cloture, quand plus personne ne peut modifier son
        // pronostic. Le decompte ne bouge plus. Rien n'est range en base : la
        // memoire disparait au redemarrage du serveur.
        private const int CHOIX_MEMOIRE_SECONDES = 300;

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,
            (DateTime Heure, Dictionary<string, int> Decompte, int Participants)> memoireChoix = new();

        // Les pronostics ferment ce nombre d'heures avant le coup d'envoi (heure de
        // Paris). Le site porte la même valeur, CLOTURE_AVANT_MS : les changer ensemble.
        // Publique : UserService s'en sert pour savoir quels matchs un joueur
        // pouvait jouer. Une seule definition du delai de cloture dans tout le back.
        public const int ClotureAvantHeures = 2;

        // --- Les recompenses de groupe ---
        // Seuils de points cumules donnant droit a chaque metal, du bronze au diamant.
        private static readonly int[] MedalThresholds = { 15, 22, 29, 36, 43 };
        // Ce que chaque metal apporte a la coupe.
        private static readonly int[] MedalValues = { 3, 5, 7, 9, 11 };
        private static readonly string[] MedalNames = { "Bronze", "Argent", "Or", "Platine", "Diamant" };
        // Une coupe se remplit a 12 points. Le diamant vaut 11 : aucune coupe ne peut
        // donc etre remplie en un seul mini-championnat.
        private const int CupTarget = 12;
        // En dessous de trois membres, pas de medaille : un duel se felicite, il ne se
        // recompense pas.
        private const int MinMembersForMedal = 3;

        // --- Les deux natures de groupe ---
        // "amis" : le groupe historique. Dix membres au maximum, bareme positionnel,
        //          medailles et coupes.
        // "kop"  : le kop de supporters. Aucun plafond de membres, aucun point,
        //          aucune medaille. On s'y situe au coefficient expert, rien de plus.
        // Un joueur peut appartenir a plusieurs groupes d'amis, mais a un seul kop.
        public const string TypeAmis = "amis";
        public const string TypeKop = "kop";

        // Un kop de partenaire : meme mecanique qu'un kop ordinaire, mais le
        // message de partage sur X parle au nom d'un media a ses abonnes, pas
        // d'un joueur a ses amis. Aucune colonne en plus : c'est une troisieme
        // valeur de Type.
        public const string TypeKopMedia = "kopmedia";

        // Au-dela, le nom ne tient plus dans le message de partage sur X.
        private const int KopNameMaxLength = 30;

        // Un kop d'un seul membre n'est pas affiche dans la liste : il disparait tout
        // seul sans qu'on ait a le supprimer, et son lien direct continue de marcher.
        private const int KopMinMembersToList = 2;

        private static string NormalizeType(string? type)
        {
            string t = (type ?? "").Trim();
            if (string.Equals(t, TypeKopMedia, StringComparison.OrdinalIgnoreCase)) { return TypeKopMedia; }
            if (string.Equals(t, TypeKop, StringComparison.OrdinalIgnoreCase)) { return TypeKop; }
            return TypeAmis;
        }

        // Les deux sortes de kop se comportent pareil : pas de plafond, pas de
        // medaille, un seul par joueur. Seul le texte de partage differe.
        private static bool IsKop(string? type)
        {
            string t = (type ?? "").Trim();
            return string.Equals(t, TypeKop, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, TypeKopMedia, StringComparison.OrdinalIgnoreCase);
        }

        // Bascule un kop en kop de partenaire, et l'inverse. Appelee depuis
        // Swagger par Antoine, une fois par partenaire.
        public async Task<GroupResult> SetTypeAsync(Guid groupId, string type)
        {
            string src = "GroupService.SetTypeAsync";
            string voulu = NormalizeType(type);

            var group = await dbContext.Groups
                .Include(i => i.Members)
                .FirstOrDefaultAsync(w => w.Id.Equals(groupId));
            if (group == null) { throw BaseException.NotFound(-1, src); }

            group.Type = voulu;
            await dbContext.SaveChangesAsync();

            return new GroupResult
            {
                Id = group.Id,
                Name = group.Name,
                InviteCode = group.InviteCode,
                CreatedDate = group.CreatedDate,
                MemberCount = group.Members.Count,
                CreatorId = group.CreatorId,
                Type = group.Type
            };
        }

        // Un joueur n'appartient qu'a un seul kop a la fois. Pour en changer, il doit
        // quitter le sien d'abord.
        private async Task EnsureNoOtherKopAsync(Guid userId, string src)
        {
            bool dejaDansUnKop = await dbContext.GroupMembers
                .AnyAsync(w => w.UserId.Equals(userId) && (w.Group.Type == TypeKop || w.Group.Type == TypeKopMedia));
            if (dejaDansUnKop) { throw BaseException.AlreadyInDb(-9, src); }
        }

        public class CreateGroupInput
        {
            public string Name { get; set; } = null!;

            // "amis" (par defaut) ou "kop". Absent ou inconnu : groupe d'amis.
            public string? Type { get; set; }
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

            // "amis" ou "kop".
            public string Type { get; set; } = TypeAmis;
        }

        public class GroupMemberRankingResult
        {
            public Guid UserId { get; set; }
            public string DisplayName { get; set; } = null!;
            public double TotalScore { get; set; }
            public int MatchesPlayed { get; set; }
            public double ExpertCoef { get; set; }
            public int Rank { get; set; }

            // Le rang avant le dernier match joué. 0 = pas de mouvement à montrer.
            public int RankBefore { get; set; }
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
            public string Type { get; set; } = TypeAmis;

            // Le site s'en sert pour n'offrir le renommage qu'à celui qui a créé
            // le groupe. La vérification est refaite côté serveur : un identifiant
            // dans l'adresse ne prouve rien.
            public Guid CreatorId { get; set; }
            public DateTime CreatedDate { get; set; }
            public bool IsSeasonComplete { get; set; }
            public int MatchesCounted { get; set; }
            public int MatchesPlayed { get; set; }
            public int SeasonLength { get; set; }
            public int CycleNumber { get; set; }
            public string? ChampionName { get; set; } = null;
            public List<GroupMemberRankingResult> Ranking { get; set; } = new();

            // La meilleure note jamais obtenue par un membre du groupe, avec la
            // forme de ce match-là. Null tant que personne n'a de note.
            public GroupRecordResult? Record { get; set; }
        }

        public class GroupRecordResult
        {
            public string DisplayName { get; set; } = null!;
            public double Score { get; set; }
            public string? MatchLabel { get; set; }
            public DateTime MatchDate { get; set; }

            // Les six axes, dans l'ordre de l'hexagone.
            public double Composition { get; set; }
            public double Score6 { get; set; }
            public double Possession { get; set; }
            public double Shots { get; set; }
            public double Fouls { get; set; }
            public double Crosses { get; set; }
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

            // Le classement cumulé du mini-championnat, après ce match et avant lui.
            // Sert à montrer qui monte et qui descend. 0 = non classé.
            public int? CyclePoints { get; set; }
            public int? CycleRank { get; set; }
            public int? CycleRankBefore { get; set; }
        }

        public class SalonResult
        {
            // Le nombre de joueurs du SITE ENTIER ayant pronostique ce match. C'est le
            // denominateur de « choisi n fois » : dans un groupe de six, « 4/6 » ne dit
            // pas grand-chose ; sur l'ensemble des participants, le chiffre a du poids.
            public int ParticipantCount { get; set; }

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

        // Publique : UserService s'en sert pour savoir s'il fait jour de match.
        public static TimeZoneInfo ParisTimeZoneInfo => ParisTimeZone;

        private static TimeZoneInfo ResolveParisTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"); }
            catch
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time"); }
                catch { return TimeZoneInfo.Utc; }
            }
        }

        public static DateTime ParisToUtc(DateTime parisTime)
        {
            var unspecified = DateTime.SpecifyKind(parisTime, DateTimeKind.Unspecified);
            try { return TimeZoneInfo.ConvertTimeToUtc(unspecified, ParisTimeZone); }
            catch { return unspecified; }
        }

        // Combien de fois chaque joueur de l'effectif a ete choisi sur ce match, et
        // combien de personnes ont pronostique. Le meme resultat pour tous les
        // groupes et tous les membres, donc garde en memoire quelques minutes.
        private async Task<(Dictionary<string, int> Decompte, int Participants)> DecompteDesChoixAsync(
            string teamId, string matchId)
        {
            string cle = teamId + "|" + matchId;

            if (memoireChoix.TryGetValue(cle, out var entree)
                && (DateTime.UtcNow - entree.Heure).TotalSeconds < CHOIX_MEMOIRE_SECONDES)
            {
                return (entree.Decompte, entree.Participants);
            }

            var toutesPredictions = await dbContext.UserMatches
                .Where(w => w.MatchId.Equals(matchId) && w.TeamId.Equals(teamId))
                .Include(i => i.UserPlayerForMatches)
                .ToListAsync();

            int participants = toutesPredictions.Count;

            var decompte = new Dictionary<string, int>();
            foreach (var prediction in toutesPredictions)
            {
                foreach (var pick in prediction.UserPlayerForMatches)
                {
                    if (pick.PersonId == null) { continue; }
                    if (decompte.ContainsKey(pick.PersonId)) { decompte[pick.PersonId] += 1; }
                    else { decompte[pick.PersonId] = 1; }
                }
            }

            memoireChoix[cle] = (DateTime.UtcNow, decompte, participants);
            return (decompte, participants);
        }

        // ===== QUI ETAIT LA, ET QUAND =====
        // Un joueur qui rejoint un groupe aujourd'hui n'a pas a figurer dans le
        // classement des matchs joues avant son arrivee : il n'a rien manque, il
        // n'etait pas la. La table des membres garde la date d'entree de chacun,
        // c'est elle qui fait foi.
        //
        // Le repere est la CLOTURE DES PRONOSTICS, pas le coup d'envoi : celui qui
        // arrive apres la cloture n'a pas pu jouer ce match, il n'en fait donc pas
        // partie. Celui qui arrive avant, meme d'une minute, en fait partie.
        //
        // Les dates d'entree sont enregistrees en heure universelle et les coups
        // d'envoi en heure de Paris : la conversion est indispensable, sans elle
        // deux heures d'ecart fausseraient chaque comparaison.
        private static bool EtaitPresent(GroupMember membre, DateTime coupDEnvoiParis)
        {
            DateTime clotureUtc = ParisToUtc(coupDEnvoiParis.AddHours(-ClotureAvantHeures));
            return membre.DateJoined <= clotureUtc;
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

            string type = NormalizeType(input.Type);
            string name = input.Name.Trim();

            if (IsKop(type))
            {
                if (name.Length > KopNameMaxLength) { throw BaseException.InvalidModel(-5, src); }
                await EnsureNoOtherKopAsync(creatorId, src);
            }

            string inviteCode;
            do { inviteCode = GenerateInviteCode(); }
            while (await dbContext.Groups.AnyAsync(w => w.InviteCode.Equals(inviteCode)));

            var group = new Group
            {
                Id = Guid.NewGuid(),
                Name = name,
                InviteCode = inviteCode,
                Type = type,
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
                CreatorId = group.CreatorId,
                Type = group.Type
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

            // Le plafond des dix ne concerne que les groupes d'amis. Un kop n'en a pas,
            // mais on n'y entre que si l'on n'appartient a aucun autre kop.
            if (IsKop(group.Type))
            {
                await EnsureNoOtherKopAsync(userId, src);
            }
            else if (group.Members.Count >= MaxMembers)
            {
                throw BaseException.InvalidModel(-4, src);
            }

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
                CreatorId = group.CreatorId,
                Type = group.Type
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

            // Le classement se construit deux fois : sur tous les matchs joues, et
            // sur tous sauf le dernier. La comparaison donne le mouvement de chacun.
            List<GroupMemberRankingResult> Construire(List<string> matchsRetenus)
            {
                var pts = memberIds.ToDictionary(k => k, v => 0);
                var nb = memberIds.ToDictionary(k => k, v => 0);

                foreach (var idMatch in matchsRetenus)
                {
                    var notes = predictions
                        .Where(w => w.MatchId.Equals(idMatch) && w.ResultTotal.HasValue)
                        .OrderByDescending(o => o.ResultTotal.Value)
                        .ToList();

                    int r = 0;
                    for (int i = 0; i < notes.Count; i++)
                    {
                        if (i > 0 && notes[i].ResultTotal.Value != notes[i - 1].ResultTotal.Value) { r = i; }
                        int sc = notes.Count - r + 1;
                        if (sc < 2) { sc = 2; }
                        pts[notes[i].UserId] += sc;
                        nb[notes[i].UserId] += 1;
                    }
                }

                // Le departage : la moyenne des notes SUR LES MATCHS DU GROUPE, et non
                // le coefficient expert de la saison. Deux joueurs a egalite de points
                // sont separes par ce qu'ils ont fait dans ce mini-championnat, sur
                // autant de matchs qu'il en a ete joue.
                // Seuls les membres deja presents au dernier match retenu figurent
                // au classement. Celui qui vient d'arriver entrera au prochain match.
                DateTime borne = matchsRetenus.Count > 0
                    ? cycleMatches.Where(w => matchsRetenus.Contains(w.Id))
                                  .Select(s => s.DateTime).Max()
                    : DateTime.MaxValue;

                return group.Members
                    .Where(w => EtaitPresent(w, borne))
                    .Select(m => new GroupMemberRankingResult
                {
                    UserId = m.UserId,
                    DisplayName = m.User.DisplayName ?? "?",
                    TotalScore = pts.ContainsKey(m.UserId) ? pts[m.UserId] : 0,
                    MatchesPlayed = nb.ContainsKey(m.UserId) ? nb[m.UserId] : 0,
                    ExpertCoef = Math.Round(predictions
                        .Where(w => w.UserId.Equals(m.UserId)
                                 && w.ResultTotal.HasValue
                                 && matchsRetenus.Contains(w.MatchId))
                        .Select(s => s.ResultTotal.Value)
                        .DefaultIfEmpty(0)
                        .Average(), 3)
                })
                .OrderByDescending(o => o.TotalScore)
                .ThenByDescending(o => o.ExpertCoef)
                .ThenBy(o => o.DisplayName)
                .ToList();
            }

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

            var idsJoues = playedMatches.Select(s => s.Id).ToList();
            var ranking = Construire(idsJoues);
            for (int i = 0; i < ranking.Count; i++) { ranking[i].Rank = i + 1; }

            // Le rang d'avant le dernier match joue. Aucun mouvement tant qu'il n'y
            // a qu'un match : personne n'avait de place auparavant.
            if (idsJoues.Count > 1)
            {
                var avant = Construire(idsJoues.Take(idsJoues.Count - 1).ToList());
                var rangsAvant = new Dictionary<Guid, int>();
                for (int i = 0; i < avant.Count; i++) { rangsAvant[avant[i].UserId] = i + 1; }

                foreach (var r in ranking)
                {
                    r.RankBefore = rangsAvant.ContainsKey(r.UserId) ? rangsAvant[r.UserId] : 0;
                }
            }

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
                Type = group.Type,
                CreatorId = group.CreatorId,
                CreatedDate = group.CreatedDate,
                IsSeasonComplete = isComplete,
                MatchesCounted = cycleMatches.Count,
                MatchesPlayed = playedMatches.Count,
                SeasonLength = SeasonLength,
                CycleNumber = cycleIndex + 1,
                ChampionName = championName,
                Ranking = ranking,
                Record = await RecordDuGroupeAsync(memberIds, group.Members)
            };
        }

        // Le record du groupe : la meilleure note d'un match, tous membres et tous
        // matchs confondus. Elle ne dépend pas du cycle en cours — un record n'a
        // pas de date de péremption.
        private async Task<GroupRecordResult?> RecordDuGroupeAsync(
            List<Guid> memberIds, ICollection<GroupMember> membres)
        {
            var meilleur = await dbContext.UserMatches
                .Where(w => memberIds.Contains(w.UserId) && w.ResultTotal.HasValue)
                .Include(i => i.Match).ThenInclude(m => m.HomeTeam).ThenInclude(t => t.Team)
                .Include(i => i.Match).ThenInclude(m => m.AwayTeam).ThenInclude(t => t.Team)
                .OrderByDescending(o => o.ResultTotal.Value)
                .FirstOrDefaultAsync();

            if (meilleur == null) { return null; }

            var membre = membres.FirstOrDefault(f => f.UserId.Equals(meilleur.UserId));
            string domicile = meilleur.Match?.HomeTeam?.Team?.OfficialName ?? "";
            string exterieur = meilleur.Match?.AwayTeam?.Team?.OfficialName ?? "";

            return new GroupRecordResult
            {
                DisplayName = membre != null ? (membre.User.DisplayName ?? "?") : "?",
                Score = Math.Round(meilleur.ResultTotal.Value, 3),
                MatchLabel = domicile + " — " + exterieur,
                MatchDate = meilleur.Match != null ? meilleur.Match.DateTime : DateTime.MinValue,
                Composition = Math.Round(meilleur.ResultTeamCompositionFormula ?? 0, 3),
                Score6 = Math.Round(meilleur.ResultTeamScoreFormula ?? 0, 3),
                Possession = Math.Round(meilleur.ResultTeamPossessionFormula ?? 0, 3),
                Shots = Math.Round(meilleur.ResultTeamShotsFormula ?? 0, 3),
                Fouls = Math.Round(meilleur.ResultTeamFoulsFormula ?? 0, 3),
                Crosses = Math.Round(meilleur.ResultTeamCrossesFormula ?? 0, 3)
            };
        }


        // --- Le palmares d'un joueur ---
        // Rejoue tous les cycles CLOS de tous ses groupes et en deduit les medailles.
        // Rien n'est stocke : le decoupage en cycles est deterministe, donc le palmares
        // se recalcule a la demande. Un seuil qui bouge se repercute partout, et aucune
        // ecriture irreversible n'est faite.
        public class MedalResult
        {
            public Guid GroupId { get; set; }
            public string GroupName { get; set; } = null!;
            public int CycleNumber { get; set; }
            public DateTime EndDate { get; set; }
            public int MemberCount { get; set; }
            public int Points { get; set; }
            public int Rank { get; set; }
            public bool IsChampion { get; set; }
            public int MetalLevel { get; set; }
            public string? MetalName { get; set; }
            public int MetalValue { get; set; }

            // Dans un groupe de deux, le pseudo de l'autre : le message de fin de
            // cycle le nomme, qu'on ait gagne ou perdu.
            public string? OpponentName { get; set; }
        }

        public class TrophiesResult
        {
            public List<MedalResult> Medals { get; set; } = new();
            public int[] MedalCounts { get; set; } = new int[5];
            public int TotalPoints { get; set; }
            public int CupsCompleted { get; set; }
            public int CupProgress { get; set; }
            public int CupTarget { get; set; }

            // Le premier palier, pour dire a celui qui n'a rien eu combien il
            // lui manquait. Ecrit ici plutot que recopie dans le site.
            public int BronzeThreshold { get; set; }
        }

        private static int MetalLevelFor(int points)
        {
            for (int i = MedalThresholds.Length - 1; i >= 0; i--)
            {
                if (points >= MedalThresholds[i]) { return i + 1; }
            }
            return 0;
        }

        public async Task<TrophiesResult> TrophiesAsync(Guid userId)
        {
            var result = new TrophiesResult
            {
                CupTarget = CupTarget,
                BronzeThreshold = MedalThresholds[0]
            };

            // Les kops sont ecartes : on n'y gagne ni point ni medaille.
            var groups = await dbContext.Groups
                .Include(i => i.Members).ThenInclude(i => i.User)
                .Where(w => w.Members.Any(a => a.UserId.Equals(userId)) && w.Type != TypeKop && w.Type != TypeKopMedia)
                .ToListAsync();

            DateTime now = DateTime.UtcNow;

            foreach (var group in groups)
            {
                var allMatches = await dbContext.Matches
                    .Where(w => w.DateTime >= group.CreatedDate)
                    .OrderBy(o => o.DateTime)
                    .Select(s => new { s.Id, s.Status, s.DateTime })
                    .ToListAsync();

                var memberIds = group.Members.Select(m => m.UserId).ToList();
                var allIds = allMatches.Select(s => s.Id).ToList();
                var entrees = group.Members.ToDictionary(k => k.UserId, v => v.DateJoined);

                var predictions = await dbContext.UserMatches
                    .Where(w => memberIds.Contains(w.UserId) && allIds.Contains(w.MatchId))
                    .Select(s => new { s.UserId, s.MatchId, s.ResultTotal })
                    .ToListAsync();

                for (int c = 0; ; c++)
                {
                    var block = allMatches.Skip(c * SeasonLength).Take(SeasonLength).ToList();
                    if (block.Count < SeasonLength) { break; }
                    if (block.Any(a => !IsPlayed(a.Status))) { break; }
                    if (now < block.Last().DateTime.AddHours(ChampionDisplayHours)) { break; }

                    // Meme bareme que le classement de groupe : ancre sur le dernier present.
                    // Seuls les membres deja presents au dernier match du cycle y figurent.
                    DateTime finDuCycle = block.Last().DateTime;
                    DateTime clotureFinDeCycle = ParisToUtc(finDuCycle.AddHours(-ClotureAvantHeures));
                    var points = memberIds
                        .Where(w => entrees.ContainsKey(w) && entrees[w] <= clotureFinDeCycle)
                        .ToDictionary(k => k, v => 0);
                    foreach (var m in block)
                    {
                        var noted = predictions
                            .Where(w => w.MatchId.Equals(m.Id) && w.ResultTotal.HasValue)
                            .OrderByDescending(o => o.ResultTotal.Value)
                            .ToList();

                        int rank = 0;
                        for (int i = 0; i < noted.Count; i++)
                        {
                            if (i > 0 && noted[i].ResultTotal.Value != noted[i - 1].ResultTotal.Value)
                            { rank = i; }

                            int score = noted.Count - rank + 1;
                            if (score < 2) { score = 2; }
                            if (points.ContainsKey(noted[i].UserId))
                            { points[noted[i].UserId] += score; }
                        }
                    }

                    // Le metal vient des points de chacun, le rang servant de plafond :
                    // personne ne porte le metal de celui qui l'a devance. A egalite de
                    // points, meme metal et le plafond ne descend pas.
                    var ordered = points.OrderByDescending(o => o.Value).ToList();
                    int plafond = MedalThresholds.Length;
                    int prevPoints = int.MinValue;
                    int prevLevel = 0;
                    int position = 0;

                    foreach (var entry in ordered)
                    {
                        position++;
                        int level;
                        if (entry.Value == prevPoints)
                        {
                            level = prevLevel;
                        }
                        else
                        {
                            level = MetalLevelFor(entry.Value);
                            if (level > plafond) { level = plafond; }
                            plafond = level > 0 ? level - 1 : 0;
                        }
                        prevPoints = entry.Value;
                        prevLevel = level;

                        if (group.Members.Count < MinMembersForMedal) { level = 0; }

                        if (!entry.Key.Equals(userId)) { continue; }

                        var medal = new MedalResult
                        {
                            GroupId = group.Id,
                            GroupName = group.Name,
                            CycleNumber = c + 1,
                            EndDate = block.Last().DateTime,
                            MemberCount = group.Members.Count,
                            Points = entry.Value,
                            Rank = position,
                            IsChampion = position == 1 && entry.Value > 0,
                            MetalLevel = level,
                            MetalName = level > 0 ? MedalNames[level - 1] : null,
                            MetalValue = level > 0 ? MedalValues[level - 1] : 0,
                            OpponentName = group.Members.Count == 2
                                ? group.Members.Where(w => !w.UserId.Equals(userId))
                                        .Select(sm => sm.User.DisplayName).FirstOrDefault()
                                : null
                        };
                        result.Medals.Add(medal);
                        if (level > 0) { result.MedalCounts[level - 1]++; }
                        result.TotalPoints += medal.MetalValue;
                    }
                }
            }

            result.Medals = result.Medals.OrderByDescending(o => o.EndDate).ToList();
            result.CupsCompleted = result.TotalPoints / CupTarget;
            result.CupProgress = result.TotalPoints % CupTarget;
            return result;
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
        //   appelant l'adresse à la main, avant tout le monde ;
        // - cette route n'écrit rien. Ni statut, ni note, ni recalcul.
        public async Task<SalonResult> SalonAsync(string teamId, string matchId, Guid userId, Guid groupId)
        {
            string src = "GroupService.SalonAsync";

            if (StringHelper.IsNull(teamId) || StringHelper.IsNull(matchId))
            { throw BaseException.InvalidModel(-1, src); }

            // Le salon deplie le pronostic et la composition de chaque membre : il est
            // taille pour dix personnes. Un kop a sa propre page, et cette route lui
            // est fermee plutot que de servir une reponse de plusieurs centaines de
            // lignes.
            var typeDuGroupe = await dbContext.Groups
                .Where(w => w.Id.Equals(groupId))
                .Select(s => s.Type)
                .FirstOrDefaultAsync();
            if (IsKop(typeDuGroupe)) { throw BaseException.InvalidModel(-9, src); }

            var match = await dbContext.Matches
                .Include(i => i.Place)
                .Include(i => i.MatchDate).ThenInclude(i => i.Calendar).ThenInclude(i => i.Competition)
                .Include(i => i.HomeTeam).ThenInclude(i => i.Team)
                .Include(i => i.HomeTeam).ThenInclude(i => i.PlayerForMatches).ThenInclude(i => i.Person)
                .Include(i => i.AwayTeam).ThenInclude(i => i.Team)
                .Include(i => i.AwayTeam).ThenInclude(i => i.PlayerForMatches).ThenInclude(i => i.Person)
                .FirstOrDefaultAsync(w => w.Id.Equals(matchId));
            if (match == null) { throw BaseException.NotFound(-2, src); }

            // Le verrou. La clôture tombe ClotureAvantHeures avant le coup d'envoi, heure de Paris.
            DateTime clotureUtc = ParisToUtc(match.DateTime.AddHours(-ClotureAvantHeures));
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

            // Seuls ceux qui etaient membres au moment du match. Celui qui a rejoint
            // le groupe depuis n'apparait pas dans le salon d'un match d'avant.
            var membresDuSoir = group.Members
                .Where(w => EtaitPresent(w, match.DateTime))
                .ToList();

            var memberIds = membresDuSoir.Select(s => s.UserId).ToList();

            // Les pronostics des membres sur ce match, avec les onze choisis par chacun.
            var predictions = await dbContext.UserMatches
                .Where(w => w.MatchId.Equals(matchId) &&
                            w.TeamId.Equals(teamId) &&
                            memberIds.Contains(w.UserId))
                .Include(i => i.UserPlayerForMatches).ThenInclude(i => i.Person)
                .ToListAsync();

            // Le dénominateur de « choisi n fois » : tous les joueurs du site ayant
            // pronostiqué ce match, pas seulement les membres du groupe.
            int predictionCount = predictions.Count;

            var (choiceCount, participantCount) = await DecompteDesChoixAsync(teamId, matchId);

            var members = new List<SalonMemberResult>();
            foreach (var member in membresDuSoir)
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

                        // Exactement la formule du moteur, prime de rareté comprise :
                        // la note affichée une heure avant le match est celle qui
                        // sera comptée. CoefRarete est écrit dans UserStatsService
                        // et lu ici, pour qu'il n'existe qu'une seule définition.
                        double note = 0;
                        foreach (var pick in row.Picks)
                        {
                            if (!pick.Found) { continue; }
                            note += UserStatsService.BASE_TITULAIRE
                                  * UserStatsService.CoefRarete(pick.ChoiceCount, participantCount);
                        }
                        row.CompositionNote = Math.Round(note, 4);
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

                // --- Le classement cumulé du mini-championnat, avant et après ---
                // Les matchs du cycle en cours, du plus ancien au plus récent, avec
                // les notes de tous les membres. Le rang « avant » se calcule en
                // rejouant le même barème sans ce match.
                var matchsDepuisCreation = await dbContext.Matches
                    .Where(w => w.DateTime >= group.CreatedDate)
                    .OrderBy(o => o.DateTime)
                    .Select(s => new { s.Id, s.Status })
                    .ToListAsync();

                var idsDuCycle = matchsDepuisCreation
                    .Where(w => IsPlayed(w.Status))
                    .Select(s => s.Id)
                    .ToList();

                var notesDuCycle = (await dbContext.UserMatches
                        .Where(w => memberIds.Contains(w.UserId)
                                 && idsDuCycle.Contains(w.MatchId)
                                 && w.ResultTotal.HasValue)
                        .Select(s => new { s.MatchId, s.UserId, Note = s.ResultTotal.Value })
                        .ToListAsync())
                    .Select(x => (x.MatchId, x.UserId, x.Note));

                var cumulApres = CumulCycle(memberIds, notesDuCycle, null);
                var cumulAvant = CumulCycle(memberIds, notesDuCycle, matchId);

                var rangsApres = RangsDepuisPoints(cumulApres);
                var rangsAvant = RangsDepuisPoints(cumulAvant);

                foreach (var row in members)
                {
                    row.CyclePoints = cumulApres.ContainsKey(row.UserId) ? cumulApres[row.UserId] : 0;
                    row.CycleRank = rangsApres.ContainsKey(row.UserId) ? rangsApres[row.UserId] : 0;

                    // Personne n'avait de rang avant le tout premier match du cycle :
                    // dans ce cas on ne montre aucun mouvement.
                    bool avaitJoue = cumulAvant.ContainsKey(row.UserId) && cumulAvant[row.UserId] > 0;
                    row.CycleRankBefore = avaitJoue && rangsAvant.ContainsKey(row.UserId)
                        ? rangsAvant[row.UserId] : 0;
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
                MemberCount = membresDuSoir.Count,
                PredictionCount = predictionCount,
                ParticipantCount = participantCount,
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

        // Le cumul du mini-championnat en cours, avec le barème positionnel ancré
        // sur le dernier présent. Le même calcul que le classement de groupe, isolé
        // ici pour pouvoir le rejouer en excluant un match — c'est ainsi qu'on
        // connaît le rang d'avant.
        private static Dictionary<Guid, int> CumulCycle(
            List<Guid> memberIds,
            IEnumerable<(string MatchId, Guid UserId, double Note)> notes,
            string? excludeMatchId)
        {
            var points = memberIds.ToDictionary(k => k, v => 0);

            var parMatch = notes
                .Where(w => excludeMatchId == null || !w.MatchId.Equals(excludeMatchId))
                .GroupBy(g => g.MatchId);

            foreach (var m in parMatch)
            {
                var classees = m.OrderByDescending(o => o.Note).ToList();
                int rang = 0;
                for (int i = 0; i < classees.Count; i++)
                {
                    if (i > 0 && classees[i].Note != classees[i - 1].Note) { rang = i; }
                    int score = classees.Count - rang + 1;
                    if (score < 2) { score = 2; }
                    if (points.ContainsKey(classees[i].UserId)) { points[classees[i].UserId] += score; }
                }
            }
            return points;
        }

        private static Dictionary<Guid, int> RangsDepuisPoints(Dictionary<Guid, int> points)
        {
            var ordre = points.OrderByDescending(o => o.Value).Select(s => s.Key).ToList();
            var rangs = new Dictionary<Guid, int>();

            int rang = 0;
            for (int i = 0; i < ordre.Count; i++)
            {
                if (i == 0 || points[ordre[i]] != points[ordre[i - 1]]) { rang = i + 1; }
                rangs[ordre[i]] = rang;
            }
            return rangs;
        }

        // --- Ce qui s'est passe pour un joueur sur un match ---
        // Alimente les messages affiches avec sa note. La route ne renvoie que des
        // faits ; c'est le site qui les met en phrases. Elle n'ecrit rien.

        public class PodiumResult
        {
            public Guid GroupId { get; set; }
            public string GroupName { get; set; } = null!;
            public int MemberCount { get; set; }
            public int Rank { get; set; }

            // Rempli seulement pour un groupe de deux : le duel a son propre texte.
            public string? OpponentName { get; set; }

            // Nombre de places manquees pour entrer dans la derniere place honoree
            // du groupe. 0 quand le joueur y est deja.
            public int PlacesFromPodium { get; set; }

            // Ecart de note avec le dernier joueur honore, quand il est proche.
            public double PointsFromPodium { get; set; }
        }

        public class MatchEventsResult
        {
            public double Score { get; set; }
            public bool HasScore { get; set; }

            // Premiere note de sa vie : ne se produit qu'une fois.
            public bool IsFirstScoredMatch { get; set; }

            // Meilleure note de sa vie, ce match compris.
            public bool IsPersonalRecord { get; set; }

            // Classement general avant et apres ce match. 0 = non classe.
            public int GeneralRankBefore { get; set; }
            public int GeneralRankAfter { get; set; }

            // Un par groupe d'amis, le mieux classe d'abord.
            public List<PodiumResult> Podiums { get; set; } = new();

            // --- Le verdict du match ---
            // Le site compare la note du joueur a la mediane de tous les joueurs
            // notes sur ce match, et cite sa meilleure ou sa pire categorie. Ici on
            // ne fait que fournir les chiffres : les seuils et les phrases sont au site.
            public int ScoredCount { get; set; }
            public double MatchMedian { get; set; }

            // Son rang sur ce match parmi tous les joueurs notes, 1 = meilleure note.
            public int MatchRank { get; set; }

            // Cle de categorie : composition, score, possession, shots, fouls, crosses.
            public string? BestCategory { get; set; }
            public double BestCategoryNote { get; set; }
            public string? WorstCategory { get; set; }
            public double WorstCategoryNote { get; set; }
        }

        // Mediane d'une liste de notes : la valeur du milieu, ou la moyenne des deux
        // du milieu quand il y en a un nombre pair. 0 pour une liste vide.
        private static double Mediane(List<double> notes)
        {
            if (notes == null || notes.Count == 0) { return 0; }
            var triees = notes.OrderBy(o => o).ToList();
            int n = triees.Count;
            return (n % 2 == 1)
                ? triees[n / 2]
                : (triees[n / 2 - 1] + triees[n / 2]) / 2.0;
        }

        // Le rang a partir duquel on felicite, selon la taille du groupe.
        // 1er partout des 3 membres, 2e a partir de 5, 3e a partir de 7.
        private static int PodiumDepthFor(int memberCount)
        {
            if (memberCount < 3) { return 0; }
            if (memberCount < 5) { return 1; }
            if (memberCount < 7) { return 2; }
            return 3;
        }

        public async Task<MatchEventsResult> MatchEventsAsync(string teamId, string matchId, Guid userId)
        {
            string src = "GroupService.MatchEventsAsync";
            if (StringHelper.IsNull(teamId) || StringHelper.IsNull(matchId))
            { throw BaseException.InvalidModel(-1, src); }

            var result = new MatchEventsResult();

            // --- Sa note sur ce match, et son histoire ---
            var siennes = await dbContext.UserMatches
                .Where(w => w.UserId.Equals(userId) && w.TeamId.Equals(teamId) && w.ResultTotal.HasValue)
                .Select(s => new { s.MatchId, Note = s.ResultTotal.Value })
                .ToListAsync();

            var laSienne = siennes.FirstOrDefault(f => f.MatchId.Equals(matchId));
            if (laSienne == null) { return result; }

            result.HasScore = true;
            result.Score = Math.Round(laSienne.Note, 3);
            result.IsFirstScoredMatch = siennes.Count == 1;
            result.IsPersonalRecord = siennes.Count > 1
                && siennes.All(a => a.MatchId.Equals(matchId) || a.Note < laSienne.Note);

            // --- Le verdict : la mediane du match et ses categories extremes ---
            var toutesLesNotes = await dbContext.UserMatches
                .Where(w => w.MatchId.Equals(matchId) && w.TeamId.Equals(teamId) && w.ResultTotal.HasValue)
                .Select(s => s.ResultTotal.Value)
                .ToListAsync();
            result.ScoredCount = toutesLesNotes.Count;
            result.MatchMedian = Math.Round(Mediane(toutesLesNotes), 3);
            result.MatchRank = toutesLesNotes.Count(c => c > laSienne.Note) + 1;

            var sonProno = await dbContext.UserMatches
                .Where(w => w.UserId.Equals(userId) && w.MatchId.Equals(matchId) && w.TeamId.Equals(teamId))
                .Select(s => new
                {
                    Composition = s.ResultTeamCompositionFormula,
                    Score = s.ResultTeamScoreFormula,
                    Possession = s.ResultTeamPossessionFormula,
                    Shots = s.ResultTeamShotsFormula,
                    Fouls = s.ResultTeamFoulsFormula,
                    Crosses = s.ResultTeamCrossesFormula
                })
                .FirstOrDefaultAsync();

            if (sonProno != null)
            {
                // Les memes notes que celles du detail par categorie sur l'ecran des resultats.
                var categories = new List<KeyValuePair<string, double?>>
                {
                    new("composition", sonProno.Composition),
                    new("score", sonProno.Score),
                    new("possession", sonProno.Possession),
                    new("shots", sonProno.Shots),
                    new("fouls", sonProno.Fouls),
                    new("crosses", sonProno.Crosses)
                };
                var connues = categories.Where(c => c.Value.HasValue).ToList();
                if (connues.Count > 0)
                {
                    var meilleure = connues.OrderByDescending(o => o.Value.Value).First();
                    var pire = connues.OrderBy(o => o.Value.Value).First();
                    result.BestCategory = meilleure.Key;
                    result.BestCategoryNote = Math.Round(meilleure.Value.Value, 3);
                    result.WorstCategory = pire.Key;
                    result.WorstCategoryNote = Math.Round(pire.Value.Value, 3);
                }
            }

            // --- Le classement general, avant et apres ---
            var apres = await userService.ExpertCoefAllAsync(teamId);
            var avant = await userService.ExpertCoefAllAsync(teamId, matchId);

            result.GeneralRankAfter = RangDans(apres, userId);
            result.GeneralRankBefore = RangDans(avant, userId);

            // --- Sa place dans chacun de ses groupes d'amis sur ce match ---
            var groupes = await dbContext.Groups
                .Include(i => i.Members).ThenInclude(i => i.User)
                .Where(w => w.Members.Any(a => a.UserId.Equals(userId)) && w.Type != TypeKop && w.Type != TypeKopMedia)
                .ToListAsync();

            // La date du match sert de repere : un membre arrive depuis n'a pas
            // sa place dans le classement de ce soir-la.
            var dateDuMatch = await dbContext.Matches
                .Where(w => w.Id.Equals(matchId))
                .Select(s => s.DateTime)
                .FirstOrDefaultAsync();

            foreach (var g in groupes)
            {
                var membresDuSoir = g.Members.Where(w => EtaitPresent(w, dateDuMatch)).ToList();
                if (!membresDuSoir.Any(a => a.UserId.Equals(userId))) { continue; }

                var membreIds = membresDuSoir.Select(m => m.UserId).ToList();

                var notes = await dbContext.UserMatches
                    .Where(w => w.MatchId.Equals(matchId) && w.TeamId.Equals(teamId)
                             && membreIds.Contains(w.UserId) && w.ResultTotal.HasValue)
                    .Select(s => new { s.UserId, Note = s.ResultTotal.Value })
                    .ToListAsync();

                var classees = notes.OrderByDescending(o => o.Note).ToList();
                int rang = classees.FindIndex(f => f.UserId.Equals(userId)) + 1;
                if (rang == 0) { continue; }

                // Un classement a un joueur n'en est pas un. Tant qu'un seul membre
                // du groupe est note sur ce match, on ne dit rien : sans ce garde-fou,
                // un duel ou l'adversaire n'a pas joue annonce « tu as battu X », et
                // un groupe de huit ou l'on est seul annonce « meilleure note ».
                if (classees.Count < 2) { continue; }

                int profondeur = PodiumDepthFor(g.Members.Count);
                var podium = new PodiumResult
                {
                    GroupId = g.Id,
                    GroupName = g.Name,
                    MemberCount = membresDuSoir.Count,
                    Rank = rang,
                    OpponentName = membresDuSoir.Count == 2
                        ? membresDuSoir.Where(w => !w.UserId.Equals(userId))
                                       .Select(sm => sm.User.DisplayName).FirstOrDefault()
                        : null
                };

                if (profondeur > 0 && rang > profondeur)
                {
                    podium.PlacesFromPodium = rang - profondeur;
                    if (classees.Count >= profondeur)
                    {
                        podium.PointsFromPodium =
                            Math.Round(classees[profondeur - 1].Note - laSienne.Note, 3);
                    }
                }

                result.Podiums.Add(podium);
            }

            result.Podiums = result.Podiums.OrderBy(o => o.Rank).ToList();
            return result;
        }

        private static int RangDans(Dictionary<Guid, double> coefs, Guid userId)
        {
            if (!coefs.ContainsKey(userId)) { return 0; }
            return coefs.OrderByDescending(o => o.Value)
                        .Select(s => s.Key)
                        .ToList()
                        .IndexOf(userId) + 1;
        }

        // --- La page d'un kop de supporters ---
        // Trois blocs : le prono collectif du kop face au reel, les meilleurs du dernier
        // match, et le classement des membres au coefficient expert avec leur rang
        // general. Consultable sans compte : userId est facultatif.
        // Cette route n'ecrit rien.

        public class KopPlayerResult
        {
            public string Id { get; set; } = null!;
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public int ChoiceCount { get; set; }
            public bool Found { get; set; }
        }

        public class KopPronoResult
        {
            // Le nombre de membres ayant reellement pronostique ce match.
            public int PredictionCount { get; set; }

            // Le onze le plus choisi par le kop, du plus consensuel au moins consensuel.
            public List<KopPlayerResult> TopEleven { get; set; } = new();

            public double TeamPossession { get; set; }
            public double OpponentPossession { get; set; }
            public double TeamShots { get; set; }
            public double OpponentShots { get; set; }
            public double TeamFouls { get; set; }
            public double OpponentFouls { get; set; }
            public double TeamCrosses { get; set; }
            public double OpponentCrosses { get; set; }

            // Le score le plus souvent pronostique, et par combien de membres.
            public int TopTeamScore { get; set; }
            public int TopOpponentScore { get; set; }
            public int TopScoreCount { get; set; }
        }

        public class KopMatchRowResult
        {
            public Guid UserId { get; set; }
            public string DisplayName { get; set; } = null!;
            public double Score { get; set; }
            public int Rank { get; set; }
            public bool IsMe { get; set; }
        }

        // Les six memes axes que l'ecran « Dernier resultat », dans le meme ordre.
        public class KopHexaSixResult
        {
            public double Composition { get; set; }
            public double Score { get; set; }
            public double Possession { get; set; }
            public double Shots { get; set; }
            public double Fouls { get; set; }
            public double Crosses { get; set; }
        }

        public class KopHexaResult
        {
            public KopHexaSixResult Me { get; set; } = new();
            public KopHexaSixResult Kop { get; set; } = new();
            public double MyTotal { get; set; }
            public double KopTotal { get; set; }
            public int ScoredCount { get; set; }
        }

        public class KopRankRowResult
        {
            public Guid UserId { get; set; }
            public string DisplayName { get; set; } = null!;
            public double ExpertCoef { get; set; }
            public int Rank { get; set; }
            public int GeneralRank { get; set; }
            public int MatchesPlayed { get; set; }
            public bool IsMe { get; set; }

            // Le rang dans le kop avant le dernier match note. 0 = rien a montrer.
            public int RankBefore { get; set; }
        }

        public class KopResult
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = null!;
            public string InviteCode { get; set; } = null!;
            public int MemberCount { get; set; }
            public bool IsMember { get; set; }

            // "kop" ou "kopmedia" : le site s'en sert pour choisir le texte de
            // partage. Oublie a la premiere ecriture, d'ou un partage qui
            // proposait toujours le message des amis.
            public string Type { get; set; } = TypeKop;

            // L'affiche du match sur lequel porte le bloc 1, et son etat :
            // "none" (aucun match cloture), "closed", "composition", "results".
            public string? MatchId { get; set; }
            public string? MatchLabel { get; set; }

            // L'adversaire seul, pour le titre « Les pronos du kop face à X ».
            public string? OpponentName { get; set; }
            public string MatchState { get; set; } = "none";

            public KopPronoResult? Prono { get; set; }
            public SalonRealResult? Real { get; set; }

            // Le onze officiel, celui du coach. Vide tant qu'il n'est pas saisi.
            public List<KopPlayerResult> OfficialEleven { get; set; } = new();

            public List<KopMatchRowResult> LastMatch { get; set; } = new();

            // Les six notes de categorie du dernier match note : celles du joueur et
            // la moyenne du kop. C'est ce que l'hexagone compare. Null s'il n'y a
            // rien a comparer — pas de note, ou personne d'autre dans le kop.
            public KopHexaResult? Hexa { get; set; }
            public List<KopRankRowResult> Ranking { get; set; } = new();
        }

        public async Task<KopResult> KopAsync(string teamId, Guid groupId, Guid? userId)
        {
            string src = "GroupService.KopAsync";
            if (StringHelper.IsNull(teamId)) { throw BaseException.InvalidModel(-1, src); }

            var group = await dbContext.Groups
                .Include(i => i.Members).ThenInclude(i => i.User)
                .FirstOrDefaultAsync(w => w.Id.Equals(groupId));
            if (group == null) { throw BaseException.NotFound(-2, src); }
            if (!IsKop(group.Type)) { throw BaseException.InvalidModel(-3, src); }

            var memberIds = group.Members.Select(m => m.UserId).ToList();

            var result = new KopResult
            {
                Id = group.Id,
                Name = group.Name,
                InviteCode = group.InviteCode,
                MemberCount = group.Members.Count,
                IsMember = userId.HasValue && memberIds.Contains(userId.Value),
                Type = group.Type
            };

            // --- Bloc 3 : le classement du kop au coefficient expert ---
            var coefs = await userService.ExpertCoefAllAsync(teamId);

            var comptes = await dbContext.UserMatches
                .Where(w => w.TeamId.Equals(teamId) && w.ResultTotal.HasValue && memberIds.Contains(w.UserId))
                .GroupBy(gb => gb.UserId)
                .Select(g => new { UserId = g.Key, Nb = g.Count() })
                .ToListAsync();

            // Le rang general se lit sur le classement complet, pas sur celui du kop :
            // etre 1er de son kop et 43e du jeu, c'est deux informations, pas une.
            var classementGeneral = coefs
                .OrderByDescending(o => o.Value)
                .Select(s => s.Key)
                .ToList();

            result.Ranking = group.Members
                .Select(m => new KopRankRowResult
                {
                    UserId = m.UserId,
                    DisplayName = m.User.DisplayName ?? "?",
                    ExpertCoef = coefs.ContainsKey(m.UserId) ? coefs[m.UserId] : 0,
                    MatchesPlayed = comptes.Where(w => w.UserId.Equals(m.UserId)).Select(s => s.Nb).FirstOrDefault(),
                    GeneralRank = classementGeneral.IndexOf(m.UserId) + 1,
                    IsMe = userId.HasValue && m.UserId.Equals(userId.Value)
                })
                .OrderByDescending(o => o.ExpertCoef)
                .ThenBy(o => o.DisplayName)
                .ToList();

            for (int i = 0; i < result.Ranking.Count; i++) { result.Ranking[i].Rank = i + 1; }

            // Le mouvement depuis le dernier match note : on rejoue le classement du
            // kop en retirant ce match, et on compare les deux ordres. Meme methode
            // que le classement general.
            var dernierNote = await dbContext.UserMatches
                .Where(w => w.TeamId.Equals(teamId) && w.ResultTotal.HasValue)
                .Include(i => i.Match)
                .OrderByDescending(o => o.Match.DateTime)
                .Select(s => s.MatchId)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrEmpty(dernierNote))
            {
                var coefsAvant = await userService.ExpertCoefAllAsync(teamId, dernierNote);
                var ordreAvant = group.Members
                    .Select(m => new
                    {
                        m.UserId,
                        Coef = coefsAvant.ContainsKey(m.UserId) ? coefsAvant[m.UserId] : 0
                    })
                    .Where(w => w.Coef > 0)
                    .OrderByDescending(o => o.Coef)
                    .Select(s => s.UserId)
                    .ToList();

                foreach (var ligne in result.Ranking)
                {
                    int pos = ordreAvant.IndexOf(ligne.UserId);
                    ligne.RankBefore = pos >= 0 ? pos + 1 : 0;
                }
            }

            // --- Le match de reference : le dernier dont les pronostics sont fermes ---
            DateTime now = DateTime.UtcNow;
            var matchs = await dbContext.Matches
                .OrderByDescending(o => o.DateTime)
                .Take(40)
                .ToListAsync();

            var courant = matchs.FirstOrDefault(f => ParisToUtc(f.DateTime.AddHours(-ClotureAvantHeures)) <= now);
            if (courant == null) { return result; }

            var match = await dbContext.Matches
                .Include(i => i.HomeTeam).ThenInclude(i => i.Team)
                .Include(i => i.HomeTeam).ThenInclude(i => i.PlayerForMatches).ThenInclude(i => i.Person)
                .Include(i => i.AwayTeam).ThenInclude(i => i.Team)
                .Include(i => i.AwayTeam).ThenInclude(i => i.PlayerForMatches).ThenInclude(i => i.Person)
                .Include(i => i.Place)
                .Include(i => i.MatchDate).ThenInclude(i => i.Calendar).ThenInclude(i => i.Competition)
                .FirstOrDefaultAsync(w => w.Id.Equals(courant.Id));
            if (match == null) { return result; }

            var detail = BaseMatchResult.FromDb(match);
            bool teamIsHome = detail.HomeTeam != null && detail.HomeTeam.Id != null &&
                              detail.HomeTeam.Id.Equals(teamId, StringComparison.OrdinalIgnoreCase);
            var teamSide = teamIsHome ? detail.HomeTeam : detail.AwayTeam;
            var opponentSide = teamIsHome ? detail.AwayTeam : detail.HomeTeam;
            if (teamSide == null || opponentSide == null) { return result; }

            result.MatchId = match.Id;
            result.MatchLabel = (detail.HomeTeam != null ? detail.HomeTeam.Name : "?")
                              + " — " + (detail.AwayTeam != null ? detail.AwayTeam.Name : "?");
            result.OpponentName = opponentSide.Name;

            var officialPlayers = teamSide.Players
                .Where(w => !(w.Position != null && w.Position.Trim().Equals("SUBSTITUTE", StringComparison.OrdinalIgnoreCase)))
                .ToList();
            bool hasComposition = officialPlayers.Count > 0;
            var officialIds = officialPlayers.Where(w => w.Id != null).Select(s => s.Id).ToList();
            bool scored = IsScored(match.Status);

            result.MatchState = scored ? "results" : (hasComposition ? "composition" : "closed");

            foreach (var player in officialPlayers)
            {
                if (player.Id == null) { continue; }
                result.OfficialEleven.Add(new KopPlayerResult
                {
                    Id = player.Id,
                    FirstName = player.FirstName,
                    LastName = player.LastName,
                    ChoiceCount = 0,
                    Found = true
                });
            }

            // --- Bloc 1 : le prono du kop ---
            var predictions = await dbContext.UserMatches
                .Where(w => w.MatchId.Equals(match.Id) &&
                            w.TeamId.Equals(teamId) &&
                            memberIds.Contains(w.UserId))
                .Include(i => i.UserPlayerForMatches).ThenInclude(i => i.Person)
                .ToListAsync();

            if (predictions.Count > 0)
            {
                var prono = new KopPronoResult { PredictionCount = predictions.Count };

                var choix = new Dictionary<string, int>();
                var personnes = new Dictionary<string, UserPlayerForMatch>();
                foreach (var prediction in predictions)
                {
                    foreach (var pick in prediction.UserPlayerForMatches)
                    {
                        if (pick.PersonId == null) { continue; }
                        if (choix.ContainsKey(pick.PersonId)) { choix[pick.PersonId] += 1; }
                        else { choix[pick.PersonId] = 1; personnes[pick.PersonId] = pick; }
                    }
                }

                prono.TopEleven = choix
                    .OrderByDescending(o => o.Value)
                    .Take(11)
                    .Select(e => new KopPlayerResult
                    {
                        Id = e.Key,
                        FirstName = personnes[e.Key].Person != null ? personnes[e.Key].Person.FirstName : null,
                        LastName = personnes[e.Key].Person != null ? personnes[e.Key].Person.LastName : null,
                        ChoiceCount = e.Value,
                        Found = officialIds.Contains(e.Key)
                    })
                    .ToList();

                // Le terrain colore le onze d'Enrique selon ce que le kop avait choisi :
                // chaque titulaire officiel porte donc son propre decompte.
                foreach (var officiel in result.OfficialEleven)
                {
                    officiel.ChoiceCount = choix.ContainsKey(officiel.Id) ? choix[officiel.Id] : 0;
                }

                prono.TeamPossession = Math.Round(predictions.Average(a => a.PreTeamPossession), 1);
                prono.OpponentPossession = Math.Round(predictions.Average(a => a.PreOpponentPossession), 1);
                prono.TeamShots = Math.Round(predictions.Average(a => (double)a.PreTeamShots), 1);
                prono.OpponentShots = Math.Round(predictions.Average(a => (double)a.PreOpponentShots), 1);
                prono.TeamFouls = Math.Round(predictions.Average(a => (double)a.PreTeamFouls), 1);
                prono.OpponentFouls = Math.Round(predictions.Average(a => (double)a.PreOpponentFouls), 1);
                prono.TeamCrosses = Math.Round(predictions.Average(a => (double)a.PreTeamCrosses), 1);
                prono.OpponentCrosses = Math.Round(predictions.Average(a => (double)a.PreOpponentCrosses), 1);

                // Le score le plus souvent pronostique. A egalite, le plus favorable au PSG.
                var scores = predictions
                    .GroupBy(g => new { g.PreTeamScore, g.PreOpponentScore })
                    .Select(g => new { g.Key.PreTeamScore, g.Key.PreOpponentScore, Nb = g.Count() })
                    .OrderByDescending(o => o.Nb)
                    .ThenByDescending(o => o.PreTeamScore - o.PreOpponentScore)
                    .FirstOrDefault();

                if (scores != null)
                {
                    prono.TopTeamScore = scores.PreTeamScore;
                    prono.TopOpponentScore = scores.PreOpponentScore;
                    prono.TopScoreCount = scores.Nb;
                }

                result.Prono = prono;
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

                // --- Bloc 2 : les meilleurs du dernier match ---
                var notes = predictions
                    .Where(w => w.ResultTotal.HasValue)
                    .OrderByDescending(o => o.ResultTotal.Value)
                    .ToList();

                for (int i = 0; i < notes.Count; i++)
                {
                    var membre = group.Members.FirstOrDefault(f => f.UserId.Equals(notes[i].UserId));
                    result.LastMatch.Add(new KopMatchRowResult
                    {
                        UserId = notes[i].UserId,
                        DisplayName = membre != null ? (membre.User.DisplayName ?? "?") : "?",
                        Score = Math.Round(notes[i].ResultTotal.Value, 3),
                        Rank = i + 1,
                        IsMe = userId.HasValue && notes[i].UserId.Equals(userId.Value)
                    });
                }

                // --- Les six axes de l'hexagone ---
                var mien = userId.HasValue
                    ? notes.FirstOrDefault(f => f.UserId.Equals(userId.Value))
                    : null;

                if (mien != null && notes.Count > 0)
                {
                    static double Moy(List<UserMatch> l, Func<UserMatch, double?> champ)
                    {
                        var v = l.Select(champ).Where(w => w.HasValue).Select(s => s.Value).ToList();
                        return v.Count > 0 ? Math.Round(v.Average(), 3) : 0;
                    }

                    result.Hexa = new KopHexaResult
                    {
                        ScoredCount = notes.Count,
                        MyTotal = Math.Round(mien.ResultTotal.Value, 3),
                        KopTotal = Math.Round(notes.Average(a => a.ResultTotal.Value), 3),
                        Me = new KopHexaSixResult
                        {
                            Composition = Math.Round(mien.ResultTeamCompositionFormula ?? 0, 3),
                            Score = Math.Round(mien.ResultTeamScoreFormula ?? 0, 3),
                            Possession = Math.Round(mien.ResultTeamPossessionFormula ?? 0, 3),
                            Shots = Math.Round(mien.ResultTeamShotsFormula ?? 0, 3),
                            Fouls = Math.Round(mien.ResultTeamFoulsFormula ?? 0, 3),
                            Crosses = Math.Round(mien.ResultTeamCrossesFormula ?? 0, 3)
                        },
                        Kop = new KopHexaSixResult
                        {
                            Composition = Moy(notes, n => n.ResultTeamCompositionFormula),
                            Score = Moy(notes, n => n.ResultTeamScoreFormula),
                            Possession = Moy(notes, n => n.ResultTeamPossessionFormula),
                            Shots = Moy(notes, n => n.ResultTeamShotsFormula),
                            Fouls = Moy(notes, n => n.ResultTeamFoulsFormula),
                            Crosses = Moy(notes, n => n.ResultTeamCrossesFormula)
                        }
                    };
                }
            }

            return result;
        }

        // --- Renommer un groupe ---
        // Seul celui qui l'a créé peut le faire. Le nom est le seul champ qui
        // change : ni le code d'invitation, ni les membres, ni le cycle en cours.
        public class RenameModel
        {
            public string Name { get; set; } = null!;
        }

        public async Task<bool> RenameAsync(Guid groupId, Guid userId, RenameModel model)
        {
            string src = "GroupService.RenameAsync";
            if (model == null || StringHelper.IsNull(model.Name))
            { throw BaseException.InvalidModel(-1, src); }

            string nom = model.Name.Trim();
            if (nom.Length < 2 || nom.Length > 30)
            { throw BaseException.InvalidModel(-2, src); }

            var group = await dbContext.Groups.FirstOrDefaultAsync(w => w.Id.Equals(groupId));
            if (group == null) { throw BaseException.NotFound(-3, src); }

            // La vérification qui compte : celle du serveur.
            if (!group.CreatorId.Equals(userId)) { throw BaseException.NotFound(-4, src); }

            group.Name = nom;
            await dbContext.SaveChangesAsync();
            return true;
        }

        // --- Ce que designe un code d'invitation ---
        // Lue a l'atterrissage d'un lien partage, AVANT toute adhesion : le site a
        // besoin de savoir s'il s'agit d'un kop ou d'un groupe d'amis, et sous quel
        // nom, pour proposer d'entrer au lieu de faire entrer d'office.
        // Elle n'ecrit rien et ne revele que ce qui figure deja dans le lien.
        public class GroupByCodeResult
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = null!;
            public string Type { get; set; } = TypeAmis;
            public int MemberCount { get; set; }
            public bool IsFull { get; set; }
            public bool AlreadyMember { get; set; }
        }

        public async Task<GroupByCodeResult> ByCodeAsync(string inviteCode, Guid? userId)
        {
            string src = "GroupService.ByCodeAsync";
            if (StringHelper.IsNull(inviteCode)) { throw BaseException.InvalidModel(-1, src); }

            string code = inviteCode.Trim();

            var group = await dbContext.Groups
                .Include(i => i.Members)
                .FirstOrDefaultAsync(w => w.InviteCode.Equals(code));
            if (group == null) { throw BaseException.NotFound(-2, src); }

            return new GroupByCodeResult
            {
                Id = group.Id,
                Name = group.Name,
                Type = group.Type,
                MemberCount = group.Members.Count,
                IsFull = !IsKop(group.Type) && group.Members.Count >= MaxMembers,
                AlreadyMember = userId.HasValue && group.Members.Any(a => a.UserId.Equals(userId.Value))
            };
        }

        // --- La liste des kops de supporters ---
        // Triee par nombre de membres, du plus gros au plus petit. Les kops d'un seul
        // membre n'y figurent pas : ils s'effacent d'eux-memes sans qu'on ait a les
        // supprimer, et le lien direct de leur fondateur continue de fonctionner.
        public class KopListItemResult
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = null!;
            public string InviteCode { get; set; } = null!;
            public int MemberCount { get; set; }

            // La meilleure note du kop sur le dernier match note, avec son auteur.
            // C'est ce qui distingue un kop vivant d'un kop endormi.
            public double? BestScore { get; set; }
            public string? BestScoreName { get; set; }
        }

        public class KopListResult
        {
            // Le kop du joueur, s'il en a un. Le site s'en sert pour afficher
            // « Quitte ton kop pour en rejoindre un autre » au lieu des boutons.
            public Guid? MyKopId { get; set; }
            public string? MyKopName { get; set; }
            public List<KopListItemResult> Kops { get; set; } = new();
        }

        public async Task<KopListResult> KopListAsync(Guid userId)
        {
            var result = new KopListResult();

            var kops = await dbContext.Groups
                .Include(i => i.Members).ThenInclude(i => i.User)
                .Where(w => w.Type == TypeKop || w.Type == TypeKopMedia)
                .ToListAsync();

            var mien = kops.FirstOrDefault(k => k.Members.Any(m => m.UserId.Equals(userId)));
            if (mien != null)
            {
                result.MyKopId = mien.Id;
                result.MyKopName = mien.Name;
            }

            // Le dernier match note, tous kops confondus : une seule requete pour toute
            // la liste plutot qu'une par kop. Le filtre sur le statut se fait en memoire
            // pour rester coherent avec IsPlayed, qui ignore la casse.
            var dernierMatch = (await dbContext.Matches
                .OrderByDescending(o => o.DateTime)
                .Select(s => new { s.Id, s.Status, s.DateTime })
                .Take(30)
                .ToListAsync())
                .FirstOrDefault(f => IsPlayed(f.Status));

            var notes = new Dictionary<Guid, double>();
            if (dernierMatch != null)
            {
                var tousMembres = kops.SelectMany(k => k.Members).Select(m => m.UserId).Distinct().ToList();
                var brutes = await dbContext.UserMatches
                    .Where(w => tousMembres.Contains(w.UserId)
                             && w.MatchId.Equals(dernierMatch.Id)
                             && w.ResultTotal.HasValue)
                    .Select(s => new { s.UserId, Score = s.ResultTotal.Value })
                    .ToListAsync();

                foreach (var b in brutes) { notes[b.UserId] = b.Score; }
            }

            result.Kops = kops
                .Where(k => k.Members.Count >= KopMinMembersToList)
                .OrderByDescending(k => k.Members.Count)
                .ThenBy(k => k.Name)
                .Select(k =>
                {
                    var item = new KopListItemResult
                    {
                        Id = k.Id,
                        Name = k.Name,
                        InviteCode = k.InviteCode,
                        MemberCount = k.Members.Count
                    };

                    GroupMember? meilleur = null;
                    double meilleureNote = double.MinValue;
                    foreach (var m in k.Members)
                    {
                        if (!notes.ContainsKey(m.UserId)) { continue; }
                        if (notes[m.UserId] > meilleureNote)
                        {
                            meilleureNote = notes[m.UserId];
                            meilleur = m;
                        }
                    }

                    if (meilleur != null)
                    {
                        item.BestScore = meilleureNote;
                        item.BestScoreName = meilleur.User.DisplayName ?? "?";
                    }

                    return item;
                })
                .ToList();

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
                CreatorId = m.Group.CreatorId,
                Type = m.Group.Type
            }).ToList();
        }
    }
}
