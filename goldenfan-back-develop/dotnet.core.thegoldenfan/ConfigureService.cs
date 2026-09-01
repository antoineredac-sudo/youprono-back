using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.thegoldenfan.Services;
using dotnet.core.thegoldenfan.Services.Opta;
using dotnet.core.utils;
using dotnet.core.utils.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;


namespace dotnet.core.thegoldenfan
{
    public static class ConfigureService
    {
        public static IServiceCollection AddTheGoldenFanService(this IServiceCollection services)
        {
            if (Constant.ENV.Equals("PROD"))
            {
                services.AddDbContext<AppDbContext>(opts =>
                {
                    opts.UseNpgsql(Constant.Config.Dbs.ConnectionString["app"]["rw"].DecodeFrom64());
                });
            }
            else
            {
                services.AddDbContext<AppDbContext>(opts =>
                {
                    opts.UseNpgsql(Constant.Config.Dbs.ConnectionString["app"]["rw"]);
                    opts.EnableSensitiveDataLogging();
                    opts.EnableDetailedErrors();
                });
            }


            services.AddTransient<OptaMatchStatsService>();
            services.AddTransient<OptaSquadService>();
            services.AddTransient<OptaTeamService>();
            services.AddTransient<OptaTournamentCalendarService>();
            services.AddTransient<OptaTournamentScheduleService>();


            services.AddTransient<DatabaseService>();

            services.AddTransient<GroupService>();

            services.AddTransient<MatchService>();
            services.AddTransient<SubscriptionService>();
            services.AddTransient<TeamService>();
            services.AddTransient<UserService>();
            services.AddTransient<UserMatchService>();
            services.AddTransient<UserStatsService>();


            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            return services;
        }
    }
}
