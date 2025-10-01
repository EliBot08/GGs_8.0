#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GGs.ErrorLogViewer.Models;
using Microsoft.Extensions.Logging;

namespace GGs.ErrorLogViewer.Services
{
    public interface ILogComparisonService
    {
        Task<LogComparisonResult> CompareLogsAsync(
            IEnumerable<LogEntry> leftLogs, 
            IEnumerable<LogEntry> rightLogs,
            double similarityThreshold = 0.8);
        
        double CalculateSimilarity(LogEntry left, LogEntry right);
        List<(LogEntry Left, LogEntry Right)> FindSimilarEntries(
            IEnumerable<LogEntry> leftLogs, 
            IEnumerable<LogEntry> rightLogs, 
            double threshold = 0.8);
    }

    public class LogComparisonService : ILogComparisonService
    {
        private readonly ILogger<LogComparisonService> _logger;

        public LogComparisonService(ILogger<LogComparisonService> logger)
        {
            _logger = logger;
        }

        public async Task<LogComparisonResult> CompareLogsAsync(
            IEnumerable<LogEntry> leftLogs,
            IEnumerable<LogEntry> rightLogs,
            double similarityThreshold = 0.8)
        {
            return await Task.Run(() =>
            {
                var leftList = leftLogs.ToList();
                var rightList = rightLogs.ToList();
                
                _logger.LogInformation("Comparing {LeftCount} left logs with {RightCount} right logs",
                    leftList.Count, rightList.Count);

                var result = new LogComparisonResult();
                var processedRight = new HashSet<long>();
                var processedLeft = new HashSet<long>();

                // Find exact matches and similar entries
                foreach (var left in leftList)
                {
                    var bestMatch = FindBestMatch(left, rightList, similarityThreshold);

                    if (bestMatch.Match != null)
                    {
                        var similarity = bestMatch.Similarity;
                        
                        if (similarity >= 0.99)
                        {
                            // Exact match
                            result.Common.Add((left, bestMatch.Match));
                        }
                        else
                        {
                            // Similar but not identical
                            result.Similar.Add((left, bestMatch.Match));
                        }

                        processedLeft.Add(left.Id);
                        processedRight.Add(bestMatch.Match.Id);
                    }
                }

                // Find unique entries
                result.LeftOnly = leftList.Where(e => !processedLeft.Contains(e.Id)).ToList();
                result.RightOnly = rightList.Where(e => !processedRight.Contains(e.Id)).ToList();

                // Calculate statistics
                result.Statistics = new ComparisonStatistics
                {
                    TotalLeft = leftList.Count,
                    TotalRight = rightList.Count,
                    UniqueLeft = result.LeftOnly.Count,
                    UniqueRight = result.RightOnly.Count,
                    Identical = result.Common.Count,
                    Similar = result.Similar.Count,
                    SimilarityPercentage = CalculateOverallSimilarity(leftList.Count, rightList.Count,
                        result.Common.Count, result.Similar.Count)
                };

                _logger.LogInformation(
                    "Comparison complete: {Identical} identical, {Similar} similar, {UniqueLeft} left-only, {UniqueRight} right-only",
                    result.Statistics.Identical, result.Statistics.Similar, 
                    result.Statistics.UniqueLeft, result.Statistics.UniqueRight);

                return result;
            });
        }

        public double CalculateSimilarity(LogEntry left, LogEntry right)
        {
            if (left == null || right == null)
                return 0;

            double score = 0;
            int factors = 0;

            // Level match (20% weight)
            if (left.Level == right.Level)
            {
                score += 0.20;
            }
            factors++;

            // Source similarity (15% weight)
            var sourceSimilarity = CalculateStringSimilarity(left.Source, right.Source);
            score += sourceSimilarity * 0.15;
            factors++;

            // Message similarity (45% weight) - most important
            var messageSimilarity = CalculateStringSimilarity(left.Message, right.Message);
            score += messageSimilarity * 0.45;
            factors++;

            // Timestamp proximity (10% weight)
            var timeDiff = Math.Abs((left.Timestamp - right.Timestamp).TotalMinutes);
            var timeScore = timeDiff < 1 ? 1.0 : 
                           timeDiff < 5 ? 0.8 :
                           timeDiff < 30 ? 0.5 : 
                           timeDiff < 1440 ? 0.2 : 0.0;
            score += timeScore * 0.10;
            factors++;

            // Exception similarity (10% weight)
            if (!string.IsNullOrEmpty(left.Exception) && !string.IsNullOrEmpty(right.Exception))
            {
                var exceptionSimilarity = CalculateStringSimilarity(left.Exception!, right.Exception!);
                score += exceptionSimilarity * 0.10;
            }
            else if (string.IsNullOrEmpty(left.Exception) && string.IsNullOrEmpty(right.Exception))
            {
                score += 0.10; // Both have no exception
            }
            factors++;

            return score;
        }

        public List<(LogEntry Left, LogEntry Right)> FindSimilarEntries(
            IEnumerable<LogEntry> leftLogs,
            IEnumerable<LogEntry> rightLogs,
            double threshold = 0.8)
        {
            var leftList = leftLogs.ToList();
            var rightList = rightLogs.ToList();
            var similarPairs = new List<(LogEntry, LogEntry)>();

            foreach (var left in leftList)
            {
                var match = FindBestMatch(left, rightList, threshold);
                if (match.Match != null)
                {
                    similarPairs.Add((left, match.Match));
                }
            }

            return similarPairs;
        }

        private (LogEntry? Match, double Similarity) FindBestMatch(
            LogEntry entry, 
            List<LogEntry> candidates, 
            double threshold)
        {
            LogEntry? bestMatch = null;
            double bestScore = threshold;

            foreach (var candidate in candidates)
            {
                var similarity = CalculateSimilarity(entry, candidate);
                if (similarity > bestScore)
                {
                    bestScore = similarity;
                    bestMatch = candidate;
                }
            }

            return (bestMatch, bestScore);
        }

        private double CalculateStringSimilarity(string? str1, string? str2)
        {
            if (str1 == null && str2 == null) return 1.0;
            if (str1 == null || str2 == null) return 0.0;
            if (str1 == str2) return 1.0;

            // Use Levenshtein distance for similarity
            var distance = LevenshteinDistance(str1, str2);
            var maxLength = Math.Max(str1.Length, str2.Length);
            
            if (maxLength == 0) return 1.0;
            
            return 1.0 - ((double)distance / maxLength);
        }

        private int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }

        private double CalculateOverallSimilarity(int leftCount, int rightCount, int identical, int similar)
        {
            if (leftCount == 0 && rightCount == 0) return 100.0;
            if (leftCount == 0 || rightCount == 0) return 0.0;

            var maxCount = Math.Max(leftCount, rightCount);
            var matchScore = (identical * 1.0 + similar * 0.7) / maxCount;
            
            return Math.Min(100.0, matchScore * 100.0);
        }
    }
}
