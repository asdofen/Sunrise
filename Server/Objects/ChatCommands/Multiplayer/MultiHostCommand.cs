using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Objects.ChatCommands.Multiplayer;

[ChatCommand("host", "mp", isGlobal: true)]
public class MultiHostCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (channel == null || session.Match == null)
        {
            throw new InvalidOperationException("Multiplayer command was called without being in a multiplayer room.");
        }

        if (session.Match.HasHostPrivileges(session) == false)
        {
            session.SendChannelMessage(channel.Name, "This command can only be used by the host of the room.");
            return Task.CompletedTask;
        }

        if (args == null || args.Length == 0)
        {
            session.SendChannelMessage(channel.Name, "Usage: !mp host <username>");
            return Task.CompletedTask;
        }

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();
        var targetSession = sessions.GetSession(username: args[0]);

        if (targetSession == null)
        {
            session.SendChannelMessage(channel.Name, "User not found.");
            return Task.CompletedTask;
        }

        var targetSlot = session.Match.Slots.FirstOrDefault(x => x.Value.UserId == targetSession.User.Id);

        if (targetSlot.Value == null)
        {
            session.SendChannelMessage(channel.Name, "User is not in the room.");
            return Task.CompletedTask;
        }

        session.Match.TransferHost(targetSlot.Key);

        session.SendChannelMessage(channel.Name, $"Host has been transferred to {args[0]}.");

        return Task.CompletedTask;
    }
}