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

    private readonly ICallGateSubscriber<bool> penumbraModsState;
    private readonly ICallGateSubscriber<object> penumbraInit;
    private readonly ICallGateSubscriber<object> penumbraDispose;
    private readonly ICallGateSubscriber<string> interfaceCollection;
    private readonly ICallGateSubscriber<(int Breaking, int FeatureLevel)> apiVersions;
    private readonly ICallGateSubscriber<IList<(string modDirectory, string modName)>> mods;
    private readonly ICallGateSubscriber<string, string, string, bool, (PenumbraApiEc status, (bool modEnabled, int priority, IDictionary<string, IList<string>> optionDetails, bool settingsInherited)? settings)> modSettings;
    private readonly ICallGateSubscriber<ModSettingChange, string, string, bool, Action?> modSettingsChanged;
    private readonly ICallGateSubscriber<bool, Action<bool>?> penumbraEnabledChange;

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

    public PenumbraIpc(DalamudPluginInterface _pluginInterface, IPluginLog _pluginLog)
    {
        log = _pluginLog;

        apiVersions = _pluginInterface.GetIpcSubscriber<(int, int)>("Penumbra.ApiVersions");

        if (penumbraApiVersion.Breaking is not 4)
        {
            throw new NotSupportedException("Penumbra API out of date. Version " + penumbraApiVersion.Breaking.ToString(CultureInfo.InvariantCulture));
        }
        if (penumbraApiVersion.FeatureLevel is not 0 and not 23)
        {
            log.Debug("Penumbra API Feature Level changed {ver}", penumbraApiVersion.FeatureLevel);
        }

        penumbraModsState = _pluginInterface.GetIpcSubscriber<bool>("Penumbra.GetEnabledState");
        interfaceCollection = _pluginInterface.GetIpcSubscriber<string>("Penumbra.GetInterfaceCollectionName");
        mods = _pluginInterface.GetIpcSubscriber<IList<(string modDirectory, string modName)>>("Penumbra.GetMods");
        modSettings = _pluginInterface.GetIpcSubscriber<string, string, string, bool, (PenumbraApiEc status, (bool modEnabled, int priority, IDictionary<string, IList<string>> optionDetails, bool settingsInherited)? settings)>("Penumbra.GetCurrentModSettings");
        penumbraInit = _pluginInterface.GetIpcSubscriber<object>("Penumbra.Initialized");
        penumbraDispose = _pluginInterface.GetIpcSubscriber<object>("Penumbra.Disposed");
        penumbraEnabledChange = _pluginInterface.GetIpcSubscriber<bool, Action<bool>?>("Penumbra.EnabledChange");
        modSettingsChanged = _pluginInterface.GetIpcSubscriber<ModSettingChange, string, string, bool, Action?>("Penumbra.ModSettingChanged");

        penumbraEnabledChange.Subscribe(CheckState);
        modSettingsChanged.Subscribe(CheckModChanges);
        penumbraInit.Subscribe(PenumbraInit);
        penumbraDispose.Subscribe(PenumbraDispose);

        if (penumbraModsEnabled)
        {
            NativeUiBanned = CheckMUIPresence(mods.InvokeFunc(), interfaceCollection.InvokeFunc());
        }
    }

    private void CheckState(bool penumbraEnabled)
    {
        NativeUiBanned = penumbraEnabled && CheckMUIPresence(mods.InvokeFunc(), interfaceCollection.InvokeFunc());
    }

    private void CheckModChanges(ModSettingChange type, string collectionName, string modDirectory, bool inherited)
    {
        if ((type is not ModSettingChange.EnableState && type is not ModSettingChange.Inheritance) || !interfaceCollection.InvokeFunc().Equals(collectionName, StringComparison.Ordinal))
        {
            return;
        }
        var modList = mods.InvokeFunc().Where(currentMod => currentMod.modDirectory.Equals(modDirectory, StringComparison.Ordinal));
        NativeUiBanned = CheckMUIPresence(modList, collectionName);
    }

    private bool CheckMUIPresence(IEnumerable<(string modDirectory, string modName)> modList, string collection)
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
            // Inheritance bool arg4 is flipped
            var modDetails = modSettings.InvokeFunc(collection, mod.modDirectory, mod.modName, arg4: false);
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
        modSettingsChanged.Subscribe(CheckModChanges);
        if (penumbraModsEnabled)
        {
            NativeUiBanned = CheckMUIPresence(mods.InvokeFunc(), interfaceCollection.InvokeFunc());
        }
    }

    private void PenumbraDispose()
    {
        penumbraEnabledChange.Unsubscribe(CheckState);
        modSettingsChanged.Unsubscribe(CheckModChanges);
        NativeUiBanned = false;
    }

    public void Dispose()
    {
        PenumbraDispose();
        penumbraInit.Unsubscribe(PenumbraInit);
        penumbraDispose.Unsubscribe(PenumbraDispose);
    }
}
