using dotnet.core.thegoldenfan;
using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.utils.Helpers;
using dotnet.core.utils.server;
using Microsoft.EntityFrameworkCore;


ProgramHelper.Init();
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureWebServer();
builder.Services.AddTheGoldenFanService();
builder.Services.AddCorsService();
builder.Services.AddJwtService();
builder.Services.AddControllers().AddXmlSerializerFormatters();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerService();


var app = builder.Build();

// --- Mise a niveau de la base au demarrage ---
// La colonne "Type" de la table "Group" distingue les groupes d'amis des kops
// de supporters. IF NOT EXISTS : l'instruction ne fait rien si la colonne est
// deja la, donc elle peut tourner a chaque demarrage sans risque.
// Le try/catch garantit qu'un echec ici n'empeche jamais l'application de
// demarrer : le jeu continue de tourner, seuls les kops seraient indisponibles.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE \"Group\" ADD COLUMN IF NOT EXISTS \"Type\" character varying(20) NOT NULL DEFAULT 'amis';");

        // L'adresse e-mail. Nullable : les joueurs inscrits avant cette version
        // n'en ont pas, on la leur demandera a leur prochaine connexion.
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE \"User\" ADD COLUMN IF NOT EXISTS \"Email\" character varying(320);");
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE \"User\" ADD COLUMN IF NOT EXISTS \"EmailOptIn\" boolean NOT NULL DEFAULT false;");

        // Correction ponctuelle : quelques joueurs n'ont qu'un nom d'usage et ont
        // ete enregistres avec le meme prenom et le meme nom — « Marquinhos
        // Marquinhos », « Vitinha Vitinha ». On vide le prenom.
        // La condition FirstName = LastName ne peut viser personne d'autre, et
        // l'instruction ne trouve plus rien une fois la correction faite : elle
        // peut donc tourner a chaque demarrage sans risque.
        // L'identifiant de la personne n'est pas touche : les pronostics deja
        // enregistres continuent de pointer sur la bonne fiche.
        int corriges = db.Database.ExecuteSqlRaw(
            "UPDATE \"Person\" SET \"FirstName\" = '', \"NormalizedFirstName\" = '', " +
            "\"MatchName\" = \"LastName\", \"NormalizedMatchName\" = \"NormalizedLastName\" " +
            "WHERE \"FirstName\" IS NOT NULL AND \"FirstName\" <> '' AND \"FirstName\" = \"LastName\";");

        // Le rang lateral, de gauche a droite dans la ligne, vu des tribunes.
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE \"Player\" ADD COLUMN IF NOT EXISTS \"PositionOrder\" integer NOT NULL DEFAULT 0;");

        // --- Postes et placement lateral de l'effectif, donnes par Antoine ---
        // Le rang est l'ordre de gauche a droite DANS SA LIGNE, vu des tribunes.
        // Defense : 1 gauche, 2 axe gauche, 3 axe droit, 4 droite.
        // Milieu et attaque : 1 gauche, 2 centre, 3 droite.
        // Le poste et le rang ne servent qu'au dessin du terrain, jamais a la
        // notation : le moteur compte onze noms trouves, sans regarder ou.
        // Chaque instruction ne fait rien si la fiche est deja a jour, donc
        // l'ensemble peut tourner a chaque demarrage.
        var effectif = new (string nom, string poste, int rang)[]
        {
            ("Mendes", "Défenseur", 1), ("Digne", "Défenseur", 1),
            ("Pacho", "Défenseur", 2), ("Hernández", "Défenseur", 2),
            ("Marquinhos", "Défenseur", 3), ("Zabarnyi", "Défenseur", 3),
            ("Hakimi", "Défenseur", 4), ("Boly", "Défenseur", 4),

            ("Ruiz", "Milieu", 1), ("Fernández", "Milieu", 1), ("Mayulu", "Milieu", 1),
            ("Vitinha", "Milieu", 2), ("Beraldo", "Milieu", 2),
            ("Neves", "Milieu", 3), ("Zaïre-Emery", "Milieu", 3),

            ("Kvaratskhelia", "Attaquant", 1), ("Godts", "Attaquant", 1),
            ("Dembélé", "Attaquant", 2), ("Torres", "Attaquant", 2),
            ("Doué", "Attaquant", 3), ("Akliouche", "Attaquant", 3)
        };

        int fiches = 0;
        foreach (var j in effectif)
        {
            fiches += db.Database.ExecuteSqlRaw(
                "UPDATE \"Player\" SET \"Position\" = {1}, \"PositionOrder\" = {2} " +
                "FROM \"Person\" p " +
                "WHERE \"Player\".\"PersonId\" = p.\"Id\" AND p.\"LastName\" = {0} " +
                "AND (\"Player\".\"Position\" IS DISTINCT FROM {1} OR \"Player\".\"PositionOrder\" <> {2});",
                j.nom, j.poste, j.rang);
        }

        Console.WriteLine("[YouProno] Colonnes verifiees. Prenoms corriges : " + corriges
            + ". Fiches joueur mises a jour : " + fiches + ".");
    }
    catch (Exception ex)
    {
        Console.WriteLine("[YouProno] Colonne Group.Type - echec : " + ex.Message);
    }
}

app.UseSwaggerService();
app.UseCors(dotnet.core.utils.server.ConfigureService.CorsOrigins);
app.MapControllers();
app.Run();
