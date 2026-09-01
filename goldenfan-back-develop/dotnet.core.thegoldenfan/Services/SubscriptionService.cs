using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.utils;
using dotnet.core.utils.server.Helpers;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.Services
{
    public class SubscriptionService
    {
        private readonly AppDbContext dbContext;


        public SubscriptionService(AppDbContext dbContext)
        {
            this.dbContext = dbContext;
        }


        public async Task<PaginationModel<Subscription>?> PageAsync(int page = 1, int limit = 10)
        {
            PaginationModel<Subscription> res = null;
            var lo = dbContext.Subscriptions;
            if (lo != null)
            {
                res = await PaginationModel<Subscription>.CreatePageAsync(lo, page, limit);
            }
            return res;
        }

        public async Task<Subscription> ByIdAsync(Guid id)
        {
            string src = "SubscriptionService.ById";
            var res = await dbContext.Subscriptions.FirstOrDefaultAsync(w => w.Id.Equals(id));
            if (res == null) { throw BaseException.NotFound(-1, src); }
            return res;
        }

        public async Task<PaginationModel<Subscription>?> ByUserAsync(Guid model, int page=1, int limit=10)
        {
            PaginationModel<Subscription> res = null;
            var lo = dbContext.Subscriptions.Where(w => w.UserId.Equals(model));
            if (lo != null)
            {
                res = await PaginationModel<Subscription>.CreatePageAsync(lo, page, limit);
            }
            return res;
        }


        public class SubscriptionInputModel
        {
            public Guid UserId { get; set; }
            public string EndPoint { get; set; } = null!;
            public string P256dh { get; set; } = null!;
            public string Auth { get; set; } = null!;
            public DateTime? ExpirationTime { get; set; }
            public Subscription Create()
            {
                var newObj = new Subscription()
                {
                    UserId = this.UserId,
                    EndPoint = this.EndPoint,
                    P256dh = this.P256dh,
                    Auth = this.Auth,
                    ExpirationTime = this.ExpirationTime
                };
                return newObj;
            }
        }
        public async Task<SubscriptionInputModel> CreateAsync(SubscriptionInputModel model)
        {
            string src = "SubscriptionService.Create";
            if (model == null) { throw BaseException.InvalidModel(-1, src); }

            var user = await dbContext.Users.FirstOrDefaultAsync(w => w.Id.Equals(model.UserId));
            if (user == null) { throw BaseException.NotFound(-2, src); }

            var newObj = model.Create();
            await dbContext.Subscriptions.AddAsync(newObj);
            await dbContext.SaveChangesAsync();
            return model;
        }

        public async Task<SubscriptionUpdateModel> UpdateAsync(Guid id, SubscriptionUpdateModel model)
        {
            string src = "SubscriptionService.UpdateAsync";
            if (model == null || !id.Equals(model.Id)) { throw BaseException.InvalidModel(-1, src); }

            var inDb = await dbContext.Subscriptions.FirstOrDefaultAsync(w => w.Id.Equals(id));
            if (inDb == null) { throw BaseException.NotFound(-2, src); }

            inDb.Auth = model.Auth == null ? inDb.Auth : model.Auth;
            inDb.EndPoint = model.EndPoint == null ? inDb.EndPoint : model.EndPoint;
            inDb.P256dh = model.P256dh == null ? inDb.P256dh : model.P256dh;
            inDb.ExpirationTime = model.ExpirationTime == null ? inDb.ExpirationTime : model.ExpirationTime;

            dbContext.Subscriptions.Update(inDb);
            await dbContext.SaveChangesAsync();
            return model;
        }

        public async Task<Subscription> DeleteAsync(Guid model)
        {
            string src = "SubscriptionService.DeleteAsync";

            var inDb = await dbContext.Subscriptions.FirstOrDefaultAsync(w => w.Id.Equals(model));
            if (inDb == null) { throw BaseException.NotFound(-2, src); }

            dbContext.Subscriptions.Remove(inDb);
            await dbContext.SaveChangesAsync();
            return inDb;
        }
    }

    public class SubscriptionUpdateModel
    {
        public Guid Id { get; set; }
        public string EndPoint { get; set; } = null!;
        public string P256dh { get; set; } = null!;
        public string Auth { get; set; } = null!;
        public DateTime? ExpirationTime { get; set; }
    }
}
