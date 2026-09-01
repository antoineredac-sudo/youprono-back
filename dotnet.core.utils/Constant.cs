using System;
using System.Collections.Generic;

namespace dotnet.core.utils
{
    // Reconstruction : centralise la config lue depuis les variables d'environnement.
    // Le code du jeu attend précisément cette forme (Constant.ENV, Constant.Config.Dbs.ConnectionString["app"]["rw"],
    // Constant.Application.Services["opta"]["key"]) — on la respecte pour ne rien casser ailleurs.
    public static class Constant
    {
        public static string ENV { get; set; } =
            Environment.GetEnvironmentVariable("GOLDENFAN_ENV") ?? "DEV";

        public static class Config
        {
            public static class Dbs
            {
                public static Dictionary<string, Dictionary<string, string>> ConnectionString { get; set; } = new()
                {
                    ["app"] = new Dictionary<string, string>
                    {
                        ["rw"] = Environment.GetEnvironmentVariable("GOLDENFAN_DB_CONNECTION")
                                 ?? "Host=localhost;Port=5432;Database=goldenfan;Username=postgres;Password=goldenfan"
                    }
                };
            }
        }

        public static class Application
        {
            // Le code appelle .DecodeFrom64() sur cette valeur : elle doit donc être stockée en base64.
            public static Dictionary<string, Dictionary<string, string>> Services { get; set; } = new()
            {
                ["opta"] = new Dictionary<string, string>
                {
                    ["key"] = Environment.GetEnvironmentVariable("GOLDENFAN_OPTA_KEY_B64") ?? ""
                }
            };
        }
    }
}
