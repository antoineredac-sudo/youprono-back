using dotnet.core.utils;
using dotnet.core.thegoldenfan.Dbs;
using dotnet.core.thegoldenfan.Models;
using dotnet.core.thegoldenfan.Services;
using dotnet.core.thegoldenfan.Services.Opta;
using dotnet.core.utils.Models;
using dotnet.core.utils.server.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static dotnet.core.thegoldenfan.Services.SubscriptionService;
using static dotnet.core.thegoldenfan.Services.UserService;

namespace dotnet.core.thegoldenfan.controllers
{
    [Route("[controller]")]
    [ApiController]
    //[Authorize]
    public class SubscriptionController : ControllerBase
    {
        private readonly SubscriptionService service;


        public SubscriptionController(SubscriptionService service)
        {
            this.service = service;
        }

        [HttpGet("Page/{page}/{limit}")]
        public async Task<ResponseModel<PaginationModel<Subscription>>> PageAsync(int page=1, int limit=10)
        {
            ResponseModel<PaginationModel<Subscription>> res = ResponseModel<PaginationModel<Subscription>>.CreateDefault();
            try
            {
                var obj = await service.PageAsync(page, limit);
                res = new ResponseModel<PaginationModel<Subscription>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<PaginationModel<Subscription>>.Exception(ex);
            }
            return res;
        }
        [HttpGet("ByUser/{userId}")]
        public async Task<ResponseModel<PaginationModel<Subscription>>> ByUserAsync(Guid userId, int page = 1, int limit = 10)
        {
            ResponseModel<PaginationModel<Subscription>> res = ResponseModel<PaginationModel<Subscription>>.CreateDefault();
            try
            {
                var obj = await service.ByUserAsync(userId, page, limit);
                res = new ResponseModel<PaginationModel<Subscription>>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<PaginationModel<Subscription>>.Exception(ex);
            }
            return res;
        }
        [HttpGet("ById/{id}")]
        public async Task<ResponseModel<Subscription>> ByIdAsync(Guid id)
        {
            ResponseModel<Subscription> res = ResponseModel<Subscription>.CreateDefault();
            try
            {
                var obj = await service.ByIdAsync(id);
                res = new ResponseModel<Subscription>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<Subscription>.Exception(ex);
            }
            return res;
        }

        [HttpPost("create")]
        public async Task<ResponseModel<SubscriptionInputModel>> CreateAsync([FromBody] SubscriptionInputModel model)
        {
            ResponseModel<SubscriptionInputModel> res = ResponseModel<SubscriptionInputModel>.CreateDefault();
            try
            {
                var obj = await service.CreateAsync(model);
                res = new ResponseModel<SubscriptionInputModel>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<SubscriptionInputModel>.Exception(ex);
            }
            return res;
        }

        [HttpPut("update/{id}")]
        public async Task<ResponseModel<SubscriptionUpdateModel>> UpdateAsync(Guid id, [FromBody] SubscriptionUpdateModel model)
        {
            ResponseModel<SubscriptionUpdateModel> res = ResponseModel<SubscriptionUpdateModel>.CreateDefault();
            try
            {
                var obj = await service.UpdateAsync(id, model);
                res = new ResponseModel<SubscriptionUpdateModel>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<SubscriptionUpdateModel>.Exception(ex);
            }
            return res;
        }


        [HttpDelete("Delete/{id}")]
        public async Task<ResponseModel<Subscription>> DeleteAsync(Guid id)
        {
            ResponseModel<Subscription> res = ResponseModel<Subscription>.CreateDefault();
            try
            {
                var obj = await service.DeleteAsync(id);
                res = new ResponseModel<Subscription>(0, obj);
            }
            catch (Exception ex)
            {
                res = ResponseModel<Subscription>.Exception(ex);
            }
            return res;
        }
    }
}
