using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace KyleKoh.Server.Hubs
{
  public class WordleHub : Hub
  {
    private static String wordsFullString;

    public WordleHub() : base()
    {
      if (wordsFullString == null)
      {
        String wordsPath = Path.Combine(Directory.GetParent(".").FullName, "words.txt");
        StreamReader streamReader = new(wordsPath);
        StringBuilder sb = new();
        while (!streamReader.EndOfStream)
        {
          String word = streamReader.ReadLine().ToLower().Trim();
          if (word.Length > 0)
            sb.AppendLine(word);
        }
        streamReader.Close();
        wordsFullString = sb.ToString().Replace("\r", "").Trim();
      }
    }

    public async Task GetWordsFull()
    {
      await Clients.Caller.SendAsync("WordsFullString", wordsFullString);
    }
  }
}
