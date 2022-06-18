using System.Collections.Generic;
using athenaeumBot.Models;

namespace artveeBot.Models
{
    public class Artist :IWebItem
    {
        public string Url { get; set; }
        public string Name { get; set; }
        public string Country { get; set; }
        public string Date { get; set; }
        public List<Artwork> Artworks { get; set; }

    }
}