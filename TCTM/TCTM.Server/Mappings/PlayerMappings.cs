using TCTM.Server.DataModel;
using TCTM.Server.Dto;

namespace TCTM.Server.Mappings;

public static class PlayerMappings
{
    public static PlayerDto ToDto(this Player player)
    {
        return new PlayerDto(
            player.Id,
            player.DisplayName,
            player.Seed
        );
    }
}
