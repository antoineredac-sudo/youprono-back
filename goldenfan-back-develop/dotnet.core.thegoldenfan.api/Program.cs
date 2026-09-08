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

        // Desire Doue etait enregistre comme milieu : c'est un attaquant.
        // Le poste sert au dessin du terrain, pas a la notation.
        int postes = db.Database.ExecuteSqlRaw(
            "UPDATE \"Player\" SET \"Position\" = 'Attaquant' " +
            "FROM \"Person\" p " +
            "WHERE \"Player\".\"PersonId\" = p.\"Id\" " +
            "AND p.\"LastName\" = 'Doué' AND \"Player\".\"Position\" <> 'Attaquant';");

        Console.WriteLine("[YouProno] Colonnes verifiees. Fiches corrigees : " + corriges
            + ". Postes corriges : " + postes + ".");
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
