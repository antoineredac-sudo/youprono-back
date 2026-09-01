using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.utils.server.Dbs;
using Microsoft.EntityFrameworkCore;

namespace dotnet.core.thegoldenfan.Services
{
    public class DatabaseService : BaseDatabaseService
    {
        private readonly AppDbContext dbContext;

        public DatabaseService(AppDbContext dbContext)
            :base(dbContext)
        {
            this.dbContext = dbContext;
        }

        public void UpdateDb()
        {
            dbContext.Database.Migrate();
        }
    }
}
