using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace dotnet.core.utils
{
    public class PaginationModel<T>
    {
        // La liste des éléments de la page en cours (le code du jeu l'appelle "Page", pas "Items")
        public List<T> Page { get; set; } = new();
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

        // Version synchrone : pour paginer une liste déjà en mémoire (ex: résultat déjà reçu d'Opta)
        public static PaginationModel<T> CreatePage(IEnumerable<T> source, int pageIndex, int pageSize)
        {
            var list = source.ToList();
            var totalCount = list.Count;
            var items = list.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();

            return new PaginationModel<T>
            {
                Page = items,
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        // Version asynchrone : pour paginer directement une requête base de données (Entity Framework)
        public static async Task<PaginationModel<T>> CreatePageAsync(IQueryable<T> query, int pageIndex, int pageSize)
        {
            var totalCount = await query.CountAsync();
            var items = await query.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();

            return new PaginationModel<T>
            {
                Page = items,
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        // Convertit une page de résultats bruts (ex: entités base de données) en page de résultats
        // formatés pour l'API, en conservant les infos de pagination (page, taille, total).
        // La liste elle-même est laissée vide : à l'appelant de la remplir avec les objets convertis.
        public static PaginationModel<T> ConvertTo<TSource>(PaginationModel<TSource> source)
        {
            return new PaginationModel<T>
            {
                Page = new List<T>(),
                PageIndex = source.PageIndex,
                PageSize = source.PageSize,
                TotalCount = source.TotalCount
            };
        }
    }
}
