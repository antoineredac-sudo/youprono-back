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

        Console.WriteLine("[YouProno] Colonnes Group.Type, User.Email et User.EmailOptIn verifiees.");
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
