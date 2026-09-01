using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotnet.core.thegoldenfan.Models
{
    public class TournamentCalendar
    {
        public List<CompetitionModel> Competition { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
