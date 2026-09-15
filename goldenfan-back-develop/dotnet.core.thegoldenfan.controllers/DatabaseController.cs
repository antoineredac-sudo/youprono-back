using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using dotnet.core.thegoldenfan.Services;
using dotnet.core.utils.Models;

namespace dotnet.core.thegoldenfan.controllers
{
    // FERME LE 15 SEPTEMBRE 2026. Ces trois routes n'avaient aucune protection :
    // n'importe qui connaissant l'adresse du serveur pouvait, d'une seule requete
    // DELETE /Database/Delete, effacer toute la base. Elles datent de l'epoque du
    // developpement et le site ne s'en sert pas. Le code est garde
    // tel quel ; [NonAction] retire seulement les routes du serveur. Pour en rouvrir
    // une un jour, il suffit d'enlever la ligne [NonAction] correspondante.
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


        [NonAction]
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

        [NonAction]
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

        [NonAction]
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
