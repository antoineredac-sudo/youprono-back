using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace dotnet.core.utils.server.Dbs
{
    public abstract class BaseDatabaseService
    {
        protected readonly DbContext context;

        protected BaseDatabaseService(DbContext context)
        {
            this.context = context;
        }

        public Task<bool> EnsureCreatedAsync()
        {
            return context.Database.EnsureCreatedAsync();
        }

        public Task<bool> EnsureDeletedAsync()
        {
            return context.Database.EnsureDeletedAsync();
        }
    }
}
