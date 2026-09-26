using dotnet.core.thegoldenfan.Services;
using dotnet.core.utils.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.controllers
{
    // Le renouvellement du jeton de connexion (26 septembre 2026). Un fichier a
    // part plutot qu'une retouche de UserController : sa route vit sous le meme
    // prefixe « User », et elle n'existe pas deja la-bas.
    [Route("User")]
    [ApiController]
    public class JetonController : ControllerBase
    {
        private readonly UserService service;

        public JetonController(UserService service)
        {
            this.service = service;
        }

        // Le site envoie son jeton actuel dans l'en-tete Authorization et recoit
        // un jeton neuf, valable 30 jours de plus.
        [HttpPost("Jeton")]
        public async Task<ResponseModel<string>> RenouvelerAsync()
        {
            ResponseModel<string> res = ResponseModel<string>.CreateDefault();
            try
            {
                var jeton = await service.RenouvelerJetonAsync(Request.Headers["Authorization"].ToString());
                res = new ResponseModel<string>(0, jeton);
            }
            catch (Exception ex)
            {
                res = ResponseModel<string>.Exception(ex);
            }
            return res;
        }
    }
}
