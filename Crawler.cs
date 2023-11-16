using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WebCrawler;

public class Crawler
{
    private readonly string _seedUrl;
    private readonly int _maxPageNo;

    private int _fetchError;
    private int _typeError;

    private int _repositoryUrl;
    private int _discoveredUrl;

    private readonly HashSet<string> _urlSet;
    private readonly Queue<string> _urlQueue;
    private HashSet<string> _disallowPath;

    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public Crawler(string seedUrl, int maxPageNo)
    {
        _seedUrl = seedUrl;
        _maxPageNo = maxPageNo;
        _disallowPath = new HashSet<string>();
        _urlSet = new HashSet<string> { seedUrl };
        _urlQueue = new Queue<string>();
        _urlQueue.Enqueue(seedUrl);
    }

    public async Task Run()
    {
        var watch = Stopwatch.StartNew();
        _disallowPath = await ReadRobotsDisallowPath();
        while (_urlQueue.Count > 0 && _repositoryUrl < _maxPageNo)
        {
            await Crawl();
        }
        watch.Stop();

        Console.WriteLine($"Total Time:          {watch.Elapsed.Hours}:{watch.Elapsed.Minutes}:{watch.Elapsed.Seconds}");
        Console.WriteLine($"Queue Urls Count:    {_urlQueue.Count}");
        Console.WriteLine($"Checked Urls Count:  {_urlSet.Count}");
        Console.WriteLine($"Parsed Urls Count:   {_discoveredUrl}");
        Console.WriteLine($"Fetch Error Count:   {_fetchError}");
        Console.WriteLine($"Type Error Count:    {_typeError}");
    }

    private async Task Crawl()
    {
        var url = _urlQueue.Dequeue();
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
            {
                _fetchError++;
                return null;
            }

            if (!htmlResponse.Content.Headers.ContentType!.MediaType!.Contains("text/html"))
            {
                _typeError++;
                return null;
            }

            return await htmlResponse.Content.ReadAsStringAsync();
        }
        catch (Exception)
        {
            _fetchError++;
            return null;
        }
    }

    private void Save(string url, string htmlContent)
    {
        var htmlPath = Directory.GetCurrentDirectory() + "\\Pages\\Pages.html";
        File.AppendAllText(htmlPath, htmlContent);

        var repPath = Directory.GetCurrentDirectory() + "\\Pages\\URLRep.txt";
        File.AppendAllText(repPath, url + "\n");

        _repositoryUrl++;
    }

    public void ParseUrLs(string htmlContent)
    {
        var regexLink = new Regex("(?<=<a\\s+(?:[^>]*?\\s+)?href=(?:'|\"))[^'\"]*?(?=(?:'|\"))");

        foreach (var match in regexLink.Matches(htmlContent))
        {
            try
            {
                var link = match.ToString()!.Trim().Trim('\n');
                _discoveredUrl++;

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

            if (link.StartsWith("#") || link.StartsWith("$"))
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
            try
            {
                var linkUri = new Uri(link);
                return _disallowPath.Any(x => linkUri.AbsolutePath.StartsWith(x));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
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