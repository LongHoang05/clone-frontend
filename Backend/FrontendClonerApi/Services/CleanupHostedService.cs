using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FrontendClonerApi.Services;

public class CleanupHostedService : IHostedService, IDisposable
{
    private readonly ILogger<CleanupHostedService> _logger;
    private Timer? _timer;
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "FrontendCloner");
    private readonly TimeSpan _maxAge = TimeSpan.FromHours(1);

    public CleanupHostedService(ILogger<CleanupHostedService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cleanup Hosted Service running.");

        // Run every 1 hour
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromHours(1));

        return Task.CompletedTask;
    }

    private void DoWork(object? state)
    {
        _logger.LogInformation("Cleanup task starting...");

        if (!Directory.Exists(_tempDirectory))
            return;

        try
        {
            var now = DateTime.Now;

            // Delete old files (.zip)
            var files = Directory.GetFiles(_tempDirectory);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (now - fileInfo.CreationTime > _maxAge)
                {
                    try
                    {
                        fileInfo.Delete();
                        _logger.LogInformation($"Deleted old file: {file}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to delete file {file}: {ex.Message}");
                    }
                }
            }

            // Delete old directories
            var directories = Directory.GetDirectories(_tempDirectory);
            foreach (var dir in directories)
            {
                var dirInfo = new DirectoryInfo(dir);
                if (now - dirInfo.CreationTime > _maxAge)
                {
                    try
                    {
                        dirInfo.Delete(true);
                        _logger.LogInformation($"Deleted old directory: {dir}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to delete directory {dir}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in cleanup task: {ex.Message}");
        }

        _logger.LogInformation("Cleanup task finished.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cleanup Hosted Service is stopping.");

        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
