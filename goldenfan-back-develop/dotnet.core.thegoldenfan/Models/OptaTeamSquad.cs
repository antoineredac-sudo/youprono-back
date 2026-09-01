using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.Models
{
    public class OptaTeamSquad
    {
        public List<TeamSquadModel> Squad { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
