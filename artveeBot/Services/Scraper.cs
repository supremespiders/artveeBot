﻿using artveeBot.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using artveeBot.Extensions;
using ExcelHelperExe;
using Newtonsoft.Json;

namespace artveeBot.Services
{
    public class Scraper
    {
        private readonly HttpClient _client;
        private readonly List<HttpClient> _clients;
        private readonly int _threads;
        private int _idx;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly bool _useProxies = false;

        public Scraper(int threads)
        {
            _threads = threads;
            _client = new HttpClient(new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            }){Timeout = TimeSpan.FromSeconds(30)};
            
            if (File.Exists("proxies.txt"))
            {
                _clients = new List<HttpClient>();
                var proxies = File.ReadAllLines("proxies.txt");
                foreach (var p in proxies)
                {
                    var pp = p.Split(':');
                    var proxy = new WebProxy($"{pp[0]}:{pp[1]}", true)
                    {
                        UseDefaultCredentials = false,
                        Credentials = new NetworkCredential(pp[2], pp[3]),
                    };
                    _clients.Add(new HttpClient(new HttpClientHandler()
                    {
                        Proxy = proxy,
                        UseCookies = false,
                        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                    }){Timeout = TimeSpan.FromSeconds(30)});
                }
                _clients.Add(_client);
            }
        }
        
        async Task<HttpClient> GetNextClient()
        {
            await _semaphore.WaitAsync();
            var client = _clients[_idx];
            _idx++;
            if (_idx == _clients.Count)
                _idx = 0;
            _semaphore.Release();
            return client;
        }

        async Task GetArtistLinks()
        {
            var allLinks = new List<string>();
            var artistPres = new List<ArtistPre>();
            for (var i = 1; i <= 13; i++)
            {
                Notifier.Display($"Artist page {i}");
                var doc = await _client.GetHtml($"https://artvee.com/artists/page/{i}/").ToDoc();
                var nodes = doc.DocumentNode.SelectNodes("//h3[@class='category-title']/..");
                foreach (var node in nodes)
                {
                    var url = node.GetAttributeValue("href", "");
                    var s = node.SelectSingleNode(".//mark").InnerText.Split(',')[1].Replace("Items", "").Trim();
                    var count = int.Parse(s);
                    artistPres.Add(new ArtistPre
                    {
                        Url = url,
                        Count = count
                    });
                }

                //var links = doc.DocumentNode.SelectNodes("//a[@class='category-link']").Select(x => x.GetAttributeValue("href", "")).ToList();
                // allLinks.AddRange(links);
            }

            artistPres.Save();
            // File.WriteAllLines("artistLinks", allLinks);
        }

        async Task GetArtists(CancellationToken ct)
        {
            var links = File.ReadAllLines("ArtistUrls");
            var artists = await links.ScrapeParallel(_threads, x => GetArtist(x, ct));
            artists.Save();
        }

        async Task<Artist> GetArtist(string url, CancellationToken ct)
        {
            var doc = await _client.GetHtml($"{url}", ct: ct).ToDoc();
            var title = doc.DocumentNode.SelectSingleNode("//h1").InnerText;
            var sub = doc.DocumentNode.SelectSingleNode("//h1/following-sibling::span/div[1]")?.InnerText.Split(',');
            var country = sub?[0].Trim();
            var date = sub?[1].Trim();
            //var nodes = doc.DocumentNode.SelectNodes("//div[@class='product-wrapper snax']");
            var artist = new Artist()
            {
                Url = url,
                Name = title,
                Date = date,
                Country = country,
                Artworks = new List<Artwork>()
            };
            // foreach (var node in nodes)
            // {
            //     var link = node.SelectSingleNode(".//a[@class='product-image-link linko']").GetAttributeValue("href", "");
            //     var category = node.SelectSingleNode("..//div[@class='woodmart-product-cats']/a").InnerText;
            //     artist.Artworks.Add(new Artwork()
            //     {
            //         Url = link,
            //         Category = category
            //     });
            // }

            return artist;
        }

        async Task GetArtworks(CancellationToken ct)
        {
            // var artists = nameof(Artist).Load<Artist>();
            // var links = artists.SelectMany(x => x.Artworks).ToList();
            var links = File.ReadAllLines("artLinks");
            var artworks = await links.ScrapeParallel(_threads, x => GetArtworkDetails(x, ct));
            artworks.Save();
        }

        async Task<List<ArtworkPre>> GetArtsLinks(string url,string category)
        {
            var links = new List<ArtworkPre>();
            var client = await GetNextClient();
            do
            {
                Notifier.Display(url);
                var doc = await client.GetHtml(url,5).ToDoc();
                var nodes = doc.DocumentNode.SelectNodes("//div[@class='product-wrapper snax']");
              
                foreach (var node in nodes)
                {
                    var link = node.SelectSingleNode(".//a[@class='product-image-link linko']").GetAttributeValue("href", "");
                    links.Add(new ArtworkPre()
                    {
                        Url = link,
                        Category = category
                    });
                }

                url = doc.DocumentNode.SelectSingleNode("//a[@class='next page-numbers']")?.GetAttributeValue("href","");
                if (url == null) break;
            } while (true);
            
            return links;
        }

        async Task<Artwork> GetArtworkDetails(string url, CancellationToken ct)
        {
            var client = _useProxies ? await GetNextClient() : _client;
            var doc = await client.GetHtml(url, ct: ct).ToDoc();
            var title = doc.DocumentNode.SelectSingleNode("//h1").InnerText;
            var artistName = doc.DocumentNode.SelectSingleNode("//div[@class='tartist']//a[contains(@href,'https://artvee.com/artist/')]").InnerText;
            var artistLink = doc.DocumentNode.SelectSingleNode("//div[@class='tartist']//a[contains(@href,'https://artvee.com/artist/')]").GetAttributeValue("href", "");
            var imageLink = doc.DocumentNode.SelectSingleNode("(//a[@data-snax-collection='downloads'])[last()]").GetAttributeValue("href", "");
            var isPublicDomain = doc.DocumentNode.SelectSingleNode("//*[text()='Why is this image in the public domain?']") != null;
            if (!isPublicDomain)
                Debug.WriteLine(url);
            var artwork = new Artwork
            {
                Url = url,
                Title = title,
                ArtistName = artistName,
                ArtistUrl = artistLink,
                ImageUrl = imageLink,
                Copyright = isPublicDomain ? "Public Domain" : "Copyright"
            };
            return artwork;
        }

        async Task GetArtsByCategories()
        {
             //var links = File.ReadAllLines("artLinks").ToList();
             //var links = new List<ArtworkPre>();
             var links = nameof(ArtworkPre).Load<ArtworkPre>();
             //var y = links.ToHashSet();
             //links.AddRange(await GetArtsLinks("https://artvee.com/c/asian-art/?per_page=1000&_pjax=.main-page-wrapper","Asian Art"));
              // links.AddRange(await GetArtsLinks("https://artvee.com/c/abstract/?per_page=1000&_pjax=.main-page-wrapper","Abstract"));
              // links.AddRange(await GetArtsLinks("https://artvee.com/c/figurative/?per_page=1000&_pjax=.main-page-wrapper","Figurative"));
              // links.AddRange(await GetArtsLinks("https://artvee.com/c/landscape/?per_page=1000&_pjax=.main-page-wrapper","Landscape"));
             links.AddRange(await GetArtsLinks("https://artvee.com/c/religion/?per_page=1000&_pjax=.main-page-wrapper","Religion"));
             links.AddRange(await GetArtsLinks("https://artvee.com/c/mythology/?per_page=1000&_pjax=.main-page-wrapper","Mythology"));
             links.AddRange(await GetArtsLinks("https://artvee.com/c/posters/?per_page=1000&_pjax=.main-page-wrapper","Posters"));
             links.AddRange(await GetArtsLinks("https://artvee.com/c/drawings/?per_page=1000&_pjax=.main-page-wrapper","Drawings"));
             links.AddRange(await GetArtsLinks("https://artvee.com/c/illustration/?per_page=1000&_pjax=.main-page-wrapper","Illustration"));
             links.AddRange(await GetArtsLinks("https://artvee.com/c/still-life/?per_page=1000&_pjax=.main-page-wrapper","Still Life"));
             links.AddRange(await GetArtsLinks("https://artvee.com/c/animals/?per_page=1000&_pjax=.main-page-wrapper","Animals"));
             links.AddRange(await GetArtsLinks("https://artvee.com/c/botanical/?per_page=1000&_pjax=.main-page-wrapper","Botanical"));
             //File.WriteAllLines("artLinks",links);
             links.Save();
            //await GetArtsLinks("https://artvee.com/c/landscape/?per_page=5000");
        }

        async Task ParseArtworks()
        {
            var artworks = nameof(Artwork).Load<Artwork>();
           // var artists = nameof(Artist).Load<Artist>().Select(x=>x.Url).ToHashSet();
            // var artistsFromArtworks = artworks.Select(x => x.ArtistUrl).ToHashSet();
            // File.WriteAllLines("ArtistUrls",artistsFromArtworks);
           // var remaining = artistsFromArtworks.Where(x => !artists.Contains(x));
         
           // var pres = nameof(ArtworkPre).Load<ArtworkPre>().ToDictionary(x=>x.Url,x=>x.Category);
           // foreach (var artwork in artworks)
           // {
           //     if (pres.ContainsKey(artwork.Url))
           //         artwork.Category = pres[artwork.Url];
           //     else
           //         Debug.WriteLine("");
           // }
           //JG100000
           var i = 0;
           var dic = new Dictionary<string, string>();
           foreach (var artwork in artworks)
           {
               i++;
               var s ="img/JG1"+ i.ToString().PadLeft(5, '0')+".jpg";
               artwork.ImageLocal = s;
               dic.Add(artwork.ImageUrl,s);
               // artwork.Title = artwork.Title.Replace("\n", "").Trim();
           }
          // File.WriteAllText("imgLinks",JsonConvert.SerializeObject(dic));
            artworks.Save(); 
        }

        async Task DownloadAllImages2(CancellationToken ct)
        {
            Directory.CreateDirectory("img");
            Notifier.Display("Parsing images links...");
            var dic = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("imgLinks"));

            var links = new Dictionary<string, string>();
            foreach (var v in dic)
            {
                if (v.Value == null) continue;
                if (File.Exists(v.Key)) continue;
                links.Add(v.Key, v.Value);
            }
            //var y = dic.Keys.Count(x => x.Length > 240);


            await links.ToList().Work(_threads, x => _client.DownloadFile(x.Key, x.Value, ct));
        }
        
        async Task DownloadAllImages(CancellationToken ct)
        {
            Directory.CreateDirectory("img");
            Notifier.Display("Parsing images links...");
            var artworks = nameof(Artwork).Load<Artwork>();
            var remainingArtworks = new List<Artwork>();
            foreach (var v in artworks)
            {
                if (File.Exists(v.ImageLocal)) continue;
                remainingArtworks.Add(v);
            }

            await remainingArtworks.Work(_threads, x => Download(x, ct));
        }

        async Task Download(Artwork artwork, CancellationToken ct)
        {
            var client = _useProxies ? await GetNextClient() : _client;
            var doc = await client.GetHtml(artwork.Url, ct: ct).ToDoc();
            var imageLink = doc.DocumentNode.SelectSingleNode("(//a[@data-snax-collection='downloads'])[last()]").GetAttributeValue("href", "");
            try
            {
                await client.DownloadFile(imageLink, artwork.ImageLocal,ct);
            }
            catch (TaskCanceledException)
            {
                if (ct.IsCancellationRequested) throw;
                throw new Exception("Timed out");
            }
        }

        public async Task MainWork(CancellationToken ct)
        {
           //  var ars = nameof(ArtistPre).Load<ArtistPre>();
           //  var y = ars.Sum(x => x.Count);
           // await ars.SaveToExcel("artistsWorks.xlsx");
            //await GetArtistLinks();
             //await GetArtists(ct);
           // await GetArtworks(ct);
            // await GetArtsByCategories();
           //await ParseArtworks();
            //await Task.Run(()=>DownloadAllImages(ct), ct);
            await DownloadAllImages(ct);
        }
    }
}