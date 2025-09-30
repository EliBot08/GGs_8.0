using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using GGs.Shared.Api;
using GGs.Shared.Enums;
using GGs.Shared.Tweaks;
using GGs.Desktop.Services;
using GGs.Desktop.Extensions;
using System.Windows;

namespace GGs.Desktop.ViewModels;

public class TweakManagementViewModel : BaseViewModel
{
    private readonly GGs.Shared.Api.ApiClient _api;
    private readonly TweakExecutionService _executor;
    private readonly UndoRedoService _undoRedo;
    
    public bool AllowedEdit { get; set; } = true;
    public bool AllowedDelete { get; set; } = true;
    public bool AllowedExecute { get; set; } = true; // dynamically evaluated for high-risk tweaks

    public ObservableCollection<TweakDefinition> Tweaks { get; } = new();

    private TweakDefinition? _selectedTweak;
    public TweakDefinition? SelectedTweak
    {
        get => _selectedTweak;
        set
        {
            if (SetField(ref _selectedTweak, value))
            {
                if (value != null)
                    EditModel = Clone(value);
                RefreshCapabilities();
            }
        }
    }

    private TweakDefinition _editModel = new() { Name = "", CommandType = CommandType.Registry };
    public TweakDefinition EditModel
    {
        get => _editModel;
        set
        {
            if (SetField(ref _editModel, value))
            {
                RefreshCapabilities();
            }
        }
    }

    private string _status = string.Empty;
    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ExecuteLocalCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand ExportProfileCommand { get; }
    public ICommand ImportProfileCommand { get; }

    // Scripts policy (Prompt 30)
    private string _scriptMode = string.Empty;
    public string ScriptMode
    {
        get => _scriptMode;
        set => SetField(ref _scriptMode, value);
    }
    public IEnumerable<string> ScriptModes => Services.SecurityPolicyService.Modes;
    public bool IsAdmin => Services.EntitlementsService.IsAdmin;
    public ICommand ApplyScriptModeCommand { get; }

    // Enum sources for bindings
    public IEnumerable<CommandType> CommandTypes => Enum.GetValues<CommandType>();
    public IEnumerable<SafetyLevel> SafetyLevels => Enum.GetValues<SafetyLevel>();
    public IEnumerable<RiskLevel> RiskLevels => Enum.GetValues<RiskLevel>();
    public IEnumerable<ServiceAction> ServiceActions => Enum.GetValues<ServiceAction>();

    public TweakManagementViewModel(GGs.Shared.Api.ApiClient api)
    {
        _api = api;
        _executor = new TweakExecutionService();
        _undoRedo = new UndoRedoService(_executor);

        RefreshCommand = new RelayCommand(async () => await LoadTweaks());

        // Permissions from entitlements
        try { UpdatePermissions(); } catch { }
        Services.EntitlementsService.Changed += (_, __) => { try { UpdatePermissions(); } catch { } };
        NewCommand = new RelayCommand(() => { if (AllowedEdit) EditModel = new TweakDefinition { Name = "New Tweak", CommandType = CommandType.Registry, Safety = SafetyLevel.Medium, Risk = RiskLevel.Medium, RequiresAdmin = true, AllowUndo = true }; else Status = "You do not have permission to create tweaks."; });
        SaveCommand = new RelayCommand(async () => await SaveAsync());
        DeleteCommand = new RelayCommand(async () => await DeleteAsync());
        ExecuteLocalCommand = new RelayCommand(async () => await ExecuteLocalAsync());
        UndoCommand = new RelayCommand(async () => await UndoAsync(), () => _undoRedo.CanUndo);
        RedoCommand = new RelayCommand(async () => await RedoAsync(), () => _undoRedo.CanRedo);
        ExportProfileCommand = new RelayCommand(() => ExportProfile());
        ImportProfileCommand = new RelayCommand(async () => await ImportProfile());

        // Init scripts policy state
        try { ScriptMode = Services.SecurityPolicyService.GetScriptMode(); } catch { ScriptMode = "moderate"; }
        ApplyScriptModeCommand = new RelayCommand(() => ApplyScriptMode());
    }

    public async Task LoadTweaks()
    {
        Tweaks.Clear();
        var list = await _api.GetTweaksAsync();
        foreach (var t in list) Tweaks.Add(t);
        Status = $"Loaded {Tweaks.Count} tweaks.";
    }

    private async Task SaveAsync()
    {
        if (!AllowedEdit)
        {
            Status = "You do not have permission to save tweaks.";
            return;
        }
        if (SelectedTweak == null || SelectedTweak.Id == Guid.Empty)
        {
            var created = await _api.CreateTweakAsync(new GGs.Shared.Tweaks.TweakDefinition
            {
                Id = Guid.NewGuid(),
                Name = EditModel.Name,
                Description = EditModel.Description,
                Category = EditModel.Category,
                CommandType = EditModel.CommandType,
                RegistryPath = EditModel.RegistryPath,
                RegistryValueName = EditModel.RegistryValueName,
                RegistryValueType = EditModel.RegistryValueType,
                RegistryValueData = EditModel.RegistryValueData,
                ServiceName = EditModel.ServiceName,
                ServiceAction = EditModel.ServiceAction,
                ScriptContent = EditModel.ScriptContent,
                Safety = EditModel.Safety,
                Risk = EditModel.Risk,
                RequiresAdmin = EditModel.RequiresAdmin,
                AllowUndo = EditModel.AllowUndo,
                UndoScriptContent = EditModel.UndoScriptContent
            });
            if (created)
            {
                // Add the new tweak to the collection
                var newTweak = new GGs.Shared.Tweaks.TweakDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = EditModel.Name,
                    Description = EditModel.Description,
                    Category = EditModel.Category,
                    CommandType = EditModel.CommandType,
                    RegistryPath = EditModel.RegistryPath,
                    RegistryValueName = EditModel.RegistryValueName,
                    RegistryValueType = EditModel.RegistryValueType,
                    RegistryValueData = EditModel.RegistryValueData,
                    ServiceName = EditModel.ServiceName,
                    ServiceAction = EditModel.ServiceAction,
                    ScriptContent = EditModel.ScriptContent,
                    Safety = EditModel.Safety,
                    Risk = EditModel.Risk,
                    RequiresAdmin = EditModel.RequiresAdmin,
                    AllowUndo = EditModel.AllowUndo,
                    UndoScriptContent = EditModel.UndoScriptContent
                };
                Tweaks.Add(newTweak);
                SelectedTweak = newTweak;
                Status = "Created new tweak.";
            }
        }
        else
        {
            var updated = await _api.UpdateTweakAsync(new GGs.Shared.Tweaks.TweakDefinition
            {
                Id = SelectedTweak.Id,
                Name = EditModel.Name,
                Description = EditModel.Description,
                Category = EditModel.Category,
                CommandType = EditModel.CommandType,
                RegistryPath = EditModel.RegistryPath,
                RegistryValueName = EditModel.RegistryValueName,
                RegistryValueType = EditModel.RegistryValueType,
                RegistryValueData = EditModel.RegistryValueData,
                ServiceName = EditModel.ServiceName,
                ServiceAction = EditModel.ServiceAction,
                ScriptContent = EditModel.ScriptContent,
                Safety = EditModel.Safety,
                Risk = EditModel.Risk,
                RequiresAdmin = EditModel.RequiresAdmin,
                AllowUndo = EditModel.AllowUndo,
                UndoScriptContent = EditModel.UndoScriptContent
            });
            if (updated)
            {
                // Update the existing tweak in the collection
                var existingTweak = Tweaks.FirstOrDefault(t => t.Id == SelectedTweak.Id);
                if (existingTweak != null)
                {
                    existingTweak.Name = EditModel.Name;
                    existingTweak.Description = EditModel.Description;
                    existingTweak.Category = EditModel.Category;
                    existingTweak.CommandType = EditModel.CommandType;
                    existingTweak.RegistryPath = EditModel.RegistryPath;
                    existingTweak.RegistryValueName = EditModel.RegistryValueName;
                    existingTweak.RegistryValueType = EditModel.RegistryValueType;
                    existingTweak.RegistryValueData = EditModel.RegistryValueData;
                    existingTweak.ServiceName = EditModel.ServiceName;
                    existingTweak.ServiceAction = EditModel.ServiceAction;
                    existingTweak.ScriptContent = EditModel.ScriptContent;
                    existingTweak.Safety = EditModel.Safety;
                    existingTweak.Risk = EditModel.Risk;
                    existingTweak.RequiresAdmin = EditModel.RequiresAdmin;
                    existingTweak.AllowUndo = EditModel.AllowUndo;
                    existingTweak.UndoScriptContent = EditModel.UndoScriptContent;
                }
                Status = "Saved changes.";
            }
        }
    }

    private async Task DeleteAsync()
    {
        if (!AllowedDelete)
        {
            Status = "You do not have permission to delete tweaks.";
            return;
        }
        if (SelectedTweak == null) return;
        await _api.DeleteTweakAsync(SelectedTweak.Id.ToString());
        Tweaks.Remove(SelectedTweak);
        Status = "Deleted tweak.";
        SelectedTweak = null;
        EditModel = new TweakDefinition { Name = "", CommandType = CommandType.Registry };
    }

    private async Task ExecuteLocalAsync()
    {
        var model = SelectedTweak ?? EditModel;
        // dynamic evaluation in case settings/service state changed
        RefreshCapabilities();
        if (!AllowedExecute)
        {
            var (exists, running) = GetAgentState();
            if (!Services.SettingsService.DeepOptimizationEnabled)
            {
                Status = "Deep Optimization is disabled. Enable it in Settings to run high-risk tweaks.";
            }
            else if (!exists)
            {
                Status = "Agent service not installed. Install it from Settings > Deep Optimization.";
            }
            else if (!running)
            {
                Status = "Agent service is stopped. Start it from Settings > Deep Optimization.";
            }
            else
            {
                Status = "Execution is not allowed by current policy.";
            }
            return;
        }
        // Risk notification/confirmation
        if (model.Risk == RiskLevel.High || model.Risk == RiskLevel.Critical)
        {
            var res = MessageBox.Show($"This tweak is marked as {model.Risk}. Proceed?", "Risk Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) { Status = "Execution cancelled."; return; }
        }
        var log = await _executor.ExecuteTweakAsync(model);
        if (log != null)
        {
            if (model.AllowUndo)
                _undoRedo.RecordAction("Execute", log);
            Status = BuildStatusFromLog("Execute", log);
            try { await _api.PostAuditLogAsync(log); } catch { /* ignore */ }
            Services.NotificationCenter.Add(Services.NotificationType.Tweak, log.Success ? $"Executed '{model.Name}'" : $"Failed '{model.Name}': {log.Error}", navigateTab: "Analytics", tweakId: model.Id, logId: log.Id);
        }
    }

    private async Task<bool> UndoAsync()
    {
        var ok = await _undoRedo.UndoAsync();
        Status = ok ? "Undo complete." : "Nothing to undo.";
        return ok;
    }
    private async Task<bool> RedoAsync()
    {
        var ok = await _undoRedo.RedoAsync();
        Status = ok ? "Redo complete." : "Nothing to redo.";
        return ok;
    }

    private void ExportProfile()
    {
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "GGs Profiles (*.ggsp)|*.ggsp|JSON (*.json)|*.json", FileName = "profile.ggsp" };
            if (dlg.ShowDialog() == true)
            {
            var list = (SelectedTweak != null ? new[] { SelectedTweak } : Tweaks.ToArray()).ToList();
                Services.ProfileService.Export(dlg.FileName, list, name: "Exported Profile");
                Status = $"Exported {list.Count()} tweak(s).";
            }
        }
        catch (Exception ex) { Status = ex.Message; }
    }

    private Task ImportProfile()
    {
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "GGs Profiles (*.ggsp;*.json)|*.ggsp;*.json" };
            if (dlg.ShowDialog() == true)
            {
                var profile = Services.ProfileService.Import(dlg.FileName);
                int added = 0;
                foreach (var t in profile.Tweaks)
                {
                    // Avoid duplicate IDs in the local list
                    if (!Tweaks.Any(x => x.Id == t.Id)) { Tweaks.Add(t); added++; }
                }
                Status = $"Imported profile '{profile.Name}' with {added} new tweak(s). Save to persist to server.";
            }
        }
        catch (Exception ex) { Status = ex.Message; }
        return Task.CompletedTask;
    }

    private void ApplyScriptMode()
    {
        if (!IsAdmin)
        {
            Status = "Only administrators can change script policy mode.";
            return;
        }
        try
        {
            var ok = Services.SecurityPolicyService.SetScriptMode(ScriptMode, machineWide: true);
            Status = ok ? $"Applied script policy mode: {ScriptMode}. Changes may require restarting the agent service." : "Failed to apply script policy mode.";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private static TweakDefinition Clone(TweakDefinition t)
        => new()
        {
            Id = t.Id,
            Name = t.Name,
            Description = t.Description,
            Category = t.Category,
            CommandType = t.CommandType,
            RegistryPath = t.RegistryPath,
            RegistryValueName = t.RegistryValueName,
            RegistryValueType = t.RegistryValueType,
            RegistryValueData = t.RegistryValueData,
            ServiceName = t.ServiceName,
            ServiceAction = t.ServiceAction,
            ScriptContent = t.ScriptContent,
            Safety = t.Safety,
            Risk = t.Risk,
            RequiresAdmin = t.RequiresAdmin,
            AllowUndo = t.AllowUndo,
            UndoScriptContent = t.UndoScriptContent
        };

    private void RefreshCapabilities()
    {
        try
        {
            var t = SelectedTweak ?? EditModel;
            if (t == null)
            {
                AllowedExecute = Services.EntitlementsService.HasCapability(GGs.Shared.Enums.Capability.ExecuteTweaks);
                return;
            }
            // Start with entitlements
            var allow = Services.EntitlementsService.HasCapability(GGs.Shared.Enums.Capability.ExecuteTweaks);
            // Gate high-risk tweaks behind Deep Optimization + running Agent service
            if (allow && (t.Risk == RiskLevel.High || t.Risk == RiskLevel.Critical))
            {
                var deep = Services.SettingsService.DeepOptimizationEnabled;
                var (_, running) = GetAgentState();
                allow = deep && running;
            }
            AllowedExecute = allow;
        }
        catch { AllowedExecute = true; }
    }

    private void UpdatePermissions()
    {
        AllowedEdit = Services.EntitlementsService.HasCapability(GGs.Shared.Enums.Capability.ManageTweaks);
        AllowedDelete = Services.EntitlementsService.HasCapability(GGs.Shared.Enums.Capability.ManageTweaks);
        // Recompute execute state based on current selection and entitlements
        RefreshCapabilities();
        OnPropertyChanged(nameof(AllowedEdit));
        OnPropertyChanged(nameof(AllowedDelete));
        OnPropertyChanged(nameof(AllowedExecute));
    }

    private static (bool exists, bool running) GetAgentState()
    {
        try
        {
            bool exists, running;
            var ok = Services.AgentServiceHelper.TryGetStatus(out exists, out running);
            return ok ? (exists, running) : (false, false);
        }
        catch { return (false, false); }
    }

    private static string BuildStatusFromLog(string action, TweakApplicationLog log)
    {
        return log.Success
            ? $"{action} OK — Before: {Trim(log.BeforeState)} -> After: {Trim(log.AfterState)}"
            : $"{action} Failed — {log.Error}";
    }

    private static string Trim(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "<none>";
        s = s.Replace("\n", " ").Replace("\r", " ");
        return s.Length > 160 ? s.Substring(0, 160) + "…" : s;
    }
}

