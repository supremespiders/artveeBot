using athenaeumBot.Models;

namespace artveeBot.Models
{
    public class Artwork : IWebItem
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string ImageUrl { get; set; }
        public string ImageLocal { get; set; }
        public string Copyright { get; set; }
        public string ArtistName { get; set; }
        public string ArtistUrl { get; set; }
    }
}