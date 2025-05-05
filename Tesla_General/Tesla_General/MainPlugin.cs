using System;
using System.Timers;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using Exiled.Events.EventArgs.Warhead;
using Exiled.Events.EventArgs;
using Exiled.Events.Handlers;
using PlayerRoles;

// Дополнительные using для PromptGenerator / TeslaApiClient
using Tesla_General.Processing;
using Tesla_General.Networking;
using System.Collections.Generic;

namespace Tesla_General
{
    /// <summary>
    /// Основной класс плагина «Tesla_General».
    /// Собирает события, раз в 5 секунд отправляет их на лямбда-менеджер (если есть секретный ключ),
    /// получает команды, выполняет их.
    /// </summary>
    public class MainPlugin : Plugin<Config>
    {
        public static MainPlugin Singleton { get; private set; }

        public override string Name => "Tesla_General";
        public override string Author => "YourName";
        public override Version Version => new Version(1, 0, 0);

        private Timer dataSendTimer;

        // Запоминаем время последнего спавна MTF и Chaos
        private static DateTime lastMtfSpawnTime = DateTime.MinValue;
        private static DateTime lastChaosSpawnTime = DateTime.MinValue;

        // Время начала/конца раунда
        private static DateTime roundStartTime;
        private static DateTime? roundEndTime;

        public static DateTime RoundStartTime => roundStartTime;
        public static DateTime? RoundEndTime => roundEndTime;

        public override void OnEnabled()
        {
            Singleton = this;

            if (Config.Debug)
                Log.Info("[MainPlugin] Tesla_General plugin enabled (Debug mode).");

            EventCollector.AddSystemEvent("Plugin enabled");

            // Запускаем таймер каждые 5 секунд
            dataSendTimer = new Timer(5000);
            dataSendTimer.Elapsed += OnDataSendTimerElapsed;
            dataSendTimer.AutoReset = true;
            dataSendTimer.Start();

            if (Config.Debug)
                Log.Info("[MainPlugin] Data sending timer started (5s).");

            // Подписки на события
            // -----------------------------------------------
            Exiled.Events.Handlers.Player.Hurting += OnPlayerHurting;
            Exiled.Events.Handlers.Player.PickingUpItem += OnItemPickedUp;
            Exiled.Events.Handlers.Player.DroppingItem += OnItemDropped;

            Exiled.Events.Handlers.Player.Verified += OnPlayerVerified;
            Exiled.Events.Handlers.Player.Left += OnPlayerLeave;

            Exiled.Events.Handlers.Server.RespawningTeam += OnRespawningTeam;
            Exiled.Events.Handlers.Warhead.Starting += OnWarheadStarting;
            Exiled.Events.Handlers.Warhead.Stopping += OnWarheadStopping;
            Exiled.Events.Handlers.Player.ChangingRole += OnRoleChange;
            Exiled.Events.Handlers.Player.Spawning += OnPlayerSpawning;

            Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
            Exiled.Events.Handlers.Server.RoundEnded += OnRoundEnded;
            Exiled.Events.Handlers.Server.RestartingRound += OnServerRestarting;

            // Смерти (Dying)
            Exiled.Events.Handlers.Player.Dying += OnTeamKillDeath;
            Exiled.Events.Handlers.Player.Dying += OnCuffKillDeath;
            Exiled.Events.Handlers.Player.Dying += OnNormalDeath;
            Exiled.Events.Handlers.Player.Dying += OnSuicide;

            // Бан/Разбан/Кик/Мут/Размут
            Exiled.Events.Handlers.Player.Banned += OnBanned;
            Exiled.Events.Handlers.Player.Kicking += OnKicking;
            Exiled.Events.Handlers.Player.IssuingMute += OnIssuingMute;
            Exiled.Events.Handlers.Player.RevokingMute += OnIssuingUnmute;

            // Локальные репорты
            Exiled.Events.Handlers.Server.LocalReporting += OnLocalReporting;

            // Наручники
            Exiled.Events.Handlers.Player.Handcuffing += OnCuffing;
            Exiled.Events.Handlers.Player.RemovedHandcuffs += OnUncuffed;

            // Анонсы хаоса и НТФ
            Exiled.Events.Handlers.Map.AnnouncingChaosEntrance += OnChaosEnter;
            Exiled.Events.Handlers.Map.AnnouncingNtfEntrance += OnNtfEnter;

            Exiled.Events.Handlers.Player.SendingAdminChatMessage += OnSendingAdminChatMessage;

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            dataSendTimer?.Stop();
            dataSendTimer?.Dispose();

            // Отписки
            // -----------------------------------------------
            Exiled.Events.Handlers.Player.Hurting -= OnPlayerHurting;
            Exiled.Events.Handlers.Player.PickingUpItem -= OnItemPickedUp;
            Exiled.Events.Handlers.Player.DroppingItem -= OnItemDropped;
            Exiled.Events.Handlers.Player.Verified -= OnPlayerVerified;
            Exiled.Events.Handlers.Player.Left -= OnPlayerLeave;

            Exiled.Events.Handlers.Server.RespawningTeam -= OnRespawningTeam;
            Exiled.Events.Handlers.Warhead.Starting -= OnWarheadStarting;
            Exiled.Events.Handlers.Warhead.Stopping -= OnWarheadStopping;
            Exiled.Events.Handlers.Player.ChangingRole -= OnRoleChange;
            Exiled.Events.Handlers.Player.Spawning -= OnPlayerSpawning;

            Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
            Exiled.Events.Handlers.Server.RoundEnded -= OnRoundEnded;
            Exiled.Events.Handlers.Server.RestartingRound -= OnServerRestarting;

            // Dying
            Exiled.Events.Handlers.Player.Dying -= OnTeamKillDeath;
            Exiled.Events.Handlers.Player.Dying -= OnCuffKillDeath;
            Exiled.Events.Handlers.Player.Dying -= OnNormalDeath;
            Exiled.Events.Handlers.Player.Dying -= OnSuicide;

            // Бан/Разбан/Кик/Мут/Размут
            Exiled.Events.Handlers.Player.Banned -= OnBanned;
            Exiled.Events.Handlers.Player.Kicking -= OnKicking;
            Exiled.Events.Handlers.Player.IssuingMute -= OnIssuingMute;
            Exiled.Events.Handlers.Player.RevokingMute -= OnIssuingUnmute;

            // Локальные репорты
            Exiled.Events.Handlers.Server.LocalReporting -= OnLocalReporting;

            // Наручники
            Exiled.Events.Handlers.Player.Handcuffing -= OnCuffing;
            Exiled.Events.Handlers.Player.RemovedHandcuffs -= OnUncuffed;

            // Хаос/НТФ анонсы
            Exiled.Events.Handlers.Map.AnnouncingChaosEntrance -= OnChaosEnter;
            Exiled.Events.Handlers.Map.AnnouncingNtfEntrance -= OnNtfEnter;

            Exiled.Events.Handlers.Player.SendingAdminChatMessage -= OnSendingAdminChatMessage;

            Singleton = null;

            base.OnDisabled();
        }

        private void OnDataSendTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // Если плагин выключен или нет секретного ключа — не отправляем
            if (!Config.IsEnabled || string.IsNullOrWhiteSpace(Config.SecretKey))
                return;

            if (!EventCollector.HasEvents())
            {
                if (Config.Debug)
                    Log.Info("[MainPlugin] No events to send at the moment.");
                return;
            }

            // Подсчитываем время с последнего спавна
            TimeSpan? timeSinceLastMtf = null;
            if (lastMtfSpawnTime != DateTime.MinValue)
                timeSinceLastMtf = DateTime.UtcNow - lastMtfSpawnTime;

            TimeSpan? timeSinceLastChaos = null;
            if (lastChaosSpawnTime != DateTime.MinValue)
                timeSinceLastChaos = DateTime.UtcNow - lastChaosSpawnTime;

            // Генерируем JSON
            var json = PromptGenerator.GenerateDataJson(timeSinceLastMtf, timeSinceLastChaos);

            if (!string.IsNullOrEmpty(json))
            {
                _ = TeslaApiClient.SendEventsData(json);
                EventCollector.ClearEvents();
            }
        }

        // -------------------- События --------------------

        private void OnPlayerHurting(Exiled.Events.EventArgs.Player.HurtingEventArgs ev)
        {
            if (Config.Debug)
                Log.Info($"[OnPlayerHurting] {ev.Attacker?.Nickname ?? "Unknown"} attacked {ev.Player.Nickname} for {ev.Amount} damage.");

            // Сформируем подробности
            var data = new Dictionary<string, string>
            {
                ["DamageAmount"] = ev.Amount.ToString("F1"),
                ["AttackerName"] = ev.Attacker?.Nickname ?? "Unknown",
                ["AttackerRole"] = ev.Attacker?.Role.Name ?? "None",
                ["AttackerTeam"] = ev.Attacker?.Role.Team.ToString() ?? "None",
                ["AttackerUserId"] = ev.Attacker?.UserId ?? "Unknown",
                ["AttackerIp"] = ev.Attacker?.IPAddress ?? "Unknown",
                ["VictimName"] = ev.Player.Nickname,
                ["VictimRole"] = ev.Player.Role.Name,
                ["VictimTeam"] = ev.Player.Role.Team.ToString(),
                ["VictimUserId"] = ev.Player.UserId,
                ["VictimIp"] = ev.Player.IPAddress
            };

            // Если после удара жизней <= 0, это будет Kill
            float newHealth = ev.Player.Health - ev.Amount;
            if (newHealth <= 0)
                EventCollector.AddPlayerEvent("Kill", ev.Attacker?.Nickname ?? "Unknown", ev.Player.Nickname, data);
            else
                EventCollector.AddPlayerEvent("Hurt", ev.Attacker?.Nickname ?? "Unknown", ev.Player.Nickname, data);
        }

        private void OnItemPickedUp(Exiled.Events.EventArgs.Player.PickingUpItemEventArgs ev)
        {
            var data = new Dictionary<string, string>
            {
                ["Item"] = ev.Pickup.Type.ToString(),
                ["PlayerUserId"] = ev.Player.UserId,
                ["PlayerIp"] = ev.Player.IPAddress,
                ["PlayerRole"] = ev.Player.Role.Name,
                ["PlayerTeam"] = ev.Player.Role.Team.ToString()
            };

            if (Config.Debug)
                Log.Info($"[OnItemPickedUp] {ev.Player.Nickname} picked up {ev.Pickup.Type}");

            EventCollector.AddPlayerEvent("PickupItem", ev.Player.Nickname, ev.Pickup.Type.ToString(), data);
        }

        private void OnItemDropped(Exiled.Events.EventArgs.Player.DroppingItemEventArgs ev)
        {
            var data = new Dictionary<string, string>
            {
                ["Item"] = ev.Item.Type.ToString(),
                ["PlayerUserId"] = ev.Player.UserId,
                ["PlayerIp"] = ev.Player.IPAddress,
                ["PlayerRole"] = ev.Player.Role.Name,
                ["PlayerTeam"] = ev.Player.Role.Team.ToString()
            };

            if (Config.Debug)
                Log.Info($"[OnItemDropped] {ev.Player.Nickname} dropped {ev.Item.Type}");

            EventCollector.AddPlayerEvent("DropItem", ev.Player.Nickname, ev.Item.Type.ToString(), data);
        }

        private void OnPlayerVerified(VerifiedEventArgs ev)
        {
            if (Config.Debug)
                Log.Info($"[OnPlayerVerified] {ev.Player.Nickname} connected.");

            var data = new Dictionary<string, string>
            {
                ["UserId"] = ev.Player.UserId,
                ["Ip"] = ev.Player.IPAddress,
                ["Role"] = ev.Player.Role.Name,
                ["Team"] = ev.Player.Role.Team.ToString()
            };

            EventCollector.AddPlayerEvent("Connect", ev.Player.Nickname, null, data);
        }

        private void OnPlayerLeave(LeftEventArgs ev)
        {
            if (Config.Debug)
                Log.Info($"[OnPlayerLeft] {ev.Player.Nickname} disconnected.");

            var data = new Dictionary<string, string>
            {
                ["UserId"] = ev.Player.UserId,
                ["Ip"] = ev.Player.IPAddress,
                ["Role"] = ev.Player.Role.Name,
                ["Team"] = ev.Player.Role.Team.ToString()
            };

            EventCollector.AddPlayerEvent("Disconnect", ev.Player.Nickname, null, data);
        }

        private void OnRespawningTeam(RespawningTeamEventArgs ev)
        {
            string teamName = ev.NextKnownTeam == Faction.FoundationStaff ? "MTF" : "ChaosInsurgency";

            if (Config.Debug)
                Log.Info($"[OnRespawningTeam] {teamName} respawned.");

            if (ev.NextKnownTeam == Faction.FoundationStaff)
                lastMtfSpawnTime = DateTime.UtcNow;
            else if (ev.NextKnownTeam == Faction.FoundationEnemy)
                lastChaosSpawnTime = DateTime.UtcNow;

            EventCollector.AddSystemEvent($"{teamName} respawned.");
        }

        private void OnWarheadStarting(StartingEventArgs ev)
        {
            if (Config.Debug)
                Log.Info("[OnWarheadStarting] Warhead starting...");

            EventCollector.AddSystemEvent("Warhead activation started.");
        }

        private void OnWarheadStopping(StoppingEventArgs ev)
        {
            if (Config.Debug)
                Log.Info("[OnWarheadStopping] Warhead stopping...");

            EventCollector.AddSystemEvent("Warhead activation stopped.");
        }

        private void OnRoleChange(Exiled.Events.EventArgs.Player.ChangingRoleEventArgs ev)
        {
            if (Config.Debug)
                Log.Info($"[OnRoleChange] {ev.Player.Nickname} changes role to {ev.NewRole}");

            var data = new Dictionary<string, string>
            {
                ["OldRole"] = ev.Player.Role.Name,
                ["NewRole"] = ev.NewRole.ToString(),
                ["UserId"] = ev.Player.UserId,
                ["Ip"] = ev.Player.IPAddress
            };

            EventCollector.AddPlayerEvent("RoleChange", ev.Player.Nickname, $"To {ev.NewRole}", data);
        }

        private void OnPlayerSpawning(Exiled.Events.EventArgs.Player.SpawningEventArgs ev)
        {
            if (Config.Debug)
                Log.Info($"[OnPlayerSpawning] {ev.Player.Nickname} spawns as {ev.Player.Role}");

            var data = new Dictionary<string, string>
            {
                ["NewRole"] = ev.Player.Role.Name,
                ["UserId"] = ev.Player.UserId,
                ["Ip"] = ev.Player.IPAddress
            };

            EventCollector.AddPlayerEvent("Spawn", ev.Player.Nickname, $"Role: {ev.Player.Role}", data);
        }

        private void OnWaitingForPlayers()
        {
            if (Config.Debug)
                Log.Info("[OnWaitingForPlayers] Waiting for players...");

            EventCollector.AddSystemEvent("Waiting for players");
        }

        private void OnRoundStarted()
        {
            if (Config.Debug)
                Log.Info("[OnRoundStarted] Round started!");

            roundStartTime = DateTime.UtcNow;
            roundEndTime = null;

            EventCollector.AddSystemEvent("Round started");
        }

        private void OnRoundEnded(RoundEndedEventArgs ev)
        {
            if (Config.Debug)
                Log.Info($"[OnRoundEnded] Round ended. LeadingTeam: {ev.LeadingTeam}");

            EventCollector.AddSystemEvent($"Round ended. LeadingTeam: {ev.LeadingTeam}");
            roundEndTime = DateTime.UtcNow;

            // Принудительно сделаем «финальную» отправку
            ForceSendEventsNow();
        }

        private void OnServerRestarting()
        {
            if (Config.Debug)
                Log.Info("[OnServerRestarting] Server restarting...");

            EventCollector.AddSystemEvent("Server restarting");
        }

        /// <summary>
        /// Вызвать отправку данных «прямо сейчас» (например, при окончании раунда).
        /// </summary>
        private void ForceSendEventsNow()
        {
            if (!Config.IsEnabled || string.IsNullOrWhiteSpace(Config.SecretKey))
                return;

            if (!EventCollector.HasEvents())
                return;

            // То же самое, что и в OnDataSendTimerElapsed
            TimeSpan? timeSinceLastMtf = null;
            if (lastMtfSpawnTime != DateTime.MinValue)
                timeSinceLastMtf = DateTime.UtcNow - lastMtfSpawnTime;

            TimeSpan? timeSinceLastChaos = null;
            if (lastChaosSpawnTime != DateTime.MinValue)
                timeSinceLastChaos = DateTime.UtcNow - lastChaosSpawnTime;

            var json = PromptGenerator.GenerateDataJson(timeSinceLastMtf, timeSinceLastChaos);
            if (!string.IsNullOrEmpty(json))
            {
                _ = TeslaApiClient.SendEventsData(json);
                EventCollector.ClearEvents();
            }
        }

        // =======================================================================
        //        Ниже блоки для событий смерти (Dying) и модерации
        // =======================================================================

        // ---------- Смерти (Dying) ----------

        private void OnTeamKillDeath(Exiled.Events.EventArgs.Player.DyingEventArgs ev)
        {
            // ev.Killer - атакующий, ev.Player - жертва
            if (ev.Attacker == null) return;
            if (ev.Attacker == ev.Player) return; // самоубийство - пропускаем здесь
            if (ev.Attacker.Role.Team == ev.Player.Role.Team) // значит teamkill
            {
                var data = new Dictionary<string, string>
                {
                    ["KillerName"] = ev.Attacker.Nickname,
                    ["KillerRole"] = ev.Attacker.Role.Name,
                    ["KillerTeam"] = ev.Attacker.Role.Team.ToString(),
                    ["KillerUserId"] = ev.Attacker.UserId,
                    ["KillerIp"] = ev.Attacker.IPAddress,

                    ["VictimName"] = ev.Player.Nickname,
                    ["VictimRole"] = ev.Player.Role.Name,
                    ["VictimTeam"] = ev.Player.Role.Team.ToString(),
                    ["VictimUserId"] = ev.Player.UserId,
                    ["VictimIp"] = ev.Player.IPAddress
                };

                EventCollector.AddPlayerEvent("TeamKill", ev.Attacker.Nickname, ev.Player.Nickname, data);
            }
        }

        private void OnCuffKillDeath(Exiled.Events.EventArgs.Player.DyingEventArgs ev)
        {
            // Если жертва в наручниках => "CuffKill"
            if (ev.Player.IsCuffed && ev.Attacker != null && ev.Attacker != ev.Player)
            {
                var data = new Dictionary<string, string>
                {
                    ["KillerName"] = ev.Attacker.Nickname,
                    ["KillerRole"] = ev.Attacker.Role.Name,
                    ["KillerTeam"] = ev.Attacker.Role.Team.ToString(),
                    ["KillerUserId"] = ev.Attacker.UserId,
                    ["KillerIp"] = ev.Attacker.IPAddress,

                    ["VictimName"] = ev.Player.Nickname,
                    ["VictimRole"] = ev.Player.Role.Name,
                    ["VictimTeam"] = ev.Player.Role.Team.ToString(),
                    ["VictimUserId"] = ev.Player.UserId,
                    ["VictimIp"] = ev.Player.IPAddress,
                    ["WasCuffed"] = "true"
                };

                EventCollector.AddPlayerEvent("CuffKill", ev.Attacker.Nickname, ev.Player.Nickname, data);
            }
        }

        private void OnNormalDeath(Exiled.Events.EventArgs.Player.DyingEventArgs ev)
        {
            // Если атакер и цель разные и НЕ в одной команде => обычное убийство
            if (ev.Attacker != null && ev.Attacker != ev.Player &&
                ev.Attacker.Role.Team != ev.Player.Role.Team && !ev.Player.IsCuffed)
            {
                var data = new Dictionary<string, string>
                {
                    ["KillerName"] = ev.Attacker.Nickname,
                    ["KillerRole"] = ev.Attacker.Role.Name,
                    ["KillerTeam"] = ev.Attacker.Role.Team.ToString(),
                    ["KillerUserId"] = ev.Attacker.UserId,
                    ["KillerIp"] = ev.Attacker.IPAddress,

                    ["VictimName"] = ev.Player.Nickname,
                    ["VictimRole"] = ev.Player.Role.Name,
                    ["VictimTeam"] = ev.Player.Role.Team.ToString(),
                    ["VictimUserId"] = ev.Player.UserId,
                    ["VictimIp"] = ev.Player.IPAddress
                };

                EventCollector.AddPlayerEvent("Kill", ev.Attacker.Nickname, ev.Player.Nickname, data);
            }
        }

        private void OnSuicide(Exiled.Events.EventArgs.Player.DyingEventArgs ev)
        {
            // Если атакер == жертва => "Suicide"
            if (ev.Attacker == ev.Player && ev.Player != null)
            {
                var data = new Dictionary<string, string>
                {
                    ["SuiciderName"] = ev.Player.Nickname,
                    ["SuiciderRole"] = ev.Player.Role.Name,
                    ["SuiciderTeam"] = ev.Player.Role.Team.ToString(),
                    ["SuiciderUserId"] = ev.Player.UserId,
                    ["SuiciderIp"] = ev.Player.IPAddress
                };

                EventCollector.AddPlayerEvent("Suicide", ev.Player.Nickname, null, data);
            }
        }

        // ---------- Бан/разбан/кик/мут/размут ----------

        private void OnBanned(Exiled.Events.EventArgs.Player.BannedEventArgs ev)
        {
            var d = ev.Details;

            var extra = new Dictionary<string, string>
            {
                ["BannedName"] = d.OriginalName,
                ["BannedId"] = d.Id,
                ["BanReason"] = d.Reason ?? "No reason",
                ["BanIssuedAt"] = d.IssuanceTime.ToString("o"),
                ["BanExpiresAt"] = d.Expires.ToString("o"),

            };

            // "action", "staffOrSystem", "targetPlayer", reason, extraData
            // Обратите внимание, что "targetPlayer" тут логичнее = OriginalName/Id
            EventCollector.AddModerationEvent("Ban", d.OriginalName, d.Reason);
        }


        private void OnKicking(Exiled.Events.EventArgs.Player.KickingEventArgs ev)
        {
            // ev.Player => кто кикает (админ?), ev.Target => кого
            if (!ev.IsAllowed) return;

            var issuerName = ev.Player?.Nickname ?? "Server";
            var issuerId = ev.Player?.UserId ?? "Server";
            var issuerIp = ev.Player?.IPAddress ?? "Unknown";

            var targetName = ev.Target?.Nickname ?? "Unknown";
            var targetId = ev.Target?.UserId ?? "Unknown";
            var targetIp = ev.Target?.IPAddress ?? "Unknown";

            var extra = new Dictionary<string, string>
            {
                ["IssuerName"] = issuerName,
                ["IssuerId"] = issuerId,
                ["IssuerIp"] = issuerIp,

                ["KickedPlayer"] = targetName,
                ["KickedId"] = targetId,
                ["KickedIp"] = targetIp
            };

            EventCollector.AddModerationEvent("Kick", issuerName, targetName, ev.Reason, extra);
        }

        private void OnIssuingMute(Exiled.Events.EventArgs.Player.IssuingMuteEventArgs ev)
        {
            if (!ev.IsAllowed) return;

            // ev.Player => кто мьютит (админ), ev.Target => кого, ev.Reason, ev.Duration
            var issuerName = ev.Player?.Nickname ?? "Server";
            var issuerId = ev.Player?.UserId ?? "Server";
            var issuerIp = ev.Player?.IPAddress ?? "Unknown";

            var targetName = ev.Player?.Nickname ?? "Unknown";
            var targetId = ev.Player?.UserId ?? "Unknown";
            var targetIp = ev.Player?.IPAddress ?? "Unknown";

            var extra = new Dictionary<string, string>
            {
                ["IssuerName"] = issuerName,
                ["IssuerId"] = issuerId,
                ["IssuerIp"] = issuerIp,

                ["MutedPlayer"] = targetName,
                ["MutedId"] = targetId,
                ["MutedIp"] = targetIp,

            };

            EventCollector.AddModerationEvent("Mute", issuerName, targetName);
        }

        private void OnIssuingUnmute(Exiled.Events.EventArgs.Player.RevokingMuteEventArgs ev)
        {
            if (!ev.IsAllowed) return;

            // ev.Player => админ, ev.Target => кого размьютили
            var issuerName = ev.Player?.Nickname ?? "Server";
            var issuerId = ev.Player?.UserId ?? "Server";
            var issuerIp = ev.Player?.IPAddress ?? "Unknown";

            var targetName = ev.Player?.Nickname ?? "Unknown";
            var targetId = ev.Player?.UserId ?? "Unknown";
            var targetIp = ev.Player?.IPAddress ?? "Unknown";

            var extra = new Dictionary<string, string>
            {
                ["IssuerName"] = issuerName,
                ["IssuerId"] = issuerId,
                ["IssuerIp"] = issuerIp,

                ["UnmutedPlayer"] = targetName,
                ["UnmutedId"] = targetId,
                ["UnmutedIp"] = targetIp
            };

            EventCollector.AddModerationEvent("Unmute", issuerName, targetName, "Mute revoked", extra);
        }

        // ---------- Админ-чат (SendingRemoteAdminChatMessage) ----------

        private void OnSendingAdminChatMessage(Exiled.Events.EventArgs.Player.SendingAdminChatMessageEventsArgs ev)
        {
            if (!ev.IsAllowed) return;

            // ev.Sender => кто пишет, ev.Message => сообщение, ev.Channel => текстовый или голосовой
            var extra = new Dictionary<string, string>
            {
                ["SenderName"] = ev.Player.Nickname,
                ["SenderId"] = ev.Player.UserId,
                ["SenderIp"] = ev.Player.IPAddress,
                ["Message"] = ev.Message
            };

            EventCollector.AddModerationEvent("AdminChat", ev.Player.Nickname, null, $"Msg={ev.Message}", extra);
        }

        // ---------- Локальные репорты ----------

        private void OnLocalReporting(Exiled.Events.EventArgs.Server.LocalReportingEventArgs ev)
        {
            // ev.IsAllowed, ev.Player (кто репортит), ev.Target (кого), ev.Reason
            if (!ev.IsAllowed) return;

            var reporterName = ev.Player?.Nickname ?? "Unknown";
            var reporterId = ev.Player?.UserId ?? "Unknown";
            var reporterIp = ev.Player?.IPAddress ?? "Unknown";

            var reportedName = ev.Target?.Nickname ?? "Unknown";
            var reportedId = ev.Target?.UserId ?? "Unknown";
            var reportedIp = ev.Target?.IPAddress ?? "Unknown";

            var extra = new Dictionary<string, string>
            {
                ["ReporterName"] = reporterName,
                ["ReporterId"] = reporterId,
                ["ReporterIp"] = reporterIp,

                ["ReportedName"] = reportedName,
                ["ReportedId"] = reportedId,
                ["ReportedIp"] = reportedIp,

                ["ReportReason"] = ev.Reason ?? "No reason"
            };

            EventCollector.AddModerationEvent("LocalReport", reporterName, reportedName, ev.Reason, extra);
        }

        // ---------- Наручники ----------

        private void OnCuffing(Exiled.Events.EventArgs.Player.HandcuffingEventArgs ev)
        {
            // ev.Player => тот, кто одевает наручники, ev.Target => кому
            var data = new Dictionary<string, string>
            {
                ["CufferName"] = ev.Player.Nickname,
                ["CufferId"] = ev.Player.UserId,
                ["CufferIp"] = ev.Player.IPAddress,
                ["CufferRole"] = ev.Player.Role.Name,
                ["CufferTeam"] = ev.Player.Role.Team.ToString(),

                ["TargetName"] = ev.Target.Nickname,
                ["TargetId"] = ev.Target.UserId,
                ["TargetIp"] = ev.Target.IPAddress,
                ["TargetRole"] = ev.Target.Role.Name,
                ["TargetTeam"] = ev.Target.Role.Team.ToString()
            };

            EventCollector.AddPlayerEvent("Cuff", ev.Player.Nickname, ev.Target.Nickname, data);
        }

        private void OnUncuffed(Exiled.Events.EventArgs.Player.RemovedHandcuffsEventArgs ev)
        {
            // ev.Player => кто снимает, ev.Target => с кого снимают
            var data = new Dictionary<string, string>
            {
                ["UncufferName"] = ev.Player.Nickname,
                ["UncufferId"] = ev.Player.UserId,
                ["UncufferIp"] = ev.Player.IPAddress,
                ["UncufferRole"] = ev.Player.Role.Name,
                ["UncufferTeam"] = ev.Player.Role.Team.ToString(),

                ["TargetName"] = ev.Target.Nickname,
                ["TargetId"] = ev.Target.UserId,
                ["TargetIp"] = ev.Target.IPAddress,
                ["TargetRole"] = ev.Target.Role.Name,
                ["TargetTeam"] = ev.Target.Role.Team.ToString()
            };

            EventCollector.AddPlayerEvent("Uncuff", ev.Player.Nickname, ev.Target.Nickname, data);
        }

        // ---------- Анонсы входа Хаоса / НТФ ----------

        private void OnChaosEnter(AnnouncingChaosEntranceEventArgs ev)
        {
            // System event (можно указать число заспавнившихся, но Exiled даёт только факт + кол-во?)
            EventCollector.AddSystemEvent("Chaos is entering the facility");
        }

        private void OnNtfEnter(AnnouncingNtfEntranceEventArgs ev)
        {
            // Ev содержит UnitName, UnitNumber, UnitEnding...
            var msg = $"NTF is entering the facility (UnitName={ev.UnitName}, UnitNumber={ev.UnitNumber}, UnitEnding={ev.Wave})";
            EventCollector.AddSystemEvent(msg);
        }
    }
}

