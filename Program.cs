using WebCrawler;

const string seedUrl = "https://gooshishop.com";
const int maxPageNo = 2000;

var crawler = new Crawler(seedUrl, maxPageNo);
await crawler.Run();