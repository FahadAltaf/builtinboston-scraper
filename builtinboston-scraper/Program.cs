using CsvHelper;
using HtmlAgilityPack;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace builtinboston_scraper
{
    public class DataModel
    {
        public string SearchedKeyword { get; set; }
        public string SearchedLocation { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string CompanyName { get; set; }
        public string ShortDescription { get; set; }
        public string Location { get; set; }
        public string Type { get; set; }
        public string PostTime { get; set; }
        public string Rating { get; set; }
        public string FullDescription { get; set; }
        public string CurrentTime { get { return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); } }
    }

    public class Locations
    {
        public string name { get; set; }
        public string url { get; set; }
    }
    class Program
    {
        static string keyword = "director of rehabilitation";
        static string searchedLocation = "United States";
        static List<DataModel> entries = new List<DataModel>();
        static bool getDescription = false;
        static List<Locations> locations = new List<Locations>();

        static void Main(string[] args)
        {
            var json = File.ReadAllText("locations.json");
            locations = JsonConvert.DeserializeObject<List<Locations>>(json);
            try
            {
                Console.WriteLine("Enter your search query: ");
                keyword = Console.ReadLine();
                Console.WriteLine("All Locations: ");
                foreach (var item in locations)
                {
                    var index = locations.IndexOf(item) + 1;
                    Console.WriteLine((item.name.Contains("(All)")) ? $"{index} {item.name}" : $"{index}    {item.name}");
                }
                Console.WriteLine("Select location to be searched: ");
                searchedLocation = Console.ReadLine();

                var selectedLocation = locations[Convert.ToInt32(searchedLocation)-1];

                searchedLocation = selectedLocation.name;
                Console.WriteLine("Do you want to scrape full description? (if yes press y else just press enter): ");
                var selection = Console.ReadLine();

                if (selection.ToLower() == "y")
                    getDescription = true;

                ChromeOptions options = new ChromeOptions();
                options.AddArguments((IEnumerable<string>)new List<string>()
                {
                    //"--silent-launch",
                    //"--no-startup-window",
                    //"no-sandbox",
                    //"headless",
                    //"incognito"
                });

                ChromeDriverService defaultService = ChromeDriverService.CreateDefaultService();
                defaultService.HideCommandPromptWindow = true;
                bool showGUI = true;
                using (IWebDriver driver = (showGUI) ? new ChromeDriver() : (IWebDriver)new ChromeDriver(defaultService, options))
                {
                    driver.Manage().Window.Maximize();
                    try
                    {
                        Console.WriteLine(selectedLocation.url + $"?search={keyword}");
                        driver.Navigate().GoToUrl(selectedLocation.url + $"?search={keyword}");
                        IWait<IWebDriver> wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60.00));
                        wait.Until(driver1 => ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete"));
                        Thread.Sleep(15000);
                        var doc = new HtmlDocument();
                        doc.LoadHtml(driver.PageSource);

                        var url = driver.Url;
                        int pages = 1;
                        var node = doc.DocumentNode.SelectSingleNode("//div[@class='count']");
                        if (node != null)
                        {
                            var totalRecords = Convert.ToInt32(node.InnerText.Replace(",", "").Replace("jobs", "").Replace("Total of", "").Replace("\r\n", "").Trim());
                            if (totalRecords > 0)
                            {
                                Console.WriteLine("Processing page 1");
                                ScrapeData(driver);

                                pages = (totalRecords + 20 - 1) / 20;
                                for (int i = 2; i <= pages; i++)
                                {
                                    Console.WriteLine("Processing page " + i);
                                    var newUrl = url.Replace("?", $"?page={i}&");
                                    driver.Navigate().GoToUrl(newUrl);
                                    Thread.Sleep(15000);
                                    ScrapeData(driver);
                                }
                            }
                            else
                                Console.WriteLine("No results found against your search.");
                        }
                        else
                            Console.WriteLine("Unable to continue because total records not found");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                    }

                    driver?.Close();
                    Thread.Sleep(3000);
                    driver?.Quit();
                }

                if (entries.Count > 0)
                {
                    var today = DateTime.Now;
                    string name = $"{today.Year}{today.Month}{today.Day}{today.Hour}{today.Minute}{today.Second}.csv";
                    using (var writer = new StreamWriter(name))
                    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                    {
                        csv.WriteRecords(entries);
                    }
                    Console.WriteLine("Data exported successfully");
                }
                else
                    Console.WriteLine("We have nothing to export");



            }

            catch (Exception ex)
            {
                Console.WriteLine("We are unable to continue. Reason: " + ex.Message);
            }
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }

        public static void ScrapeData(IWebDriver driver)
        {
            try
            {
                List<DataModel> records = new List<DataModel>();

                var doc = new HtmlDocument();
                doc.LoadHtml(driver.PageSource);
                List<string> links = new List<string>();
                var listing = doc.DocumentNode.SelectNodes("//div[@class='job-item']");
                if (listing != null)
                {
                    foreach (var list in listing)
                    {
                        DataModel entry = new DataModel { SearchedKeyword = keyword, SearchedLocation = searchedLocation };
                        var subDoc = new HtmlDocument();
                        subDoc.LoadHtml(list.InnerHtml);

                        var titleNode = subDoc.DocumentNode.SelectSingleNode("/a[1]/div[1]/div[2]/div[2]/h2[1]"); //
                        if (titleNode != null)
                        {
                            entry.Title = HttpUtility.HtmlDecode(titleNode.InnerText.Replace("\n", "").Replace("\r", "")).Trim();
                            entry.Url = HttpUtility.HtmlDecode(subDoc.DocumentNode.SelectSingleNode("/a[1]").Attributes.FirstOrDefault(x => x.Name == "href").Value);
                            links.Add(entry.Url);
                            Console.WriteLine(entry.Title);
                        }
                        //  
                        var postedNode = subDoc.DocumentNode.SelectSingleNode("/a[1]/div[1]/div[2]/div[2]/div[1]/div[5]"); //
                        if (postedNode != null)
                        {
                            entry.PostTime = HttpUtility.HtmlDecode(postedNode.InnerText.Replace("\n", "").Replace("\r", "").Trim());
                        }

                        var companyNode = subDoc.DocumentNode.SelectSingleNode("/a[1]/div[1]/div[2]/div[2]/div[1]/div[1]"); //
                        if (companyNode != null)
                        {
                            entry.CompanyName = HttpUtility.HtmlDecode(companyNode.InnerText.Replace("\n", "").Replace("\r", "").Trim());
                        }

                        var locationNode = subDoc.DocumentNode.SelectSingleNode("/a[1]/div[1]/div[2]/div[2]/div[1]/div[2]"); //
                        if (locationNode != null)
                        {
                            entry.Location = HttpUtility.HtmlDecode(locationNode.InnerText.Replace("\n", "").Replace("\r", "").Trim());
                        }



                        var shortDecNode = subDoc.DocumentNode.SelectSingleNode("/a[1]/div[1]/div[2]/div[2]/div[2]"); //
                        if (shortDecNode != null)
                        {
                            entry.ShortDescription = HttpUtility.HtmlDecode(shortDecNode.InnerText.Replace("\n", "").Replace("\r", "").Trim());
                        }

                        records.Add(entry);
                    }
                }

                if (getDescription)
                    for (int i = 0; i < records.Count; i++)
                    {
                        driver.Navigate().GoToUrl(records[i].Url);
                        IWait<IWebDriver> wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60.00));
                        wait.Until(driver1 => ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete"));
                        Thread.Sleep(7000);
                        driver.FindElement(By.XPath("//*[@id=\"read-more-description-toggle\"]/span")).Click();
                        Thread.Sleep(3000);
                        doc.LoadHtml(driver.PageSource);


                        var descNode = doc.DocumentNode.SelectSingleNode("//div[@class='job-description']");
                        if (descNode != null)
                        {
                            records[i].FullDescription = descNode.InnerText.Replace("\n", "").Replace("\r", "");
                        }

                        var typeNode = doc.DocumentNode.SelectSingleNode("/html/body/div[1]/div[1]/main/div/div/div/div/div[1]/div[1]/div/div/div[1]/article/div[3]/div/div[2]/span/span/span/span/span/span/span");
                        if (typeNode != null)
                        {
                            records[i].Type = HttpUtility.HtmlDecode(typeNode.InnerText.Replace("\n", "").Replace("\r", "").Trim());
                        }



                    }

                if (records.Count > 0)
                    entries.AddRange(records);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to scrape this page. Reason: " + ex.Message);
            }
        }
    }
}
