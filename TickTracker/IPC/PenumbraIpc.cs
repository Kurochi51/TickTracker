using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;

using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace TickTracker.IPC;

public sealed class PenumbraIpc : IDisposable
{
    private readonly IPluginLog log;

    private readonly ICallGateSubscriber<(int Breaking, int FeatureLevel)> apiVersions;
    private readonly ICallGateSubscriber<bool> penumbraModsState;
    private readonly ICallGateSubscriber<object> penumbraInit;
    private readonly ICallGateSubscriber<object> penumbraDispose;
    private readonly ICallGateSubscriber<bool, Action<bool>?> penumbraEnabledChange;
    /// <summary>
    ///     Key is directory name, Value is mod name.
    /// </summary>
    private readonly ICallGateSubscriber<Dictionary<string, string>> modList;
    private readonly ICallGateSubscriber<Guid, string, string, bool,
        (PenumbraApiEc status, (bool modEnabled, int priority, Dictionary<string, List<string>> optionDetails, bool ignoreInheritance)? settings)> modSettings;
    private readonly ICallGateSubscriber<ModSettingChange, Guid, string, bool, Action?> modSettingsChanged;
    private readonly (Guid Id, string Name) interfaceCollection;

    [Obsolete("Changed with Penumbra API rework")]
    private readonly ICallGateSubscriber<string> oldInterfaceCollection;
    [Obsolete("Changed with Penumbra API rework")]
    private readonly ICallGateSubscriber<IList<(string modDirectory, string modName)>> oldModList;
    [Obsolete("Changed with Penumbra API rework")]
    private readonly ICallGateSubscriber<string, string, string, bool, (PenumbraApiEc status, (bool modEnabled, int priority, IDictionary<string, IList<string>> optionDetails, bool settingsInherited)? settings)> oldModSettings;
    [Obsolete("Changed with Penumbra API rework")]
    private readonly ICallGateSubscriber<ModSettingChange, string, string, bool, Action?> oldModSettingsChanged;

    public bool NativeUiBanned { get; private set; }

    private bool penumbraModsEnabled
    {
        get
        {
            try
            {
                return penumbraModsState.InvokeFunc();
            }
            catch
            {
                return false;
            }
        }
    }

    private (int Breaking, int FeatureLevel) penumbraApiVersion
    {
        get
        {
            try
            {
                return apiVersions.InvokeFunc();
            }
            catch
            {
                return (0, 0);
            }
        }
    }

#pragma warning disable CS8618
    public PenumbraIpc(DalamudPluginInterface _pluginInterface, IPluginLog _pluginLog)
    {
        log = _pluginLog;

        try
        {
            apiVersions = _pluginInterface.GetIpcSubscriber<(int, int)>("Penumbra.ApiVersions");
        }
        catch
        {
            apiVersions = _pluginInterface.GetIpcSubscriber<(int, int)>("Penumbra.ApiVersion");
        }

        if (penumbraApiVersion.Breaking is not 4 and not 5)
        {
            throw new NotSupportedException("Penumbra API out of date. Version " + penumbraApiVersion.Breaking.ToString(CultureInfo.InvariantCulture));
        }

        penumbraModsState = _pluginInterface.GetIpcSubscriber<bool>("Penumbra.GetEnabledState");
        penumbraInit = _pluginInterface.GetIpcSubscriber<object>("Penumbra.Initialized");
        penumbraDispose = _pluginInterface.GetIpcSubscriber<object>("Penumbra.Disposed");
        penumbraEnabledChange = _pluginInterface.GetIpcSubscriber<bool, Action<bool>?>("Penumbra.EnabledChange");
        if (penumbraApiVersion.Breaking is 4)
        {
            oldInterfaceCollection = _pluginInterface.GetIpcSubscriber<string>("Penumbra.GetInterfaceCollectionName");
            oldModList = _pluginInterface.GetIpcSubscriber<IList<(string modDirectory, string modName)>>("Penumbra.GetMods");
            oldModSettings = _pluginInterface.GetIpcSubscriber<string, string, string, bool, (PenumbraApiEc status, (bool modEnabled, int priority, IDictionary<string, IList<string>> optionDetails, bool settingsInherited)? settings)>("Penumbra.GetCurrentModSettings");
            oldModSettingsChanged = _pluginInterface.GetIpcSubscriber<ModSettingChange, string, string, bool, Action?>("Penumbra.ModSettingChanged");
            oldModSettingsChanged.Subscribe(OldCheckModChanges);
        }
        else if (penumbraApiVersion.Breaking is 5)
        {
            interfaceCollection = _pluginInterface.GetIpcSubscriber<ApiCollectionType, (Guid id, string Name)>("Penumbra.GetCollection").InvokeFunc(ApiCollectionType.Interface);
            modList = _pluginInterface.GetIpcSubscriber<Dictionary<string, string>>("Penumbra.GetModList");
            modSettings = _pluginInterface.GetIpcSubscriber<Guid, string, string, bool,
                (PenumbraApiEc status, (bool modEnabled, int priority, Dictionary<string, List<string>> optionDetails, bool ignoreInheritance)? settings)>
                ("Penumbra.GetCurrentModSettings");
            modSettingsChanged = _pluginInterface.GetIpcSubscriber<ModSettingChange, Guid, string, bool, Action?>("Penumbra.ModSettingChanged");
            modSettingsChanged.Subscribe(CheckModChanges);
        }

        penumbraEnabledChange.Subscribe(CheckState);
        penumbraInit.Subscribe(PenumbraInit);
        penumbraDispose.Subscribe(PenumbraDispose);

        if (penumbraModsEnabled)
        {
            NativeUiBanned = penumbraApiVersion.Breaking is 4 ? OldCheckMUIPresence(oldModList!.InvokeFunc(), oldInterfaceCollection!.InvokeFunc())
                : CheckMUIPresence(modList!.InvokeFunc(), interfaceCollection.Id);
        }
    }
#pragma warning restore CS8618

    private void CheckState(bool penumbraEnabled)
    {
        NativeUiBanned = penumbraEnabled
            && penumbraApiVersion.Breaking == 4 ? OldCheckMUIPresence(oldModList.InvokeFunc(), oldInterfaceCollection.InvokeFunc())
            : CheckMUIPresence(modList.InvokeFunc(), interfaceCollection.Id);
    }

    private void CheckModChanges(ModSettingChange type, Guid collectionId, string modDirectory, bool inherited)
    {
        if ((type is not ModSettingChange.EnableState && type is not ModSettingChange.Inheritance) || interfaceCollection.Id != collectionId)
        {
            return;
        }
        var currentMods = modList.InvokeFunc().Where(currentMod => currentMod.Key.Equals(modDirectory, StringComparison.Ordinal)).ToDictionary(StringComparer.Ordinal);
        NativeUiBanned = CheckMUIPresence(currentMods, collectionId);
    }

    private bool CheckMUIPresence(Dictionary<string, string> modList, Guid collection)
    {
        if (modList.Count < 1)
        {
            return false;
        }
        foreach (var mod in modList)
        {
            if (!mod.Value.Contains("Material UI", StringComparison.OrdinalIgnoreCase)
                && !mod.Key.Contains("Material UI", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var modDetails = modSettings.InvokeFunc(collection, mod.Key, mod.Value, arg4: false);
            if (modDetails.status is not PenumbraApiEc.Success || !modDetails.settings.HasValue)
            {
                log.Error("Failed to retrieve mod details. {status}", modDetails.status);
                continue;
            }
            return modDetails.settings.Value.modEnabled;
        }
        return false;
    }


    private void OldCheckModChanges(ModSettingChange type, string collectionName, string modDirectory, bool inherited)
    {
        if ((type is not ModSettingChange.EnableState && type is not ModSettingChange.Inheritance) || !oldInterfaceCollection.InvokeFunc().Equals(collectionName, StringComparison.Ordinal))
        {
            return;
        }
        var currentMods = oldModList.InvokeFunc().Where(currentMod => currentMod.modDirectory.Equals(modDirectory, StringComparison.Ordinal));
        NativeUiBanned = OldCheckMUIPresence(currentMods, collectionName);
    }

    private bool OldCheckMUIPresence(IEnumerable<(string modDirectory, string modName)> modList, string collection)
    {
        if (!modList.Any())
        {
            return false;
        }
        foreach (var mod in modList)
        {
            if (!mod.modName.Contains("Material UI", StringComparison.OrdinalIgnoreCase) && !mod.modDirectory.Contains("Material UI", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var modDetails = oldModSettings.InvokeFunc(collection, mod.modDirectory, mod.modName, arg4: false);
            if (modDetails.status is not PenumbraApiEc.Success || !modDetails.settings.HasValue)
            {
                log.Error("Failed to retrieve mod details. {stat}", modDetails.status);
                continue;
            }
            return modDetails.settings.Value.modEnabled;
        }
        return false;
    }


    private void PenumbraInit()
    {
        penumbraEnabledChange.Subscribe(CheckState);
        if (penumbraApiVersion.Breaking is 4)
        {
            oldModSettingsChanged.Subscribe(OldCheckModChanges);
        }
        else if (penumbraApiVersion.Breaking is 5)
        {
            modSettingsChanged.Subscribe(CheckModChanges);
        }
        if (penumbraModsEnabled)
        {
            NativeUiBanned = penumbraApiVersion.Breaking is 4 ? OldCheckMUIPresence(oldModList.InvokeFunc(), oldInterfaceCollection.InvokeFunc())
                : CheckMUIPresence(modList.InvokeFunc(), interfaceCollection.Id);
        }
    }

    private void PenumbraDispose()
    {
        penumbraEnabledChange.Unsubscribe(CheckState);
        if (penumbraApiVersion.Breaking is 4)
        {
            oldModSettingsChanged.Unsubscribe(OldCheckModChanges);
        }
        else if (penumbraApiVersion.Breaking is 5)
        {
            modSettingsChanged.Unsubscribe(CheckModChanges);
        }
        NativeUiBanned = false;
    }

    public void Dispose()
    {
        PenumbraDispose();
        penumbraInit.Unsubscribe(PenumbraInit);
        penumbraDispose.Unsubscribe(PenumbraDispose);
    }
}
