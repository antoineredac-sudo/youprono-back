using System;

namespace dotnet.core.utils.Helpers
{
    public static class ProgramHelper
    {
        // Point de démarrage appelé tout au début de Program.cs.
        // Pour l'instant : juste une trace au démarrage. On pourra enrichir plus tard
        // (chargement d'un fichier .env, configuration des logs, etc.) si besoin.
        public static void Init()
        {
            Console.WriteLine($"[Goldenfan] Démarrage — environnement: {Constant.ENV}");
        }
    }
}
