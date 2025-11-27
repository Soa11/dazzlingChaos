# Changelog

## [2.0.0]

### Added
- PingClient - new functionality for measuring latency to coherence servers. Supports both UDP and TCP protocols with configurable timeout and cancellation token support.
- CoherenceNode and CoherenceSync editors now detect if CoherenceSync contains invalid configuration for the CoherenceNode, and display warning messages as well buttons to fix the issue.
- Support for ClientID as a command and component primitive.
- coherence logging to file now supports game builds.
- Improved error messages for when a simulator slug has been set but simulator support has not been enabled for rooms in the Online Dashboard.
- Support for multiple tags in coherence sync tags and tag queries using | separator.
- CoherenceSync inspector now disables Request Authority buttons when Authority Transfer is set to "Not transferable", providing clear visual feedback to users.
- Error icons on Scene Hierarchy window for CoherenceBridge, CoherenceQuery and CoherenceCloudLogin (shown when errors found while in Play Mode). 
- Optimization window now visualizes prefab values overrides of optimizations done on prefab variants.
- Added ability to apply prefab value overrides to base prefab in the Optimization window.
- Added ability to revert prefab value override changes done on top of a base prefab in the Optimization window.
- You will now see warnings if you have synced more than the maximum number of variables per component (32) in the CoherenceSync Editor, the Configuration window and during baking.
- Support for syncing bindings and commands containing big data over fragmented channels.
- Extended Project Settings with ability to customize whether messages logged to the Console window by coherence should include a watermark, timestamp or source type.
- An error box is shown in CoherenceSync Editor if the GameObject is not a prefab nor a prefab instance.
- Worlds UI sample will now automatically try to reconnect to the last connected world if the backend shuts it down, in case the backend restarts the world.
- Added ICoherenceSync.SetActive(bool). This works the same as GameObject.SetActive(bool), except that it handles unparenting child networked objects before setting the GameObject inactive if 'Preserve Children' is enabled, which can help avoid encountering the error 'Cannot set the parent of the GameObject 'X' while activating or deactivating the parent GameObject 'Y'.
- Added support for sending commands to an interface as well as the concrete class.
- Added optional IRelay.Flush() callback method for custom relay implementations.
- Added CloudService.Regions access point for region-related functionality.
- Refresh button in Networked Prefabs window to update the view.

### Changed
- Updated Simulator Slug configuration GUI in the Hub to better clarify that it applies to the project as a whole and not just  simulator builds. Also added a link to documentation.
- Removed the size limit of commands and inputs.
- Removed the size limit of strings and byte arrays in both commands and bindings.
- Made it possible to create lobbies that are not associated with any rooms. This makes it possible to create lobbies even if you have a Simulator Slug configured in coherence Hub, but have not enabled simulators for rooms in the Online Dashboard.
- Migrated CloudStorage access point to CloudStorage from CloudService.GameServices.
- Marked GameServices.MatchmakerService as obsolete. CloudService.Lobbies should be used instead.
- CoherenceSync.CoherenceBridge will now continue returning a non-null value even after the networked entity has been destroyed. This means that other components can safely access the bridge via this property during their OnDisable and OnDestroy events and more easily do things like unsubscribe event handlers from the CoherenceBridge's events.
- Made LobbiesService.GetLobbySessions obsolete, because it could not reliably return data for the lobbies that the player
  belongs if they log out out then log back in while inside a lobby. Added LobbiesService.GetLobbySessionsAsync and
  LobbiesService.GetLobbySessionIds which work reliably even in this situation.
- INetworkObjectInstantiator.Destroy is no longer unnecessarily executed for networked objects during a disconnection event if the object's scene is already being unloaded.
- Renamed CreateLobbyOptions.Secret to CreateLobbyOptions.Password.
- 'Remove All Invalid Bindings' buttons in CoherenceSync's Editor and the Networked Prefabs window now clearly list which objects contain which invalid bindings when prompting for confirmation.
- Messages logged to the Console window by coherence are now cleaner by default, no longer including timestamps or message source types. This can be customized in Project Settings.
- Improved some coherence Hub window error messages in cases where upload operations fail.
- Removed requirement of an unlock token for launching the replication server in the runtime settings since the replication server restrictions are now lifted for everyone.
- Marked CloudRooms region-specific functionality obsolete; RegionsService should now be used for those instead.
- Configure window will only inspect a CoherenceSync when it's selected, following more closely how the Inspector works.
- Configure window button in CoherenceSync inspector usable in Play Mode.
- An OnApplicationQuitSender GameObject is no longer created in the hierarchy during initialization.
- Improve compilation speed by lowering the amount of files baking generates.
- Renamed 'Import Connect Dialog...' into 'Open Sample UIs in Hub'.
- Clone Mode message box changed into a toolbar.
- New icon for Clone Mode to indicate 'Allow Editing' is enabled.

### Fixed
- Fixed "Write to File" logging option not setting the scripting symbols.
- Fixed inconsistent terminology used when referring to the Online Dashboard.
- All async methods in LobbiesService now automatically defer sending of requests as needed to avoid Too Many Requests errors.
- Fixed Hub sections maintaining scroll position when switching between modules.
- Fixed obsolete member warnings in Unity versions 6.2 and 6.3.
- Dialog asking to restore old setting is now shown properly if local simulator build is cancelled
  midway through the process.
- Fixed exception if AuthClient was disposed while a Login operation was in progress and the Login operation was not cancelled.
- Fixed possible NullReferenceException in CoherenceClientConnectionManager when exiting Play Mode.
- CoherenceSync editor now shows warning about invalid bindings also when inspecting prefab instances and prefab assets that are not being edited in prefab mode.
- CoherenceSync editor now also warns about invalid bindings targeting components in child GameObjects.
- Fixed issue in Matchmaking sample where leaving and re-entering lobbies would result in errors about LobbySession not being disposed properly.
- Improved reliability of applying optimizations done to a base prefab to all its variants.
- Fixed issue in Matchmaking sample where a NullRefereceException would occur for non-lobby owners when joining a lobby.
- Coherence Node Editor buttons for automatically fixing Coherence Sync configuration issues now are dynamically disabled for scene objects in Play Mode, as well as handle different kinds of prefab targets properly.
- Fixed crash when changing the selected project in the coherence Hub window in Unity 6.3 Alpha.
- Fixed possible NullReferenceException in World UI sample.
- Don't allow negative sizes for pooled networked instantiations.
- Dynamically added prefabs to a CoherenceSync with a complex nested prefab seup using PrefabSyncGroup will now delete on non-auth clients correctly where before they would not.
- 'Select All' in the Configuration window will no longer ever select more than the maximum supported number of variables per component (32).
- Fixed issue where 'Delesect All' could deselect bindings from different components of the same type.
- A descriptive warning is now logged if a CoherenceSync that is not an instance of a prefab is loaded at runtime, instead of a generic NullReferenceException getting thrown.
- Added [Serializable] attribute to all types used with [SerializeReference] to get rid of warnings from Unity.
- Fixed MissingReferenceException in CoherenceSyncConfigPostprocessor when both a networked prefab and its config were deleted simultaneously.
- Fixed compile error in Unity 6+ when baking if a LOD's Distance was set to a very high value in the Optimization window.
- 'Find Match' button Matchmaking UI sample is now disabled when no regions are selected.
- Fixed issue where selected prefab asset could get deselected when making changes to its state because of usage of AssetDatabase.StartAssetEditing in an AssetPostprocessor, even if no changes actually were made to any prefabs.
- Made it possible to instantiate and destroy networked objects inside [OnValueChanged] event handlers without it resulting in exceptions.
- Fixed Optimize window throwing a NullReferenceException if you open a networked prefab in the prefab stage, delete the prefab asset, and select the GameObject in the prefab stage.
- Updated CoherenceSync header tooltip to refer to Networked Prefabs window using its correct name.
- Interpolation artifacts when InterpolationSettings.TeleportDistance is triggered.
- Fixed issue with the error "Cannot set the parent of the GameObject 'X' while activating or deactivating the parent GameObject 'Y'." sometimes appearing in the Console when a networked object with nested networked objects and 'Preserve Children' enabled was set inactive.
- WebTransport dropping last packet containing the disconnect request, reporting the disconnect reason always as channel closed.
- Fix ArgumentException when making changes to a prefab asset when the open scene contains an instance of said asset and the prefab instance contains a CoherenceSync component but the prefab asset doesn't.
- Unticking 'Sync with coherence' in the GameObject header no longer results in a GUI error.
- Ticking 'Sync with coherence' no longer applies any prefab instance overrides to prefab asset except for the CoherenceSync component, as intended.
- Many races around Shifting Origin which would lead to entities having wrong position.
- Fixed scale interpolation issue when parenting changes
- Improved accuracy of Schema In Cloud sync state indicator.
- Fixed possible NullReferenceException from CoherenceSceneLoader when exiting Play Mode or quitting the application.
- Fixed CoherenceSceneLoader causing 'ArgumentException: SceneManager.SetActiveScene failed; invalid scene' if the scene that it loaded was unloaded immediately after being loaded.
- Fixed issue where could encounter "Some objects were not cleaned up when closing the scene. (Did you spawn new GameObjects from OnDestroy?)" warning when exiting Play Mode.
- Fixed issue where OnApplicationQuit could get executed multiple times for INetworkObjectInstantiator and INetworkObjectProvider when quitting the application.
- Fixed issue where a NullReferenceException could occur when a login attempt failed in certain circumstances.
- 'Fix Invalid Bindings' to be used only on Prefab Assets.
- Bake icon (Project window) marks when to bake in many more scenarios.
- Stability of Configure and Optimize windows when kept open.
- Able to inspect Gathered.schema when using enums.
- Support enums with negative values.
- Deleting a folder with networked prefabs was not deleting their linked configs properly.
- Fixed issue where local schema would not get uploaded to the cloud after baking, even if Upload Schemas to Cloud On Baking Complete was enabled in Project Settings, if the baking process didn't result in any changes to scripts.
- Fixed transform interpolation artifacts when entities are unparented or reparented under a non-CoherenceSync GameObjects (also known as folders for scene organization).
- Fixed 'Sync Prefab Stage' button not always working.
- Disallow certain operations, like uploading schemas, when working with clones (ParrelSync, MPPM, etc).
- Performance regression of Networked Prefabs window when having lots of prefabs
- Disallow certain operations, like uploading schemas, when working with clones (ParrelSync, MPPM, etc).
- 'Sync with coherence' header works with added components to instances.
- CoherenceSync inspector prompts to apply prefab override instead of creating a new prefab.
- Fixed a race when an entity is instantiated, parented and transferred authority in the same frame, causing the parenting to not be replicated.
- Fixed 'Fix Authority Type' on Coherence Bridge Editor not updating all in-memory versions of the targeted prefab immediately.
- Fixed issue where 'Select All' button in Configuration window could result in maximum number of bound variables per component getting exceeded if some members had the [Sync] attribute but were not yet added into the bindings list of the component.

## [1.8.0]

### Added
- Hub tab: Samples
- New sample: Dissonance voice integration
- Added CoherenceCloudLogin component that can be used to log in using all the different methods offered by the CoherenceCloud API.
- Added new 'Project Selector' option to Project Settings, that can be used to enable separate project selectors to be shown in coherence Hub for release builds and development builds and the editor.
- CoherenceSync Editor help box text improvements.
- Optimization window context menu item 'Apply To All Variants' now also works with prefabs that are open in Prefab Mode.
- LiveQuery hysteresis of entity create / destroy by introducing a buffer to the extent.
- Option to increase default max entity count from 65k to up to 2^32.
- Ability to send and access the command metadata - sender's ClientID and simulation frame.
- Network Asset ID can now be viewed in the Networked Prefabs window.
- Added sending commands directly to the input authority.
- Simulator frame limiting settings exposed on RuntimeSettings.
- Added cancellation token support for all async methods in LobbiesService.

### Changed
- Main Menu simplified.
- Scene Hierarchy icons for Authority updated to show all states (state/input and authority/remote).
- Marked RuntimeSettings.OrganizationID and RuntimeSettings.OrganizationName obsolete and added ProjectSettings.OrganizationId and ProjectSettings.OrganizationName that can be used instead in the Editor.
- CoherenceSync Editor help box text improvements.
- Optimization window context menu item 'Apply To All Variants' now also works with prefabs that are open in Prefab Mode.
- CoherenceBridge.LoginAsGuest now logs in as a different guest on each separate instance of the application that is running simultaneously on the same device, to help with local testing of multiplayer functionality.
- Automatically created GlobalQuery from the CoherenceBridge now instantiates a new "Coherence GlobalQuery" gameobject.
- Project window status icon now reflects schema states immediately, instead of sometimes only updating when the window is mouseovered.
- Deprecated MessageTarget.AuthorityOnly and replaced it with MessageTarget.StateAuthority only to support sending commands to the input authority directly.
- Added handling for any exceptions thrown by PlayerAccount event handlers.

### Fixed
- Improved stability of world position when using CoherenceSync.SetParent.
- Fixed issue with PlayerAccount.Username being empty after logging in with a Session Token, even if a username was associated with the player account.
- Fixed PlayerAccount.Username not reflecting the username of the player account after logging in using certain login methods.
- Fixed issue where the state of Client Connections was incomplete during OnLiveQuerySynced callback.
- Fixed Optimization window sometimes causing GUI layout exceptions when Auto Save was turned off.
- Fixed socket send and receive buffers being too small in some situations.
- Byte array bindings will now sync properly if the array is changed by index.
- Fixed ArgumentOutOfRangeException sometimes being thrown in when clicking a LOD element in the Optimization window.
- Fixed NullReferenceException sometimes occurring in Optimization window during the OnEnable event.
- Fixed issue with Optimization window target sometimes changing from the selected prefab asset to prefab that is open in the Prefab Stage.
- Fixed ExitGUIException sometimes occurring in the Optimization window on selection change.
- Fixed NullReferenceExceptions on the Networked Prefabs window when there's no prefabs.
- Improved reliability of login time out failure handling and logic for automatically trying to login again in case of certain kinds of failures.
- Changes made to selected organization and project in coherence Hub window are now more reliably saved immediately.
- Fixed issue where an error about an invalid filter could be shown by the Lobbies UI sample.
- Fixed issue where the Configuration window could cause an ExitGUIException error to appear in the Console window.
- Fixed CoherenceCloudLogin.CloudUniqueId throwing a StackOverflowException if COHERENCE_SIMULATOR scripting symbol was defined but no shared authentication token was provided.
- Fixed issue where maximizing the Configuration window could cause an 'Invalid editor window' error to appear in the Console.
- Networked Prefabs window updates when making changes to CoherenceSync via Inspector.
- Local simulator builds can be cancelled.
- Temporary changes made to settings are now reverted more reliably after building and uploading a simulator even if the operation fails.

### Removed
- Explore Samples window. Use the Hub instead.

## [1.7.0]

### Added
- Support for Linux ARM64 Replication Server.
- Support for validating connections by using HostAuthority.ValidateConnection feature.
- Support for simulators to kick connections.
- Setting to change the default AuthorityTransferType.
- Added checks to the local simulator build action to ensure that the server module for the target platform is available.
- AutoSimulatorConnection: can set a specific CoherenceBridge.
- CoherenceSync: OnNetworkedDestruction, OnNetworkedInstantiation, OnBeforeNetworkedInstantiation and OnAuthorityRequest APIs.
- CoherenceSync: expose OnNetworkedParentChanged, OnAuthorityRequestRejected, OnAuthTransferComplete and OnAuthorityRequest on the inspector.
- Added HelpUrlAttributes to several components to link to relevant online documentation.

### Changed
- Explore Samples window remains open after importing a sample.
- Removed the obsolete disk persistence from the SDK.
- Changed 'coherence Cloud' to 'Player Accounts' in CoherenceBridge's editor for clarity. Also updated documentation link to point to documentation for Player Accounts.
- Deprecated OnConnectedEntityChanged in favor of OnNetworkedParentChanged.
- Deprecated OnAuthorityRequested in favor of OnAuthorityRequest.

### Fixed
- Invalid Linux Replication Server executable
- Fixed issue with Optimization window not being marked as having unsaved changes when changes were made using certain controls.
- Fixed a compatibility with older versions of Unity in the simulator build pipeline.
- Fixed an issue where a local simulator build that had been renamed could not be chosen from the file picker.
- Compilation error when UGUI wasn't present on the project.
- Disabled CoherenceInput doesn't throw errors.
- CoherenceSync's editor no longer displays misleading text "CoherenceSync is automatically disabled when there's no CoherenceBridge found..." when the GameObject is inactive due to it being simulated on the server side.
- Fixed NullReferenceException when clicing 'Add AutoSimulatorConnection' in coherence Hub or selecting 'coherence > Simulator > Add AutoSimulatorConnection' top menu item.
- 'Manage Cloud Plan' button in coherence Hub now opens the correct Online Dashboard page.
- Explore Samples: Add to Scene instantiates the prefab to the scene.
- Sending an entity update in the same change as the transfer of the entity's input authority no longer throws an error and applies the change.
- Fixed 'Schema in Cloud' status indicator in coherence Hub sometimes getting stuck showing 'In Progress' after entering Play Mode with 'Bake On Enter Play Mode' enabled in Project Settings.
- Fixed PlayerAccount.OnMainChanged event not being raised if logging in via CoherenceBridge instead of CoherenceCloud.

## [1.6.0]

### Added
- Support for launching RS with host authority options for restricting entity creation to host or simulator.
- Support for creating RS rooms with host authority options for restricting entity creation to host or simulator.
- Support for connecting to an RS as a host in worlds or rooms.
- BakeConditional attribute for controlling type networking via scripting define symbols. 
- CoherenceGlobalQuery component to create queries on simulators for other clients.
- Toggle to set a CoherenceSync object as global so it can be visible to global queries.
- Max Overshoot Allowed setting in interpolation settings.
- New simplified coherence Cloud user management API accessible via the User class.
- Added the ability to log in to coherence Cloud using a Steam, Epic Games, PlayStation, Xbox, Nintendo account, or a JWT.
- Added ability to link multiple authentication methods to the same coherence Cloud account.
- Added support for generating and logging in to coherence Cloud using a one-time code. This can be used for recovering lost accounts, and for logging in to the same account with a new device.
- Added ability to change coherence Cloud username, password, email, display name and display image.
- Added ability to get all coherence Cloud account information.
- Support for enums in commands and component bindings.

### Changed
- Multi-Room Simulators are deprecated, and will be completely removed in the future.
- Renamed 'CoherenceSync Objects' to 'Networked Prefabs'.
- Hub: Renamed 'Overview' to 'Learn' (renders 'Welcome').
- Hub: Renamed 'coherence Cloud' to 'Cloud'.
- Hub: Renamed 'Scene' to 'Quick Start'.
- Hub: Removed 'Links', 'Baking' and 'GameObject'.
- Hub: most tabs have been revisited, with simplicity and usability in mind.
- Hub: tabs can offer 'Online Resources' section.
- Installs and updates open Hub on the 'Learn' tab instead of 'Welcome' window.
- MenuItem: 'Welcome' removed.
- MenuItem: 'coherence Hub' renamed to 'Hub'.
- Removed the dialog box that pops up the first time you assign a non-transferable prefab to the coherence bridge's client or simulator connection prefab slot. A warning is now shown underneath with a 'Fix' button.
- Prefab Variants no longer automatically pruned for missing or leaked references (use Prune button on Configure window if needed).
- Re-ordered simulator build warnings and errors logically.
- Removed information panel on local simulator builds because the `COHERENCE_SIMULATOR` symbol is automatically included when a local simulator is built.
- Removed Reflection Mode. Baking is now a requirement.
- Reworked Settings window.
- Reworked the Play Mode panel (that tracks authority and persistence) on the CoherenceSync inspector.
- Room, Lobby, World and Matchmaking Connection Dialog samples now work regardless of whether or not Coherence Bridge has been configured to login to coherence Cloud automatically.
- CoherenceBridge disconnects on OnDestroy.
- Invalid bindings produced by the [Sync] and [Command] attributes are now automatically removed from affected prefabs.
- Updated engine to v6.6.1

### Fixed
- Nested prefabs parented to a prefab variant logged an error when fixing the instance after a sync group was added.
- Fixed an issue with the Interpolate On dropdown where "Nothing" was selectable.
- An error message was not displayed when an uninstalled simulator build target option was selected.
- Addressable prefabs automatically update their "Load Via" configuration when added/removed from the addressables registry.
- Fixed wrong error message shown when a PrefabSyncGroup component was applied to a non-root object.
- Prefab was not getting updated when "Fix Authority Type" button clicked in the Coherence Bridge's Client Connections section.
- Path to local sim build contained an extension if full-stops were used in the name on a non-Windows platform.
- Fixed an issue where the org dropdown list was really small if only one four-lettered organization was available.
- Made ToolkitArchetype duplicate Component detection rely on reference comparisons, so that it can't be broken by unusual IEquatable<Component> implementations.
- Space to display helper text for sharing a build's path was too small. Added an additional warning space to allow more room for the text.
- Removed usages of Task.Delay and fixed usages of ContinueWith for WebGL support.
- Improved readability of some warnings and errors that can occur when baking.
- Snapping after interpolation overshooting.
- Time desync after a frame freeze.
- Optimized the networked prefab windows to make it more performant for large amounts of prefabs in the game.
- Removed layout warnings when opening the editor with the hub window visible.
- Issues when multiple Newtonsoft.Json installations are found in the project.
- Removed warning that was logged if AuthClient was disposed and there were no Prefs to be saved.
- Fixed an issue where a re-login to the cloud in the same session wouldn't work on MacOS.
- Fixed issue where NetworkPool could throw a NullReferenceException when exiting Play Mode in the Editor.
- Fixed an issue where warnings appeared after logging in to coherence cloud on a Mac.
- Fixing the authority type using the "Fix Authority Type" button did not invalidate the schema even though a property value on a prefab had changed.
- Sample Scenes scene 3 no longer duplicates balls after reconnecting.

## [1.5.1]

### Added
- Added a warning if Interpolate On is set to Nothing.
- Org and project names on the simulator build part of the coherence hub to indicate where the build will be sent.
- Added `SpawnInfo` to the API documentation.
- Limit to the maximum number of queries a client can create.
- In the CoherenceSync Objects window another column has been added to show the CoherenceSyncConfig object's name.

### Changed
- Package no longer brings Moq as a dependency.
- Package dependencies updated. This avoids compilation errors when disabling required built-in modules in your project that are needed for the package to work.
- Disabled console warning messages when offline or a bridge is disconnected.
- Organisation and project drop down lists grow and shrink based on the number of items in each list.
- Removed previously obsoleted members from AuthClient.
- Renaming a prefab will attempt to rename the corresponding coherence sync config file to match the new name.

### Fixed
- Parenting races in bad network conditions.
- Rare crash in query when changing floating origin and destroying bridges.
- Fix for fetching rooms locally from an RS failing even after starting an RS.
- Replication Server hanging in client-hosting mode when using Steam Relay.
- ReplicationServerRoomsService.IsOnline works on WebGL.
- Rare NullRefException during unique duplicate resolving.
- Fixed spelling mistake in client permissions error.
- Fixed an issue that prevented connections if the prefs file got corrupted.
- Fixed ordered commands dropped randomly when they were sent right after an entity is created.
- Fixed tooltip being wrong when initially hovering over the share build upload button.
- Fixed cloud sim transport type to always use UDP.
- coherence debug and trace logs are now correctly enabled in builds.
- Position jittering on rigidbody objects when reparenting in the FixedUpdate.
- Fixed project settings not reset after a headless Linux client build.
- Fixed error being displayed in the console window when changing logger settings.
- A race which causes children in a PrefabSyncGroup to have a wrong authority.
- Fixed CoherenceBridge not properly disposing some services during OnDestroy.
- Logging repeatedly no longer automatically authenticates with your last known credentials.
- An error is now logged to the Console if bundling replication server in the Streaming Assets folder should fail for any reason.

## [1.5.0]

### Added
- Simulator slug values are stored per-project.
- The Coherence Bridge editor will now display a warning if a connection prefabs with an unsupported authority type is assigned.
- Added new Cloud Storage API that can be used to persist object states into coherence Cloud.
- Added an Auto Save toggle to the Optimization window. It can be useful to disable Auto Save if experiencing long import times when making modifications. This can happen, for example, if the prefab being modified has many prefab variants.

### Changed
- Sending a command to AuthorityOnly on orphaned entity now issues a warning because that command will not be received by anyone.
- Cloned projects using ParrelSync can change log settings.
- CoherenceSync editor areas disabled for in-scene prefab instances as per the warning message.
- Wording and button action changed when a new prefab is opened in the stage view for the first time. If out of sync, the user can press a button to bring it into sync.
- Removed simulator region drop down and tidied up local simulator connection UI.

### Fixed
- Fixed issue where the last letter of "Organization" could wrap to the next line in coherence Hub > coherence Cloud > Account.
- coherence Hub tab text sometimes corrupts when opening the editor
- Fixed modifier key text for MacOS in the Coherence Sync Objects window.
- Nullref when adding coherence sync to a prefab and deselecting the hub
- Local simulator builds require at least one valid scene to build.
- Removed duplicates editor logs from diagnostic bug report.
- Build and upload headless Linux client button had the wrong style.
- Share build dropdown preselects the current platform.
- Account settings remembers the last organization and project selected when user logs back in.
- Added message to the console if the project is run after an upgrade without baking first.
- Simulator slugs are required for headless Linux builds. Disabled the build button in the coherence hub if no value is given for the simulator slug.
- An organization and project are required to upload a headless client to the cloud. Create an organization and project in the coherence portal's [dashboard](https://coherence.io/dashboard).
- Made the Network Entry section in CoherenceSync read-only when editing a prefab in Context Mode.
- CoherenceSync editor now warns about the component being attached to game objects that are not the root of a prefab asset more reliably.

## [1.4.3]

### Fixed
- Fixed issue where the Replication Server wouldn't be bundled correctly in the builds.

## [1.4.0]

### Added
- Ordered commands.
- Added safe-guards to make sure bindings are not affected by automated processed when CloneMode.Enabled is set to True.
- Added a new context menu item into the Optimization window, that can be used to applying optimizations for a binding from a base prefab into all of its variants.
- Support for launching self-hosted Replication Server in IL2CPP builds.
- CoherenceBridge inspector shows the transport used.
- CoherenceSync and Baking now support any valid C# identifier name. This includes components, variables, commands and command parameters.
- Prevented Unity linker from stripping code from coherence's assemblies and causing functionality to sometimes break when using high stripping levels.

### Changed
- Local simulator build remembers the last path and no longer prompts you when you re-build for the current session.
- Changed the Matchmaking Sample to only write updates to the console when the number of players in the lobby changes
- Simplified log-in API.
- If Project Settings > coherence > Logs > Console Level (Editor) is set to Debug, the `COHERENCE_LOG_DEBUG` symbol will be added to simulator builds before compilation.
- The configuration button in the coherence Sync editor window is now enabled in play mode.
- The rooms sample dialog displays a warning if the 'Auto Login As Guest' flag is unchecked.
- When replacement strategy is replace then the entity is now disabled in the scene instead of left running.
- Run in background warning will only show if the bridge is connected
- Building a local simulator will auto-populate the path in the "Run Local Simulator Build" section of the hub.
- Added method for updating arguments to the SimulatorUtility to enable using the simulator utility without rebuilding local simulators.
- Improved error message if an attempt is made to transfer an orphaned entity.
- Removed an error when destroying an entity the client core didn't know about anyway.
- Added description property to ITransport and an accessor in IClient.  These are API breaking changes.
- Backup world data has been placed behind a feature flag and disabled by default.
- Improved CoherenceLiveQuery inspector
- WebGL now uses `makeDynCall()` and `UTF8ToString()`. See [Replace deprecated browser interaction code](https://docs.unity3d.com/6000.0/Documentation/Manual/web-interacting-browser-deprecated.html#dyncall)

### Fixed
- Fixed issue with errors appearing when selecting a simulator build in coherence hub > Simulators.
- Fixed InvalidCastException that could happen in some rare situations when pressing the Sign Up / Login In button in coherence Hub.
- Log an error when a network entity create arrives with an unknown asset ID.
- Fixed warnings about inconsistent line endings when bakings scripts on a computer running Windows.
- Selected organization and project are no longer cleared when logging in and out.
- Fixed the validation to allow "Only predict with input authority" to be selected if a CoherenceInput was attached to a game object.
- Fixed resource leaks when a CoherenceBridge is destroyed.
- Fixed AuthToken.LoginAsGuest and LoginWithPassword failing the first time they are called if there's no session token.
- Parenting a run-time instantiated entity to an existing nested prefab race where the entity might not appear.
- Culture settings could cause issues launching replication server from the menu/hub. Command line options are now culture independent
- Fixed an issue adding CoherenceInput to an unsaved prefab that might cause a nullref error to be displayed in the console
- Fixed matchmaking sample showing warnings for obsolete Unity classes
- Re-compiling a script could display an organization not set warning in the project window if the coherence Hub was not open
- Removing invalid bindings stability.
- Button style for "Run Local Simulator" in Hub's Simulator tab.
- Changing interpolation from/to none hints a rebake is required.
- Warn about rebake needed when creating CoherenceSync duplicates or variants.
- HandleDisconnect exception handler now logs the correct AuthorityType.
- Unique nested prefabs would sometimes not replicate the children correctly.
- Revisited stability of bindings around edge cases (invalid bindings, missing components, null references, duplicates...).
- Fixed issue where removed components from prefab instances could not be repaired.
- Missing components are displayed correctly in the 'Fix Prefab Instance' dialog.
- Fixed an issue when a local simulator build is incorrectly marked as an invalid binary by MacOS running on Apple Silicon.
- Avoid prefab validation and updating when entering Play Mode.
- Fixed issue where a replaced duplicate could be disabled in a bad network environment.
- Commands referencing yet unknown entities are now being held onto until the referenced entities are received.
- Fixed the simulator build path for Windows.
- Name collision when using the lobbies sample if a class called Player exists in the project.
- Compilation error when using a version of Test Framework below 1.4.4.
- When using [Sync] and [Command] attributes, Prefab Variants will no longer create unnecessary binding overrides.
- Fixed exception when changing INetworkObjectProvider (CoherenceSync editor's "Load via" setting) when the Prefab is within a subfolder (for example, Resources/Prefabs)
- Missing reference when joining a cloud room on certain projects
- Upload build button disabled until build folder is selected again if project changes.
- Interpolation overshoot is now limited instead of infinite.
- Lowered interpolation latency and improved on some edge cases.
- Improved reliability of CoherenceBridge's 'Auto Login As Guest' behaviour.
- Added message to the console if the project is run after an upgrade without baking first.

## [1.3.1]

### Added
- Error log in the connection when sending more bytes than the specified MTU.
- Moved AutoSimulatorConnection from Coherence.UI into Coherence.Simulator.
- Avoid postprocessing assets on clones.
- Extended Project window icon to inform about more problems with project configuration.

### Changed
- Changed `ICoherenceBridge` interface from `Entity UnityObjectToEntityId(CoherenceSync from)` to `Entity UnityObjectToEntityId(ICoherenceSync from)` which is potentially a breaking change.
- Changed `ICoherenceSync` interface to add accessors for the `transform` and `gameObject` property which is in line with the `CoherenceSync` behaviour. This is potentially a breaking change.

### Fixed
- EntitiesManager now catches any exceptions thrown by user code during disconnection handling, ensuring that the clean up process will finish up properly even if this happens.
- Fixed issue with offline commands not working if CoherenceBridge was `null`.
- WebGL build reliability improved.
- Fixed ArgumentNullException in CoherenceSync Editor when a CoherenceSync component was attached to a prefab instance.
- Fixed issue where inspecting a CoherenceSync component using Debug Mode while in Edit Mode, could result in a NullReferenceException when later entering Play Mode.
- Fixed the validation to allow "Only predict with input authority" to be selected if a CoherenceInput was attached to a game object.

## [1.3.0]

### Added

- Sync attribute has a new _DefaultSyncMode_ property to configure _SyncMode_ on the binding.
- Invalid command bindings are reported on recompile. Warnings and errors are shown in the console if a method containing an invalid signature is decorated with a CommandAttribute
- A new property called EnableClientConnections has been added on CoherenceBridge. This replaces the 'globalQueryOn' field and 'GlobalQueryOn' property. They have both been marked as obsolete and should not be used moving forward.
- Added main menu item ´coherence > Help > Report a Bug...´ which can be used to auto-generate a diagnostics file, which can be shared with coherence to help with debugging an issue.
- If the flag Application.runInBackground is not 'true' a warning is written to the console when the game is played in the editor
- Added more unit testing to EntitiesManager
- CoherenceSyncEditor and the Bindings window now give accurate information about binding states in various contexts, as well as more accurately detect edge cases where bindings should not be editable, and clearly inform the user about them.
- Extended CoherenceSync editor with the ability to detect more undesired prefab overrides in prefab instances, and display a warning box and a button to automatically fix them.

### Fixed

- NullReferenceException on ArchetypeComponent (Prefabs using LODs).
- NullReferenceException while changing variable or method settings on the Configure window.
- Hub: account name label could wrap the last character unnecessarily.
- Hub: cloud status label could word wrap unnecessarily.
- Reparenting after gaining authority could be reverted.
- Project settings: Disable connection timeout not working with rooms.
- Quaternion interpolation jittering.
- Cleared up the error messages in CoherenceInput to show if the scene needs baked or there is a missing CoherenceBridge.
- NullReferenceException on instantiating two copies of the same prefab using addressables in the same frame.
- Replicated prefabs loaded via addressables could lose commands sent to them from the authority.
- Fix observed interpolation jitter when instantiating and immediately changing the position of an entity with a large delta.
- Removed an error that appeared in the console when a fresh, unbaked, project was opened.
- `Coroutine couldn't be started` error when asynchronously unloading a scene containing a coherence bridge with a client connection.
- Changed the ´Is Kinematic´ component action for Rigidbody and Rigidbody2D to have ´Enable On Authority´ enabled by default. This most likely results in the desired behaviour.
- Library folder no longer appears alongside application when running a stand alone app
- Fixed auto-adoption of orphaned entities with 'Simulate In' set to 'Server Side With Client Input'.
- Added missing method to custom instantiator template
- Fixed game object title bar text being difficult to see when using Light skin.
- Removed error message logged to the console if Android build tries to bundle replication server
- Fixed path to `/UI/icons/Loader anim.png` from absolute to relative
- Fixed issue where position bindings could get removed from nested coherence sync objects.
- Samples browser shows correct thumbnail when opened
- Possibility of entity being created with old binding values in bad network conditions.
- Fixed issue with parenting and changing the floating origin in the same packet causing the child position to be wrong.
- Fixed org and project settings in coherence Cloud tab on hub not being saved between sessions
- Disabled the Simulator "Begin Upload" button if the organization or project hasn't been selected.
- Fixed issue where reusing the same CoherenceBridge instance to connect, disconnect and connect again, could in rare situations result in an unsynced network entity not getting claimed before the second disconnection.

### Changed
- It is not necessary anymore to manually add/remove COHERENCE_LOG_TRACE and COHERENCE_LOG_DEBUG defines. Those are now automatically added/removed when LogLevel is changed.
- Optimized `CoherenceSync.Awake()` and `CoherenceSync.OnEnable()`.
- Moved `RuntimeInterpolationSettings` from `Binding` to `ValueBinding.Interpolator`, changing the type from `InterpolationSettings` to `BindingInterpolator<T>`
- Network packets contain an end marker for better integration with 3rd party relays.
- `CoherenceBridge.EnableClientConnections` property replaces the `CoherenceBridge.globalQueryOn` field and the `CoherenceBridge.GlobalQueryOn` property.
- Obsolete methods can still be used as commands if adorned with the CommandAttribute.
- Reduced the bandwidth overhead of creating entities ~30 bytes per entity.
- Update engine version to v6.5.0
- Removed `schemaID` field from `RuntimeSettings` to stop it changing every time the application is baked. The `SchemaID` property of `RuntimeSettings` is still public but is now read-only
- Removed `CalcSHA1Hash()` from `Schema`
- Replication server endpoint for local simulator builds are stored between sessions.
- coherence Cloud tab now has clickable links to the main organization portal page, the project page and the organization usage page
- Added vertical scroll bars to the organization and project drop downs

## [1.2.4]

## Changed

- Documentation links.

## [1.2.3]

### Fixed

- When importing a project for the first time (no Library folder or Reimport All operation), the CoherenceSyncConfigRegistry (if missing) is created first.
- Prefab Instance UUID property gets removed on Prefab Assets now.
- RoomCreationOptions.FindOrCreate no longer hardcoded to false.
- When a CoherenceSync has property overrides that can make networking fail, the inspector informs you and allows you to delete them.
- Multi-Room Simulators: Local Forwarder no longer active on Cloud builds.
- ReplicationServerConfig.AutoShutdownTimeout to add new Replication Server CLI flag `--auto-shutdown-timeout-ms`.
- Disconnect and entity destroy operations release the entity ID now, allowing for scenarios where a huge amount of entities are created.
- Customizable MTU in ConnectInfo.

## [1.2.2]

### Added

- Lobby API ErrorCodes: LobbyNameTooLong and LobbyTagTooLong.
- Link to Release Notes on the Welcome window (on install or update).

### Fixed

- Handling a huge amount of entities no longer throws exceptions.
- Sending a command to a entity that's being destroyed no longer throws exceptions.
- `coherence > Local Replication Server > Backup World Data` toggle not being checked when the Editor is first loaded.
- Various links to documentation where leading to 'Page not found'.
- Inconsistencies when manually controlling time scale.
- Ability to bake variables named `data`.

## [1.2.1]

### Fixed

- Issue when installing the package on fresh projects, where a bunch of PreloadedSingleton-related exceptions would trigger.

## [1.2.0]

### Added

- Improved code stripping support for baked code.
- CoherenceSyncUtils API.
- Support for Unity's MPPM and ParrelSync for better in-editor testing.
- Schema inspector now shows all definitions, and links them with the related prefabs.
- On recompile, when coherence changes prefabs, they will be listed on a popup.
- CoherenceSync Inspector Header: Able to jump to the CoherenceSync definition while on inner child GameObjects.

### Fixed

- Reliability of automations around `[Sync]` and `[Command]`.
- Variables in Configure Window with the same name as Animator parameters could show the Animator icon.
- Code generation with lots of definitions.
- Configure Window rendering improvements - takes less time to render now.
- Allocations made per frame by CoherenceLoop even when disconnected.
- Stutter during reparenting.

### Changed

- CoherenceSync Inspector Header: Reworked, works with GameObjects in scenes and handles more prefab configurations.
- Reworked CoherenceSyncConfigRegistry and related APIs.
- Reworked Client-Side Prediction to be usabe outside a Server-Side with Client Input (CoherenceInput) setup. Variables exposed on the Configure Window can set this setting (coherence icon).
- Gathered.schema definition names to not include names of prefabs or Unity components.
- Enforce UDP for Cloud Simulators.
- Increased MaxStablePingDeviation for ping stability.
- IClient.SetTransportFactory no longer accepts null argument. Use the new IClient.SetTransportType instead.

### Removed

- CoherenceSyncConfigManager. See CoherenceSyncConfigRegistry and CoherenceSyncConfigUtils.
- CoherenceSyncConfigRegistry: save modes. Now CoherenceSyncConfigs are referenced exclusively as external assets in Assets/coherence/CoherenceSyncConfigs. The registry can still read subassets, but this functionality will be deprecated in future versions.
- Deprecated members since 1.0 and earlier.

## [1.1.5]

### Fixed

- Multi-Room Simulators: fixed connection being rejected by the replication server.
- Multi-Room Simulators: connection to subsequent rooms no longer fails.
- Initial support for code stripping.
- Latency is reported correctly now.
- Increased max deviation for stable ping from 10 to 17.

## [1.1.4]

### Fixed
- Commands routing popup would show scrollbars on specific scaling settings, hiding the information.
- CoherenceRelayManager could cause a stack overflow if there were errors in the underlying UDP transport.

## [1.1.3]

### Fixed
- Labels no longer overflow when using a UI Scaling setting different that 100% (on Windows: Edit > Preferences... > UI Scaling).

### Changed
- Creating a Command on the Configure Window now defaults to "Send to Everyone, including yourself".

## [1.1.2]

### Added
- Sample Scenes: accessible from the package samples section and coherence's Explore Samples menu.

### Fixed
- Source Generator skips baked scripts that don't compile.
- CoherenceNode: no longer teleports to origin when reparenting.

## [1.1.1]

### Added
- `coherence > Local Replication Server > Backup World Data` toggle.

### Changed
- Hub: Persistence renamed to _Backup World Data_, and disabled by default for local replication servers.

### Fixed

- Version label in the Hub copies correct OS information to the clipboard.
- CoherenceSync's Configure window no longer shows the Animator Controller on variables that have the same name as animation parameters.
- No longer send invalid updates when gaining authority.

## [1.1.0]

### Added

- CoherenceBridge: ability to disconnect from within the inspector.
- CoherenceSync: support for `byte`, `sbyte`, `short`, `ushort` and `char`.
- CoherenceSync: support for nested prefabs and hierarchies via `PrefabSyncGroup` component.
- Sample: matchmaking with manual authentication.

### Fixed

- Game Object getting deselected after adding CoherenceSync.
- Reduced amount of GC allocs made.
- Release WebSocket when leaving Play-Mode.
- Instantiated InterpolationSettings never getting destroyed.
- Time scale related issues, including jitter and low-FPS scenarios.
- Restrict and warn about the _31 components per entity_ limitation.
- Resolve inheritance when sending commands.
- Client Connection prefab instances initialization issue when using the `CreateScene` API.
- Issue where mixing `Transform` and `RectTransform` networked variables could break.
- Console error after baking when `Upload Schema on Baking Complete` was on.
- Profiler: count messages received.
- Sample: wasn't refreshing worlds properly in some scenarios.
- CoherenceSync: deleting a Component or a GameObject on the hierarchy spammed errors on the console.
- Uploading WebGL builds now show the correct information on the progress bar.
- Console errors after deleting a prefab, when the Coherence Objects Window has been opened and closed.

### Changed

- CoherenceNode: improved stability.
- CoherenceSync: reworked Play-Mode panel on the inspector.
- CoherenceSync: allow sending commands while disconnected (method gets called locally).
- CoherenceSync: Configure window shows variables and methods tagged with `[Sync]` and `[Command]`.
- CoherenceScene: editor scene visibility settings split for clients and simulators.
- Reworked interpolation. Removed overshoot settings.
- Reworked code generation. Now, it's implemented on the Unity/C#-side.
- Updated JWT implementation, which is no longer provided as a pre-build DLL. This also prevents dependency conflicts.
- HTTP request timeout changed from 1 second to 10 seconds.
- Multi-Room Simulators Wizard: reworked, simplified layout.

### Removed

- Unity 2020 support.

## [1.0.5]

### Added

- Extrapolation: Max Overshoot Allowed range increased to [0, 20].
- Extrapolation: Expose stale factor (defaults to 2).

### Fixed

- WebSocket request timings.
- OnValueSynced not triggering when the update packet came with a parenting change.
- Shifting the floating origin now correctly shifts rigidbodies in the same frame without kicking in the physics interpolation.

### Changed

- Highly reduced GC allocs made while serializing.

## [1.0.4]

### Added

- CoherenceBridge API: TranslateFloatingOrigin(Vector3d) overload.
- Descriptor Providers: Added OverrideGetter and Setter options to be able to fully customize how the getters and setters are generated in the baked code.

### Fixed

- Floating Origin: fixed entities jittering on remote clients when Floating Origin is changed.

## [1.0.3]

### Added

- Tutorial Project: Released the Campfire Tutorial Project where you will be able to delve into more advanced aspects of how to use coherence.

## [1.0.2]

### Fixed

- CoherenceNode: Reset on OnDisable.
- Reset entity reference when destroying the entity.
- Force re-cache SchemaID after writing Gathered.schema to disk.

## [1.0.1]

### Fixed

- Commands: fixed trying to send a command from an inherited class that is bound in the parent class.
- Bindings: fixed rare cases where RectTransform bindings would be reported as missing.
- Client Connection Prefabs: fixed Client Connection instances duplication when changing Physics scene.
- Coherence Profiler: fixed count of messages received.
- ReplicationServerRoomsService: fixed RemoveRoom methods.
- Optimize window rendering issue where rows would disappear.

## [1.0.0]

### Added
- Lobby Rooms support.
- Multi-scene support.
- TCP fallback support.
- Self-hosting support.
- Protocol: added support for ordered components so parenting component changes always arrive with related position changes.
- CoherenceSyncConfigRegistry: Added new ScriptableObject to soft reference CoherenceSync assets in runtime and serialize how they are loaded and instantiated.
- INetworkObjectProvider: Added runtime interface to be able to customize how CoherenceSync assets are loaded, with three default implementations (Resources, Direct Reference and Addressables)
- INetworkObjectInstantiator: Added runtime interface to be able to customize how CoherenceSync prefabs are instantiated, with three default implementations (Default, Pooling and DestroyCoherenceSync)
- Added support for object pooling and a default pooling implementation.
- CoherenceSyncConfigUtils: Editor public API to be able to add, delete and browse CoherenceSync assets, aswell as start or stop syncing variables via scripting.
- CoherenceSync Objects Editor Window: New Editor window to be able to browse CoherenceSync assets, found under coherence => CoherenceSync Objects menu item.
- UniquenessManager: Encapsulated uniqueness logic under CoherenceBridge.UniquenessManager, where you will be able to register unique IDs at runtime.
- AuthorityManager: Encapsulated authority transfer logic under CoherenceBridge.AuthorityManager, where you will be able to send authority transfer requests without accessing the CoherenceSync directly.
- CloudService: non-static public API to communicate with your coherence Cloud project.
- ReplicationServerRoomsService: non-static public API to be able to create, delete and fetch rooms from self-hosted Replication Servers.
- Samples: Explore Samples window.
- Samples: New Connection Dialog Samples for Rooms and Worlds.
- Enter Play Mode Options (No Domain Reload) is now fully supported.
- CoherenceSync: Rigidbody Update Mode.
- CoherenceSync: Advanced Uniqueness Options.
- CoherenceSync: CoherenceBridge resolver API.
- Coherence.Editor.UploadBuildToCoherence API.
- Profiler: A coherence Profiler module for basic networking information.

### Fixed
- ParrelSync/symlink support: avoid read exceptions on Gathered.schema.
- CoherenceSync: Initialization of disabled objects.
- CoherenceSync: adding the component to a prefab no longer deselects it.
- Source Generator: avoid running on IDEs, to avoid IO-related exceptions.
- Simulator Slug not being encoded correctly.
- CoherenceNode: race condition when setting parent.

### Changed
- CoherenceSync: CoherenceSync instances are now automatically synced and usynced with the network in the OnEnable/OnDisable methods, instead of Start/OnDestroy.
- CoherenceSync: CoherenceSync instances can now be disabled and reused for different network entities, which allows for object pooling to happen.
- CoherenceSync Baked Scripts: Baked scripts for CoherenceSync prefabs are no longer MonoBehaviours.
- Uniqueness: Improved handling unique IDs for Unique CoherenceSyncs when creating serialized scene prefab instances.
- Updated JWT to 10.0.2.
- Reworked AutoSimulatorConnection component.
- Minimum supported version is now Unity 2021.3 LTS.

### Deprecated
- PlayResolver API: Play, PlayClient and PlayResolver have been deprecated in favour of CloudService API.
- Sample UI: Old Sample UI, that depended on the PlayResolver API has been deprecated in favor of the new Unity Package Samples.
- CoherenceUUID: This Component is no longer needed and it has been deprecated, the functionality has been baked into the Editor automatically. Prefabs that use this Component can stop using it.
- CoherenceMonoBridgeSender: This Component is no longer needed and it no longer has any functionality, it can be safely removed.
- Client Connection Prefab References: References to the prefabs in CoherenceBridge have been deprecated. Please use CoherenceSyncConfig references instead.

### Removed
- PrefabMapper has been removed in favor of CoherenceSyncConfigRegistry.
- NetworkTime.Time

## [0.10.15]

### Fixed
- Don't destroy children of duplicate unique entity.

### Removed
- TCP fallback (to be reworked in the next major version).

## [0.10.14]

### Fixed
- Permissions issue while executing baking or a Replication Server on macOS.

## [0.10.13]

### Added
- Floating Origin support. It's now possible to move the whole world underneath an entity.

## [0.10.12]

### Fixed
- Compilation on Unity 2022.2+.
- Revert warning on the CoherenceSync inspector where the object is not in the Prefab Mapper.

## [0.10.11]

### Added
- Warn about prefabs not being added to the Prefab Mapper when baking from the CoherenceSync inspector.

### Fixed
- BakeUtil.Bake method return value.
- LogSettings saved on fresh imports.
- Multi-Room Simulators: no longer attempt to serve more than one HTTP server.

### Changed
- Log settings are now stored in `Library/coherence/logSettings.json`.
- Improved coherence Hub performance.

## [0.10.10]

### Added
- BakeUtil.OnBakeStarted and BakeUtil.OnBakeEnded events.
- Multi-Room Simulators: reconnection timeout.

### Fixed
- Sample UI hanging when there's no connection.
- Reduced allocations made by the SDK.
- Auto-update RuntimeSettings after bake.
- Entering Play Mode is faster on big projects.
- CoherenceInput: works with prefabs containing spaces in their name.
- Faster synchronization of Transform's position.
- Connection: packets filtered by roomID to prevent cross-room data contamination.

### Changed
- CoherenceNode: fixed order of execution. Now, updates are applied before parenting is resolved.
- Gathered.schema moved to Assets/coherence/Gathered.schema (from Library/coherence/Gathered.schema).
- Can no longer disable Toolkit/Reflection/Generic Schema on Settings. It's required.
- RuntimeSettings' SchemaID property warns about outdated schemas.
- Optimize window: state that it's an experimental feature.

### Removed
- No longer prompt to bake before entering Play Mode.
- Bake Window button found in Optimize window.

## [0.10.9]

### Fixed
- macOS builds uploaded to coherence Cloud keep the Application Bundle (.app) structure intact.
- When a simulator fails to build, scripting define symbol `COHERENCE_SIMULATOR` is properly removed.
- Faster assembly reloads, since coherence no longer tries to load your project prefabs (was done to inform about possible issues or misconfigurations).
- WebGL won't crash when failing to create a websocket.
- coherence Hub: warn about 'Run in Background' being disabled.

## [0.10.8]

### Added
- TCP fallback when UDP connections fail or are blocked.
- CoherenceSync: ability to trigger authority transfers from within the inspector while in Play Mode.
- CoherenceSync: when adding CoherenceSync to a Prefab Instance, it's possible to apply CoherenceSync to the original Prefab, instead of creating a new one.

### Fixed
- Sample UI: refreshing was performed multiple times. Now, it also takes less time to get the state updated.
- Sample UI: highly reduced GC allocations in watermark.
- CoherenceSync: adding a CoherenceSync inside a Prefab Stage resolves 'Load via' properly now.
- CoherenceSync: moving to Assets/Resources won't fail if the folder doesn't exist yet, and it's possible to issue from within a Prefab Stage.
- Replication issues when commands or inputs being sent right after instantiation.

### Changed

- Sample UI: layout.
- Sample UI: watermark shows current room/world ID, region and schema ID.

## [0.10.7]

### Added
- New PlayResolver.RemoveRoom API.
- Sample UI: detect available local Replication Server.
- CoherencePlayerName component to ease networking the player name set on the Sample UI.

### Fixed
- Packets received from incorrect endpoints are ignored. 
- Texture-loading related issues (editor).
- Build uploading on macOS.
- Hub: improvements to the Overview section.
- Hub: Multi-Room Simulators Wizard no longer reopens.
- Wait for AssetDatabase to be ready before performing changes to Prefabs.
- CoherenceSync: No Duplicates resolves uniqueness properly now.

### Changed
- Bake automatically defaults to false.
- Sample UI: improved layout and better state management.
- CoherenceSync: rewritten tooltips.
- PlayResolver.FetchWorlds takes optional region and simulator slug as arguments.

## [0.10.6]

### Fixed
- Uploading schemas from CI using COHERENCE_PORTAL_TOKEN.

### Changed
- Baking automatically on building a Unity Player no longer updates prefabs.

## [0.10.5]

### Fixed
- CoherenceSync: with rigidbody, parented under a object without the CoherenceSync, now correctly syncs position

### Changed
- Connection Dialog defaults to rooms.

## [0.10.4]

### Added
- Play.IsLoggedIn to wait before interacting further with the Play API.
- Authority-side sample interpolation that removes jitter for bindings that are sampled at high frequencies. 
- CoherenceSync: fully configurable in prefab variants. Prefab overrides can be visualized both in the CoherenceSync inspector and the Configure window.
- CoherenceSync: 'Preserve Children' to prevent connected entities (CoherenceSyncs in hierarchy) from being destroyed if the parent entity is destroyed.
- CoherenceSync: [Sync] and [Command] attributes now allow you to specify the old name of the member to migrate it in place (example: `[Sync("myOldVar")] public float myVar;`).
- CoherenceMonoBridge: onLiveQuerySynced exposed on the inspector (OnLiveQuerySynced deprecated).
- Build sharing: improved build path validation.
- Baking: inform about current limitation of 32 schema components.
- Source Generator: improved reports so that it's clear when there's a failure.
- PlayResolver: Option to create or join existing room with matching tags.
- DescriptorProvider custom data object for extended binding support.

### Fixed
- Networked prefabs are now instantiated with their original rotation if the rotation is not synced.
- Truncated floating point precision in the 'Optimize' window.
- Losing latest changes when transferring authority.
- Overshooting and overflowing of integers while interpolating
- WebGL client automatically disconnecting after Replication Server shutdown.
- Recycling of EntityIDs caused by a double delete of an ID when an entity is destroyed because of a parent moving out of the query.
- Negative latency calculations during cpu spikes.
- Fixed ConnectionSettings.PingOnConnect causing a timeout when connecting.
- Editor getting stuck in an infinite loop when trying to import packages that reference missing types.
- CoherenceNode script is now executed before CoherenceSync to mitigate parenting validation warnings.
- Entities that exceed the room's max entity count are destroyed and a warning is logged.
- Cloning GameObject instances with baked bindings using Instantiate would result in binding errors due to missing guids.
- Warnings spammed when exiting playmode in Unity Editor.
- Baking: process running longer than expected.
- Baking: gather bindings from inactive GameObjects in hierarchy.
- Baking: fixed NullReferenceException by skipping missing scripts.
- Hub: don't show synchronous fetch organization progress bar.
- Hub: improved scene setup instructions.
- Hub: improved multi-room simulators setup.
- More coherence components implement a custom editor now, showing the logo and a brief description.
- Source Generator: allow on 2020.3.
- Source Generator: first time import was failing due to directories not existing.
- CoherenceSync: Binding the same method as a command multiple times in the same prefab is now supported.
- CoherenceSync: Fixed issue with parenting and interpolation which would result in a few frames of the connected entity being in the wrong place.
- CoherenceSync: Use the method SendCommandToChildren to send a command to every bound component in the hierarchy.
- CoherenceSync: New overload for the SendCommand method that accepts an Action, you can use it to send a command to a specific Component instance, if a method is bound to more than one component in the hierarchy.
- CoherenceSync: disable prediction controls on reflection (non-baked) mode.
- CoherenceSync: set default interpolation.
- CoherenceSync: fix error on cancelling interpolation asset creation.
- CoherenceSync: opening the inspector should not load additional assets (noticeable speedups on bigger projects).
- CoherenceSync: don't allow binding to generic members.
- CoherenceSync: collapse 'Custom Events' section by default.
- CoherenceSync: don't force 'Steal' authority transfer on persistent entities.
- CoherenceSync: destroying entity that was transferred immediately after creation.
- CoherenceNode: executed before CoherenceSync to mitigate parenting validation warnings.
- Sample UI: don't auto-generate EventSystem, but inform when it's missing.
- Sample UI: fix default room name.
- Sample UI: fix Refresh button throttling (too many requests).
- PlayClient: Fixed missing client version header.
- PlayResolver: Cached rooms not filtered by tags.
- Analytics: Exception thrown due to threading error.

### Changed
- CoherenceSync: polymorphic bindings using serialized references. Upgrading from older coherence versions requires migration (done automatically, can trigger manually via menu item 'coherence / Reimport coherence Assets').
- CoherenceSync: configurable rigidbody component action. You can decide when isKinematic should be (un)set.
- CoherenceSync: authority transfer 'No' -> 'Disabled'.
- CoherenceHub: last tab opened is now persistent when closing and opening the window again.

### Removed
- CoherenceMonoBridge: OnShutdown event.

## [0.9.2]
### Fixed
- Spawning of session-based unique entities that had a UUID in the prefab and in an instance saved in the scene.
- OnInputSimulatorConnected is now raised for Simulator or Client-As-Host only if they have both input and state authority.
- Selecting prefab variants no longer updates the internal archetype name (hence, no unintended schema changes).
- Configure window: don't add Animator parameters if they already exist.
- Warn when there's internal inconsistencies within CoherenceSync archetype data.
- Hub: disabled interactions with coherence Cloud when logged out.
- State and Input authority change callbacks are now called on persistent unique entities when a client loses state or input authority.
- Editor getting stuck in an infinite loop when trying to import packages that reference missing types.

### Changed
- The Welcome window is presented when the package is upgraded.

## [0.9.1]
### Added
- Exposed OnConnectionError event in CoherenceMonoBridge.

### Fixed
- Automatic passing of room secret when using PlayResolver.GetRoomEndpointData.

## [0.9.0]
### Added
- Per-binding sampling rate configurable from Optimize window.
- Additional delay parameters added to InterpolationSettings.
- RS can send the client commands to change the connection's send frequency of packets.
- New floating point compression methods.
- Quaternion and Color compression control.
- CoherenceMonoBridge.OnLiveQuerySynced event for notifying when the initial query finishes syncing.
- CoherenceMonoBridge.ClientConnections.OnSynced event for notifying when connections finish syncing.
- Threaded keep alive packets to keep connection alive even when the main thread is stalled.
- Allow manually sending changes on a CoherenceSync object.
- SimulatorBuildPipeline public API: static API to build simulators, with headless support out of the box.
- Unity 2021: Headless Unity Players are now built with the Dedicated Server modules.
- coherence Hub window: A singular tool combining all the functionality you need to work with coherence.
- Added option to copy the run local simulator build terminal command to the clipboard.
- Queries can be added to any synced game object.
- Queries are applied to the input authority. This enables server-authoritative games to restrict client visibility by placing a query on the player prefab.
- Client can connect as a simulator.
- Client can connect as a host for server-simulated inputs (Experimental).
- Local RS can now set both send and receive frequencies.
- Added options in settings to auto bake and upload schemas.
- Added support for Addressables
- Log to file in Library/coherence.
- Icons showing input authority in editor hierarchy.

### Removed
- Manual and auto-latency removed. Latency is now fixed to the binding's sample rate.
- 'World size' setting.
- Manual disabling of updates to rotation and position of the CoherenceSync object.
- Removed CoherenceMonoBridge.OnConnectionCreated that was deprecated in v0.8.
- Removed CoherenceMonoBridge.OnConnectionDestroyed that was deprecated in v0.8.
- Removed CoherenceMonoBridge.ClientConnectionCount that was deprecated in v0.8.
- Removed CoherenceMonoBridge.GetMyConnection that was deprecated in v0.8.
- Removed CoherenceMonoBridge.GetClientConnection that was deprecated in v0.8.
- Removed CoherenceMonoBridge.GetAllClientConnections that was deprecated in v0.8.
- Removed CoherenceMonoBridge.GetOtherConnections that was deprecated in v0.8.
- Removed CoherenceMonoBridge.SendClientMessage that was deprecated in v0.8.
- Removed dependency to the experimental package Platforms, as a result, your existing BuildConfiguration assets will have its defining script missing. To recreate the Simulator build configuration with the new pipeline, you can do it from the Simulators Module in Coherence hub.
- Removed dependencies to the following packages: Collections, Properties, Test Framework, Jobs, Burst.
- No longer show byte arrays, bools or strings as interpolable types in the Configure window.

### Changed
- Entity changes are now based on the simulation frames from their origin.
- ServerUpdateFrequency is now SendFrequency.
- New fields marked for syncing use no compression by default.
- Commands do not use any compression for simple types.
- CoherenceMonoBridge.ClientConnections.OnDestroyed is now called for the local connection.
- Baking timeout increased to 2 minutes.
- Baking is now cancellable.
- CoherenceSync.HasPhysics is now obsolete.
- CoherenceSync's Rigidbody position and rotation is now updated only in the FixedUpdate. In all other cases the Transform.position/rotation will be updated directly.
- Predicted toggle is available for prefabs with no CoherenceInput, but prompts a warning in this case. 
- Exposed IsInterpolationNone on InterpolationSettings so runtime can check if a binding will interpolate.
- Moved the auto-fill functionality from the Endpoint data to run a local simulator, from being automatic, to do it manually through a button in the UI.
- CoherenceInput fields now only transmit changed field state instead of entire component to save bandwidth.
- Authority requests and transfers use ClientID instead of ConnectionID.
- Requesting authority over orphaned entities now works only via CoherenceSync.Adopt() call.
- Improved layout of settings.
- Button in prefabmapper now automatically places prefabs in the correct list (Addressable/non-addressable)
- Reconnect method added to the CoherenceMonoBridge.

### Fixed
- Baking timing out with high number of synced components.
- CoherenceLiveQuery and CoherenceTagQuery handling on disable and after disconnect.
- Sending commands from the `onConnected` callback.
- OnValueSynced callbacks working for all value types
- Byte array sync bugfix.
- Sampling and send rates now account for time drift across update calls.
- Error when trying to dispose non-activated InterpolationSettings.
- Unsigned long handling in reflection mode.
- Creating connection objects with Global Query turned off.
- "WebSocket: already connected" error when changing scenes.
- Bug where reconnecting would cause incoming commands to duplicate.
- OnEntityUpdated exception caused by samples with frames out-of-order.
- WebGL connection timeout issues.
- Double query creation in some circumstances.
- AutoSimulatorConnection fixed and made the default way to reconnect simulators. Two obsolete scripts removed.
- OnValueSynced event will raise after smaller deltas.
- If baked script is missing, fallback to generic pool will map bindings.
- OnValueSynced on components shared by multiple prefabs.
- Deleting and recreating unique entities in the same frame no longer results in broken state.
- Pending entity changes that are not sent in a given packet have their priority increased so that they don't starve out.
- Prevent sending component remove updates until the client knows the RS ack'd the original create.
- Checking for a received packet before deciding we haven't received one and disconnecting. Fixes issue when the main thread stalls before we read packets causing a timeout disconnect.
- Authority change handling for unique entities.
- CoherenceSync spawn issues in bad connection conditions.
- Include the instantiating bridge as the fist resolver when resolving bridges in a CoherenceSync object.
- Building Simulators: Fixed issue where you had to click the Build button twice, in order for the build to trigger.
- CustomBinding respects setToLastSamples parameter during Reset.
- InputAuthority is displayed correctly in the CoherenceSync inspector.
- SmoothTime is now 0 when using InterpolationSettings.None.
- Default Interpolator now implements latest sample instead of nearest neighbor to lower latency.
- Interpolation Type set to None now doesn't throw exceptions.
- Interpolation overshooting now works as expected.
- Interpolation doesn't jitter on first movement.
- Prefabs set to load using Resources will no longer be overridden to use prefabmapper.
- Prefab instance name could break LOD's.

### Deprecated
- 'AuthorityMode' has been deprecated in favour of 'AuthorityType'.
- 'IClient.OnAuthorityTransferRequest' has been deprecated in favour of 'IClient.OnAuthorityRequested'.
- 'IClient.OnAuthorityTransferRejected' has been deprecated in favour of 'IClient.OnAuthorityRequestRejected'.
- 'IClient.OnAuthorityChanged' has been deprecated in favour of 'IClient.OnAuthorityChange'.
- 'IClient.EntityIsOwned' has been deprecated in favour of 'IClient.HasAuthorityOverEntity'.
- 'IClient.SendAuthorityTransferRequest' has been deprecated in favour of 'IClient.SendAuthorityRequest'.
- 'IClient.AuthorizeAuthorityTransfer' has been deprecated in favour of 'IClient.SendAuthorityTransfer'.
- 'SpawnInfo.isLocal' has been deprecated in favour of 'SpawnInfo.authorityType'.
- 'CoherenceInput.SetInputOwner' has been deprecated in favour of 'CoherenceSync.TransferAuthority'.
- 'CoherenceSync.isSimulated' has been deprecated in favour of 'CoherenceSync.HasStateAuthority'.
- 'CoherenceSync.OnAuthorityRequestedByConnection' has been deprecated in favour of 'CoherenceSync.OnAuthorityRequested'.
- 'CoherenceSync.OnAuthorityGained' has been deprecated in favour of 'CoherenceSync.OnStateAuthorityGained'.
- 'CoherenceSync.OnAuthorityLost' has been deprecated in favour of 'CoherenceSync.OnStateAuthorityLost'.
- 'CoherenceSync.OnAuthorityTransferRejected' has been deprecated in favour of 'CoherenceSync.OnAuthorityRequestRejected'.
- 'CoherenceSync.OnInputOwnerAssigned' has been deprecated in favour of 'CoherenceSync.OnInputAuthorityGained' and 'CoherenceSync.OnInputAuthorityGained'.
- 'CoherenceSync.RequestAuthority' has been deprecated in favour of 'CoherenceSync.RequestAuthority(AuthorityType)'.
- 'MonoBridgeStore.GetBridge' has been deprecated in favour of 'MonoBridgeStore.TryGetBridge'.

## [0.8.0]
### Added
- New command target "Other": send a command to all instances, excluding yourself.
- CoherenceSync.ResetInterpolation() to prevent interpolation to kick in in some cases, such as teleporting entities.
- BakeUtil exposes bake functionality.
- Double precision NetworkTime utility.
- FixedUpdateInput in the CoherenceMonoBridge that solves the 'FixedUpdate' input sampling (loss) issue.
- Debug ability to pause sending packets from Coherence.Connection to provide 'dropped' packet integration tests.
- Disconnection reason in the 'OnDisconnect' event.
- Support for multi-room simulators.
- CoherenceSceneLoader and CoherenceScene to handle multi-scene setups.
- SimulatorEventHandler component to expose events depending on if your build should behave as a client or as a simulator.
- ConnectionEventHandler component to expose connection related events.
- Support for all protocol primitives in reflected mode (long, quaternions, vector types) in both commands and components.
- Per-scene context for logging.
- Unsigned int / long types to protocol primitives.
- Interpolation of unsigned int / longs.

### Changed
- CoherenceClientConnection no longer requires subclassing, any GameObject with the CoherenceSync attached can be used.
- Separate prefabs for client and simulator connection objects in the CoherenceMonoBridge.
- Fine grained control over connection object prefab through the CoherenceMonoBridge.ClientConnections.ProvidePrefab event.
- Schema definitions for String64 and Byte4096 changed to String and Bytes.
- Support 63 byte strings in schema fields, up from 61
- Support 511 byte byte arrays in schema fields, down from 4096, but could never send that much anyway, so memory is saved.
- Removed 'connectionID' from the 'OnDisconnect' event.
- A CoherenceMonoBridge should be included in the scene for coherence find it — they won't be created on the fly anymore.
- Optimized sending command parameters in reflected mode.  Only as much data as needed is sent and the limitations on numbers of parameters is removed except for entity references which is still capped at 4.  The rest of the parameters only have to fit in 511 bytes, in total, to be sent.
- Authentication is now enforced when connecting to the Replication Server. This is handled automatically when using the provided UI utility or when developing locally. When connecting manually, the 'EndpointData.authToken' must be set before calling the 'IClient.Connect'. Use 'Auth.SessionToken' to retrieve token provided by Play services.
- PlayClient requests are now throttled.
- CoherenceSync authority events no longer fire on instantiation and upon connecting to server.

### Fixed
- Input owner initialization for the CoherenceInput with 'ClientSide' simulation.
- Interpolation elasticity using properly scaled network time.
- Exception in CoherenceSync.Awake() when using CoherenceInput.
- CoherenceInput.Delay is now reset back to the base value upon time sync.
- CoherenceInput.IsReadyToProcessInputs now takes into account input ownership.
- String serialization for non-ASCII characters.
- No longer sending delete entity updates after a dropped create.
- Sending only when there's space in the ack window.
- Command name string length off-by-one error.
- CoherenceInput network time reset loop with 'Disconnect on time reset' enabled.
- CoherenceInputSimulation can be run with just one client.
- CoferenceInput pause logic.
- Now detecting when a message is too large to serialize and logging error instead of sending broken packet to RS.
- Fix allowing Simulators to regain authority over transferred CoherenceInput objects with a refactor of how auto authority is processed.
- Fix allowing commands with no parameters to work again.
- Interpolation of integer types at large values.
- Issue when selecting actively updating CoherenceSync object which caused editor warnings and errors.
- Server-side inputs not working due to the forever-withheld authority transfer.
- CoherenceSync "Optimize" window layout issues.
- CoherenceSync "Optimize" window numeric limits handling.
- Clamping int and uint values to the range specified in the "Optimize" window.
- Auto-adopt is now more reliable, there are not disputes over authority.


## [0.7.1]
### Fixed
- Reliability layer issues.
- Crash when using debug mode on streams.
- Authority transfer for WebGL.
- Handle prefabs with InterpolationLocation set to 0.
- Allow instances with different UUIDs.
- CoherenceSync leak through CoherenceMonoBridge.OnConnected.
- CoherenceInputSimulation now uses correct mono bridge.
- CoherenceClientConnection is now automatically destroyed upon disconnect.
- Entity with CoherenceInput and simulation mode set to 'Server Side With Client Input' is now destroyed upon disconnect of the original owner.
- (De)serialization of the empty & null strings.
- IConnection OnError event not firing.
- Watchdog support on Windows.
- Renaming variables or methods updates internal binding representations.
- RuntimeSettings preserved during upgrades.
- Don't set isKinematic automatically on GameObjects with CoherenceSync.
- Able to send commands to Transform and Animator methods.
- Schema hash calculation treats '\r\n' and '\n' the same.

### Added
- Allow setting the Coherence.ClientCore on the Coherence.UI.StatsCollection script directly instead of relying on using the Coherence.Toolkit.MonoBridge.
- Report player connection events to Coherence backend for active users reporting.
- Support RigidBody2D in CoherenceSync.
- Ability to define interpolation settings per binding (of any component). Supported types: int, float, Vector2, Vector3 and Quaternion.
- Support for Vector2 fields in reflected mode.
- Support for byte[] (byte4096) in reflected mode.
- Support for animator parameter interpolation.
- Prevent keepalive spamming.
- Input reordering.
- Input debugger.
- CoherenceSimulation utility.
- Events: on before networked instantiation, on authority requested.
- Handling Replication Server connection denial.
- Tag query.
- IClient now exposes the 'ServerUpdateFrequency'.
- CoherenceMonoBridge helper functions for coherence connections management.
- CoherenceMonoBridge can now be manually resolved using the MonoBridgeResolve function.
- Component actions: triggering actions in components based on network events made easy. Accessible via Select window in CoherenceSync.
- Quick Access window.
- Configurable host for local testing.
- Room creation and joining.
- Input owner: assign which client controls an object. 
- Callbacks for synced member value change through the OnValueChangedAttribute.

### Changed
- Sample UI (Connect Dialog) rework.
- CoherenceSync inspector rework.
- Renamed LinkedEntity to EntityID.
- Override base components when using single LOD step.
- LOD thresholds no longer limited to integers.
- Don't send connected entity updates on Start.
- Select variables and methods window rework.
- Optimize CoherenceSync window, including LOD and compression.
- CoherenceUniqueInScene deprecated.
- Duplicates + persistence is allowed now.
- Allow Duplicates sets the UUID to null at runtime.
- Instance CoherenceMonoBridge has precedence over singleton one.
- Global query is now set as a child of the CoherenceMonoBridge.
- New server launching keybindings: Ctrl + Shift + Alt + R/W for Rooms/World server.
- Keep-alive rate is now based on replication server update frequency and ping (if enabled).
- Default max number of entities for a locally created room is now based on the 'maxClients' parameter (x5).
- Broadcasting is now the default routing method for commands.
- Baked scripts instantiated at runtime — no need to attach them to the prefabs.

## [0.5.1]
### Fixed
- Custom bindings updating in play mode.
- Interpolation when ticked in the Update loop.

## [0.5.0]
### Added
- Support for WebGL.
- Support for broadcast commands.
- Support for client messages.
- Support for parent-child transform relationship synchronization.
- Support for editing CoherenceSync scene instances.
- Support for undoing bakes.
- Support for byte array type synchronization (4096 bytes).
- Safe Mode Baking: if baked scripts cause any compilation error, you can bake them in Safe Mode while you troubleshoot the issue.
- Watchdog: identifies compiler errors caused by changes to bound data, and tries to recover from them.
- Diagnosis Window: helps identifying data binding issues in coherence prefabs.
- Selectable Update method for interpolation.
- Build uploading to the Developer Portal.

### Changed
- CoherenceSync inspector.
- Persistence and uniqueness are now separate properties.
- Windows: Bake, Quickstart, Upgrade, Settings.
- Main Menu.
- OnAuthorityTransfer event is now split in OnAuthorityGained and OnAuthorityLost.

### Removed
- Dependency on DOTS (entities package).
- Persistence expiry.

### Fixed
- Commands using method overloads.
- Archetypes with no bindings are not emitted.

[2.0.0]: https://github.com/coherence/unity/compare/v1.8.0...v2.0.0
[1.8.0]: https://github.com/coherence/unity/compare/v1.7.0...v1.8.0
[1.7.0]: https://github.com/coherence/unity/compare/v1.6.0...v1.7.0
[1.6.0]: https://github.com/coherence/unity/compare/v1.5.1...v1.6.0
[1.5.1]: https://github.com/coherence/unity/compare/v1.5.0...v1.5.1
[1.5.0]: https://github.com/coherence/unity/compare/v1.4.3...v1.5.0
[1.4.3]: https://github.com/coherence/unity/compare/v1.4.0...v1.4.3
[1.4.0]: https://github.com/coherence/unity/compare/v1.3.1...v1.4.0
[1.3.1]: https://github.com/coherence/unity/compare/v1.3.0...v1.3.1
[1.3.0]: https://github.com/coherence/unity/compare/v1.2.4...v1.3.0
[1.2.4]: https://github.com/coherence/unity/compare/v1.2.3...v1.2.4
[1.2.3]: https://github.com/coherence/unity/compare/v1.2.2...v1.2.3
[1.2.2]: https://github.com/coherence/unity/compare/v1.2.1...v1.2.2
[1.2.1]: https://github.com/coherence/unity/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/coherence/unity/compare/v1.1.5...v1.2.0
[1.1.5]: https://github.com/coherence/unity/compare/v1.1.4...v1.1.5
[1.1.4]: https://github.com/coherence/unity/compare/v1.1.3...v1.1.4
[1.1.3]: https://github.com/coherence/unity/compare/v1.1.2...v1.1.3
[1.1.2]: https://github.com/coherence/unity/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/coherence/unity/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/coherence/unity/compare/v1.0.5...v1.1.0
[1.0.5]: https://github.com/coherence/unity/compare/v1.0.4...v1.0.5
[1.0.4]: https://github.com/coherence/unity/compare/v1.0.3...v1.0.4
[1.0.3]: https://github.com/coherence/unity/compare/v1.0.2...v1.0.3
[1.0.2]: https://github.com/coherence/unity/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/coherence/unity/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/coherence/unity/compare/v0.10.15...v1.0.0
[0.10.15]: https://github.com/coherence/unity/compare/v0.10.14...v0.10.15
[0.10.14]: https://github.com/coherence/unity/compare/v0.10.13...v0.10.14
[0.10.13]: https://github.com/coherence/unity/compare/v0.10.12...v0.10.13
[0.10.12]: https://github.com/coherence/unity/compare/v0.10.11...v0.10.12
[0.10.11]: https://github.com/coherence/unity/compare/v0.10.10...v0.10.11
[0.10.10]: https://github.com/coherence/unity/compare/v0.10.9...v0.10.10
[0.10.9]: https://github.com/coherence/unity/compare/v0.10.8...v0.10.9
[0.10.8]: https://github.com/coherence/unity/compare/v0.10.7...v0.10.8
[0.10.7]: https://github.com/coherence/unity/compare/v0.10.6...v0.10.7
[0.10.6]: https://github.com/coherence/unity/compare/v0.10.5...v0.10.6
[0.10.5]: https://github.com/coherence/unity/compare/v0.10.4...v0.10.5
[0.10.4]: https://github.com/coherence/unity/compare/v0.9.2...v0.10.4
[0.9.2]: https://github.com/coherence/unity/compare/v0.9.2...v0.9.1
[0.9.1]: https://github.com/coherence/unity/compare/v0.9.1...v0.9.0
[0.9.0]: https://github.com/coherence/unity/compare/v0.9.0...v0.8.1
[0.8.0]: https://github.com/coherence/unity/compare/v0.7.1...v0.8.0
[0.7.1]: https://github.com/coherence/unity/compare/v0.5.1...v0.7.1
[0.5.1]: https://github.com/coherence/unity/compare/v0.5.0...v0.5.1
[0.5.0]: https://github.com/coherence/unity/releases/tag/v0.5.0
