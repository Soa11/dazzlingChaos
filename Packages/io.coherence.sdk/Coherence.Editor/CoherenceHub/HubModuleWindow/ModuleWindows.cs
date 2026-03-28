// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    internal class LinksModuleWindow : ModuleWindow<LinksModuleWindow, LinksModule> { }
    internal class SceneModuleWindow : ModuleWindow<SceneModuleWindow, QuickStartModule> { }
    internal class SimulatorsModuleWindow : ModuleWindow<SimulatorsModuleWindow, SimulatorsModule> { }
    internal class GameObjectModuleWindow : ModuleWindow<GameObjectModuleWindow, GameObjectModule> { }
    internal class LocalServerModuleWindow : ModuleWindow<LocalServerModuleWindow, ReplicationServerModule> { }
    internal class SchemaAndBakeModuleWindow : ModuleWindow<SchemaAndBakeModuleWindow, NetworkedPrefabsModule> { }
    internal class OnlineModuleWindow : ModuleWindow<OnlineModuleWindow, CloudModule> { }
    internal class CoherenceSyncObjectsWindow : ModuleWindow<CoherenceSyncObjectsWindow, CoherenceSyncObjectsModule> { }
}
