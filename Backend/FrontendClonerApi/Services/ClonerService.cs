using Microsoft.Playwright;
using System.Text.RegularExpressions;
using System.IO.Compression;
using Microsoft.AspNetCore.SignalR;

namespace FrontendClonerApi.Services;

public interface IClonerService
{
    Task<string> CloneWebsiteAsync(string url, string connectionId, bool deepScan = false);
}

public class ClonerService : IClonerService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ClonerService> _logger;
    private readonly Microsoft.AspNetCore.SignalR.IHubContext<FrontendClonerApi.Hubs.CloneProgressHub> _hubContext;

    public ClonerService(IHttpClientFactory httpClientFactory, ILogger<ClonerService> logger, Microsoft.AspNetCore.SignalR.IHubContext<FrontendClonerApi.Hubs.CloneProgressHub> hubContext)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task<string> CloneWebsiteAsync(string url, string connectionId, bool deepScan = false)
    {
        await SendLogAsync(connectionId, "[5%] Bắt đầu quá trình Clone...", 5);
        var baseTempDir = Path.Combine(Path.GetTempPath(), "FrontendCloner");
        Directory.CreateDirectory(baseTempDir);
        var tempFolder = Path.Combine(baseTempDir, Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempFolder);

        try
        {
            var uri = new Uri(url);
            var baseUrl = $"{uri.Scheme}://{uri.Host}{(uri.Port == 80 || uri.Port == 443 ? "" : ":" + uri.Port)}";

            await SendLogAsync(connectionId, "[10%] Đang khởi tạo Playwright Browser...", 10);
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
            });
            
            // Stealth mode for deep scan to bypass basic Anti-Bot scripts
            if (deepScan)
            {
                await context.AddInitScriptAsync(@"
                    Object.defineProperty(navigator, 'webdriver', {get: () => undefined});
                    window.chrome = { runtime: {} };
                    Object.defineProperty(navigator, 'languages', {get: () => ['en-US', 'en']});
                    Object.defineProperty(navigator, 'plugins', {get: () => [1, 2, 3]});
                ");
            }

            var page = await context.NewPageAsync();

            try
            {
                await SendLogAsync(connectionId, $"[20%] Đang điều hướng đến {url}...", 20);
                // Use Load instead of NetworkIdle to prevent hanging on sites with constant network requests
                await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 30000 });
                
                if (deepScan)
                {
                    _logger.LogInformation("Deep scan enabled. Injecting auto-scroll script and interacting...");
                    
                    // Auto scroll to bottom smoothly
                    await page.EvaluateAsync(@"async () => {
                        await new Promise((resolve) => {
                            let totalHeight = 0;
                            const distance = 100;
                            const timer = setInterval(() => {
                                const scrollHeight = document.body.scrollHeight;
                                window.scrollBy(0, distance);
                                totalHeight += distance;
                                if(totalHeight >= scrollHeight - window.innerHeight){
                                    clearInterval(timer);
                                    resolve();
                                }
                            }, 100);
                        });
                    }");
                    
                    // Wait after heavy scrolling for images/ajax to load
                    await page.WaitForTimeoutAsync(5000);
                    
                    // Specific interactions: Click common expand/load more buttons
                    try
                    {
                        var expandSelectors = new[] { ".tab-item", ".btn-expand", ".load-more", "button:has-text('Read more')", "button:has-text('Load more')" };
                        foreach (var sel in expandSelectors)
                        {
                            var locators = await page.Locator(sel).AllAsync();
                            foreach (var loc in locators)
                            {
                                if (await loc.IsVisibleAsync()) await loc.ClickAsync();
                            }
                        }
                    } catch { /* ignore interaction errors */ }

                    // Wrap external links/buttons with mock alerts locally instead of causing 404s
                    await page.EvaluateAsync(@"() => {
                        document.querySelectorAll('a, button').forEach(el => {
                            if (el.tagName === 'A' && el.href && (el.href.startsWith('http') && !el.href.includes(window.location.hostname))) {
                                el.removeAttribute('href');
                                el.onclick = (e) => { e.preventDefault(); alert('Navigation disabled: Tính năng hoặc trang này ngoại tuyến trong bản Clone.'); };
                            }
                            if (el.tagName === 'BUTTON' && el.getAttribute('onclick')) {
                                el.setAttribute('data-original-onclick', el.getAttribute('onclick'));
                                el.setAttribute('onclick', ""alert('Action disabled: Nút bấm này đã được vô hiệu hoá khi chạy Clone offline.');"");
                            }
                        });
                    }");
                }
                else
                {
                    // Wait a few seconds for SPA javascript to render the DOM normally
                    await page.WaitForTimeoutAsync(5000);
                }
            }
            catch (Exception ex) when (ex.Message.Contains("Timeout"))
            {
                await SendLogAsync(connectionId, $"[30%] Timeout khi chờ trang load, tiếp tục với phần đã render...", 30);
                _logger.LogWarning("Playwright navigation timeout exceeded, proceeding with whatever is rendered.");
            }

            await SendLogAsync(connectionId, $"[40%] Đang quét cấu trúc HTML và CSS...", 40);
            var html = await page.ContentAsync();

            // Find all link hrefs, script srcs, and img srcs
            var resources = new List<string>();
            var linkHrefs = await page.EvaluateAsync<string[]>("Array.from(document.querySelectorAll('link[rel=\"stylesheet\"], link[rel=\"icon\"]')).map(el => el.href).filter(h => h)");
            var scriptSrcs = await page.EvaluateAsync<string[]>("Array.from(document.querySelectorAll('script')).map(el => el.src).filter(s => s)");
            var imgSrcs = await page.EvaluateAsync<string[]>("Array.from(document.querySelectorAll('img')).map(el => el.src).filter(s => s)");

            resources.AddRange(linkHrefs);
            resources.AddRange(scriptSrcs);
            resources.AddRange(imgSrcs);
            resources = resources.Distinct().ToList();

            await SendLogAsync(connectionId, $"[50%] Tìm thấy {resources.Count} assets cần tải. Đang tiến hành tải...", 50);

            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            var downloadedCount = 0;
            var totalAssets = resources.Count;
            
            var downloadTasks = resources.Select(async resUrl => 
            {
                try
                {
                    var resUri = new Uri(resUrl);
                    var relativePath = resUri.AbsolutePath.TrimStart('/');
                    if (string.IsNullOrEmpty(relativePath)) relativePath = "index.html";
                    
                    // Decode URL parts to make valid folder/file names
                    relativePath = Uri.UnescapeDataString(relativePath);
                    // sanitize windows invalid chars
                    relativePath = Regex.Replace(relativePath, @"[<>:""|?*]", "_");
                    
                    if (relativePath.Length > 150)
                    {
                        var ext = Path.GetExtension(relativePath);
                        if (ext.Length > 20) ext = ext.Substring(0, 20);
                        using var md5 = System.Security.Cryptography.MD5.Create();
                        var hash = string.Concat(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(relativePath)).Select(b => b.ToString("x2")));
                        relativePath = "assets/" + hash + ext;
                    }

                    var localPath = Path.Combine(tempFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    var dir = Path.GetDirectoryName(localPath);
                    if (dir != null && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    var bytes = await client.GetByteArrayAsync(resUri);
                    await File.WriteAllBytesAsync(localPath, bytes);

                    // Rewrite HTML
                    if (resUrl.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
                    {
                        var cssContent = System.Text.Encoding.UTF8.GetString(bytes);
                        cssContent = await ProcessCssAsync(cssContent, resUri, tempFolder, client);
                        await File.WriteAllTextAsync(localPath, cssContent);
                    }
                    
                    var currentDownloaded = Interlocked.Increment(ref downloadedCount);
                    if (currentDownloaded % 10 == 0 || currentDownloaded == totalAssets)
                    {
                        // Update progress from 50 to 90
                        var progress = 50 + (int)((double)currentDownloaded / totalAssets * 40);
                        await SendLogAsync(connectionId, $"[Assets] Đã tải {currentDownloaded}/{totalAssets} file...", progress);
                    }
                    
                    return new { Original = resUrl, Relative = relativePath };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to download {resUrl}: {ex.Message}");
                    return null;
                }
            }).ToList();

            var downloadedItems = await Task.WhenAll(downloadTasks);

            // Build a lookup: fullAbsoluteUrl → local relative path
            var urlMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in downloadedItems.Where(i => i != null))
            {
                if (item == null) continue;
                // Map full absolute URL
                urlMap[item.Original] = "./" + item.Relative.Replace('\\', '/');
                // Map just the path part (root-relative)
                var originalUri2 = new Uri(item.Original);
                urlMap[originalUri2.AbsolutePath] = "./" + item.Relative.Replace('\\', '/');
                // Map path+query for resources that have query strings
                if (!string.IsNullOrEmpty(originalUri2.Query))
                    urlMap[originalUri2.PathAndQuery] = "./" + item.Relative.Replace('\\', '/');
            }

            // Sort by key length descending to replace the most specific (longest) first
            // This prevents partial replacements
            foreach (var kv in urlMap.OrderByDescending(kv => kv.Key.Length))
            {
                html = html.Replace(kv.Key, kv.Value);
            }

            await File.WriteAllTextAsync(Path.Combine(tempFolder, "index.html"), html);

            await SendLogAsync(connectionId, $"[95%] Đang nén file ZIP...", 95);

            var zipPath = tempFolder + ".zip";
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(tempFolder, zipPath);

            await SendLogAsync(connectionId, $"[100%] Hoàn tất! File sẵn sàng để tải xuống.", 100);

            return zipPath;
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                try { Directory.Delete(tempFolder, true); } catch { /* Ignore cleanup errors on temp folders */ }
            }
        }
    }

    private async Task SendLogAsync(string connectionId, string message, int percent)
    {
        if (string.IsNullOrEmpty(connectionId)) return;
        try
        {
            await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveLog", message, percent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to send SignalR log: {ex.Message}");
        }
    }

    private async Task<string> ProcessCssAsync(string cssContent, Uri cssUri, string tempFolder, HttpClient client)
    {
        var regex = new Regex(@"url\((['""]?)(.*?)\1\)", RegexOptions.IgnoreCase);
        var matches = regex.Matches(cssContent);
        var downloadTasks = new List<Task<(string oldUrl, string newUrl)>>();

        var cssBaseUrl = $"{cssUri.Scheme}://{cssUri.Host}{(cssUri.Port == 80 || cssUri.Port == 443 ? "" : ":" + cssUri.Port)}";
        var cssDir = Path.GetDirectoryName(cssUri.AbsolutePath) ?? "";
        cssDir = cssDir.Replace('\\', '/');
        if (!cssDir.EndsWith("/")) cssDir += "/";

        var uniqueUrls = matches.Select(m => m.Groups[2].Value).Distinct().Where(u => !u.StartsWith("data:")).ToList();

        foreach (var relativeOrAbsoluteUrl in uniqueUrls)
        {
            downloadTasks.Add(Task.Run(async () => 
            {
                try
                {
                    Uri assetUri;
                    if (relativeOrAbsoluteUrl.StartsWith("http"))
                    {
                        assetUri = new Uri(relativeOrAbsoluteUrl);
                    }
                    else if (relativeOrAbsoluteUrl.StartsWith("/"))
                    {
                        assetUri = new Uri(new Uri(cssBaseUrl), relativeOrAbsoluteUrl);
                    }
                    else
                    {
                        assetUri = new Uri(new Uri(cssBaseUrl), cssDir + relativeOrAbsoluteUrl);
                    }

                    var relativePath = assetUri.AbsolutePath.TrimStart('/');
                    relativePath = Uri.UnescapeDataString(relativePath);
                    relativePath = Regex.Replace(relativePath, @"[<>:""|?*]", "_");

                    if (relativePath.Length > 150)
                    {
                        var ext = Path.GetExtension(relativePath);
                        if (ext.Length > 20) ext = ext.Substring(0, 20);
                        using var md5 = System.Security.Cryptography.MD5.Create();
                        var hash = string.Concat(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(relativePath)).Select(b => b.ToString("x2")));
                        relativePath = "assets/" + hash + ext;
                    }

                    var localPath = Path.Combine(tempFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    var dir = Path.GetDirectoryName(localPath);
                    if (dir != null && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    if (!File.Exists(localPath))
                    {
                        var bytes = await client.GetByteArrayAsync(assetUri);
                        await File.WriteAllBytesAsync(localPath, bytes);
                    }

                    // Compute relative path from css file to asset
                    var cssRelativeDir = cssUri.AbsolutePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                    var cssDepth = Math.Max(0, cssRelativeDir.Length - 1);
                    var pathUp = string.Concat(Enumerable.Repeat("../", cssDepth));
                    var newRelativeUrl = pathUp + relativePath;

                    return (relativeOrAbsoluteUrl, newRelativeUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to download CSS asset {relativeOrAbsoluteUrl}: {ex.Message}");
                    return (relativeOrAbsoluteUrl, relativeOrAbsoluteUrl);
                }
            }));
        }

        var results = await Task.WhenAll(downloadTasks);
        foreach (var res in results)
        {
            if (res.oldUrl != res.newUrl)
            {
                cssContent = cssContent.Replace(res.oldUrl, res.newUrl);
            }
        }
        return cssContent;
    }
}
