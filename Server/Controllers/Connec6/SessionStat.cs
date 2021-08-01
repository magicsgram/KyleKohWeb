using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KyleKoh.Server.Hubs
{
  public class SessionStat
  {
    public Int64 Id { get; set; }
    public Int64 TotalSessions { get; set;} = 0;
    public Int64 TotalConnections { get; set; } = 0;
    public Int64 TotalMultiplayerGame { get; set; } = 0;

    public DateTime LastDbCleaningAt { get; set; } = DateTime.Now;
  }
}
