using System;
using Exiled.API.Features;
using PlayerStatsSystem;
using Map = Exiled.API.Features.Map;

namespace Tesla_General
{
    /// <summary>
    /// Выполняет команды, пришедшие от лямбды (KillPlayer, Broadcast, StartWarhead и т.д.).
    /// </summary>
    public static class TeslaCommandProcessor
    {
        public static void ProcessActions(GameAction[] actions)
        {
            if (actions == null || actions.Length == 0)
            {
                if (MainPlugin.Singleton?.Config.Debug == true)
                    Log.Info("No actions to process.");
                return;
            }

            foreach (var action in actions)
            {
                switch (action.Command)
                {
                    case "Broadcast":
                        Map.Broadcast((ushort)action.Duration, action.Message);
                        if (MainPlugin.Singleton?.Config.Debug == true)
                            Log.Info($"Global broadcast sent: {action.Message}");
                        break;

                    case "BroadcastToPlayer":
                        var targetPlayer = Player.Get(action.TargetPlayer);
                        if (targetPlayer != null)
                        {
                            targetPlayer.Broadcast((ushort)action.Duration, action.Message);
                            if (MainPlugin.Singleton?.Config.Debug == true)
                                Log.Info($"Broadcast sent to {action.TargetPlayer}: {action.Message}");
                        }
                        else
                        {
                            if (MainPlugin.Singleton?.Config.Debug == true)
                                Log.Warn($"Player {action.TargetPlayer} not found for BroadcastToPlayer.");
                        }
                        break;

                    case "CassieAnnouncement":
                        if (!string.IsNullOrEmpty(action.Message))
                        {
                            Cassie.Message(action.Message);
                            if (MainPlugin.Singleton?.Config.Debug == true)
                                Log.Info($"CASSIE announcement made: {action.Message}");
                        }
                        else
                        {
                            if (MainPlugin.Singleton?.Config.Debug == true)
                                Log.Warn("CassieAnnouncement command received with an empty message.");
                        }
                        break;

                    case "KillPlayer":
                        var player = Player.Get(action.TargetPlayer);
                        if (player != null)
                        {
                            player.Kill(new CustomReasonDamageHandler("Killed by remote command"));
                            if (MainPlugin.Singleton?.Config.Debug == true)
                                Log.Info($"Player {action.TargetPlayer} has been killed by remote command.");
                        }
                        else
                        {
                            if (MainPlugin.Singleton?.Config.Debug == true)
                                Log.Warn($"Player {action.TargetPlayer} not found.");
                        }
                        break;

                    case "CancelWarhead":
                        if (Warhead.IsInProgress)
                        {
                            Warhead.Stop();
                            if (MainPlugin.Singleton?.Config.Debug == true)
                                Log.Info("Warhead deactivated by remote command.");
                        }
                        else
                        {
                            if (MainPlugin.Singleton?.Config.Debug == true)
                                Log.Warn("Warhead is not active.");
                        }
                        break;

                    case "StartWarhead":
                        if (!Warhead.IsInProgress)
                        {
                            Warhead.Start();
                            if (MainPlugin.Singleton?.Config.Debug == true)
                                Log.Info("Warhead started by remote command.");
                            EventCollector.AddSystemEvent("Warhead started by remote command.");
                        }
                        else
                        {
                            if (MainPlugin.Singleton?.Config.Debug == true)
                                Log.Warn("Warhead is already active.");
                        }
                        break;

                    case "DetonateWarhead":
                        if (Warhead.IsInProgress)
                        {
                            Warhead.Detonate();
                            if (MainPlugin.Singleton?.Config.Debug == true)
                                Log.Info("Warhead detonated by remote command.");
                            EventCollector.AddSystemEvent("Warhead detonated by remote command.");
                        }
                        else
                        {
                            if (MainPlugin.Singleton?.Config.Debug == true)
                                Log.Warn("Cannot detonate warhead as it is not active.");
                        }
                        break;

                    case "GiveItemToPlayer":
                        var recipientPlayer = Player.Get(action.TargetPlayer);
                        if (recipientPlayer != null)
                        {
                            if (Enum.TryParse(action.ItemId, out ItemType itemType))
                            {
                                recipientPlayer.AddItem(itemType);
                                if (MainPlugin.Singleton?.Config.Debug == true)
                                    Log.Info($"Item {action.ItemId} given to player {action.TargetPlayer} by remote command.");
                                EventCollector.AddPlayerEvent("GiveItem", action.TargetPlayer, $"Item: {action.ItemId}");
                            }
                            else
                            {
                                if (MainPlugin.Singleton?.Config.Debug == true)
                                    Log.Warn($"Invalid item type: {action.ItemId}");
                            }
                        }
                        else
                        {
                            if (MainPlugin.Singleton?.Config.Debug == true)
                                Log.Warn($"Player {action.TargetPlayer} not found for GiveItemToPlayer.");
                        }
                        break;

                    case "GiveEffectToPlayer":
                        var effectPlayer = Player.Get(action.TargetPlayer);
                        if (effectPlayer != null)
                        {
                            if (Enum.TryParse(action.EffectId, true, out Exiled.API.Enums.EffectType effectType))
                            {
                                effectPlayer.EnableEffect(effectType, action.Duration);
                                if (MainPlugin.Singleton?.Config.Debug == true)
                                    Log.Info($"Effect {action.EffectId} applied to player {action.TargetPlayer} for {action.Duration} seconds.");
                                EventCollector.AddPlayerEvent(
                                    "GiveEffect",
                                    action.TargetPlayer,
                                    $"Effect: {action.EffectId}, Duration: {action.Duration}"
                                );
                            }
                            else
                            {
                                if (MainPlugin.Singleton?.Config.Debug == true)
                                    Log.Warn($"Invalid effect type: {action.EffectId}");
                            }
                        }
                        else
                        {
                            if (MainPlugin.Singleton?.Config.Debug == true)
                                Log.Warn($"Player {action.TargetPlayer} not found for GiveEffectToPlayer.");
                        }
                        break;

                    case "TeleportToCoordinates":
                        var playerToTeleport = Player.Get(action.TargetPlayer);
                        if (playerToTeleport != null)
                        {
                            if (action.X.HasValue && action.Y.HasValue && action.Z.HasValue)
                            {
                                playerToTeleport.Position = new UnityEngine.Vector3(action.X.Value, action.Y.Value, action.Z.Value);
                                if (MainPlugin.Singleton?.Config.Debug == true)
                                    Log.Info($"Player {action.TargetPlayer} teleported to ({action.X}, {action.Y}, {action.Z}).");
                                EventCollector.AddPlayerEvent(
                                    "Teleport",
                                    action.TargetPlayer,
                                    $"Coordinates: ({action.X}, {action.Y}, {action.Z})"
                                );
                            }
                            else
                            {
                                if (MainPlugin.Singleton?.Config.Debug == true)
                                    Log.Warn("Coordinates are missing or invalid for TeleportToCoordinates action.");
                            }
                        }
                        else
                        {
                            if (MainPlugin.Singleton?.Config.Debug == true)
                                Log.Warn($"Player {action.TargetPlayer} not found for TeleportToCoordinates.");
                        }
                        break;

                    case "TeleportToPlayer":
                        var playerToMove = Player.Get(action.TargetPlayer);
                        var destinationPlayer = Player.Get(action.DestinationPlayer);
                        if (playerToMove != null && destinationPlayer != null)
                        {
                            playerToMove.Position = destinationPlayer.Position;
                            if (MainPlugin.Singleton?.Config.Debug == true)
                                Log.Info($"Player {action.TargetPlayer} teleported to {action.DestinationPlayer}.");
                            EventCollector.AddPlayerEvent(
                                "Teleport",
                                action.TargetPlayer,
                                $"To Player: {action.DestinationPlayer}"
                            );
                        }
                        else
                        {
                            if (playerToMove == null && MainPlugin.Singleton?.Config.Debug == true)
                                Log.Warn($"Player {action.TargetPlayer} not found for TeleportToPlayer.");
                            if (destinationPlayer == null && MainPlugin.Singleton?.Config.Debug == true)
                                Log.Warn($"Destination Player {action.DestinationPlayer} not found for TeleportToPlayer.");
                        }
                        break;

                    default:
                        if (MainPlugin.Singleton?.Config.Debug == true)
                            Log.Warn($"Unknown command: {action.Command}");
                        break;
                }
            }
        }
    }
}
