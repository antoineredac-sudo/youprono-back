using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace dotnet.core.utils.server
{
    public static class ConfigureService
    {
        // Nom de la politique CORS, réutilisé tel quel dans Program.cs (app.UseCors(...CorsOrigins))
        public static string CorsOrigins { get; set; } = "GoldenfanCors";

        public static IWebHostBuilder ConfigureWebServer(this IWebHostBuilder builder)
        {
            var port = Environment.GetEnvironmentVariable("GOLDENFAN_API_PORT") ?? "5000";
            return builder.UseUrls($"http://0.0.0.0:{port}");
        }

        public static IServiceCollection AddCorsService(this IServiceCollection services)
        {
            return services.AddCors(options =>
            {
                options.AddPolicy(CorsOrigins, policy =>
                {
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                });
            });
        }

        // Remplace l'ancien système lié à dotnet.core.identity (Twitter) : ici un JWT "maison",
        // signé avec un secret local. À adapter quand l'inscription par pseudo sera en place.
        public static IServiceCollection AddJwtService(this IServiceCollection services)
        {
            var secret = Environment.GetEnvironmentVariable("GOLDENFAN_JWT_SECRET") ?? "goldenfan-dev-secret-change-me";

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
                    };
                });

            return services;
        }

        public static IServiceCollection AddSwaggerService(this IServiceCollection services)
        {
            services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Colle uniquement le jeton reçu après Login — pas besoin d'écrire \"Bearer\" devant.",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        Array.Empty<string>()
                    }
                });
            });
            return services;
        }

        public static WebApplication UseSwaggerService(this WebApplication app)
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            return app;
        }
    }
}
