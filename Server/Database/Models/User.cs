﻿using osu.Shared;
using Sunrise.Server.Types.Enums;
using Watson.ORM.Core;

namespace Sunrise.Server.Database.Models;

[Table("user")]
public class User
{
    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string Username { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string Email { get; set; }

    [Column(DataTypes.Nvarchar, 64, false)]
    public string Passhash { get; set; }

    [Column(DataTypes.Int, false)]
    public short Country { get; set; }

    [Column(DataTypes.Int, false)]
    public UserPrivileges Privilege { get; set; }

    [Column(DataTypes.DateTime, false)]
    public DateTime RegisterDate { get; set; } = DateTime.UtcNow;

    [Column(DataTypes.Nvarchar, 1024, false)]
    public string Friends { get; set; } = string.Empty;

    [Column(DataTypes.Boolean, false)]
    public bool IsRestricted { get; set; } = false;

    [Column(DataTypes.DateTime, false)]
    public DateTime SilencedUntil { get; set; } = DateTime.MinValue;

    public List<int> FriendsList => Friends.Split(',')
        .Where(x => !string.IsNullOrEmpty(x))
        .Select(int.Parse)
        .ToList();

    public void AddFriend(int friendId)
    {
        if (FriendsList.Contains(friendId))
            return;

        Friends += $",{friendId}";
    }

    public void RemoveFriend(int friendId)
    {
        if (!FriendsList.Contains(friendId))
            return;

        Friends = string.Join(',', FriendsList.Where(x => x != friendId));
    }

    public PlayerRank GetPrivilegeRank()
    {
        var privilegeRank = PlayerRank.Default;

        if (Privilege.HasFlag(UserPrivileges.Developer))
        {
            privilegeRank |= PlayerRank.SuperMod;
        }

        if (Privilege.HasFlag(UserPrivileges.Admin) || Privilege.HasFlag(UserPrivileges.Bat))
        {
            privilegeRank |= PlayerRank.Bat;
        }

        if (Privilege.HasFlag(UserPrivileges.Supporter))
        {
            privilegeRank |= PlayerRank.Supporter;
        }

        return privilegeRank;
    }
}