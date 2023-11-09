using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace WebCrawler;

public class Crawler
{
    private readonly string _seedUrl;
    private readonly int _maxPageNo;

    private readonly HashSet<string> _repository;
    private readonly HashSet<string> _urlSet;
    private readonly Queue<string> _urlQueue;
    private HashSet<string> _disallowPath;

    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public Crawler(string seedUrl, int maxPageNo)
    {
        _seedUrl = seedUrl;
        _maxPageNo = maxPageNo;
        _disallowPath = new HashSet<string>();
        _repository = new HashSet<string>();
        _urlSet = new HashSet<string> { seedUrl };
        _urlQueue = new Queue<string>();
        _urlQueue.Enqueue(seedUrl);
    }

    public async Task Run()
    {
        var watch = Stopwatch.StartNew();
        _disallowPath = await ReadRobotsDisallowPath();
        while (_urlQueue.Count > 0 && _repository.Count < _maxPageNo)
        {
            await Crawl();
            Thread.Sleep(new TimeSpan(0, 0, 0, 0, 50));
        }
        watch.Stop();

        Console.WriteLine($"Crawled Pages Count: {_repository.Count}");
        Console.WriteLine($"Parsed Urls Count:   {_urlSet.Count}");
        Console.WriteLine($"Queue Urls Count:    {_urlQueue.Count}");
        Console.WriteLine($"Total Time:          {watch.Elapsed.Hours}:{watch.Elapsed.Minutes}:{watch.Elapsed.Seconds}");
    }

    private async Task Crawl()
    {
        var url = _urlQueue.Dequeue();
        // Console.WriteLine(url);
        var htmlContent = await DownloadPage(url);
        if (!string.IsNullOrWhiteSpace(htmlContent))
        {
            Save(url, htmlContent);
            ParseUrLs(htmlContent);
        }
    }

    private async Task<string?> DownloadPage(string url)
    {
        try
        {
            var htmlResponse = await _httpClient.GetAsync(url);

            if (htmlResponse is not { IsSuccessStatusCode: true })
                return null;

            if (!htmlResponse.Content.Headers.ContentType!.MediaType!.Contains("text/html"))
                return null;

            return await htmlResponse.Content.ReadAsStringAsync();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void Save(string url, string htmlContent)
    {
        var htmlPath = Directory.GetCurrentDirectory() + "\\Pages\\Pages.html";
        File.AppendAllText(htmlPath, htmlContent);

        var repPath = Directory.GetCurrentDirectory() + "\\Pages\\URLRep.txt";
        File.AppendAllText(repPath, url + "\n");

        _repository.Add(url);
    }

    public void ParseUrLs(string htmlContent)
    {
        var regexLink = new Regex("(?<=<a\\s*?href=(?:'|\"))[^'\"]*?(?=(?:'|\"))");
        foreach (var match in regexLink.Matches(htmlContent))
        {
            try
            {
                var link = match.ToString()!.Trim().Trim('\n');

                if (!IsLinkAcceptable(link))
                    continue;

                if (link.StartsWith("/"))
                    link = _seedUrl + link;

                if (link.EndsWith("/"))
                    link = link[..^1];

                if (IsLinkDisallow(link))
                    continue;

                if (_urlSet.Contains(link))
                    continue;

                _urlSet.Add(link);
                _urlQueue.Enqueue(link);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        bool IsLinkAcceptable(string link)
        {
            if (string.IsNullOrEmpty(link) || string.IsNullOrWhiteSpace(link))
                return false;

            if (link.StartsWith("#"))
                return false;

            if (link.StartsWith("mailto:") || link.StartsWith("tel:") || link.StartsWith("sms:"))
                return false;

            if (link.ToLower().StartsWith("http:") || link.ToLower().StartsWith("https:"))
            {
                var seedUri = new Uri(_seedUrl);
                var linkUri = new Uri(link);
                if (linkUri.Host != seedUri.Host)
                    return false;
            }

            var frags = link.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return frags.Any();
        }

        bool IsLinkDisallow(string link)
        {
            var linkUri = new Uri(link);
            return _disallowPath.Any(x => linkUri.AbsolutePath.StartsWith(x));
        }
    }

    private async Task<HashSet<string>> ReadRobotsDisallowPath()
    {
        var res = new HashSet<string>();
        var robotsContent = await _httpClient.GetStringAsync(_seedUrl + "/robots.txt");

        var lines = robotsContent.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("Disallow:")) continue;
            var path = trimmed.Substring(trimmed.IndexOf(':') + 1).Trim();
            res.Add(path);
        }

        return res;
    }
}