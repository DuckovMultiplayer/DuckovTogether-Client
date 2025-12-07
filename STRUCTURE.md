# Duckov Coop Mod - Project Structure

## Directory Layout

```
EscapeFromDuckovCoopMod/
|-- Core/                    # Core systems
|   |-- Loader/             # Mod loader
|   |-- Localization/       # Language files
|   |-- ModManager/         # Mod management
|   |-- CoopManager.cs      # Main COOP manager
|   |-- CoopUtils.cs        # COOP utilities
|   |-- ServerModeDetector.cs # Server mode detection
|   `-- ...
|
|-- Game/                    # Game logic
|   |-- AI/                 # AI synchronization
|   |-- Audio/              # Audio events
|   |-- Health/             # Health system
|   |-- Item/               # Item handling
|   |-- Player/             # Local player
|   |-- Scene/              # Scene management
|   |   |-- SceneService/   # Scene services
|   |   |-- WeatherAndTime/ # Weather system
|   |   `-- Teleport/       # Teleportation
|   |-- Voice/              # Voice chat
|   |-- Weapon/             # Weapon system
|   `-- FxManager.cs        # Visual effects
|
|-- Net/                     # Network layer
|   |-- Client/             # Client core
|   |   |-- CoopNetClient.cs    # Main network client
|   |   |-- CoopNetBootstrap.cs # Client bootstrap
|   |   `-- ClientService/      # Client services
|   |-- Handlers/           # Message handlers
|   |   |-- NetCombatHandler.cs
|   |   |-- NetItemHandler.cs
|   |   |-- NetWorldHandler.cs
|   |   `-- NetMessageRouter.cs
|   |-- Messages/           # Message definitions
|   |   |-- NetAI*.cs       # AI messages
|   |   |-- NetSceneVote.cs # Scene voting
|   |   `-- ...
|   |-- Sync/               # Synchronization
|   |   |-- NetPlayerController.cs
|   |   |-- NetAIController.cs
|   |   |-- NetInterpolation.cs
|   |   `-- NetLootSync.cs
|   |-- HybridNet/          # Hybrid networking
|   |-- NetPack/            # Packet utilities
|   `-- Relay/              # Relay services
|
|-- Patch/                   # Harmony patches
|   |-- Character/          # Character patches
|   |-- InventoryAndLootBox/# Inventory patches
|   |-- Item/               # Item patches
|   |-- Scene/              # Scene patches
|   `-- HarmonyFix.cs       # Main patch entry
|
|-- UI/                      # User interface
|   |-- ModUI.cs            # Main mod UI
|   `-- ...
|
|-- Utils/                   # Utility classes
|   |-- Logger/             # Logging
|   |-- GameObjectCache/    # Object caching
|   `-- ...
|
|-- Jobs/                    # Unity Jobs
|-- NetTag/                  # Network tagging
|-- SyncData/               # Sync data definitions
|-- Compat/                 # Compatibility layer
`-- Properties/             # Assembly properties
```

## Key Components

### Net/Client/CoopNetClient.cs
Main network client handling server connection, message routing, and state synchronization.

### Core/ServerModeDetector.cs
Detects whether connected to dedicated server and controls local logic execution.

### Net/Sync/NetPlayerController.cs
Manages remote player instantiation and interpolation.

### Net/Sync/NetAIController.cs
Manages remote AI entity synchronization.

### Game/AI/
AI-related game logic and synchronization helpers.

### Patch/
Harmony patches for intercepting game events and injecting multiplayer logic.
