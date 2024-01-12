using WebCrawler;

const string seedUrl = "https://meghdadit.com/";
const int maxPageNo = 2000;

var crawler = new Crawler(seedUrl, maxPageNo);
await crawler.Run();