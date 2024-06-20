using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;

using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;
using Dalamud.Plugin.Services;
using Dalamud.Plugin;

namespace TickTracker.IPC;

public sealed class PenumbraApi : IDisposable
{
    private readonly IPluginLog log;
    private readonly DalamudPluginInterface pluginInterface;

    private readonly ApiVersion apiVersion;
    private readonly GetEnabledState getEnabledState;
    private readonly GetModList getModList;
    private readonly GetCurrentModSettings getCurrentModSettings;    
    private readonly (Guid Id, string Name)? interfaceCollection;

    private readonly EventSubscriber init, disposed;
    private EventSubscriber<bool> enabledChanged;
    private EventSubscriber<ModSettingChange, Guid, string, bool> modSettingsChanged;

    public bool NativeUiBanned { get; private set; }

    private bool penumbraModsEnabled
    {
        get
        {
            try
            {
                return getEnabledState.Invoke();
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
                return apiVersion.Invoke();
            }
            catch
            {
                return (0, 0);
            }
        }
    }

    public PenumbraApi(DalamudPluginInterface _pluginInterface, IPluginLog _pluginLog)
    {
        log = _pluginLog;
        pluginInterface = _pluginInterface;
        apiVersion = new ApiVersion(pluginInterface);

        if (penumbraApiVersion.Breaking is not 5)
        {
            throw new NotSupportedException("Penumbra API out of date. Version " + penumbraApiVersion.Breaking.ToString(CultureInfo.InvariantCulture));
        }

        getEnabledState = new GetEnabledState(pluginInterface);
        getModList = new GetModList(pluginInterface);
        getCurrentModSettings = new GetCurrentModSettings(pluginInterface);
        var getCollection = new GetCollection(pluginInterface);
        interfaceCollection = getCollection.Invoke(ApiCollectionType.Interface) ?? getCollection.Invoke(ApiCollectionType.Default);

        init = Initialized.Subscriber(pluginInterface, PenumbraInit);
        disposed = Disposed.Subscriber(pluginInterface, PenumbraDispose);
        enabledChanged = EnabledChange.Subscriber(pluginInterface, CheckState);
        modSettingsChanged = ModSettingChanged.Subscriber(pluginInterface, CheckModChanges);
        init.Enable();
        disposed.Enable();
        enabledChanged.Enable();
        modSettingsChanged.Enable();
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
            var modDetails = getCurrentModSettings.Invoke(collection, mod.Key, mod.Value, ignoreInheritance: false);
            if (modDetails.Item1 is not PenumbraApiEc.Success || !modDetails.Item2.HasValue)
            {
                log.Error("Failed to retrieve mod details. {status}", modDetails.Item1);
                continue;
            }
            return modDetails.Item2.Value.Item1;
        }
        return false;
    }

    private void CheckState(bool penumbraEnabled)
    {
        NativeUiBanned = penumbraEnabled && CheckMUIPresence(getModList.Invoke(), interfaceCollection!.Value.Id);
    }

    private void CheckModChanges(ModSettingChange type, Guid collectionId, string modDirectory, bool inherited)
    {
        if ((type is not ModSettingChange.EnableState && type is not ModSettingChange.Inheritance) || interfaceCollection!.Value.Id != collectionId)
        {
            return;
        }
        var currentMods = getModList.Invoke().Where(currentMod => currentMod.Key.Equals(modDirectory, StringComparison.Ordinal)).ToDictionary(StringComparer.Ordinal);
        NativeUiBanned = CheckMUIPresence(currentMods, collectionId);
    }

    private void PenumbraInit()
    {
        enabledChanged = EnabledChange.Subscriber(pluginInterface, CheckState);
        modSettingsChanged = ModSettingChanged.Subscriber(pluginInterface, CheckModChanges);
        enabledChanged.Enable();
        modSettingsChanged.Enable();
        if (penumbraModsEnabled)
        {
            NativeUiBanned = CheckMUIPresence(getModList.Invoke(), interfaceCollection!.Value.Id);
        }
    }

    private void PenumbraDispose()
    {
        enabledChanged.Dispose();
        modSettingsChanged.Dispose();
        NativeUiBanned = false;
    }

    public void Dispose()
    {
        PenumbraDispose();
        init.Dispose();
        disposed.Dispose();
    }
}
