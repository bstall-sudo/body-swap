using System.Collections.Generic;
using AppV2.Runtime.Scripts.Dialogue.Persistence;

namespace AppV2.Runtime.Scripts.Config
{
    /// <summary>Only differences from the immutable source session are stored.</summary>
    public static class NpcRoleOverrides
    {
        public static int SeatedHeight(ConversationRoleMeta source) => source.HeightOfSeatedRoleCm > 0
            ? source.HeightOfSeatedRoleCm : (int)(source.HeightOfRoleCm / 1.35f);

        public static List<AvatarOverride> Copy(List<AvatarOverride> values)
        {
            var result = new List<AvatarOverride>();
            if (values != null)
                foreach (var v in values)
                    if (v != null) result.Add(new AvatarOverride { RoleId = v.RoleId, AvatarId = v.AvatarId,
                        HeightOfRoleCm = v.HeightOfRoleCm, HeightOfSeatedRoleCm = v.HeightOfSeatedRoleCm });
            return result;
        }

        public static void Set(NpcGroupSettings group, ConversationRoleMeta source,
            string avatar, int height, int seated)
        {
            group.AvatarOverrides ??= new List<AvatarOverride>();
            group.AvatarOverrides.RemoveAll(v => v == null || v.RoleId == source.RoleId);
            var value = new AvatarOverride { RoleId = source.RoleId,
                AvatarId = avatar == source.AvatarId ? null : avatar,
                HeightOfRoleCm = height == source.HeightOfRoleCm ? 0 : height,
                HeightOfSeatedRoleCm = seated == SeatedHeight(source) ? 0 : seated };
            if (!string.IsNullOrWhiteSpace(value.AvatarId) || value.HeightOfRoleCm > 0 || value.HeightOfSeatedRoleCm > 0)
                group.AvatarOverrides.Add(value);
        }
    }
}
