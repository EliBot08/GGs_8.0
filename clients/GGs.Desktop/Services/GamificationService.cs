using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace GGs.Desktop.Services;

public class GamificationService
{
    private UserProfile _userProfile;
    private readonly List<Achievement> _allAchievements;
    private readonly List<DailyChallenge> _dailyChallenges;
    private readonly string _savePath;
    
    public event EventHandler<AchievementUnlockedEventArgs>? AchievementUnlocked;
    public event EventHandler<LevelUpEventArgs>? LeveledUp;
    public event EventHandler<ChallengeCompletedEventArgs>? ChallengeCompleted;
    
    public GamificationService()
    {
        _savePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GGs", "UserProgress");
        Directory.CreateDirectory(_savePath);
        
        _userProfile = LoadUserProfile();
        _allAchievements = InitializeAchievements();
        _dailyChallenges = GenerateDailyChallenges();
    }
    
    public void AddXP(int amount, string reason)
    {
        var oldLevel = _userProfile.Level;
        _userProfile.XP += amount;
        
        // Check for level up
        var newLevel = CalculateLevel(_userProfile.XP);
        if (newLevel > oldLevel)
        {
            _userProfile.Level = newLevel;
            LeveledUp?.Invoke(this, new LevelUpEventArgs 
            { 
                NewLevel = newLevel, 
                UnlockedFeatures = GetUnlockedFeatures(newLevel) 
            });
        }
        
        // Track statistics
        _userProfile.Statistics[reason] = _userProfile.Statistics.GetValueOrDefault(reason, 0) + 1;
        
        SaveUserProfile();
    }
    
    public void CheckAchievements(string action, object data)
    {
        foreach (var achievement in _allAchievements.Where(a => !_userProfile.UnlockedAchievements.Contains(a.Id)))
        {
            if (achievement.CheckCondition(action, data, _userProfile))
            {
                UnlockAchievement(achievement);
            }
        }
    }
    
    private void UnlockAchievement(Achievement achievement)
    {
        _userProfile.UnlockedAchievements.Add(achievement.Id);
        _userProfile.Points += achievement.Points;
        
        AchievementUnlocked?.Invoke(this, new AchievementUnlockedEventArgs { Achievement = achievement });
        
        AddXP(achievement.XPReward, "Achievement");
        SaveUserProfile();
    }
    
    public void CheckDailyChallenge(string action, object data)
    {
        foreach (var challenge in _dailyChallenges.Where(c => !c.IsCompleted))
        {
            challenge.UpdateProgress(action, data);
            
            if (challenge.IsCompleted)
            {
                CompleteChallenge(challenge);
            }
        }
    }
    
    private void CompleteChallenge(DailyChallenge challenge)
    {
        _userProfile.CompletedChallenges.Add(challenge.Id);
        _userProfile.DailyStreak++;
        
        ChallengeCompleted?.Invoke(this, new ChallengeCompletedEventArgs { Challenge = challenge });
        
        AddXP(challenge.XPReward, "Daily Challenge");
        SaveUserProfile();
    }
    
    private int CalculateLevel(int xp)
    {
        // XP required per level increases exponentially
        return (int)Math.Floor(Math.Sqrt(xp / 100.0)) + 1;
    }
    
    private List<string> GetUnlockedFeatures(int level)
    {
        var features = new List<string>();
        
        if (level == 5) features.Add("Custom Profiles");
        if (level == 10) features.Add("Advanced Tweaks");
        if (level == 15) features.Add("Cloud Sync");
        if (level == 20) features.Add("Pro Optimizations");
        if (level == 25) features.Add("Beta Features");
        if (level == 30) features.Add("Elite Status");
        
        return features;
    }
    
    private List<Achievement> InitializeAchievements()
    {
        return new List<Achievement>
        {
            new Achievement
            {
                Id = "first_optimization",
                Name = "First Steps",
                Description = "Perform your first system optimization",
                Icon = "🎯",
                Points = 10,
                XPReward = 50,
                Category = "Beginner",
                CheckCondition = (action, data, profile) => action == "optimization" && profile.Statistics.GetValueOrDefault("optimization", 0) == 1
            },
            new Achievement
            {
                Id = "clean_100gb",
                Name = "Space Liberator",
                Description = "Clean 100GB of disk space",
                Icon = "💾",
                Points = 50,
                XPReward = 200,
                Category = "Cleaning",
                CheckCondition = (action, data, profile) => action == "clean" && profile.Statistics.GetValueOrDefault("totalCleaned", 0) >= 100000
            },
            new Achievement
            {
                Id = "gaming_master",
                Name = "Gaming Master",
                Description = "Optimize 10 different games",
                Icon = "🎮",
                Points = 100,
                XPReward = 500,
                Category = "Gaming",
                CheckCondition = (action, data, profile) => action == "game_optimized" && profile.Statistics.GetValueOrDefault("gamesOptimized", 0) >= 10
            },
            new Achievement
            {
                Id = "daily_warrior",
                Name = "Daily Warrior",
                Description = "Complete daily challenges for 30 days",
                Icon = "⚔️",
                Points = 200,
                XPReward = 1000,
                Category = "Dedication",
                CheckCondition = (action, data, profile) => profile.DailyStreak >= 30
            },
            new Achievement
            {
                Id = "performance_king",
                Name = "Performance King",
                Description = "Achieve 50% performance improvement",
                Icon = "👑",
                Points = 150,
                XPReward = 750,
                Category = "Performance",
                CheckCondition = (action, data, profile) => action == "performance_boost" && data is double boost && boost >= 50
            }
        };
    }
    
    private List<DailyChallenge> GenerateDailyChallenges()
    {
        var challenges = new List<DailyChallenge>
        {
            new DailyChallenge
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Quick Optimizer",
                Description = "Run 3 optimizations today",
                RequiredCount = 3,
                XPReward = 100,
                ExpiresAt = DateTime.Today.AddDays(1)
            },
            new DailyChallenge
            {
                Id = Guid.NewGuid().ToString(),
                Name = "System Monitor",
                Description = "Keep CPU usage below 50% for 2 hours",
                RequiredCount = 120, // minutes
                XPReward = 150,
                ExpiresAt = DateTime.Today.AddDays(1)
            },
            new DailyChallenge
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Clean Sweep",
                Description = "Clean at least 1GB of space",
                RequiredCount = 1000, // MB
                XPReward = 75,
                ExpiresAt = DateTime.Today.AddDays(1)
            }
        };
        
        return challenges;
    }
    
    private UserProfile LoadUserProfile()
    {
        try
        {
            var profilePath = Path.Combine(_savePath, "profile.json");
            if (File.Exists(profilePath))
            {
                var json = File.ReadAllText(profilePath);
                return JsonSerializer.Deserialize<UserProfile>(json) ?? new UserProfile();
            }
        }
        catch { }
        
        return new UserProfile { Username = Environment.UserName };
    }
    
    private void SaveUserProfile()
    {
        try
        {
            var profilePath = Path.Combine(_savePath, "profile.json");
            var json = JsonSerializer.Serialize(_userProfile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(profilePath, json);
        }
        catch { }
    }
    
    public UserProfile GetProfile() => _userProfile;
    public List<Achievement> GetAchievements() => _allAchievements;
    public List<DailyChallenge> GetDailyChallenges() => _dailyChallenges;
    
    public List<LeaderboardEntry> GetLeaderboard()
    {
        // In production, this would fetch from server
        return new List<LeaderboardEntry>
        {
            new LeaderboardEntry { Rank = 1, Username = "ProGamer123", Level = 42, Points = 15000 },
            new LeaderboardEntry { Rank = 2, Username = "OptimizeKing", Level = 38, Points = 13500 },
            new LeaderboardEntry { Rank = 3, Username = _userProfile.Username, Level = _userProfile.Level, Points = _userProfile.Points },
            new LeaderboardEntry { Rank = 4, Username = "TechWizard", Level = 28, Points = 9800 },
            new LeaderboardEntry { Rank = 5, Username = "SpeedDemon", Level = 25, Points = 8500 }
        };
    }
}

public class UserProfile
{
    public string Username { get; set; } = "";
    public int Level { get; set; } = 1;
    public int XP { get; set; } = 0;
    public int Points { get; set; } = 0;
    public int DailyStreak { get; set; } = 0;
    public List<string> UnlockedAchievements { get; set; } = new();
    public List<string> CompletedChallenges { get; set; } = new();
    public Dictionary<string, int> Statistics { get; set; } = new();
    public DateTime LastLogin { get; set; } = DateTime.Now;
}

public class Achievement
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
    public int Points { get; set; }
    public int XPReward { get; set; }
    public string Category { get; set; } = "";
    public Func<string, object, UserProfile, bool> CheckCondition { get; set; } = (_, _, _) => false;
}

public class DailyChallenge
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int CurrentProgress { get; set; }
    public int RequiredCount { get; set; }
    public int XPReward { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsCompleted => CurrentProgress >= RequiredCount;
    
    public void UpdateProgress(string action, object data)
    {
        // Update based on action type
        CurrentProgress++;
    }
}

public class LeaderboardEntry
{
    public int Rank { get; set; }
    public string Username { get; set; } = "";
    public int Level { get; set; }
    public int Points { get; set; }
}

public class AchievementUnlockedEventArgs : EventArgs
{
    public Achievement Achievement { get; set; } = null!;
}

public class LevelUpEventArgs : EventArgs
{
    public int NewLevel { get; set; }
    public List<string> UnlockedFeatures { get; set; } = new();
}

public class ChallengeCompletedEventArgs : EventArgs
{
    public DailyChallenge Challenge { get; set; } = null!;
}
