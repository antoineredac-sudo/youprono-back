using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using dotnet.core.thegoldenfan.Services;
using dotnet.core.utils.Models;

namespace dotnet.core.thegoldenfan.controllers
{
    [Route("[controller]/[action]")]
    [ApiController]
    //[Authorize(Roles = "administrators, roots")]
    public class DatabaseController : ControllerBase
    {
        private readonly DatabaseService service;


        public DatabaseController(DatabaseService service)
        {
            this.service = service;
        }


        [HttpPost]
        public async Task<ActionResult<ResponseModel<bool>>> CreateAsync()
        {
            var res = ResponseModel<bool>.CreateDefault();
            try
            { 
                var tmp = await service.EnsureCreatedAsync();
                res = new ResponseModel<bool>(0, tmp);
            }
            catch(Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
                return StatusCode(500, res);
            }
            return res;
        }

        [HttpPut]
        public ActionResult<ResponseModel<bool>> Update()
        {
            var res = ResponseModel<bool>.CreateDefault();
            try
            {
                service.UpdateDb();
                res = new ResponseModel<bool>(0, true);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
                return StatusCode(500, res);
            }
            return res;
        }

        [HttpDelete]
        public async Task<ActionResult<ResponseModel<bool>>> DeleteAsync()
        {
            var res = ResponseModel<bool>.CreateDefault();
            try
            {
                var tmp = await service.EnsureDeletedAsync();
                res = new ResponseModel<bool>(0, tmp);
            }
            catch (Exception ex)
            {
                res = ResponseModel<bool>.Exception(ex);
                return StatusCode(500, res);
            }
            return res;
        }
    }
}
