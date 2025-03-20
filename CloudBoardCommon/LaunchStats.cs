using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CloudBoardCommon
{
    public class LaunchStats
    {
        public int LaunchNumber { get; set; }
        public DateTime LaunchDate { get; set; }
        public DateTime? PreviousLaunchDate { get; set; }

        public override string ToString()
        {
            return $"LaunchNumber: {LaunchNumber}, LaunchDate: {LaunchDate}, PreviousLaunchDate: {PreviousLaunchDate}";
        }
    }

    public interface LaunchStatsStoreProtocol
    {
        Task<LaunchStats> LoadAndUpdateLaunchStats();
    }

    public class OnDiskLaunchStatsStore : LaunchStatsStoreProtocol
    {
        private readonly DirectoryInfo _directoryInfo;
        private readonly string _filePath;
        private readonly ILogger<OnDiskLaunchStatsStore> _logger;

        public OnDiskLaunchStatsStore(DirectoryInfo directoryInfo)
        {
            _directoryInfo = directoryInfo;
            _filePath = Path.Combine(directoryInfo.FullName, "launch_stats.json");
            _logger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<OnDiskLaunchStatsStore>();
        }

        public async Task<LaunchStats> LoadAndUpdateLaunchStats()
        {
            LaunchStats stats;

            try
            {
                if (File.Exists(_filePath))
                {
                    var json = await File.ReadAllTextAsync(_filePath);
                    stats = JsonSerializer.Deserialize<LaunchStats>(json) ?? new LaunchStats();
                }
                else
                {
                    stats = new LaunchStats();
                }

                // Update stats for this launch
                var previousLaunchDate = stats.LaunchDate;
                stats.PreviousLaunchDate = previousLaunchDate != default ? previousLaunchDate : null;
                stats.LaunchDate = DateTime.UtcNow;
                stats.LaunchNumber++;

                // Save updated stats
                await SaveStatsAsync(stats);

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading or updating launch stats");

                // Return default stats on error
                stats = new LaunchStats
                {
                    LaunchNumber = 1,
                    LaunchDate = DateTime.UtcNow
                };

                try
                {
                    await SaveStatsAsync(stats);
                }
                catch
                {
                    // Ignore errors when saving default stats
                }

                return stats;
            }
        }

        private async Task SaveStatsAsync(LaunchStats stats)
        {
            // Ensure directory exists
            Directory.CreateDirectory(_directoryInfo.FullName);

            // Write stats to file
            var json = JsonSerializer.Serialize(stats);
            await File.WriteAllTextAsync(_filePath, json);
        }
    }
}
